using Menu.Remix.MixedUI;
using System.Collections.Generic;

namespace loading_screen;

public class PluginInterface : OptionInterface
{
  public readonly Configurable<bool> useRandomScene, showInitLists;
  public readonly Configurable<string> selectedSceen;

  private UIelement[] options;

  public PluginInterface()
  {
    useRandomScene = config.Bind("useRandomScene", Plugin.useRandomScene);
    showInitLists = config.Bind("showInitLists", Plugin.showInitLists);
    selectedSceen = config.Bind("selectedSceen", Plugin.selectedScene);
  }

  public override void Initialize()
  {
    OpTab optionsTab = new(this, "Options");
    Tabs = [optionsTab];

    List<string> allowedScenes = Plugin.GetAllowedScenes();

    options = [
      new OpLabel(10f, 550f, "Options", bigText: true),

      new OpCheckBox(useRandomScene, new(10f, 510f)),
      new OpLabel(40f, 510f, "Use random scene(may contain spoilers)"),

      new OpCheckBox(showInitLists, new(10f, 480f)),
      new OpLabel(40f, 480f, "Show left panel with initialization lists"),

      new OpLabel(10f, 450f, "Loading screen scene"),
      new OpListBox(selectedSceen, new(10f, 110f), 400f, allowedScenes.ToArray(), 15)
    ];

    optionsTab.AddItems(options);
  }
}