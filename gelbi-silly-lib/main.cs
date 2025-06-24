using BepInEx;
using System;
using System.Collections;
using System.Collections.Generic;
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
    public const string PLUGIN_VERSION = "1.0.0";

    public void OnEnable()
    {

    }
  }
}