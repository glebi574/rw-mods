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
  public const string PLUGIN_VERSION = "1.0.3";

  public static bool isInit = false;

  public void OnEnable()
  {
    try
    {
      if (isInit)
        return;
      isInit = true;

      LogWrapper.Log = Logger;
      HighPriorityMods.Add(PLUGIN_GUID);

      On.RainWorld.PostModsInit += RainWorld_PostModsInit;
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