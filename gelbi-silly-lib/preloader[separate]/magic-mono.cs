using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib.MonoModUtils;

/// <summary>
/// Extensions for different mono related methods
/// </summary>
public static class Extensions
{
  /// <summary>
  /// Executes provided action with type and each type nested in it
  /// </summary>
  public static void ForEach(this TypeDefinition self, Action<TypeDefinition> action)
  {
    foreach (TypeDefinition type in self.NestedTypes)
      type.ForEach(action);
    action(self);
  }

  /// <summary>
  /// Executes provided action with each type in module, including nested types
  /// </summary>
  public static void ForEachType(this ModuleDefinition self, Action<TypeDefinition> action)
  {
    foreach (TypeDefinition type in self.Types)
      type.ForEach(action);
  }

  /// <summary>
  /// Returns list with all types and nested types, defined in module
  /// </summary>
  public static List<TypeDefinition> GetAllTypes(this ModuleDefinition self)
  {
    List<TypeDefinition> types = new();
    self.ForEachType(type => types.Add(type));
    return types;
  }

  /// <summary>
  /// Returns target method
  /// </summary>
  public static MethodBase GetTarget(this IDetour self)
  {
    if (self is Detour detour)
      return detour.Target;
    if (self is Hook hook)
      return hook.Target;
    if (self is ILHook ilhook)
      return ilhook.Manipulator.Method;
    if (self is NativeDetour native && RuntimeDetourManager.nativeDetourTargets.TryGetValue(native, out MethodBase target))
      return target;
    return null;
  }

  /// <summary>
  /// Returns target's assembly. Will return `null` for internal detours.
  /// </summary>
  public static Assembly GetAssembly(this IDetour self)
  {
    return self.GetTarget()?.DeclaringType?.Assembly;
  }

  /// <summary>
  /// Returns target method definition similar to how it'd be written in c#
  /// </summary>
  public static string GetSimpleTargetName(this IDetour self)
  {
    return self.GetTarget()?.GetSimpleName();
  }

  /// <summary>
  /// Logs body of IL hook
  /// </summary>
  public static void LogBody(this ILContext il)
  {
    LogInfo($" * Logging IL body of {il.Method}");
    foreach (Instruction i in il.Body.Instructions)
      LogInfo($"{i.Offset:X4}: {i.OpCode} {i.Operand}");
  }

  /// <summary>
  /// Returns string with logged il body of the method
  /// </summary>
  public static void LogBody(this MethodDefinition self)
  {
    foreach (Instruction i in self.Body.Instructions)
      LogInfo($"{i.Offset:X4}: {i.OpCode} {i.Operand}");
  }
}