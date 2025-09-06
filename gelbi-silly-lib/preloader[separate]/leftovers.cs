using System.Collections.Generic;

namespace gelbi_silly_lib.Other;

public static class Extensions
{
  /// <summary>
  /// Adds element to list at given key or creates list, containing that element
  /// </summary>
  public static void AddOrCreateWith<T, V>(this Dictionary<T, List<V>> self, T key, V value)
  {
    if (self.TryGetValue(key, out List<V> list))
      list.Add(value);
    else
      self[key] = new() { value };
  }

  /// <summary>
  /// Adds item to the list if it isn't present already
  /// </summary>
  public static void AddUnique<T>(this List<T> self, T item)
  {
    if (!self.Contains(item))
      self.Add(item);
  }
}