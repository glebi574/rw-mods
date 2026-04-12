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
using static CommonWrapper;

// optimizes region loading by predicting future and stuff
public static class M_World2
{
  public static ConcurrentDictionary<string, byte> cachedDirectories = [];
  public static ConcurrentDictionary<string, string[]> cachedRooms = [];

  public static void StaticWorld_InitStaticWorld(On.StaticWorld.orig_InitStaticWorld orig)
  {
    orig();

    try
    {
      cachedDirectories.Clear();
      cachedRooms.Clear();
      new Task(() =>
      {
        foreach (string line in Custom.rainWorld.progression.GetProgLinesFromMemory())
        {
          if (!line.StartsWith("SAVE STATE"))
            continue;
          string[] parts = line.Split(["<progDivB>"], StringSplitOptions.None);
          if (parts.Length == 2 && BackwardsCompatibilityRemix.ParseSaveNumber(parts[1]) == Custom.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat)
            foreach (string a in parts[1].Split(["<svA>"], StringSplitOptions.None))
              if (a.Length != 0 && a.StartsWith("REGIONSTATE"))
                foreach (string b in a.Split(["<svB>"], StringSplitOptions.None)[1].Split(["<rgA>"], StringSplitOptions.None))
                  if (b.StartsWith("REGIONNAME"))
                  {
                    CacheRooms("world", b.Split(["<rgB>"], StringSplitOptions.None)[1].ToLowerInvariant() + "-rooms");
                    goto _cacheCurrentEnd;
                  }
        }
      _cacheCurrentEnd:
        CacheTemplates();
        CacheRooms("world", "gates");
        CacheRooms("world", "gate shelters");
      }).Start();
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }

  public static void CacheRooms(params string[] directories)
  {
    string path = Path.Combine(directories);
    if (cachedDirectories.ContainsKey(path))
      return;
    ConcurrentBag<string> paths = [];
    List<string> targetPaths = [Path.Combine(Custom.RootFolderDirectory(), "mergedmods")];
    foreach (ModManager.Mod mod in ModManager.ActiveMods)
    {
      if (mod.hasTargetedVersionFolder)
        targetPaths.Add(mod.TargetedPath);
      if (mod.hasNewestFolder)
        targetPaths.Add(mod.NewestPath);
      targetPaths.Add(mod.path);
    }
    targetPaths.Add(Custom.RootFolderDirectory());
    Parallel.ForEach(targetPaths, localPath =>
    {
      string fullPath = Path.Combine(localPath, path);
      if (Directory.Exists(fullPath))
        foreach (string filePath in Directory.GetFiles(fullPath, "*.txt"))
        {
          string lowerPath = filePath.ToLowerInvariant();
          if (!cachedRooms.ContainsKey(lowerPath))
            paths.Add(lowerPath);
        }
    });
    Parallel.ForEach(paths, path => cachedRooms[path] = File.ReadAllLines(path));
    cachedDirectories[path] = 0;
  }

  public static void CacheTemplates()
  {
    ConcurrentBag<string> paths = [];
    List<string> targetPaths = [Path.Combine(Custom.RootFolderDirectory(), "mergedmods")];
    foreach (ModManager.Mod mod in ModManager.ActiveMods)
    {
      if (mod.hasTargetedVersionFolder)
        targetPaths.Add(mod.TargetedPath);
      if (mod.hasNewestFolder)
        targetPaths.Add(mod.NewestPath);
      targetPaths.Add(mod.path);
    }
    targetPaths.Add(Custom.RootFolderDirectory());
    Parallel.ForEach(targetPaths, localPath =>
    {
      string fullPath = Path.Combine(localPath, "world");
      if (!Directory.Exists(fullPath))
        return;
      foreach (string path in Directory.GetDirectories(fullPath))
      {
        string local = Path.GetFileName(path).ToLowerInvariant();
        if (local != "gate shelters" && local != "gates" && local != "indexmaps" && !local.EndsWith("-rooms"))
        {
          string template = local + "_settingstemplate";
          foreach (string filePath in Directory.GetFiles(path, "*.txt"))
          {
            if (Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant().StartsWith(template))
              paths.Add(filePath.ToLowerInvariant());
          }
        }
      }
    });
    Parallel.ForEach(paths, path => cachedRooms[path] = File.ReadAllLines(path));
  }

  public static void OverWorld_LoadWorld(On.OverWorld.orig_LoadWorld_string_Name_Timeline_bool orig, OverWorld self, string worldName, SlugcatStats.Name playerCharacterNumber, SlugcatStats.Timeline time, bool singleRoomWorld)
  {
    CacheRooms("world", worldName.ToLowerInvariant() + "-rooms");
    orig(self, worldName, playerCharacterNumber, time, singleRoomWorld);
  }

  public static World WorldLoader_ReturnWorld(On.WorldLoader.orig_ReturnWorld orig, WorldLoader self)
  {
    World world = orig(self);
    HashSet<string> regions = [];
    foreach (int index in world.gates)
      if (world.GetAbstractRoom(index)?.name is string room && room.StartsWith("GATE_"))
      {
        room = room.Substring(5);
        int separator = room.IndexOf('_');
        if (separator == -1 || separator == room.Length - 1)
          continue;
        regions.Add(room.Substring(0, separator));
        regions.Add(room.Substring(separator + 1));
      }
    regions.Remove(world.name);
    new Task(() =>
    {
      foreach (string region in regions)
        CacheRooms("world", region.ToLowerInvariant() + "-rooms");
    }).Start();
    return world;
  }

  public static string[] GetCachedStrings(string path)
  {
    path = path.ToLowerInvariant();
    if (!cachedRooms.TryGetValue(path, out string[] array))
      cachedRooms[path] = array = File.ReadAllLines(path);
    return array;
  }

  public static void WorldLoader_LoadAbstractRoom(World world, string roomName, AbstractRoom room, RainWorldGame.SetupValues setupValues)
  {
    if (room.altFileName != null)
      roomName = room.altFileName;
    string path = FindRoomFile(roomName);
    string[] array = GetCachedStrings(path);
    bool flag = RoomPreprocessor.VersionFix(ref array);
    if (int.Parse(array[9].SubstringUntil('|'), NumberStyles.Any, CultureInfo.InvariantCulture) >= world.preProcessingGeneration)
      room.InitNodes(RoomPreprocessor.StringToConnMap(array[9]), array[1]);
    else
    {
      array = RoomPreprocessor.PreprocessRoom(room, array, world, setupValues, world.preProcessingGeneration);
      flag = true;
    }
    if (flag)
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
      {
        string[] array = GetCachedStrings(self.filePath);
        for (int i = 0; i < array.Length; ++i)
        {
          if (!array[i].StartsWith("Template"))
            continue;
          string[] parts = array[i].Split([": "], StringSplitOptions.None);
          self.parent = parts[1] == "NONE" ? DefaultRoomSettings.ancestor : region.GetRoomSettingsTemplate(parts[1].Split('_')[2].ToLowerInvariant());
          break;
        }
      }
    }
    self.InheritEffects();
    self.InheritAmbientSounds();
  }

