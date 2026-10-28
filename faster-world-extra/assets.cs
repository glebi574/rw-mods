using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace faster_world_extra;

public static class M_Assets
{
  public static bool indexedMergedMods = false, foundWorkshopPath = false;
  public static readonly string gameVersion = (string)typeof(RainWorld).GetField(nameof(RainWorld.GAME_VERSION_STRING)).GetValue(null);
  public static readonly Dictionary<string, string> modsCache = [], mergedmodsCache = [], consolefilesCache = [];
  public static readonly ConcurrentDictionary<string, string> rootfolderCache = [];
  public static readonly HashSet<string> skippedDirectories = ["mods", "mergedmods"];
  public static ModManager.Mod[] lastModList = [];
  public static string gamePath, rootfolderPath, modsPath, workshopPath, mergedmodsPath, consolefilesPath = null;

  static M_Assets()
  {
    mergedmodsPath = Path.Combine(rootfolderPath = UnityEngine.Application.streamingAssetsPath.Replace('/', '\\') + '\\', "mergedmods\\");
    modsPath = rootfolderPath + "mods\\";
    gamePath = Directory.GetParent(rootfolderPath).Parent.Parent.FullName + '\\';
  }

  public static void ModManager_RefreshModsLists(ILContext il)
  {
    ILCursor c = new(il);

    if (c.TryGotoNext(i => i.OpCode == OpCodes.Ret))
      c.Emit(OpCodes.Call, ((Delegate)IndexStatic).Method)
       .Emit(OpCodes.Call, ((Delegate)IndexMods).Method);
  }

  public static void RainWorld_ctor(On.RainWorld.orig_ctor orig, RainWorld self)
  {
    foreach (string filePath in Directory.EnumerateFiles(rootfolderPath))
      rootfolderCache[filePath.Substring(rootfolderPath.Length).ToLowerInvariant()] = filePath;
    Parallel.ForEach(Directory.EnumerateDirectories(rootfolderPath), dirPath =>
    {
      if (!skippedDirectories.Contains(Path.GetFileName(dirPath)))
        foreach (string filePath in Directory.EnumerateFiles(dirPath, "*", SearchOption.AllDirectories))
          rootfolderCache[filePath.Substring(rootfolderPath.Length).ToLowerInvariant()] = filePath;
    });
    orig(self);
  }

  public static void ModApplyer_ctor(On.ModManager.ModApplyer.orig_ctor orig, ModManager.ModApplyer self, ProcessManager manager, List<bool> pendingEnabled, List<int> pendingLoadOrder)
  {
    indexedMergedMods = false;
    orig(self, manager, pendingEnabled, pendingLoadOrder);
  }

  public static void StaticWorld_InitStaticWorld(On.StaticWorld.orig_InitStaticWorld orig)
  {
    orig();
    IndexAssets();
  }

  public static void IndexStatic()
  {
    if (AssetManager.GetConsoleFilesSubfolder() is not string consoleFilesSubfolder)
      return;
    string newConsolefilesPath = Path.Combine(rootfolderPath, "consolefiles", consoleFilesSubfolder + '\\');
    if (consolefilesPath == newConsolefilesPath)
      return;
    consolefilesPath = newConsolefilesPath;
    consolefilesCache.Clear();
    foreach (string filePath in Directory.EnumerateFiles(consolefilesPath, "*", SearchOption.AllDirectories))
      consolefilesCache[filePath.Substring(consolefilesPath.Length).ToLowerInvariant()] = filePath;
  }

  public static void IndexAssets()
  {
    if (indexedMergedMods)
      return;
    mergedmodsCache.Clear();
    foreach (string filePath in Directory.EnumerateFiles(mergedmodsPath, "*", SearchOption.AllDirectories))
      mergedmodsCache[filePath.Substring(mergedmodsPath.Length).ToLowerInvariant()] = filePath;
    indexedMergedMods = true;
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
    modsCache.Clear();
    int versionLength = gameVersion.Length + 1;
    Dictionary<string, string>[] paths = new Dictionary<string, string>[ModManager.ActiveMods.Count];

    Parallel.For(0, ModManager.ActiveMods.Count, i =>
    {
      int index = ModManager.ActiveMods.Count - i - 1;
      string path = ModManager.ActiveMods[index].path.Replace('/', '\\').ToLowerInvariant() + '\\';
      Dictionary<string, string> modPaths = paths[index] = [];
      List<(string, string)> modVersionedPaths = [], modNewestPaths = [];
      if (!foundWorkshopPath)
      {
        DirectoryInfo parent = Directory.GetParent(path).Parent;
        if (parent.Name != "mods")
          workshopPath = parent.FullName;
      }

      // can be made 3 times faster by iterating files natively
      foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
      {
        string localPath = filePath.Substring(path.Length).ToLowerInvariant();
        modPaths[localPath] = filePath;
        int dirIndex = localPath.IndexOf('\\');
        if (dirIndex == -1)
          continue;
        string dir = localPath.Substring(0, dirIndex);
        if (dir == gameVersion)
          modVersionedPaths.Add((localPath.Substring(versionLength), filePath));
        else if (dir == "newest")
          modNewestPaths.Add((localPath.Substring(7), filePath));
      }
      foreach ((string localTarget, string filePath) in modNewestPaths)
        modPaths[localTarget] = filePath;
      foreach ((string localTarget, string filePath) in modVersionedPaths)
        modPaths[localTarget] = filePath;
    });
    foreach (Dictionary<string, string> modPaths in paths)
      foreach (KeyValuePair<string, string> cachedPath in modPaths)
        modsCache[cachedPath.Key] = cachedPath.Value;
    if (!foundWorkshopPath && workshopPath != null)
    {
      workshopPath = workshopPath.Replace('/', '\\').ToLowerInvariant() + '\\';
      foundWorkshopPath = true;
    }
    lastModList = [.. ModManager.ActiveMods];
  }

