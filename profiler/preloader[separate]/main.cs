using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace profiler;

public class MethodStats
{
  public MethodDefinition method = null;
  public uint invokeCounter = 0;
  public long totalTime = 0, minTime = long.MaxValue, maxTime = 0;

  public MethodStats(MethodDefinition method)
  {
    this.method = method;
  }
}

public static class Profiler
{
  public static class Logger
  {
    public static readonly ConcurrentQueue<string> logQueue = new();
    public static readonly AutoResetEvent logSignal = new(false);
    public static Thread logThread;
    public static bool running = true;

    public static void Init(string path)
    {
      (logThread = new(() =>
      {
        StreamWriter writer = new(path, false);
        while (running)
        {
          logSignal.WaitOne();
          while (logQueue.TryDequeue(out string msg))
            writer.WriteLine(msg);
          writer.Flush();
        }
      }) { IsBackground = true }).Start();
    }

    public static void Log(string msg)
    {
      logQueue.Enqueue(msg);
      logSignal.Set();
    }
  }

  public static class Processor
  {
    public static readonly ConcurrentQueue<(int id, long duration)> queue = new();
    public static Thread worker;
    public static bool running = true;
    public static string outputPath;

    public static void Init(string _outputPath)
    {
      outputPath = _outputPath;
      (worker = new Thread(Loop) { IsBackground = true }).Start();
      AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit; ;
    }

    public static void CurrentDomain_ProcessExit(object sender, EventArgs e)
    {
      running = false;
      worker.Join();

      Log($"{Patcher.CurrentTime} Saving profiler data");
      try
      {
        StreamWriter profilerDataStream = new(outputPath, false);
        profilerDataStream.WriteLine("Method\tavg(ms)\tmin(ms)\tmax(ms)\ttotal(ms)\tinvokes");
        foreach (MethodStats stats in methodStats)
        {
          if (stats.invokeCounter == 0)
            continue;
          double modifier = 1000.0 / Stopwatch.Frequency,
            totalTime = stats.totalTime * modifier,
            avgTime = totalTime / stats.invokeCounter,
            minTime = stats.minTime * modifier,
            maxTime = stats.maxTime * modifier;
          profilerDataStream.WriteLine($"{stats.method}\t{avgTime}\t{minTime}\t{maxTime}\t{totalTime}\t{stats.invokeCounter}");
        }
        profilerDataStream.Flush();
        profilerDataStream.Close();
      }
      catch (Exception ei)
      {
        LogError($"Failed to save profiler data: {ei}");
      }
    }

    public static void Loop()
    {
      while (running || !queue.IsEmpty)
      {
        while (queue.TryDequeue(out (int id, long duration) entry))
        {
          MethodStats stats = methodStats[entry.id];
          ++stats.invokeCounter;
          stats.totalTime += entry.duration;
          stats.minTime = Math.Min(entry.duration, stats.minTime);
          stats.maxTime = Math.Max(entry.duration, stats.maxTime);
        }
        Thread.Sleep(1);
      }
    }
  }

  public static MethodReference Profiler_Start, Profiler_End;
  public static List<MethodStats> methodStats = new();
  public static int index = 0;

  public static void Log(string str)
  {
    Logger.Log(str);
  }

  public static void LogError(object obj)
  {
    Logger.Log($"[Error] {obj}");
  }

  public static void ProfilerHook(MethodDefinition method)
  {
    MethodBody body = method.Body;
    ILProcessor il = body.GetILProcessor();
    Instruction first = body.Instructions[0];

    methodStats.Add(new(method));
    int id = index++;

    VariableDefinition local = new(Profiler_Start.ReturnType);
    body.Variables.Add(local);
    body.InitLocals = true;

    il.InsertBefore(first, il.Create(OpCodes.Call, Profiler_Start));
    il.InsertBefore(first, il.Create(OpCodes.Stloc, local));

    foreach (Instruction i in body.Instructions)
      if (i.Operand is Instruction && i.OpCode.OperandType == OperandType.ShortInlineBrTarget)
        i.OpCode = i.OpCode.ToLongOp();
    foreach (Instruction ret in body.Instructions.Where(i =>
    {
      OpCode c = i.OpCode;
      return c == OpCodes.Ret || c == OpCodes.Throw;
    }).ToList())
    {
      il.InsertAfter(ret, il.Create(ret.OpCode));
      il.InsertAfter(ret, il.Create(OpCodes.Call, Profiler_End));
      il.InsertAfter(ret, il.Create(OpCodes.Ldloc, local));

      ret.OpCode = OpCodes.Ldc_I4;
      ret.Operand = id;
    }
  }

