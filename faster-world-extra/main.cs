//#define _DEV

global using static faster_world_extra.CommonWrapper;
using BepInEx;
using gelbi_silly_lib;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

#if _DEV
using profiler;
#endif

namespace faster_world_extra
{
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
  }

  [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
  public class Plugin : BaseUnityPlugin
  {
    public const string PLUGIN_GUID = "gelbi.faster-world-extra", PLUGIN_NAME = "Faster World Extra", PLUGIN_VERSION = "1.0.0", targetVersion = "1.11.8";
    public static bool isInit = false, gslEnabled = false;
    public static BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    public void OnEnable()
    {
      Log = Logger;

      try
      {
        On.Futile.ctor += Futile_ctor;
      }
      catch (Exception e)
      {
        Logger.LogError(e);
      }
    }
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

    public static void Futile_ctor(On.Futile.orig_ctor orig, Futile self)
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

        #region 1.0.0

        ReplaceF(typeof(WorldLoader), "FindRoomFile", M_Assets.WorldLodaer_FindRoomFile);
        ReplaceFL(typeof(AssetManager), "ResolveFilePath", [typeof(string), typeof(bool), typeof(bool)], M_Assets.AssetManager_ResolveFilePath);

        IL.ModManager.RefreshModsLists += M_Assets.ModManager_RefreshModsLists;

        #endregion

#if _DEV
        //MethodPatcher.PatchMethods([
        //  typeof(WorldLoader).GetMethod("FindRoomFile", flags),
        //  typeof(AssetManager).GetMethod("ResolveFilePath", flags, null, [typeof(string), typeof(bool), typeof(bool)], null),
        //]);

        MethodPatcher.PatchMethods([
          M_Assets.AssetManager_ResolveFilePath,
        ]);
#endif
      }
      catch (Exception e)
      {
        Log.LogError(e);
      }
    }
  }
}