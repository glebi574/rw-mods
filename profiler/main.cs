using BepInEx;
using System;
using System.Diagnostics;
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
  public const string PLUGIN_VERSION = "1.0.1";

  public static bool isInit = false;

  public void OnEnable()
  {
    if (isInit)
      return;
    isInit = true;

    Log = Logger;

    On.RainWorld.OnModsInit += RainWorld_OnModsInit;
    On.Menu.InitializationScreen.Update += InitializationScreen_Update;
  }

  public void InitializationScreen_Update(On.Menu.InitializationScreen.orig_Update orig, Menu.InitializationScreen self)
  {
    long time = Stopwatch.GetTimestamp();
    orig(self);
    double executionTime = (double)(Stopwatch.GetTimestamp() - time) / Stopwatch.Frequency;
    string msg = $"Finished initialization step {self.currentStep} in {executionTime} seconds";
    Log.LogInfo(msg);
    Profiler.Log(msg);
  }

  public void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
  {
    orig(self);

    try
    {

    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }
}
