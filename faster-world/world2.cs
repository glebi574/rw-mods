using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace faster_world;

// optimizes region loading by predicting future and stuff
public static class M_World2
{
  public static ConcurrentDictionary<string, string> cachedRoomDirectories = [];
  public static ConcurrentDictionary<string, string[]> cachedRooms = [];

  public static void StaticWorld_InitStaticWorld(On.StaticWorld.orig_InitStaticWorld orig)
  {
    orig();
    MachineConnector.SetRegisteredOI("gelbi.faster-world", Plugin.pluginInterface = new());

    try
    {
      cachedRoomDirectories.Clear();
      cachedRooms.Clear();
      if (!SPrecacheRooms)
        return;
      new Task(() =>
      {
        foreach (string line in Custom.rainWorld.progression.GetProgLinesFromMemory())
        {
          if (!line.StartsWith("SAVE STATE"))
            continue;
          string[] parts = line.Split("<progDivB>");
          if (parts.Length == 2 && BackwardsCompatibilityRemix.ParseSaveNumber(parts[1]) == Custom.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat)
            foreach (string a in parts[1].Split("<svA>"))
              if (a.Length != 0 && a.StartsWith("REGIONSTATE"))
                foreach (string b in a.Split("<svB>")[1].Split("<rgA>"))
                  if (b.StartsWith("REGIONNAME"))
                  {
                    CacheRooms(b.Split("<rgB>")[1], true);
                    goto _cacheCurrentEnd;
                  }
        }
      _cacheCurrentEnd:
        CacheTemplates();
        CacheRooms("world\\gates", false);
        CacheRooms("world\\gate shelters", false);
      }).Start();
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }

  public static HashSet<string> templateSkippedDirectories = ["gate shelters", "gates", "indexmaps"];
  public static void CacheTemplates()
  {
    ConcurrentBag<string> paths = [];
    List<string> targetPaths = [Path.Combine(Custom.rootFolderDirectory, "mergedmods")];
    foreach (ModManager.Mod mod in ModManager.ActiveMods)
    {
      if (mod.hasTargetedVersionFolder)
        targetPaths.Add(mod.TargetedPath);
      if (mod.hasNewestFolder)
        targetPaths.Add(mod.NewestPath);
      targetPaths.Add(mod.path);
    }
    targetPaths.Add(Custom.rootFolderDirectory);
    Parallel.ForEach(targetPaths, localPath =>
    {
      string fullPath = Path.Combine(localPath, "world");
      if (!Directory.Exists(fullPath))
        return;
      foreach (string path in Directory.EnumerateDirectories(fullPath))
      {
        string local = Path.GetFileName(path).ToLowerInvariant();
        if (templateSkippedDirectories.Contains(local) || local.EndsWith("-rooms"))
          continue;
        string template = local + "_settingstemplate";
        foreach (string filePath in Directory.EnumerateFiles(path, "*.txt"))
          if (Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant().StartsWith(template))
            paths.Add(filePath.Replace('/', '\\').ToLowerInvariant());
      }
    });
    Parallel.ForEach(paths, path => cachedRooms[path] = File.ReadAllLines(path));
  }

  public static void CacheRooms(string path, bool isRegion)
  {
    string region = null;
    if (isRegion)
      path = string.Concat("world\\", region = path.ToLowerInvariant(), "-rooms");
    if (cachedRoomDirectories.ContainsKey(path))
      return;
    ConcurrentBag<string> paths = [];
    List<string> targetPaths = [Path.Combine(Custom.rootFolderDirectory, "mergedmods")];
    foreach (ModManager.Mod mod in ModManager.ActiveMods)
    {
      if (mod.hasTargetedVersionFolder)
        targetPaths.Add(mod.TargetedPath);
      if (mod.hasNewestFolder)
        targetPaths.Add(mod.NewestPath);
      targetPaths.Add(mod.path);
    }
    targetPaths.Add(Custom.rootFolderDirectory);
    Parallel.ForEach(targetPaths, localPath =>
    {
      string fullPath = Path.Combine(localPath, path);
      if (Directory.Exists(fullPath))
        foreach (string filePath in Directory.EnumerateFiles(fullPath, "*.txt"))
        {
          string lowerPath = filePath.Replace('/', '\\').ToLowerInvariant();
          if (!cachedRooms.ContainsKey(lowerPath))
            paths.Add(lowerPath);
        }
    });
    Parallel.ForEach(paths, path => cachedRooms[path] = File.ReadAllLines(path));
    cachedRoomDirectories[path] = isRegion ? region : "";
  }

  public static void OverWorld_LoadWorld(On.OverWorld.orig_LoadWorld_string_Name_Timeline_bool orig, OverWorld self, string worldName, SlugcatStats.Name playerCharacterNumber, SlugcatStats.Timeline time, bool singleRoomWorld)
  {
    if (SPreloadRooms)
      CacheRooms(worldName, true);
    orig(self, worldName, playerCharacterNumber, time, singleRoomWorld);
    if (SCacheRooms)
      return;
    cachedRooms.Clear();
    cachedRoomDirectories.Clear();
  }

  public static World WorldLoader_ReturnWorld(On.WorldLoader.orig_ReturnWorld orig, WorldLoader self)
  {
    if (!SCacheRooms)
      return orig(self);
    World world = orig(self);
    HashSet<string> regions = [];
    foreach (int index in world.gates)
      if (world.GetAbstractRoom(index)?.name is string room && room.StartsWith("GATE_"))
      {
        room = room.Substring(5);
        int separator = room.IndexOf('_');
        if (separator == -1 || separator == room.Length - 1)
          continue;
        regions.Add(room.Substring(0, separator).ToLowerInvariant());
        regions.Add(room.Substring(separator + 1).ToLowerInvariant());
      }
    new Task(() =>
    {
      foreach (string region in regions)
        CacheRooms(region, true);
      List<string> oldRegions = [];
      foreach (KeyValuePair<string, string> dir in cachedRoomDirectories)
        if (dir.Value.Length != 0 && !regions.Contains(dir.Value))
          oldRegions.Add(dir.Key);
      foreach (string key in oldRegions)
        cachedRoomDirectories.TryRemove(key, out _);
      oldRegions.Clear();
      foreach (KeyValuePair<string, string[]> cache in cachedRooms)
      {
        string path = cache.Key;
        if (path.Contains("\\world\\gate"))
          continue;
        int worldIndex = path.IndexOf("\\world\\"), roomsIndex = path.IndexOf("-rooms\\");
        if (worldIndex > 0 && roomsIndex > worldIndex && !regions.Contains(path.Substring(worldIndex + 7, roomsIndex - worldIndex - 7)))
          oldRegions.Add(path);
      }
      foreach (string key in oldRegions)
        cachedRooms.TryRemove(key, out _);
    }).Start();
    return world;
  }

  public static string[] GetCachedStrings(string path)
  {
    if (!SCacheRooms)
    {
      if (SPreloadRooms && cachedRooms.TryGetValue(path.Replace('/', '\\').ToLowerInvariant(), out string[] lines))
        return lines;
      return File.ReadAllLines(path);
    }
    path = path.Replace('/', '\\').ToLowerInvariant();
    if (!cachedRooms.TryGetValue(path, out string[] array))
      cachedRooms[path] = array = File.ReadAllLines(path);
    return array;
  }

  public static void WorldLoader_LoadAbstractRoom(World world, string roomName, AbstractRoom room, RainWorldGame.SetupValues setupValues)
  {
    if (room.altFileName != null)
      roomName = room.altFileName;
    string path = WorldLoader.FindRoomFile(roomName, false, ".txt");
    string[] array = GetCachedStrings(path);
    bool versionFix = RoomPreprocessor.VersionFix(ref array);
    if (int.Parse(array[9].SubstringUntil('|'), NumberStyles.Any, CultureInfo.InvariantCulture) >= world.preProcessingGeneration)
      room.InitNodes(RoomPreprocessor.StringToConnMap(array[9]), array[1]);
    else
    {
      array = RoomPreprocessor.PreprocessRoom(room, array, world, setupValues, world.preProcessingGeneration);
      versionFix = true;
    }
    if (versionFix)
      File.WriteAllLines(path, array);
  }

  public static void RoomSettings_FindParent(RoomSettings self, Region region)
  {
    self.parent = DefaultRoomSettings.ancestor;
    if (region == null)
      return;
    if (!self.isTemplate)
    {
      if (!self.isAncestor && region.roomSettingsTemplates.Length != 0)
        self.parent = region.roomSettingsTemplates[0];
      if (File.Exists(self.filePath))
        foreach (string line in GetCachedStrings(self.filePath))
          if (line.StartsWith("Template"))
          {
            string def = line.Split(": ")[1];
            self.parent = def == "NONE" ? DefaultRoomSettings.ancestor : region.GetRoomSettingsTemplate(def.Split('_')[2].ToLowerInvariant());
            break;
          }
    }
    self.InheritEffects();
    self.InheritAmbientSounds();
  }

  public static void RoomSettings_Save_string_bool(On.RoomSettings.orig_Save_string_bool orig, RoomSettings self, string path, bool saveAsTemplate)
  {
    orig(self, path, saveAsTemplate);
    if (SCacheRooms)
      cachedRooms[path.Replace('/', '\\').ToLowerInvariant()] = File.ReadAllLines(path);
  }

  public static void RoomSettings_Load_Timeline(ILContext il)
  {
    ILCursor c = new(il);

    if (!c.TryGotoNext(MoveType.After, i => i.MatchCall(typeof(File), "ReadAllLines")))
      return;
    Instruction target = c.Next;
    --c.Index;
    c.Emit(OpCodes.Call, ((Delegate)GetCachedStrings).Method)
     .Emit(OpCodes.Br_S, target);
  }
}