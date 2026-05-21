using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using Steamworks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace faster_world;

public static class M_ModManager
{
  public static readonly string gameVersion = (string)typeof(RainWorld).GetField(nameof(RainWorld.GAME_VERSION_STRING)).GetValue(null);

  public static void ModManager_RefreshModsLists(ILContext il)
  {
    ILCursor c = new(il);

    if (c.TryGotoNext(i => i.MatchCall(typeof(Custom).GetMethod("RootFolderDirectory"))))
      c.Emit(OpCodes.Ldarg_0)
       .Emit(OpCodes.Call, ((Delegate)RefreshModsLists).Method)
       .Emit(OpCodes.Br, il.Instrs.First(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference m && m.Name == "_RefreshOIs"));
    if (c.TryGotoNext(i => i.OpCode == OpCodes.Ret))
      c.Emit(OpCodes.Call, ((Delegate)UpdateConsolePaths).Method);
  }

  public static void UpdateConsolePaths() =>
    M_Assets.consolefilesPath = AssetManager.GetConsoleFilesSubfolder() is string consoleFilesSubfolder
      ? Path.Combine(Custom.rootFolderDirectory, "consolefiles", consoleFilesSubfolder + '\\')
      : null;

  static void RefreshModsLists(RainWorld rainWorld)
  {
    ConcurrentDictionary<int, ModManager.Mod> storedMods = [];
    string[] modFolders = Directory.GetDirectories(Path.Combine(Custom.rootFolderDirectory, "mods"));
    int modAmount = modFolders.Length;
    Parallel.For(0, modAmount, i =>
    {
      if (ModManager.LoadModFromJson(rainWorld, modFolders[i], modFolders[i]) is ModManager.Mod mod)
        storedMods[i] = mod;
    });
    if (rainWorld.processManager?.mySteamManager != null)
    {
      PublishedFileId_t[] subscribedItems = rainWorld.processManager.mySteamManager.GetSubscribedItems();
      Parallel.For(0, subscribedItems.Length, i =>
      {
        if (!SteamUGC.GetItemInstallInfo(subscribedItems[i], out _, out string modFolder, 1024U, out _)
        || ModManager.LoadModFromJson(rainWorld, modFolder, modFolder) is not ModManager.Mod mod)
          return;
        mod.workshopId = subscribedItems[i].m_PublishedFileId;
        mod.workshopMod = true;
        storedMods[modAmount + i] = mod;
      });
      modAmount += subscribedItems.Length;
    }

    HashSet<string> modIDs = [];
    for (int i = 0; i < modAmount; ++i)
      if (storedMods.TryGetValue(i, out ModManager.Mod mod) && modIDs.Add(mod.id))
        ModManager.InstalledMods.Add(mod);
  }