  public static void RoomSettings_Load_Timeline(ILContext il)
  {
    ILCursor c = new(il);

    if (c.TryGotoNext(i => i.MatchCall(typeof(File), "ReadAllLines")))
    {
      ++c.Index;
      Instruction target = c.Next;
      --c.Index;
      c.Emit(OpCodes.Call, ((Delegate)GetCachedStrings).Method);
      c.Emit(OpCodes.Br_S, target);
    }
  }

  public static string FindRoomFile(string roomName)
  {
    string region = roomName.SubstringUntil('_'), roomFile = roomName + ".txt", text;
    if (File.Exists(text = AssetManager.ResolveFilePath(Path.Combine("World", region + "-Rooms", roomFile)))
      || region.ToUpper() == "GATE"
      && File.Exists(text = AssetManager.ResolveFilePath(Path.Combine("World", "Gates", roomFile)))
      || File.Exists(text = AssetManager.ResolveFilePath(Path.Combine("World", "Gates", "gate_shelters", roomFile)))
      || File.Exists(text = AssetManager.ResolveFilePath(Path.Combine("Levels", roomFile)))
      || ModManager.MSC && roomName.ToLowerInvariant().Contains("challenge") && File.Exists(text = AssetManager.ResolveFilePath(Path.Combine("Levels", "Challenges", roomFile))))
      return text;
    return null;
  }

  public static string WorldLodaer_FindRoomFile(string roomName, bool includeRootDirectory, string additionalAppend, bool showWarning = true)
  {
    string region = roomName.SubstringUntil('_'), roomFile = roomName + additionalAppend, text;
    if (File.Exists(text = AssetManager.ResolveFilePath(Path.Combine("World", region + "-Rooms", roomFile)))
      || region.ToUpper() == "GATE"
      && File.Exists(text = AssetManager.ResolveFilePath(Path.Combine("World", "Gates", roomFile)))
      || File.Exists(text = AssetManager.ResolveFilePath(Path.Combine("World", "Gates", "gate_shelters", roomFile)))
      || File.Exists(text = AssetManager.ResolveFilePath(Path.Combine("Levels", roomFile)))
      || ModManager.MSC && roomName.ToLowerInvariant().Contains("challenge") && File.Exists(text = AssetManager.ResolveFilePath(Path.Combine("Levels", "Challenges", roomFile))))
    {
      if (includeRootDirectory)
        return "file:///" + text;
      return text;
    }
    return null;
  }

  public static AbstractRoomNode[] RoomPreprocessor_StringToConnMap(string str)
  {
    string[] defs = str.Split('|');
    AbstractRoomNode[] nodes = new AbstractRoomNode[int.Parse(defs[1], CultureInfo.InvariantCulture)];
    int size = int.Parse(defs[2], CultureInfo.InvariantCulture);
    for (int i = 0; i < nodes.Length; ++i)
    {
      string[] roomValues = defs[i + 3].Split(',');
      int[,,] connectivity = (nodes[i] =
        new(new(ExtEnum<AbstractRoomNode.Type>.values.GetEntry(int.Parse(roomValues[0], CultureInfo.InvariantCulture))),
        int.Parse(roomValues[1], CultureInfo.InvariantCulture),
        nodes.Length,
        int.Parse(roomValues[2], CultureInfo.InvariantCulture) == 1,
        int.Parse(roomValues[3], CultureInfo.InvariantCulture),
        int.Parse(roomValues[4], CultureInfo.InvariantCulture))).connectivity;
      for (int j = 0, offset = 5; j < size; ++j, offset += nodes.Length)
        for (int k = 0; k < nodes.Length; ++k)
        {
          string[] values = roomValues[offset + k].Split(' ');
          connectivity[j, k, 0] = int.Parse(values[0], CultureInfo.InvariantCulture);
          connectivity[j, k, 1] = int.Parse(values[1], CultureInfo.InvariantCulture);
        }
    }
    return nodes;
  }
}