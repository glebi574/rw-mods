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
  public const string PLUGIN_VERSION = "1.0.2";

  public static PluginInterface pluginInterface;
  public static bool isInit = false;

  public void OnEnable()
  {
    if (isInit)
      return;
    isInit = true;

    Log = Logger;
    AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

    On.RainWorld.OnModsInit += RainWorld_OnModsInit;
    if (Patcher.profileModInit)
      On.Menu.InitializationScreen.Update += InitializationScreen_Update;
  }

  public void CurrentDomain_ProcessExit(object sender, EventArgs e)
  {
    Patcher.settings["profileGlobal"] = pluginInterface.profileGlobal.Value;
    Patcher.settings["profileMods"] = pluginInterface.profileMods.Value;
    Patcher.settings["profileModInit"] = pluginInterface.profileModInit.Value;
    Patcher.saveManager.Write(Patcher.settings);
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
      pluginInterface = new();
      MachineConnector.SetRegisteredOI(PLUGIN_GUID, pluginInterface);
    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }
}
