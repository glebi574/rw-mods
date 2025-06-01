using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Watcher;
using Debug = UnityEngine.Debug;

namespace rot_apply_palette_fix
{
  public static class Extensions
  {
    public static void InitiateSpritesF(this DaddyCorruption self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
      sLeaser.sprites = new FSprite[self.totalSprites];
      if (ModManager.MSC && rCam.room.world.region != null && rCam.room.world.name == "HR")
      {
        self.effectColor = RainWorld.SaturatedGold;
        self.eyeColor = self.effectColor;
      }
      else if (rCam.room.world.region != null)
      {
        self.effectColor = rCam.room.world.region.regionParams.corruptionEffectColor;
        self.eyeColor = rCam.room.world.region.regionParams.corruptionEyeColor;
      }
      else
      {
        self.effectColor = new Color(0f, 0f, 1f);
        self.eyeColor = self.effectColor;
      }
      foreach (DaddyCorruption.Bulb bulb in self.allBulbs)
      {
        bulb.InitiateSpritesF(sLeaser, rCam);
      }
      for (int i = 0; i < self.climbTubes.Count; i++)
      {
        self.climbTubes[i].graphic.InitiateSprites(sLeaser, rCam);
      }
      for (int j = 0; j < self.restrainedDaddies.Count; j++)
      {
        self.restrainedDaddies[j].graphic.InitiateSprites(sLeaser, rCam);
      }
      if (ModManager.MSC)
      {
        for (int k = 0; k < self.neuronLegs.Count; k++)
        {
          self.neuronLegs[k].graphic.InitiateSprites(sLeaser, rCam);
        }
      }
      self.AddToContainer(sLeaser, rCam, null);
      self.ApplyPalette(sLeaser, rCam, rCam.currentPalette);
    }

    public static void InitiateSpritesF(this DaddyCorruption.Bulb self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
      sLeaser.sprites[self.firstSprite] = new FSprite("Futile_White", true);
      sLeaser.sprites[self.firstSprite].scale = self.rad / 8f;
      sLeaser.sprites[self.firstSprite].shader = rCam.room.game.rainWorld.Shaders["JaggedCircleBothSides"];
      sLeaser.sprites[self.firstSprite].alpha = 0.6f;
      if (self.hasEye)
      {
        for (int i = 0; i < 2; i++)
        {
          sLeaser.sprites[self.firstSprite + 1 + i] = self.MakeSlitMesh();
          sLeaser.sprites[self.firstSprite + 1 + i].shader = rCam.room.game.rainWorld.Shaders["RippleBasicBothSides"];
        }
      }
      if (self.hasBlackGoo)
      {
        sLeaser.sprites[self.BlackGooSprite] = new FSprite("corruption", true);
        FNode fnode = sLeaser.sprites[self.BlackGooSprite];
        float num = self.rad * Mathf.Clamp(self.rad * 0.35f, 3f, 7f) / 100f;
        DaddyCorruption.CustomRotData customRotData = self.customData;
        fnode.scale = num * ((customRotData != null) ? customRotData.darknessScale : 1f);
        sLeaser.sprites[self.BlackGooSprite].shader = rCam.room.game.rainWorld.Shaders["BlackGooBothSides"];
        sLeaser.sprites[self.BlackGooSprite].rotation = self.rotation;
      }
      if (self.hasDot)
      {
        sLeaser.sprites[self.dotSprite] = new FSprite("Futile_White", true);
        FNode fnode2 = sLeaser.sprites[self.dotSprite];
        float num2 = self.rad / 8f;
        DaddyCorruption.CustomRotData customRotData2 = self.customData;
        float num3 = ((customRotData2 != null) ? customRotData2.minEyeSize : 0.45f);
        DaddyCorruption.CustomRotData customRotData3 = self.customData;
        fnode2.scale = num2 * global::UnityEngine.Random.Range(num3, (customRotData3 != null) ? customRotData3.maxEyeSize : 0.65f);
        sLeaser.sprites[self.dotSprite].shader = rCam.room.game.rainWorld.Shaders["JaggedCircleBothSides"];
        sLeaser.sprites[self.dotSprite].alpha = 0.2f;
      }
      if (self.leg != null)
      {
        self.leg.graphic.InitiateSprites(sLeaser, rCam);
      }
      DaddyCorruption.Tendril tendril = self.tendril;
      if (tendril != null)
      {
        tendril.graphic.InitiateSprites(sLeaser, rCam);
      }
      self.ApplyPalette(sLeaser, rCam, rCam.currentPalette);
    }
  }

