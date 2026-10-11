//#define _DEV

global using static faster_world.CommonWrapper;
#if _DEV
using profiler;
#endif

using BepInEx;
using gelbi_silly_lib;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;
using BepInEx.Bootstrap;
using System.Collections.Generic;
using Mono.Cecil;
using System.IO;
using UnityEngine;
using Watcher;
using RWCustom;
using System.Text;

namespace faster_world;

public static class CommonWrapper
{
  public static BepInEx.Logging.ManualLogSource Log;
  public static byte[] _checksumSalt = Encoding.UTF8.GetBytes(Custom.encrptString.Substring(64, 97));

  public static string SubstringUntil(this string self, char c)
  {
    int i = self.IndexOf(c);
    if (i == -1)
      return self;
    return self.Substring(0, i);
  }

  public static string SubstringUntil(this string self, string value)
  {
    int i = self.IndexOf(value);
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

  public static string[] Split(this string self, string pattern) => self.Split([pattern], StringSplitOptions.None);

  public static T[] ConcatArrays<T>(T[] array1, T[] array2)
  {
    T[] result = new T[array1.Length + array2.Length];
    Buffer.BlockCopy(array1, 0, result, 0, array1.Length);
    Buffer.BlockCopy(array2, 0, result, array1.Length, array2.Length);
    return result;
  }
  #region CLog
  public static void CLogImportant(params string[] values)
  {
    if (ModManager.DevTools)
      UnityEngine.Debug.Log(string.Concat(values));
  }

  public static void CLogImportant(params object[] values)
  {
    if (ModManager.DevTools)
      UnityEngine.Debug.Log(string.Concat(values));
  }

  public static void CLogImportant(string value)
  {
    if (ModManager.DevTools)
      UnityEngine.Debug.Log(value);
  }

  public static void CLogWarning(params string[] values)
  {
    if (ModManager.DevTools)
      UnityEngine.Debug.LogWarning(string.Concat(values));
  }

  public static void CLogWarning(params object[] values)
  {
    if (ModManager.DevTools)
      UnityEngine.Debug.LogWarning(string.Concat(values));
  }

  public static void CLogWarning(string value)
  {
    if (ModManager.DevTools)
      UnityEngine.Debug.LogWarning(value);
  }
  #endregion

  public static bool SPreloadRooms { get => Plugin.pluginInterface.preloadRooms.Value; }
  public static bool SCacheRooms { get => SPreloadRooms && Plugin.pluginInterface.cacheRooms.Value; }
  public static bool SPrecacheRooms { get => SPreloadRooms && Plugin.pluginInterface.precacheRooms.Value; }
}

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
  public const string PLUGIN_GUID = "0gelbi.faster-world", PLUGIN_NAME = "Faster World", PLUGIN_VERSION = "1.1.0", targetVersion = "1.11.8";
  public static bool isInit = false, gslEnabled = false, compatibilityMode = false;
  public static PluginInterface pluginInterface;
  public static BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
  public static HashSet<string> compatibilityAssemblies = ["CustomRegionsSupport", "RegionKit"];

  public static Action<Delegate, Delegate> ReplaceD;
  public static Action<Type, Type[], Delegate> ReplaceC;
  public delegate void ReplaceF_d(Type type, string methodName, Delegate target, bool detour = false);
  public static ReplaceF_d ReplaceF;
  public static Action<Type, string, Type[], Delegate> ReplaceFL;

  public static void VersionSpecificInit()
  {
    VersionSpecific.Update(targetVersion, VersionSpecific.MismatchAction.Warn);
    ReplaceD = static (from, to) => VersionSpecific.newNativeDetour(from, to);
    ReplaceC = static (type, args, target) => VersionSpecific.newNativeDetour(type.GetConstructor(flags, null, args, null), target.Method);
    ReplaceF = static (type, methodName, target, detour) =>
    {
      if (detour)
        VersionSpecific.newDetour(type.GetMethod(methodName, flags), target.Method);
      else
        VersionSpecific.newNativeDetour(type.GetMethod(methodName, flags), target.Method);
    };
    ReplaceFL = static (type, methodName, parameterTypes, target) => VersionSpecific.newNativeDetour(type.GetMethod(methodName, flags, null, parameterTypes, null), target.Method);
  }

  public static void SetVersionFlagImportant() => VersionSpecific.Update(targetVersion, VersionSpecific.MismatchAction.Skip);

  public void OnEnable()
  {
    Log = Logger;

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
        ReplaceD = static (from, to) => new NativeDetour(from, to);
        ReplaceC = static (type, args, target) => new NativeDetour(type.GetConstructor(flags, null, args, null), target.Method);
        ReplaceF = static (type, methodName, target, detour) =>
        {
          if (detour)
            new Detour(type.GetMethod(methodName, flags), target.Method);
          else
            new NativeDetour(type.GetMethod(methodName, flags), target.Method);
        };
        ReplaceFL = static (type, methodName, parameterTypes, target) => new NativeDetour(type.GetMethod(methodName, flags, null, parameterTypes, null), target.Method);
      }

