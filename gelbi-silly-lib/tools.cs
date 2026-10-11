using gelbi_silly_lib.Other;
using Mono.Cecil;
using MonoMod.Utils;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace gelbi_silly_lib;

/// <summary>
/// Tools for debugging
/// </summary>
public static class GSLTools
{
  /// <summary>
  /// Tool name - delegate pairs. You can add your tools directly before Futile..ctor (in OnEnable)
  /// </summary>
  public static Dictionary<string, Action> toolDefs = new()
  {
    { "lar", LogAssemblyReferences },
    { "logreferences", LogAssemblyReferences },
    { "lh", LogHooks },
    { "loghooks", LogHooks },
    { "ldt", LogDependencyTargets },
    { "logdependencytargets", LogDependencyTargets },
    { "sur", ScanForUnresolvedReferences },
    { "scanunresolvedreferences", ScanForUnresolvedReferences },
    { "las", LogAllSprites },
    { "logsprites", LogAllSprites },
    { "lmd", LogModifyDefs },
    { "logmodifydefs", LogModifyDefs },
    { "lwd", LogWorldDefs },
    { "logworlddefs", LogWorldDefs },
    { "sbmr", ScanForBrokenModRooms },
    { "scanmodrooms", ScanForBrokenModRooms },
    { "sbmmr", ScanForBrokenMergedmodsRooms },
    { "scanmergedmodsrooms", ScanForBrokenMergedmodsRooms },
  };
  internal static List<Action> futile = [], preModsInit = [], onModsInit = [], postModsInit = [], refreshModsLists = [], gameCtor = [];
  /// <summary>
  /// Target name - invokation list pairs. You can add your targets directly before Futile..ctor (in OnEnable)
  /// <para>You also have to call <see cref="Invoke(List{Action})"/> at respective stage</para>
  /// </summary>
  public static Dictionary<string, List<Action>> toolTargets = new()
  {
    { "Futile", futile },
    { "PreModsInit", preModsInit },
    { "OnModsInit", onModsInit },
    { "PostModsInit", postModsInit },
    { "RefreshModsLists", refreshModsLists },
    { "RainWorldGame..ctor", gameCtor },
  };

  static void ApplyMethods(ref List<Action> methods, string defs)
  {
    foreach (string def in defs.ToLowerInvariant().Trim().Split('|'))
      if (toolDefs.TryGetValue(def.Trim(), out Action method))
        methods.AddUnique(method);
      else
        LogWarning($"Unrecognized tool name: '{def.Trim()}'");
  }

  public static void Invoke(List<Action> methods)
  {
    try
    {
      foreach (Action method in methods)
        method();
    }
    catch (Exception e)
    {
      LogError(e);
    }
  }

  internal static void Setup()
  {
    if (!File.Exists("gslsetup.txt"))
      return;
    string[] lines = File.ReadAllLines("gslsetup.txt");
    if (lines.Length == 0)
      return;
    LogInfo("Processing gslsetup.txt");
    foreach (string line in lines)
    {
      if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
        continue;
      if (line == "ret")
        break;
      int separatorIndex = line.IndexOf(':');
      if (separatorIndex == -1)
      {
        LogWarning($"Invalid line in gsl setup: '{line}'. Line must contain ':' separator");
        continue;
      }
      string target = line.Substring(0, separatorIndex).Trim(), defs = line.Substring(separatorIndex + 1);
      if (toolTargets.TryGetValue(target, out List<Action> methods))
        ApplyMethods(ref methods, defs);
      else
      {
        StringBuilder sb = new();
        foreach (KeyValuePair<string, List<Action>> targetData in toolTargets)
          sb.Append(targetData.Key).Append(", ");
        sb.Length -= 2;
        LogWarning($"Unrecognized target in gsl setup: '{target}'. Must be one of following: {sb}");
      }
    }
    if (preModsInit.Count != 0)
      On.RainWorld.PreModsInit += RainWorld_PreModsInit;
    if (postModsInit.Count != 0)
      On.RainWorld.PostModsInit += RainWorld_PostModsInit;
    if (refreshModsLists.Count != 0)
      On.ModManager.RefreshModsLists += ModManager_RefreshModsLists;
    if (gameCtor.Count != 0)
      On.RainWorldGame.ctor += RainWorldGame_ctor;
    if (futile.Count != 0)
      Invoke(futile);
  }