  [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
  public class Plugin : BaseUnityPlugin
  {
    public const string PLUGIN_GUID = "gelbi.rot-apply-palette-fix";
    public const string PLUGIN_NAME = "Rot ApplyPalette Fix";
    public const string PLUGIN_VERSION = "1.0.0";

    public void OnEnable()
    {
      On.RoomCamera.SpriteLeaser.ctor += SpriteLeaser_ctor;
    }

    private void SpriteLeaser_ctor(On.RoomCamera.SpriteLeaser.orig_ctor orig, RoomCamera.SpriteLeaser self, IDrawable obj, RoomCamera rCam)
    {
      if (obj is not DaddyCorruption daddyCorruption)
      {
        orig(self, obj, rCam);
        return;
      }
      self.drawableObject = obj;
      daddyCorruption.InitiateSpritesF(self, rCam);
      self.drawableObject.ApplyPalette(self, rCam, rCam.currentPalette);
      if (rCam.rippleData != null)
      {
        CosmeticRipple.ReplaceBasicShader(self.sprites);
      }
    }
    /*
    private void Bulb_ApplyPalette(On.DaddyCorruption.Bulb.orig_ApplyPalette orig, DaddyCorruption.Bulb self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
    {
      Debug.Log("Applying palette for DaddyCorruption.Bulb");
      Debug.Log($"  > sLeaser: {sLeaser}");
      Debug.Log($"  > sprites: {sLeaser.sprites}");
      Color effectColor = self.EffectColor;
      if (self.customData != null)
      {
        float num = 0f;
        if (self.customData.eyeType == DaddyCorruption.CustomRotData.EyeType.Dot)
        {
          num = 0f;
        }
        else if (self.customData.eyeType == DaddyCorruption.CustomRotData.EyeType.Cross)
        {
          num = Mathf.Lerp(self.customData.minColor, self.customData.maxColor, Mathf.Pow(global::UnityEngine.Random.value, 0.5f));
        }
        sLeaser.sprites[self.firstSprite].color = Color.Lerp(palette.blackColor, effectColor, num);
      }
      else
      {
        Debug.Log($"  > self.firstSprite: {self.firstSprite}");
        Debug.Log($"  > sLeaser.sprites[self.firstSprite]: {sLeaser.sprites[self.firstSprite]}");
        sLeaser.sprites[self.firstSprite].color = palette.blackColor;
      }
      if (self.hasDot)
      {
        Debug.Log($"  > self.dotSprite: {self.dotSprite}");
        Debug.Log($"  > sLeaser.sprites[self.dotSprite]: {sLeaser.sprites[self.dotSprite]}");
        sLeaser.sprites[self.dotSprite].color = Color.Lerp(palette.blackColor, effectColor, Mathf.Lerp(self.customData.minColor, self.customData.maxColor, self.corruptionLevelAtMySpot));
      }
      if (self.leg != null)
      {
        Debug.Log($"  > self.leg: {self.leg}");
        Debug.Log($"  > self.leg.graphic: {self.leg.graphic}");
        self.leg.graphic.ApplyPalette(sLeaser, rCam, palette);
      }
      DaddyCorruption.Tendril tendril = self.tendril;
      if (tendril == null)
      {
        return;
      }
      tendril.graphic.ApplyPalette(sLeaser, rCam, palette);
    }

    private void DaddyCorruption_ApplyPalette(On.DaddyCorruption.orig_ApplyPalette orig, DaddyCorruption self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
    {
      Debug.Log("Applying palette for DaddyCorruption");
      Debug.Log($"  > sLeaser: {sLeaser}");
      Debug.Log($"  > sprites: {sLeaser.sprites}");
      Debug.Log($"  > DaddyCorruption: {self}");
      Debug.Log($"  > effectColorA: {self.effectColorA}");
      Debug.Log($"  > effectColorB: {self.effectColorB}");
      Debug.Log($"  > palette: {palette}");
      Debug.Log($"  > texture: {palette.texture}");
      Debug.Log($"  > allBulbs: {self.allBulbs}");
      if (sLeaser.sprites.Length == 0)
      {
        return;
      }
      self.effectColorA = palette.texture.GetPixel(31, 5);
      self.effectColorB = palette.texture.GetPixel(31, 3);
      foreach (DaddyCorruption.Bulb bulb in self.allBulbs)
      {
        Debug.Log($"    > bulb: {bulb}");
        bulb.ApplyPalette(sLeaser, rCam, palette);
      }
      for (int i = 0; i < self.climbTubes.Count; i++)
      {
        Debug.Log($"  > self.climbTubes[i]: {self.climbTubes[i]}");
        Debug.Log($"  > self.climbTubes[i].graphic: {self.climbTubes[i].graphic}");
        self.climbTubes[i].graphic.ApplyPalette(sLeaser, rCam, palette);
      }
      for (int j = 0; j < self.restrainedDaddies.Count; j++)
      {
        Debug.Log($"    > self.restrainedDaddies[j]: {self.restrainedDaddies[j]}");
        Debug.Log($"    > self.restrainedDaddies[j].graphic: {self.restrainedDaddies[j].graphic}");
        self.restrainedDaddies[j].graphic.ApplyPalette(sLeaser, rCam, palette);
      }
      if (ModManager.MSC)
      {
        for (int k = 0; k < self.neuronLegs.Count; k++)
        {
          Debug.Log($"    > self.neuronLegs[k]: {self.neuronLegs[k]}");
          Debug.Log($"    > self.neuronLegs[k].graphic: {self.neuronLegs[k].graphic}");
          self.neuronLegs[k].graphic.ApplyPalette(sLeaser, rCam, palette);
        }
      }
    }
    */
  }
}
