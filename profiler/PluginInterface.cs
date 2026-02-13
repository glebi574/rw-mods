using Menu.Remix.MixedUI;

namespace profiler;

public class PluginInterface : OptionInterface
{
  public Configurable<bool> profileGlobal, conditionalProfileGlobal, profileMods, profileModInit;

  public UIelement[] options;

  public PluginInterface()
  {
    profileGlobal = config.Bind("profileGlobal", Patcher.settings.profileGlobal);
    conditionalProfileGlobal = config.Bind("conditionalProfileGlobal", Patcher.settings.conditionalProfileGlobal);
    profileMods = config.Bind("profileMods", Patcher.settings.profileMods);
    profileModInit = config.Bind("profileModInit", Patcher.settings.profileModInit);
  }

  public override void Initialize()
  {
    OpTab optionsTab = new(this, "Options");
    Tabs = [optionsTab];

    options = [
      new OpLabel(10f, 550f, "Options", bigText: true),

      new OpCheckBox(profileGlobal, new(10f, 510f)),
      new OpLabel(40f, 510f, "Profile Rain World and related assemblies"),
      new OpCheckBox(conditionalProfileGlobal, new(30f, 480f)),
      new OpLabel(60f, 480f, "Profile only complex methods"),
      new OpCheckBox(profileMods, new(10f, 450f)),
      new OpLabel(40f, 450f, "Profile mods' methods"),
      new OpCheckBox(profileModInit, new(10f, 420f)),
      new OpLabel(40f, 420f, "Profile mod init stages"),
    ];

    optionsTab.AddItems(options);
  }

  public override void Update()
  {
    base.Update();

    (options[3] as OpCheckBox).greyedOut = (options[1] as OpCheckBox)._value == "false";
  }
}