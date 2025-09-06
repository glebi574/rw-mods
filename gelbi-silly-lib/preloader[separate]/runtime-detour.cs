using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.ReflectionUtils;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
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
  public static Dictionary<Assembly, List<IDetour>> hookLists = new();
  /// <summary>
  /// Hooks per hooked method
  /// </summary>
  public static Dictionary<MethodBase, List<IDetour>> hookMaps = new();
  /// <summary>
  /// Targets of native detours
  /// </summary>
  public static Dictionary<NativeDetour, MethodBase> nativeDetourTargets = new();

  private static bool DisposeInvalidHook(IDetour self)
  {
    if (self.IsValid)
      return false;
    self.Dispose();
    return true;
  }

  /// <summary>
  /// Removes invalid hooks
  /// </summary>
  public static void Update()
  {
    foreach (KeyValuePair<Assembly, List<IDetour>> hookListKVP in hookLists)
      hookListKVP.Value.RemoveAll(DisposeInvalidHook);

    List<MethodBase> emptyMaps = new();
    foreach (KeyValuePair<MethodBase, List<IDetour>> hookMapKVP in hookMaps)
    {
      hookMapKVP.Value.RemoveAll(DisposeInvalidHook);
      if (hookMapKVP.Value.Count == 0)
        emptyMaps.Add(hookMapKVP.Key);
    }
    foreach (MethodBase method in emptyMaps)
      hookMaps.Remove(method);

    List<NativeDetour> invalidDetours = new();
    foreach (KeyValuePair<NativeDetour, MethodBase> nativeKVP in nativeDetourTargets)
      if (DisposeInvalidHook(nativeKVP.Key))
        invalidDetours.Add(nativeKVP.Key);
    foreach (NativeDetour native in invalidDetours)
      nativeDetourTargets.Remove(native);
  }

  public static void AddHook(IDetour self, MethodBase from, MethodBase to)
  {
    if (self is Detour detour && detour.Target.DeclaringType == null)
      return;

    if (hookLists.TryGetValue(to.DeclaringType.Assembly, out List<IDetour> hookList))
      hookList.Add(self);
    else
      hookLists[to.DeclaringType.Assembly] = new() { self };

    if (hookMaps.TryGetValue(from, out List<IDetour> hookMap))
      hookMap.Add(self);
    else
      hookMaps[from] = new() { self };

    if (self is NativeDetour native)
      nativeDetourTargets[native] = to;
  }

  public static void Hook_Apply(Action<Hook> orig, Hook self)
  {
    AddHook(self, self.Method, self.Target);
    orig(self);
  }

  public static void ILHook_Apply(Action<ILHook> orig, ILHook self)
  {
    AddHook(self, self.Method, self.Manipulator.Method);
    orig(self);
  }

  public static void Detour_Apply(Action<Detour> orig, Detour self)
  {
    AddHook(self, self.Method, self.Target);
    orig(self);
  }

  public delegate void NativeDetour_ctor_d(NativeDetour self, MethodBase from, MethodBase to, ref NativeDetourConfig config);
  public static void NativeDetour_ctor_mmc(NativeDetour_ctor_d orig, NativeDetour self, MethodBase from, MethodBase to, ref NativeDetourConfig config)
  {
    orig(self, from, to, ref config);
    AddHook(self, from, to);
  }

  public static void NativeDetour_ctor_mm(Action<NativeDetour, MethodBase, MethodBase> orig, NativeDetour self, MethodBase from, MethodBase to)
  {
    orig(self, from, to);
    AddHook(self, from, to);
  }

  static RuntimeDetourManager()
  {
    new Hook(typeof(Hook).GetMethod("Apply"), Hook_Apply);
    new Hook(typeof(ILHook).GetMethod("Apply"), ILHook_Apply);
    new Hook(typeof(Detour).GetMethod("Apply"), Detour_Apply);
    new Hook(typeof(NativeDetour).GetConstructor(new Type[] { typeof(MethodBase), typeof(MethodBase), typeof(NativeDetourConfig).MakeByRefType() }), NativeDetour_ctor_mmc);
    new Hook(typeof(NativeDetour).GetConstructor(new Type[] { typeof(MethodBase), typeof(MethodBase) }), NativeDetour_ctor_mm);
  }

  /// <summary>
  /// Logs all hooks by defining assembly
  /// </summary>
  public static void LogAllHookLists()
  {
    LogInfo($" * Logging all caught hooks by assembly:");
    foreach (KeyValuePair<Assembly, List<IDetour>> hookListKVP in hookLists)
    {
      LogInfo($"{hookListKVP.Key}");
      foreach (IDetour idetour in hookListKVP.Value)
        LogInfo($"  ({idetour.GetType().Name}{(idetour.IsApplied ? "" : "/inactive")}) {idetour.GetTarget()?.GetFullName()}");
    }
    LogInfo($" * Finished logging");
  }

  /// <summary>
  /// Logs all hooks by hooked method
  /// </summary>
  public static void LogAllHookMaps()
  {
    LogInfo($" * Logging all caught hooks by hooked method:");
    foreach (KeyValuePair<MethodBase, List<IDetour>> hookMapKVP in hookMaps)
    {
      LogInfo($"{hookMapKVP.Key.GetSimpleName()}");
      foreach (IDetour idetour in hookMapKVP.Value)
        LogInfo($"  ({idetour.GetType().Name}{(idetour.IsApplied ? "" : "/inactive")}) {idetour.GetTarget()?.GetFullName()}");
    }
    LogInfo($" * Finished logging");
  }
}

// Method - method, that's being hooked
// Target/Manipulator.Method - method, that's being hooked with

/*

test:

public void wawa1() { }
public void wawa2(Action<RuntimeDetourManager> orig, RuntimeDetourManager self) { }

new Hook(typeof(RuntimeDetourManager).GetMethod("wawa1"), wawa2);

*/
