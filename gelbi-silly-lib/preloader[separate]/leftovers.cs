using Mono.Cecil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib.Other
{
  public static class Extensions
  {
    /// <summary>
    /// If character is present in given string, returns substring until that character. Otherwise returns given string
    /// </summary>
    public static string SubstringUntil(this string self, char c)
    {
      int i = self.IndexOf(c);
      if (i == -1)
        return self;
      return self.Substring(0, i);
    }

    /// <summary>
    /// Returns substring from <paramref name="startIndex"/> to <paramref name="endIndex"/>
    /// </summary>
    public static string SubstringUntil(this string self, int startIndex, int endIndex) => self.Substring(startIndex, endIndex - startIndex + 1);

    /// <summary>
    /// Adds element to list at given key or creates list, containing that element
    /// </summary>
    public static void AddOrCreateWith<T, V>(this Dictionary<T, List<V>> self, T key, V value)
    {
      if (self.TryGetValue(key, out List<V> list))
        list.Add(value);
      else
        self[key] = [value];
    }

    /// <summary>
    /// Adds element to hashset at given key or creates hashset, containing that element
    /// </summary>
    public static void AddOrCreateWith<T, V>(this Dictionary<T, HashSet<V>> self, T key, V value)
    {
      if (self.TryGetValue(key, out HashSet<V> hashSet))
        hashSet.Add(value);
      else
        self[key] = [value];
    }

    /// <summary>
    /// Adds key-value pair at given <paramref name="key1"/> or creates dictionary, containing that pair
    /// </summary>
    public static void AddOrCreateWith<T1, T2, V>(this Dictionary<T1, Dictionary<T2, V>> self, T1 key1, T2 key2, V value)
    {
      if (self.TryGetValue(key1, out Dictionary<T2, V> dictionary))
        dictionary[key2] = value;
      else
        self[key1] = new() {{key2, value}};
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
}

namespace gelbi_silly_lib
{
  public static class GSLPUtils
  {
    /// <summary>
    /// Game version value
    /// </summary>
    public static readonly string gameVersion = null;
    /// <summary>
    /// Names of Rain World's assemblies
    /// </summary>
    public static HashSet<string> baseAssemblies = ["HOOKS-Assembly-CSharp"];

    static GSLPUtils()
    {
      try
      {
        gameVersion = ((string)AssemblyDefinition.ReadAssembly("RainWorld_Data\\Managed\\Assembly-CSharp.dll").MainModule
          .GetType("RainWorld").FindField("GAME_VERSION_STRING").Constant).Substring(1); // 9ms
      }
      catch (Exception e)
      {
        LogError(e);
      }

      foreach (string path in Directory.GetFiles("RainWorld_Data\\Managed", "*.dll"))
        baseAssemblies.Add(Path.GetFileNameWithoutExtension(path));
      foreach (string path in Directory.GetFiles("BepInEx\\core", "*.dll"))
        baseAssemblies.Add(Path.GetFileNameWithoutExtension(path));
    }

    public static double VersionToValue(string version)
    {
      StringBuilder sb = new(16);
      foreach (char c in version)
        if (c == '.' || char.IsDigit(c))
          sb.Append(c);
        else
          sb.Append('.').Append(((int)c).ToString().PadLeft(3, '0'));
      string[] values = sb.ToString().Split('.');
      double value = 0;
      for (int i = 0; i < values.Length; ++i)
        value += long.Parse(values[i]) * Math.Pow(0.1, i * 3 + 3);
      return value;
    }
  }
}