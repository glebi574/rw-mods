using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Random = UnityEngine.Random;

namespace gelbi_silly_lib;

public class PluginInterface : OptionInterface
{
  private UIelement[] options, versioning;

  public Configurable<bool> wrapHooks, wrapPluginInit, noUpdateDisable, disableEOS, hideChangelogs, sillyMainMenu, biggerErrorBox,
    checkHarmonyPatches, checkNativeDetours, checkILHooks, checkDetours;
  public Configurable<int> checksumThreshold;
  public Configurable<float> sizeThreshold;
  public Configurable<string> selectedVersion1, selectedVersion2, selectedAssembly, selectedDate;

  public PluginInterface()
  {
    hideChangelogs = config.Bind("hideChangelogs", false);
    sillyMainMenu = config.Bind("sillyMainMenu", false);
    biggerErrorBox = config.Bind("biggerErrorBox", true);
    wrapHooks = config.Bind("wrapHooks", GSLSettings.instance.wrapHooks);
    wrapPluginInit = config.Bind("wrapPluginInit", GSLSettings.instance.wrapPluginInit);
    noUpdateDisable = config.Bind("noUpdateDisable", GSLSettings.instance.noUpdateDisable);
    disableEOS = config.Bind("disableEOS", GSLSettings.instance.disableEOS);

    checkHarmonyPatches = config.Bind("checkHarmonyPatches", false);
    checkNativeDetours = config.Bind("checkNativeDetours", true);
    checkILHooks = config.Bind("checkILHooks", true);
    checkDetours = config.Bind("checkDetours", false);
    checksumThreshold = config.Bind("checksumThreshold", 256);
    sizeThreshold = config.Bind("sizeThreshold", 0.05f);
    selectedVersion1 = config.Bind("selectedVersion1", "1.9.15b");
    selectedVersion2 = config.Bind("selectedVersion2", GSLPUtils.gameVersion);
    selectedAssembly = config.Bind("selectedAssembly", "gelbi-silly-lib");
    selectedDate = config.Bind("selectedDate", "");
  }

