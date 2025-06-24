using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace slugsprites
{
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
    public const string PLUGIN_VERSION = "0.2.0";

    public static bool isInit = false;

    public delegate void InitCachedSpriteNamesD(PlayerGraphics self);
    public delegate void InitiateSpritesD(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser);
    public delegate void DrawSpritesD(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos);
    public delegate void AddToContainerD(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner);
    public delegate void ApplyPaletteD(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette);

    public static event InitCachedSpriteNamesD OnInitCachedSpriteNames;
    public static event InitiateSpritesD OnInitiateSprites;
    public static event DrawSpritesD OnDrawSprites;
    public static event AddToContainerD OnAddToContainer;
    public static event ApplyPaletteD OnApplyPalette;

    public void OnEnable()
    {
      if (isInit)
        return;
      isInit = true;

      On.RainWorld.PostModsInit += RainWorld_PostModsInit;
    }

    public void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
    {
      orig(self);

      try
      {
        LogWrapper.Log = Logger;
        SpriteHandler.LoadCustomSprites();

        OnInitCachedSpriteNames += SpriteHandler.InitCachedSpriteNames;
        OnInitiateSprites += SpriteHandler.InitiateSprites;
        OnDrawSprites += SpriteHandler.DrawSprites;
        OnAddToContainer += SpriteHandler.AddToContainer;
        OnApplyPalette += SpriteHandler.ApplyPalette;

        On.PlayerGraphics.InitCachedSpriteNames += PlayerGraphics_InitCachedSpriteNames;
        On.PlayerGraphics.AddToContainer += PlayerGraphics_AddToContainer;
        On.PlayerGraphics.ApplyPalette += PlayerGraphics_ApplyPalette;

        IL.PlayerGraphics.InitiateSprites += PlayerGraphics_InitiateSprites;
        IL.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
      }
      catch (Exception e)
      {
        Logger.LogError(e);
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
      i => i.MatchCall(typeof(Mathf), "Lerp"),
      i => i.MatchAdd(),
      i => i.MatchCallvirt(typeof(FNode).Name, "set_scale")))
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
        i => i.OpCode == OpCodes.Ldarg_1,
        i => i.OpCode == OpCodes.Ldarg_2,
        i => i.OpCode == OpCodes.Ldarg_0,
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
      OnInitCachedSpriteNames.Invoke(self);
    }

    public static void InitiateSprites_Wrapper(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser)
    {
      OnInitiateSprites.Invoke(self, sLeaser);
    }

    public static void DrawSprites_Wrapper(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
      OnDrawSprites.Invoke(self, sLeaser, rCam, timeStacker, camPos);
    }

    public void PlayerGraphics_AddToContainer(On.PlayerGraphics.orig_AddToContainer orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
    {
      orig(self, sLeaser, rCam, newContatiner);
      OnAddToContainer.Invoke(self, sLeaser, rCam, newContatiner);
    }

    public void PlayerGraphics_ApplyPalette(On.PlayerGraphics.orig_ApplyPalette orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
    {
      orig(self, sLeaser, rCam, palette);
      OnApplyPalette.Invoke(self, sLeaser, rCam, palette);
    }
  }
}