      foreach (KeyValuePair<string, List<PluginInfo>> pluginInfo in TypeLoader.FindPluginTypes(Paths.PluginPath, Chainloader.ToPluginInfo, (Func<AssemblyDefinition, bool>)typeof(Chainloader).GetMethod("HasBepinPlugins", flags).CreateDelegate(typeof(Func<AssemblyDefinition, bool>)), "chainloader"))
        if (compatibilityAssemblies.Contains(Path.GetFileNameWithoutExtension(pluginInfo.Key)))
        {
          compatibilityMode = true;
          break;
        }

      On.Futile.ctor += Futile_ctor;
      // crs compatibility
      ReplaceF(typeof(RoomPreparer), "UpdateThread", M_Dispose.RoomPreparer_UpdateThread, compatibilityMode);
      ReplaceF(typeof(WorldLoader), "LoadAbstractRoom", M_World2.WorldLoader_LoadAbstractRoom, compatibilityMode);
    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }

  public static void OptimizeF<T>(string methodName) => typeof(T).GetMethod(methodName, flags).MethodHandle.GetFunctionPointer(); // so that's why it works
  public static void OptimizeC<T>(Type[] types) => typeof(T).GetConstructor(types).MethodHandle.GetFunctionPointer();

  public void Futile_ctor(On.Futile.orig_ctor orig, Futile self)
  {
    orig(self);

    if (isInit)
      return;
    isInit = true;

    try
    {
      #region 1.1.0

#if _DEV // will make better solution later, keeping it there for now to keep profiler data for methods invoking these correct
      OptimizeF<Room>("Loaded");
      OptimizeF<Room>("AddObject");
      OptimizeF<AbstractPhysicalObject>("Realize");
      OptimizeF<PlacedObject>("GenerateEmptyData");
      OptimizeC<RainWorldGame>([typeof(ProcessManager)]);
#endif

      IL.ModManager.RefreshModsLists += M_ModManager.ModManager_RefreshModsLists;

      ReplaceD(ModManager.LoadModFromJson, M_ModManager.LoadModFromJson);
      ReplaceD(ModManager.ComputeModChecksum, M_ModManager.ComputeModChecksum);

      ReplaceF(typeof(PhysicalObject), "WeightedPush", M_Math.PhysicalObject_WeightedPush);
      ReplaceF(typeof(PhysicalObject), "IsTileSolid", M_Math.PhysicalObject_IsTileSolid);
      ReplaceF(typeof(BodyPart), "PushOutOfTerrain", M_Math.BodyPart_PushOutOfTerrain);
      ReplaceF(typeof(BodyPart), "OnOtherSideOfTerrain", M_Math.BodyPart_OnOtherSideOfTerrain);
      ReplaceF(typeof(Mathf), "InverseLerp", M_Math.Mathf_InverseLerp);
      ReplaceF(typeof(Custom), "Md5Sum", M_Math.Custom_Md5Sum);

      ReplaceF(typeof(Dangler), "DrawSprite", M_Graphics.Dangler_DrawSprite);
      ReplaceF(typeof(Dangler.DanglerSegment), "Update", M_Graphics.DanglerSegment_Update);

      ReplaceF(typeof(WorldLoader), "CappingBrokenExits", M_World.WorldLoader_CappingBrokenExits);
      ReplaceC(typeof(CreatureSpecificAImap), [typeof(AImap), typeof(CreatureTemplate)], M_World.CreatureSpecificAImap_ctor);
      ReplaceF(typeof(RoomPreprocessor), "ConnMapToString", M_World.RoomPreprocessor_ConnMapToString);
      ReplaceF(typeof(RoomPreprocessor), "StringToConnMap", M_World.RoomPreprocessor_StringToConnMap);
      ReplaceF(typeof(RoomPreprocessor), "CompressAIMapsToString", M_World.RoomPreprocessor_CompressAIMapsToString);
      ReplaceF(typeof(RoomPreprocessor), "IntArrayToString", M_World.RoomPreprocessor_IntArrayToString);
      ReplaceF(typeof(RoomPreprocessor), "FloatArrayToString", M_World.RoomPreprocessor_FloatArrayToString);

      ReplaceF(typeof(Room), "RayTraceTilesForTerrain", M_World.Room_RayTraceTilesForTerrain);
      ReplaceF(typeof(AImap), "ConnectionCostForCreature", M_World.AImap_ConnectionCostForCreature);
      ReplaceF(typeof(World), "RegionTransportationDistanceBetweenNodes", M_World.World_RegionTransportationDistanceBetweenNodes);
      ReplaceF(typeof(AbstractSpacePathFinder), "Path", M_World.AbstractSpacePathFinder_Path, true); // rcs compatibility
      ReplaceF(typeof(ScavengerAbstractAI), "TryAssembleSquad", M_World.ScavengerAbstractAI_TryAssembleSquad);
      ReplaceF(typeof(ScavengerAbstractAI), "GoToRoom", M_World.ScavengerAbstractAI_GoToRoom);

      ReplaceF(typeof(RoomSettings), "FindParent", M_World2.RoomSettings_FindParent, compatibilityMode);

      ReplaceF(typeof(WorldLoader), "FindRoomFile", M_Assets.WorldLoader_FindRoomFile);
      ReplaceFL(typeof(AssetManager), "ResolveFilePath", [typeof(string), typeof(bool), typeof(bool)], M_Assets.AssetManager_ResolveFilePath);

      ReplaceF(typeof(BackwardsCompability), "IndexMapWorld", M_Merging.BackwardsCompability_IndexMapWorld);

      ReplaceF(typeof(AssetManager), "HardCleanFutileAssets", M_Assets.AssetManager_HardCleanFutileAssets);
      ReplaceF(typeof(DynamicLevelElement), "AddLevelCombiner", M_Dispose.DynamicLevelElement_AddLevelCombiner);

      On.RainWorldGame.ShutDownProcess += M_Dispose.RainWorldGame_ShutDownProcess;
      On.Watcher.MaskSource.ctor_Material += M_Dispose.MaskSource_ctor_Material;

      On.StaticWorld.InitStaticWorld += M_World2.StaticWorld_InitStaticWorld;
      On.OverWorld.LoadWorld_string_Name_Timeline_bool += M_World2.OverWorld_LoadWorld;
      On.WorldLoader.ReturnWorld += M_World2.WorldLoader_ReturnWorld;
      On.RoomSettings.Save_string_bool += M_World2.RoomSettings_Save_string_bool;
      IL.RoomSettings.Load_Timeline += M_World2.RoomSettings_Load_Timeline;

      if (gslEnabled)
        SetVersionFlagImportant();

      ReplaceF(typeof(PlayerProgression), "SaveDeathPersistentDataOfCurrentState", M_Save.PlayerProgression_SaveDeathPersistentDataOfCurrentState);
      ReplaceF(typeof(BackwardsCompatibilityRemix), "ParseSaveNumber", M_Save.BackwardsCompatibilityRemix_ParseSaveNumber);
      ReplaceF(typeof(PlayerProgression), "SaveToDisk", M_Save.PlayerProgression_SaveToDisk);
      ReplaceF(typeof(PlayerProgression.MiscProgressionData), "ToString", M_Save.MiscProgressionData_ToString);
      ReplaceF(typeof(SaveState), "SaveToString", M_Save.SaveState_SaveToString);

      #endregion

#if _DEV
      MethodPatcher.PatchMethods([
        //typeof(SaveState).GetMethod("SaveToString", flags),
        //typeof(ScavengerAbstractAI).GetMethod("TryAssembleSquad", flags),
        //typeof(AbstractSpacePathFinder).GetMethod("Path", flags),
        typeof(ScavengerAbstractAI.ScavengerSquad).GetMethod("CommonMovement", flags),
        //typeof(ScavengerAbstractAI).GetMethod("GoToRoom", flags),
        //typeof(World).GetMethod("RegionTransportationDistanceBetweenNodes", flags),
        //typeof(PlayerProgression.MiscProgressionData).GetMethod("ToString", flags),
        //typeof(AssetManager).GetMethod("HardCleanFutileAssets", flags),
        //typeof(Mathf).GetMethod("InverseLerp", flags),
        //typeof(RoomSettings).GetConstructor([typeof(Room), typeof(string), typeof(Region), typeof(bool), typeof(bool), typeof(SlugcatStats.Timeline), typeof(RainWorldGame)]),
      ]);

      MethodPatcher.PatchMethods([
        //M_Save.PlayerProgression_SaveToDisk,
        //M_Save.SaveState_SaveToString,
        M_World.ScavengerAbstractAI_TryAssembleSquad,
        M_World.World_RegionTransportationDistanceBetweenNodes,
        M_World.ScavengerAbstractAI_GoToRoom,
        M_World.AbstractSpacePathFinder_Path,
        //M_Math.Custom_Md5Sum,
      ]);

      //Profiler.AddTrackedMethod("ProcessManager.PreSwitchMainProcess");
      //Profiler.AddTrackedMethod("ProcessManager.PostSwitchMainProcess");
#endif
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }
}