  public override void Initialize()
  {
    OpTab optionsTab = new(this, "Options"), versioningTab = new(this, "Versioning");
    Tabs = [optionsTab, versioningTab];

    float a = 190f, b = 92.5f, h = 5f;
    Color marked = new(0.8f, 0.75f, 0.6f);
    options = [
      new OpLabel(10f, 550f, "Options and tools", bigText: true),

      new OpSimpleButton(new(10f, 510f), new Vector2(b, 20f), "Log hooks") { soundClick = SoundID.Death_Lightning_Spark_Spontaneous },
      new OpSimpleButton(new(10f + b + h, 510f), new Vector2(a, 20f), "Log modify defs") { soundClick = SoundID.SL_AI_Talk_2 },
      new OpSimpleButton(new(10f + b + h * 2f + a, 510f), new Vector2(a, 20f), "Log world defs") { soundClick = SoundID.Token_Collect },
      new OpSimpleButton(new(10f + b + h * 3f + a * 2f, 510f), new Vector2(b, 20f), "---") { soundClick = SoundID.Vulture_Grub_Green_Blink },
      new OpSimpleButton(new(10f, 480f), new Vector2(a, 20f), "Find broken rooms") { soundClick = SoundID.Lizard_Voice_Pink_B, colorEdge = marked },
      new OpSimpleButton(new(10f + a + h, 480f), new Vector2(a, 20f), "Find broken merged rooms") { soundClick = SoundID.Lizard_Head_Shield_Deflect, colorEdge = marked },
      new OpSimpleButton(new(10f + a * 2f + h * 2f, 480f), new Vector2(a, 20f), "Find unresolved references") { soundClick = SoundID.SS_AI_Exit_Work_Mode, colorEdge = marked },
      new OpSimpleButton(new(10f, 450f), new Vector2(b, 20f), "Log references") { soundClick = SoundID.SL_AI_Protest_1 },
      new OpSimpleButton(new(10f + b + h, 450f), new Vector2(a, 20f), "Log dependency targets") { soundClick = SoundID.SS_AI_Give_The_Mark_Boom },
      new OpSimpleButton(new(10f + b + h * 2f + a, 450f), new Vector2(a, 20f), "Log loaded sprites") { soundClick = SoundID.SS_AI_Talk_5},
      new OpSimpleButton(new(10f + b + h * 3f + a * 2f, 450f), new Vector2(b, 20f), "---") { soundClick = SoundID.HUD_Exit_Game },

      new OpLabel(10f, 434f, "(marked tools can take a few seconds to execute)") { color = marked },

      new OpCheckBox(sillyMainMenu, new(10f, 350f)),
      new OpLabel(40f, 350f, "Alternative main menu buttons"),
      new OpCheckBox(hideChangelogs, new(10f, 320f)),
      new OpLabel(40f, 320f, "Hide changelogs button from main menu"),
      new OpCheckBox(biggerErrorBox, new(10f, 290f)),
      new OpLabel(40f, 290f, "Modify appearence of error dialog boxes to be bigger and text in them - left aligned"),

      new OpLabel(10f, 230f, "* Changes to following options will take effect on restart"),

      new OpCheckBox(wrapHooks, new(10f, 200f)),
      new OpLabel(40f, 200f, "[hook wrapping] Wrap all hooks to prevent init failures. May help fix some mods or break them\neven further. Also adds more redundancy to sillier hooks."),
      new OpCheckBox(wrapPluginInit, new(10f, 170f)),
      new OpLabel(40f, 170f, "[plugin init wrapping] Wrap plugin initialization methods to catch all issues that can occur\non different stages and wouldn't be logged otherwise."),
      new OpCheckBox(disableEOS, new(10f, 140f)),
      new OpLabel(40f, 140f, "Disable EpicOnlineServices"),
      new OpCheckBox(noUpdateDisable, new(10f, 110f)),
      new OpLabelLong(new(40f, 70f), new(560f, 60f), "Prevent mods from being disabled on game update. For this option to work, on game update this mod has to be enabled, and you have to manually update version in \"StreamingAssets\\enabledModsVersion.txt\". If you're using this to swap assemblies, just terminate process at the end of preloader patch."),
    ];
    optionsTab.AddItems(options);

    string[] versions = [.. AssemblyMap.managedVersions];
    Array.Sort(versions, (a, b) => GSLPUtils.VersionToValue(b).CompareTo(GSLPUtils.VersionToValue(a)));
    List<string> assemblyNames = [];
    Dictionary<string, Assembly> assemblyMap = [];
    foreach (KeyValuePair<string, HashSet<Assembly>> mod in PluginUtils.modAssemblies)
      foreach (Assembly asm in mod.Value)
      {
        string name = asm.GetName().Name;
        assemblyNames.Add(name);
        assemblyMap[name] = asm;
      }
    versioning = [
      new OpLabel(10f, 550f, "Versioning tools", bigText: true),

      new OpSimpleButton(new(10f, 330f), new(100f, 24f), "Create diff"),
      new OpSimpleButton(new(300f, 330f), new(100f, 24f), "Check hooks"),

      new OpLabelLong(new(10f, 120f), new(280f, 200f), "Creates diff files for specified versions with specified restrictions and outputs it to \"gelbi-silly-lib\\asm-maps\\\".\n\nYou can decide on initial version based on either target version or via workshop update date with the help of the table below."),
      new OpLabelLong(new(300f, 120f), new(280f, 200f), "Checks detours of assembly for changes for specified versions and logs comparison result into \"LogOutput.log\" (and \"customLogs\\gslLog.txt\"). Generally you'd only want to check IL hooks and native detours - other types of hooks can break with updates as well, but more likely issue for them would be removal of members they access or change in other logic. Base methods changing with updates doesn't mean hook is broken - how to interpret results depends on hook itself."),

      new OpTextBox(checksumThreshold, new(10f, 450f), 100f),
      new OpLabel(116f, 452f, "Checksum threshold"),
      new OpTextBox(sizeThreshold, new(10f, 420f), 100f),
      new OpLabel(116f, 422f, "Size threshold"),

      new OpCheckBox(checkDetours, new(300f, 450f)),
      new OpLabel(330f, 452f, "Check detours and hooks"),
      new OpCheckBox(checkILHooks, new(300f, 420f)),
      new OpLabel(330f, 422f, "Check IL hooks"),
      new OpCheckBox(checkNativeDetours, new(300f, 390f)),
      new OpLabel(330f, 392f, "Check native detours"),
      new OpCheckBox(checkHarmonyPatches, new(300f, 360f)),
      new OpLabel(330f, 362f, "Check harmony patches"),

      new OpLabel(10f, 510f, "Initial version"),
      new OpComboBox(selectedVersion1, new(10f, 480f), 100f, versions),
      new OpLabel(120f, 510f, "Target version"),
      new OpComboBox(selectedVersion2, new(120f, 480f), 100f, versions),

      new OpLabel(300f, 510f, "Defining assembly"),
      new OpComboBox(selectedAssembly, new(300f, 480f), 280f, [.. assemblyNames]),

      new OpListBox(selectedDate, new(10f, 70f), 280f, [
        "1.9.01 | 19 January 2023",
        "1.9.02 | 22 January 2023",
        "1.9.03 | 28 January 2023",
        "1.9.04 | 5 February 2023",
        "1.9.05 | 11 February 2023",
        "1.9.06 | 22 February 2023",
        "1.9.07 | 20 March 2023",
        "1.9.07b | 21 March 2023",
        "1.9.13 | 22 December 2023",
        "1.9.15 | 18 March 2024",
        "1.9.15b | 1 April 2024",
        "1.10.0 | 28 March 2025",
        "1.10.1 | 29 March 2025",
        "1.10.2 | 15 April 2025",
        "1.10.3 | 29 April 2025",
        "1.10.4 | 13 May 2025",
        "1.11.1 | 25 September 2025",
        "1.11.2 | 9 October 2025",
        "1.11.3 | 22 October 2025",
        "1.11.4 | 24 November 2025",
        "1.11.5 | 17 December 2025",
        "1.11.6 | 2 February 2026",
        "1.11.7 | 31 March 2026",
        "1.11.7b | 1 April 2026",
        "1.11.8 | 14 April 2026",
      ]),
    ];
    versioningTab.AddItems(versioning);

    for (int i = 1; i < 12; ++i)
      (options[i] as OpSimpleButton).OnClick += element => new SillyButton((OpSimpleButton)element);

    (options[1] as OpSimpleButton).OnClick += _ => GSLTools.LogHooks();
    (options[2] as OpSimpleButton).OnClick += _ => GSLTools.LogModifyDefs();
    (options[3] as OpSimpleButton).OnClick += _ => GSLTools.LogWorldDefs();
    (options[5] as OpSimpleButton).OnClick += _ => GSLTools.ScanForBrokenModRooms();
    (options[6] as OpSimpleButton).OnClick += _ => GSLTools.ScanForBrokenMergedmodsRooms();
    (options[7] as OpSimpleButton).OnClick += _ => GSLTools.ScanForUnresolvedReferences();
    (options[8] as OpSimpleButton).OnClick += _ => GSLTools.LogAssemblyReferences();
    (options[9] as OpSimpleButton).OnClick += _ => GSLTools.LogDependencyTargets();
    (options[10] as OpSimpleButton).OnClick += _ => GSLTools.LogAllSprites();

    (versioning[1] as OpSimpleButton).OnClick +=
      _ => AssemblyMap.Diff((versioning[18] as OpComboBox).value, (versioning[20] as OpComboBox).value, (versioning[5] as OpTextBox).valueInt, (versioning[7] as OpTextBox).valueInt);

    (versioning[2] as OpSimpleButton).OnClick +=
      _ => AssemblyMap.FindChangesForHooks(assemblyMap[(versioning[22] as OpComboBox).value], (versioning[18] as OpComboBox).value,
      (versioning[20] as OpComboBox).value, (versioning[15] as OpCheckBox).value == "true", (versioning[13] as OpCheckBox).value == "true",
      (versioning[11] as OpCheckBox).value == "true", (versioning[9] as OpCheckBox).value == "true");
  }

