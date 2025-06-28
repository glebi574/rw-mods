using BepInEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace gelbi_silly_lib
{
  public static class Extensions
  {
    public static bool TryGetValueWithType<T, TKey, TValue>(this Dictionary<TKey, TValue> self, TKey name, out T result)
    {
      if (self.TryGetValue(name, out TValue value) && value is T converted)
      {
        result = converted;
        return true;
      }
      result = default;
      return false;
    }

    public static void TryUpdateValueWithType<T, TKey, TValue>(this Dictionary<TKey, TValue> self, TKey name, ref T result)
    {
      if (self.TryGetValueWithType(name, out T value))
        result = value;
    }

    public static bool TryGetNumber<TKey>(this Dictionary<TKey, object> self, TKey name, out int result)
    {
      if (self.TryGetValue(name, out object value))
      {
        result = Convert.ToInt32(value);
        return true;
      }
      result = default;
      return false;
    }

    public static bool TryGetNumber<TKey>(this Dictionary<TKey, object> self, TKey name, out long result)
    {
      if (self.TryGetValue(name, out object value))
      {
        result = Convert.ToInt64(value);
        return true;
      }
      result = default;
      return false;
    }

    public static bool TryGetNumber<TKey>(this Dictionary<TKey, object> self, TKey name, out float result)
    {
      if (self.TryGetValue(name, out object value))
      {
        result = Convert.ToSingle(value);
        return true;
      }
      result = default;
      return false;
    }

    public static bool TryGetNumber<TKey>(this Dictionary<TKey, object> self, TKey name, out double result)
    {
      if (self.TryGetValue(name, out object value))
      {
        result = Convert.ToDouble(value);
        return true;
      }
      result = default;
      return false;
    }

    public static void TryUpdateNumber<TKey>(this Dictionary<TKey, object> self, TKey name, ref int result)
    {
      if (self.TryGetNumber(name, out int value))
        result = value;
    }

    public static void TryUpdateNumber<TKey>(this Dictionary<TKey, object> self, TKey name, ref long result)
    {
      if (self.TryGetNumber(name, out long value))
        result = value;
    }

    public static void TryUpdateNumber<TKey>(this Dictionary<TKey, object> self, TKey name, ref float result)
    {
      if (self.TryGetNumber(name, out float value))
        result = value;
    }

    public static void TryUpdateNumber<TKey>(this Dictionary<TKey, object> self, TKey name, ref double result)
    {
      if (self.TryGetNumber(name, out double value))
        result = value;
    }

    public static bool TryGetColorFromHex<TKey>(this Dictionary<TKey, object> self, TKey name, out Color result)
    {
      if (self.TryGetValueWithType(name, out string value))
      {
        result = value.AsRGBAColor();
        return true;
      }
      result = default;
      return false;
    }

    public static void TryUpdateColorFromHex<TKey>(this Dictionary<TKey, object> self, TKey name, ref Color result)
    {
      if (self.TryGetColorFromHex(name, out Color value))
        result = value;
    }

    public static bool TryGetExtEnum<T>(this string self, out T result, bool ignoreCase = false) where T : ExtEnum<T>
    {
      if (ExtEnumBase.TryParse(typeof(T), self, ignoreCase, out ExtEnumBase value)
        && value is T converted)
      {
        result = converted;
        return true;
      }
      result = default;
      return false;
    }

    public static void TryUpdateExtEnum<T>(this string self, ref T result, bool ignoreCase = false) where T : ExtEnum<T>
    {
      if (self.TryGetExtEnum(out T value, ignoreCase))
        result = value;
    }

    public static bool TryGetExtEnum<T, TKey>(this Dictionary<TKey, string> self, TKey name, out T result) where T : ExtEnum<T>
    {
      if (self.TryGetValue(name, out string fieldName) && fieldName.TryGetExtEnum(out T value, true))
      {
        result = value;
        return true;
      }
      result = default;
      return false;
    }

    public static bool TryGetExtEnum<T, TKey, TValue>(this Dictionary<TKey, TValue> self, TKey name, out T result) where T : ExtEnum<T>
    {
      if (self.TryGetValueWithType(name, out string fieldName) && fieldName.TryGetExtEnum(out T value, true))
      {
        result = value;
        return true;
      }
      result = default;
      return false;
    }

    public static void TryUpdateExtEnum<T, TKey>(this Dictionary<TKey, string> self, TKey name, ref T result) where T : ExtEnum<T>
    {
      if (self.TryGetExtEnum(name, out T value))
        result = value;
    }

    public static void TryUpdateExtEnum<T, TKey, TValue>(this Dictionary<TKey, TValue> self, TKey name, ref T result) where T : ExtEnum<T>
    {
      if (self.TryGetExtEnum(name, out T value))
        result = value;
    }

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
        colors[i] = (self[i] as string).AsRGBColor();
      return colors;
    }

    public static Color[] ToRGBAColorArray(this List<object> self)
    {
      Color[] colors = new Color[self.Count];
      for (int i = 0; i < self.Count; ++i)
        colors[i] = (self[i] as string).AsRGBAColor();
      return colors;
    }

    public static List<AbstractCreature> GetCreaturesWithType(this List<AbstractCreature> creatures, CreatureTemplate.Type type)
    {
      List<AbstractCreature> result = new();
      foreach (AbstractCreature creature in creatures)
        if (creature.creatureTemplate.type == type)
          result.Add(creature);
      return result;
    }
  }

  public static class Utils
  {
    public static void LogAllAtlases()
    {
      foreach (var v in Futile.atlasManager._allElementsByName)
        Debug.Log($"sprite: {v.Key}");
    }
  }

  [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
  public class Plugin : BaseUnityPlugin
  {
    public const string PLUGIN_GUID = "gelbi.gelbi-silly-lib";
    public const string PLUGIN_NAME = "gelbi's Silly Lib";
    public const string PLUGIN_VERSION = "1.0.1";

    public void OnEnable()
    {

    }
  }
}