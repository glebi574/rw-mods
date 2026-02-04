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
using static faster_world.LogWrapper;

namespace faster_world;

public static class M_ModManager
{
  public static readonly string gameVersion = (string)typeof(RainWorld).GetField(nameof(RainWorld.GAME_VERSION_STRING)).GetValue(null);

  public static void ModManager_RefreshModsLists(ILContext il)
  {
    ILCursor c = new(il);

    if (c.TryGotoNext(
      i => i.MatchCall(typeof(Custom).GetMethod("RootFolderDirectory"))))
    {
      c.Emit(OpCodes.Ldarg_0);
      c.EmitDelegate(RefreshModsLists);
      c.Emit(OpCodes.Br, il.Instrs.First(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference m && m.Name == "_RefreshOIs"));
    }
  }

  public static void RefreshModsLists(RainWorld rainWorld)
  {
    // I dare you to check original implementation
    ConcurrentDictionary<int, ModManager.Mod> storedMods = new();
    string[] modFolders = Directory.GetDirectories($"{Custom.RootFolderDirectory()}{Path.DirectorySeparatorChar}mods");
    int modAmount = modFolders.Length;
    Parallel.For(0, modFolders.Length, i =>
    {
      ModManager.Mod mod = ModManager.LoadModFromJson(rainWorld, modFolders[i], modFolders[i]);
      if (mod != null)
        storedMods[i] = mod;
    });
    if (rainWorld.processManager?.mySteamManager != null)
    {
      PublishedFileId_t[] subscribedItems = rainWorld.processManager.mySteamManager.GetSubscribedItems();
      Parallel.For(0, subscribedItems.Length, i =>
      {
        if (!SteamUGC.GetItemInstallInfo(subscribedItems[i], out _, out string modFolder, 1024U, out _))
          return;
        ModManager.Mod mod = ModManager.LoadModFromJson(rainWorld, modFolder, modFolder);
        if (mod == null)
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

  public static void AssignStringList(ref string[] target, object jsonData)
  {
    List<object> data = (List<object>)jsonData;
    target = new string[data.Count];
    for (int i = 0; i < target.Length; ++i)
      target[i] = data[i].ToString();
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
      modifiesRegions = false,
      workshopId = 0UL,
      workshopMod = false,
      hasDLL = false,
      loadOrder = 0,
      enabled = false
    };
    if (File.Exists(modInfoPath))
    {
      Dictionary<string, object> modInfoData = File.ReadAllText(modInfoPath).dictionaryFromJson();
      if (modInfoData == null)
        return null;
      foreach (KeyValuePair<string, object> kvp in modInfoData)
        switch (kvp.Key)
        {
          case "id":
            mod.id = kvp.Value.ToString();
            break;
          case "name":
            mod.name = kvp.Value.ToString();
            break;
          case "version":
            mod.version = kvp.Value.ToString();
            break;
          case "hide_version":
            mod.hideVersion = (bool)kvp.Value;
            break;
          case "target_game_version":
            mod.targetGameVersion = kvp.Value.ToString();
            break;
          case "authors":
            mod.authors = kvp.Value.ToString();
            break;
          case "description":
            mod.description = kvp.Value.ToString();
            break;
          case "youtube_trailer_id":
            mod.trailerID = kvp.Value.ToString();
            break;
          case "requirements":
            AssignStringList(ref mod.requirements, kvp.Value);
            break;
          case "requirements_names":
            AssignStringList(ref mod.requirementsNames, kvp.Value);
            break;
          case "tags":
            AssignStringList(ref mod.tags, kvp.Value);
            break;
          case "priorities":
            AssignStringList(ref mod.priorities, kvp.Value);
            break;
          case "checksum_override_version":
            mod.checksumOverrideVersion = (bool)kvp.Value;
            break;
        }
    }
    if (Directory.Exists((folderPath + "world").ToLowerInvariant()))
      mod.modifiesRegions = true;
    if (ModManager.ModFolderHasDLLContent(modpath))
      mod.hasDLL = true;
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
    {
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
    if (File.Exists(modinfoPath))
    {
      Dictionary<string, object> dictionary = File.ReadAllText(modinfoPath).dictionaryFromJson();
      if (dictionary != null && dictionary.ContainsKey("version") && dictionary.TryGetValue("checksum_override_version", out object overrideChecksum) && (bool)overrideChecksum)
        return dictionary["version"].ToString();
    }

    string relativeRoot = Custom.RootFolderDirectory().ToLowerInvariant();
    string[] fileNames = Directory.GetFiles(modFolder.ToLowerInvariant(), "*.txt", SearchOption.AllDirectories);
    Array.Sort(fileNames);
    MD5 md = MD5.Create();
    byte[][] fileNamesBytes = new byte[fileNames.Length][], fileContentsBytes = new byte[fileNames.Length][];
    Parallel.For(0, fileNames.Length, i =>
    {
      string fileName = fileNames[i];
      if (relativeRoot == "" || !fileName.Contains(relativeRoot))
        fileNamesBytes[i] = Encoding.UTF8.GetBytes(fileName);
      else
        fileNamesBytes[i] = Encoding.UTF8.GetBytes(fileName.Substring(fileName.IndexOf(relativeRoot) + relativeRoot.Length));
      fileContentsBytes[i] = Encoding.UTF8.GetBytes(new FileInfo(fileName).Length.ToString());
    });
    for (int i = 0; i < fileNames.Length; ++i)
    {
      md.TransformBlock(fileNamesBytes[i], 0, fileNamesBytes[i].Length, fileNamesBytes[i], 0);
      md.TransformBlock(fileContentsBytes[i], 0, fileContentsBytes[i].Length, fileContentsBytes[i], 0);
    }
    md.TransformFinalBlock([], 0, 0);
    string result = BitConverter.ToString(md.Hash).Replace("-", "").ToLower();
    md.Dispose();
    return result;
  }
}