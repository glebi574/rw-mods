using BepInEx.Preloader.Patching;
using gelbi_silly_lib;
using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace profiler;

public class MethodStats
{
  public object method = null;
  public uint invokeCounter = 0;
  public long totalTime = 0, minTime = long.MaxValue, maxTime = 0;

  public MethodStats(object method)
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
      })
      { IsBackground = true }).Start();
    }

    public static void Log(string msg)
    {
      logQueue.Enqueue(msg);
      logSignal.Set();
    }
  }

  public static class Processor
  {
    public static ConcurrentQueue<ProfilerEntry> queue = new();
    public static Thread worker;
    public static bool running = true;
    public static string outputPath;

    public static void Init(string _outputPath)
    {
      outputPath = _outputPath;
      (worker = new Thread(Loop) { IsBackground = true }).Start();
      AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
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
          profilerDataStream.WriteLine($"{(stats.method is System.Reflection.MethodBase methodBase ? methodBase.GetFullName() : stats.method)}" +
            $"\t{avgTime:0.######}" +
            $"\t{minTime:0.######}" +
            $"\t{maxTime:0.######}" +
            $"\t{totalTime:0.######}" +
            $"\t{stats.invokeCounter}");
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
        while (queue.TryDequeue(out ProfilerEntry entry))
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

  public struct ProfilerEntry(int id, long duration)
  {
    public int id = id;
    public long duration = duration;
  }

  public static MethodReference Profiler_End, ref_GetTimestamp;
  public static TypeReference ref_long;
  public static List<MethodStats> methodStats = [];
  public static int index = 0;

  public static void Log(string str)
  {
    Logger.Log(str);
  }

  public static void LogError(object obj)
  {
    Logger.Log($"[Error] {obj}");
  }

  public static void ProfilerPatcher(MethodDefinition method)
  {
    MethodBody body = method.Body;
    ILProcessor il = body.GetILProcessor();
    Instruction first = body.Instructions[0];

    methodStats.Add(new(method));
    int id = index++;

    VariableDefinition localTime = new(ref_long);
    body.Variables.Add(localTime);
    body.InitLocals = true;

    il.InsertBefore(first, il.Create(OpCodes.Call, ref_GetTimestamp));
    il.InsertBefore(first, il.Create(OpCodes.Stloc, localTime));

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
      il.InsertAfter(ret, il.Create(OpCodes.Ldloc, localTime));

      ret.OpCode = OpCodes.Ldc_I4;
      ret.Operand = id;
    }
  }

  public static void End(int id, long startTime)
  {
    Processor.queue.Enqueue(new(id, Stopwatch.GetTimestamp() - startTime));
  }

  public static void PatchMethod(System.Reflection.MethodBase methodBase)
  {
    try
    {
      new ILHook(methodBase, ProfilerHook);
      methodStats.Add(new(methodBase));
      Log($"{Patcher.CurrentTime} Patching {methodBase.GetFullName()}");
    }
    catch (Exception e)
    {
      Log($"{Patcher.CurrentTime} Failed to patch {methodBase.GetFullName()}: {e}");
    }
  }

  /// <summary>
  /// Allows to specify methods to profile if neither of global profiling options are enabled
  /// </summary>
  public static void PatchMethods(params System.Reflection.MethodBase[] methods)
  {
    if (Patcher.profileGlobal || Patcher.profileMods)
    {
      Log($"{Patcher.CurrentTime} Skipping additional patches");
      return;
    }
    Log($"{Patcher.CurrentTime} Patching provided methods");
    foreach (System.Reflection.MethodBase methodBase in methods)
      PatchMethod(methodBase);
    Log($"{Patcher.CurrentTime} Finished additional patches");
  }

  /// <summary>
  /// Allows to specify methods to profile if neither of global profiling options are enabled
  /// </summary>
  public static void PatchMethods(params Delegate[] methods)
  {
    if (Patcher.profileGlobal || Patcher.profileMods)
    {
      Log($"{Patcher.CurrentTime} Skipping additional patches");
      return;
    }
    Log($"{Patcher.CurrentTime} Patching provided methods");
    foreach (Delegate method in methods)
      PatchMethod(method.Method);
    Log($"{Patcher.CurrentTime} Finished additional patches");
  }

  public static void ProfilerHook(ILContext il)
  {
    ILCursor c = new(il);
    MethodBody body = il.Body;
    int id = index++;

    VariableDefinition localTime = new(il.Module.ImportReference(typeof(long)));
    body.Variables.Add(localTime);
    body.InitLocals = true;

    c.EmitDelegate(Stopwatch.GetTimestamp);
    c.Emit(OpCodes.Stloc, localTime);

    foreach (Instruction ret in body.Instructions.Where(i =>
    {
      OpCode c = i.OpCode;
      return c == OpCodes.Ret || c == OpCodes.Throw;
    }).ToList())
    {
      c.Goto(ret);
      c.Emit(OpCodes.Ldc_I4, id);
      c.Emit(OpCodes.Ldloc, localTime);
      c.EmitDelegate(End);
    }
  }
}

public static class Patcher
{
  public const string customLogsPath = "customLogs", profilerLogPath = $"{customLogsPath}/profilerLog.txt",
    profilerDataPath = $"{customLogsPath}/profilerData.txt";

  public static string CurrentTime { get => $"[{DateTime.Now:HH:mm:ss}]"; }

  public static SavedDataManager saveManager;
  public static Dictionary<string, object> settings;
  public static int allPatchedMethodCounter = 0;
  public static long initializeTime = 0;
  public static bool profileGlobal = false, profileMods = false, profileModInit = true;

