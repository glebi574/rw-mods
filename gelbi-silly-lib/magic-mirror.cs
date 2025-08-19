using BepInEx;
using gelbi_silly_lib.BepInExUtils;
using gelbi_silly_lib.Other;
using gelbi_silly_lib.ReflectionUtils;
using gelbi_silly_lib.ReflectionValueUtils;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib.ReflectionValueUtils
{
  /// <summary>
  /// Extensions for Reflection, optimizing getting/setting/invoking
  /// <para>Separated into their own namespace for being annoying</para>
  /// </summary>
  public static class Extensions
  {
    /// <summary>
    /// Returns value of field in provided instance
    /// </summary>
    public static object GetFieldValue(this object self, string fieldName)
    {
      return self.GetType().GetField(fieldName, BFlags.any).GetValue(self);
    }

    /// <summary>
    /// Sets value of field in provided instance
    /// </summary>
    public static void SetFieldValue(this object self, string fieldName, object value)
    {
      self.GetType().GetField(fieldName, BFlags.any).SetValue(self, value);
    }

    /// <summary>
    /// Invokes method in provided instance
    /// </summary>
    public static object InvokeStaticMethod(this object self, string fieldName, params object[] args)
    {
      return self.GetType().GetMethod(fieldName, BFlags.any).Invoke(self, args);
    }

    /// <summary>
    /// Returns value of static field in provided type
    /// </summary>
    public static object GetStaticFieldValue(this Type self, string fieldName)
    {
      return self.GetField(fieldName, BFlags.any).GetValue(null);
    }

    /// <summary>
    /// Sets value of static field in provided type
    /// </summary>
    public static void SetStaticFieldValue(this Type self, string fieldName, object value)
    {
      self.GetField(fieldName, BFlags.any).SetValue(null, value);
    }

    /// <summary>
    /// Invokes static method in provided type
    /// </summary>
    public static object InvokeStaticMethod(this Type self, string fieldName, params object[] args)
    {
      return self.GetMethod(fieldName, BFlags.any).Invoke(null, args);
    }
  }
}

namespace gelbi_silly_lib.ReflectionUtils
{
  /// <summary>
  /// Extensions for Reflection
  /// </summary>
  public static class Extensions
  {
    /// <summary>
    /// Returns types, defined by assembly. May throw less, than default version
    /// </summary>
    public static IEnumerable<Type> GetTypesSafe(this Assembly self)
    {
      foreach (Module module in self.GetModules())
        foreach (Type type in module.GetTypes())
          yield return type;
    }

    /// <summary>
    /// Returns types, defined by assembly. May throw less, than default version
    /// </summary>
    public static FieldInfo[] GetFieldsSafe(this Type self, BindingFlags flags)
    {
      try
      {
        return self.GetFields(flags);
      } catch { }
      return new FieldInfo[0];
    }

    /// <summary>
    /// Calls <c>cctor</c> of provided type
    /// </summary>
    public static void RunClassConstructor(this Type type)
    {
      System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
    }

    /// <summary>
    /// Checks if type inherits other generic type
    /// </summary>
    public static bool InheritsGenericType(this Type self, Type other)
    {
      return self.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == other);
    }

    /// <summary>
    /// Returns type definition similar to how it'd be initially written in c# (doesn't append ref/out)
    /// </summary>
    public static string GetSimpleName(this Type type)
    {
      if (GSLUtils.baseTypeNameAtlas.TryGetValue(type, out string name))
        return name;
      if (type.IsArray)
        return $"{type.GetElementType().GetSimpleName()}[]";
      if (!type.IsGenericType)
        return type.FullName;
      if (type.FullName == null)
        return type.ToString();
      if (type.FullName.StartsWith("System.Nullable"))
        return $"{type.GenericTypeArguments[0].GetSimpleName()}?";

      string baseName = "";
      if ((type.Namespace?.StartsWith("System.") ?? false) || type.Namespace == "System")
        baseName = type.FullName.Substring(type.Namespace.Length + 1);
      else
        baseName = type.FullName;
      int genericCutIndex = baseName.IndexOf('`');
      if (genericCutIndex != -1)
        baseName = baseName.Substring(0, genericCutIndex);

      return $"{baseName}<{string.Join(", ", type.GenericTypeArguments.Select(GetSimpleName))}>";
    }

    /// <summary>
    /// Returns field definition similar to how it'd be written in c#
    /// </summary>
    public static string GetSimpleName(this FieldInfo field)
    {
      return $"{field.FieldType.GetSimpleName()} {field.DeclaringType?.FullName}+{field.Name}";
    }

