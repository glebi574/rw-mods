using BepInEx;
using gelbi_silly_lib.Other;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

/// <summary>
/// <see cref="PluginInfo"/>, but worse and with accessible fields
/// </summary>
public class PluginData(BepInPlugin metadata, BaseUnityPlugin instance)
{
  public BepInPlugin metadata = metadata;
  public BaseUnityPlugin instance = instance;
}

/// <summary>
/// Class with some utilities for BepInPlugin
/// </summary>
public static class PluginUtils
{
  /// <summary>
  /// Dictionary with GUID - plugin pairs linked to assemblies
  /// </summary>
  public static Dictionary<Assembly, Dictionary<string, PluginData>> plugins = [];
  /// <summary>
  /// Dictionary with active assemblies linked to mod folder names
  /// </summary>
  public static Dictionary<string, HashSet<Assembly>> modAssemblies = [];

  static PluginUtils()
  {
    // BaseUnityPlugin and PluginInfo can't be hooked
    new Hook(typeof(MetadataHelper).GetMethod("GetMetadata", [typeof(object)]), MetadataHelper_GetMetadata);
  }

  static BepInPlugin MetadataHelper_GetMetadata(Func<object, BepInPlugin> orig, object plugin)
  {
    if (orig(plugin) is not BepInPlugin metadata)
      return null;
    Assembly asm = plugin.GetType().Assembly;
    modAssemblies.AddOrCreateWith(GetModFolderName(asm), asm);
    plugins.AddOrCreateWith(asm, metadata.GUID, new(metadata, (BaseUnityPlugin)plugin));
    return metadata;
  }

  /// <summary>
  /// Returns defining mod  folder name of assembly
  /// </summary>
  public static string GetModFolderName(this Assembly asm)
  {
    DirectoryInfo folder = Directory.GetParent(asm.Location).Parent;
    return folder.Parent.Name switch
    {
      "mods" or "312520" => folder.Name,
      _ => folder.Parent.Name
    };
  }
}
