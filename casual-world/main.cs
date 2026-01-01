using BepInEx;
using System;
using static casual_world.LogWrapper;

namespace casual_world;

public static class LogWrapper
{
  public static BepInEx.Logging.ManualLogSource Log;
}

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
  public const string PLUGIN_GUID = "gelbi.casual_world";
  public const string PLUGIN_NAME = "Casual World";
  public const string PLUGIN_VERSION = "1.0.2";

  public static bool isInit = false;
  public PluginInterface pluginInterface;

  public void OnEnable()
  {
    try
    {
      Log = Logger;

      On.RainWorld.OnModsInit += RainWorld_OnModsInit;
    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }

  public void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
  {
    orig(self);

    if (isInit)
      return;
    isInit = true;

    try
    {
      MachineConnector.SetRegisteredOI(PLUGIN_GUID, pluginInterface = new());

      new Hooks(pluginInterface).Apply();

      Watcher.LoachAI.Behavior.Hunt = Watcher.LoachAI.Behavior.Idle;
    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }
}