    /// <summary>
    /// Returns method definition similar to how it'd be written in c#
    /// </summary>
    public static string GetSimpleName(this MethodInfo method)
    {
      return $"{method.ReturnType.GetSimpleName()} {method.DeclaringType.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(x => x.ParameterType.GetSimpleName()))})";
    }

    /// <summary>
    /// Returns method definition similar to how it'd be written in c#
    /// </summary>
    public static string GetSimpleName(this ConstructorInfo method)
    {
      return $"{method.DeclaringType.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(x => x.ParameterType.GetSimpleName()))})";
    }

    /// <summary>
    /// Returns method definition similar to how it'd be written in c#
    /// </summary>
    public static string GetSimpleName(this MethodBase method)
    {
      if (method is MethodInfo methodInfo)
        return methodInfo.GetSimpleName();
      if (method is ConstructorInfo constructorInfo)
        return constructorInfo.GetSimpleName();
      return method.ToString();
    }
  }
}

namespace gelbi_silly_lib.BepInExUtils
{
  /// <summary>
  /// Extensions for some BepInEx features
  /// </summary>
  public static class Extensions
  {
    /// <summary>
    /// Checks whether assembly defines BaseUnityPlugin classes
    /// </summary>
    public static bool HasPluginClasses(this Assembly self) // BepInEx has similar method, but I'm not sure how it works
    {
      foreach (Type type in self.GetTypes())
        if (type.IsSubclassOf(typeof(BaseUnityPlugin)))
          return true;
      return false;
    }

    /// <summary>
    /// Returns BaseUnityPlugin classes, defined in this assembly
    /// </summary>
    public static IEnumerable<Type> GetPluginClasses(this Assembly self)
    {
      foreach (Type type in self.GetTypes())
        if (type.IsSubclassOf(typeof(BaseUnityPlugin)))
          yield return type;
    }

    /// <summary>
    /// Returns BaseUnityPlugin classes, defined in this assembly safely... aaaaaaaa
    /// </summary>
    public static IEnumerable<Type> GetPluginClassesSafe(this Assembly self)
    {
      foreach (Type type in self.GetTypesSafe())
        if (type.IsSubclassOf(typeof(BaseUnityPlugin)))
          yield return type;
    }

    public static BepInPlugin GetPluginAttribute(this Type self)
    {
      return (BepInPlugin)self.GetCustomAttribute(typeof(BepInPlugin));
    }
  }
}

namespace gelbi_silly_lib.ModManagerUtils
{
  /// <summary>
  /// Extensions for Mod/Assembly interactions
  /// </summary>
  public static class Extensions
  {
    /// <summary>
    /// Returns main assembly, containing plugin class, in same folder of dll, defining this assembly.
    /// Will return this assembly, if there's too little dlls in that folder
    /// </summary>
    public static Assembly GetMainModAssembly(this Assembly self)
    {
      string[] dllPaths = Directory.GetFiles(Path.GetDirectoryName(self.Location), "*.dll", SearchOption.AllDirectories);
      if (dllPaths.Length < 2)
        return self;
      foreach (KeyValuePair<Assembly, List<IDetour>> hookListKVP in RuntimeDetourManagerInternal.hookLists)
      {
        string path1 = hookListKVP.Key.Location;
        foreach (string path2 in dllPaths)
          if (path1 == path2)
            return hookListKVP.Key;
      }
      return null;
    }

    /// <summary>
    /// Returns mod, containing this assembly, if one exists
    /// </summary>
    public static ModManager.Mod GetDefiningMod(this Assembly self)
    {
      if (GSLUtils.assemblyOwnerCache.TryGetValue(self, out ModManager.Mod owner))
        return owner;

      string dllPath = self.Location.Replace('/', '\\');
      foreach (ModManager.Mod mod in ModManager.InstalledMods)
        if (dllPath.StartsWith(mod.basePath.Replace('/', '\\')))
        {
          GSLUtils.assemblyOwnerCache[self] = mod;
          GSLUtils.modAssemblyCache.AddOrCreateWith(mod, self);
          return mod;
        }
      return null;
    }

