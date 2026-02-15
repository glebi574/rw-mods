using BepInEx;
using gelbi_silly_lib.Converter;
using gelbi_silly_lib.ReflectionUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using gelbi_silly_lib.Other;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

public static class ModUtils
{
  private static readonly object _lock = new();
  /// <summary>
  /// Dictionary containing folder name - mod pairs
  /// </summary>
  public static ConcurrentDictionary<string, ModManager.Mod> mods = [];
  /// <summary>
  /// Dictionary containing mod id - mod pairs
  /// </summary>
  public static ConcurrentDictionary<string, ModManager.Mod> modIDMap = [];
  /// <summary>
  /// Dictionary containing folder name - changelog pairs
  /// </summary>
  public static ConcurrentDictionary<string, string> changelogs = [];
  /// <summary>
  /// Saved data manager, mainly containing dictionary with folder name - previous registered mod version pairs
  /// </summary>
  public static SimpleSavedDataHandler previousModVersions = new(["gelbi"], "previous-mod-versions");
  public static int longestFolderName = 1;

  internal static ModManager.Mod ModManager_LoadModFromJson(On.ModManager.orig_LoadModFromJson orig, RainWorld rainWorld, string basepath, string modpath)
  {
    if (orig(rainWorld, basepath, modpath) is not ModManager.Mod mod)
      return null;
    string folderName = Path.GetFileName(basepath), changelogPath;
    lock (_lock)
      if (folderName.Length > longestFolderName)
        longestFolderName = folderName.Length;
    if (!changelogs.ContainsKey(folderName) && File.Exists(changelogPath = Path.Combine(basepath, "changelog.txt")))
    {
      changelogs[folderName] = File.ReadAllText(changelogPath);
      if (!ChangelogMenu.modsUpdated && (!previousModVersions.data.TryGetValueWithType(folderName, out string version) || version != mod.version))
        ChangelogMenu.modsUpdated = true;
    }
    return modIDMap[mod.id] = mods[folderName] = mod;
  }

  /// <summary>
  /// Checks whether mod with specified id is enabled
  /// </summary>
  public static bool IsEnabled(string id) => modIDMap.TryGetValue(id, out ModManager.Mod mod) && mod.enabled;

  /// <summary>
  /// Returns assembly, that has plugin classes, from same folder if one exists. Otherwise returns <c>null</c>
  /// </summary>
  public static Assembly GetMainModAssembly(Assembly asm)
  {
    if (PluginUtils.modAssemblies.TryGetValue(PluginUtils.GetModFolderName(asm), out HashSet<Assembly> assemblies))
      foreach (Assembly modAssembly in assemblies)
        if (PluginUtils.plugins.ContainsKey(modAssembly))
          return modAssembly;
    return null;
  }

  /// <summary>
  /// Returns mod, containing specified assembly, if one exists
  /// </summary>
  public static ModManager.Mod GetDefiningMod(Assembly asm)
  {
    if (mods.TryGetValue(PluginUtils.GetModFolderName(asm), out ModManager.Mod mod))
      return mod;
    return null;
  }

  /// <summary>
  /// Returns array with file paths for files that exist at specified local path including versioned folders in order:
  /// folder for current version, "newest" folder, mod folder itself
  /// </summary>
  public static string[] GetVersionedFiles(this ModManager.Mod self, string localPath)
  {
    List<string> paths = new(3);
    string path;
    if (File.Exists(path = Path.Combine(self.path, "v" + GSLPUtils.gameVersion, localPath)))
      paths.Add(path);
    if (File.Exists(path = Path.Combine(self.path, "newest", localPath)))
      paths.Add(path);
    if (File.Exists(path = Path.Combine(self.path, localPath)))
      paths.Add(path);
    return [.. paths];
  }

  /// <summary>
  /// Returns array with directory paths for directories that exist at specified local path including versioned folders in order:
  /// folder for current version, "newest" folder, mod folder itself
  /// </summary>
  public static string[] GetVersionedDirectories(this ModManager.Mod self, string localPath)
  {
    List<string> paths = new(3);
    string path;
    if (Directory.Exists(path = Path.Combine(self.path, "v" + GSLPUtils.gameVersion, localPath)))
      paths.Add(path);
    if (Directory.Exists(path = Path.Combine(self.path, "newest", localPath)))
      paths.Add(path);
    if (Directory.Exists(path = Path.Combine(self.path, localPath)))
      paths.Add(path);
    return [.. paths];
  }

  /// <summary>
  /// Checks if container has mod
  /// </summary>
  public static bool ContainsMod(this IEnumerable<ModManager.Mod> self, ModManager.Mod other) => self.Any(mod => mod.id == other.id);

