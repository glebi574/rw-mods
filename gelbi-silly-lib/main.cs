using BepInEx;
using System;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
  public const string PLUGIN_GUID = "0gelbi.silly-lib";
  public const string PLUGIN_NAME = "gelbi's Silly Lib";
  public const string PLUGIN_VERSION = "1.0.7";

  public static PluginInterface pluginInterface;
  public static bool isInit = false;

  public void OnEnable()
  {
    if (isInit)
      return;
    isInit = true;

    try
    {
      SetImplementation(Logger.LogInfo, Logger.LogMessage, Logger.LogWarning, Logger.LogError, Logger.LogFatal, Logger.LogDebug);

      On.RainWorld.OnModsInit += RainWorld_OnModsInit;
    }
    catch (Exception e)
    {
      LogError(e);
    }
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
      LogError(e);
    }
  }
}