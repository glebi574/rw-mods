using Mono.Cecil.Cil;
using MonoMod.Cil;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib.Other
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

    /// <summary>
    /// Adds element to list at given key or creates list, containing that element
    /// </summary>
    public static void AddOrCreateWith<T, V>(this Dictionary<T, List<V>> self, T key, V value)
    {
      if (self.TryGetValue(key, out List<V> hookList))
        hookList.Add(value);
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

    /// <summary>
    /// Returns string with mod's name, id and version
    /// </summary>
    public static string GetFullName(this ModManager.Mod self)
    {
      return $"[\"{self.name}\", \"{self.id}\", \"{self.version}\"]";
    }

    /// <summary>
    /// Checks if container has mod
    /// </summary>
    public static bool ContainsMod(this IEnumerable<ModManager.Mod> self, ModManager.Mod other)
    {
      return self.Any(mod => mod.id == other.id);
    }
  }
}

namespace gelbi_silly_lib
{
  public static partial class GSLUtils
  {
    public static void LogAllAtlases()
    {
      foreach (KeyValuePair<string, FAtlasElement> spriteKVP in Futile.atlasManager._allElementsByName)
        Debug.Log($"sprite: {spriteKVP.Key}");
    }

    public static void LogActiveMods()
    {
      foreach (ModManager.Mod mod in ModManager.ActiveMods)
        Log.LogInfo(mod.name);
    }
  }

  /// <summary>
  /// Mod IDs added to it would be automatically sorted to the bottom of mod list
  /// <para>Doesn't do much, but makes Remix Menu look silly</para>
  /// </summary>
  public static class HighPriorityMods
  {
    private static List<string> managedMods = new();

    public static void Add(string id)
    {
      if (!managedMods.Contains(id))
        managedMods.Add(id);
    }

    private static void SortModList(Menu.Remix.MenuModList self)
    {
      Dictionary<Menu.Remix.MenuModList.ModButton, int> priorities = new();
      foreach (Menu.Remix.MenuModList.ModButton button in self.sortedModButtons)
      {
        int index = managedMods.IndexOf(button.itf.mod.id);
        priorities[button] = index == -1 ? -1 : managedMods.Count - index;
      }
      self.sortedModButtons.Sort((a, b) => priorities[a].CompareTo(priorities[b]));
    }

    public static void MenuModList_RefreshAllButtons(ILContext il)
    {
      ILCursor c = new(il);
      while (c.TryGotoNext(i => i.MatchLdfld<Menu.Remix.MenuModList>("sortedModButtons"))) { }
      ++c.Index;
      c.Emit(OpCodes.Ldarg_0);
      c.EmitDelegate(SortModList);
    }
  }
}
