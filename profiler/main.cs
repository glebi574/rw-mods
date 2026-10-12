global using static profiler.LogWrapper;
using BepInEx;
using gelbi_silly_lib.SavedDataManagerExtensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace profiler;

public static class LogWrapper
{
  public static BepInEx.Logging.ManualLogSource Log;
}

public class ProfilerOIBinder(ProfilerSettings manager, PluginInterface oi) : BaseOIBinder<ProfilerSettings, PluginInterface>(manager, oi)
{
  public override void RemixLoad(Dictionary<string, object> data)
  {
    BaseLoad(data);
    oi.profileGlobal.Value = Patcher.settings.profileGlobal;
    oi.conditionalProfileGlobal.Value = Patcher.settings.conditionalProfileGlobal;
    oi.profileMods.Value = Patcher.settings.profileMods;
    oi.profileModInit.Value = Patcher.settings.profileModInit;
    oi.profileMemory.Value = Patcher.settings.profileMemory;
  }

  public override void RemixSave()
  {
    Data["profileGlobal"] = oi.profileGlobal.Value;
    Data["conditionalProfileGlobal"] = oi.conditionalProfileGlobal.Value;
    Data["profileMods"] = oi.profileMods.Value;
    Data["profileModInit"] = oi.profileModInit.Value;
    Data["profileMemory"] = oi.profileMemory.Value;
    Write();
  }
}

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
  public const string PLUGIN_GUID = "gelbi.profiler", PLUGIN_NAME = "Profiler", PLUGIN_VERSION = "1.0.4";

  public static PluginInterface pluginInterface;
  public static bool isInit = false;

  public void OnEnable()
  {
    Log = Logger;

    try
    {
      On.RainWorld.OnModsInit += RainWorld_OnModsInit;
      if (Patcher.settings.profileModInit)
        On.Menu.InitializationScreen.Update += InitializationScreen_Update;
      gelbi_silly_lib.GSLTools.toolDefs["profiler.reset"] = gelbi_silly_lib.GSLTools.toolDefs["profiler.r"] = () =>
      {
        foreach (MethodStats method in Profiler.methodStats)
        {
          method.maxTime = method.totalTime = method.invokeCounter = 0;
          method.minTime = long.MaxValue;
        }
      };
    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }

  public static void InitializationScreen_Update(On.Menu.InitializationScreen.orig_Update orig, Menu.InitializationScreen self)
  {
    Stopwatch stopwatch = Stopwatch.StartNew();
    Menu.InitializationScreen.InitializationStep step = self.currentStep;
    orig(self);
    string msg = $"Finished initialization step {step} in {stopwatch.Elapsed.TotalSeconds:F6} seconds";
    Log.LogInfo(msg);
    Profiler.Log(msg);
  }

  public static void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
  {
    orig(self);

    if (isInit)
      return;
    isInit = true;

    try
    {
      pluginInterface = new();
      MachineConnector.SetRegisteredOI(PLUGIN_GUID, pluginInterface);
      new ProfilerOIBinder(Patcher.settings, pluginInterface);
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }
}