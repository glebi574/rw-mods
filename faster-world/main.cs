//#define _DEV

using BepInEx;
using gelbi_silly_lib;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

namespace faster_world;
using static CommonWrapper;

#if _DEV
using profiler;
#endif

public static class CommonWrapper
{
  public static BepInEx.Logging.ManualLogSource Log;

  public static string SubstringUntil(this string self, char c)
  {
    int i = self.IndexOf(c);
    if (i == -1)
      return self;
    return self.Substring(0, i);
  }

  public static string SubstringAfter(this string self, char c)
  {
    int i = self.IndexOf(c);
    if (i == self.Length - 1)
      return "";
    return self.Substring(i + 1);
  }
}

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
  public const string PLUGIN_GUID = "0gelbi.faster-world", PLUGIN_NAME = "Faster World", PLUGIN_VERSION = "1.0.11", targetVersion = "1.11.7b";

  public static bool isInit = false, gslEnabled = false;
  public static BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

  public void OnEnable()
  {
    Log = Logger;

    On.Futile.ctor += Futile_ctor;
  }

  public static void Optimize<T>(string methodName) => typeof(T).GetMethod(methodName, flags).MethodHandle.GetFunctionPointer(); // so that's why it works

  public static Action<Delegate, Delegate> ReplaceD;
  public static Action<Type, Type[], Delegate> ReplaceC;
  public static Action<Type, string, Delegate> ReplaceF;
  public static Action<Type, string, Type[], Delegate> ReplaceFL;

  public static void VersionSpecificInit()
  {
    VersionSpecific.Update(targetVersion, VersionSpecific.MismatchAction.Warn);
    ReplaceD = (from, to) => VersionSpecific.newNativeDetour(from, to);
    ReplaceC = (type, args, target) => VersionSpecific.newNativeDetour(type.GetConstructor(flags, null, args, null), target.Method);
    ReplaceF = (type, methodName, target) => VersionSpecific.newNativeDetour(type.GetMethod(methodName, flags), target.Method);
    ReplaceFL = (type, methodName, parameterTypes, target) => VersionSpecific.newNativeDetour(type.GetMethod(methodName, flags, null, parameterTypes, null), target.Method);
  }

  public static void SetVersionFlagImportant()
  {
    VersionSpecific.Update(targetVersion, VersionSpecific.MismatchAction.Skip);
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
        ReplaceFL = (type, methodName, parameterTypes, target) => new NativeDetour(type.GetMethod(methodName, flags, null, parameterTypes, null), target.Method);
      }

      #region 1.0.11

      Optimize<Room>("Loaded");
      Optimize<PlacedObject>("GenerateEmptyData");

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

      ReplaceF(typeof(WorldLoader), "LoadAbstractRoom", M_World2.WorldLoader_LoadAbstractRoom);
      ReplaceF(typeof(WorldLoader), "FindRoomFile", M_World2.WorldLodaer_FindRoomFile);
      ReplaceF(typeof(RoomPreprocessor), "StringToConnMap", M_World2.RoomPreprocessor_StringToConnMap);
      ReplaceF(typeof(RoomSettings), "FindParent", M_World2.RoomSettings_FindParent);
      On.StaticWorld.InitStaticWorld += M_World2.StaticWorld_InitStaticWorld;
      On.OverWorld.LoadWorld_string_Name_Timeline_bool += M_World2.OverWorld_LoadWorld;
      On.WorldLoader.ReturnWorld += M_World2.WorldLoader_ReturnWorld;
      On.RoomSettings.Save_string_bool += M_World2.RoomSettings_Save_string_bool;
      IL.RoomSettings.Load_Timeline += M_World2.RoomSettings_Load_Timeline;

      if (gslEnabled)
        SetVersionFlagImportant();

      ReplaceF(typeof(PlayerProgression), "SaveDeathPersistentDataOfCurrentState", M_Save.PlayerProgression_SaveDeathPersistentDataOfCurrentState);

      #endregion

#if _DEV
      //Profiler.PatchMethods([
      //  typeof(OverWorld).GetMethod("LoadFirstWorld", flags),
      //]);

      Profiler.PatchMethods([
        M_World2.OverWorld_LoadWorld,
        M_World2.WorldLoader_ReturnWorld,
      ]);
#endif
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }
}