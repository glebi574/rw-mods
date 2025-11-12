using Menu.Remix.MixedUI;

namespace profiler;

public class PluginInterface : OptionInterface
{
  public Configurable<bool> profileGlobal, profileMods, profileModInit;

  public UIelement[] options;

  public PluginInterface()
  {
    profileGlobal = config.Bind("profileGlobal", Patcher.profileGlobal);
    profileMods = config.Bind("profileMods", Patcher.profileMods);
    profileModInit = config.Bind("profileModInit", Patcher.profileModInit);
  }

  public override void Initialize()
  {
    OpTab optionsTab = new(this, "Options");
    Tabs = [optionsTab];

    options = [
      new OpLabel(10f, 550f, "Options", bigText: true),

      new OpCheckBox(profileGlobal, new(10f, 510f)),
      new OpLabel(40f, 510f, "Profile Rain World and related assemblies"),
      new OpCheckBox(profileMods, new(10f, 480f)),
      new OpLabel(40f, 480f, "Profile mods' methods"),
      new OpCheckBox(profileModInit, new(10f, 450f)),
      new OpLabel(40f, 450f, "Profile mod init stages"),
    ];

    optionsTab.AddItems(options);
  }
}