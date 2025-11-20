using Menu.Remix.MixedUI;
using System.Collections.Generic;

namespace loading_screen;

public class PluginInterface : OptionInterface
{
  public readonly Configurable<bool> useRandomScene;
  public readonly Configurable<string> selectedSceen;

  private UIelement[] options;

  public PluginInterface()
  {
    useRandomScene = config.Bind("useRandomScene", Plugin.useRandomScene);
    selectedSceen = config.Bind("selectedSceen", Plugin.selectedScene);
  }

  public override void Initialize()
  {
    OpTab optionsTab = new(this, "Options");
    Tabs = [optionsTab];

    useRandomScene.Value = Plugin.useRandomScene;
    selectedSceen.Value = Plugin.selectedScene;
    List<string> allowedScenes = Plugin.GetAllowedScenes();

    options = [
      new OpLabel(10f, 550f, "Options", bigText: true),

      new OpCheckBox(useRandomScene, new(10f, 510f)),
      new OpLabel(40f, 510f, "Use random scene(may contain spoilers)"),

      new OpLabel(10f, 480f, "Loading screen scene"),
      new OpComboBox(selectedSceen, new(10f, 450f), 400f, allowedScenes.ToArray())
    ];

    optionsTab.AddItems(options);
  }
}