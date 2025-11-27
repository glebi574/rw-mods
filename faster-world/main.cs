// #define _DEV

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
  public const string PLUGIN_GUID = "gelbi.faster-world";
  public const string PLUGIN_NAME = "Faster World";
  public const string PLUGIN_VERSION = "1.0.6";

  public static bool isInit = false;

  public void OnEnable()
  {
    Log = Logger;

    On.Futile.ctor += Futile_ctor;
  }

  public void Futile_ctor(On.Futile.orig_ctor orig, Futile self)
  {
    orig(self);

    if (isInit)
      return;
    isInit = true;

    try
    {
      IL.ModManager.RefreshModsLists += M_ModManager.ModManager_RefreshModsLists;
      new NativeDetour(typeof(ModManager).GetMethod("ComputeModChecksum"), typeof(M_ModManager).GetMethod("ComputeModChecksum"));

      new NativeDetour(typeof(PhysicalObject).GetMethod("WeightedPush"), typeof(M_Math).GetMethod("PhysicalObject_WeightedPush"));
      new NativeDetour(typeof(PhysicalObject).GetMethod("IsTileSolid"), typeof(M_Math).GetMethod("PhysicalObject_IsTileSolid"));
      new NativeDetour(typeof(BodyPart).GetMethod("PushOutOfTerrain"), typeof(M_Math).GetMethod("BodyPart_PushOutOfTerrain"));
      new NativeDetour(typeof(BodyPart).GetMethod("OnOtherSideOfTerrain"), typeof(M_Math).GetMethod("BodyPart_OnOtherSideOfTerrain"));

      new NativeDetour(typeof(Dangler).GetMethod("DrawSprite"), typeof(M_Graphics).GetMethod("Dangler_DrawSprite"));
      new NativeDetour(typeof(Dangler.DanglerSegment).GetMethod("Update"), typeof(M_Graphics).GetMethod("DanglerSegment_Update"));

#if _DEV
      Profiler.PatchMethods([
        // typeof(RainWorldGame).GetConstructor([typeof(ProcessManager)]),
        typeof(CreatureSpecificAImap).GetConstructor([typeof(AImap), typeof(CreatureTemplate)])
      ]);

      Profiler.PatchMethods([
        M_World.AccessibilityDijkstraMapper_Update,
        M_World.CreatureSpecificAImap_ctor,
      ]);
#endif

      new NativeDetour(typeof(WorldLoader).GetMethod("CappingBrokenExits", BindingFlags.Instance | BindingFlags.NonPublic),
        typeof(M_World).GetMethod("WorldLoader_CappingBrokenExits"));
      new NativeDetour(typeof(RoomPreprocessor).GetMethod("ConnMapToString"), typeof(M_World).GetMethod("RoomPreprocessor_ConnMapToString"));
      new NativeDetour(typeof(RoomPreprocessor).GetMethod("CompressAIMapsToString"), typeof(M_World).GetMethod("RoomPreprocessor_CompressAIMapsToString"));
      new NativeDetour(typeof(RoomPreprocessor).GetMethod("IntArrayToString"), typeof(M_World).GetMethod("RoomPreprocessor_IntArrayToString"));
      new NativeDetour(typeof(RoomPreprocessor).GetMethod("FloatArrayToString"), typeof(M_World).GetMethod("RoomPreprocessor_FloatArrayToString"));
      new NativeDetour(typeof(CreatureSpecificAImap).GetConstructor([typeof(AImap), typeof(CreatureTemplate)]), typeof(M_World).GetMethod("CreatureSpecificAImap_ctor"));

      new NativeDetour(typeof(Room).GetMethod("RayTraceTilesForTerrain"), typeof(M_World).GetMethod("Room_RayTraceTilesForTerrain"));
      new NativeDetour(typeof(AImap).GetMethod("ConnectionCostForCreature"), typeof(M_World).GetMethod("AImap_ConnectionCostForCreature"));

      new NativeDetour(typeof(AImap).GetMethod("IsTooCloseToTerrain", BindingFlags.Instance | BindingFlags.NonPublic),
        typeof(M_World).GetMethod("AImap_IsTooCloseToTerrain"));
      new NativeDetour(typeof(AIdataPreprocessor.AccessibilityDijkstraMapper).GetMethod("Update"), typeof(M_World).GetMethod("AccessibilityDijkstraMapper_Update"));

      new ILHook(typeof(Room).GetMethod("Loaded"), UltimateMethodOptimizer);
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }

  public static void UltimateMethodOptimizer(ILContext _) { /* it just works */ }
}