  /// <summary>
  /// Returns label with mod's name and version
  /// </summary>
  public static string GetSimpleLabel(this ModManager.Mod self) => $"[{self.name} {self.version}]";

  /// <summary>
  /// Returns string with mod's name, id and version
  /// </summary>
  public static string MetadataToString(this ModManager.Mod self) => $"{self.name} ({self.id}) {self.version}";

  /// <summary>
  /// Returns string with plugin's name, id and version
  /// </summary>
  public static string MetadataToString(this BepInPlugin self) => $"{self.Name} ({self.GUID}) {self.Version}";

  /// <summary>
  /// Returns string with plugin's name, id and version if metadata is defined
  /// </summary>
  public static string MetadataToString(Type type)
  {
    if (MetadataHelper.GetMetadata(type) is BepInPlugin metadata)
      return metadata.MetadataToString();
    return "<No plugin metadata>";
  }

  /// <summary>
  /// Returns mod label, including information from both remix mod and type
  /// </summary>
  public static string GetMatchedPluginLabel(Type type)
  {
    StringBuilder sb = new(128);
    sb.Append('[').Append(GetDefiningMod(type.Assembly) is ModManager.Mod mod ? mod.MetadataToString() : "<No defining mod>")
      .Append(" | ").Append(MetadataToString(type))
      .Append(" : ").Append(type.GetSimpleName()).Append(']');
    return sb.ToString();
  }

  /// <summary>
  /// Returns mod label with information about active plugins and defining assemblies
  /// </summary>
  public static string GetFullModInfo(this ModManager.Mod self)
  {
    string folderName = Path.GetFileName(self.path);
    if (!PluginUtils.modAssemblies.TryGetValue(folderName, out HashSet<Assembly> assemblies))
      return $"[{Directory.GetParent(self.path).Name,6}\\{folderName}".PadRight(longestFolderName + 8) + $" | {self.MetadataToString()} : <No active plugins>]";
    StringBuilder sb = new(128);
    sb.Append('[').Append(Directory.GetParent(self.path).Name.PadLeft(6)).Append('\\').Append(folderName);
    if (sb.Length < longestFolderName + 8)
      sb.Append(' ', longestFolderName - sb.Length + 8);
    sb.Append(" | ").Append(self.MetadataToString()).Append(" | ");
    foreach (Assembly asm in assemblies)
    {
      sb.Append(asm.GetName().Name);
      if (PluginUtils.plugins.TryGetValue(asm, out Dictionary<string, PluginData> plugins))
      {
        sb.Append('{');
        foreach (KeyValuePair<string, PluginData> pluginData in plugins)
          sb.Append('<').Append(pluginData.Value.metadata.MetadataToString()).Append(" : ")
            .Append(pluginData.Value.instance.GetType().GetSimpleNameWithNamespace()).Append("> ");
        --sb.Length;
        sb.Append('}');
      }
      sb.Append(' ');
    }
    --sb.Length;
    return sb.Append(']').ToString();
  }

  /// <summary>
  /// Logs all files and their defining mods in folders nested at local path. Mainly for modify and world files.
  /// </summary>
  public static void LogDefsForPath(string localPath, string name)
  {
    LogInfo($" * Logging all {name} files and their sources:");
    Dictionary<string, Dictionary<string, HashSet<string>>> defs = new(256);
    int longestLine = 0;
    foreach (KeyValuePair<string, ModManager.Mod> mod in ModUtils.mods)
      if (mod.Value.enabled)
        foreach (string path in mod.Value.GetVersionedDirectories(localPath))
          foreach (string directory in Directory.GetDirectories(path))
          {
            string dirname = Path.GetFileName(directory);
            if (!defs.TryGetValue(dirname, out Dictionary<string, HashSet<string>> files))
              defs[dirname] = files = [];
            foreach (string file in Directory.GetFiles(directory, "*.txt"))
            {
              string filename = Path.GetFileNameWithoutExtension(file);
              files.AddOrCreateWith(filename, mod.Value.name);
              if (dirname.Length + filename.Length > longestLine)
                longestLine = dirname.Length + filename.Length;
            }
          }
    ++longestLine;
    foreach (KeyValuePair<string, Dictionary<string, HashSet<string>>> directory in defs)
    {
      string dir = directory.Key + '\\';
      foreach (KeyValuePair<string, HashSet<string>> file in directory.Value)
        LogInfo((dir + file.Key).PadRight(longestLine) + " : " + string.Join(", ", file.Value));
    }
    LogInfo($" * Finished logging");
  }
}