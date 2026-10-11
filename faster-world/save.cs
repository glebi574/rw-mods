using Kittehface.Framework20;
using Menu;
using MoreSlugcats;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Watcher;

namespace faster_world;

public static class M_Save
{
  public static void PlayerProgression_SaveDeathPersistentDataOfCurrentState(PlayerProgression self, bool saveAsIfPlayerDied, bool saveAsIfPlayerQuit)
  {
    if (self.currentSaveState == null)
    {
      CLogWarning("Couldn't save death persistent data because no current save state");
      return;
    }
    if (ModManager.MMF)
      if (self.rainWorld.processManager?.currentMainLoop is GhostEncounterScreen)
        CLogImportant("SAVE GHOST, IGNORING STARVATION FOR SAVE");
      else if (self.currentSaveState.malnourished && !MMF.cfgVanillaExploits.Value)
        saveAsIfPlayerDied = true;
    if (saveAsIfPlayerDied || saveAsIfPlayerQuit)
      WarpPoint.ScrubNonDeathPersistentData(self.currentSaveState);
    string saveData = self.currentSaveState.deathPersistentSaveData.SaveToString(saveAsIfPlayerDied, saveAsIfPlayerQuit);
    if (saveData.Length == 0)
    {
      CLogWarning("NO DATA TO WRITE");
      return;
    }
    string[] progLinesFromMemory = self.GetProgLinesFromMemory();
    StringBuilder sb = new(256);
    foreach (string progLine in progLinesFromMemory)
    {
      string[] lines = progLine.Split("<progDivB>");
      if (lines[0] == "SAVE STATE" && BackwardsCompatibilityRemix.ParseSaveNumber(lines[1]) == self.currentSaveState.saveStateNumber)
      {
        StringBuilder data = new(256);
        string[] lineData = progLine.Split("<svA>");
        foreach (string line in lineData)
          if (line.SubstringUntil("<svB>") == "DEATHPERSISTENTSAVEDATA")
            data.Append("DEATHPERSISTENTSAVEDATA<svB>").Append(saveData).Append("<svA>");
          else
            data.Append(line).Append("<svA>");
        sb.Append(data).Append("<progDivA>");
      }
      else
      {
        sb.Append(progLine);
        if (progLine.Length != 0)
          sb.Append("<progDivA>");
      }
    }
    if (self.saveFileDataInMemory == null || self.loadInProgress)
      return;
    string baseSave = sb.ToString();
    self.saveFileDataInMemory.Set("save", Custom.Md5Sum(baseSave) + baseSave, self.canSave ? UserData.WriteMode.Immediate : UserData.WriteMode.Deferred);
    CLogImportant(self.canSave ? "Playerprog cansave is: Immediate" : "Playerprog cansave is: Deferred");
  }

  public static SlugcatStats.Name BackwardsCompatibilityRemix_ParseSaveNumber(string firstProgDiv)
  {
    int indexEnd = firstProgDiv.IndexOf("<svA>"), indexStart = firstProgDiv.LastIndexOf("<svB>", indexEnd) + 5;
    string slugcatID = firstProgDiv.Substring(indexStart, indexEnd - indexStart);
    if (int.TryParse(slugcatID, NumberStyles.Any, CultureInfo.InvariantCulture, out int num))
      return BackwardsCompatibilityRemix.ParsePlayerNumber(num);
    return new(slugcatID, false);
  }

