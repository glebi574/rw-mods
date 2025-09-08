using BepInEx;
using gelbi_silly_lib;
using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using UnityEngine;
using Watcher;
using static slugsprites.LogWrapper;

namespace slugsprites;

public static class LogWrapper
{
  public static BepInEx.Logging.ManualLogSource Log;
}

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
[BepInDependency("slime-cubed.slugbase", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
  public const string PLUGIN_GUID = "gelbi.slugsprites";
  public const string PLUGIN_NAME = "SlugSprites";
  public const string PLUGIN_VERSION = "0.2.8";

  public static bool isInit = false, isSlugBaseActive = false;

  public delegate void InitCachedSpriteNamesD(PlayerGraphics self, SlugcatSprites sprites);
  public delegate void InitiateSpritesD(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites);
  public delegate void DrawSpritesD(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites);
  public delegate void AddToContainerD(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner, SlugcatSprites sprites);
  public delegate void ApplyPaletteD(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette, SlugcatSprites sprites);
  
  /// <summary>
  /// Event, used to modify or define cached sprite names(used by Rain World to animate some body parts)
  /// </summary>
  public static event InitCachedSpriteNamesD OnInitCachedSpriteNames;
  /// <summary>
  /// Event, called on sprite initialization(each time you enter new room or hot reload sprites)
  /// </summary>
  public static event InitiateSpritesD OnInitiateSprites;
  /// <summary>
  /// Event, called each redraw cycle
  /// </summary>
  public static event DrawSpritesD OnDrawSprites;
  /// <summary>
  /// Event, used to determine order in which sprites are drawn
  /// </summary>
  public static event AddToContainerD OnAddToContainer;
  /// <summary>
  /// Event, used to apply room palette or other features(called on sprite initiation and to apply some features, like hypothermia)
  /// </summary>
  public static event ApplyPaletteD OnApplyPalette;

  public static PluginInterface pluginInterface;

  public void OnEnable()
  {
    if (isInit)
      return;
    isInit = true;

    try
    {
      On.RainWorld.OnModsInit += RainWorld_OnModsInit;
      On.RainWorld.PostModsInit += RainWorld_PostModsInit;
    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }

  public void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
  {
    orig(self);

    try
    {
      pluginInterface = new PluginInterface();
      MachineConnector.SetRegisteredOI(PLUGIN_GUID, pluginInterface);
    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }

  public void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
  {
    orig(self);

    try
    {
      Log = Logger;

      // silly cctors
      typeof(AnimationType).RunClassConstructor();
      typeof(AnimationColor.Subtype).RunClassConstructor();
      typeof(AnimationColor.ColorModifierType).RunClassConstructor();

      AnimationHandler.Initialize();
      MeshHandler.Initialize();
      SpriteHandler.LoadCustomSprites();

      foreach (ModManager.Mod mod in ModManager.ActiveMods)
        if (mod.id == "slime-cubed.slugbase")
        {
          isSlugBaseActive = true;
          break;
        }

      On.PlayerGraphics.InitCachedSpriteNames += PlayerGraphics_InitCachedSpriteNames;
      On.PlayerGraphics.AddToContainer += PlayerGraphics_AddToContainer;
      On.PlayerGraphics.ApplyPalette += PlayerGraphics_ApplyPalette;
      On.RainWorld.Update += RainWorld_Update;

      IL.PlayerGraphics.InitiateSprites += PlayerGraphics_InitiateSprites;
      IL.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
    }
    catch (Exception e)
    {
      Logger.LogError(e);
    }
  }

  public void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
  {
    orig(self);
    if (!pluginInterface.debugMode.Value || !Input.GetKeyDown(pluginInterface.reloadKey.Value))
      return;
    SpriteHandler.LoadCustomSprites();
    if (self.processManagerInitialized && self.processManager.currentMainLoop is RainWorldGame game)
    {
      SpriteHandler.managedPlayerList.RemoveAll(p => !p.TryGetTarget(out _));
      foreach (WeakReference<Player> playerRef in SpriteHandler.managedPlayerList)
      {
        playerRef.TryGetTarget(out Player player);
        SpriteHandler.managedPlayers.TryGetValue(player, out ManagedPlayerData managedPlayer);
        managedPlayer.sLeaser.RemoveAllSpritesFromContainer();
        (player.graphicsModule as PlayerGraphics).InitCachedSpriteNames();
        player.graphicsModule.InitiateSprites(managedPlayer.sLeaser, managedPlayer.rCam);
        player.graphicsModule.ApplyPalette(managedPlayer.sLeaser, managedPlayer.rCam, managedPlayer.rCam.currentPalette);
        if (managedPlayer.rCam.rippleData != null)
          CosmeticRipple.ReplaceBasicShader(managedPlayer.sLeaser.sprites);
      }
    }
  }

  public void PlayerGraphics_DrawSprites(ILContext il)
  {
    ILCursor c = new(il);

    if (c.TryGotoNext(
    i => i.MatchLdarg(1),
    i => i.MatchLdfld<RoomCamera.SpriteLeaser>("sprites"),
    i => i.MatchLdcI4(10),
    i => i.MatchLdelemRef(),
    i => i.MatchLdcR4(1),
    i => i.MatchLdarg(0),
    i => i.MatchLdfld<PlayerGraphics>("lastMarkAlpha"),
    i => i.MatchLdarg(0),
    i => i.MatchLdfld<PlayerGraphics>("markAlpha"),
    i => i.MatchLdarg(3),
    i => i.MatchCall<Mathf>("Lerp"),
    i => i.MatchAdd(),
    i => i.MatchCallvirt<FNode>("set_scale")))
    {
      c.Index++;

      c.Emit(OpCodes.Ldarg_0);
      c.Emit(OpCodes.Ldarg_1);
      c.Emit(OpCodes.Ldarg_2);
      c.Emit(OpCodes.Ldarg_3);
      c.Emit(OpCodes.Ldarg, 4);
      c.EmitDelegate(DrawSprites_Wrapper);
    }
  }

  public void PlayerGraphics_InitiateSprites(ILContext il)
  {
    ILCursor c = new(il);

    if (c.TryGotoNext(
      i => i.MatchLdarg(1),
      i => i.MatchLdarg(2),
      i => i.MatchLdarg(0),
      i => i.MatchLdfld<PlayerGraphics>("firstMudSprite")))
    {
      c.Index--;

      c.Emit(OpCodes.Ldarg_0);
      c.Emit(OpCodes.Ldarg_1);
      c.EmitDelegate(InitiateSprites_Wrapper);
    }
  }

  public void PlayerGraphics_InitCachedSpriteNames(On.PlayerGraphics.orig_InitCachedSpriteNames orig, PlayerGraphics self)
  {
    orig(self);
    self.TryGetSupportedSlugcat(out SlugcatSprites sprites);
    OnInitCachedSpriteNames?.Invoke(self, sprites);
  }

  public static void InitiateSprites_Wrapper(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser)
  {
    SlugcatSprites sprites = SpriteHandler.InitiateSprites(self, sLeaser);
    OnInitiateSprites?.Invoke(self, sLeaser, sprites);
  }

  public static void DrawSprites_Wrapper(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
  {
    SlugcatSprites sprites = SpriteHandler.DrawSprites(self, sLeaser, rCam, timeStacker, camPos);
    OnDrawSprites?.Invoke(self, sLeaser, rCam, timeStacker, camPos, sprites);
  }

  public void PlayerGraphics_AddToContainer(On.PlayerGraphics.orig_AddToContainer orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
  {
    orig(self, sLeaser, rCam, newContatiner);
    SlugcatSprites sprites = SpriteHandler.AddToContainer(self, sLeaser, rCam, newContatiner);
    OnAddToContainer?.Invoke(self, sLeaser, rCam, newContatiner, sprites);
  }

  public void PlayerGraphics_ApplyPalette(On.PlayerGraphics.orig_ApplyPalette orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
  {
    orig(self, sLeaser, rCam, palette);
    SlugcatSprites sprites = SpriteHandler.ApplyPalette(self, sLeaser, rCam, palette);
    OnApplyPalette?.Invoke(self, sLeaser, rCam, palette, sprites);
  }
}

public static class HandlerUtils
{
  public static List<string> ListDirectoryE(string path, out FileUtils.Result opResult)
  {
    List<string> paths = FileUtils.ListDirectory(path, out opResult, ".json");
    if (opResult == FileUtils.Result.NoDirectory)
      Log.LogWarning($"Failed to load - \"{path}\" folder doesn't exist");
    if (opResult == FileUtils.Result.NoFiles)
      Log.LogWarning($"Failed to load - \"{path}\" folder doesn't contain any files");
    if (opResult == FileUtils.Result.NoFilesWithExtension)
      Log.LogError($"Failed to load - \"{path}\" folder doesn't contain any .json files");
    return paths;
  }
}