  static void RainWorld_PreModsInit(On.RainWorld.orig_PreModsInit orig, RainWorld self)
  {
    orig(self);
    Invoke(preModsInit);
  }

  static void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
  {
    orig(self);
    Invoke(postModsInit);
  }

  static void ModManager_RefreshModsLists(On.ModManager.orig_RefreshModsLists orig, RainWorld rainWorld)
  {
    orig(rainWorld);
    Invoke(gameCtor);
  }

  static void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
  {
    orig(self, manager);
    Invoke(gameCtor);
  }

  internal static void OnModsInit() => Invoke(onModsInit);

  /// <summary>
  /// Logs assembly references of each loaded assembly
  /// </summary>
  public static void LogAssemblyReferences()
  {
    LogInfo(" * Logging all assembly references:");
    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
      if (GSLPUtils.baseAssemblies.Contains(assembly.GetName().Name))
        continue;
      LogInfo($"{assembly.GetName().Name}:");
      foreach (AssemblyName referenceName in assembly.GetReferencedAssemblies())
        if (!GSLPUtils.baseAssemblies.Contains(referenceName.Name))
          LogInfo($"  {referenceName.FullName}");
    }
    LogInfo(" * Finished logging");
  }

  /// <summary>
  /// Logs all hooks instantiated at the moment of invokation
  /// </summary>
  public static void LogHooks()
  {
    RuntimeDetourManager.LogAllHookLists();
    RuntimeDetourManager.LogAllHookMaps();
  }

  /// <summary>
  /// Logs modinfo dependencies of all active mods
  /// </summary>
  public static void LogDependencyTargets()
  {
    Dictionary<string, List<string>> dependencyTargets = [];
    int longestModName = 0;
    foreach (ModManager.Mod mod in ModManager.ActiveMods)
    {
      foreach (string dependency in mod.requirements)
        dependencyTargets.AddOrCreateWith(dependency, mod.name);
      if (mod.name.Length > longestModName)
        longestModName = mod.name.Length;
    }
    LogInfo(" * Logging all dependency targets");
    foreach (KeyValuePair<string, List<string>> dependency in dependencyTargets)
      LogInfo((ModUtils.modIDMap.TryGetValue(dependency.Key, out ModManager.Mod mod) ? mod.name : "<missing>" + dependency.Key).PadRight(longestModName) + " : " + string.Join(", ", dependency.Value));
    LogInfo(" * Finished logging");
  }

  /// <summary>
  /// Logs all assemblies and unresolved references they have for assemblies that have unresolved references
  /// </summary>
  public static void ScanForUnresolvedReferences()
  {
    Stopwatch sw = Stopwatch.StartNew();
    LogInfo(" * Searching for unresolved member references:");
    try
    {
      foreach (KeyValuePair<Assembly, Dictionary<string, PluginData>> plugin in PluginUtils.plugins)
      {
        if (!File.Exists(plugin.Key.Location))
          continue;
        bool unresolvedMembers = false;
        foreach (MemberReference mref in AssemblyDefinition.ReadAssembly(plugin.Key.Location).MainModule.GetMemberReferences())
        {
          bool resolved = false;
          try { resolved = mref.Resolve() == null && mref.ResolveReflection() == null; } catch { }
          if (resolved)
          {
            if (!unresolvedMembers)
            {
              unresolvedMembers = true;
              LogInfo($"> {plugin.Key}");
            }
            LogInfo($"  {mref}");
          }
        }
      }
    }
    catch (Exception e)
    {
      LogError(e);
    }
    LogInfo($" * Finished searching in {sw.ElapsedTicks * 1000.0 / Stopwatch.Frequency:0.###}ms");
  }

  /// <summary>
  /// Logs all cached sprites
  /// </summary>
  public static void LogAllSprites()
  {
    LogInfo(" * Logging all cached sprites:");
    foreach (KeyValuePair<string, FAtlasElement> sprite in Futile.atlasManager._allElementsByName)
      LogInfo(sprite.Key);
    LogInfo(" * Finished logging");
  }

  /// <summary>
  /// Logs all modify files and their sources
  /// </summary>
  public static void LogModifyDefs() => ModUtils.LogDefsForPath("modify\\world", "modify");

  /// <summary>
  /// Logs all world files and their sources
  /// </summary>
  public static void LogWorldDefs() => ModUtils.LogDefsForPath("world", "world");

  /// <summary>
  /// Logs all broken rooms, defined by mods
  /// </summary>
  public static void ScanForBrokenModRooms()
  {
    string lastRoom = null;
    try
    {
      Stopwatch sw = Stopwatch.StartNew();
      LogInfo("* Searching for broken mod rooms");
      List<(string def, string path)> rooms = [];
      foreach (KeyValuePair<string, ModManager.Mod> mod in ModUtils.mods)
        if (mod.Value.enabled && !ModUtils.baseMods.Contains(mod.Value.id))
        {
          int baseLength = Directory.GetParent(mod.Value.path).FullName.Length + 1;
          foreach (string path in mod.Value.GetVersionedDirectories("world"))
            foreach (string directory in Directory.GetDirectories(path, "*-rooms"))
              foreach (string file in Directory.GetFiles(directory, "*.txt"))
              {
                string localPath = file.Substring(baseLength).ToLowerInvariant(), filename = Path.GetFileNameWithoutExtension(localPath);
                if (filename.Length < 7 || !filename.Contains("_settings"))
                  rooms.Add((localPath, file));
              }
          foreach (string path in mod.Value.GetVersionedDirectories("levels"))
            foreach (string file in Directory.GetFiles(path, "*.txt"))
            {
              string localPath = file.Substring(baseLength).ToLowerInvariant(), filename = Path.GetFileNameWithoutExtension(localPath);
              if (filename.Length < 7 || !filename.EndsWith("_settings") && !filename.EndsWith("_arena") && !filename.EndsWith("_properties"))
                rooms.Add((localPath, file));
            }
        }
      foreach ((string def, string path) in rooms)
      {
        lastRoom = def;
        RoomRepresentation roomRepresentation = new(path);
        if (roomRepresentation.GetError(out string msg))
          LogInfo($"{def} : {msg}\n{roomRepresentation.representation}");
      }
      LogInfo($"* Finished searching in {sw.ElapsedTicks * 1000.0 / Stopwatch.Frequency:0.###}ms");
    }
    catch (Exception e)
    {
      LogError($"Error while scanning mod rooms [{lastRoom}]: {e}");
    }
  }

  /// <summary>
  /// Logs all broken rooms in mergedmods
  /// </summary>
  public static void ScanForBrokenMergedmodsRooms()
  {
    string lastRoom = null;
    try
    {
      Stopwatch sw = Stopwatch.StartNew();
      LogInfo("* Searching for broken mergedmods rooms");
      List<(string def, string path)> rooms = [];
      int baseLength = Custom.rootFolderDirectory.Length + 1;
      string path;
      if (Directory.Exists(path = Path.Combine(Custom.rootFolderDirectory, "mergedmods\\world")))
        foreach (string directory in Directory.GetDirectories(path, "*-rooms"))
          foreach (string file in Directory.GetFiles(directory, "*.txt"))
          {
            string localPath = file.Substring(baseLength).ToLowerInvariant(), filename = Path.GetFileNameWithoutExtension(localPath);
            if (filename.Length < 7 || !filename.Contains("_settings"))
              rooms.Add((localPath, file));
          }
      if (Directory.Exists(path = Path.Combine(Custom.rootFolderDirectory, "mergedmods\\levels")))
        foreach (string file in Directory.GetFiles(path, "*.txt"))
        {
          string localPath = file.Substring(baseLength).ToLowerInvariant(), filename = Path.GetFileNameWithoutExtension(localPath);
          if (filename.Length < 7 || !filename.EndsWith("_settings") && !filename.EndsWith("_arena") && !filename.EndsWith("_properties"))
            rooms.Add((localPath, file));
        }
      foreach ((string def, string roomPath) in rooms)
      {
        lastRoom = def;
        RoomRepresentation roomRepresentation = new(roomPath);
        if (roomRepresentation.GetError(out string msg))
          LogInfo($"{def} : {msg}\n{roomRepresentation.representation}");
      }
      LogInfo($"* Finished searching in {sw.ElapsedTicks * 1000.0 / Stopwatch.Frequency:0.###}ms");
    }
    catch (Exception e)
    {
      LogError($"Error while scanning mergedmods rooms [{lastRoom}]: {e}");
    }
  }
}