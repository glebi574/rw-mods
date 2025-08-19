using gelbi_silly_lib.Other;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace gelbi_silly_lib.Converter;

/// <summary>
/// Converter extensions mainly for JSON reading
/// </summary>
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

  public static void UpdateNumber(this List<object> self, int index, ref int result)
  {
    result = Convert.ToInt32(self[index]);
  }

  public static void UpdateNumber(this List<object> self, int index, ref long result)
  {
    result = Convert.ToInt64(self[index]);
  }

  public static void UpdateNumber(this List<object> self, int index, ref float result)
  {
    result = Convert.ToSingle(self[index]);
  }

  public static void UpdateNumber(this List<object> self, int index, ref double result)
  {
    result = Convert.ToDouble(self[index]);
  }

  public static bool TryGetRGBColorFromHex<TKey>(this Dictionary<TKey, object> self, TKey name, out Color result)
  {
    if (self.TryGetValueWithType(name, out string value))
    {
      result = value.AsRGBColor();
      return true;
    }
    result = default;
    return false;
  }

  public static bool TryGetRGBAColorFromHex<TKey>(this Dictionary<TKey, object> self, TKey name, out Color result)
  {
    if (self.TryGetValueWithType(name, out string value))
    {
      result = value.AsRGBAColor();
      return true;
    }
    result = default;
    return false;
  }

  public static void TryUpdateRGBColorFromHex<TKey>(this Dictionary<TKey, object> self, TKey name, ref Color result)
  {
    if (self.TryGetRGBColorFromHex(name, out Color value))
      result = value;
  }

  public static void TryUpdateRGBAColorFromHex<TKey>(this Dictionary<TKey, object> self, TKey name, ref Color result)
  {
    if (self.TryGetRGBAColorFromHex(name, out Color value))
      result = value;
  }

  /*
    If either of these throws and you're wondering why:
    * ensure cctor for your custom ExtEnum is being called before any of these is called - typeof doesn't trigger cctor
    * ...
    * that's it ig, idk what else could throw as long as you aren't silly
    * ...
    * I didn't event comment these, I wonder if anyone would even dare to use these :>
  */
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
    if (self.TryGetValue(name, out string fieldName))
      return fieldName.TryGetExtEnum(out result, true);
    result = default;
    return false;
  }

  public static bool TryGetExtEnum<T, TKey, TValue>(this Dictionary<TKey, TValue> self, TKey name, out T result) where T : ExtEnum<T>
  {
    if (self.TryGetValueWithType(name, out string fieldName))
      return fieldName.TryGetExtEnum(out result, true);
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

  public static bool TryGetExtEnum<T>(this List<object> self, int index, out T result) where T : ExtEnum<T>
  {
    if (self[index] is string name)
      return name.TryGetExtEnum(out result, true);
    result = default;
    return false;
  }

  public static void TryUpdateExtEnum<T>(this List<object> self, int index, ref T result) where T : ExtEnum<T>
  {
    if (self.TryGetExtEnum(index, out T value))
      result = value;
  }
}