  public override void Update()
  {
    base.Update();
    SillyButton.Update();

    (versioning[1] as OpSimpleButton).greyedOut = (versioning[2] as OpSimpleButton).greyedOut = (versioning[18] as OpComboBox).value == (versioning[20] as OpComboBox).value;
  }
}

public class SillyButton
{
  public OpSimpleButton button;
  public int timer = 400;
  public float velocity, angle, direction;

  public SillyButton(OpSimpleButton self)
  {
    button = self;
    self.greyedOut = true;
    self.bumpBehav.greyedOut = false;
    int sign = Random.Range(0, 2) * 2 - 1;
    direction = Random.Range(0.1f, 0.2f) * sign;
    angle = Random.Range(Mathf.PI / -2f, 0f) * sign;
    velocity = Random.Range(3f, 4f);
    buttons.Add(this);
  }

  public static List<SillyButton> buttons = [];
  public static void Update()
  {
    for (int i = buttons.Count - 1; i >= 0;  --i)
    {
      SillyButton button = buttons[i];
      float dx = Mathf.Cos(button.angle) * button.velocity, dy = Mathf.Sin(button.angle) * button.velocity + button.direction;
      button.button._pos.x += dx;
      button.button._pos.y += dy;
      button.angle = Mathf.Atan2(dy, dx);
      button.velocity = Mathf.Sqrt(dx * dx + dy * dy);
      if (--button.timer < 0)
        buttons.RemoveAt(i);
    }
  }
}