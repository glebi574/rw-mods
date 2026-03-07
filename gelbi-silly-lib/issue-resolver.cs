using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

// will relocate to preloader after 1.1.2 ~hotfix release
public static class IssueResolver
{
  public enum ResolverState
  {
    Observe,
    Resolve,
    Finalize,
  }

  public static ResolverState state = ResolverState.Observe;
  public static int issueCounter = 0;
  public static Exception latestException;
  public static List<ILHook> appliedHooks = [];
  public static List<ResolvedTraceMethod> targetResolvedMethods;
  public static int targetMethodIndex = 0;
  public static int[] finalInstructions;

  internal static void ApplyHooks()
  {
    IL.RainWorld.HandleLog += RainWorld_HandleLog;
    On.RainWorld.Update += RainWorld_Update;
  }

  public static string ExceptionToStringAlt(Exception self)
  {
    StringBuilder sb = new(512);
    sb.Append("<simplified exception> ").AppendLine(self.Message.TrimEnd());

    void ProcessException(Exception e)
    {
      List<(string prefix, string method)> lines = [];
      int maxPrefix = 2;

      void AddLine(string prefix, string method)
      {
        lines.Add((prefix, method));
        if (maxPrefix < prefix.Length)
          maxPrefix = prefix.Length;
      }

      foreach (ResolvedTraceMethod resolvedMethod in e.GetResolvedMethodTrace())
      {
        string offset = " at IL_" + resolvedMethod.offset.ToString("X4");
        if (resolvedMethod.flags == 0)
          AddLine("", resolvedMethod.method.GetSimpleName() + offset + resolvedMethod.codeLocation);
        else if (resolvedMethod.IsNative)
          AddLine("<native> ", resolvedMethod.additionalData + resolvedMethod.codeLocation);
        else
        {
          string label = "", name = resolvedMethod.method.GetSimpleName();
          if (resolvedMethod.IsInlinedHook)
            label = "inlined hook";
          else
          {
            name += offset;
            if (resolvedMethod.IsHook)
              label = "hook";
            else if (resolvedMethod.IsDynamicOrig)
              label = "orig";
          }
          if (resolvedMethod.IsILModified)
            if (label.Length == 0)
              label = "IL*";
            else
              label += "\\IL*";
          AddLine($"<{label}> ", name + resolvedMethod.codeLocation);
        }
      }

      foreach ((string prefix, string method) in lines)
        sb.Append(prefix.PadLeft(maxPrefix)).AppendLine(method);
    }

    if (self.InnerException is Exception inner)
    {
      ProcessException(inner);
      sb.AppendLine("--- End of stack trace from previous location where exception was thrown ---");
    }
    ProcessException(self);

    return sb.ToString();
  }

  static void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
  {
    try
    {
      orig(self);
      issueCounter = 0;
    }
    catch (Exception e)
    {
      ++issueCounter;
      latestException = e;
      throw;
    }
    finally
    {
      Update();
    }
  }

  static void RainWorld_HandleLog(ILContext il)
  {
    ILCursor c = new(il);

    if (c.TryGotoNext(i => i.OpCode == OpCodes.Ldstr))
      c.EmitDelegate(() =>
      {
        if (latestException == null)
          return;
        File.AppendAllText("exceptionLog.txt", RuntimeDetourManager.GetAdditionalExceptionInfo(latestException));
        GSLLog.GLog($"{GSLLog.TimeLabel()} [unhandled exception] {ExceptionToStringAlt(latestException)}\n{latestException}\n");
      });
  }

  static void Update()
  {
    switch (state)
    {
      case ResolverState.Observe:
        if (issueCounter != 12)
          return;
        state = ResolverState.Resolve;
        LogError("Game stayed frozen due to exception being thrown for more than 12 frames in a row. State of the stack trace will be logged in 1 frame.");
        // assuming inner exceptions can't be thrown in such conditions
        targetMethodIndex = 0;
        targetResolvedMethods = latestException.GetResolvedMethodTrace();
        for (int i = targetResolvedMethods.Count - 1; i >= 0; --i)
          if (targetResolvedMethods[i].method == null || targetResolvedMethods.IndexOf(targetResolvedMethods[i]) != i)
            targetResolvedMethods.RemoveAt(i);
        if (targetResolvedMethods[targetResolvedMethods.Count - 1].method.DeclaringType == typeof(IssueResolver))
          targetResolvedMethods.RemoveAt(targetResolvedMethods.Count - 1);
        finalInstructions = new int[targetResolvedMethods.Count];

        foreach (ResolvedTraceMethod resolvedMethod in targetResolvedMethods)
        {
          appliedHooks.Add(DetourUtils.newILHook(resolvedMethod.method, MethodBodyIterator));
          ++targetMethodIndex;
        }
        break;
      case ResolverState.Resolve:
        state = ResolverState.Finalize;
        targetMethodIndex = 0;
        foreach (ILHook ilhook in appliedHooks)
          ilhook.Dispose();
        appliedHooks.Clear();

        foreach (ResolvedTraceMethod resolvedMethod in targetResolvedMethods)
        {
          appliedHooks.Add(DetourUtils.newILHook(resolvedMethod.method, StateLogger));
          ++targetMethodIndex;
        }
        break;
      case ResolverState.Finalize:
        state = ResolverState.Observe;
        foreach (ILHook ilhook in appliedHooks)
          ilhook.Dispose();
        appliedHooks.Clear();
        break;
    }
    return;
  }