  public static bool PlayerProgression_SaveToDisk(PlayerProgression self, bool saveCurrentState, bool saveMaps, bool saveMiscProg)
  {
    if (ModManager.MMF != self.onLoadMMFState
     || ModManager.MSC != self.onLoadMSCState
     || ModManager.Expedition != self.onLoadExpeditionState
     || ModManager.JollyCoop != self.onLoadJollyState
     || ModManager.Watcher != self.onLoadWatcherState)
      goto _invalidData;
    Dictionary<string, bool> installedModStates = [];
    foreach (ModManager.Mod mod in ModManager.InstalledMods)
      if (!self.onLoadEnabledModsState.TryGetValue(mod.id, out bool enabled) || enabled != mod.enabled)
        goto _invalidData;
      else
        installedModStates[mod.id] = mod.enabled;
    foreach (string enabledMod in self.rainWorld.options.enabledMods)
      if (!self.onLoadEnabledModsState.TryGetValue(enabledMod, out bool enabled) || !enabled)
        goto _invalidData;
    foreach (KeyValuePair<string, bool> modState in self.onLoadEnabledModsState)
      if (!installedModStates.TryGetValue(modState.Key, out bool enabled) || enabled != modState.Value)
        goto _invalidData;
    goto _validData;
  _invalidData:
    CLogWarning("!! NOT SAVING BECAUSE SAVE DATA IN MEMORY IS OUT OF SYNC AND INCOMPATIBLE WITH CURRENT GAME STATE!");
    CLogWarning(["MSC: ", ModManager.MSC, " :: ", self.onLoadMSCState]);
    CLogWarning(["MMF: ", ModManager.MMF, " :: ", self.onLoadMMFState]);
    CLogWarning(["Jolly: ", ModManager.JollyCoop, " :: ", self.onLoadJollyState]);
    CLogWarning(["Expedition: ", ModManager.Expedition, " :: ", self.onLoadExpeditionState]);
    CLogWarning(["Watcher: ", ModManager.Watcher, " :: ", self.onLoadWatcherState]);
    CLogWarning("Current mods: ");
    foreach (ModManager.Mod mod in ModManager.InstalledMods)
      CLogWarning([mod.id, " :: ", mod.enabled]);
    CLogWarning("In-memory mods: ");
    foreach (KeyValuePair<string, bool> modState in self.onLoadEnabledModsState)
      CLogWarning([modState.Key, " :: ", modState.Value]);
    return false;
  _validData:
    if (!saveCurrentState && !saveMaps && !saveMiscProg)
    {
      CLogWarning("SaveToDisk without anything to save.");
      return true;
    }
    bool flag3 = false, flag4 = false, moddedRegionsEnabled = ModManager.ModdedRegionsEnabled;
    List<string> maps = [], mapUpdates = [];
    if (saveMaps)
      self.miscProgressionData.SaveDiscoveredShelters(ref self.tempSheltersDiscovered);
    self.tempSheltersDiscovered.Clear();
    string[] progLinesFromMemory = self.GetProgLinesFromMemory();
    StringBuilder sb = new();
    foreach (string line in progLinesFromMemory)
    {
      bool flag5 = false;
      string[] array = line.Split("<progDivB>");
      string def = array[0], saveNumber;
      switch (def)
      {
        case "SAVE STATE":
          saveNumber = array[1];
          if (saveCurrentState && self.currentSaveState != null && BackwardsCompatibilityRemix.ParseSaveNumber(saveNumber) == self.currentSaveState.saveStateNumber)
          {
            sb.Append("SAVE STATE<progDivB>").Append(self.currentSaveState.SaveToString());
            flag3 = true;
          }
          else
            sb.Append(line);
          flag5 = true;
          break;
        case "MAP":
          saveNumber = array[1];
          if (!moddedRegionsEnabled && saveMaps && self.mapDiscoveryTextures.TryGetValue(saveNumber, out Texture2D tex) && tex != null)
          {
            sb.Append("MAP<progDivB>").Append(saveNumber).Append("<progDivB>").Append(Convert.ToBase64String(self.mapDiscoveryTextures[saveNumber].EncodeToPNG()));
            if (self.mapLastUpdatedTime.ContainsKey(saveNumber))
              sb.Append("<progDivA>MAPUPDATE<progDivB>").Append(saveNumber).Append("<progDivB>").Append(self.mapLastUpdatedTime[saveNumber]);
          }
          else
            sb.Append(line);
          flag5 = true;
          maps.Add(saveNumber);
          break;
        case "MISCPROG":
          if (!saveMiscProg)
            sb.Append(line);
          else
            sb.Append("MISCPROG<progDivB>").Append(self.miscProgressionData.ToString());
          flag5 = flag4 = true;
          break;
        default:
          if (!def.StartsWith("MAP_"))
            break;
          if (!def.EndsWith(self.PlayingAsSlugcat.value))
          {
            sb.Append(line);
            flag5 = true;
            break;
          }
          saveNumber = array[1];
          if (moddedRegionsEnabled && saveMaps && self.mapDiscoveryTextures.TryGetValue(saveNumber, out tex) && tex != null)
          {
            sb.Append("MAP_").Append(self.PlayingAsSlugcat.value).Append("<progDivB>").Append(saveNumber).Append("<progDivB>").Append(Convert.ToBase64String(self.mapDiscoveryTextures[saveNumber].EncodeToPNG()));
            if (self.mapLastUpdatedTime.ContainsKey(saveNumber))
              sb.Append("<progDivA>MAPUPDATE_").Append(self.PlayingAsSlugcat.value).Append("<progDivB>").Append(saveNumber).Append("<progDivB>").Append(self.mapLastUpdatedTime[saveNumber]);
          }
          else
            sb.Append(line);
          flag5 = true;
          mapUpdates.Add(saveNumber);
          break;
      }
      if (flag5)
        sb.Append("<progDivA>");
    }
    if (saveCurrentState && !flag3 && self.currentSaveState != null)
      sb.Append("SAVE STATE<progDivB>").Append(self.currentSaveState.SaveToString()).Append("<progDivA>");
    if (saveMaps)
    {
      foreach (KeyValuePair<string, Texture2D> discoveryTex in self.mapDiscoveryTextures)
        if (!moddedRegionsEnabled && !maps.Contains(discoveryTex.Key) && discoveryTex.Value != null)
          sb.Append("MAP<progDivB>").Append(discoveryTex.Key).Append("<progDivB>").Append(Convert.ToBase64String(discoveryTex.Value.EncodeToPNG())).Append("<progDivA>");
        else if (moddedRegionsEnabled && !mapUpdates.Contains(discoveryTex.Key) && discoveryTex.Value != null)
          sb.Append("MAP_").Append(self.PlayingAsSlugcat.value).Append("<progDivB>").Append(discoveryTex.Key).Append("<progDivB>").Append(Convert.ToBase64String(discoveryTex.Value.EncodeToPNG())).Append("<progDivA>");
      foreach (KeyValuePair<string, long> lastUpdated in self.mapLastUpdatedTime)
        if (!moddedRegionsEnabled && !maps.Contains(lastUpdated.Key))
          sb.Append("MAPUPDATE<progDivB>").Append(lastUpdated.Key).Append("<progDivB>").Append(lastUpdated.Value).Append("<progDivA>");
        else if (moddedRegionsEnabled && !mapUpdates.Contains(lastUpdated.Key))
          sb.Append("MAPUPDATE_").Append(self.PlayingAsSlugcat.value).Append("<progDivB>").Append(lastUpdated.Key).Append("<progDivB>").Append(lastUpdated.Value).Append("<progDivA>");
    }
    if (saveMiscProg && !flag4)
      sb.Append("MISCPROG<progDivB>").Append(self.miscProgressionData.ToString()).Append("<progDivA>");
    string baseSave = sb.ToString();
    if (self.saveFileDataInMemory != null && !self.loadInProgress)
      self.saveFileDataInMemory.Set("save", Custom.Md5Sum(baseSave) + baseSave, self.canSave ? UserData.WriteMode.Immediate : UserData.WriteMode.Deferred);
    return true;
  }

