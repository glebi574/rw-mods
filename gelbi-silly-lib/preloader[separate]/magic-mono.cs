using gelbi_silly_lib.Other;
using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using static gelbi_silly_lib.LogWrapper;
using static MonoMod.Cil.ILContext;

namespace gelbi_silly_lib.MonoModUtils;

public static class MonoModUtils
{
  public static readonly Dictionary<MetadataType, string> baseTypeNameAtlas = new()
  {
    { MetadataType.Void, "void" },
    { MetadataType.Object, "object" },
    { MetadataType.String, "string" },
    { MetadataType.Boolean, "bool" },
    { MetadataType.Char, "char" },
    { MetadataType.Byte, "byte" },
    { MetadataType.SByte, "sbyte" },
    { MetadataType.Int16, "short" },
    { MetadataType.UInt16, "ushort" },
    { MetadataType.Int32, "int" },
    { MetadataType.UInt32, "uint" },
    { MetadataType.Int64, "long" },
    { MetadataType.UInt64, "ulong" },
    { MetadataType.Single, "float" },
    { MetadataType.Double, "double" }
  };
}

/// <summary>
/// Extensions for different Mono methods
/// </summary>
public static class Extensions
{
  /// <summary>
  /// Recoursively invokes expression for this type and each type nested in it
  /// </summary>
  public static void ForEach(this TypeDefinition self, Action<TypeDefinition> action)
  {
    foreach (TypeDefinition type in self.NestedTypes)
      type.ForEach(action);
    action(self);
  }

