using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;
using BepInEx;
using IL;

namespace no_stuck_pups
{
  [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
  public class NoStackPups : BaseUnityPlugin
  {
    public const string PLUGIN_GUID = "gelbi.no_stuck_pups";
    public const string PLUGIN_NAME = "No stuck pups";
    public const string PLUGIN_VERSION = "1.0.2";

    public int latest_cycle = -1, attempt_counter = 0;

    public void OnEnable()
    {
      On.Room.Loaded += Room_Loaded;
      On.RainWorldGame.ExitToMenu += RainWorldGame_ExitToMenu;
    }

    private void RainWorldGame_ExitToMenu(On.RainWorldGame.orig_ExitToMenu orig, RainWorldGame self)
    {
      orig(self);
      latest_cycle = -1;
    }

    public void Room_Update(On.Room.orig_Update orig, Room self)
    {
      orig(self);
      int slugcat_counter = 0;
      bool is_pup = false;
      foreach (AbstractCreature creature in self.abstractRoom.creatures)
        if (creature is AbstractPhysicalObject apo && apo.realizedObject is Player slugcat)
        {
          ++slugcat_counter;
          if (slugcat.isSlugpup)
            is_pup = true;
        }
      if (slugcat_counter == 0)
        return;
      if (is_pup && slugcat_counter > 1)
      {
        WorldCoordinate target_pos = new WorldCoordinate();
        List<Player> pups = new List<Player>();
        foreach (AbstractCreature creature in self.abstractRoom.creatures)
          if (creature is AbstractPhysicalObject apo && apo.realizedObject is Player slugcat)
            if (slugcat.isSlugpup)
            {
              if (pups.Count() == 0 && target_pos == default)
                target_pos = slugcat.abstractCreature.pos;
              pups.Add(slugcat);
            }
            else
              target_pos = slugcat.abstractCreature.pos;
        foreach (Player pup in pups)
        {
          pup.abstractCreature.pos = target_pos;
        }
        On.Room.Update -= Room_Update;
        return;
      }
      if (++attempt_counter > 10)
        On.Room.Update -= Room_Update;
    }

    public void Room_Loaded(On.Room.orig_Loaded orig, Room self)
    {
      orig(self);
      if (self.world == null || self.world.regionState == null || latest_cycle == self.world.regionState.saveState.cycleNumber)
        return;
      latest_cycle = self.world.regionState.saveState.cycleNumber;
      attempt_counter = 0;
      On.Room.Update += Room_Update;
    }
  }
}
