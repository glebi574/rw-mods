using BepInEx.Preloader.Patching;
using gelbi_silly_lib.Other;
using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

/// <summary>
/// Class with some assembly utilities. Mainly stores dynamic patches
/// </summary>
public static class AssemblyUtils
{
  /// <summary>
  /// Patches applied to all loaded assemblies
  /// </summary>
  public static List<Action<AssemblyDefinition>> universalPatches = [];
  /// <summary>
  /// Patches applied to assemblies with specified name
  /// </summary>
  public static Dictionary<string, List<Action<AssemblyDefinition>>> assemblyPatches = [];

  public static Dictionary<string, string> locations = [];

  static AssemblyUtils()
  {
    new Hook(typeof(Assembly).GetMethod("LoadFile", BFlags.anyDeclaredStatic, null, [typeof(string)], null), Assembly_LoadFile);
    new Hook(typeof(Assembly).GetProperty("Location").GetGetMethod(), Assembly_getLocation);
  }

  /// <summary>
  /// Adds patch which would be applied to all loaded assemblies
  /// </summary>
  public static void AddUniversalAssemblyPatch(Action<AssemblyDefinition> patch) => universalPatches.Add(patch);

  /// <summary>
  /// Adds patch which would be applied to assembly with specified filename when it loads
  /// </summary>
  public static void AddPatchForAssembly(string filename, Action<AssemblyDefinition> patch) => assemblyPatches.AddOrCreateWith(filename, patch);

  static Assembly Assembly_LoadFile(Func<string, Assembly> orig, string path)
  {
    List<Action<AssemblyDefinition>> patches = null;
    string filename = Path.GetFileName(path);
    if (universalPatches.Count == 0 && !assemblyPatches.TryGetValue(filename, out patches))
      return orig(path);

    AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(path);
    if (patches != null)
      foreach (Action<AssemblyDefinition> patch in patches)
        patch(asm);
    foreach (Action<AssemblyDefinition> patch in universalPatches)
      patch(asm);

    locations[Path.GetFileNameWithoutExtension(path)] = path;
    AssemblyPatcher.Load(asm, filename);
    return AppDomain.CurrentDomain.GetAssemblies().Last();
  }

  static string Assembly_getLocation(Func<Assembly, string> orig, Assembly self)
  {
    string location = orig(self);
    if (string.IsNullOrWhiteSpace(location))
      return locations[self.GetName().Name];
    return location;
  }
}