  /// <summary>
  /// Invokes expression for each type in the module and each type nested in it
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
    List<TypeDefinition> types = [];
    self.ForEachType(types.Add);
    return types;
  }

  /// <summary>
  /// Returns detour method(won't work for pointer based native detours)
  /// </summary>
  public static MethodBase GetMethod(this IDetour self)
  {
    return self switch
    {
      Detour detour => detour.Method,
      Hook hook => hook.Method,
      ILHook ilhook => ilhook.Method,
      NativeDetour native => RuntimeDetourManager.nativeDetourMethods.TryGetValue(native, out MethodBase method) ? method : null,
      _ => null,
    };
  }

  /// <summary>
  /// Returns target method(won't work for detours, targeting IntPtr)
  /// </summary>
  public static MethodBase GetTarget(this IDetour self)
  {
    return self switch
    {
      Detour detour => detour.Target,
      Hook hook => hook.Target,
      ILHook ilhook => ilhook.Manipulator.Method,
      NativeDetour native => RuntimeDetourManager.nativeDetourTargets.TryGetValue(native, out MethodBase target) ? target : null,
      _ => null,
    };
  }

  /// <summary>
  /// Logs il body of the method
  /// </summary>
  public static void LogBody(this MethodDefinition self)
  {
    foreach (Instruction i in self.Body.Instructions)
      LogInfo($"{i.Offset:X4}: {i.OpCode} {i.Operand}");
  }

  /// <summary>
  /// Returns string, containing formatted list of generic types, presented in collection
  /// </summary>
  public static string GetFormattedGenericTypes<T>(this Mono.Collections.Generic.Collection<T> self) where T : TypeReference
  {
    if (self.Count == 0)
      return "";
    StringBuilder sb = new(self[0].GetSimpleName());
    for (int i = 1; i < self.Count; ++i)
    {
      sb.Append(", ");
      sb.Append(self[i].GetSimpleName());
    }
    return sb.ToString();
  }

  /// <summary>
  /// Returns string, containing formatted list of parameter types, presented in collection
  /// </summary>
  public static string GetFormattedParameterTypes(this Mono.Collections.Generic.Collection<ParameterDefinition> self)
  {
    if (self.Count == 0)
      return "";
    StringBuilder sb = new(self[0].ParameterType.GetSimpleName());
    for (int i = 1; i < self.Count; ++i)
    {
      sb.Append(", ");
      sb.Append(self[i].ParameterType.GetSimpleName());
    }
    return sb.ToString();
  }

  /// <summary>
  /// Returns type definition similar to how it'd be initially written in c# (doesn't differentiate ref/out)
  /// </summary>
  public static string GetSimpleName(this TypeReference type)
  {
    MetadataType metadataType = type.MetadataType;
    if (MonoModUtils.baseTypeNameAtlas.TryGetValue(metadataType, out string name))
      return name;
    if (type is ArrayType arrayType)
      return $"{arrayType.GetElementType().GetSimpleName()}[{new string(',', arrayType.Rank - 1)}]";
    if (type.IsByReference)
      return $"{type.GetElementType().GetSimpleName()}&";
    if (type.IsPointer)
      return $"{type.GetElementType().GetSimpleName()}*";
    if (type.IsGenericParameter)
      return type.Name;

    if (type.HasGenericParameters)
    {
      if (type.Namespace == "System")
        if (type.Name.Length > 8 && type.Name.StartsWith("Nullable"))
          return $"{type.GenericParameters[0].GetSimpleName()}?";
        else if (type.Name.Length > 10 && type.Name.StartsWith("ValueTuple"))
          return $"({type.GenericParameters.GetFormattedGenericTypes()})";
      return $"{type.Name.SubstringUntil('`')}<{type.GenericParameters.GetFormattedGenericTypes()}>";
    }
    else if (type is GenericInstanceType genericInstance)
    {
      if (type.Namespace == "System")
        if (type.Name.Length > 8 && type.Name.StartsWith("Nullable"))
          return $"{genericInstance.GenericArguments[0].GetSimpleName()}?";
        else if (type.Name.Length > 10 && type.Name.StartsWith("ValueTuple"))
          return $"({genericInstance.GenericArguments.GetFormattedGenericTypes()})";
      return $"{type.Name.SubstringUntil('`')}<{genericInstance.GenericArguments.GetFormattedGenericTypes()}>";
    }
    if (type.DeclaringType == null)
      return type.Name;
    return $"{type.DeclaringType.GetSimpleName()}.{type.Name}";
  }

  /// <summary>
  /// Returns method definition similar to how it'd be written in c#
  /// </summary>
  public static string GetSimpleName(this MethodDefinition method)
  {
    StringBuilder sb = new(128);
    sb.Append(method.ReturnType.GetSimpleName()).Append(' ');
    if (method.DeclaringType.Namespace.Length != 0)
      sb.Append(method.DeclaringType.Namespace).Append('.');
    sb.Append(method.DeclaringType.GetSimpleName()).Append('.').Append(method.Name).Append('(').Append(method.Parameters.GetFormattedParameterTypes()).Append(')');
    return sb.ToString();
  }
}

/// <summary>
/// Class with universal signatures for detours and some simplified definitions.
/// <list type="bullet">Generic methods automate reflection.</list>
/// <list type="bullet"><c>RND</c> methods automate native detour redirection</list>
/// </summary>
public static class DetourUtils
{
  public static NativeDetour newNativeDetour(Delegate from, Delegate to) => new(from.Method, to.Method);
  public static NativeDetour newNativeDetour(Delegate from, Delegate to, NativeDetourConfig config) => new(from.Method, to.Method, config);
  public static NativeDetour newNativeDetour(Delegate from, Delegate to, ref NativeDetourConfig config) => new(from.Method, to.Method, ref config);
  public static NativeDetour newNativeDetour(Delegate from, MethodInfo to) => new(from.Method, to);
  public static NativeDetour newNativeDetour(Delegate from, MethodInfo to, NativeDetourConfig config) => new(from.Method, to, config);
  public static NativeDetour newNativeDetour(Delegate from, MethodInfo to, ref NativeDetourConfig config) => new(from.Method, to, ref config);
  public static NativeDetour newNativeDetour(MethodBase from, Delegate to) => new(from, to.Method);
  public static NativeDetour newNativeDetour(MethodBase from, Delegate to, NativeDetourConfig config) => new(from, to.Method, config);
  public static NativeDetour newNativeDetour(MethodBase from, Delegate to, ref NativeDetourConfig config) => new(from, to.Method, ref config);
  public static NativeDetour newNativeDetour(MethodBase from, MethodInfo to) => new(from, to);
  public static NativeDetour newNativeDetour(MethodBase from, MethodInfo to, NativeDetourConfig config) => new(from, to, config);
  public static NativeDetour newNativeDetour(MethodBase from, MethodInfo to, ref NativeDetourConfig config) => new(from, to, ref config);