    /// <summary>
    /// Returns assemblies, defined by that mod
    /// </summary>
    public static List<Assembly> GetAssemblies(this ModManager.Mod self)
    { // ModManager.Mod instances are recreated each time mods are reapplied, but it still serves its purpose, just less efficient
      if (GSLUtils.modAssemblyCache.TryGetValue(self, out List<Assembly> assemblies))
        return assemblies;

      string basePath = self.basePath.Replace('/', '\\');
      assemblies = new();
      foreach (KeyValuePair<Assembly, List<IDetour>> hookListKVP in RuntimeDetourManagerInternal.hookLists)
        if (hookListKVP.Key.Location.Replace('/', '\\').StartsWith(basePath))
          assemblies.Add(hookListKVP.Key);
      if (assemblies.Count != 0)
      {
        foreach (Assembly asm in assemblies)
          GSLUtils.assemblyOwnerCache[asm] = self;
        GSLUtils.modAssemblyCache[self] = assemblies;
      }
      return assemblies;
    }

    /// <summary>
    /// Returns simple mod nameplate
    /// </summary>
    public static string GetSimpleName(this ModManager.Mod self)
    {
      return $"[{self.name} {self.version}]";
    }

    /// <summary>
    /// Returns simple mod nameplate, based on type
    /// </summary>
    public static string GetSimplePluginName(this Type self)
    {
      BepInPlugin attribute = self.GetPluginAttribute();
      return $"[{attribute.Name} {attribute.Version}]";
    }

    /// <summary>
    /// Returns full mod nameplate, including information from both remix mod and type
    /// </summary>
    public static string GetFullPluginName(this Type self)
    {
      ModManager.Mod mod = self.Assembly.GetDefiningMod();
      if (mod == null)
        return self.GetSimplePluginName();
      BepInPlugin attribute = self.GetPluginAttribute();
      return $"[{mod.name}{{{attribute.Name}}} | {mod.id}{{{attribute.GUID}}} {mod.version}]";
    }

    /// <summary>
    /// Returns plugin information from all plugin classes in assembly
    /// </summary>
    public static string GetSimplePluginName(this Assembly self)
    {
      IEnumerable<Type> plugins = self.GetPluginClassesSafe();
      string str = "";
      for (int i = 0; i < plugins.Count(); ++i)
      {
        if (i != 0)
          str += "<LINE>";
        str += plugins.ElementAt(i).GetSimplePluginName();
      }
      return str;
    }

    /// <summary>
    /// Returns extended plugin information from all plugin classes in assembly
    /// </summary>
    public static string GetFullPluginName(this Assembly self)
    {
      IEnumerable<Type> plugins = self.GetPluginClassesSafe();
      string str = "";
      for (int i = 0; i < plugins.Count(); ++i)
      {
        if (i != 0)
          str += "<LINE>";
        str += plugins.ElementAt(i).GetFullPluginName();
      }
      return str;
    }
  }
}

namespace gelbi_silly_lib.MonoModUtils
{
  /// <summary>
  /// Extensions for different hook/il related methods
  /// </summary>
  public static class Extensions
  {
    /// <summary>
    /// Returns target method
    /// </summary>
    public static MethodBase GetTarget(this IDetour self)
    {
      if (self is Detour detour)
        return detour.Target;
      if (self is Hook hook)
        return hook.Target;
      if (self is ILHook ilhook)
        return ilhook.Manipulator.Method;
      return null;
    }

    /// <summary>
    /// Returns target's assembly. Will return `null` for internal detours.
    /// </summary>
    public static Assembly GetAssembly(this IDetour self)
    {
      return self.GetTarget()?.DeclaringType?.Assembly;
    }

    /// <summary>
    /// Returns target method definition similar to how it'd be written in c#
    /// </summary>
    public static string GetSimpleTargetName(this IDetour self)
    {
      return self.GetTarget()?.GetSimpleName();
    }

    /// <summary>
    /// Logs body of IL hook
    /// </summary>
    public static void LogBody(this ILContext il)
    {
      Log.LogInfo($" * Logging IL body of {il.Method}");
      foreach (Instruction i in il.Body.Instructions)
        Log.LogInfo($"{i.Offset:X4}: {i.OpCode} {i.Operand}");
    }
  }
}

namespace gelbi_silly_lib
{
  /// <summary>
  /// Some common binding flags
  /// </summary>
  public static class BFlags
  {
    /// <summary>
    /// <c>static / not static / private / public</c>
    /// </summary>
    public const BindingFlags any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    /// <summary>
    /// <c>static / private / public</c>
    /// </summary>
    public const BindingFlags anyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    /// <summary>
    /// <c>not static / private / public</c>
    /// </summary>
    public const BindingFlags anyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
  }

