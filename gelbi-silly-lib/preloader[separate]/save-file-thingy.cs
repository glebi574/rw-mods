using gelbi_silly_lib.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

/// <summary>
/// File manager, allowing to save and retrieve data, mainly for use at preloader stage.
/// <para>All overwritten non-backup files are saved in backup location.</para>
/// Backups are not restored automatically.
/// </summary>
public class SavedDataManager
{
  /// <summary>
  /// Persistent gsl local paths. All managed files are saved in these folders.
  /// </summary>
  public static readonly string localDataPath, localBackupPath;
  public const string baseFolderName = "gsl-managed", backupFolderName = "gsl-backup";
  public static bool successfullInit = false;

  public string path, backupPath;

  static SavedDataManager()
  {
    try
    {
      localDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low", "Videocult", "Rain World");
      if (!Directory.Exists(localDataPath))
      {
        LogWarning("SavedDataManager failed to find Appdata\\LocalLow");
        return;
      }

      localBackupPath = Path.Combine(localDataPath, backupFolderName);
      localDataPath = Path.Combine(localDataPath, baseFolderName);

      Directory.CreateDirectory(localDataPath);
      Directory.CreateDirectory(localBackupPath);

      if (!Directory.Exists(localDataPath))
      {
        LogWarning($"SavedDataManager failed to create {baseFolderName} folder anyhow");
        return;
      }
      if (!Directory.Exists(localBackupPath))
      {
        LogWarning($"SavedDataManager failed to create {backupFolderName} folder anyhow");
        return;
      }
    }
    catch (Exception e)
    {
      LogError(e);
      return;
    }
    successfullInit = true;
  }

  /// <summary>
  /// Defines save manager for specified file
  /// </summary>
  /// <param name="filename">Name of the file without extension</param>
  public SavedDataManager(string filename)
  {
    path = Path.Combine(localDataPath, filename + ".json");
    backupPath = Path.Combine(localBackupPath, filename + ".json");
  }

  /// <summary>
  /// Defines save manager for file at nestedFolder1/nestedFolder2/.../nestedFolderN/filename.json
  /// </summary>
  /// <param name="nestedFolders">Names of nested folders</param>
  /// <param name="filename">Name of the file without extension</param>
  public SavedDataManager(string[] nestedFolders, string filename)
  {
    path = localDataPath;
    backupPath = localBackupPath;
    foreach (string nestedFolder in nestedFolders)
    {
      path = Path.Combine(path, nestedFolder);
      Directory.CreateDirectory(path);

      backupPath = Path.Combine(backupPath, nestedFolder);
      Directory.CreateDirectory(backupPath);
    }
    path = Path.Combine(path, filename + ".json");
    backupPath = Path.Combine(backupPath, filename + ".json");
  }

  /// <summary>
  /// Reads data from the saved file
  /// </summary>
  public Dictionary<string, object> Read()
  {
    if (File.Exists(path))
      return (Dictionary<string, object>)JsonParser.Parse(File.ReadAllText(path));
    return null;
  }

  /// <summary>
  /// Saves data in json format to persistent path
  /// </summary>
  /// <param name="data">Dictionary to save</param>
  /// <param name="makeReadable">Whether to add tabs, to make output readable. All following options don't matter if set to <c>false</c></param>
  /// <param name="tabSize">Amount of spaces for each new nested thing</param>
  /// <param name="maxItemsToInline">Maximum amount of items, for which list or dictionary will be inlined. Lists or dictionaries are never inlined inside each other</param>
  /// <param name="inlineDictionaries">Whether to inline dictionaries. Depends on <paramref name="maxItemsToInline"/></param>
  public void Write(Dictionary<string, object> data, bool makeReadable = false, int tabSize = 2, int maxItemsToInline = 6, bool inlineDictionaries = false)
  {
    if (File.Exists(path))
      File.Copy(path, backupPath, true);
    File.WriteAllText(path, JsonSerializer.Serialize(data, makeReadable, tabSize, maxItemsToInline, inlineDictionaries));
  }
}