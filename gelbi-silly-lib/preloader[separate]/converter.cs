using System;
using System.Collections.Generic;

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

  public static void UpdateNumber(this List<object> self, int index, ref int result) => result = Convert.ToInt32(self[index]);

  public static void UpdateNumber(this List<object> self, int index, ref long result) => result = Convert.ToInt64(self[index]);

  public static void UpdateNumber(this List<object> self, int index, ref float result) => result = Convert.ToSingle(self[index]);

  public static void UpdateNumber(this List<object> self, int index, ref double result) => result = Convert.ToDouble(self[index]);

  public static void UpdateValue(this string[] self, int index, ref bool result) => result = Convert.ToBoolean(self[index]);

  public static void UpdateNumber(this string[] self, int index, ref int result) => result = Convert.ToInt32(self[index]);

  public static void UpdateNumber(this string[] self, int index, ref long result) => result = Convert.ToInt64(self[index]);

  public static void UpdateNumber(this string[] self, int index, ref float result) => result = Convert.ToSingle(self[index]);

  public static void UpdateNumber(this string[] self, int index, ref double result) => result = Convert.ToDouble(self[index]);
}