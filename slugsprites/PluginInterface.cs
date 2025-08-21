using Menu.Remix.MixedUI;
using UnityEngine;

namespace slugsprites;

public class PluginInterface : OptionInterface
{
  public readonly Configurable<bool> debugMode;
  public readonly Configurable<KeyCode> reloadKey;

  private UIelement[] options;

  public PluginInterface()
  {
    debugMode = config.Bind("debugMode", false);
    reloadKey = config.Bind("reloadKey", KeyCode.J);
  }

  public override void Initialize()
  {
    OpTab optionsTab = new OpTab(this, "Options");
    Tabs = new OpTab[] { optionsTab };

    options = new UIelement[] {
      new OpLabel(10f, 550f, "Options", bigText: true),

      new OpLabel(10f, 510f, "Debug mode"),
      new OpCheckBox(debugMode, new(10f, 480f)),

      new OpLabel(10f, 450f, "Reload key"),
      new OpKeyBinder(reloadKey, new(10f, 415f), new(80f, 30f))
    };

    optionsTab.AddItems(options);

    (options[2] as OpCheckBox).OnValueChanged += (UIconfig config, string value, string oldValue) => _debug_mode = value == "true";
  }

  public bool _debug_mode;

  public override void Update()
  {
    base.Update();

    (options[4] as UIfocusable).greyedOut = !_debug_mode;
  }
}
