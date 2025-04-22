using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace meadow_customizations
{
  public class PluginInterface : OptionInterface
  {
    public enum BodyColorMode
    {
      Constant,
      Random
    }

    public enum EyeColorMode
    {
      Constant,
      RandomConstant,
      BodyColor,
      Random,
      Wave,
      SpeedBased
    }

    public readonly Configurable<BodyColorMode> bodyColorMode;
    public readonly Configurable<EyeColorMode> eyeColorMode;

    public readonly Configurable<bool> useCustomBodyColor;
    public readonly Configurable<Color> customBodyColor;
    public readonly Configurable<bool> useCustomEyeColor;
    public readonly Configurable<Color> customEyeColor;
    public readonly Configurable<Color> customEyeWaveColor;
    public readonly Configurable<int> waveSpeed;
    public readonly Configurable<int> eyeSwitchTimer;
    public readonly Configurable<int> colorSpeedMultiplier;
    public readonly Configurable<bool> useCustomNickname;
    public readonly Configurable<bool> showArenaStats;
    public readonly Configurable<bool> useCustomName;
    public readonly Configurable<bool> keepSameArenaStats;
    public readonly Configurable<string> customName;

    private UIelement[] options, bodyColorOptions, eyeColorOptions, nicknameOptions;

    public PluginInterface()
    {
      bodyColorMode = config.Bind("bodyColorMode", BodyColorMode.Constant);
      eyeColorMode = config.Bind("customEyeColorMode", EyeColorMode.Constant);
      useCustomBodyColor = config.Bind("useCustomBodyColor", false);
      customBodyColor = config.Bind("customBodyColor", new Color(0f, 0f, 0f));
      useCustomEyeColor = config.Bind("useCustomEyeColor", false);
      customEyeColor = config.Bind("customEyeColor", new Color(1f, 1f, 1f));
      customEyeWaveColor = config.Bind("customEyeWaveColor", new Color(1f, 1f, 1f));
      waveSpeed = config.Bind("waveSpeed", 69);
      eyeSwitchTimer = config.Bind("eyeSwitchTimer", 10);
      colorSpeedMultiplier = config.Bind("colorSpeedMultiplier", 10);
      useCustomNickname = config.Bind("useCustomNickname", false);
      showArenaStats = config.Bind("showArenaStats", false);
      useCustomName = config.Bind("useCustomName", false);
      keepSameArenaStats = config.Bind("keepSameArenaStats", false);
      customName = config.Bind("customName", "");
    }

    public override void Initialize()
    {
      OpTab optionsTab = new OpTab(this, "Options");
      Tabs = new OpTab[] { optionsTab };

      options = new UIelement[] {
        new OpLabel(10f, 550f, "Options", bigText: true),

        new OpCheckBox(useCustomBodyColor, new Vector2(10f, 510f)),
        new OpLabel(40f, 510f, "Custom body color", bigText: true),

        new OpCheckBox(useCustomEyeColor, new Vector2(310f, 510f)),
        new OpLabel(340f, 510f, "Custom eye color", bigText: true),

        new OpCheckBox(useCustomNickname, new Vector2(10f, 310f)),
        new OpLabel(40f, 310f, "Custom nickname", bigText: true),
      };

      bodyColorOptions = new UIelement[] {
        new OpLabel(40f, 500f, "Note: only changes in the start of round"),
        new OpColorPicker(customBodyColor, new Vector2(10f, 350f)),
        new OpLabel(170f, 480f, "Body color mode"),
        new OpComboBox(bodyColorMode, new Vector2(170f, 450f), 130f, OpResourceSelector.GetEnumNames(null, typeof(BodyColorMode)).ToList()),
      };

      eyeColorOptions = new UIelement[] {
        new OpLabel(340f, 480f, "Base eye color"),
        new OpColorPicker(customEyeColor, new Vector2(310f, 330f)),
        new OpLabel(340f, 300f, "Eye wave color"),
        new OpColorPicker(customEyeWaveColor, new Vector2(310f, 150f)),
        new OpLabel(470f, 480f, "Wave speed"),
        new OpSlider(waveSpeed, new Vector2(470f, 440f), 130) {min = 10, max = 400},
        new OpLabel(470f, 410f, "Color switch timer"),
        new OpSlider(eyeSwitchTimer, new Vector2(470f, 370f), 130) {min = 0, max = 100},
        new OpLabel(470f, 340f, "Speed multiplier"),
        new OpSlider(colorSpeedMultiplier, new Vector2(470f, 300f), 130) {min = 1, max = 100},
        new OpLabel(470f, 180f, "Eye color mode"),
        new OpComboBox(eyeColorMode, new Vector2(470f, 150f), 130f, OpResourceSelector.GetEnumNames(null, typeof(EyeColorMode)).ToList()),
      };

      nicknameOptions = new UIelement[] {
        new OpLabel(40f, 300f, "Note: only changes name, shown over you"),
        new OpCheckBox(showArenaStats, new Vector2(10f, 270f)),
        new OpLabel(40f, 272f, "Show kills/deaths in Arena"),
        new OpCheckBox(keepSameArenaStats, new Vector2(10f, 240f)),
        new OpLabel(40f, 242f, "Keep kills/deaths values in the same lobby"),
        new OpCheckBox(useCustomName, new Vector2(10f, 210f)),
        new OpLabel(40f, 212f, "Use custom name"),
        new OpTextBox(customName, new Vector2(10f, 180f), 150f)
      };

      optionsTab.AddItems(options);
      optionsTab.AddItems(bodyColorOptions);
      optionsTab.AddItems(eyeColorOptions);
      optionsTab.AddItems(nicknameOptions);

      (options[1] as OpCheckBox).OnValueChanged += (UIconfig config, string value, string oldValue) => _use_body_color = value == "true";
      (options[3] as OpCheckBox).OnValueChanged += (UIconfig config, string value, string oldValue) => _use_eye_color = value == "true";
      (options[5] as OpCheckBox).OnValueChanged += (UIconfig config, string value, string oldValue) => _use_nickname = value == "true";
      (nicknameOptions[1] as OpCheckBox).OnValueChanged += (UIconfig config, string value, string oldValue) => _show_arena_stats = value == "true";
      (nicknameOptions[5] as OpCheckBox).OnValueChanged += (UIconfig config, string value, string oldValue) => _use_custom_name = value == "true";
      (bodyColorOptions[3] as OpComboBox).OnValueChanged += (UIconfig config, string value, string oldValue) =>
      _body_color_mode = (BodyColorMode)Enum.Parse(typeof(BodyColorMode), value);
      (eyeColorOptions[11] as OpComboBox).OnValueChanged += (UIconfig config, string value, string oldValue) =>
      _eye_color_mode = (EyeColorMode)Enum.Parse(typeof(EyeColorMode), value);
    }

    public bool _use_body_color, _use_eye_color, _use_nickname, _show_arena_stats, _use_custom_name;
    public BodyColorMode _body_color_mode;
    public EyeColorMode _eye_color_mode;

    public override void Update()
    {
      base.Update();

      (bodyColorOptions[1] as UIfocusable).greyedOut = !_use_body_color
        || _body_color_mode == BodyColorMode.Random;
      (bodyColorOptions[3] as UIfocusable).greyedOut = !_use_body_color;

      (eyeColorOptions[1] as UIfocusable).greyedOut = !_use_eye_color
        || _eye_color_mode == EyeColorMode.Random
        || _eye_color_mode == EyeColorMode.RandomConstant
        || _eye_color_mode == EyeColorMode.BodyColor;
      (eyeColorOptions[3] as UIfocusable).greyedOut = !_use_eye_color
        || (_eye_color_mode != EyeColorMode.Wave
        && _eye_color_mode != EyeColorMode.SpeedBased);
      (eyeColorOptions[5] as UIfocusable).greyedOut = !_use_eye_color
        || _eye_color_mode != EyeColorMode.Wave;
      (eyeColorOptions[7] as UIfocusable).greyedOut = !_use_eye_color
        || _eye_color_mode != EyeColorMode.Random;
      (eyeColorOptions[9] as UIfocusable).greyedOut = !_use_eye_color
        || _eye_color_mode != EyeColorMode.SpeedBased;
      (eyeColorOptions[11] as UIfocusable).greyedOut = !_use_eye_color;

      (nicknameOptions[1] as UIfocusable).greyedOut = !_use_nickname;
      (nicknameOptions[3] as UIfocusable).greyedOut = !_use_nickname
        || !_show_arena_stats;
      (nicknameOptions[5] as UIfocusable).greyedOut = !_use_nickname;
      (nicknameOptions[7] as UIfocusable).greyedOut = !_use_nickname
        || !_use_custom_name;
    }
  }
}
