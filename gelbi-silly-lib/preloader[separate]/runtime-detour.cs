using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.Other;
using gelbi_silly_lib.ReflectionUtils;
using HarmonyLib;
using HarmonyLib.Public.Patching;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.Platforms;
using MonoMod.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

/// <summary>
/// Tracks all hooks(automatically untracks internal detours)
/// </summary>
public static class RuntimeDetourManager
{
  /// <summary>
  /// MethodInfo instance of HarmonyLib's Manipulator
  /// </summary>
  public static MethodInfo HarmonyLibManipulator = typeof(ManagedMethodPatcher).GetMethod("Manipulator", BFlags.anyInstance);
  /// <summary>
  /// Hooks per assembly
  /// </summary>
  public static Dictionary<Assembly, List<IDetour>> hookLists = [];
  /// <summary>
  /// Hooks per hooked method
  /// </summary>
  public static Dictionary<MethodBase, List<IDetour>> hookMaps = [];
  /// <summary>
  /// Methods of native detours
  /// </summary>
  public static Dictionary<NativeDetour, MethodBase> nativeDetourMethods = [];
  /// <summary>
  /// Targets of native detours
  /// </summary>
  public static Dictionary<NativeDetour, MethodBase> nativeDetourTargets = [];
  /// <summary>
  /// Native detours per method
  /// </summary>
  public static Dictionary<MethodBase, List<NativeDetour>> nativeDetourMaps = [];
  /// <summary>
  /// Pinned methods at pointers
  /// </summary>
  public static ConcurrentDictionary<long, MethodBase> pinnedMethods = [];
  /// <summary>
  /// Hooks defining DMDs
  /// </summary>
  public static Dictionary<MethodBase, IDetour> DMDOwners = [];

  public static FieldInfo StackFrame_methodAddress = typeof(StackFrame).GetField("methodAddress", BFlags.anyDeclaredInstance);

  public static void AddHook(IDetour self, MethodBase from, MethodBase to)
  {
    if (self is Detour internalDetour && internalDetour.Target.DeclaringType == null)
      return;

    hookLists.AddOrCreateWith(to.DeclaringType.Assembly, self);
    hookMaps.AddOrCreateWith(from, self);

    switch (self)
    {
      case NativeDetour native:
        nativeDetourMethods[native] = from;
        nativeDetourTargets[native] = to;
        nativeDetourMaps.AddOrCreateWith(from, native);
        return;
      case Detour detour:
        DMDOwners[detour.TargetReal] = self;
        return;
      case Hook hook:
        DMDOwners[hook.Detour.TargetReal] = self;
        return;
    }
  }

  /// <summary>
  /// Wraps expression and introduces checks for native detours if possible. Just enable hook wrapping to use this one.
  /// Otherwise you'd only have a reason to use this if you're using hook config, that specifies manual application.
  /// </summary>
  public static void WrappedApply<T>(Action<T> orig, T self) where T : IDetour
  {
    try
    {
      if (self is NativeDetour native
        && nativeDetourMethods.TryGetValue(native, out MethodBase method) && nativeDetourTargets.TryGetValue(native, out MethodBase target))
      {
        if (!method.GetReturnType().IsCompatible(target.GetReturnType()))
          throw new InvalidOperationException($"Return type of native detour for {method.GetFullSimpleName()} doesn't match, must be {method.GetReturnType().GetSimpleName()}");
        ParameterInfo[] methodArgs = method.GetParameters(), targetArgs = target.GetParameters();
        int offset = method.IsStatic ? 0 : 1;
        if (methodArgs.Length + offset != targetArgs.Length)
          throw new InvalidOperationException($"Parameter count of native detour for {method.GetFullSimpleName()} doesn't match, must be {methodArgs.Length + offset}");
        if (offset == 1 && !method.DeclaringType.IsCompatible(targetArgs[0].ParameterType))
          throw new InvalidOperationException($"Parameter #0 of native detour for {method.GetFullSimpleName()} doesn't match, must be {method.DeclaringType.GetSimpleName()} or related");
        for (int i = 0; i < methodArgs.Length; ++i)
          if (!methodArgs[i].ParameterType.IsCompatible(targetArgs[i + offset].ParameterType))
            throw new InvalidOperationException($"Parameter #{i + offset} of native detour for {method.GetFullSimpleName()} doesn't match, must be {methodArgs[i].ParameterType.GetSimpleName()} or related");
      }
      orig(self);
    }
    catch (Exception e)
    {
      LogError($"Exception, while applying hook{{{self.GetTarget().GetFullSimpleName()}}}: {e}\n  \\ hook will be disposed");
      self.Dispose();
    }
  }

  static void Hook_Apply(Action<Hook> orig, Hook self)
  {
    AddHook(self, self.Method, self.Target);
    orig(self);
  }

  static void Hook_ApplyW(Action<Hook> orig, Hook self)
  {
    AddHook(self, self.Method, self.Target);
    WrappedApply(orig, self);
  }

  static void ILHook_Apply(Action<ILHook> orig, ILHook self)
  {
    AddHook(self, self.Method, self.Manipulator.Method);
    orig(self);
  }

