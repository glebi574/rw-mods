global using static paletteless_world.LogWrapper;
using BepInEx;
using RWCustom;
using System;
using System.Collections.Generic;
using System.IO;

namespace paletteless_world;

public static class LogWrapper
{
  public static BepInEx.Logging.ManualLogSource Log;
}

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
  public const string PLUGIN_GUID = "gelbi.paletteless-world", PLUGIN_NAME = "Paletteless World", PLUGIN_VERSION = "1.0.0";
  public static bool isInit = false;

  public static PluginInterface pluginInterface;
  public static HashSet<string> palettes = [];

  public void OnEnable()
  {
    try
    {
      Log = Logger;

      On.RainWorld.OnModsInit += RainWorld_OnModsInit;
      On.ModManager.RefreshModsLists += ModManager_RefreshModsLists;
      On.RoomCamera.ChangeBothPalettes += RoomCamera_ChangeBothPalettes;
      On.RoomCamera.ChangeMainPalette += RoomCamera_ChangeMainPalette;
      On.RoomCamera.ChangeFadePalette += RoomCamera_ChangeFadePalette;
    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }

  public static void RoomCamera_ChangeFadePalette(On.RoomCamera.orig_ChangeFadePalette orig, RoomCamera self, int palB, float blend)
    => orig(self, pluginInterface.FadePalette, blend);
  public static void RoomCamera_ChangeMainPalette(On.RoomCamera.orig_ChangeMainPalette orig, RoomCamera self, int palA)
    => orig(self, pluginInterface.Palette);
  public static void RoomCamera_ChangeBothPalettes(On.RoomCamera.orig_ChangeBothPalettes orig, RoomCamera self, int palA, int palB, float blend)
    => orig(self, pluginInterface.Palette, pluginInterface.FadePalette, blend);

  public static void ModManager_RefreshModsLists(On.ModManager.orig_RefreshModsLists orig, RainWorld rainWorld)
  {
    orig(rainWorld);
    palettes.Clear();
    foreach (string path in Directory.GetFiles(Path.Combine(Custom.rootFolderDirectory, "palettes"), "palette*.png"))
      palettes.Add(Path.GetFileNameWithoutExtension(path).Substring(7));
    foreach (string path in Directory.GetFiles(Path.Combine(Custom.rootFolderDirectory, "mergedmods\\palettes"), "palette*.png"))
      palettes.Add(Path.GetFileNameWithoutExtension(path).Substring(7));
  }

  public static void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
  {
    orig(self);

    if (isInit)
      return;
    isInit = true;

    try
    {
      MachineConnector.SetRegisteredOI(PLUGIN_GUID, pluginInterface = new());
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }
}