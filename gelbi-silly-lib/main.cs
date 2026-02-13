using BepInEx;
using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.SavedDataManagerExtensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

public class GSLOIBinder(GSLSettingsManager manager, PluginInterface oi) : BaseOIBinder<GSLSettingsManager, PluginInterface>(manager, oi)
{
  public override void RemixLoad(Dictionary<string, object> data)
  {
    BaseLoad(data);
    oi.wrapHooks.Value = manager.wrapHooks;
    oi.noUpdateDisable.Value = manager.noUpdateDisable;
    oi.disableEOS.Value = manager.disableEOS;
  }

  public override void RemixSave()
  {
    Data["wrapHooks"] = oi.wrapHooks.Value;
    Data["noUpdateDisable"] = oi.noUpdateDisable.Value;
    Data["disableEOS"] = oi.disableEOS.Value;
    Write();
  }
}

[BepInPlugin(Patcher.PLUGIN_GUID, Patcher.PLUGIN_NAME, Patcher.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
  public static PluginInterface pluginInterface;
  public static bool isInit = false;

  public void OnEnable()
  {
    try
    {
      SetImplementation(Logger.LogInfo, Logger.LogMessage, Logger.LogWarning, Logger.LogError, Logger.LogFatal, Logger.LogDebug, true);

      IL.RainWorld.HandleLog += RainWorld_HandleLog;
      On.RainWorld.Update += RainWorld_Update;
      On.Futile.ctor += Futile_ctor;
      On.RainWorld.OnModsInit += RainWorld_OnModsInit;
      On.OptionInterface._LoadConfigFile += OptionInterface__LoadConfigFile;
      On.OptionInterface._SaveConfigFile += OptionInterface__SaveConfigFile;
      if (GSLSettings.instance.noUpdateDisable)
        IL.ModManager.RefreshModsLists += ModManager_RefreshModsLists;
    }
    catch (Exception e)
    {
      LogError(e);
    }
  }

  static void OptionInterface__LoadConfigFile(On.OptionInterface.orig__LoadConfigFile orig, OptionInterface self)
  {
    orig(self);
    if (SavedDataManager.successfullInit && SavedDataManagerOI.managedInterfaces.TryGetValue(self, out SavedDataManagerOI managed))
      managed.load(managed.instance.Read() ?? []);
  }

  static void OptionInterface__SaveConfigFile(On.OptionInterface.orig__SaveConfigFile orig, OptionInterface self)
  {
    orig(self);
    if (SavedDataManager.successfullInit && SavedDataManagerOI.managedInterfaces.TryGetValue(self, out SavedDataManagerOI managed))
      managed.save();
  }

  static void ModManager_RefreshModsLists(ILContext il)
  {
    ILCursor c = new(il);

    if (c.TryGotoNext(i => i.OpCode == OpCodes.Ble))
    {
      c.Emit(OpCodes.Pop);
      c.Emit(OpCodes.Ldc_I4, int.MaxValue);
    }
  }

  public static Exception latestException;
  static void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
  {
    try
    {
      orig(self);
    }
    catch (Exception e)
    {
      latestException = e;
      throw;
    }
  }

  static void RainWorld_HandleLog(ILContext il)
  {
    ILCursor c = new(il);
    if (c.TryGotoNext(i => i.OpCode == OpCodes.Ldstr))
      c.EmitDelegate(() =>
      {
        if (latestException == null)
          return;
        File.AppendAllText("exceptionLog.txt", RuntimeDetourManager.GetAdditionalExceptionInfo(latestException));
        GSLLog.GLog($"{GSLLog.TimeLabel()} [unhandled exception] {latestException}");
      });
  }

  static void Futile_ctor(On.Futile.orig_ctor orig, Futile self)
  {
    orig(self);

    try
    {
      DetourUtils.newHookRND(ModManager.LoadModFromJson, ModUtils.ModManager_LoadModFromJson);
    }
    catch (Exception e)
    {
      LogError(e);
    }
  }

  public static void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
  {
    orig(self);

    if (isInit)
      return;
    isInit = true;

    try
    {
      MainMenuHooks.Apply();
      AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

      pluginInterface = new();
      MachineConnector.SetRegisteredOI(Patcher.PLUGIN_GUID, pluginInterface);
      new GSLOIBinder(GSLSettings.instance, pluginInterface);

      if (ModManager.ActiveMods.Count == 0)
        GSLLog.GLog("<no enabled mods>");
      else
      {
        GSLLog.GLog();
        foreach (ModManager.Mod mod in ModManager.ActiveMods)
          GSLLog.GLog(mod.GetSimpleLabel());
        GSLLog.GLog(new string(' ', Math.Max(0, ModUtils.longestFolderName - 6)) + "(source\\folder | mod metadata | assemblyN{<plugin metadataN : plugin name>})");
        foreach (ModManager.Mod mod in ModManager.ActiveMods)
          GSLLog.GLog(ModUtils.GetFullModInfo(mod));
        GSLLog.GLog();
      }
    }
    catch (Exception e)
    {
      LogError(e);
    }
  }

  static void CurrentDomain_ProcessExit(object sender, EventArgs e)
  {
    foreach (KeyValuePair<string, ModManager.Mod> mod in ModUtils.mods)
      ModUtils.previousModVersions.data[mod.Key] = mod.Value.version;
    ModUtils.previousModVersions.Write();
  }
}