  static void ILHook_ApplyW(Action<ILHook> orig, ILHook self)
  {
    AddHook(self, self.Method, self.Manipulator.Method);
    WrappedApply(orig, self);
  }

  static void Detour_Apply(Action<Detour> orig, Detour self)
  {
    AddHook(self, self.Method, self.Target);
    orig(self);
  }

  static void Detour_ApplyW(Action<Detour> orig, Detour self)
  {
    AddHook(self, self.Method, self.Target);
    // internal detours will rethrow to be processed by actual hook
    if (self.Target.DeclaringType == null)
      orig(self);
    else
      WrappedApply(orig, self);
  }

  static void NativeDetour_ApplyW(Action<NativeDetour> orig, NativeDetour self)
  {
    WrappedApply(orig, self);
  }

  public delegate void NativeDetour_ctor_mmrc_d(NativeDetour self, MethodBase from, MethodBase to, ref NativeDetourConfig config);
  static void NativeDetour_ctor_mmrc(NativeDetour_ctor_mmrc_d orig, NativeDetour self, MethodBase from, MethodBase to, ref NativeDetourConfig config)
  {
    AddHook(self, from, to);
    orig(self, from, to, ref config);
  }

  static void NativeDetour_ctor_mmc(Action<NativeDetour, MethodBase, MethodBase, NativeDetourConfig> orig, NativeDetour self, MethodBase from, MethodBase to, NativeDetourConfig config)
  {
    AddHook(self, from, to);
    orig(self, from, to, config);
  }

  static void NativeDetour_ctor_mm(Action<NativeDetour, MethodBase, MethodBase> orig, NativeDetour self, MethodBase from, MethodBase to)
  {
    AddHook(self, from, to);
    orig(self, from, to);
  }

  static void DetourRuntimeILPlatform_Pin(Action<DetourRuntimeILPlatform, MethodBase> orig, DetourRuntimeILPlatform self, MethodBase method)
  {
    orig(self, method);
    // as a side effect anyhow optimizes retrieval of some function pointers(thus reducing instantiation time for all hooks)
    new Task(() => pinnedMethods[(long)method.MethodHandle.GetFunctionPointer()] = method).Start();
  }

  static string Exception_ToString(Func<Exception, string> orig, Exception self) => GetAdditionalExceptionInfo(self) + orig(self);

  static RuntimeDetourManager()
  {
    Hook initialHook;
    if (GSLSettings.instance.wrapHooks)
    {
      initialHook = DetourUtils.newHook<Hook>("Apply", Hook_ApplyW);
      DetourUtils.newHook<ILHook>("Apply", ILHook_ApplyW);
      DetourUtils.newHook<Detour>("Apply", Detour_ApplyW);
      DetourUtils.newHook<NativeDetour>("Apply", NativeDetour_ApplyW);
    }
    else
    {
      initialHook = DetourUtils.newHook<Hook>("Apply", Hook_Apply);
      DetourUtils.newHook<ILHook>("Apply", ILHook_Apply);
      DetourUtils.newHook<Detour>("Apply", Detour_Apply);
    }
    AddHook(initialHook, initialHook.Method, initialHook.Target);
    DetourUtils.newHook<DetourRuntimeILPlatform>("Pin", DetourRuntimeILPlatform_Pin);
    new Hook(typeof(NativeDetour).GetConstructor([typeof(MethodBase), typeof(MethodBase), typeof(NativeDetourConfig).MakeByRefType()]), NativeDetour_ctor_mmrc);
    new Hook(typeof(NativeDetour).GetConstructor([typeof(MethodBase), typeof(MethodBase), typeof(NativeDetourConfig)]), NativeDetour_ctor_mmc);
    new Hook(typeof(NativeDetour).GetConstructor([typeof(MethodBase), typeof(MethodBase)]), NativeDetour_ctor_mm);

    new Hook(typeof(Exception).GetMethod("ToString"), Exception_ToString);
  }

  /// <summary>
  /// If possible, provides additional information about exception, that includes throwing method(exact detour target if applicable) and offset, that's omitted sometimes
  /// </summary>
  public static string GetAdditionalExceptionInfo(Exception self)
  {
    StringBuilder sb = new();
    StackTrace stackTrace = new(self.InnerException ?? self);
    if (stackTrace.FrameCount == 0)
      return "";
    sb.Append("<thrown by ");
    StackFrame frame = stackTrace.GetFrame(0);
    if (frame.GetMethod() is MethodBase method)
      sb.Append(method.GetFullSimpleName());
    else if (pinnedMethods.TryGetValue((long)StackFrame_methodAddress.GetValue(frame), out MethodBase pinned))
    {
      if (DMDOwners.TryGetValue(pinned, out IDetour idetour))
        sb.Append(idetour.GetTarget()?.GetFullSimpleName() ?? "(???)");
      else
      {
        string def = pinned.ToString();
        int nameStart = def.IndexOf('<'), nameEnd = def.LastIndexOf('>');
        if (nameStart != -1 && nameStart < nameEnd && nameEnd < def.Length)
          sb.Append(def.Substring(nameStart + 1, nameEnd - nameStart - 1));
        else
          sb.Append("(orig described by stack frame)");
      }
    }
    else
      sb.Append("(unknown method)");
    sb.Append(" after ");
    int offset = frame.GetILOffset();
    if (offset < 0)
      sb.Append("(unknown offset)");
    else
      sb.Append("IL_").Append(offset.ToString("X4"));
    return sb.Append("> ").ToString();
  }

