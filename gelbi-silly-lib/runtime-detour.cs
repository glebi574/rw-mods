using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.ReflectionUtils;
using MonoMod.RuntimeDetour;
using System.Collections.Generic;
using System.Reflection;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

/// <summary>
/// Tracks all hooks(automatically untracks internal detours)
/// </summary>
public static class RuntimeDetourManager
{
  /// <summary>
  /// Hooks per assembly
  /// </summary>
  public static Dictionary<Assembly, List<IDetour>> HookLists { get => RuntimeDetourManagerInternal.hookLists; }
  /// <summary>
  /// Hooks per hooked method
  /// </summary>
  public static Dictionary<MethodBase, List<IDetour>> HookMaps { get => RuntimeDetourManagerInternal.hookMaps; }

  /// <summary>
  /// Removes invalid hooks
  /// </summary>
  public static void Update() => RuntimeDetourManagerInternal.Update();

  /// <summary>
  /// Logs all hooks by defining assembly
  /// </summary>
  public static void LogAllHookLists()
  {
    Log.LogInfo($" * Logging all caught hooks by assembly:");
    foreach (KeyValuePair<Assembly, List<IDetour>> hookListKVP in HookLists)
    {
      Log.LogInfo($"{hookListKVP.Key}");
      foreach (IDetour idetour in hookListKVP.Value)
        Log.LogInfo($"  {idetour.GetSimpleTargetName()}");
    }
    Log.LogInfo($" * Finished logging");
  }

  /// <summary>
  /// Logs all hooks by hooked method
  /// </summary>
  public static void LogAllHookMaps()
  {
    Log.LogInfo($" * Logging all caught hooks by hooked method:");
    foreach (KeyValuePair<MethodBase, List<IDetour>> hookMapKVP in HookMaps)
    {
      Log.LogInfo($"{hookMapKVP.Key.GetSimpleName()}");
      foreach (IDetour idetour in hookMapKVP.Value)
        Log.LogInfo($"  {idetour.GetSimpleTargetName()}");
    }
    Log.LogInfo($" * Finished logging");
  }
}