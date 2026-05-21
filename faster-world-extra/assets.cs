using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace faster_world_extra;

public static class M_Assets
{
  public static readonly string gameVersion = (string)typeof(RainWorld).GetField(nameof(RainWorld.GAME_VERSION_STRING)).GetValue(null);
  public static readonly Dictionary<string, string> cachedPaths = [];
  public static ModManager.Mod[] lastModList = [];
  public static string mergedmodsPath, consolefilesPath;

  static M_Assets()
  {
    mergedmodsPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "mergedmods\\");
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
    int versionLength = gameVersion.Length + 1;
    string modsPath = null, workshopPath = null;
    Dictionary<string, string>[] paths = new Dictionary<string, string>[ModManager.ActiveMods.Count],
      versionedPaths = new Dictionary<string, string>[paths.Length],
      newestPaths = new Dictionary<string, string>[paths.Length];
    Parallel.For(0, ModManager.ActiveMods.Count, i =>
    {
      int index = ModManager.ActiveMods.Count - i - 1;
      Dictionary<string, string> modPaths = paths[index] = [], modVersionedPaths = versionedPaths[index] = [], modNewestPaths = newestPaths[index] = [];
      string path = ModManager.ActiveMods[index].path + '\\';
      DirectoryInfo parent = Directory.GetParent(path);
      if (parent.Parent.Name == "mods")
        modsPath = parent.Parent.FullName;
      else
        workshopPath = parent.Parent.FullName;

      foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
      {
        string localPath = filePath.Substring(path.Length).ToLowerInvariant();
        modPaths[localPath] = localPath;
        if (localPath.StartsWith(gameVersion))
          modVersionedPaths[localPath.Substring(versionLength)] = localPath;
        else if (localPath.StartsWith("newest"))
          modNewestPaths[localPath.Substring(7)] = localPath;
      }
      foreach (KeyValuePair<string, string> local in modNewestPaths)
        modPaths[local.Key] = local.Value;
      foreach (KeyValuePair<string, string> local in modVersionedPaths)
        modPaths[local.Key] = local.Value;
      foreach (KeyValuePair<string, string> local in (KeyValuePair<string, string>[])[.. modPaths])
        modPaths[local.Key] = path + local.Value;
    });
    foreach (Dictionary<string, string> modPaths in paths)
      foreach (KeyValuePair<string, string> cachedPath in modPaths)
        cachedPaths[cachedPath.Key] = cachedPath.Value;
    lastModList = [.. ModManager.ActiveMods];
    consolefilesPath = AssetManager.GetConsoleFilesSubfolder() is string consoleFilesSubfolder
      ? Path.Combine(Custom.rootFolderDirectory, "consolefiles", consoleFilesSubfolder + '\\')
      : null;
  }

  public static string WorldLodaer_FindRoomFile(string roomName, bool includeRootDirectory, string additionalAppend, bool showWarning = true)
  {
    string region = (roomName = roomName.ToLowerInvariant()).SubstringUntil('_'), roomFile = roomName + additionalAppend, text;
    if ( (text = ResolveFilePath(string.Concat("world\\", region, "-rooms\\", roomFile))) != null
      || region == "gate"
      && (text = ResolveFilePath("world\\gates\\" + roomFile)) != null
      || (text = ResolveFilePath("world\\gates\\gate_shelters\\" + roomFile)) != null
      || (text = ResolveFilePath("levels\\" + roomFile)) != null
      || ModManager.MSC && roomName.Contains("challenge")
      && (text = ResolveFilePath("levels\\challenges\\" + roomFile)) != null)
      return includeRootDirectory ? "file:///" + text : text;
    return null;
  }

  public static string ResolveFilePath(string path)
  {
    string text;
    if ( File.Exists(text = mergedmodsPath + path)
      || cachedPaths.TryGetValue(path.ToLowerInvariant().Replace('/', '\\'), out text)
      || consolefilesPath != null && File.Exists(text = consolefilesPath + path)
      || File.Exists(text = Path.Combine(Custom.rootFolderDirectory, path)))
      return text;
    return null;
  }

  public static string AssetManager_ResolveFilePath(string path, bool skipMergedMods = false, bool skipConsoleFiles = false)
  {
    string text;
    path = path.ToLowerInvariant();
    if ( !skipMergedMods && File.Exists(text = mergedmodsPath + path)
      || cachedPaths.TryGetValue(path.Replace('/', '\\'), out text)
      || !skipConsoleFiles && consolefilesPath != null && File.Exists(text = consolefilesPath + path))
      return text;
    return Path.Combine(Custom.rootFolderDirectory, path);
  }
}