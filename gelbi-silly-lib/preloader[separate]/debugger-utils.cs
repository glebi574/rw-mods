using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib.Debugging;

/// <summary>
/// Method representing underlying implementation(with dynamic methods resolved to base ones) for one or multiple frames of stack trace.
/// </summary>
public class ResolvedTraceMethod(MethodBase method, int offset, ResolvedTraceMethod.Flags flags, string codeLocation, string additionalData = "")
{
  public enum Flags : ushort
  {
    /// <summary>
    /// General method invocation
    /// </summary>
    Method = 0,
    /// <summary>
    /// Invocation of hook target
    /// </summary>
    Hook = 1,
    /// <summary>
    /// Invocation of inlined hook target
    /// </summary>
    InlinedHook = 2,
    /// <summary>
    /// IL of invoked method is modified via IL hook
    /// </summary>
    ILModified = 4,
    /// <summary>
    /// Invocation of dynamically generated orig based on method
    /// </summary>
    DynamicOrig = 8,
    /// <summary>
    /// Invocation of native method
    /// </summary>
    Native = 16,
  }

  public readonly MethodBase method = method;
  /// <summary>
  /// Approximate offset after which invokation or issue has occurred. Relatively correct for anything that has flags and doesn't mean anything for native methods or inlined hooks.
  /// </summary>
  public int offset = offset;
  /// <summary>
  /// Path, line number and column, if possible to retrieve.
  /// </summary>
  public string codeLocation = codeLocation;
  /// <summary>
  /// Internal name of native method if frame is resolved to it.
  /// </summary>
  public string additionalData = additionalData;
  public Flags flags = flags;

  public bool IsHook => (flags & Flags.Hook) != 0;
  public bool IsInlinedHook => (flags & Flags.InlinedHook) != 0;
  public bool IsILModified => (flags & Flags.ILModified) != 0;
  public bool IsDynamicOrig => (flags & Flags.DynamicOrig) != 0;
  public bool IsNative => (flags & Flags.Native) != 0;
}

public class TrackedIssue(List<ResolvedTraceMethod> resolvedTrace)
{
  public enum DebuggingState
  {
    Wait,
    Capture,
    Finalize,
  }

  public static Dictionary<int, TrackedIssue> indexedIssues = [];
  public static Dictionary<MethodBase, TrackedIssue> issues = [];
  public static HashSet<MethodBase> hookedMethods = [], resolvedIssues = [];
  public static int issueCounter = 0;

  public DebuggingState state = DebuggingState.Wait;
  public int dismissCounter = 0, failureCounter = 0, targetMethodIndex = 0, id = issueCounter;
  public List<ResolvedTraceMethod> stack = resolvedTrace;
  public List<ILHook> appliedHooks = [];
  public bool allowedToResolve = false, capturedFinalIL = false, invokeFinalTarget = false;
  public int[] finalInstructions;
  public Dictionary<string, string>[] capturedArguments, capturedLocals;

  static void SetFinalInstruction(int issueIndex, int methodIndex, int instructionIndex) => indexedIssues[issueIndex].finalInstructions[methodIndex] = instructionIndex;

  static void SetCapturedFlag(int issueIndex) => indexedIssues[issueIndex].capturedFinalIL = true;
  static void SetInvokedFlag(int issueIndex) => indexedIssues[issueIndex].invokeFinalTarget = true;

  public static MethodInfo m_SetFinalInstruction = typeof(TrackedIssue).GetMethod(nameof(SetFinalInstruction), BFlags.anyDeclaredStatic),
    m_SetCapturedFlag = typeof(TrackedIssue).GetMethod(nameof(SetCapturedFlag), BFlags.anyDeclaredStatic),
    m_SetInvokedFlag = typeof(TrackedIssue).GetMethod(nameof(SetInvokedFlag), BFlags.anyDeclaredStatic);

  void MethodBodyIterator(ILContext il)
  {
    ILProcessor ilp = il.IL;
    MethodReference methodRef = il.Module.ImportReference(m_SetFinalInstruction);
    int index = 0;
    // doesn't process branches, but they wouldn't matter
    foreach (Instruction i in il.Instrs.ToArray())
    {
      ilp.InsertBefore(i, ilp.Create(OpCodes.Ldc_I4, id));
      ilp.InsertBefore(i, ilp.Create(OpCodes.Ldc_I4, targetMethodIndex));
      ilp.InsertBefore(i, ilp.Create(OpCodes.Ldc_I4, index++));
      ilp.InsertBefore(i, ilp.Create(OpCodes.Call, methodRef));
    }
    if (targetMethodIndex == 0)
    {
      Instruction i = il.Instrs[0];
      ilp.InsertBefore(i, ilp.Create(OpCodes.Ldc_I4, id));
      ilp.InsertBefore(i, ilp.Create(OpCodes.Call, il.Module.ImportReference(m_SetCapturedFlag)));
    }
  }

