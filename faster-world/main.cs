//#define _DEV

using BepInEx;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;
using static faster_world.LogWrapper;
using MonoMod.Cil;

#if _DEV
using profiler;
#endif

namespace faster_world;

public static class LogWrapper
{
  public static BepInEx.Logging.ManualLogSource Log;
}

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
  public const string PLUGIN_GUID = "0gelbi.faster-world";
  public const string PLUGIN_NAME = "Faster World";
  public const string PLUGIN_VERSION = "1.0.7";

  public static bool isInit = false;
  public static BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

  public void OnEnable()
  {
    Log = Logger;

    On.Futile.ctor += Futile_ctor;
  }

  public static void UltimateMethodOptimizer(ILContext _) { /* it just works */ }

  public static void Optimize<T>(string methodName) => new ILHook(typeof(T).GetMethod(methodName, flags), UltimateMethodOptimizer);

  public static void Replace<T>(string methodName, Delegate target) => new NativeDetour(typeof(T).GetMethod(methodName, flags), target.Method);

  public static void Replace<T>(Type[] args, Delegate target) => new NativeDetour(typeof(T).GetConstructor(flags, null, args, null), target.Method);

  public static void Replace(Type type, string methodName, Delegate target) => new NativeDetour(type.GetMethod(methodName, flags), target.Method);

  public void Futile_ctor(On.Futile.orig_ctor orig, Futile self)
  {
    orig(self);

    if (isInit)
      return;
    isInit = true;

    try
    {
      #region 1.0.7

      Optimize<Room>("Loaded");

      IL.ModManager.RefreshModsLists += M_ModManager.ModManager_RefreshModsLists;

      Replace<ModManager>("LoadModFromJson", M_ModManager.LoadModFromJson);
      Replace<ModManager>("ComputeModChecksum", M_ModManager.ComputeModChecksum);
      
      Replace<PhysicalObject>("WeightedPush", M_Math.PhysicalObject_WeightedPush);
      Replace<PhysicalObject>("IsTileSolid", M_Math.PhysicalObject_IsTileSolid);
      Replace<BodyPart>("PushOutOfTerrain", M_Math.BodyPart_PushOutOfTerrain);
      Replace<BodyPart>("OnOtherSideOfTerrain", M_Math.BodyPart_OnOtherSideOfTerrain);

      Replace<Dangler>("DrawSprite", M_Graphics.Dangler_DrawSprite);
      Replace<Dangler.DanglerSegment>("Update", M_Graphics.DanglerSegment_Update);
      
      Replace<WorldLoader>("CappingBrokenExits", M_World.WorldLoader_CappingBrokenExits);
      Replace<CreatureSpecificAImap>([typeof(AImap), typeof(CreatureTemplate)], M_World.CreatureSpecificAImap_ctor);
      Replace(typeof(RoomPreprocessor), "ConnMapToString", M_World.RoomPreprocessor_ConnMapToString);
      Replace(typeof(RoomPreprocessor), "CompressAIMapsToString", M_World.RoomPreprocessor_CompressAIMapsToString);
      Replace(typeof(RoomPreprocessor), "IntArrayToString", M_World.RoomPreprocessor_IntArrayToString);
      Replace(typeof(RoomPreprocessor), "FloatArrayToString", M_World.RoomPreprocessor_FloatArrayToString);

      Replace<Room>("RayTraceTilesForTerrain", M_World.Room_RayTraceTilesForTerrain);
      Replace<AImap>("ConnectionCostForCreature", M_World.AImap_ConnectionCostForCreature);
      Replace<AIdataPreprocessor.AccessibilityDijkstraMapper>("Update", M_World.AccessibilityDijkstraMapper_Update);

      Replace<PlayerProgression>("SaveDeathPersistentDataOfCurrentState", M_Save.PlayerProgression_SaveDeathPersistentDataOfCurrentState);

      #endregion

#if _DEV
      //Profiler.PatchMethods([
      //  typeof(PlayerProgression).GetMethod("SaveDeathPersistentDataOfCurrentState"),
      //]);

      Profiler.PatchMethods([
        M_Save.PlayerProgression_SaveDeathPersistentDataOfCurrentState,
      ]);
#endif
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }
}