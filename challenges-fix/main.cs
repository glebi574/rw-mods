using BepInEx;
using MoreSlugcats;
using RWCustom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Watcher;
using MonoMod.RuntimeDetour;

namespace challenges_fix
{
  [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
  public class Plugin : BaseUnityPlugin
  {
    public const string PLUGIN_GUID = "gelbi.challenges_fix";
    public const string PLUGIN_NAME = "Challenges Fix";
    public const string PLUGIN_VERSION = "1.0.2";

    public void OnEnable()
    {
      On.AbstractCreature.setCustomFlags += AbstractCreature_setCustomFlags;
      On.SuperStructureFuses.ctor += SuperStructureFuses_ctor;

      _ = new Hook(typeof(DangleFruit)
        .GetProperty(nameof(DangleFruit.AbstrConsumable))
        .GetGetMethod(),
        (Func<Func<DangleFruit, DangleFruit.AbstractDangleFruit>, DangleFruit, DangleFruit.AbstractDangleFruit>)get_abstrConsumable);
    }

    public void SuperStructureFuses_ctor(On.SuperStructureFuses.orig_ctor orig, SuperStructureFuses self, PlacedObject placedObject, IntRect rect, Room room)
    {
      self.placedObject = placedObject;
      self.pos = placedObject.pos;
      self.rect = rect;
      self.size = 10f;
      self.lights = new float[rect.Width * (int)(20f / self.size), rect.Height * (int)(20f / self.size), 5];
      self.depth = 0;
      for (int i = rect.left; i <= rect.right; i++)
      {
        for (int j = rect.bottom; j <= rect.top; j++)
        {
          if (!room.GetTile(i, j).Solid && (room.GetTile(i, j).wallbehind ? 1 : 2) > self.depth)
          {
            self.depth = (room.GetTile(i, j).wallbehind ? 1 : 2);
          }
        }
      }
      if (room.world.game.IsArenaSession)
      {
        self.broken = room.roomSettings.GetEffectAmount(RoomSettings.RoomEffect.Type.CorruptionSpores);
      }
      else
      {
        self.broken = room.roomSettings.GetEffectAmount(RoomSettings.RoomEffect.Type.CorruptionSpores);
        if (room.world.region != null && room.world.region.name != "SS" && room.world.region.name != "UW")
        {
          self.broken = 1f;
        }
      }
      self.gravityDependent = room.roomSettings.GetEffectAmount(RoomSettings.RoomEffect.Type.BrokenZeroG) > 0f;
      self.power = 1f;
      self.powerFlicker = 1f;
    }

    public static DangleFruit.AbstractDangleFruit get_abstrConsumable(Func<DangleFruit, DangleFruit.AbstractDangleFruit> orig, DangleFruit self)
    {
      if (self.abstractPhysicalObject is DangleFruit.AbstractDangleFruit abstract_fruit)
        return abstract_fruit;
      int index = (self.abstractPhysicalObject as AbstractConsumable).placedObjectIndex;
      DangleFruit.AbstractDangleFruit abstrConsumable = new DangleFruit.AbstractDangleFruit(
        self.room.world,
        self.abstractPhysicalObject.realizedObject,
        self.abstractPhysicalObject.pos,
        self.abstractPhysicalObject.ID,
        self.room.abstractRoom.index,
        index,
        index == -1 ? false : ModManager.Watcher && self.room.roomSettings.placedObjects[index].type == WatcherEnums.PlacedObjectType.RottenDangleFruit,
        index == -1 ? null : self.room.roomSettings.placedObjects[index].data as PlacedObject.ConsumableObjectData);
      abstrConsumable.isConsumed = false;
      self.abstractPhysicalObject = abstrConsumable;
      return abstrConsumable;
    }

    public void AbstractCreature_setCustomFlags(On.AbstractCreature.orig_setCustomFlags orig, AbstractCreature self)
    {
      if (self.Room == null)
      {
        return;
      }
      self.nightCreature = false;
      self.ignoreCycle = false;
      self.superSizeMe = false;
      self.tentacleImmune = false;
      self.voidCreature = false;
      if (self.Room.world.game.IsStorySession && self.Room.world.rainCycle.BlizzardWorldActive)
      {
        if (ModManager.HypothermiaModule && self.creatureTemplate.BlizzardAdapted)
        {
          self.HypothermiaImmune = true;
        }
        if (self.creatureTemplate.BlizzardWanderer)
        {
          self.ignoreCycle = true;
        }
      }
      if (ModManager.MSC && self.Room.world.game.IsArenaSession && self.Room.world.game.GetArenaGameSession.arenaSitting.gameTypeSetup.gameType == MoreSlugcatsEnums.GameTypeID.Challenge)
      {
        ChallengeInformation.ChallengeMeta challengeMeta = self.Room.world.game.GetArenaGameSession.arenaSitting.gameTypeSetup.challengeMeta;
        if (challengeMeta.globalTag != null && challengeMeta.globalTag == "Lavasafe")
        {
          self.lavaImmune = true;
        }
        else if (challengeMeta.globalTag != null && challengeMeta.globalTag == "Voidsea")
        {
          self.voidCreature = true;
          self.lavaImmune = true;
        }
        else if (challengeMeta.globalTag != null && challengeMeta.globalTag == "TentacleImmune")
        {
          self.tentacleImmune = true;
        }
      }

      List<string> list = new List<string>(), list2;
      if (self.Room.world.region != null)
      {
        list = self.Room.world.region.regionParams.globalCreatureFlags_All.ToList<string>();
        if (self.Room.world.region.regionParams.globalCreatureFlags_Specific.TryGetValue(self.creatureTemplate.type, out list2))
          list.AddRange(list2);
      }

      if (self.spawnData != null && self.spawnData[0] == '{')
      {
        list.AddRange(self.spawnData.Substring(1, self.spawnData.Length - 2).Split(new char[] { ',', '|' }));
      }
      for (int i = 0; i < list.Count; i++)
      {
        if (list[i].Length > 0)
        {
          string text = list[i].Split(new char[] { ':' })[0];
          if (text != null)
          {
            uint num = 0;
            if (text != null)
            {
              num = 2166136261U;
              for (int q = 0; q < text.Length; q++)
                num = ((uint)text[q] ^ num) * 16777619U;
            }
            if (num <= 1833284959U)
            {
              if (num <= 823615862U)
              {
                if (num != 181328940U)
                {
                  if (num != 273444142U)
                  {
                    if (num == 823615862U)
                    {
                      if (text == "Voidsea")
                      {
                        self.lavaImmune = true;
                        self.voidCreature = true;
                      }
                    }
                  }
                  else if (text == "RotType")
                  {
                    if (self.state != null)
                    {
                      LizardState lizardState = self.state as LizardState;
                      if (lizardState != null)
                      {
                        int num2 = int.Parse(list[i].Split(new char[] { ':' })[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                        lizardState.SetRotType(new LizardState.RotType(ExtEnum<LizardState.RotType>.values.entries[Mathf.Min(num2, ExtEnum<LizardState.RotType>.values.entries.Count - 1)], false));
                      }
                    }
                  }
                }
                else if (text == "Winter")
                {
                  self.Winterized = true;
                }
              }
              else if (num != 1374120126U)
              {
                if (num != 1538563819U)
                {
                  if (num == 1833284959U)
                  {
                    if (text == "AlternateForm")
                    {
                      self.superSizeMe = true;
                    }
                  }
                }
                else if (text == "Ignorecycle")
                {
                  self.ignoreCycle = true;
                }
              }
              else if (text == "TentacleImmune")
              {
                self.tentacleImmune = true;
              }
            }
            else if (num <= 2872150327U)
            {
              if (num != 2093489894U)
              {
                if (num != 2358907137U)
                {
                  if (num == 2872150327U)
                  {
                    if (text == "Ripple")
                    {
                      self.rippleCreature = true;
                      self.rippleLayer = 1;
                    }
                  }
                }
                else if (text == "Night")
                {
                  self.nightCreature = true;
                  self.ignoreCycle = false;
                }
              }
              else if (text == "Lavasafe")
              {
                self.lavaImmune = true;
              }
            }
            else if (num != 2911605551U)
            {
              if (num != 3497134284U)
              {
                if (num == 4145925224U)
                {
                  if (text == "PreCycle")
                  {
                    self.preCycle = true;
                  }
                }
              }
              else if (text == "Seed")
              {
                self.ID.setAltSeed(int.Parse(list[i].Split(new char[] { ':' })[1], NumberStyles.Any, CultureInfo.InvariantCulture));
                self.personality = new AbstractCreature.Personality(self.ID);
              }
            }
            else if (text == "Slayer")
            {
              if (self.state != null)
              {
                Tardigrade.TardigradeState tardigradeState = self.state as Tardigrade.TardigradeState;
                if (tardigradeState != null)
                {
                  tardigradeState.slayer = true;
                }
              }
            }
          }
        }
      }
      if (ModManager.PrecycleModule && self.preCycle && self.Room.shelter)
      {
        Custom.Log(new string[]
        {
        self.ToString(),
        "precycle flag disabled, creature started with player in the shelter!"
        });
        self.preCycle = false;
      }
      if (ModManager.Watcher && self.state != null)
      {
        LizardState lizardState2 = self.state as LizardState;
        if (lizardState2 != null && self.Room.world.regionState != null && self.Room.world.regionState.sentientRotProgression.ContainsKey(self.Room.name))
        {
          RegionState.SentientRotState sentientRotState = self.Room.world.regionState.sentientRotProgression[self.Room.name];
          LizardState.RotType rotType = LizardState.RotType.None;
          if (sentientRotState.rotIntensity > 0.75f)
          {
            if (self.ID.number % 10 < 4)
            {
              rotType = LizardState.RotType.Full;
            }
            else if (self.ID.number % 10 < 7)
            {
              rotType = LizardState.RotType.Opossum;
            }
            else
            {
              rotType = LizardState.RotType.Slight;
            }
          }
          else if (sentientRotState.rotIntensity > 0.5f)
          {
            if (self.ID.number % 10 < 1)
            {
              rotType = LizardState.RotType.Full;
            }
            else if (self.ID.number % 10 < 4)
            {
              rotType = LizardState.RotType.Opossum;
            }
            else if (self.ID.number % 10 < 8)
            {
              rotType = LizardState.RotType.Slight;
            }
          }
          else if (sentientRotState.rotIntensity > 0.25f)
          {
            if (self.ID.number % 10 < 2)
            {
              rotType = LizardState.RotType.Opossum;
            }
            else if (self.ID.number % 10 < 6)
            {
              rotType = LizardState.RotType.Slight;
            }
          }
          else if (sentientRotState.rotIntensity > 0f)
          {
            if (self.ID.number % 10 < 1)
            {
              rotType = LizardState.RotType.Opossum;
            }
            else if (self.ID.number % 10 < 3)
            {
              rotType = LizardState.RotType.Slight;
            }
          }
          if (Region.IsWatcherVanillaRegion(self.Room.world.name) && self.Room.world.game.IsStorySession && self.Room.world.game.GetStorySession.saveState.miscWorldSaveData.discoveredWarpPoints.Count == 0)
          {
            if (self.Room.world.game.GetStorySession.saveState.deathPersistentSaveData.maximumRippleLevel >= 0.5f)
            {
              if (rotType == LizardState.RotType.Full)
              {
                rotType = LizardState.RotType.Opossum;
              }
            }
            else
            {
              rotType = LizardState.RotType.None;
            }
          }
          if (rotType.Index > lizardState2.rotType.Index)
          {
            lizardState2.SetRotType(rotType);
          }
        }
      }
    }
  }
}
