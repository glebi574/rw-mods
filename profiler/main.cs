using BepInEx;
using gelbi_silly_lib;
using gelbi_silly_lib.SavedDataManagerExtensions;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static profiler.LogWrapper;

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
  }

  public override void RemixSave()
  {
    Data["profileGlobal"] = oi.profileGlobal.Value;
    Data["conditionalProfileGlobal"] = oi.conditionalProfileGlobal.Value;
    Data["profileMods"] = oi.profileMods.Value;
    Data["profileModInit"] = oi.profileModInit.Value;
    Write();
  }
}

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
  public const string PLUGIN_GUID = "gelbi.profiler";
  public const string PLUGIN_NAME = "Profiler";
  public const string PLUGIN_VERSION = "1.0.3";

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
    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }

  public void InitializationScreen_Update(On.Menu.InitializationScreen.orig_Update orig, Menu.InitializationScreen self)
  {
    Stopwatch stopwatch = Stopwatch.StartNew();
    orig(self);
    string msg = $"Finished initialization step {self.currentStep} in {stopwatch.Elapsed.TotalSeconds:F6} seconds";
    Log.LogInfo(msg);
    Profiler.Log(msg);
  }

  public void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
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
      Logger.LogError(e);
    }
  }
}