  public static Detour newDetour(Delegate from, Delegate to) => new(from.Method, to.Method);
  public static Detour newDetour(Delegate from, Delegate to, DetourConfig config) => new(from.Method, to.Method, config);
  public static Detour newDetour(Delegate from, Delegate to, ref DetourConfig config) => new(from.Method, to.Method, ref config);
  public static Detour newDetour(Delegate from, MethodInfo to) => new(from.Method, to);
  public static Detour newDetour(Delegate from, MethodInfo to, DetourConfig config) => new(from.Method, to, config);
  public static Detour newDetour(Delegate from, MethodInfo to, ref DetourConfig config) => new(from.Method, to, ref config);
  public static Detour newDetour(MethodBase from, Delegate to) => new(from, to.Method);
  public static Detour newDetour(MethodBase from, Delegate to, DetourConfig config) => new(from, to.Method, config);
  public static Detour newDetour(MethodBase from, Delegate to, ref DetourConfig config) => new(from, to.Method, ref config);
  public static Detour newDetour(MethodBase from, MethodInfo to) => new(from, to);
  public static Detour newDetour(MethodBase from, MethodInfo to, DetourConfig config) => new(from, to, config);
  public static Detour newDetour(MethodBase from, MethodInfo to, ref DetourConfig config) => new(from, to, ref config);

  public static Hook newHook(Delegate from, Delegate to) => new(from.Method, to.Method, to.Target);
  public static Hook newHook(Delegate from, Delegate to, HookConfig config) => new(from.Method, to.Method, to.Target, config);
  public static Hook newHook(Delegate from, Delegate to, ref HookConfig config) => new(from.Method, to.Method, to.Target, ref config);
  public static Hook newHook(Delegate from, MethodInfo to) => new(from.Method, to, null);
  public static Hook newHook(Delegate from, MethodInfo to, HookConfig config) => new(from.Method, to, null, config);
  public static Hook newHook(Delegate from, MethodInfo to, ref HookConfig config) => new(from.Method, to, null, ref config);
  public static Hook newHook(MethodBase from, Delegate to) => new(from, to.Method, to.Target);
  public static Hook newHook(MethodBase from, Delegate to, HookConfig config) => new(from, to.Method, to.Target, config);
  public static Hook newHook(MethodBase from, Delegate to, ref HookConfig config) => new(from, to.Method, to.Target, ref config);
  public static Hook newHook(MethodBase from, MethodInfo to) => new(from, to, null);
  public static Hook newHook(MethodBase from, MethodInfo to, HookConfig config) => new(from, to, null, config);
  public static Hook newHook(MethodBase from, MethodInfo to, ref HookConfig config) => new(from, to, null, ref config);

  public static ILHook newILHook(Delegate from, Manipulator manipulator) => new(from.Method, manipulator);
  public static ILHook newILHook(Delegate from, Manipulator manipulator, ILHookConfig config) => new(from.Method, manipulator, config);
  public static ILHook newILHook(Delegate from, Manipulator manipulator, ref ILHookConfig config) => new(from.Method, manipulator, ref config);
  public static ILHook newILHook(MethodBase from, Manipulator manipulator) => new(from, manipulator);
  public static ILHook newILHook(MethodBase from, Manipulator manipulator, ILHookConfig config) => new(from, manipulator, config);
  public static ILHook newILHook(MethodBase from, Manipulator manipulator, ref ILHookConfig config) => new(from, manipulator, ref config);

