using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace faster_world_extra;

public static class M_Assets
{
  public static readonly string gameVersion = (string)typeof(RainWorld).GetField(nameof(RainWorld.GAME_VERSION_STRING)).GetValue(null);
  public static readonly Dictionary<string, string> mergedmodsCache = [], consolefilesCache = [];
  public static readonly ConcurrentDictionary<string, string> cachedPaths = [], modsCache = [];
  public static ModManager.Mod[] lastModList = [];
  public static string gamePath, rootfolderPath, mergedmodsPath, consolefilesPath = null;

  static M_Assets()
  {
    mergedmodsPath = Path.Combine(rootfolderPath = UnityEngine.Application.streamingAssetsPath.Replace('/', '\\').ToLowerInvariant() + '\\', "mergedmods\\");
    gamePath = Directory.GetParent(rootfolderPath).Parent.Parent.FullName + '\\';
  }

  public static void ModManager_RefreshModsLists(ILContext il)
  {
    ILCursor c = new(il);

    if (c.TryGotoNext(i => i.OpCode == OpCodes.Ret))
      c.Emit(OpCodes.Call, ((Delegate)IndexMods).Method);
  }

  public static void IndexMods()
  {
    if (lastModList.Length != ModManager.ActiveMods.Count)
      goto _index;
    for (int i = 0; i < lastModList.Length; ++i)
      if (lastModList[i].id != ModManager.ActiveMods[i].id)
        goto _index;
    return;
  _index:
    cachedPaths.Clear();
    modsCache.Clear();
    mergedmodsCache.Clear();
    consolefilesCache.Clear();
    if (consolefilesPath == null && AssetManager.GetConsoleFilesSubfolder() is string consoleFilesSubfolder)
      consolefilesPath = Path.Combine(rootfolderPath, "consolefiles", consoleFilesSubfolder + '\\');
    int versionLength = gameVersion.Length + 1;
    Dictionary<string, string>[] paths = new Dictionary<string, string>[ModManager.ActiveMods.Count];

    foreach (string filePath in Directory.EnumerateFiles(rootfolderPath))
      cachedPaths[filePath.Substring(rootfolderPath.Length).ToLowerInvariant()] = filePath;
    Parallel.ForEach(Directory.EnumerateDirectories(rootfolderPath), dirPath =>
    {
      if (Path.GetFileName(dirPath) != "mods")
        foreach (string filePath in Directory.EnumerateFiles(dirPath, "*", SearchOption.AllDirectories))
          cachedPaths[filePath.Substring(rootfolderPath.Length).ToLowerInvariant()] = filePath;
    });
    if (consolefilesPath != null)
      foreach (string filePath in Directory.EnumerateFiles(consolefilesPath, "*", SearchOption.AllDirectories))
      {
        string localPath = filePath.Substring(consolefilesPath.Length).ToLowerInvariant();
        cachedPaths[localPath] = consolefilesCache[localPath] = filePath;
      }
    Parallel.For(0, ModManager.ActiveMods.Count, i =>
    {
      int index = ModManager.ActiveMods.Count - i - 1;
      string path = ModManager.ActiveMods[index].path.Replace('/', '\\').ToLowerInvariant() + '\\';
      Dictionary<string, string> modPaths = paths[index] = [];
      List<string> localPaths = [];
      List<(string, string)> modVersionedPaths = [], modNewestPaths = [];

      // can be made 3 times faster by iterating files natively
      foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
      {
        string localPath = filePath.Substring(path.Length).ToLowerInvariant();
        modPaths[localPath] = modsCache[localPath] = filePath;
        if (localPath.StartsWith(gameVersion))
          modVersionedPaths.Add((localPath.Substring(versionLength), filePath));
        else if (localPath.StartsWith("newest"))
          modNewestPaths.Add((localPath.Substring(7), filePath));
      }
      foreach ((string localTarget, string filePath) in modNewestPaths)
        modPaths[localTarget] = modsCache[localTarget] = filePath;
      foreach ((string localTarget, string filePath) in modVersionedPaths)
        modPaths[localTarget] = modsCache[localTarget] = filePath;
    });
    foreach (Dictionary<string, string> modPaths in paths)
      foreach (KeyValuePair<string, string> cachedPath in modPaths)
        cachedPaths[cachedPath.Key] = cachedPath.Value;
    foreach (string filePath in Directory.EnumerateFiles(mergedmodsPath, "*", SearchOption.AllDirectories))
    {
      string localPath = filePath.Substring(mergedmodsPath.Length).ToLowerInvariant();
      cachedPaths[localPath] = mergedmodsCache[localPath] = filePath;
    }
    lastModList = [.. ModManager.ActiveMods];
  }

  public static string WorldLoader_FindRoomFile(string roomName, bool includeRootDirectory, string additionalAppend, bool showWarning = true)
  {
    string region = (roomName = roomName.ToLowerInvariant()).SubstringUntil('_'), roomFile = roomName + additionalAppend;
    if (cachedPaths.TryGetValue(string.Concat("world\\", region, "-rooms\\", roomFile), out string path)
     || region == "gate"
     && cachedPaths.TryGetValue("world\\gates\\" + roomFile, out path)
     || cachedPaths.TryGetValue("world\\gates\\gate_shelters\\" + roomFile, out path)
     || cachedPaths.TryGetValue("levels\\" + roomFile, out path)
     || ModManager.MSC && roomName.Contains("challenge")
     && cachedPaths.TryGetValue("levels\\challenges\\" + roomFile, out path))
      return includeRootDirectory ? "file:///" + path : path;
    return null;
  }

