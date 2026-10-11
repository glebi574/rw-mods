using RWCustom;
using System.IO;
using UnityEngine;

namespace faster_world;

public static class M_Assets
{
  public static string mergedmodsPath, consolefilesPath;

  static M_Assets()
  {
    mergedmodsPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "mergedmods\\");
  }

  public static string WorldLoader_FindRoomFile(string roomName, bool includeRootDirectory, string additionalAppend, bool showWarning = true)
  {
    string region = (roomName = roomName.ToLowerInvariant()).SubstringUntil('_'), roomFile = roomName + additionalAppend, fullPath;
    if (ResolveFilePath(string.Concat("world\\", region, "-rooms\\", roomFile), out fullPath)
     || region == "gate"
     && ResolveFilePath("world\\gates\\" + roomFile, out fullPath)
     || ResolveFilePath("world\\gates\\gate_shelters\\" + roomFile, out fullPath)
     || ResolveFilePath("levels\\" + roomFile, out fullPath)
     || ModManager.MSC && roomName.Contains("challenge")
     && ResolveFilePath("levels\\challenges\\" + roomFile, out fullPath))
      return includeRootDirectory ? "file:///" + fullPath : fullPath;
    return null;
  }

  public static bool ResolveFilePath(string path, out string fullPath)
  {
    if (File.Exists(fullPath = mergedmodsPath + path))
      return true;
    for (int i = ModManager.ActiveMods.Count - 1; i >= 0; --i)
      if (ModManager.ActiveMods[i].hasTargetedVersionFolder
       && File.Exists(fullPath = Path.Combine(ModManager.ActiveMods[i].TargetedPath, path))
       || ModManager.ActiveMods[i].hasNewestFolder
       && File.Exists(fullPath = Path.Combine(ModManager.ActiveMods[i].NewestPath, path))
       || File.Exists(fullPath = Path.Combine(ModManager.ActiveMods[i].path, path)))
        return true;
    if (consolefilesPath != null
     && File.Exists(fullPath = consolefilesPath + path)
     || File.Exists(fullPath = Path.Combine(Custom.rootFolderDirectory, path)))
      return true;
    fullPath = null;
    return false;
  }

  public static string AssetManager_ResolveFilePath(string path, bool skipMergedMods = false, bool skipConsoleFiles = false)
  {
    string text;
    path = path.ToLowerInvariant();
    if (!skipMergedMods && File.Exists(text = mergedmodsPath + path))
      return text;
    for (int i = ModManager.ActiveMods.Count - 1; i >= 0; --i)
      if (ModManager.ActiveMods[i].hasTargetedVersionFolder
       && File.Exists(text = Path.Combine(ModManager.ActiveMods[i].TargetedPath, path))
       || ModManager.ActiveMods[i].hasNewestFolder
       && File.Exists(text = Path.Combine(ModManager.ActiveMods[i].NewestPath, path))
       || File.Exists(text = Path.Combine(ModManager.ActiveMods[i].path, path)))
        return text;
    if (!skipConsoleFiles && consolefilesPath != null && File.Exists(text = consolefilesPath + path))
      return text;
    return Path.Combine(Custom.rootFolderDirectory, path);
  }

  public static void AssetManager_HardCleanFutileAssets()
  {
    HeavyTexturesCache.ClearRegisteredFutileAtlases();
    HeavyTexturesCache.ClearMiscTextures();
    Futile.stage.renderer.Clear();
    Resources.UnloadUnusedAssets();
    UnityEngine.Object[] meshes = Resources.FindObjectsOfTypeAll(typeof(Mesh));
    for (int i = 0; i < meshes.Length; ++i)
    {
      if (meshes[i].name != "FRenderLayer (Futile.stage)")
        continue;
      Object.Destroy(meshes[i]);
      meshes[i] = null;
    }
    UnityEngine.Object[] materials = Resources.FindObjectsOfTypeAll(typeof(Material));
    for (int i = 0; i < materials.Length; ++i)
    {
      if (materials[i].name != "Unlit Transparent Vertex Colored")
        continue;
      Object.Destroy(materials[i]);
      materials[i] = null;
    }
  }
}