  static StringBuilder MPDAppendFor<T>(this StringBuilder sb, List<T> items, string name)
  {
    if (items.Count == 0)
      return sb;
    sb.Append(name).Append(items[0]?.ToString());
    for (int i = 1; i < items.Count; ++i)
      sb.Append(',').Append(items[i]?.ToString());
    return sb.Append("<mpdA>");
  }

  static StringBuilder MPDAppendForC<T>(this StringBuilder sb, List<T> items, string name)
  {
    if (items.Count == 0)
      return sb;
    sb.Append(name).Append(items[0]?.ToString());
    for (int i = 1; i < items.Count; ++i)
      sb.Append("<mpdC>").Append(items[i]?.ToString());
    return sb.Append("<mpdA>");
  }

  public static string MiscProgressionData_ToString(PlayerProgression.MiscProgressionData self)
  {
    StringBuilder sb = new();
    sb.Append("CURRENTSLUGCAT<mpdB>").Append(self.currentlySelectedSinglePlayerSlugcat.value).Append("<mpdA>");
    foreach (KeyValuePair<string, List<string>> shelter in self.discoveredShelters)
    {
      if (shelter.Value == null || shelter.Value.Count == 0)
        continue;
      sb.Append("SHELTERLIST<mpdB>").Append(shelter.Key).Append("<mpdB>");
      foreach (string item in shelter.Value)
        sb.Append(item).Append("<mpdC>");
      sb.Length -= 6;
      sb.Append("<mpdA>");
    }
    if (self.ConditionalShelterDiscovery.Count > 0)
    {
      sb.Append("CONDITIONALSHELTERDATA<mpdB>");
      foreach (PlayerProgression.MiscProgressionData.ConditionalShelterData shelterData in self.ConditionalShelterDiscovery)
        if (shelterData != null)
          sb.Append(shelterData.ToString());
      sb.Append("<mpdA>");
    }
    sb.MPDAppendFor(self.levelTokens, "LEVELTOKENS<mpdB>")
      .MPDAppendFor(self.sandboxTokens, "SANDBOXTOKENS<mpdB>");
    if (ModManager.NewSlugcatsModule)
      sb.MPDAppendFor(self.classTokens, "CLASSTOKENS<mpdB>");
    if (ModManager.MSC)
      sb.MPDAppendFor(self.safariTokens, "SAFARITOKENS<mpdB>").Append("SAINTSTOMACH<mpdB>").Append(self.saintStomachRolloverObject).Append("<mpdA>");
    sb.MPDAppendForC(self.everPlayedArenaLevels, "PLAYEDARENAS<mpdB>");
    if (ModManager.TimelineModule && self.CloakTimelinePosition != null)
      sb.Append("CLOAKTIMELINE<mpdB>").Append(self.CloakTimelinePosition.value).Append("<mpdA>");
    if (ModManager.MSC)
    {
      sb.MPDAppendFor(self.decipheredPearls, "LORE<mpdB>")
        .MPDAppendFor(self.decipheredPebblesPearls, "LOREP<mpdB>")
        .MPDAppendFor(self.decipheredDMPearls, "LOREDM<mpdB>")
        .MPDAppendFor(self.decipheredFuturePearls, "LOREFUT<mpdB>");
      if (self.hasDoneHeartReboot)
        sb.Append("HASDONEHEARTREBOOT<mpdB>1<mpdA>");
      sb.MPDAppendFor(self.discoveredBroadcasts, "BROADCASTS<mpdB>")
        .MPDAppendForC(self.challengeArenaUnlocks, "CHARENAS<mpdB>");
      if (self.completedChallenges.Count > 0)
      {
        sb.Append("CHCLEAR<mpdB>");
        foreach (bool item in self.completedChallenges)
          sb.Append(item ? "1" : "0");
        sb.Append("<mpdA>");
      }
      sb.MPDAppendForC(self.completedChallengeTimes, "CHCLEARTIMES<mpdB>");
      if (self.completedCustomChallenges.Count > 0)
      {
        sb.Append("CUSTOMCHALLENGES<mpdB>");
        foreach (KeyValuePair<string, int> item in self.completedCustomChallenges)
          sb.Append(item.Key).Append(":").Append(item.Value.ToString()).Append("<mpdC>");
        sb.Length -= 6;
        sb.Append("<mpdA>");
      }
    }
    if (ModManager.MMF)
      foreach (KeyValuePair<string, bool> color in self.colorsEnabled)
        if (self.colorChoices.TryGetValue(color.Key, out List<string> colors) && colors?.Count > 0)
        {
          sb.Append("CUSTCOLORS<mpdB>").Append(color.Key).Append("<mpdB>").Append(color.Value ? "1" : "0").Append("<mpdB>");
          foreach (string item in colors)
            sb.Append(item).Append("<mpdC>");
          sb.Length -= 6;
          sb.Append("<mpdA>");
        }
    foreach (KeyValuePair<string, SpeedRunTimer.CampaignTimeTracker> campaignTimer in self.campaignTimers)
      sb.Append("CAMPAIGNTIME<mpdB>").Append(campaignTimer.Key).Append("<mpdB>")
        .Append(Math.Min(SpeedRunTimer.CampaignTimeTracker.maxTimeMilliseconds, campaignTimer.Value.UndeterminedFreeTime).ToString("0")).Append("<mpdB>")
        .Append(Math.Min(SpeedRunTimer.CampaignTimeTracker.maxTimeMilliseconds, campaignTimer.Value.CompletedFreeTime).ToString("0")).Append("<mpdB>")
        .Append(Math.Min(SpeedRunTimer.CampaignTimeTracker.maxTimeMilliseconds, campaignTimer.Value.LostFreeTime).ToString("0")).Append("<mpdB>")
        .Append(Math.Min(SpeedRunTimer.CampaignTimeTracker.maxTimeMilliseconds, campaignTimer.Value.UndeterminedFixedTime).ToString("0")).Append("<mpdB>")
        .Append(Math.Min(SpeedRunTimer.CampaignTimeTracker.maxTimeMilliseconds, campaignTimer.Value.CompletedFixedTime).ToString("0")).Append("<mpdB>")
        .Append(Math.Min(SpeedRunTimer.CampaignTimeTracker.maxTimeMilliseconds, campaignTimer.Value.LostFixedTime).ToString("0")).Append("<mpdA>");
    sb.Append("VISITED<mpdB>");
    foreach (KeyValuePair<string, List<string>> region in self.regionsVisited)
    {
      sb.Append(region.Key).Append("<mpdC>");
      foreach (string item in region.Value)
        sb.Append(item).Append(',');
      if (region.Value.Count != 0)
        --sb.Length;
      sb.Append("<mpdB>");
    }
    if (self.regionsVisited.Count != 0)
      sb.Length -= 6;
    sb.Append("<mpdA>").Append("INTEGERS<mpdB>").Append(SaveUtils.SaveIntegerArray(',', self.integers, self.unrecognizedIntegers)).Append("<mpdA>");
    if (ModManager.MMF)
      sb.Append("INTEGERSMMF<mpdB>").Append(SaveUtils.SaveIntegerArray(',', self.integersMMF, self.unrecognizedIntegersMMF)).Append("<mpdA>");
    if (ModManager.MSC)
      sb.Append("INTEGERSMSC<mpdB>").Append(SaveUtils.SaveIntegerArray(',', self.integersMSC, self.unrecognizedIntegersMSC)).Append("<mpdA>");
    if (ModManager.Watcher)
      sb.Append("INTEGERSWATCHER<mpdB>").Append(SaveUtils.SaveIntegerArray(',', self.integersWatcher, self.unrecognizedIntegersWatcher)).Append("<mpdA>");
    if (self.redsFlower != null)
      sb.Append("REDSFLOWER<mpdB>").Append(self.redsFlower.Value.ResolveRoomName() ?? self.redsFlower.Value.room.ToString())
        .Append('.').Append(self.redsFlower.Value.x)
        .Append('.').Append(self.redsFlower.Value.y)
        .Append('.').Append(self.redsFlower.Value.abstractNode).Append("<mpdA>");
    if (self.menuRegion != null)
      sb.Append("MENUREGION<mpdB>").Append(self.menuRegion).Append("<mpdA>");
    foreach (string str in self.unrecognizedSaveStrings)
      sb.Append(str).Append("<mpdA>");
    return sb.ToString();
  }

