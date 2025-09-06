using gelbi_silly_lib.Converter;
using gelbi_silly_lib.OtherExt;
using System.Collections.Generic;
using UnityEngine;

namespace gelbi_silly_lib.ConverterExt;

/// <summary>
/// Converter extensions for colors, ExtEnums and other things
/// </summary>
public static class Extensions
{
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
    If either of these throw and you're wondering why:
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