  public static NativeDetour newNativeDetour<T>(string methodName, Delegate to) => new(typeof(T).GetMethod(methodName, BFlags.anyDeclared), to.Method);
  public static NativeDetour newNativeDetour<T>(string methodName, MethodInfo to) => new(typeof(T).GetMethod(methodName, BFlags.anyDeclared), to);
  public static NativeDetour newNativeDetour(Type type, string methodName, Delegate to) => new(type.GetMethod(methodName, BFlags.anyDeclared), to.Method);
  public static NativeDetour newNativeDetour(Type type, string methodName, MethodInfo to) => new(type.GetMethod(methodName, BFlags.anyDeclared), to);

  public static Detour newDetour<T>(string methodName, Delegate to) => new(typeof(T).GetMethod(methodName, BFlags.anyDeclared), to.Method);
  public static Detour newDetour<T>(string methodName, MethodInfo to) => new(typeof(T).GetMethod(methodName, BFlags.anyDeclared), to);
  public static Detour newDetour(Type type, string methodName, Delegate to) => new(type.GetMethod(methodName, BFlags.anyDeclared), to.Method);
  public static Detour newDetour(Type type, string methodName, MethodInfo to) => new(type.GetMethod(methodName, BFlags.anyDeclared), to);

  public static Hook newHook<T>(string methodName, Delegate to) => new(typeof(T).GetMethod(methodName, BFlags.anyDeclared), to.Method);
  public static Hook newHook<T>(string methodName, MethodInfo to) => new(typeof(T).GetMethod(methodName, BFlags.anyDeclared), to);
  public static Hook newHook(Type type, string methodName, Delegate to) => new(type.GetMethod(methodName, BFlags.anyDeclared), to.Method);
  public static Hook newHook(Type type, string methodName, MethodInfo to) => new(type.GetMethod(methodName, BFlags.anyDeclared), to);

  public static ILHook newILHook<T>(string methodName, Manipulator manipulator) => new(typeof(T).GetMethod(methodName, BFlags.anyDeclared), manipulator);
  public static ILHook newILHook(Type type, string methodName, Manipulator manipulator) => new(type.GetMethod(methodName, BFlags.anyDeclared), manipulator);

  public static Hook newHookRND(Delegate from, Delegate to) => new(from.Method.GetInvocationTarget(), to.Method, to.Target);
  public static Hook newHookRND(Delegate from, Delegate to, HookConfig config) => new(from.Method.GetInvocationTarget(), to.Method, to.Target, config);
  public static Hook newHookRND(Delegate from, Delegate to, ref HookConfig config) => new(from.Method.GetInvocationTarget(), to.Method, to.Target, ref config);
  public static Hook newHookRND(Delegate from, MethodInfo to) => new(from.Method.GetInvocationTarget(), to, null);
  public static Hook newHookRND(Delegate from, MethodInfo to, HookConfig config) => new(from.Method.GetInvocationTarget(), to, null, config);
  public static Hook newHookRND(Delegate from, MethodInfo to, ref HookConfig config) => new(from.Method.GetInvocationTarget(), to, null, ref config);
  public static Hook newHookRND(MethodBase from, Delegate to) => new(from.GetInvocationTarget(), to.Method, to.Target);
  public static Hook newHookRND(MethodBase from, Delegate to, HookConfig config) => new(from.GetInvocationTarget(), to.Method, to.Target, config);
  public static Hook newHookRND(MethodBase from, Delegate to, ref HookConfig config) => new(from.GetInvocationTarget(), to.Method, to.Target, ref config);
  public static Hook newHookRND(MethodBase from, MethodInfo to) => new(from.GetInvocationTarget(), to, null);
  public static Hook newHookRND(MethodBase from, MethodInfo to, HookConfig config) => new(from.GetInvocationTarget(), to, null, config);
  public static Hook newHookRND(MethodBase from, MethodInfo to, ref HookConfig config) => new(from.GetInvocationTarget(), to, null, ref config);
}