  public static string WorldLoader_FindRoomFile(string roomName, bool includeRootDirectory, string additionalAppend, bool showWarning = true)
  {
    string region = (roomName = roomName.ToLowerInvariant()).SubstringUntil('_'), roomFile = roomName + additionalAppend.ToLowerInvariant();
    if (ResolveFilePath(string.Concat("world\\", region, "-rooms\\", roomFile), out string path)
     || region == "gate"
     && ResolveFilePath("world\\gates\\" + roomFile, out path)
     || ResolveFilePath("world\\gates\\gate_shelters\\" + roomFile, out path)
     || ResolveFilePath("levels\\" + roomFile, out path)
     || ModManager.MSC && roomName.Contains("challenge")
     && ResolveFilePath("levels\\challenges\\" + roomFile, out path))
      return includeRootDirectory ? "file:///" + path : path;
    return null;
  }

  public static bool ResolveFilePath(string path, out string fullPath)
  {
    if ((indexedMergedMods ? mergedmodsCache.TryGetValue(path, out fullPath) : File.Exists(fullPath = mergedmodsPath + path))
     || modsCache.TryGetValue(path, out fullPath)
     || consolefilesCache.TryGetValue(path, out fullPath)
     || rootfolderCache.TryGetValue(path, out fullPath)
     || File.Exists(fullPath = rootfolderPath + path))
      return true;
    fullPath = null;
    return false;
  }

  // can't make paths relative, as methods that use this one may append file:/// which requires full paths, e.g. VirtualMicrophone.PlayCustom*
  public static string AssetManager_ResolveFilePath(string path, bool skipMergedMods = false, bool skipConsoleFiles = false)
  {
    path = path.Replace("//", "\\").Replace('/', '\\').Replace("\\\\", "\\").ToLowerInvariant();
    if (!skipMergedMods
     && (indexedMergedMods ? mergedmodsCache.TryGetValue(path, out string fullPath) : File.Exists(fullPath = mergedmodsPath + path))
     || modsCache.TryGetValue(path, out fullPath)
     || !skipConsoleFiles
     && consolefilesCache.TryGetValue(path, out fullPath))
      return fullPath;
    // rootfolderPath + path is a lie - it's literally same, but breaks everything for some reason
    return Path.Combine(Custom.rootFolderDirectory, path);
  }

  public static void RoomSettings_Save_string_bool(On.RoomSettings.orig_Save_string_bool orig, RoomSettings self, string path, bool saveAsTemplate)
  {
    orig(self, path, saveAsTemplate);
    path = path.Replace('/', '\\').ToLowerInvariant();
    string localPath;
    if (path.StartsWith(workshopPath))
      localPath = path.Substring(workshopPath.Length);
    else if (path.StartsWith(modsPath.ToLowerInvariant()))
      localPath = path.Substring(modsPath.Length);
    else if (path.StartsWith(rootfolderPath.ToLowerInvariant()))
      localPath = path.Substring(rootfolderPath.Length);
    else
      goto _index;
    int i = localPath.IndexOf('\\');
    if (i == -1)
      goto _eval;
    string dir = localPath.Substring(0, i);
    if (dir == gameVersion)
      localPath = localPath.Substring(gameVersion.Length + 1);
    else if (dir == "newest")
      localPath = localPath.Substring(7);
    _eval:
    if (modsCache.ContainsKey(localPath))
      goto _index;
    modsCache[localPath] = path;
    return;
  _index:
    lastModList = [];
    indexedMergedMods = false;
    IndexMods();
    IndexAssets();
  }
}