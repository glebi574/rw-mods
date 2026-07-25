using Menu.Remix.MixedUI;
using System;
using System.IO;
using UnityEngine;

namespace paletteless_world;

public class PluginInterface : OptionInterface
{
  public readonly Configurable<string> selectedPalette, selectedFadePalette;

  private UIelement[] options;

  public PluginInterface()
  {
    selectedPalette = config.Bind("selectedPalette", "0");
    selectedFadePalette = config.Bind("selectedFadePalette", "0");
  }

  public override void Initialize()
  {
    OpTab optionsTab = new(this, "Options");
    Tabs = [optionsTab];

    Texture2D palette = new(32, 16, TextureFormat.ARGB32, false), fadePalette = new(32, 16, TextureFormat.ARGB32, false);
    UpdatePalette(ref palette, selectedPalette.Value);
    UpdatePalette(ref fadePalette, selectedFadePalette.Value);

    OpImage paletteImage, fadePaletteImage;
    options = [
      new OpLabel(10f, 550f, "Options", bigText: true),

      new OpLabel(10f, 510f, "Selected palette"),
      new OpListBox(selectedPalette, new(10f, 270f), 80f, [.. Plugin.palettes], 10),

      new OpLabel(200f, 510f, "Selected fade palette"),
      new OpListBox(selectedFadePalette, new(200f, 270f), 80f, [.. Plugin.palettes], 10),

      paletteImage = new OpImage(new(100f, 470f), palette) { scale = new(2f, 2f) },
      fadePaletteImage = new OpImage(new(300f, 470f), fadePalette) { scale = new(2f, 2f) },
    ];

    (options[2] as OpListBox).OnValueChanged += (config, value, oldValue) =>
    {
      UpdatePalette(ref palette, value);
      paletteImage.ChangeImage(palette);
    };
    (options[4] as OpListBox).OnValueChanged += (config, value, oldValue) =>
    {
      UpdatePalette(ref fadePalette, value);
      fadePaletteImage.ChangeImage(fadePalette);
    };

    optionsTab.AddItems(options);
  }

  static void UpdatePalette(ref Texture2D texture, string palette)
  {
    try
    {
      AssetManager.SafeWWWLoadTexture(ref texture, AssetManager.ResolveFilePath(Path.Combine("palettes", "palette" + palette + ".png")), false, true);
    }
    catch (Exception)
    {
      AssetManager.SafeWWWLoadTexture(ref texture, AssetManager.ResolveFilePath(Path.Combine("palettes", "palette-1.png")), false, true);
    }
  }

  public int Palette => int.Parse(selectedPalette.Value);
  public int FadePalette => int.Parse(selectedFadePalette.Value);
}