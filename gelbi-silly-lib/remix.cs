using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

public class PluginInterface : OptionInterface
{
  private UIelement[] options;
  public bool loggedHooks = false, loggedModList = false, loggedReferences = false;

  public Configurable<bool> wrapHooks, noUpdateDisable, disableEOS;

  public PluginInterface()
  {
    wrapHooks = config.Bind("wrapHooks", GSLSettings.instance.wrapHooks);
    noUpdateDisable = config.Bind("noUpdateDisable", GSLSettings.instance.noUpdateDisable);
    disableEOS = config.Bind("disableEOS", GSLSettings.instance.disableEOS);
  }

  public override void Initialize()
  {
    OpTab optionsTab = new(this, "Options");
    Tabs = [optionsTab];

    options = [
      new OpLabel(10f, 550f, "Options and tools", bigText: true),

      new OpSimpleButton(new(10f, 510f), new(80f, 20f), "Log hooks"),
      new OpSimpleButton(new(100f, 510f), new(80f, 20f), "---") {greyedOut = true},
      new OpSimpleButton(new(10f, 480f), new(170f, 20f), "Log references"),
      new OpSimpleButton(new(10f, 450f), new(170f, 20f), "Log loaded sprites"),

      new OpLabel(10f, 230f, "* Changes to following options will take effect on restart"),

      new OpCheckBox(wrapHooks, new(10f, 200f)),
      new OpLabel(40f, 200f, "Wrap all hooks to prevent init failures. May help fix some mods or break them even further.\nAlso adds more redundancy to sillier hooks."),
      
      new OpCheckBox(disableEOS, new(10f, 170f)),
      new OpLabel(40f, 170f, "Disable EpicOnlineServices"),

      new OpCheckBox(noUpdateDisable, new(10f, 130f)),
      new OpLabelLong(new(40f, 100f), new(560f, 60f), "Prevent mods from being disabled on game update. For this option to work on game update mod has to be enabled and you have to manually update version in \"StreamingAssets\\GameVersion.txt\". If you're using this to swap assemblies, just terminate process at the end of preloader patch."),
    ];

    optionsTab.AddItems(options);

    (options[1] as OpSimpleButton).OnClick += (element) =>
    {
      if (loggedHooks)
        return;
      loggedHooks = true;
      RuntimeDetourManager.LogAllHookLists();
      RuntimeDetourManager.LogAllHookMaps();
      element.greyedOut = true;
    };

    (options[3] as OpSimpleButton).OnClick += (element) =>
    {
      if (loggedReferences)
        return;
      loggedReferences = true;

      LogInfo(" * Logging all assembly references:");
      foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
      {
        if (GSLPUtils.baseAssemblies.Contains(assembly.GetName().Name))
          continue;
        LogInfo($"{assembly.GetName().Name}:");
        foreach (AssemblyName referenceName in assembly.GetReferencedAssemblies())
          if (!GSLPUtils.baseAssemblies.Contains(referenceName.Name))
            LogInfo($"  {referenceName.Name}");
      }
      LogInfo(" * Finished logging");

      element.greyedOut = true;
    };

    (options[4] as OpSimpleButton).OnClick += (element) =>
    {
      if (loggedHooks)
        return;

      LogInfo($" * Logging all loaded sprites:");
      GSLUtils.LogAllSprites();
      LogInfo($" * Finished logging");
    };
  }
}