  public static long Start()
  {
    return Stopwatch.GetTimestamp();
  }

  public static void End(int id, long startTime)
  {
    Processor.queue.Enqueue((id, Stopwatch.GetTimestamp() - startTime));
  }
}

public static class Patcher
{
  public const string customLogsPath = "customLogs", profilerLogPath = $"{customLogsPath}/profilerLog.txt",
    profilerDataPath = $"{customLogsPath}/profilerData.txt";

  public static string CurrentTime { get => $"[{DateTime.Now:HH:mm:ss}]"; }

  public static IEnumerable<string> TargetDLLs
  {
    get
    {
      Console.WriteLine("profiler-preloader ♥");

      if (!Directory.Exists(customLogsPath))
        Directory.CreateDirectory(customLogsPath);
      Profiler.Logger.Init(profilerLogPath);
      Profiler.Processor.Init(profilerDataPath);
      Profiler.Log($"{CurrentTime} Initializing profiler");

      yield return "Assembly-CSharp.dll";
    }
  }

  public static int patchedMethodCounter = 0;
  public static TypeReference asyncType, iteratorType;

  public static void Patch(AssemblyDefinition asm)
  {
    Profiler.Log($"{CurrentTime} Patching methods");
    long startTime = Stopwatch.GetTimestamp();
    foreach (ModuleDefinition module in asm.Modules)
    {
      Profiler.Profiler_Start = module.ImportReference(typeof(Profiler).GetMethod(nameof(Profiler.Start)));
      Profiler.Profiler_End = module.ImportReference(typeof(Profiler).GetMethod(nameof(Profiler.End)));
      asyncType = module.ImportReference(typeof(AsyncStateMachineAttribute));
      iteratorType = module.ImportReference(typeof(IteratorStateMachineAttribute));
      foreach (TypeDefinition type in module.Types)
        PatchType(type);
    }
    Profiler.Log($"{CurrentTime} Finished patching {patchedMethodCounter} methods in {(double)(Stopwatch.GetTimestamp() - startTime) / Stopwatch.Frequency} seconds");
    // asm.Write("wawa.dll");
  }

  public static readonly string[] namespacesToSkip = new string[] { "System", "On", "IL", "Mono", "Microsoft",
      "MS", "BepInEx", "Harmony", "Unity", "Steamworks", "Epic", "Sony", "Rewired", "Stove", "Galaxy",
      "PlayEveryWare", "TMPro", "Kittehface", "JetBrains", "AOT", "Dragons", "ObjCRuntimeInternal",
      "XamMac", "Internal", "AssetBundles", "profiler" };

  public static bool IsSkippedNamespace(string name)
  {
    if (name == null)
      return true;
    foreach (string skippedNamespace in namespacesToSkip)
      if (name.StartsWith(skippedNamespace))
        return true;
    return false;
  }

  public static void PatchType(TypeDefinition type, string indent = "")
  {
    if (IsSkippedNamespace(type.Namespace) || type.Name.StartsWith("<"))
      return;

    Profiler.Log(indent + type);
    foreach (TypeDefinition nestedType in type.NestedTypes)
      PatchType(nestedType, indent + "  ");

    foreach (MethodDefinition method in type.Methods)
    {
      if (!method.HasBody || method.Name.StartsWith("<") || method.IsConstructor && method.IsStatic
        || method.CustomAttributes.Any(a => a.AttributeType == asyncType || a.AttributeType == iteratorType))
        continue;

      Profiler.Log($"{indent}| {method}");
      try
      {
        Profiler.ProfilerHook(method);
      }
      catch (Exception e)
      {
        Profiler.Log($"Failed to patch {method}: {e}");
      }

      ++patchedMethodCounter;
    }
  }
}
