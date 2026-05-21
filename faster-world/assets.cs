using RWCustom;
using System.IO;

namespace faster_world;

public static class M_Assets
{
  public static string mergedmodsPath, consolefilesPath;

  static M_Assets()
  {
    mergedmodsPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "mergedmods\\");
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
    if (File.Exists(text = mergedmodsPath + path))
      return text;
    for (int i = ModManager.ActiveMods.Count - 1; i >= 0; --i)
      if ( ModManager.ActiveMods[i].hasTargetedVersionFolder
        && File.Exists(text = Path.Combine(ModManager.ActiveMods[i].TargetedPath, path))
        || ModManager.ActiveMods[i].hasNewestFolder
        && File.Exists(text = Path.Combine(ModManager.ActiveMods[i].NewestPath, path))
        || File.Exists(text = Path.Combine(ModManager.ActiveMods[i].path, path)))
        return text;
    if ( consolefilesPath != null
      && File.Exists(text = consolefilesPath + path)
      || File.Exists(text = Path.Combine(Custom.rootFolderDirectory, path)))
      return text;
    return null;
  }

  public static string AssetManager_ResolveFilePath(string path, bool skipMergedMods = false, bool skipConsoleFiles = false)
  {
    string text;
    path = path.ToLowerInvariant();
    if (!skipMergedMods && File.Exists(text = mergedmodsPath + path))
      return text;
    for (int i = ModManager.ActiveMods.Count - 1; i >= 0; --i)
      if ( ModManager.ActiveMods[i].hasTargetedVersionFolder
        && File.Exists(text = Path.Combine(ModManager.ActiveMods[i].TargetedPath, path))
        || ModManager.ActiveMods[i].hasNewestFolder
        && File.Exists(text = Path.Combine(ModManager.ActiveMods[i].NewestPath, path))
        || File.Exists(text = Path.Combine(ModManager.ActiveMods[i].path, path)))
        return text;
    if (!skipConsoleFiles && consolefilesPath != null && File.Exists(text = consolefilesPath + path))
      return text;
    return Path.Combine(Custom.rootFolderDirectory, path);
  }
}