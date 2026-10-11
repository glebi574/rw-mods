using RWCustom;
using System.Collections.Generic;
using System.IO;

namespace gelbi_silly_lib;

public static class FileUtils
{
  public enum Result
  {
    /// <summary>
    /// Directory at path [with given search options] doesn't exist
    /// </summary>
    NoDirectory,
    /// <summary>
    /// Files [with given search options] exist and were successfully retrieved
    /// </summary>
    Success,
    /// <summary>
    /// Directory at path [with given search options] exists, but doesn't contain any files
    /// </summary>
    NoFiles,
    /// <summary>
    /// Directory at path [with given search options] exists, but doesn't contain any files with required extension
    /// </summary>
    NoFilesWithExtension
  }

  /// <summary>
  /// Returns list with all files, local path of which matches specified one, relative to StreamingAssets, mergedmods and mod folders with their versioned paths
  /// </summary>
  /// <param name="path">Path to match</param>
  /// <param name="moddedOnly">If <c>true</c>, doesn't check StreamingAssets and mergedmods</param>
  /// <returns></returns>
  public static List<string> ResolveAllFilePaths(string path, bool moddedOnly = false)
  {
    path = path.Replace('/', Path.DirectorySeparatorChar).ToLowerInvariant();
    List<string> paths = [];
    foreach (ModManager.Mod mod in ModManager.ActiveMods)
      foreach (string versionedPath in mod.GetVersionedFiles(path))
        paths.Add(versionedPath);
    if (!moddedOnly)
    {
      string localPath;
      if (File.Exists(localPath = Path.Combine(Custom.rootFolderDirectory, "mergedmods", path)))
        paths.Add(localPath);
      if (File.Exists(localPath = Path.Combine(Custom.rootFolderDirectory, path)))
        paths.Add(localPath);
    }
    return paths;
  }

  /// <summary>
  /// Returns list with all files, local path of which matches specified one, relative to StreamingAssets, mergedmods and mod folders with their versioned paths
  /// <para>Only single file with highest priority will be added per mod</para>
  /// </summary>
  /// <param name="path">Path to match</param>
  /// <param name="moddedOnly">If <c>true</c>, doesn't check StreamingAssets and mergedmods</param>
  /// <returns></returns>
  public static List<string> ResolveVersionedFilePaths(string path, bool moddedOnly = false)
  {
    path = path.Replace('/', Path.DirectorySeparatorChar).ToLowerInvariant();
    List<string> paths = [];
    foreach (ModManager.Mod mod in ModManager.ActiveMods)
      if (mod.GetVersionedFile(path) is string versionedPath)
        paths.Add(versionedPath);
    if (!moddedOnly)
    {
      string localPath;
      if (File.Exists(localPath = Path.Combine(Custom.rootFolderDirectory, "mergedmods", path)))
        paths.Add(localPath);
      if (File.Exists(localPath = Path.Combine(Custom.rootFolderDirectory, path)))
        paths.Add(localPath);
    }
    return paths;
  }

  /// <summary>
  /// Lists all files/directories in directory(and/or its original directory in mod) at <paramref name="path"/>
  /// <para>Basically a copy of <see cref="AssetManager.ListDirectory(string, bool, bool, bool)"/> with more info about result</para>
  /// </summary>
  /// <param name="path">Path of directory to list</param>
  /// <param name="opResult">Result code</param>
  /// <param name="directories">If <c>true</c>, all directories will be listed instead of files</param>
  /// <param name="includeDuplicates">If <c>true</c>, duplicates, such as if directory is introduced by mod and copied to mergedmods, will be included</param>
  /// <param name="moddedOnly">If <c>true</c>, only mod directories will be included</param>
  /// <returns></returns>
  public static List<string> ListDirectory(string path, out Result opResult, bool directories = false, bool includeDuplicates = false, bool moddedOnly = false)
  {
    List<string> paths = [], duplicateNames = [],
      targetPaths = [Path.Combine(Custom.rootFolderDirectory, "mergedmods")];
    foreach(ModManager.Mod mod in ModManager.ActiveMods)
    {
      if (mod.hasTargetedVersionFolder)
        targetPaths.Add(mod.TargetedPath);
      if (mod.hasNewestFolder)
        targetPaths.Add(mod.NewestPath);
      targetPaths.Add(mod.path);
    }
    if (!moddedOnly)
      targetPaths.Add(Custom.rootFolderDirectory);
    bool directoryExists = false;
    foreach (string localPath in targetPaths)
    {
      string fullPath = Path.Combine(localPath, path.ToLowerInvariant());
      if (Directory.Exists(fullPath))
      {
        directoryExists = true;
        foreach (string filePath in directories ? Directory.GetDirectories(fullPath) : Directory.GetFiles(fullPath))
        {
          string fileName = Path.GetFileName(filePath);
          if (includeDuplicates || !duplicateNames.Contains(fileName))
          {
            paths.Add(filePath.ToLowerInvariant());
            if (!includeDuplicates)
              duplicateNames.Add(fileName);
          }
        }
      }
    }
    opResult = directoryExists ? (paths.Count == 0 ? Result.NoFiles : Result.Success) : Result.NoDirectory;
    return paths;
  }

  /// <summary>
  /// Lists all files/directories in directory(and/or its original directory in mod) at <paramref name="path"/>, that have specified <paramref name="extension"/> 
  /// </summary>
  /// <param name="path">Path of directory to list</param>
  /// <param name="opResult">Result code</param>
  /// <param name="extension"></param>
  /// <param name="includeDuplicates">If <c>true</c>, duplicates, such as if directory is introduced by mod and copied to mergedmods, will be included</param>
  /// <param name="moddedOnly">If <c>true</c>, only mod directories will be included</param>
  /// <returns></returns>
  public static List<string> ListDirectory(string path, out Result opResult, string extension, bool includeDuplicates = false, bool moddedOnly = false)
  {
    List<string> paths = ListDirectory(path, out opResult, false, includeDuplicates, moddedOnly);
    if (opResult == Result.Success)
    {
      List<string> pathsWithExtension = [];
      foreach (string existingPath in paths)
        if (existingPath.EndsWith(extension))
          pathsWithExtension.Add(existingPath);
      paths = pathsWithExtension;
      if (paths.Count == 0)
        opResult = Result.NoFilesWithExtension;
    }
    return paths;
  }
}