  public static partial class GSLUtils
  {
    public static readonly Dictionary<Type, string> baseTypeNameAtlas = new()
    {
      { typeof(void), "void" },
      { typeof(string), "string" },
      { typeof(object), "object" },
      { typeof(bool), "bool" },
      { typeof(char), "char" },
      { typeof(byte), "byte" },
      { typeof(sbyte), "sbyte" },
      { typeof(short), "short" },
      { typeof(ushort), "ushort" },
      { typeof(int), "int" },
      { typeof(uint), "uint" },
      { typeof(long), "long" },
      { typeof(ulong), "ulong" },
      { typeof(float), "float" },
      { typeof(double), "double" },
      { typeof(decimal), "decimal" }
    };

    /// <summary>
    /// Loaded assemblies, defined per mod
    /// </summary>
    public static Dictionary<ModManager.Mod, List<Assembly>> modAssemblyCache = new();
    /// <summary>
    /// Dictionary with mods, defining specific assembly
    /// </summary>
    public static Dictionary<Assembly, ModManager.Mod> assemblyOwnerCache = new();

    /// <summary>
    /// Logs all loaded assemblies in current domain
    /// </summary>
    public static void LogAssemblies()
    {
      foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        Log.LogInfo(asm);
    }
  }

  /// <summary>
  /// HoodEndpointManager related methods. HoodEndpointManager automatically tracks all but manual hooks
  /// </summary>
  public static class HookEndpointManagerUtils
  {
    /// <summary>
    /// Returns hook lists, managed by HookEndpointManager
    /// </summary>
    public static IDictionary GetHookLists()
    {
      return (IDictionary)typeof(HookEndpointManager).GetStaticFieldValue("OwnedHookLists");
    }

    /// <summary>
    /// Returns hook maps, managed by HookEndpointManager
    /// </summary>
    public static IDictionary GetHookMaps()
    {
      return (IDictionary)typeof(HookEndpointManager).GetStaticFieldValue("HookEndpointMap");
    }

    /// <summary>
    /// Returns hook list, managed by HookEndpointManager and assigned to given assembly
    /// </summary>
    public static IList GetHookList(Assembly asm)
    {
      foreach (DictionaryEntry kvp in GetHookLists())
        if ((Assembly)kvp.Key == asm)
          return kvp.Value as IList;
      return null;
    }

    /// <summary>
    /// Logs hook list, managed by HookEndpointManager
    /// </summary>
    public static void LogHookList(IList hookList, string space = "  ")
    {
      foreach (var hookEntry in hookList)
        Log.LogInfo($"{space}{(hookEntry.GetFieldValue("Hook") as Delegate).Method.GetSimpleName()}");
    }

    /// <summary>
    /// Logs all hooks, managed by HookEndpointManager, by defining assembly
    /// </summary>
    public static void LogAllHookLists()
    {
      Log.LogInfo($" * Logging all currently applied hooks by assembly(if assembly is empty, you're calling it before mod was able to apply its hooks):");
      foreach (DictionaryEntry hookListKVP in GetHookLists())
      {
        Log.LogInfo($"{hookListKVP.Key}");
        LogHookList(hookListKVP.Value as IList);
      }
      Log.LogInfo($" * Finished logging");
    }

    /// <summary>
    /// Logs hooks, managed by HookEndpointManager, by hooked method
    /// </summary>
    public static void LogHookMap(Dictionary<Delegate, Stack<IDetour>> hookMap, string space = "  ")
    {
      foreach (var hook in hookMap)
        Log.LogInfo($"{space}{hook.Key.Method.GetSimpleName()}");
    }

    /// <summary>
    /// Logs hook map, managed by HookEndpointManager and assigned to given method
    /// </summary>
    public static void LogHookMap(MethodInfo method, string space = "  ")
    {
      LogHookMap(ModManager.GetHookMap(method), space);
    }

    /// <summary>
    /// Logs all hook maps, managed by HookEndpointManager
    /// </summary>
    public static void LogAllHookMaps()
    {
      Log.LogInfo($" * Logging all currently applied hooks by hooked method(output depends on when you're calling it, thus which mods were able to apply their hooks):");
      foreach (DictionaryEntry hookMapKVP in GetHookMaps())
      {
        Log.LogInfo($"{(hookMapKVP.Key as MethodBase).GetSimpleName()}");
        LogHookMap((Dictionary<Delegate, Stack<IDetour>>)hookMapKVP.Value.GetFieldValue("HookMap"));
      }
      Log.LogInfo($" * Finished logging");
    }
  }
}
