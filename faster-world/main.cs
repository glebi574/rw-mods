//#define _DEV

using BepInEx;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;
using MonoMod.Cil;
using gelbi_silly_lib;

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
  public const string PLUGIN_GUID = "0gelbi.faster-world";
  public const string PLUGIN_NAME = "Faster World";
  public const string PLUGIN_VERSION = "1.0.8";

  public static bool isInit = false, gslEnabled = false;
  public static BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

  public void OnEnable()
  {
    Log = Logger;

    On.Futile.ctor += Futile_ctor;
  }

  public static void UltimateMethodOptimizer(ILContext _) { /* it just works */ }

  public static void Optimize<T>(string methodName) => new ILHook(typeof(T).GetMethod(methodName, flags), UltimateMethodOptimizer);

  public static Action<Delegate, Delegate> ReplaceD;
  public static Action<Type, Type[], Delegate> ReplaceC;
  public static Action<Type, string, Delegate> ReplaceF;

  public static void VersionSpecificInit()
  {
    VersionSpecific.Update("1.11.6", VersionSpecific.MismatchAction.Warn);
    ReplaceD = (from, to) => VersionSpecific.newNativeDetour(from, to);
    ReplaceC = (type, args, target) => VersionSpecific.newNativeDetour(type.GetConstructor(flags, null, args, null), target.Method);
    ReplaceF = (type, methodName, target) => VersionSpecific.newNativeDetour(type.GetMethod(methodName, flags), target.Method);
  }

  public static void SetVersionFlagImportant()
  {
    VersionSpecific.Update("1.11.6", VersionSpecific.MismatchAction.Skip);
  }

  public void Futile_ctor(On.Futile.orig_ctor orig, Futile self)
  {
    orig(self);

    if (isInit)
      return;
    isInit = true;

    try
    {
      foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        if (asm.GetName().Name == "gelbi-silly-lib-preloader")
        {
          gslEnabled = true;
          break;
        }
      if (gslEnabled)
        VersionSpecificInit();
      else
      {
        ReplaceD = (from, to) => new NativeDetour(from, to);
        ReplaceC = (type, args, target) => new NativeDetour(type.GetConstructor(flags, null, args, null), target.Method);
        ReplaceF = (type, methodName, target) => new NativeDetour(type.GetMethod(methodName, flags), target.Method);
      }

      #region 1.0.8

      IL.ModManager.RefreshModsLists += M_ModManager.ModManager_RefreshModsLists;

      ReplaceD(ModManager.LoadModFromJson, M_ModManager.LoadModFromJson);
      ReplaceD(ModManager.ComputeModChecksum, M_ModManager.ComputeModChecksum);

      ReplaceF(typeof(PhysicalObject), "WeightedPush", M_Math.PhysicalObject_WeightedPush);
      ReplaceF(typeof(PhysicalObject), "IsTileSolid", M_Math.PhysicalObject_IsTileSolid);
      ReplaceF(typeof(BodyPart), "PushOutOfTerrain", M_Math.BodyPart_PushOutOfTerrain);
      ReplaceF(typeof(BodyPart), "OnOtherSideOfTerrain", M_Math.BodyPart_OnOtherSideOfTerrain);

      ReplaceF(typeof(Dangler), "DrawSprite", M_Graphics.Dangler_DrawSprite);
      ReplaceF(typeof(Dangler.DanglerSegment), "Update", M_Graphics.DanglerSegment_Update);

      ReplaceF(typeof(WorldLoader), "CappingBrokenExits", M_World.WorldLoader_CappingBrokenExits);
      ReplaceC(typeof(CreatureSpecificAImap), [typeof(AImap), typeof(CreatureTemplate)], M_World.CreatureSpecificAImap_ctor);
      ReplaceF(typeof(RoomPreprocessor), "ConnMapToString", M_World.RoomPreprocessor_ConnMapToString);
      ReplaceF(typeof(RoomPreprocessor), "CompressAIMapsToString", M_World.RoomPreprocessor_CompressAIMapsToString);
      ReplaceF(typeof(RoomPreprocessor), "IntArrayToString", M_World.RoomPreprocessor_IntArrayToString);
      ReplaceF(typeof(RoomPreprocessor), "FloatArrayToString", M_World.RoomPreprocessor_FloatArrayToString);

      ReplaceF(typeof(Room), "RayTraceTilesForTerrain", M_World.Room_RayTraceTilesForTerrain);
      ReplaceF(typeof(AImap), "ConnectionCostForCreature", M_World.AImap_ConnectionCostForCreature);
      ReplaceF(typeof(AIdataPreprocessor.AccessibilityDijkstraMapper), "Update", M_World.AccessibilityDijkstraMapper_Update);

      if (gslEnabled)
        SetVersionFlagImportant();

      ReplaceF(typeof(PlayerProgression), "SaveDeathPersistentDataOfCurrentState", M_Save.PlayerProgression_SaveDeathPersistentDataOfCurrentState);

      Optimize<Room>("Loaded");

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