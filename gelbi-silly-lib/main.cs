using BepInEx;
using System;

namespace gelbi_silly_lib;

public static class LogWrapper
{
  public static BepInEx.Logging.ManualLogSource Log;
}

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
  public const string PLUGIN_GUID = "0gelbi.silly-lib";
  public const string PLUGIN_NAME = "gelbi's Silly Lib";
  public const string PLUGIN_VERSION = "1.0.5";

  public static PluginInterface pluginInterface;
  public static bool isInit = false;

  public void OnEnable()
  {
    if (isInit)
      return;
    isInit = true;

    try
    {
      LogWrapper.Log = Logger;
      HighPriorityMods.Add(PLUGIN_GUID);

      On.RainWorld.OnModsInit += RainWorld_OnModsInit;
      On.RainWorld.PostModsInit += RainWorld_PostModsInit;
    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }

  public void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
  {
    orig(self);

    try
    {
      pluginInterface = new PluginInterface();
      MachineConnector.SetRegisteredOI(PLUGIN_GUID, pluginInterface);
    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }

  public void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
  {
    orig(self);

    try
    {
      On.RainWorld.PostModsInit -= RainWorld_PostModsInit;
      IL.Menu.Remix.MenuModList.RefreshAllButtons += HighPriorityMods.MenuModList_RefreshAllButtons;
    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }
}