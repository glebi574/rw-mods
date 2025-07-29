using BepInEx;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Watcher;
using Debug = UnityEngine.Debug;

namespace rot_apply_palette_fix
{
  [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
  public class Plugin : BaseUnityPlugin
  {
    public const string PLUGIN_GUID = "gelbi.rot-apply-palette-fix";
    public const string PLUGIN_NAME = "Rot ApplyPalette Fix";
    public const string PLUGIN_VERSION = "1.0.1";

    public static bool isInit = false, isGravelEaterActive = false;

    public void OnEnable()
    {
      if (isInit)
        return;
      isInit = true;

      On.RainWorld.PostModsInit += RainWorld_PostModsInit;
      HookEndpointManager.OnAdd += HookEndpointManager_OnAdd;
    }

    public bool HookEndpointManager_OnAdd(MethodBase methodBase, Delegate del)
    {
      return methodBase.DeclaringType.Name != "DaddyCorruption" || del.Method.DeclaringType.FullName != "GravelSlug.Plugin" || methodBase.Name != "InitiateSprites";
    }

    public void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
    {
      orig(self);

      try
      {
        foreach (ModManager.Mod mod in ModManager.ActiveMods)
          if (mod.id == "kingmaxthe2.gravelslug")
          {
            isGravelEaterActive = true;
            break;
          }
        if (!isGravelEaterActive)
          Logger.LogWarning("Gravel Eater isn't active, you can turn off this mod");
      }
      catch (Exception e)
      {
        Logger.LogError(e);
      }
    }
  }
}
