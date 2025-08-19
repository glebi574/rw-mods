using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace gelbi_silly_lib;

/// <summary>
/// Tracks all hooks(automatically untracks internal detours)
/// </summary>
public static class RuntimeDetourManagerInternal
{
  /// <summary>
  /// Hooks per assembly
  /// </summary>
  public static Dictionary<Assembly, List<IDetour>> hookLists = new();
  /// <summary>
  /// Hooks per hooked method
  /// </summary>
  public static Dictionary<MethodBase, List<IDetour>> hookMaps = new();

  public delegate void Hook_d(Hook self, MethodBase from, MethodInfo to, object target, ref HookConfig config);
  public delegate void ILHook_d(ILHook self, MethodBase from, ILContext.Manipulator manipulator, ref ILHookConfig config);
  public delegate void Detour_d(Detour self, MethodBase from, MethodBase to, ref DetourConfig config);

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
  }

  public static void AddHook(IDetour self, MethodBase from, Assembly hookAssembly)
  {
    if (self is Detour detour && detour.Target.DeclaringType == null)
      return;

    if (hookLists.TryGetValue(hookAssembly, out List<IDetour> hookList))
      hookList.Add(self);
    else
      hookLists[hookAssembly] = new() { self };

    if (hookMaps.TryGetValue(from, out List<IDetour> hookMap))
      hookMap.Add(self);
    else
      hookMaps[from] = new() { self };
  }

  public static void Hook_Apply(Action<Hook> orig, Hook self)
  {
    AddHook(self, self.Method, self.Target.DeclaringType.Assembly);
    orig(self);
  }

  public static void ILHook_Apply(Action<ILHook> orig, ILHook self)
  {
    AddHook(self, self.Method, self.Manipulator.Method.DeclaringType.Assembly);
    orig(self);
  }

  public static void Detour_Apply(Action<Detour> orig, Detour self)
  {
    AddHook(self, self.Method, self.Target.DeclaringType?.Assembly);
    orig(self);
  }

  static RuntimeDetourManagerInternal()
  {
    new Hook(typeof(Hook).GetMethod("Apply"), Hook_Apply);
    new Hook(typeof(ILHook).GetMethod("Apply"), ILHook_Apply);
    new Hook(typeof(Detour).GetMethod("Apply"), Detour_Apply);
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
