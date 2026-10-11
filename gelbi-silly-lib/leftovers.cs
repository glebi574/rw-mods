using Menu;
using RWCustom;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

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
  }
}

namespace gelbi_silly_lib
{
  public static class GSLUtils
  {
    /// <summary>
    /// Fixes menu objects being offset on resolutions other than 16:9. Assumes you place them with margin relative to borders and definitely isn't a universal solution to the problem
    /// <para>Most compatible solution is to do what Rain World itself does and assume 200 borders everywhere</para>
    /// </summary>
    public static void CorrectForResolution(List<MenuObject> objects, int fromInclusive, int toExclusive)
    {
      if (ScreenWidth >= 1366f)
        return;
      int width = (int)ScreenWidth, diff = 1366 - width;
      for (int i = fromInclusive; i < toExclusive; ++i)
      {
        MenuObject obj = objects[i];
        if (obj is RectangularMenuObject rect && rect.size.x + rect.pos.x > width)
          rect.size.x -= diff;
        if (obj is PositionedMenuObject positioned)
          positioned.pos.x += diff / 2;
      }
    }

    /// <summary>
    /// Rain World options
    /// </summary>
    public static Options Options => Custom.rainWorld.options;
    public static Vector2 ScreenSize => Custom.rainWorld.options.ScreenSize;
    public static float ScreenWidth => Custom.rainWorld.options.ScreenSize.x;
    public static float ScreenHeight => Custom.rainWorld.options.ScreenSize.y;
    /// <summary>
    /// Difference to correct offset on width that isn't 1366. Add for sprites and substract for menu objects ☻
    /// </summary>
    public static float WidthCorrection => Custom.rainWorld.options.ScreenSize.x / 2f - 683f;
  }
}