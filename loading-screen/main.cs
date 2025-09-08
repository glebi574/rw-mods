using BepInEx;
using gelbi_silly_lib;
using gelbi_silly_lib.MonoModUtils;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using static loading_screen.LogWrapper;
using static Menu.InitializationScreen.InitializationStep;

namespace loading_screen;

public static class LogWrapper
{
  public static BepInEx.Logging.ManualLogSource Log;
}

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
  public const string PLUGIN_GUID = "gelbi.loading-screen";
  public const string PLUGIN_NAME = "Loading Screen";
  public const string PLUGIN_VERSION = "1.0.0";

  public static bool isInit = false;
  public static Assembly thisAssembly;

  public void OnEnable()
  {
    if (isInit)
      return;
    isInit = true;

    Log = Logger;

    try
    {
      thisAssembly = typeof(Plugin).Assembly;

      On.Futile.Init += Futile_Init;
      IL.Menu.InitializationScreen.Update += InitializationScreen_Update_ModifyInit;
      On.Menu.InitializationScreen.Update += InitializationScreen_Update;
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }

  public void InitializationScreen_Update_ModifyInit(ILContext il)
  {
    ILCursor c = new(il);

    if (c.TryGotoNext(i => i.MatchCall(typeof(ModManager).GetMethod("WrapModInitHooks"))))
    {
      c.Emit(OpCodes.Ldarg_0);
      c.EmitDelegate(ModifyInit);
      c.Emit(OpCodes.Ret);
    }
  }

  public static void ModifyInit(Menu.InitializationScreen self)
  {
    if (modifiedUpdate)
      return;
    modifiedUpdate = true;
    initializationScreen = self;
    loadingStringState = "Initializing mods - wrapping hooks";
    On.RainWorld.Update += RainWorld_Update;
  }

  public static void UpdateLoadingStringLeft(string targetName)
  {
    try
    {
      StringBuilder sb = new();
      foreach (KeyValuePair<MethodBase, List<IDetour>> kvp in RuntimeDetourManager.hookMaps)
        if (kvp.Key.Name == targetName)
          foreach (IDetour detour in kvp.Value)
          {
            MethodBase method = detour.GetTarget();
            if (method?.DeclaringType?.DeclaringType != typeof(ModManager))
              sb.Append($"{method.DeclaringType.FullName}.{method.Name}\n");
          }
      loadingStringLeft = sb.ToString();
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }

  public enum ModInitState
  {
    WrapInitHooks,
    PreModsInit,
    OnModsInit,
    PostModsInit,
    Finalize
  }

  public static bool modifiedUpdate = false;
  public static Menu.InitializationScreen initializationScreen;
  public static ModInitState initState = ModInitState.WrapInitHooks;

  public static void CreateRestartDialog(string restartText)
  {
    initializationScreen.requiresRestartDialog = new(initializationScreen, initializationScreen.pages[0], restartText, "RESTART",
        new(Custom.rainWorld.options.ScreenSize.x / 2f - 240f + (1366f - Custom.rainWorld.options.ScreenSize.x) / 2f, 224f), new Vector2(480f, 320f), false);
    initializationScreen.pages[0].subObjects.Add(initializationScreen.requiresRestartDialog);
    initializationScreen.currentStep = REQUIRE_RESTART;
  }

  public static void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
  {
    switch (initState)
    {
      case ModInitState.WrapInitHooks:
        ModManager.WrapModInitHooks();

        loadingBarProgress = 0.14f;
        loadingStringState = "Initializing mods - PreModsInit";
        UpdateLoadingStringLeft("PreModsInit");
        initState = ModInitState.PreModsInit;
        return;
      case ModInitState.PreModsInit:
        try
        {
          self.PreModsInit();
        }
        catch (Exception e)
        {
          Custom.LogWarning(new string[] { "EXCEPTION IN PreModsInit", e.Message, "::", e.StackTrace });
        }
        if (ModManager.CheckInitIssues(CreateRestartDialog))
        {
          UndoInit();
          return;
        }

        loadingBarProgress = RXRandom.Float(0.03f) + 0.17f;
        loadingStringState = "Initializing mods - OnModsInit";
        UpdateLoadingStringLeft("OnModsInit");
        initState = ModInitState.OnModsInit;
        return;
      case ModInitState.OnModsInit:
        try
        {
          self.OnModsInit();
        }
        catch (Exception e)
        {
          Custom.LogWarning(new string[] { "EXCEPTION IN OnModsInit", e.Message, "::", e.StackTrace });
        }
        if (ModManager.CheckInitIssues(CreateRestartDialog))
        {
          UndoInit();
          return;
        }

        loadingBarProgress = RXRandom.Float(0.1f) + 0.6f;
        loadingStringState = "Initializing mods - PostModsInit";
        UpdateLoadingStringLeft("PostModsInit");
        initState = ModInitState.PostModsInit;
        return;
      case ModInitState.PostModsInit:
        try
        {
          self.PostModsInit();
        }
        catch (Exception e)
        {
          Custom.LogWarning(new string[] { "EXCEPTION IN PostModsInit", e.Message, "::", e.StackTrace });
        }
        if (ModManager.CheckInitIssues(CreateRestartDialog))
        {
          UndoInit();
          return;
        }

        loadingBarProgress = RXRandom.Float(0.03f) + 0.88f;
        loadingStringState = "Initializing mods - finalizing";
        loadingStringLeft = "";
        initState = ModInitState.Finalize;
        return;
      case ModInitState.Finalize:
        initializationScreen.manager.InitSoundLoader();
        initializationScreen.currentStep = WAIT_FOR_MOD_INIT_ASYNC;
        UndoInit();
        return;
    }
  }

  public static void UndoInit()
  {
    On.RainWorld.Update -= RainWorld_Update;
  }

  public void InitializationScreen_Update(On.Menu.InitializationScreen.orig_Update orig, Menu.InitializationScreen self)
  {
    orig(self);
    try
    {
      if (loadingBarProgress < 0.13f)
        loadingBarProgress = Math.Min(loadingBarProgress += 0.01f, 0.12f);
      else
        loadingBarProgress = Math.Min(loadingBarProgress += 0.01f, 0.99f);

      Menu.InitializationScreen.InitializationStep currentStep = self.currentStep;
      switch (currentStep)
      {
        case MOUNT_AOC:
          loadingStringState = "MOUNT_AOC";
          return;
        case AOCUpdateAPI:
          loadingStringState = "AOCUpdateAPI";
          return;
        case WAIT_FOR_OPTIONS_READY:
          loadingStringState = "Waiting for options to be loaded";
          return;
        case LOAD_FONTS:
          loadingStringState = "Loading fonts";
          return;
        case PORT_LEGACY_SAVE_FILES:
          loadingStringState = "Porting legacy save files";
          return;
        case CHECK_GAME_VERSION_CHANGED:
          loadingStringState = "Checking for game version changes";
          return;
        case CHECK_WORKSHOP_CONTENT:
          loadingStringState = "Checking if workshop content is present";
          return;
        case START_DOWNLOAD_WORKSHOP_CONTENT:
          loadingStringState = "Starting workshop content downlaod";
          return;
        case WAIT_DOWNLOAD_WORKSHOP_CONTENT:
          loadingStringState = "Downloading workshop content";
          return;
        case VALIDATE_WORKSHOP_CONTENT:
          loadingStringState = "Validating workshop content";
          return;
        case VALIDATE_MODS:
          loadingStringState = "Validating mods";
          return;
        case APPLY_MODS:
          loadingStringState = "Applying mods";
          return;
        case REQUIRE_RESTART:
          loadingStringState = "Restart is required";
          return;
        case WAIT_FOR_ASYNC_LOAD:
          loadingStringState = "Loading";
          return;
        case MOD_INIT:
          loadingStringState = "Initializing mods";
          return;
        case WAIT_FOR_MOD_INIT_ASYNC:
          loadingStringState = "Initializing mods";
          return;
        case RELOAD_PROGRESSION:
          loadingStringState = "Reloading progression";
          return;
        case WRAP_UP:
          loadingStringState = "Wrapping up";
          return;
        case WAIT_STARTUP_DIALOGS:
          loadingStringState = "Waiting for startup dialogs";
          return;
        case LOCK_ON_PROGRESSION_FAILED_ERROR:
          loadingStringState = "Failed to lock on progression";
          return;
        case WAIT_FOR_BACKUP_RESTORE:
          loadingStringState = "Restoring backup";
          return;
        case WAIT_FOR_BACKUP_RECREATION:
          loadingStringState = "Recreating backup";
          return;
        case SAVE_FILE_FAIL_WARN_PROCEED:
          loadingStringState = "SAVE_FILE_FAIL_WARN_PROCEED";
          return;
        case LOCALIZATION_DEBUG:
          loadingStringState = "LOCALIZATION_DEBUG";
          return;
        case WAIT_FOR_PROCESS_CHANGE:
          IL.Menu.InitializationScreen.Update -= InitializationScreen_Update_ModifyInit;
          On.Menu.InitializationScreen.Update -= InitializationScreen_Update;
          loadingScreenStage = false;
          On.Menu.MainMenu.Update += MainMenu_Update;
          return;
        default:
          loadingStringState = currentStep.ToString();
          return;
      }
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }

  public void MainMenu_Update(On.Menu.MainMenu.orig_Update orig, Menu.MainMenu self)
  {
    orig(self);
    try
    {
      loadingScreenContainer.RemoveAllChildren();
      Futile.stage.RemoveChild(loadingScreenContainer);
      On.Menu.MainMenu.Update -= MainMenu_Update;
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }

  public static bool loadingScreenStage = true;
  public static FSprite background;
  public static FContainer loadingScreenContainer;
  public static float loadingBarProgress = 0f;
  public static string loadingStringState = "", loadingStringInfo = "", loadingStringLeft = "";
  public static string scenesPath = "RainWorld_Data\\StreamingAssets\\scenes\\",
    sceneFolderDefault = "main menu\\",
    sceneFolderDownpour = "main menu - downpour\\",
    sceneFolderWatcher = "RainWorld_Data\\StreamingAssets\\mods\\watcher\\scenes\\main menu - watcher\\",
    sceneNameDefault = "main menu - flat",
    sceneNameDownpour = "main menu - downpour - flat",
    sceneNameWatcher = "main menu watcher - flat";

  public void Futile_Init(On.Futile.orig_Init orig, Futile self, FutileParams futileParams)
  {
    orig(self, futileParams);
    try
    {
      string folderPath = sceneFolderWatcher, filePath, sceneName;
      /*
      if (Directory.Exists(folderPath))
      {
        filePath = folderPath + sceneNameWatcher + ".png";
        sceneName = sceneNameWatcher;
      }
      else
      */
      if (Directory.Exists(folderPath = scenesPath + sceneFolderDownpour))
      {
        filePath = folderPath + sceneNameDownpour + ".png";
        sceneName = sceneNameDownpour;
      }
      else
      {
        folderPath = scenesPath + sceneFolderDefault;
        filePath = folderPath + sceneNameDefault + ".png";
        sceneName = sceneNameDefault;
      }
      Texture2D texture = new(1, 1, TextureFormat.ARGB32, false);
      AssetManager.SafeWWWLoadTexture(ref texture, filePath, true, false);
      HeavyTexturesCache.LoadAndCacheAtlasFromTexture(sceneName, texture, false);
      background = new(sceneName)
      {
        anchorX = 0f,
        anchorY = 0f,
        alpha = 0.9f
      };
      loadingScreenContainer = new();
      loadingScreenContainer.AddChild(background);
      Futile.stage.AddChild(loadingScreenContainer);
      On.Futile.Init -= Futile_Init;
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }

  public static bool failedToLoadFont = false;
  public static Font loadedFont = null;

  public void OnGUI()
  {
    if (!loadingScreenStage)
      return;

    if (!failedToLoadFont)
      if (loadedFont == null)
      {
        string[] fontNames = Font.GetOSInstalledFontNames();
        if (fontNames.Contains("Century Gothic"))
          GUI.skin.font = Font.CreateDynamicFontFromOSFont("Century Gothic", 72);
        else
          failedToLoadFont = true;
      }
      else
        GUI.skin.font = loadedFont;
    GUI.skin.font.material.mainTexture.filterMode = FilterMode.Point;

    float offsetX = 0f, offsetY = 0f, scale = 0.03f, width = 1366f;
    float loadingBarOffsetX = Screen.width / 4, loadingBarOffsetY = Screen.height * 3 / 4, loadingBarWidth = Screen.width / 2, loadingBarHeight = 30f, loadingBarInnerOffset = 3f;
    float x1 = loadingBarOffsetX, x2 = loadingBarOffsetX + loadingBarInnerOffset,
      y1 = loadingBarOffsetY, y2 = loadingBarOffsetY + loadingBarInnerOffset;
    Color color = GUI.color;
    GUI.color = new(0.9f, 0.9f, 0.9f);
    GUI.DrawTexture(new Rect(x1, y1, loadingBarWidth, loadingBarInnerOffset), Texture2D.whiteTexture);
    GUI.DrawTexture(new Rect(x1, y1 + loadingBarHeight - loadingBarInnerOffset, loadingBarWidth, loadingBarInnerOffset), Texture2D.whiteTexture);
    GUI.DrawTexture(new Rect(x1, y1, loadingBarInnerOffset, loadingBarHeight), Texture2D.whiteTexture);
    GUI.DrawTexture(new Rect(x1 + loadingBarWidth - loadingBarInnerOffset, y1, loadingBarInnerOffset, loadingBarHeight), Texture2D.whiteTexture);
    GUI.color = new(0f, 0.6f, 0.9f);
    GUI.DrawTexture(new Rect(x2, y2, (loadingBarWidth - loadingBarInnerOffset * 2f) * loadingBarProgress, loadingBarHeight - loadingBarInnerOffset * 2f), Texture2D.whiteTexture);
    GUI.color = color;

    Matrix4x4 matrix = GUI.matrix;
    Vector2 pivot = new(offsetX, offsetY);
    GUIUtility.ScaleAroundPivot(new(scale, scale), pivot);

    GUIStyle styleCenter = new(GUI.skin.label)
    {
      fontSize = 1366,
      alignment = TextAnchor.UpperCenter,
      wordWrap = false,
      fontStyle = FontStyle.Bold
    }, styleLeft = new(GUI.skin.label)
    {
      fontSize = 420,
      wordWrap = false,
      fontStyle = FontStyle.Bold
    };
    styleCenter.normal.textColor = Color.white;
    GUI.Label(new(offsetX / scale, (offsetY + loadingBarOffsetY + loadingBarHeight) / scale, width / scale, width / scale), $"{loadingStringState}\n{loadingStringInfo}", styleCenter);
    GUI.Label(new(10f / scale, 10f / scale, width / scale, width / scale), $"{loadingStringLeft}", styleLeft);
    GUI.Label(new(offsetX / scale, (offsetY + loadingBarOffsetY + loadingBarInnerOffset * 2) / scale,
      width / scale, width / scale), $"{loadingBarProgress:P0}", styleCenter);
    GUI.matrix = matrix;
  }
}