  public static ModManager.Mod LoadModFromJson(RainWorld rainWorld, string modpath, string _)
  {
    string folderName = Path.GetFileName(modpath),
      folderLatest = Path.Combine(modpath, gameVersion),
      folderNewest = Path.Combine(modpath, "newest"),
      folderPath = modpath + Path.DirectorySeparatorChar,
      modInfoPath = folderPath + "modinfo.json";
    if (folderName == "versioning")
      return null;
    ModManager.Mod mod = new()
    {
      id = folderName,
      name = folderName,
      version = "",
      hideVersion = false,
      targetGameVersion = gameVersion,
      authors = "Unknown",
      description = "No Description.",
      path = modpath,
      basePath = modpath,
      checksum = "",
      checksumChanged = false,
      checksumOverrideVersion = false,
      requirements = [],
      requirementsNames = [],
      tags = [],
      priorities = [],
      workshopId = 0UL,
      workshopMod = false,
      loadOrder = 0,
      enabled = false
    };
    if (File.Exists(modInfoPath))
    {
      if (File.ReadAllText(modInfoPath).dictionaryFromJson() is not Dictionary<string, object> modInfoData)
        return null;
      void TryUpdateString(string key, ref string field)
      {
        if (modInfoData.TryGetValue(key, out object value))
          field = value.ToString();
      }
      void TryUpdateBool(string key, ref bool field)
      {
        if (modInfoData.TryGetValue(key, out object value))
          field = (bool)value;
      }
      void TryUpdateList(string key, ref string[] field)
      {
        if (!modInfoData.TryGetValue(key, out object value))
          return;
        List<object> data = (List<object>)value;
        field = new string[data.Count];
        for (int i = 0; i < field.Length; ++i)
          field[i] = data[i].ToString();
      }
      TryUpdateString("id", ref mod.id);
      TryUpdateString("name", ref mod.name);
      TryUpdateString("version", ref mod.version);
      TryUpdateString("target_game_version", ref mod.targetGameVersion);
      TryUpdateString("authors", ref mod.authors);
      TryUpdateString("description", ref mod.description);
      TryUpdateString("youtube_trailer_id", ref mod.trailerID);
      TryUpdateBool("hide_version", ref mod.hideVersion);
      TryUpdateBool("checksum_override_version", ref mod.checksumOverrideVersion);
      TryUpdateList("requirements", ref mod.requirements);
      TryUpdateList("requirements_names", ref mod.requirementsNames);
      TryUpdateList("tags", ref mod.tags);
      TryUpdateList("priorities", ref mod.priorities);
    }
    mod.modifiesRegions = Directory.Exists(folderPath + "world");
    mod.hasDLL = ModManager.ModFolderHasDLLContent(modpath);
    if (Directory.Exists(folderLatest))
      if (Directory.GetFiles(folderLatest).Length != 0)
        mod.hasTargetedVersionFolder = true;
      else
        foreach (string pluginFolder in Directory.GetDirectories(folderLatest))
        {
          string pluginFolderName = Path.GetFileName(pluginFolder).ToLower();
          if (pluginFolderName != "patchers" && pluginFolderName != "plugins")
          {
            mod.hasTargetedVersionFolder = true;
            break;
          }
        }
    if (!mod.hasTargetedVersionFolder && Directory.Exists(folderNewest))
      if (Directory.GetFiles(folderNewest).Length != 0)
        mod.hasNewestFolder = true;
      else
        foreach (string pluginFolder in Directory.GetDirectories(folderNewest))
        {
          string pluginFolderName = Path.GetFileName(pluginFolder).ToLower();
          if (pluginFolderName != "patchers" && pluginFolderName != "plugins")
          {
            mod.hasNewestFolder = true;
            break;
          }
        }
    string checksum = mod.checksumOverrideVersion || !rainWorld.options.enabledMods.Contains(mod.id)
      ? mod.version
      : ModManager.ComputeModChecksum(modpath);
    if (rainWorld.options.modChecksums.TryGetValue(mod.id, out string modChecksum))
    {
      mod.checksumChanged = checksum != modChecksum;
      if (mod.checksumChanged)
        Custom.LogImportant(["MOD CHECKSUM CHANGED FOR", mod.name, ": Was", modChecksum, ", is now", checksum]);
    }
    else
    {
      Custom.LogImportant(["MOD CHECKSUM DID NOT EXIST FOR", mod.name, ", NEWLY INSTALLED?"]);
      mod.checksumChanged = true;
    }
    mod.checksum = checksum;
    return mod;
  }

  // A bit faster than one, implemented by Merge Fix. Should also keep same checksums as it
  public static string ComputeModChecksum(string modFolder)
  {
    string modinfoPath = $"{modFolder}\\modinfo.json";
    if (File.Exists(modinfoPath) && File.ReadAllText(modinfoPath).dictionaryFromJson() is Dictionary<string, object> dictionary
      && dictionary.TryGetValue("version", out object version) && dictionary.TryGetValue("checksum_override_version", out object overrideChecksum) && (bool)overrideChecksum)
      return version.ToString();

    string relativeRoot = Custom.rootFolderDirectory.ToLowerInvariant();
    string[] fileNames = Directory.GetFiles(modFolder.ToLowerInvariant(), "*.txt", SearchOption.AllDirectories);
    Array.Sort(fileNames);
    using MD5 md = MD5.Create();
    byte[][] fileNamesBytes = new byte[fileNames.Length][], fileContentsBytes = new byte[fileNames.Length][];
    Parallel.For(0, fileNames.Length, i =>
    {
      string fileName = fileNames[i];
      fileNamesBytes[i] = Encoding.UTF8.GetBytes(relativeRoot == "" || !fileName.Contains(relativeRoot)
        ? fileName
        : fileName.Substring(fileName.IndexOf(relativeRoot) + relativeRoot.Length));
      fileContentsBytes[i] = Encoding.UTF8.GetBytes(new FileInfo(fileName).Length.ToString());
    });
    for (int i = 0; i < fileNames.Length; ++i)
    {
      md.TransformBlock(fileNamesBytes[i], 0, fileNamesBytes[i].Length, fileNamesBytes[i], 0);
      md.TransformBlock(fileContentsBytes[i], 0, fileContentsBytes[i].Length, fileContentsBytes[i], 0);
    }
    md.TransformFinalBlock([], 0, 0);
    return BitConverter.ToString(md.Hash).Replace("-", "").ToLower();
  }
}