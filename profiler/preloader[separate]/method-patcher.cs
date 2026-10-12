using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Diagnostics;

namespace profiler;

/// <summary>
/// Allows to hook methods at a runtime in order to profile them
/// </summary>
public static class MethodPatcher
{
  public static System.Reflection.MethodInfo Stopwatch_GetTimestamp = ((Delegate)Stopwatch.GetTimestamp).Method, Profiler_End = ((Delegate)Profiler.End).Method;

  /// <summary>
  /// Patches all methods in the type with profiler patch
  /// </summary>
  public static void PatchType(Type type)
  {
    foreach (System.Reflection.MethodInfo method in type.GetMethods(BFlags.anyDeclared))
      PatchMethod(method);
    foreach (System.Reflection.ConstructorInfo ctor in type.GetConstructors(BFlags.anyDeclaredInstance))
      PatchMethod(ctor);
  }

  /// <summary>
  /// Patches method with profiler patch
  /// </summary>
  public static void PatchMethod(System.Reflection.MethodBase methodBase)
  {
    try
    {
      new ILHook(methodBase, (il) =>
      {
        ILCursor c = new(il);
        MethodBody body = il.Body;
        int id = Profiler.GetID(methodBase);

        VariableDefinition localTime = new(il.Module.ImportReference(typeof(long)));
        body.Variables.Add(localTime);
        body.InitLocals = true;

        c.Emit(OpCodes.Call, Stopwatch_GetTimestamp)
         .Emit(OpCodes.Stloc, localTime);

        foreach (Instruction i in (Instruction[])[.. body.Instructions])
          if (i.OpCode == OpCodes.Ret || i.OpCode == OpCodes.Throw)
            c.Goto(i)
             .MoveAfterLabels()
             .Emit(OpCodes.Ldc_I4, id)
             .Emit(OpCodes.Ldloc, localTime)
             .Emit(OpCodes.Call, Stopwatch_GetTimestamp)
             .Emit(OpCodes.Call, Profiler_End);
      });
      Profiler.methodStats.Add(new(methodBase));
      Profiler.LogT($"Patched {methodBase.GetFullSimpleName()}");
    }
    catch (Exception e)
    {
      Profiler.LogT($"Failed to patch {methodBase.GetFullSimpleName()}: {e}");
    }
  }

  /// <summary>
  /// Allows to specify methods to profile if neither of global profiling options are enabled
  /// </summary>
  public static void PatchMethods(params System.Reflection.MethodBase[] methods) => PatchMethodsInternal(methods);

  /// <summary>
  /// Allows to specify methods to profile if neither of global profiling options are enabled
  /// </summary>
  public static void PatchMethods(params Delegate[] methods) => PatchMethodsInternal(methods);

  static void PatchMethodsInternal(params object[] methods)
  {
    if (Patcher.settings.profileGlobal || Patcher.settings.profileMods)
    {
      Profiler.LogT($"Skipping additional patches");
      return;
    }
    if (methods is Delegate[] delegates)
      foreach (Delegate method in delegates)
        PatchMethod(method.Method);
    else if (methods is System.Reflection.MethodBase[] methodBases)
      foreach (System.Reflection.MethodBase methodBase in methodBases)
        PatchMethod(methodBase);
    Profiler.LogT($"Finished additional patches");
  }
}