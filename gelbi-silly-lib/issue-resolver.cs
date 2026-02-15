using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

public static class IssueResolver
{
  public static Exception latestException;
  public static int issueCounter = 0, iloffset;
  public static bool loggedModifyDefs = false, needLogModifyDefs = false, appliedStateLoggers = false;
  public static List<ILHook> stateLoggers = [];

  internal static void ApplyHooks()
  {
    IL.RainWorld.HandleLog += RainWorld_HandleLog;
    On.RainWorld.Update += RainWorld_Update;
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
      latestException = e;
      throw;
    }
    finally
    {
      if (appliedStateLoggers)
      {
        foreach (ILHook hook in stateLoggers)
          hook.Undo();
        appliedStateLoggers = false;
        stateLoggers.Clear();
      }
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
        GSLLog.GLog($"{GSLLog.TimeLabel()} [unhandled exception] {latestException}");
      });

    if (c.TryGotoNext(MoveType.AfterLabel, i => i.OpCode == OpCodes.Leave_S))
      c.Emit(OpCodes.Call, ((Delegate)Update).Method);
  }

  static void Update()
  {
    if (needLogModifyDefs && issueCounter == 12)
    {
      needLogModifyDefs = false;
      loggedModifyDefs = true;
      LogInfo(".Parse was detected, logging modify defs.");
      ModUtils.LogDefsForPath("modify\\world", "modify");
    }
    if (++issueCounter != 12)
      return;
    LogError("Game stayed frozen due to exception being thrown for more than 12 frames in a row. State of the stack trace will be logged on the next frame.");

    appliedStateLoggers = true;
    StackTrace stackTrace = new(latestException);
    Dictionary<MethodBase, int> methods = [];
    foreach (StackFrame frame in stackTrace.GetFrames())
    {
      MethodBase frameMethod = frame.GetMethod();
      if (frameMethod == null && RuntimeDetourManager.pinnedMethods.TryGetValue((long)RuntimeDetourManager.StackFrame_methodAddress.GetValue(frame), out MethodBase pinned)
        && RuntimeDetourManager.DMDOwners.TryGetValue(pinned, out IDetour detour))
        frameMethod = detour.GetMethod();
      if (frameMethod != null)
        methods[frameMethod] = frame.GetILOffset();
    }
    foreach (KeyValuePair<MethodBase, int> baseMethod in methods)
      ApplyStateLogger(baseMethod.Key, baseMethod.Value);
    needLogModifyDefs = !loggedModifyDefs && stackTrace.FrameCount > 0 && stackTrace.GetFrame(0).GetMethod() is MethodBase method && method.DeclaringType?.Namespace == "System" && method.Name.StartsWith("Parse");
  }

  public static MethodBase currentMethod;
  public static void ApplyStateLogger(MethodBase method, int offset)
  {
    iloffset = offset;
    currentMethod = method;
    stateLoggers.Add(DetourUtils.newILHook(method, StateLogger));
  }

  static void LogValue(string name, object obj)
  {
    if (obj == null)
      LogInfo($"  {name}: null");
    else
      LogInfo($"  {obj.GetType().GetSimpleNameWithNamespace()} {name}: {obj}");
  }

  static void StateLogger(ILContext il)
  {
    ILCursor c = new(il);
    // assuming that part MonoMod generates is always 14 bytes long, which is most likely wrong
    if (!(c.TryGotoNext(MoveType.AfterLabel, i => i.Offset == iloffset) || iloffset >= 14 && c.TryGotoNext(MoveType.AfterLabel, i => i.Offset == iloffset - 14)))
    {
      LogWarning($"Couldn't patch {currentMethod.GetFullSimpleName()} at offset IL_{iloffset:X4} or {iloffset - 14:X4}");
      return;
    }
    static void EmitLog(ILCursor c, string str) => c.Emit(OpCodes.Ldstr, str).EmitDelegate(static (string str) => LogInfo(str));
    if (il.Method.Parameters.Count != 0)
    {
      EmitLog(c, $"> [{currentMethod.GetFullSimpleName()}] Arguments:");
      foreach (Mono.Cecil.ParameterDefinition parameter in il.Method.Parameters)
      {
        c.Emit(OpCodes.Ldstr, parameter.Name);
        if (parameter.ParameterType.IsByReference || parameter.ParameterType.IsPointer)
        {
          c.Emit(OpCodes.Ldarga, parameter.Index);
          c.Emit(OpCodes.Ldobj, parameter.ParameterType.GetElementType());
          c.Emit(OpCodes.Box, parameter.ParameterType.GetElementType());
        }
        else
        {
          c.Emit(OpCodes.Ldarg, parameter.Index);
          c.Emit(OpCodes.Box, parameter.ParameterType);
        }
        c.EmitDelegate(LogValue);
      }
    }
    else
      EmitLog(c, $" ([{currentMethod.GetFullSimpleName()}] doesn't have any arguments)");
    if (il.Body.Variables.Count != 0)
    {
      EmitLog(c, $"> [{currentMethod.GetFullSimpleName()}] Locals:");
      foreach (VariableDefinition variable in il.Body.Variables)
      {
        c.Emit(OpCodes.Ldstr, "V_" + variable.Index);
        if (variable.VariableType.IsByReference || variable.VariableType.IsPointer)
        {
          c.Emit(OpCodes.Ldloca, variable.Index);
          c.Emit(OpCodes.Ldobj, variable.VariableType.GetElementType());
          c.Emit(OpCodes.Box, variable.VariableType.GetElementType());
        }
        else
        {
          c.Emit(OpCodes.Ldloc, variable.Index);
          c.Emit(OpCodes.Box, variable.VariableType);
        }
        c.EmitDelegate(LogValue);
      }
    }
    else
      EmitLog(c, $" ([{currentMethod.GetFullSimpleName()}] doesn't have any locals)");
  }
}