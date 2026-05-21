using System.Collections.Generic;
using System.Globalization;
using System.Text;
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

    public static void LogTiles(Room room)
    {
      StringBuilder sb = new StringBuilder(room.Tiles.Length + 64).AppendLine($"* Logging tiles of {room.abstractRoom.name}:");
      for (int i = room.Tiles.GetLength(1) - 1; i >= 0; --i)
      {
        for (int j = 0; j < room.Tiles.GetLength(0); ++j)
          sb.Append(room.Tiles[j, i].Terrain switch
          {
            Room.Tile.TerrainType.ShortcutEntrance => 'E',
            Room.Tile.TerrainType.Solid => '█',
            Room.Tile.TerrainType.Air => ' ',
            Room.Tile.TerrainType.Floor => '=',
            Room.Tile.TerrainType.Slope => '◇',
          });
        sb.AppendLine();
      }
      LogInfo(sb.ToString());
    }
  }
}