  /// <summary>
  /// Returns string, describing arbutrary object
  /// </summary>
  public static string ParseState(string name, object obj)
  {
    if (obj == null)
      return $"  {name}: null";
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
    else if ((!objectType.IsValueType || !objectType.IsPrimitive && !objectType.IsEnum) && obj is not Delegate)
    {
      sb.AppendLine(" : {");
      foreach (FieldInfo field in objectType.GetFields(BFlags.anyInstance))
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
        else if (field.GetValue(obj) == null)
          sb.Append("  ").Append(field.Name).Append(": ").AppendLine("null");
      sb.Append('}');
    }
    return sb.ToString();
  }

  void ResetCapturedArguments(int index) => capturedArguments[index].Clear();
  void ResetCapturedLocals(int index) => capturedLocals[index].Clear();
  void CaptureArguments(int index, string name, object obj) => capturedArguments[index][name] = ParseState(name, obj);
  void CaptureLocals(int index, string name, object obj) => capturedLocals[index][name] = ParseState(name, obj);

  void StateLogger(ILContext il)
  {
    ILCursor c = new(il);

    c.Index += finalInstructions[targetMethodIndex];
    string methodName = stack[targetMethodIndex].method.GetFullSimpleName();
    LogInfo($"{c.Next.GetSimpleLabel()} | {methodName}");
    if (targetMethodIndex == 0)
    {
      c.Emit(OpCodes.Ldc_I4, id);
      c.Emit(OpCodes.Call, m_SetInvokedFlag);
    }
    if ((stack[targetMethodIndex].method.MethodImplementationFlags & System.Reflection.MethodImplAttributes.AggressiveInlining) != 0)
      LogWarning($"{methodName} may be inlined away");

    if (il.Method.Parameters.Count != 0)
    {
      c.Emit(OpCodes.Ldc_I4, targetMethodIndex);
      c.EmitDelegate(ResetCapturedArguments);
      foreach (ParameterDefinition parameter in il.Method.Parameters)
      {
        c.Emit(OpCodes.Ldc_I4, targetMethodIndex);
        c.Emit(OpCodes.Ldstr, parameter.Name);
        c.EmitBoxedParameter(parameter);
        c.EmitDelegate(CaptureArguments);
      }
    }
    if (il.Body.Variables.Count != 0)
    {
      c.Emit(OpCodes.Ldc_I4, targetMethodIndex);
      c.EmitDelegate(ResetCapturedLocals);
      foreach (VariableDefinition variable in il.Body.Variables)
      {
        c.Emit(OpCodes.Ldc_I4, targetMethodIndex);
        c.Emit(OpCodes.Ldstr, "V_" + variable.Index);
        c.EmitBoxedVariable(variable);
        c.EmitDelegate(CaptureLocals);
      }
    }
  }

  void DisposeHooks()
  {
    foreach (ILHook ilhook in appliedHooks)
      ilhook.Dispose();
    appliedHooks.Clear();
  }

  void Update()
  {
    switch (state)
    {
      case DebuggingState.Wait:
        allowedToResolve = true;
        foreach (ResolvedTraceMethod method in stack)
          if (hookedMethods.Contains(method.method))
            return;
        allowedToResolve = false;
        dismissCounter = 0;

        LogError($"Tracked issue #{id} based on \"{stack[0].method.GetSimpleName()}\" repeated twice. Attempting to retrieve invokation trace and capture state.");
        state = DebuggingState.Capture;
        finalInstructions = new int[stack.Count];
        capturedArguments = new Dictionary<string, string>[finalInstructions.Length];
        capturedLocals = new Dictionary<string, string>[finalInstructions.Length];

        foreach (ResolvedTraceMethod method in stack)
        {
          hookedMethods.Add(method.method);
          appliedHooks.Add(DetourUtils.newILHook(method.method, MethodBodyIterator));
          capturedArguments[targetMethodIndex] = [];
          capturedLocals[targetMethodIndex] = [];
          ++targetMethodIndex;
        }
        return;
      case DebuggingState.Capture:
        if (!capturedFinalIL)
          return;
        allowedToResolve = false;
        dismissCounter = 0;

        state = DebuggingState.Finalize;
        targetMethodIndex = 0;
        DisposeHooks();

        LogInfo($"> Captured instructions before invocation:");
        foreach (ResolvedTraceMethod method in stack)
        {
          appliedHooks.Add(DetourUtils.newILHook(method.method, StateLogger));
          ++targetMethodIndex;
        }
        LogInfo("* Finished logging stack trace.");
        return;
      case DebuggingState.Finalize:
        if (!invokeFinalTarget)
          return;

        LogInfo("Captured state:");
        for (int i = 0; i < stack.Count; ++i)
        {
          string methodName = stack[i].method.GetFullSimpleName();
          if (capturedArguments[i] == null)
            LogInfo($" ([{methodName}] doesn't have any arguments)");
          else
          {
            LogInfo($"> [{methodName}] Arguments:");
            foreach (KeyValuePair<string, string> arg in capturedArguments[i])
              LogInfo(arg.Value);
          }
          if (capturedLocals[i] == null)
            LogInfo($" ([{methodName}] doesn't have any locals)");
          else
          {
            LogInfo($"> [{methodName}] Locals:");
            foreach (KeyValuePair<string, string> local in capturedLocals[i])
              LogInfo(local.Value);
          }
        }
        LogInfo("* Finished logging state.");

        DisposeHooks();
        foreach (ResolvedTraceMethod method in stack)
          hookedMethods.Remove(method.method);
        indexedIssues.Remove(id);
        issues.Remove(stack[0].method);
        resolvedIssues.Add(stack[0].method);
        return;
    }
  }

