using BepInEx;
using MonoMod.RuntimeDetour;
using RainMeadow;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Random = System.Random;

namespace meadow_customizations;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]

public class Plugin : BaseUnityPlugin
{
  public const string PLUGIN_GUID = "gelbi.meadow_customizations";
  public const string PLUGIN_NAME = "Meadow Customizations";
  public const string PLUGIN_VERSION = "1.0.4";

  public PluginInterface pluginInterface;

  public static Random rand = new();
  public bool needNameUpdate = true, needBodyColorUpdate = true, needEyeColorUpdate = true, isArenaMode = false, wasDeadThisSession = false;
  public int playerIndex = -1, deaths = 0, eyeColorCounter = 0, eyeIterator = 0, delayedDeathCounter = 0, rainbowColorStep = 1;
  public float rainbowColorR = 1f, rainbowColorG = 0f, rainbowColorB = 0f;
  public double velocity = 0f;
  public string originalName;
  public OnlinePlayer me = null;
  public List<Player> killedPlayers = [];

  public delegate OnlineEntity.EntityData.EntityDataState
    _dSlugcatCustomization_MakeState(SlugcatCustomization self, OnlineEntity onlineEntity, OnlineResource inResouse);

  public void OnEnable()
  {
    On.RainWorld.OnModsInit += RainWorld_OnModsInit;
    On.ArenaGameSession.Update += ArenaGameSession_Update;
    On.ArenaSitting.ctor += ArenaSitting_ctor;
    On.RainWorldGame.Update += RainWorldGame_Update;

    new Hook(typeof(SlugcatCustomization).GetMethod("MakeState"), SlugcatCustomization_MakeState);
    new Hook(typeof(StoryGameMode).GetMethod("PreGameStart"), StoryGameMode_PreGameStart);
    new Hook(typeof(OnlineManager).GetMethod("LeaveLobby", BindingFlags.Public | BindingFlags.Static), OnlineManager_LeaveLobby);
  }

