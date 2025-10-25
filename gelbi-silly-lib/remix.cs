using gelbi_silly_lib.BepInExUtils;
using gelbi_silly_lib.ModManagerUtils;
using Menu.Remix.MixedUI;
using System;
using System.Reflection;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

public class PluginInterface : OptionInterface
{
  private UIelement[] options;
  public bool loggedHooks = false, loggedModList = false;

  public PluginInterface()
  {

  }

  public override void Initialize()
  {
    OpTab optionsTab = new(this, "Options");
    Tabs = [optionsTab];

    options = [
      new OpLabel(10f, 550f, "Options and tools", bigText: true),

      new OpSimpleButton(new(10f, 510f), new(80f, 20f), "Log hooks"),
      new OpSimpleButton(new(100f, 510f), new(80f, 20f), "Log mod list"),
      new OpSimpleButton(new(10f, 480f), new(170f, 20f), "Log loaded sprites")
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

    (options[2] as OpSimpleButton).OnClick += (element) =>
    {
      if (loggedModList)
        return;
      loggedModList = true;

      LogInfo($" * Logging active code mods:");
      foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        if (assembly.HasPluginClassesSafe())
          LogInfo($"{assembly.GetFullPluginName()}");
      LogInfo($" * Finished logging");

      LogInfo($" * Logging all active mods:");
      GSLUtils.LogActiveMods();
      LogInfo($" * Finished logging");

      element.greyedOut = true;
    };

    (options[3] as OpSimpleButton).OnClick += (element) =>
    {
      if (loggedHooks)
        return;

      LogInfo($" * Logging all loaded sprites:");
      GSLUtils.LogAllSprites();
      LogInfo($" * Finished logging");
    };
  }
}

/*

  private OpSimpleButton logHooksButton;
  private Vector2 logHooksButtonOffset = new(-40f, -40f);

  public override void Update()
  {
    Vector2 target = logHooksButton.MousePos + logHooksButtonOffset;
    if (target.sqrMagnitude < 2f)
      return;
    logHooksButton.pos += target.normalized;
    logHooksButton._pos.x = Mathf.Clamp(logHooksButton.pos.x, 0f, 520f);
    logHooksButton._pos.y = Mathf.Clamp(logHooksButton.pos.y, 0f, 520f);
  }

*/