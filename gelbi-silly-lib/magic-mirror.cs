using BepInEx;
using gelbi_silly_lib.BepInExUtils;
using gelbi_silly_lib.Other;
using gelbi_silly_lib.ReflectionUtils;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

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
      return self.GetTypes().Any(t => t.IsSubclassOf(typeof(BaseUnityPlugin)));
    }

    /// <summary>
    /// Checks safely whether assembly defines BaseUnityPlugin classes
    /// </summary>
    public static bool HasPluginClassesSafe(this Assembly self)
    {
      return self.GetTypesSafe().Any(t => t.IsSubclassOf(typeof(BaseUnityPlugin)));
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
      string[] dllNames = dllPaths.Select(p => Path.GetFileName(p)).ToArray();
      return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => dllNames.Contains(Path.GetFileName(a.Location)) && a.HasPluginClassesSafe());
    }

    /// <summary>
    /// Returns mod, containing this assembly, if one exists
    /// </summary>
    public static ModManager.Mod GetDefiningMod(this Assembly self)
    {
      string dllPath = self.Location.Replace('/', '\\');
      return ModManager.InstalledMods.FirstOrDefault(mod => dllPath.StartsWith(mod.basePath.Replace('/', '\\')));
    }

    /// <summary>
    /// Returns assemblies, defined by that mod
    /// </summary>
    public static List<Assembly> GetAssemblies(this ModManager.Mod self)
    {
      string basePath = self.basePath.Replace('/', '\\');
      return AppDomain.CurrentDomain.GetAssemblies().Where(a => a.Location.Replace('/', '\\').StartsWith(basePath)).ToList();
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