  static void AddDefinedPatches(List<MethodInfo> targets, Assembly asm, Patch[] patches)
  {
    foreach (Patch patch in patches)
      if (patch.PatchMethod is MethodInfo patchMethod && patchMethod.DeclaringType?.Assembly == asm)
        targets.Add(patchMethod);
  }

  /// <summary>
  /// Returns harmony patches map with all patches defined by assembly
  /// </summary>
  public static Dictionary<MethodBase, List<MethodInfo>> GetHarmonyPatches(Assembly asm)
  {
    Dictionary<MethodBase, List<MethodInfo>> patches = [];
    foreach (IDetour detour in hookLists[typeof(Harmony).Assembly])
      if (detour.GetMethod() is MethodBase method && method.GetPatchInfo() is PatchInfo patchInfo)
      {
        List<MethodInfo> targets = [];
        AddDefinedPatches(targets, asm, patchInfo.prefixes);
        AddDefinedPatches(targets, asm, patchInfo.postfixes);
        AddDefinedPatches(targets, asm, patchInfo.transpilers);
        AddDefinedPatches(targets, asm, patchInfo.finalizers);
        AddDefinedPatches(targets, asm, patchInfo.ilmanipulators);
        if (targets.Count != 0)
          patches[method] = targets;
      }
    return patches;
  }

  /// <summary>
  /// Returns native detour target if any are applied to the method. Otherwise returns method itself (hooks override native detours otherwise)
  /// </summary>
  public static MethodBase GetInvocationTarget(this MethodBase self)
  {
    if (nativeDetourMaps.TryGetValue(self, out List<NativeDetour> detours))
      return nativeDetourTargets[detours[detours.Count - 1]];
    return self;
  }

  /// <summary>
  /// Returns label with information about detour type and state
  /// </summary>
  public static string GetDetourLabel(IDetour detour)
  {
    StringBuilder sb = new(128);
    sb.Append("  (").Append(detour.GetType().Name);
    if (!detour.IsValid)
      sb.Append("/disposed");
    else if (!detour.IsApplied)
      sb.Append("/inactive");
    sb.Append(") ").Append(detour.GetTarget()?.GetFullSimpleName());

    return sb.ToString();
  }

  /// <summary>
  /// Logs harmony patches
  /// </summary>
  public static void LogPatches(Patch[] patches, string patchType)
  {
    if (patches.Length == 0)
      return;
    foreach (Patch patch in patches)
      LogInfo($"  ({patchType}) {patch.PatchMethod.GetFullSimpleName()}");
  }

  /// <summary>
  /// Logs all hooks by defining assembly, logs all harmony patches
  /// </summary>
  public static void LogAllHookLists()
  {
    LogInfo($" * Logging all caught hooks by assembly:");
    foreach (KeyValuePair<Assembly, List<IDetour>> hookListKVP in hookLists)
    {
      LogInfo($"{hookListKVP.Key}");
      if (hookListKVP.Key == typeof(Harmony).Assembly)
      {
        LogInfo("<Methods in this assembly are Harmony patches, defined by other assemblies>");
        foreach (KeyValuePair<MethodBase, PatchInfo> patchKVP in
          (Dictionary<MethodBase, PatchInfo>)typeof(PatchManager).GetField("PatchInfos", BFlags.anyDeclaredStatic).GetValue(null))
        {
          LogPatches(patchKVP.Value.prefixes, "Prefix");
          LogPatches(patchKVP.Value.postfixes, "Postfix");
          LogPatches(patchKVP.Value.transpilers, "Transpiler");
          LogPatches(patchKVP.Value.ilmanipulators, "ILManipulator");
          LogPatches(patchKVP.Value.finalizers, "Finalizer");
        }
      }
      else
        foreach (IDetour idetour in hookListKVP.Value)
          LogInfo(GetDetourLabel(idetour));
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
        if (idetour is not ILHook ilhook || ilhook.GetTarget() != HarmonyLibManipulator)
          LogInfo(GetDetourLabel(idetour));
      if (hookMapKVP.Key.GetPatchInfo() is not PatchInfo patchInfo)
        continue;
      LogPatches(patchInfo.prefixes, "Prefix");
      LogPatches(patchInfo.postfixes, "Postfix");
      LogPatches(patchInfo.transpilers, "Transpiler");
      LogPatches(patchInfo.ilmanipulators, "ILManipulator");
      LogPatches(patchInfo.finalizers, "Finalizer");
    }
    LogInfo($" * Finished logging");
  }
}

// Method - method, that's being hooked
// Target/Manipulator.Method - method, that's being hooked with
// TargetReal - compiled method