  public static FieldInfo f_finalInstructions = typeof(IssueResolver).GetField("finalInstructions");
  static void MethodBodyIterator(ILContext il)
  {
    ILProcessor ilp = il.IL;
    FieldReference fieldRef = il.Module.ImportReference(f_finalInstructions);
    int index = 0;
    // doesn't process branches, but they wouldn't matter
    foreach (Instruction i in il.Instrs.ToArray())
    {
      ilp.InsertBefore(i, ilp.Create(OpCodes.Ldsfld, fieldRef));
      ilp.InsertBefore(i, ilp.Create(OpCodes.Ldc_I4, targetMethodIndex));
      ilp.InsertBefore(i, ilp.Create(OpCodes.Ldc_I4, index++));
      ilp.InsertBefore(i, ilp.Create(OpCodes.Stelem_I4));
    }
  }

  static void LogValue(string name, object obj)
  {
    if (obj == null)
    {
      LogInfo($"  {name}: null");
      return;
    }
    StringBuilder sb = new(64);
    void AppendArray(Array array)
    {
      Type type = array.GetType();
      sb.Append('[');
      if (array.Length > 32 || array.Length == 0)
        sb.Append(array.Length);
      else if (type.GetElementType().IsPrimitive || type.GetElementType().IsEnum || type == typeof(string[]))
      {
        foreach (object value in array)
          sb.Append(value).Append(", ");
        sb.Length -= 2;
      }
      sb.Append(']');
    }
    Type objectType = obj.GetType();
    sb.Append("  ").Append(objectType.GetSimpleNameWithNamespace()).Append(" ").Append(name).Append(": ").Append(obj);
    if (objectType.IsArray)
    {
      sb.Append(" : ");
      AppendArray(obj as Array);
    }
    else if (!objectType.IsValueType || (!objectType.IsPrimitive && !objectType.IsEnum))
    {
      sb.AppendLine(" : {");
      foreach (FieldInfo field in objectType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        if (field.FieldType.IsArray)
        {
          sb.Append("  ").Append(field.Name).Append(": ");
          if (field.GetValue(obj) is Array array)
            AppendArray(array);
          else
            sb.Append("null");
          sb.AppendLine();
        }
        else if (field.FieldType.IsPrimitive || field.FieldType.IsEnum)
          sb.Append("  ").Append(field.Name).Append(": ").AppendLine(field.GetValue(obj)?.ToString() ?? "null");
      sb.Append('}');
    }
    LogInfo(sb.ToString());
  }

  static void StateLogger(ILContext il)
  {
    ILCursor c = new(il);
    void EmitLog(string str) => c.Emit(OpCodes.Ldstr, str).EmitDelegate(static (string str) => LogInfo(str));

    c.Index += finalInstructions[targetMethodIndex];
    string methodName = targetResolvedMethods[targetMethodIndex].method.GetFullSimpleName();
    if ((targetResolvedMethods[targetMethodIndex].method.MethodImplementationFlags & System.Reflection.MethodImplAttributes.AggressiveInlining) != 0)
      LogWarning($"{methodName} may be inlined away");
    if (il.Method.Parameters.Count == 0)
      EmitLog($" ([{methodName}] doesn't have any arguments)");
    else
    {
      EmitLog($"> [{methodName}] Arguments:");
      foreach (ParameterDefinition parameter in il.Method.Parameters)
      {
        c.Emit(OpCodes.Ldstr, parameter.Name);
        c.EmitBoxedParameter(parameter);
        c.EmitDelegate(LogValue);
      }
    }
    if (il.Body.Variables.Count == 0)
      EmitLog($" ([{methodName}] doesn't have any locals)");
    else
    {
      EmitLog($"> [{methodName}] Locals:");
      foreach (VariableDefinition variable in il.Body.Variables)
      {
        c.Emit(OpCodes.Ldstr, "V_" + variable.Index);
        c.EmitBoxedVariable(variable);
        c.EmitDelegate(LogValue);
      }
    }
  }
}