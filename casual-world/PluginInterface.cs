using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace casual_world
{
  public class PluginInterface : OptionInterface
  {
    public readonly Configurable<bool> ignoreEveryone;
    public readonly Configurable<bool> playerIsFriend;
    public readonly Configurable<bool> updateFriendToRandom;

    private UIelement[] options;

    public PluginInterface()
    {
      ignoreEveryone = config.Bind("ignoreEveryone", false);
      playerIsFriend = config.Bind("playerIsFriend", false);
      updateFriendToRandom = config.Bind("updateFriendToRandom", false);
    }

    public override void Initialize()
    {
      OpTab optionsTab = new OpTab(this, "Options");
      Tabs = new OpTab[] { optionsTab };

      options = new UIelement[] {
        new OpLabel(10f, 550f, "Options", bigText: true),

        new OpCheckBox(ignoreEveryone, new Vector2(10f, 510f)),
        new OpLabel(40f, 512f, "Ignore everyone, not just player"),

        new OpCheckBox(playerIsFriend, new Vector2(10f, 480f)),
        new OpLabel(40f, 482f, "Player is friend(some creatures will follow you)"),

        new OpCheckBox(updateFriendToRandom, new Vector2(10f, 450f)),
        new OpLabel(40f, 452f, "[Silly] Select random creature as a friend every minute"),
      };

      optionsTab.AddItems(options);
    }
  }
}
