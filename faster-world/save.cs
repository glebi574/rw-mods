using Kittehface.Framework20;
using Menu;
using MoreSlugcats;
using RWCustom;
using System.Text;
using System.Text.RegularExpressions;
using static faster_world.LogWrapper;

namespace faster_world;

public static class M_Save
{
	public static void PlayerProgression_SaveDeathPersistentDataOfCurrentState(PlayerProgression self, bool saveAsIfPlayerDied, bool saveAsIfPlayerQuit)
  {
    if (self.currentSaveState == null)
    {
      Custom.LogWarning(["Couldn't save death persistent data because no current save state"]);
      return;
    }
    if (ModManager.MMF)
      if (self.rainWorld.processManager?.currentMainLoop is GhostEncounterScreen)
        Custom.LogImportant(["SAVE GHOST, IGNORING STARVATION FOR SAVE"]);
      else if (self.currentSaveState.malnourished)
        if (MMF.cfgVanillaExploits.Value)
          Custom.Log(["MALNOURISHED! But vanilla exploits enabled, so karmacache!"]);
        else
        {
          Custom.Log(["MALNOURISHED! Canceling 30 second safety timer."]);
          saveAsIfPlayerDied = true;
        }
    Custom.Log([$"save deathPersistent data {self.currentSaveState.deathPersistentSaveData.karma} sub karma: {saveAsIfPlayerDied} (quit:{saveAsIfPlayerQuit})"]);
    string saveData = self.currentSaveState.deathPersistentSaveData.SaveToString(saveAsIfPlayerDied, saveAsIfPlayerQuit);
    if (saveData == "")
    {
      Custom.LogWarning(["NO DATA TO WRITE"]);
      return;
    }
    string[] progLinesFromMemory = self.GetProgLinesFromMemory();
    StringBuilder saveString = new(256);
    for (int i = 0; i < progLinesFromMemory.Length; ++i)
    {
      string[] lines = Regex.Split(progLinesFromMemory[i], "<progDivB>");
      if (lines[0] == "SAVE STATE" && BackwardsCompatibilityRemix.ParseSaveNumber(lines[1]) == self.currentSaveState.saveStateNumber)
      {
        StringBuilder data = new(256);
        string[] lineData = Regex.Split(progLinesFromMemory[i], "<svA>");
        for (int j = 0; j < lineData.Length; ++j)
          if (Regex.Split(lineData[j], "<svB>")[0] == "DEATHPERSISTENTSAVEDATA")
            data.Append("DEATHPERSISTENTSAVEDATA<svB>").Append(saveData).Append("<svA>");
          else
            data.Append(lineData[j]).Append("<svA>");
        saveString.Append(data).Append("<progDivA>");
      }
      else
      {
        saveString.Append(progLinesFromMemory[i]);
        if (progLinesFromMemory[i] != "")
          saveString.Append("<progDivA>");
      }
    }
    if (self.saveFileDataInMemory == null || self.loadInProgress)
      return;
    self.saveFileDataInMemory.Set("save", Custom.Md5Sum(saveString.ToString()) + saveString.ToString(), self.canSave ? UserData.WriteMode.Immediate : UserData.WriteMode.Deferred);
    Custom.LogImportant(["Playerprog cansave is:", self.canSave ? "Immediate" : "Deferred"]);
  }
}