  static StringBuilder SaveAppendFor(this StringBuilder sb, ICollection<string> items, string name)
  {
    sb.Append(name);
    foreach (string item in items)
      sb.Append(item).Append("<svB>");
    if (items.Count != 0)
      sb.Length -= 5;
    return sb.Append("<svA>");
  }

  static StringBuilder SaveAppendForC(this StringBuilder sb, List<string> items, string name)
  {
    if (items.Count == 0)
      return sb;
    sb.Append(name);
    foreach (string item in items)
      sb.Append(item).Append("<svC>");
    return sb.Append("<svA>");
  }

  public static string SaveState_SaveToString(SaveState self)
  {
    StringBuilder sb = new();
    sb.Append("SAV STATE NUMBER<svB>").Append(self.saveStateNumber).Append("<svA>")
      .Append("TIMELINE<svB>").Append(self.currentTimelinePosition).Append("<svA>")
      .Append("SEED<svB>").Append(self.seed).Append("<svA>")
      .Append("VERSION<svB>").Append(self.gameVersion).Append("<svA>")
      .Append("INITVERSION<svB>").Append(self.initiatedInGameVersion).Append("<svA>")
      .Append("WORLDVERSION<svB>").Append(self.worldVersion).Append("<svA>")
      .Append("DENPOS<svB>").Append(self.denPosition).Append("<svA>")
      .Append("LASTVDENPOS<svB>").Append(self.lastVanillaDen).Append("<svA>")
      .Append("CYCLENUM<svB>").Append(self.cycleNumber).Append("<svA>")
      .Append("FOOD<svB>").Append(self.food).Append("<svA>")
      .Append("NEXTID<svB>").Append(self.nextIssuedID).Append("<svA>");
    if (self.theGlow)
      sb.Append("HASTHEGLOW<svA>");
    if (self.guideOverseerDead)
      sb.Append("GUIDEOVERSEERDEAD<svA>");
    if (!self.progression.rainWorld.setup.cleanSpawns)
    {
      if (self.respawnCreatures.Count > 0)
      {
        sb.Append("RESPAWNS<svB>");
        foreach (int item in self.respawnCreatures)
          sb.Append(item).Append('.');
        --sb.Length;
        sb.Append("<svA>");
      }
      if (self.waitRespawnCreatures.Count > 0)
      {
        sb.Append("WAITRESPAWNS<svB>");
        foreach (int item in self.waitRespawnCreatures)
          sb.Append(item).Append('.');
        --sb.Length;
        sb.Append("<svA>");
      }
      if (self.creatureCommunitiesString != null)
        sb.Append("COMMUNITIES<svB>").Append(self.creatureCommunitiesString).Append("<svA>");
      for (int i = 0; i < self.regionStates.Length; ++i)
        if (self.regionStates[i] != null)
          sb.Append("REGIONSTATE<svB>").Append(self.regionStates[i].SaveToString()).Append("<svA>");
        else if (self.regionLoadStrings[i] != null)
          sb.Append("REGIONSTATE<svB>").Append(self.regionLoadStrings[i]).Append("<svA>");
      foreach (string item in self.unrecognizedRegionLoadStrings)
        sb.Append("REGIONSTATE<svB>").Append(item).Append("<svA>");
    }
    if (self.swallowedItems != null)
      sb.SaveAppendFor(self.swallowedItems, "SWALLOWEDITEMS<svB>");
    if (self.unrecognizedSwallowedItems?.Count > 0)
      sb.SaveAppendFor(self.unrecognizedSwallowedItems, "UNRECOGNIZEDSWALLOWED<svB>");
    if (self.playerGrasps != null)
      sb.SaveAppendFor(self.playerGrasps, "PLAYERGRASPS<svB>");
    if (self.unrecognizedPlayerGrasps?.Count > 0)
      sb.SaveAppendFor(self.unrecognizedPlayerGrasps, "UNRECOGNIZEDPLAYERGRASPS<svB>");
    sb.Append("DEATHPERSISTENTSAVEDATA<svB>").Append(self.deathPersistentSaveData.SaveToString(false, false)).Append("<svA>");
    string miscWorldData = self.miscWorldSaveData.ToString();
    if (miscWorldData.Length > 0)
      sb.Append("MISCWORLDSAVEDATA<svB>").Append(miscWorldData).Append("<svA>");
    if (self.dreamsState != null)
      sb.Append("DREAMSSTATE<svB>").Append(self.dreamsState.ToString()).Append("<svA>");
    sb.Append("TOTFOOD<svB>").Append(self.totFood).Append("<svA>")
      .Append("TOTTIME<svB>").Append(self.totTime).Append("<svA>")
      .Append("CURRVERCYCLES<svB>").Append(self.cyclesInCurrentWorldVersion).Append("<svA>");
    if (self.kills.Count > 0)
    {
      sb.Append("KILLS<svB>");
      foreach (KeyValuePair<IconSymbol.IconSymbolData, int> item in self.kills)
        sb.Append(item.Key).Append("<svD>").Append(item.Value).Append("<svC>");
      if (self.kills.Count > 0)
        sb.Length -= 5;
      foreach (string item in self.unrecognizedKills)
        sb.Append("<svC>").Append(item);
      sb.Append("<svA>");
    }
    if (self.redExtraCycles)
      sb.Append("REDEXTRACYCLES<svA>");
    if (self.skipNextCycleFoodDrain)
      sb.Append("JUSTBEATGAME<svA>");
    if (ModManager.MSC)
    {
      if (self.hasRobo)
        sb.Append("HASROBO<svA>");
      if (self.wearingCloak)
        sb.Append("CLOAK<svA>");
      if (self.karmaDream)
        sb.Append("KARMADREAM<svA>");
      sb.Append("FORCEPUPS<svB>").Append(self.forcePupsNextCycle).Append("<svA>");
    }
    if (ModManager.MMF)
    {
      if (self.objectTrackers.Count > 0)
      {
        sb.Append("OBJECTTRACKERS<svB>");
        foreach (PersistentObjectTracker tracker in self.objectTrackers)
        {
          if (Custom.rainWorld.processManager.currentMainLoop.manager.menuSetup.FastTravelInitCondition && MMF.cfgKeyItemPassaging.Value && tracker.lastSeenRoom == RainWorld.ShelterBeforePassage)
          {
            tracker.ChangeDesiredSpawnLocation(new((Custom.rainWorld.processManager.currentMainLoop as RainWorldGame).world.GetAbstractRoom(RainWorld.ShelterAfterPassage).index, 0, 0, -1));
            tracker.lastSeenRegion = (tracker.lastSeenRoom = RainWorld.ShelterAfterPassage).Substring(0, 2);
          }
          sb.Append(tracker.ToString()).Append("<svC>");
        }
        if (self.objectTrackers.Count > 0)
          sb.Length -= 5;
        sb.Append("<svA>");
      }
      if (!self.progression.rainWorld.setup.cleanSpawns)
        sb.SaveAppendForC(self.pendingObjects, "OBJECTS<svB>")
          .SaveAppendForC(self.pendingFriendCreatures, "FRIENDS<svB>")
          .SaveAppendForC(self.oeEncounters, "OEENCOUNTERS<svB>");
    }
    if (self.warpPointTargetAfterWarpPointSave != null)
      sb.Append("WARPPOINTTARGET<svB>").Append(self.warpPointTargetAfterWarpPointSave.ToString()).Append("<svA>");
    if (self.transferObjectsAfterWarpPointSave?.Count > 0)
    {
      sb.Append("TRANSFERWARPOBJECTS<svB>");
      foreach (AbstractPhysicalObject item in self.transferObjectsAfterWarpPointSave)
        sb.Append(item.ToString()).Append("<svC>");
      foreach (string item in self.unrecognizedTransferObjectsAfterWarpPointSave)
        sb.Append(item).Append("<svC>");
      sb.Append("<svA>");
    }
    if (self.transferObjectsAfterWarpPointSave?.Count > 0)
    {
      sb.Append("TRANSFERWARPCREATURES<svB>");
      foreach (string item in self.transferCreaturesAfterWarpPointSave)
        sb.Append(item).Append("<svC>");
      foreach (string item in self.unrecognizedTransferCreaturesAfterWarpPointSave)
        sb.Append(item).Append("<svC>");
      sb.Append("<svA>");
    }
    if (self.importantTransferEntitiesAfterWarpPointSave?.Count > 0)
    {
      sb.Append("IMPORTANTTRANSFERS<svB>");
      foreach (EntityID item in self.importantTransferEntitiesAfterWarpPointSave)
        sb.Append(item.ToString()).Append("<svC>");
      sb.Append("<svA>");
    }
    if (self.preserveWarpFatigueAfterWarpPointSave > 0)
      sb.Append("TRANSFERWARPFATIGUE<svB>").Append(self.preserveWarpFatigueAfterWarpPointSave.ToString()).Append("<svA>");
    foreach (string item in self.unrecognizedSaveStrings)
      sb.Append(item).Append("<svA>");
    return sb.ToString();
  }
}