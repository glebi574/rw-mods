using gelbi_silly_lib.ModManagerUtils;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib.OtherExt
{
  public static class Extensions
  {
    public static Color AsRGBColor(this string hex)
    {
      uint rgb = uint.Parse(hex, NumberStyles.HexNumber);
      return new(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
    }

    public static Color AsRGBAColor(this string hex)
    {
      uint rgba = uint.Parse(hex, NumberStyles.HexNumber);
      return new(((rgba >> 24) & 0xff) / 255f, ((rgba >> 16) & 0xff) / 255f, ((rgba >> 8) & 0xff) / 255f, (rgba & 0xff) / 255f);
    }

    public static Color[] ToRGBColorArray(this List<object> self)
    {
      Color[] colors = new Color[self.Count];
      for (int i = 0; i < self.Count; ++i)
        colors[i] = ((string)self[i]).AsRGBColor();
      return colors;
    }

    public static Color[] ToRGBAColorArray(this List<object> self)
    {
      Color[] colors = new Color[self.Count];
      for (int i = 0; i < self.Count; ++i)
        colors[i] = ((string)self[i]).AsRGBAColor();
      return colors;
    }

    /// <summary>
    /// Returns string with mod's name, id and version
    /// </summary>
    public static string GetFullName(this ModManager.Mod self)
    {
      return $"[\"{self.name}\", \"{self.id}\", \"{self.version}\"]";
    }

    /// <summary>
    /// Checks if container has mod
    /// </summary>
    public static bool ContainsMod(this IEnumerable<ModManager.Mod> self, ModManager.Mod other)
    {
      return self.Any(mod => mod.id == other.id);
    }
  }
}

namespace gelbi_silly_lib
{
  public static class GSLUtils
  {
    public static void LogAllSprites()
    {
      foreach (KeyValuePair<string, FAtlasElement> spriteKVP in Futile.atlasManager._allElementsByName)
        LogInfo(spriteKVP.Key);
    }

    public static void LogActiveMods()
    {
      foreach (ModManager.Mod mod in ModManager.ActiveMods)
        LogInfo(mod.GetSimpleName());
    }
  }
}
