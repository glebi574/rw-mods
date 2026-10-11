using BepInEx;
using BepInEx.Preloader.Patching;
using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.Other;
using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace gelbi_silly_lib;

/// <summary>
/// Class with some assembly utilities. Mainly stores dynamic patches
/// </summary>
public static class AssemblyUtils
{
  /// <summary>
  /// Names of <c>MonoBehaviour</c> methods invoked internally on plugin startup and shutdown
  /// </summary>
  public static readonly HashSet<string> pluginInitMethods = ["Awake", "OnEnable", "Reset", "Start"];
  /// <summary>
  /// Patches applied to all loaded assemblies
  /// </summary>
  public static List<Action<AssemblyDefinition>> universalPatches = [];
  /// <summary>
  /// Patches applied to assemblies with specified name
  /// </summary>
  public static Dictionary<string, List<Action<AssemblyDefinition>>> assemblyPatches = [];
  /// <summary>
  /// Assembly name - location pairs
  /// </summary>
  public static ConcurrentDictionary<string, string> locations = [];
  /// <summary>
  /// List of currently loaded assemblies
  /// </summary>
  public static List<Assembly> loadedAssemblies = [];
  /// <summary>
  /// Assembly hash cache for comparisons
  /// </summary>
  public static Dictionary<int, Assembly> assemblyHashes = [];
  /// <summary>
  /// Assembly name and full name cache for comparisons
  /// </summary>
  public static Dictionary<string, List<Assembly>> assemblyNames = [], assemblyFullNames = [];

  static AssemblyUtils()
  {
    new Hook(typeof(Assembly).GetMethod("LoadFile", BFlags.anyDeclaredStatic, null, [typeof(string)], null), Assembly_LoadFile);
    new Hook(typeof(ModuleDefinition).GetMethod("ReadModule", BFlags.anyDeclaredStatic, null, [typeof(string)], null), AssemblyDefinition_ReadModule);
    new Hook(typeof(AssemblyNameReference).GetProperty("Name").GetSetMethod(), AssemblyNameReference_setName);
    new Hook(typeof(Assembly).GetProperty("Location").GetGetMethod(), Assembly_getLocation);
    new ILHook(typeof(BepInEx.Preloader.EnvVars).Assembly.GetType("BepInEx.Preloader.RuntimeFixes.HarmonyInteropFix").GetMethod("OnAssemblyLoad", BFlags.anyDeclaredStatic), HarmonyInteropFix_OnAssemblyLoad);

    foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
      AddLoadedAssembly(asm);
    AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
  }

  static void AddLoadedAssembly(Assembly asm)
  {
    loadedAssemblies.Add(asm);
    assemblyHashes[asm.GetHashCode()] = asm;
    assemblyNames.AddOrCreateWith(asm.GetName().Name, asm);
    assemblyNames.AddOrCreateWith(asm.GetName().FullName, asm);
  }

  static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args) => AddLoadedAssembly(args.LoadedAssembly);

  /// <summary>
  /// Adds patch which would be applied to all loaded assemblies
  /// </summary>
  public static void AddUniversalAssemblyPatch(Action<AssemblyDefinition> patch) => universalPatches.Add(patch);

  /// <summary>
  /// Adds patch which would be applied to assembly with specified filename when it loads
  /// </summary>
  public static void AddPatchForAssembly(string filename, Action<AssemblyDefinition> patch) => assemblyPatches.AddOrCreateWith(filename, patch);

  class WrapperContext(MethodBase method)
  {
    public MethodBase method = method;

    public void WrapAction(Action<object> orig, object self)
    {
      try
      {
        orig(self);
      }
      catch (Exception e)
      {
        LogError($"Exception was thrown while invoking {method.GetSimpleName()}: {e}");
        throw;
      }
    }
  }

  static Assembly WrapPluginInit(Assembly self)
  {
    if (!GSLSettings.instance.wrapPluginInit || self.GetName().Name == "HOOKS-Assembly-CSharp")
      return self;
    try
    {
      foreach (Type type in self.GetTypes())
      {
        if (type.GetCustomAttribute(typeof(BepInPlugin)) == null)
          continue;
        if (type.GetConstructor(BFlags.anyDeclaredStatic, null, Type.EmptyTypes, null) != null)
          try
          {
            type.RunClassConstructor();
          }
          catch (Exception e)
          {
            LogError($"Exception was thrown while invoking class constructor of type {type.GetSimpleNameWithNamespace()}: {e}");
          }
        if (type.GetConstructor(BFlags.anyDeclaredInstance, null, Type.EmptyTypes, null) is ConstructorInfo ctor)
          new Hook(ctor, new WrapperContext(ctor).WrapAction);
        foreach (string name in pluginInitMethods)
          if (type.GetMethod(name, BFlags.anyDeclaredInstance, null, Type.EmptyTypes, null) is MethodInfo method)
            new Hook(method, new WrapperContext(method).WrapAction);
      }
    }
    catch (Exception e)
    {
      LogError($"Plugin init wrapper failed: {e}");
    }
    return self;
  }

  static Assembly Assembly_LoadFile(Func<string, Assembly> orig, string path)
  {
    List<Action<AssemblyDefinition>> patches = null;
    string filename = Path.GetFileName(path);
    if (universalPatches.Count == 0 && !assemblyPatches.TryGetValue(filename, out patches))
      return WrapPluginInit(orig(path));

    AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(path);
    if (patches != null)
      foreach (Action<AssemblyDefinition> patch in patches)
        patch(asm);
    foreach (Action<AssemblyDefinition> patch in universalPatches)
      patch(asm);

    AssemblyPatcher.Load(asm, filename);
    return WrapPluginInit(AppDomain.CurrentDomain.GetAssemblies().Last());
  }

  static ModuleDefinition AssemblyDefinition_ReadModule(Func<string, ModuleDefinition> orig, string fileName)
  {
    locations[Path.GetFileNameWithoutExtension(fileName)] = fileName;
    return orig(fileName);
  }

  static void AssemblyNameReference_setName(Action<AssemblyNameReference, string> orig, AssemblyNameReference self, string value)
  {
    if (value != null && self.Name != null && locations.ContainsKey(self.Name))
      locations[value] = locations[self.Name];
    orig(self, value);
  }

  static string Assembly_getLocation(Func<Assembly, string> orig, Assembly self)
  {
    string location = orig(self);
    if (string.IsNullOrWhiteSpace(location) && locations.TryGetValue(self.GetName().Name, out string savedLocation)) // assemblies created dynamically except ones for plugins won't have location
      return savedLocation;
    return location;
  }

  // prevents some assemblies from being loaded twice
  static void HarmonyInteropFix_OnAssemblyLoad(ILContext il)
  {
    ILCursor c = new(il);

    static bool IsLoaded(byte[] result) => result != null;

    if (c.TryGotoNext(i => i.MatchPop()))
      c.Emit(OpCodes.Call, ((Delegate)IsLoaded).Method)
       .Emit(OpCodes.Dup);
    if (c.TryGotoNext(i => i.MatchRet()))
      c.Emit(OpCodes.Pop);
  }
}