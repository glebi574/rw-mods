using RWCustom;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace gelbi_silly_lib.IEnumerableUtils;

/// <summary>
/// Some additions to IEnumerable methods
/// </summary>
public static class Extensions
{
  public static sbyte Sum<T>(this IEnumerable<T> self, Func<T, sbyte> method)
  {
    sbyte result = default;
    foreach (T value in self)
      result += method(value);
    return result;
  }

  public static byte Sum<T>(this IEnumerable<T> self, Func<T, byte> method)
  {
    byte result = default;
    foreach (T value in self)
      result += method(value);
    return result;
  }

  public static short Sum<T>(this IEnumerable<T> self, Func<T, short> method)
  {
    short result = default;
    foreach (T value in self)
      result += method(value);
    return result;
  }

  public static ushort Sum<T>(this IEnumerable<T> self, Func<T, ushort> method)
  {
    ushort result = default;
    foreach (T value in self)
      result += method(value);
    return result;
  }

  public static uint Sum<T>(this IEnumerable<T> self, Func<T, uint> method)
  {
    uint result = default;
    foreach (T value in self)
      result += method(value);
    return result;
  }

  public static ulong Sum<T>(this IEnumerable<T> self, Func<T, ulong> method)
  {
    ulong result = default;
    foreach (T value in self)
      result += method(value);
    return result;
  }

  public static IntVector2 Sum<T>(this IEnumerable<T> self, Func<T, IntVector2> method)
  {
    IntVector2 result = default;
    foreach (T value in self)
      result += method(value);
    return result;
  }

  public static Vector2 Sum<T>(this IEnumerable<T> self, Func<T, Vector2> method)
  {
    Vector2 result = default;
    foreach (T value in self)
      result += method(value);
    return result;
  }

  public static bool TryGetValue<TKey, TValue>(this Dictionary<TKey, TValue> self, Func<TKey, bool> comparer, out TValue value)
  {
    foreach (KeyValuePair<TKey, TValue> pair in self)
      if (comparer(pair.Key))
      {
        value = pair.Value;
        return true;
      }
    value = default;
    return false;
  }
}