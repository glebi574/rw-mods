using Mono.Cecil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("gelbi-silly-lib")]

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
    /// Returns string, based on original one, consisting of chunks below <paramref name="lineLength"/> separating whitespace characters of which are replaced by <paramref name="replacement"/>
    /// </summary>
    public static string SplitByLength(this string self, int lineLength, string replacement = "\n")
    {
      if (self.Length < lineLength)
        return self;
      StringBuilder sb = new(self.Length << 1);
      int cursor = lineLength, previous = 0;
      while (cursor < self.Length)
      {
        while (cursor >= previous && !char.IsWhiteSpace(self[cursor]))
          --cursor;
        if (cursor < previous)
        {
          cursor = previous + lineLength;
          while (cursor < self.Length && !char.IsWhiteSpace(self[cursor]))
            ++cursor;
        }
        sb.Append(self.Substring(previous, cursor - previous)).Append(replacement);
        previous = ++cursor;
        cursor += lineLength;
      }
      return sb.Append(self.Substring(previous, self.Length - previous)).ToString();
    }

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
    /// For given <paramref name="key"/>, adds <paramref name="value"/> to corresponding value or sets it to <paramref name="baseValue"/>
    /// </summary>
    public static void AddOrAdd<T>(this Dictionary<T, int> self, T key, int value = 1, int baseValue = 1)
    {
      if (self.ContainsKey(key))
        self[key] += value;
      else
        self[key] = baseValue;
    }

    /// <summary>
    /// Adds item to the list if it isn't present already
    /// </summary>
    public static void AddUnique<T>(this List<T> self, T item)
    {
      if (!self.Contains(item))
        self.Add(item);
    }

    public static int IndexOf(this StringBuilder self, char c, int startIndex = 0)
    {
      if (startIndex < 0)
        startIndex = self.Length + startIndex;
      for (int i = startIndex; i < self.Length; ++i)
        if (self[i] == c)
          return i;
      return -1;
    }

    public static int LastIndexOf(this StringBuilder self, char c, int startIndex = -1)
    {
      if (startIndex < 0)
        startIndex = self.Length + startIndex;
      for (int i = startIndex; i >= 0; --i)
        if (self[i] == c)
          return i;
      return -1;
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
    public static readonly string gameVersion = null, targetVersion = null;
    /// <summary>
    /// gsl assembly
    /// </summary>
    public static readonly Assembly gslpAssembly = typeof(GSLPUtils).Assembly;
    /// <summary>
    /// Names of Rain World's assemblies
    /// </summary>
    public static HashSet<string> baseAssemblies = [];
    /// <summary>
    /// Event invoked once when either of supported process exit events is triggered
    /// </summary>
    public static event Action OnExit;
    internal static bool runOnExit = false;

    static GSLPUtils()
    {
      try
      {
        gameVersion = ((string)AssemblyDefinition.ReadAssembly("RainWorld_Data\\Managed\\Assembly-CSharp.dll").MainModule
          .GetType("RainWorld").FindField("GAME_VERSION_STRING").Constant).Substring(1); // 9ms
        targetVersion = "v" + gameVersion;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => InvokeOnExit();
      }
      catch (Exception e)
      {
        LogError(e);
      }

      foreach (string path in Directory.GetFiles("RainWorld_Data\\Managed", "*.dll"))
        baseAssemblies.Add(Path.GetFileNameWithoutExtension(path));
      foreach (string dirPath in Directory.GetDirectories("BepInEx"))
        foreach (string path in Directory.GetFiles(dirPath, "*.dll"))
          baseAssemblies.Add(Path.GetFileNameWithoutExtension(path));
    }

    internal static void InvokeOnExit()
    {
      if (runOnExit)
        return;
      runOnExit = true;
      OnExit?.Invoke();
      foreach (WriterThread thread in WriterThread.threads)
        thread.Close();
    }

    /// <summary>
    /// Returns comparable version value. Mainly meant meant for Rain World versions, but should work for other versions too.
    /// </summary>
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