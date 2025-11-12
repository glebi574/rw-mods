// #define _DEV

using BepInEx;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;
using static faster_world.LogWrapper;

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
  public const string PLUGIN_VERSION = "1.0.5";

  public static bool isInit = false;

  public void OnEnable()
  {
    if (isInit)
      return;
    isInit = true;

    Log = Logger;

    On.Futile.ctor += Futile_ctor;
  }

  public static int i = 0;
  public static string[] st = new string[2];

  public void Futile_ctor(On.Futile.orig_ctor orig, Futile self)
  {
    orig(self);

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
      
      new NativeDetour(typeof(WorldLoader).GetMethod("CappingBrokenExits", BindingFlags.Instance | BindingFlags.NonPublic),
        typeof(M_World).GetMethod("WorldLoader_CappingBrokenExits"));
      new NativeDetour(typeof(RoomPreprocessor).GetMethod("ConnMapToString"), typeof(M_World).GetMethod("RoomPreprocessor_ConnMapToString"));
      new NativeDetour(typeof(RoomPreprocessor).GetMethod("CompressAIMapsToString"), typeof(M_World).GetMethod("RoomPreprocessor_CompressAIMapsToString"));
      new NativeDetour(typeof(RoomPreprocessor).GetMethod("IntArrayToString"), typeof(M_World).GetMethod("RoomPreprocessor_IntArrayToString"));
      new NativeDetour(typeof(RoomPreprocessor).GetMethod("FloatArrayToString"), typeof(M_World).GetMethod("RoomPreprocessor_FloatArrayToString"));
      new NativeDetour(typeof(CreatureSpecificAImap).GetConstructor([typeof(AImap), typeof(CreatureTemplate)]), typeof(M_World).GetMethod("CreatureSpecificAImap_ctor"));

#if _DEV
      Profiler.PatchMethods(
        typeof(RoomPreprocessor).GetMethod("PreprocessRoom")
      );

      Profiler.PatchMethods(
        M_World.WorldLoader_CappingBrokenExits,
        M_World.RoomPreprocessor_ConnMapToString,
        M_World.RoomPreprocessor_CompressAIMapsToString,
        M_World.RoomPreprocessor_IntArrayToString,
        M_World.RoomPreprocessor_FloatArrayToString,
        M_World.CreatureSpecificAImap_ctor
      );
#endif
    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }
}