using BepInEx;
using IL.Watcher;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace casual_world
{
  [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
  public class Plugin : BaseUnityPlugin
  {
    public const string PLUGIN_GUID = "gelbi.casual_world";
    public const string PLUGIN_NAME = "Casual World";
    public const string PLUGIN_VERSION = "1.0.1";

    public PluginInterface pluginInterface;
    public Hooks hooks;

    public void OnEnable()
    {
      On.RainWorld.OnModsInit += RainWorld_OnModsInit;
    }

    private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
      orig(self);

      pluginInterface = new();
      MachineConnector.SetRegisteredOI(PLUGIN_GUID, pluginInterface);

      hooks = new(pluginInterface);
      hooks.Apply();

      Watcher.LoachAI.Behavior.Hunt = Watcher.LoachAI.Behavior.Idle;
    }
  }
}