  // can't make paths relative, as methods that use this one may append file:/// which requires full paths, e.g. VirtualMicrophone.PlayCustom*
  public static string AssetManager_ResolveFilePath(string path, bool skipMergedMods = false, bool skipConsoleFiles = false)
  {
    path = path.Replace("//", "\\").Replace('/', '\\').ToLowerInvariant();
    if (!skipMergedMods && mergedmodsCache.TryGetValue(path, out string fullPath)
     || modsCache.TryGetValue(path, out fullPath)
     || !skipConsoleFiles && consolefilesCache.TryGetValue(path, out fullPath))
      return fullPath;
    return rootfolderPath + path;
  }

  public static void RoomSettings_Save_string_bool(On.RoomSettings.orig_Save_string_bool orig, RoomSettings self, string path, bool saveAsTemplate)
  {
    orig(self, path, saveAsTemplate);
    path = path.Replace('/', '\\').ToLowerInvariant();
    if (path.StartsWith(rootfolderPath))
    {
      if (path.StartsWith(mergedmodsPath))
      {
        string local = path.Substring(mergedmodsPath.Length);
        mergedmodsCache[local] = cachedPaths[local] = path;
      }
      cachedPaths[path.Substring(rootfolderPath.Length)] = path;
    }
    else
      IndexMods();
  }

  public static void BackwardsCompability_IndexMapWorld(int worldVersion, PlayerProgression prog, string overrideOutputPath)
  {
    StringBuilder roomIndexMap = new(), spawnerIndexMap = new();
    int roomLineCounter = 0;
    for (int i = 0; i < prog.regionNames.Length; ++i)
    {
      string regionName = prog.regionNames[i];
      string[] lines = File.ReadAllLines(AssetManager.ResolveFilePath(string.Concat("world\\", regionName, "\\world_", regionName, ".txt"), true, false));
      bool isRoomsBlock = false, isCreaturesBlock = false;
      int spawnerCounter = 0, roomBlockBase;
      foreach (string line in lines)
        if (isRoomsBlock && (roomBlockBase = line.IndexOf(" : ")) != -1)
          roomIndexMap.Append(roomLineCounter++).Append(' ').AppendLine(line.Substring(0, roomBlockBase));
        else if (isCreaturesBlock && line.Length > 2 && (line[0] != '/' || line[1] != '/'))
        {
          string baseLine = line, condition = "";
          if (line[0] == '(')
          {
            int c = line.IndexOf(')');
            if (c == -1)
              condition = line;
            else
            {
              condition = line.Substring(0, c + 1);
              baseLine = line.Substring(c + 1, line.Length - c - 1);
            }
          }
          string[] defs = baseLine.Split([" : "], StringSplitOptions.None);
          if (defs.Length < 2)
            continue;
          string param0 = defs[0], param1 = defs[1];
          if (param0 == "LINEAGE")
            spawnerIndexMap.Append(i * 1000 + spawnerCounter++).Append(' ').Append(param0).Append(condition)
              .Append(param1.Length > 3 && param1.Substring(0, 3) == "OFF" ? regionName + param1 : param1).AppendLine(defs[2]);
          else
            foreach (string value in Custom.ValidateSpacedDelimiter(param1, ",").Split([", "], StringSplitOptions.None))
              if (value.Length != 0)
                spawnerIndexMap.Append(i * 1000 + spawnerCounter++).Append(' ')
                  .Append(param0.Length > 3 && param0.Substring(0, 3) == "OFF" ? regionName + param0 : param0).Append(condition).AppendLine(value);
        }
        else switch (line)
          {
            case "ROOMS":
              isRoomsBlock = true;
              break;
            case "CREATURES":
              isCreaturesBlock = true;
              break;
            case "END ROOMS":
              isRoomsBlock = false;
              roomIndexMap.Append(roomLineCounter++).Append(' ').Append(regionName).AppendLine("OffscreenDen");
              break;
            case "END CREATURES":
              isCreaturesBlock = false;
              break;
          }
    }

    StringBuilder regionsIndexMap = new();
    int roomCounter = 0;
    foreach (string regionName in prog.regionNames)
    {
      int roomsInRegion = Region.NumberOfRoomsInRegion(regionName);
      Custom.Log($"{prog.regionNames.Length} number of rooms in {regionName} f: {roomCounter} t: {roomsInRegion}");
      regionsIndexMap.Append(roomCounter).Append(' ').AppendLine(roomsInRegion.ToString());
      roomCounter += roomsInRegion;
    }
    string basePath = overrideOutputPath ?? $"{rootfolderPath}world\\indexmaps\\";
    File.AppendAllText($"{basePath}roomIndexMap{worldVersion}.txt", roomIndexMap.ToString());
    File.AppendAllText($"{basePath}spawnerIndexMap{worldVersion}.txt", spawnerIndexMap.ToString());
    File.AppendAllText($"{basePath}regionsIndexMap{worldVersion}.txt", regionsIndexMap.ToString());
  }
}