  /// <summary>
  /// Updates issue resolver in order to discard issues that won't occur anymore
  /// </summary>
  public static void UpdateNoIssues()
  {
    foreach (MethodBase targetMethod in (List<MethodBase>)[.. issues.Keys])
    {
      TrackedIssue issue = issues[targetMethod];
      if (issue.allowedToResolve || ++issue.dismissCounter < 80)
        continue;
      issue.DisposeHooks();
      foreach (ResolvedTraceMethod method in issue.stack)
        hookedMethods.Remove(method.method);
      if (++issue.failureCounter > 1)
      {
        indexedIssues.Remove(issue.id);
        issues.Remove(targetMethod);
        continue;
      }
      issue.state = DebuggingState.Wait;
      issue.dismissCounter = 0;
      issue.failureCounter++;
    }
  }

  /// <summary>
  /// Updates issue resolver with exception, which allows to progress debugging
  /// </summary>
  public static void Update(Exception e)
  {
    List<ResolvedTraceMethod> resolvedStack = e.GetResolvedMethodTrace();
    for (int i = resolvedStack.Count - 1; i >= 0; --i)
      if (resolvedStack[i].method == null
        // remove duplicates
        || resolvedStack.IndexOf(resolvedStack[i]) != i
        || resolvedStack[i].method.DeclaringType == typeof(TrackedIssue) || resolvedStack[i].method.DeclaringType?.Name == "IssueResolver")
        resolvedStack.RemoveAt(i);
    if (resolvedStack.Count == 0 || resolvedIssues.Contains(resolvedStack[0].method))
      return;
    if (issues.TryGetValue(resolvedStack[0].method, out TrackedIssue issue))
      issue.Update();
    else
    {
      issues[resolvedStack[0].method] = indexedIssues[issueCounter] = new(resolvedStack);
      ++issueCounter;
    }
  }
}

public static class Extensions
{
  public static FieldInfo StackFrame_methodAddress = typeof(StackFrame).GetField("methodAddress", BFlags.anyDeclaredInstance);
  public static Func<StackFrame, string> StackFrame_GetInternalMethodName = typeof(StackFrame).GetMethod("GetInternalMethodName", BFlags.anyDeclaredInstance)
    .CreateDelegate<Func<StackFrame, string>>();

  /// <summary>
  /// Returns function pointer of the method
  /// </summary>
  public static long GetMethodAddress(this StackFrame self) => (long)StackFrame_methodAddress.GetValue(self);

  /// <summary>
  /// Returns string, that internally represents stack frame
  /// </summary>
  public static string GetInternalName(this StackFrame self) => StackFrame_GetInternalMethodName(self);

