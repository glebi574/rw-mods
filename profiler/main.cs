using BepInEx;
using System;
using static profiler.LogWrapper;

namespace profiler;

public static class LogWrapper
{
  public static BepInEx.Logging.ManualLogSource Log;
}

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
  public const string PLUGIN_GUID = "gelbi.profiler";
  public const string PLUGIN_NAME = "Profiler";
  public const string PLUGIN_VERSION = "1.0.0";

  public static bool isInit = false;

  public void OnEnable()
  {
    if (isInit)
      return;
    isInit = true;

    On.RainWorld.OnModsInit += RainWorld_OnModsInit;
  }

  public void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
  {
    orig(self);

    try
    {
      Log = Logger;


    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }
}
