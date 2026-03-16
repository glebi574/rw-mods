using BepInEx;
using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.SavedDataManagerExtensions;
using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using System;
using System.Collections.Generic;
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
    oi.biggerErrorBox.Value = manager.biggerErrorBox;
  }

  public override void RemixSave()
  {
    Data["wrapHooks"] = oi.wrapHooks.Value;
    Data["noUpdateDisable"] = oi.noUpdateDisable.Value;
    Data["disableEOS"] = oi.disableEOS.Value;
    Data["biggerErrorBox"] = oi.biggerErrorBox.Value;
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

      IssueResolver.ApplyHooks();
      On.Futile.ctor += Futile_ctor;
      On.RainWorld.OnModsInit += RainWorld_OnModsInit;
      On.OptionInterface._LoadConfigFile += OptionInterface__LoadConfigFile;
      On.OptionInterface._SaveConfigFile += OptionInterface__SaveConfigFile;
      On.Menu.DialogBoxNotify.ctor += DialogBoxNotify_ctor;
      if (GSLSettings.instance.noUpdateDisable)
        IL.ModManager.RefreshModsLists += ModManager_RefreshModsLists;

      //DetourUtils.newILHook<Detour>("_RefreshChain", Detour__RefreshChain);
    }
    catch (Exception e)
    {
      LogError(e);
    }
  }

  // DMDs broken by IL hooks are not freed and are used as origs, which breaks following hooks
  /*
  public static void _Stub() { }

  public static IntPtr GetNativeStart_d(MethodBase method)
  {
    try
    {
      return method.GetNativeStart();
    }
    catch
    {
      return ((Delegate)_Stub).Method.GetNativeStart();
    }
  }

  public static void Detour__RefreshChain(ILContext il)
  {
    ILCursor c = new(il);

    for (int i = 0; i < 2; ++i)
      if (!c.TryGotoNext(i => i.MatchCall(typeof(DetourHelper).GetMethod("GetNativeStart", [typeof(MethodBase)]))))
        return;

    c.Emit(OpCodes.Call, ((Delegate)GetNativeStart_d).Method);
    c.Index++;
    Instruction newobj = c.Next;
    c.Index--;
    c.Emit(OpCodes.Br, newobj);
    c.Emit(OpCodes.Ldnull);
    c.Index++;
    c.Emit(OpCodes.Pop);
  }
  */

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

  static void DialogBoxNotify_ctor(On.Menu.DialogBoxNotify.orig_ctor orig, DialogBoxNotify self, Menu.Menu menu, MenuObject owner, string text, string signalText, UnityEngine.Vector2 pos, UnityEngine.Vector2 size, bool forceWrapping)
  {
    if (signalText != "AFTERERROR")
      goto _e;
    LogError("DialogBoxNotify with error message was instantiated: " + text);
    if (!pluginInterface.biggerErrorBox.Value)
      goto _e;
    pos.x = 40f;
    pos.y = 40f;
    size.x = Custom.rainWorld.options.ScreenSize.x - 80f;
    size.y = Custom.rainWorld.options.ScreenSize.y - 80f;
    orig(self, menu, owner, text, signalText, pos, size, forceWrapping);
    self.descriptionLabel.label.alignment = FLabelAlignment.Left;
    self.descriptionLabel.label._anchorY = 1f;
    self.descriptionLabel.size = new();
    self.descriptionLabel.pos.x = 60f;
    self.descriptionLabel.pos.y = Custom.rainWorld.options.ScreenSize.y - 60f;
    return;
  _e:
    orig(self, menu, owner, text, signalText, pos, size, forceWrapping);
    return;
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