  /// <summary>
  /// Returns list of resolved methods with according data for stack trace (which doesn't include wrapper DMDs).
  /// Methods are not provided for wrapper native methods. Offsets are assigned for all methods, but not all of them make sense.
  /// </summary>
  public static List<ResolvedTraceMethod> GetResolvedMethodTrace(this StackTrace self)
  {
    List<ResolvedTraceMethod> methods = new(self.FrameCount);
    MethodBase lastMethod = null;

    foreach (StackFrame frame in self.GetFrames())
    {
      string codeLocation = frame.GetFileName();
      if (codeLocation == null)
        codeLocation = "";
      else
        codeLocation = $" in {codeLocation}:{frame.GetFileLineNumber()}:{frame.GetFileColumnNumber()}";
      if (frame.GetMethod() is MethodBase method)
      {
        lastMethod = method;
        methods.Add(new(method, frame.GetILOffset(), method.IsILModified ? ResolvedTraceMethod.Flags.ILModified : ResolvedTraceMethod.Flags.Method, codeLocation));
        continue;
      }
      if (!RuntimeDetourManager.pinnedMethods.TryGetValue(frame.GetMethodAddress(), out MethodBase pinned)
        || !RuntimeDetourManager.specificDMDMap.TryGetValue(pinned, out LinkedDMDData dmdData))
      {
        string name = frame.GetInternalName();
        if (name.Length > 27 && name[26] == ')')
          methods.Add(new(null, frame.GetILOffset(), ResolvedTraceMethod.Flags.Native, codeLocation, name.Substring(28)));
        continue;
      }
      ResolvedTraceMethod.Flags flags;
      if (dmdData.detour is Hook hook)
      {
        if (lastMethod == hook.Target)
          methods[methods.Count - 1].flags |= ResolvedTraceMethod.Flags.Hook;
        else
        {
          flags = ResolvedTraceMethod.Flags.InlinedHook;
          if (hook.Target.IsILModified)
            flags |= ResolvedTraceMethod.Flags.ILModified;
          methods.Add(new(hook.Target, frame.GetILOffset(), flags, codeLocation));
        }
        continue;
      }
      lastMethod = dmdData.definition.OriginalMethod;
      flags = ResolvedTraceMethod.Flags.DynamicOrig;
      if (dmdData.definition.OriginalMethod.IsILModified)
        flags |= ResolvedTraceMethod.Flags.ILModified;
      methods.Add(new(dmdData.definition.OriginalMethod, frame.GetILOffset(), flags, codeLocation));
    }
    return methods;
  }

  /// <summary>
  /// Returns list of resolved methods with according data for exception's stack trace (which doesn't include wrapper DMDs).
  /// Methods are not provided for wrapper native methods. Offsets are assigned for all methods, but not all of them make sense.
  /// </summary>
  public static List<ResolvedTraceMethod> GetResolvedMethodTrace(this Exception self) => new StackTrace(self, true).GetResolvedMethodTrace();
}

public static class DebuggerUtils
{
  static string Exception_ToString(Func<Exception, string> orig, Exception self) => $"\n{GetSimplifiedException(self)}\n! <original> {orig(self)}";

  static DebuggerUtils()
  {
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
    else if (RuntimeDetourManager.pinnedMethods.TryGetValue(frame.GetMethodAddress(), out MethodBase pinned))
    {
      if (RuntimeDetourManager.DMDOwners.TryGetValue(pinned, out IDetour idetour))
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

  /// <summary>
  /// Returns string with resolved stack trace and additional description for some methods
  /// </summary>
  public static string GetSimplifiedStackTrace(StackTrace stackTrace)
  {
    StringBuilder sb = new(512);
    List<(string prefix, string method)> lines = [];
    int maxPrefix = 0;

    void AddLine(string prefix, string method)
    {
      lines.Add((prefix, method));
      if (maxPrefix < prefix.Length)
        maxPrefix = prefix.Length;
    }

    foreach (ResolvedTraceMethod resolvedMethod in stackTrace.GetResolvedMethodTrace())
    {
      string offset = " at IL_" + resolvedMethod.offset.ToString("X4");
      if (resolvedMethod.flags == 0)
        AddLine("", resolvedMethod.method.GetSimpleName() + offset + resolvedMethod.codeLocation);
      else if (resolvedMethod.IsNative)
        AddLine("<native>", resolvedMethod.additionalData + resolvedMethod.codeLocation);
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
        AddLine($"<{label}>", name + resolvedMethod.codeLocation);
      }
    }

    foreach ((string prefix, string method) in lines)
      sb.Append(prefix.PadLeft(maxPrefix)).Append(" | ").AppendLine(method);

    return sb.ToString();
  }

  /// <summary>
  /// Returns string for exception with resolved stack trace and additional description for some methods
  /// </summary>
  public static string GetSimplifiedException(Exception e)
  {
    StringBuilder sb = new(512);
    sb.Append("! <simplified> ").Append(e.GetType().GetSimpleName()).Append(": ").AppendLine(e.Message.TrimEnd());
    if (e.InnerException is Exception inner)
      sb.Append(GetSimplifiedStackTrace(new(inner, true))).AppendLine("--- End of inner exception stack trace ---");
    return sb.Append(GetSimplifiedStackTrace(new(e, true))).ToString();
  }
}