  public static Color RandomColor() => new((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());

  public void OnlineManager_LeaveLobby(Action orig)
  {
    deaths = 0;
    killedPlayers.Clear();
    orig();
  }

  public void StoryGameMode_PreGameStart(Action<StoryGameMode> orig, StoryGameMode self)
  {
    NeedUpdateEverything();
    orig(self);
  }

  public OnlineEntity.EntityData.EntityDataState SlugcatCustomization_MakeState(_dSlugcatCustomization_MakeState orig,
    SlugcatCustomization self, OnlineEntity onlineEntity, OnlineResource inResource)
  {
    if (pluginInterface.useCustomNickname.Value && needNameUpdate)
    {
      originalName ??= self.nickname;
      bool needSpace = false;
      self.nickname = "";
      if (pluginInterface.showArenaStats.Value && isArenaMode)
      {
        self.nickname += $"[{killedPlayers.Count}/{deaths}]";
        needSpace = true;
      }
      if (needSpace)
        self.nickname += " ";
      if (pluginInterface.useCustomName.Value)
        self.nickname += pluginInterface.customName.Value;
      else
        self.nickname += originalName;
      needNameUpdate = false;
    }

    if (pluginInterface.useCustomBodyColor.Value)
      switch (pluginInterface.bodyColorMode.Value)
      {
        case PluginInterface.BodyColorMode.Constant:
          if (self.bodyColor != pluginInterface.customBodyColor.Value)
            self.bodyColor = pluginInterface.customBodyColor.Value;
          break;
        case PluginInterface.BodyColorMode.Random:
          if (needBodyColorUpdate)
          {
            self.bodyColor = RandomColor();
            needBodyColorUpdate = false;
          }
          break;
      }

    if (pluginInterface.useCustomEyeColor.Value)
      switch (pluginInterface.eyeColorMode.Value)
      {
        case PluginInterface.EyeColorMode.Constant:
          if (self.eyeColor != pluginInterface.customEyeColor.Value)
            self.eyeColor = pluginInterface.customEyeColor.Value;
          break;
        case PluginInterface.EyeColorMode.RandomConstant:
          if (needEyeColorUpdate)
          {
            self.eyeColor = RandomColor();
            needEyeColorUpdate = false;
          }
          break;
        case PluginInterface.EyeColorMode.BodyColor:
          if (self.eyeColor != self.bodyColor)
            self.eyeColor = self.bodyColor;
          break;
        case PluginInterface.EyeColorMode.Random:
          if (pluginInterface.eyeSwitchTimer.Value == 0 || ++eyeColorCounter % pluginInterface.eyeSwitchTimer.Value == 0)
            self.eyeColor = RandomColor();
          break;
        case PluginInterface.EyeColorMode.Wave:
          float multiplier = (float)Math.Sin(++eyeIterator * pluginInterface.waveSpeed.Value / 1000.0);
          Color dColor = pluginInterface.customEyeWaveColor.Value - pluginInterface.customEyeColor.Value;
          self.eyeColor = pluginInterface.customEyeColor.Value + new Color(
            dColor.r * multiplier,
            dColor.g * multiplier,
            dColor.b * multiplier);
          break;
        case PluginInterface.EyeColorMode.SpeedBased:
          float velMultiplier = (float)Math.Min(1.0, Math.Sqrt(velocity / 24.0));
          Color dVelColor = pluginInterface.customEyeWaveColor.Value - pluginInterface.customEyeColor.Value;
          self.eyeColor = pluginInterface.customEyeColor.Value + new Color(
            dVelColor.r * velMultiplier,
            dVelColor.g * velMultiplier,
            dVelColor.b * velMultiplier);
          break;
        case PluginInterface.EyeColorMode.RainbowA:
          float step = pluginInterface.waveSpeed.Value / 1000f;
          switch (rainbowColorStep)
          {
            case 0:
              rainbowColorR = Math.Min(rainbowColorR + step, 1f);
              rainbowColorB = Math.Max(rainbowColorB - step, 0f);
              if (rainbowColorB == 0f)
                rainbowColorStep = 1;
              break;
            case 1:
              rainbowColorG = Math.Min(rainbowColorG + step, 1f);
              rainbowColorR = Math.Max(rainbowColorR - step, 0f);
              if (rainbowColorR == 0f)
                rainbowColorStep = 2;
              break;
            case 2:
              rainbowColorB = Math.Min(rainbowColorB + step, 1f);
              rainbowColorG = Math.Max(rainbowColorG - step, 0f);
              if (rainbowColorG == 0f)
                rainbowColorStep = 0;
              break;
          }
          self.eyeColor = new(rainbowColorR, rainbowColorB, rainbowColorG);
          break;
        case PluginInterface.EyeColorMode.RainbowB:
          float step2 = pluginInterface.waveSpeed.Value / 1000f;
          switch (rainbowColorStep)
          {
            case 0:
              rainbowColorR = Math.Min(rainbowColorR + step2, 1f);
              rainbowColorB = Math.Max(rainbowColorB - step2, 0f);
              rainbowColorG = Math.Min(rainbowColorG + step2, 1f);
              if (rainbowColorB == 0f)
                rainbowColorStep = 1;
              break;
            case 1:
              rainbowColorR = Math.Max(rainbowColorR - step2, 0f);
              rainbowColorB = Math.Min(rainbowColorB + step2, 1f);
              rainbowColorG = Math.Min(rainbowColorG + step2, 1f);
              if (rainbowColorR == 0f)
                rainbowColorStep = 2;
              break;
            case 2:
              rainbowColorR = Math.Min(rainbowColorR + step2, 1f);
              rainbowColorB = Math.Min(rainbowColorB + step2, 1f);
              rainbowColorG = Math.Max(rainbowColorG - step2, 0f);
              if (rainbowColorG == 0f)
                rainbowColorStep = 0;
              break;
          }
          self.eyeColor = new(rainbowColorR, rainbowColorB, rainbowColorG);
          break;
      }

    return orig(self, onlineEntity, inResource);
  }

  public void RainWorldGame_Update(On.RainWorldGame.orig_Update orig, RainWorldGame self)
  {
    orig(self);
    if (OnlineManager.lobby == null || self.IsArenaSession)
      return;

    foreach (AbstractCreature abstractPlayer in self.Players)
      if (abstractPlayer?.realizedCreature?.mainBodyChunk?.vel != null && abstractPlayer.ID.number == 0)
        UpdateVelocity(abstractPlayer.realizedCreature.mainBodyChunk.vel);
  }

  public void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
  {
    orig(self);

    pluginInterface = new PluginInterface();
    MachineConnector.SetRegisteredOI(PLUGIN_GUID, pluginInterface);
  }

  public void NeedUpdateEverything()
  {
    needNameUpdate = true;
    needBodyColorUpdate = true;
    needEyeColorUpdate = true;
    isArenaMode = false;
    wasDeadThisSession = false;
  }

  public void ResetFull()
  {
    NeedUpdateEverything();
    me = null;
    playerIndex = -1;
    if (pluginInterface.keepSameArenaStats.Value)
      return;
    deaths = 0;
    killedPlayers.Clear();
  }

  public bool TryGetMyPlayer(ArenaGameSession self)
  {
    if (OnlineManager.lobby == null || self == null || !self.playersSpawned)
      return false;
    if (playerIndex != -1 && playerIndex < self.arenaSitting.players.Count)
      return true;
    ArenaOnlineGameMode onlineArena = OnlineManager.lobby.gameMode as ArenaOnlineGameMode;
    foreach (ArenaSitting.ArenaPlayer player in self.arenaSitting.players)
    {
      OnlinePlayer onlineArenaPlayer = ArenaHelpers.FindOnlinePlayerByFakePlayerNumber(onlineArena, player.playerNumber);
      if (!onlineArenaPlayer.isMe)
        continue;
      me = onlineArenaPlayer;
      playerIndex = player.playerNumber;
      NeedUpdateEverything();
      return true;
    }
    return false;
  }

  public void UpdateVelocity(Vector2 vel)
  {
    double dx = vel.x, dy = vel.y - 1.5, newVelocity = (float)Math.Sqrt(dx * dx + dy * dy);
    velocity += (newVelocity - velocity) * pluginInterface.colorSpeedMultiplier.Value / 100.0;
  }

  public void ArenaGameSession_Update(On.ArenaGameSession.orig_Update orig, ArenaGameSession self)
  {
    orig(self);
    if (!pluginInterface.useCustomNickname.Value || !TryGetMyPlayer(self))
      return;
    isArenaMode = true;

    foreach (AbstractCreature abstractPlayer in self.Players)
    {
      if (abstractPlayer.ID.number == 0)
        UpdateVelocity(abstractPlayer.realizedCreature.mainBodyChunk.vel);
      if (abstractPlayer.realizedCreature is Player player && player.dead && player.killTag != null && player.killTag.ID.number == 0 && !killedPlayers.Contains(player))
      {
        killedPlayers.Add(player);
        needNameUpdate = true;
      }
    }

    if (delayedDeathCounter != self.arenaSitting.players[playerIndex].deaths)
    {
      delayedDeathCounter = self.arenaSitting.players[playerIndex].deaths;
      needNameUpdate = true;
      if (delayedDeathCounter != 0)
        ++deaths;
    }
  }

  public void ArenaSitting_ctor(On.ArenaSitting.orig_ctor orig, ArenaSitting self, ArenaSetup.GameTypeSetup gameTypeSetup, MultiplayerUnlocks multiplayerUnlocks)
  {
    orig(self, gameTypeSetup, multiplayerUnlocks);
    if (OnlineManager.lobby == null)
      return;
    ResetFull();
  }
}