  public static void Initialize()
  {
    initializeTime = Stopwatch.GetTimestamp();
    Console.WriteLine("profiler-preloader ♥");
    Profiler.Log($"{CurrentTime} Initializing profiler");

    saveManager = new(["gelbi"], "profiler-settings");
    settings = saveManager.Read();
    if (settings == null)
      settings = [];
    else
    {
      profileGlobal = (bool)settings["profileGlobal"];
      profileMods = (bool)settings["profileMods"];
      profileModInit = (bool)settings["profileModInit"];
    }

    if (!Directory.Exists(customLogsPath))
      Directory.CreateDirectory(customLogsPath);
    Profiler.Logger.Init(profilerLogPath);
    Profiler.Processor.Init(profilerDataPath);
  }

  public static IEnumerable<string> TargetDLLs
  {
    get
    {
      if (!profileGlobal)
        yield break;
      foreach (string assemblyPath in Directory.GetFiles("RainWorld_Data\\Managed").Where(p => Path.GetExtension(p) == ".dll"))
      {
        string assemblyName = Path.GetFileName(assemblyPath);
        if (!assembliesToSkip.Contains(assemblyName))
          yield return assemblyName;
      }
    }
  }

  public static void Finish()
  {
    Profiler.Log($"{CurrentTime} ** Finished patching initial {allPatchedMethodCounter} methods in {(double)(Stopwatch.GetTimestamp() - initializeTime) / Stopwatch.Frequency} seconds");
    if (errorCounter != 0)
      Profiler.Log($"Failed to patch {errorCounter} methods");

    if (!profileMods)
      return;
    new Hook(typeof(System.Reflection.Assembly).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
      .First(m => m.Name == "LoadFile" && m.GetParameters().Count() == 1), Assembly_LoadFile);
  }

  public static System.Reflection.Assembly Assembly_LoadFile(Func<string, System.Reflection.Assembly> orig, string path)
  {
    AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(path);
    Patch(asm);
    AssemblyPatcher.Load(asm, Path.GetFileName(path));
    return AppDomain.CurrentDomain.GetAssemblies().Last();
  }

  public static int patchedMethodCounter = 0, errorCounter = 0;
  public static TypeReference asyncType, iteratorType;

  public static void Patch(AssemblyDefinition asm)
  {
    patchedMethodCounter = 0;
    Profiler.Log($"{CurrentTime} Patching methods of {asm.Name.Name}");
    long startTime = Stopwatch.GetTimestamp();
    foreach (ModuleDefinition module in asm.Modules)
    {
      Profiler.Profiler_End = module.ImportReference(typeof(Profiler).GetMethod(nameof(Profiler.End)));
      Profiler.ref_GetTimestamp = module.ImportReference(typeof(Stopwatch).GetMethod(nameof(Stopwatch.GetTimestamp)));
      Profiler.ref_long = module.ImportReference(typeof(long));
      asyncType = module.ImportReference(typeof(AsyncStateMachineAttribute));
      iteratorType = module.ImportReference(typeof(IteratorStateMachineAttribute));
      foreach (TypeDefinition type in module.Types)
        PatchType(type);
    }
    Profiler.Log($"{CurrentTime} Finished patching {patchedMethodCounter} methods in {(double)(Stopwatch.GetTimestamp() - startTime) / Stopwatch.Frequency} seconds");
  }

  public static readonly HashSet<string> namespacesToSkip = [
      "System", "On", "IL", "Microsoft", "Rewired",
      "MS", "Sony", "Stove", "ObjCRuntimeInternal",
      "XamMac", "Internal", "AssetBundles", "profiler" ],
    assembliesToSkip = [
      "System.Core.dll", "MonoMod.Utils.dll", "Mono.Cecil.dll", "Mono.Cecil.Mdb.dll", "Mono.Cecil.Pdb.dll", "Mono.Cecil.Rocks.dll",
      "MonoMod.RuntimeDetour.dll", "Mono.Security.dll", "System.Configuration.dll", "System.Xml.dll", "Rewired_Windows.dll",
      "Rewired_Core.dll", "gelbi-silly-lib-preloader.dll" ];

  public static bool IsSkippedNamespace(string name)
  {
    if (name == null)
      return true;
    foreach (string skippedNamespace in namespacesToSkip)
      if (name.StartsWith(skippedNamespace))
        return true;
    return false;
  }

  public static readonly (string declaringType, string name)[] skippedMethods =
  [
    ("ObjectsPage", "Refresh"),
    ("SlugNPCAI", "Move"),
  ];

  public static void PatchType(TypeDefinition type, string indent = "")
  {
    if (IsSkippedNamespace(type.Namespace) || type.Name.StartsWith("<"))
      return;

    Profiler.Log(indent + type);
    foreach (TypeDefinition nestedType in type.NestedTypes)
      PatchType(nestedType, indent + "  ");

    ConcurrentBag<MethodDefinition> methods = [];
    Parallel.ForEach(type.Methods, method =>
    {
      if (method.HasBody && !method.Name.StartsWith("<") && (!method.IsConstructor || !method.IsStatic)
        && !method.CustomAttributes.Any(a => a.AttributeType == asyncType || a.AttributeType == iteratorType)
        && !skippedMethods.Any(m => method.DeclaringType.Name == m.declaringType && method.Name == m.name))
        methods.Add(method);
    });

    foreach (MethodDefinition method in methods)
    {
      Profiler.Log($"{indent}| {method}");
      try
      {
        Profiler.ProfilerPatcher(method);
      }
      catch (Exception e)
      {
        Profiler.Log($"Failed to patch {method}: {e}");
        ++errorCounter;
      }

      ++allPatchedMethodCounter;
      ++patchedMethodCounter;
    }
  }
}
