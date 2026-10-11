using Menu.Remix.MixedUI;

namespace faster_world;

public class PluginInterface : OptionInterface
{
  public UIelement[] options;

  public Configurable<bool> preloadRooms, cacheRooms, precacheRooms;

  public PluginInterface()
  {
    preloadRooms = config.Bind("preloadRooms", true);
    cacheRooms = config.Bind("cacheRooms", true);
    precacheRooms = config.Bind("precacheRooms", true);
  }

  public override void Initialize()
  {
    OpTab optionsTab = new(this, "Options");
    Tabs = [optionsTab];

    options = [
      new OpLabel(10f, 550f, "Options", bigText: true),

      new OpLabelLong(new(10f, 430f), new(590f, 120f), "Faster World implements a number of optimizations, that speed up world loading at the cost of increased memory usage. If your RAM capacity is low (<4GB), you may want to tweak some of these. Nobody reported issues related to increased memory usage before (virtual memory on SSD/NVMEs is rather fast now, which may already solve any issues that arise on devices with low memory capacity), so this may be completely unnecessary though. By default region files are read simultaneously (instead of each file being read from disk when needed), with regions connected to it via gates being loaded in background. Only current and neighbouring regions' cache is preserved (besides some other minor cache). Overall that increases average memory usage by ~500MB (40% of memory usage of the base game), but actual value depends on regions."),

      new OpCheckBox(preloadRooms, new(10f, 380f)),
      new OpLabelLong(new(40f, 363f), new(560f, 60f), "Preload region. If enabled, region files are loaded at once, which is faster than the way game loads them one by one. Slightly increases peak memory usage when region is being loaded. Required for other caching optimizations.") { verticalAlignment = OpLabel.LabelVAlignment.Bottom },
      new OpCheckBox(cacheRooms, new(10f, 330f)),
      new OpLabelLong(new(40f, 327f), new(560f, 60f), "Cache rooms. If enabled, loaded and preloaded room files will be cached, so they don't have to be read from disk later. Speeds up load times at the cost of increased memory usage.") { verticalAlignment = OpLabel.LabelVAlignment.Bottom },
      new OpCheckBox(precacheRooms, new(10f, 280f)),
      new OpLabelLong(new(40f, 277f), new(560f, 60f), "Precache rooms. If enabled, on startup mod will attempt to cache region, last selected slugcat is located at in current save. Also precaches some other small files.") { verticalAlignment = OpLabel.LabelVAlignment.Bottom },
    ];

    optionsTab.AddItems(options);
  }

  public override void Update()
  {
    base.Update();

    (options[6] as OpCheckBox).greyedOut = (options[4] as OpCheckBox).greyedOut = (options[2] as OpCheckBox)._value == "false";
    (options[6] as OpCheckBox).greyedOut = (options[4] as OpCheckBox).greyedOut || (options[4] as OpCheckBox)._value == "false";
  }
}