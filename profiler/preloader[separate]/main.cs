using gelbi_silly_lib;
using gelbi_silly_lib.Converter;
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace profiler;

public class MethodStats(object method)
{
  public object method = method;
  public uint invokeCounter = 0;
  public long totalTime = 0, minTime = long.MaxValue, maxTime = 0;
}

public static class Profiler
{
  public static class Processor
  {
    public static ConcurrentQueue<ProfilerEntry> queue = new();
    public static Thread worker;
    public static bool running = true;
    public static string outputPath;

    public static void Init(string _outputPath)
    {
      outputPath = _outputPath;
      (worker = new(Loop) { IsBackground = true }).Start();
      AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
    }

    public static void CurrentDomain_ProcessExit(object sender, EventArgs e)
    {
      running = false;
      worker.Join();

      LogT($"Saving profiler data");
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
          profilerDataStream.WriteLine($"{(stats.method is System.Reflection.MethodBase methodBase ? methodBase.GetSimpleName() : stats.method)}" +
            $"\t{avgTime.ToString("0.######", CultureInfo.InvariantCulture)}" +
            $"\t{minTime.ToString("0.######", CultureInfo.InvariantCulture)}" +
            $"\t{maxTime.ToString("0.######", CultureInfo.InvariantCulture)}" +
            $"\t{totalTime.ToString("0.######", CultureInfo.InvariantCulture)}" +
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

  public static WriterThread Logger;
  public static MethodReference Profiler_End, ref_GetTimestamp;
  public static TypeReference ref_long;
  public static List<MethodStats> methodStats = [];
  public static int index = 0;

  public static void Log(string str) => Logger.WriteLine(str);

  public static void LogT(string str) => Logger.WriteLine(DateTime.Now.ToString("[HH:mm:ss] ") + str);

  public static void LogError(object obj) => Logger.WriteLine($"[Error] {obj}");

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
      new ILHook(methodBase, ProfilerHook);
      methodStats.Add(new(methodBase));
      LogT($"Patched {methodBase.GetFullSimpleName()}");
    }
    catch (Exception e)
    {
      LogT($"Failed to patch {methodBase.GetFullSimpleName()}: {e}");
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
      LogT($"Skipping additional patches");
      return;
    }
    if (methods is Delegate[] delegates)
      foreach (Delegate method in delegates)
        PatchMethod(method.Method);
    else if (methods is System.Reflection.MethodBase[] methodBases)
      foreach (System.Reflection.MethodBase methodBase in methodBases)
        PatchMethod(methodBase);
    LogT($"Finished additional patches");
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

public class ProfilerSettings : BaseSavedDataHandler
{
  public bool profileGlobal = false, conditionalProfileGlobal = true, profileMods = false, profileModInit = true;

  public ProfilerSettings(string filename) : base(filename) { }

  public ProfilerSettings(string[] nestedFolders, string filename) : base(nestedFolders, filename) { }

  public override void BaseLoad()
  {
    data.TryUpdateValueWithType("profileGlobal", ref profileGlobal);
    data.TryUpdateValueWithType("conditionalProfileGlobal", ref conditionalProfileGlobal);
    data.TryUpdateValueWithType("profileMods", ref profileMods);
    data.TryUpdateValueWithType("profileModInit", ref profileModInit);
  }
}

public static class Patcher
{
  public const string customLogsPath = "customLogs", profilerLogPath = $"{customLogsPath}/profilerLog.txt",
    profilerDataPath = $"{customLogsPath}/profilerData.txt";

  public static int allPatchedMethodCounter = 0;
  public static Stopwatch initializationStopwatch;
  public static ProfilerSettings settings;

  static Patcher()
  {
    Profiler.Logger = new(profilerLogPath);
  }

  public static void Initialize()
  {
    initializationStopwatch = Stopwatch.StartNew();
    Console.WriteLine("profiler-preloader ♥");
    Profiler.LogT($"Initializing profiler");

    settings = new(["gelbi"], "profiler-settings");

    Directory.CreateDirectory(customLogsPath);
    Profiler.Processor.Init(profilerDataPath);
  }

  public static IEnumerable<string> TargetDLLs
  {
    get
    {
      if (!settings.profileGlobal)
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
    Profiler.LogT($"** Finished patching initial {allPatchedMethodCounter} methods in {initializationStopwatch.Elapsed.TotalSeconds:F6} seconds");
    if (errorCounter != 0)
      Profiler.Log($"Failed to patch {errorCounter} methods");

    if (settings.profileMods)
      AssemblyUtils.AddUniversalAssemblyPatch(Patch);
  }

  public static int patchedMethodCounter = 0, errorCounter = 0;
  public static TypeReference asyncType, iteratorType;

  public static void Patch(AssemblyDefinition asm)
  {
    patchedMethodCounter = 0;
    Profiler.LogT($"Patching methods of {asm.Name.Name}");
    Stopwatch stopwatch = Stopwatch.StartNew();
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
    Profiler.LogT($"Finished patching {patchedMethodCounter} methods in {stopwatch.Elapsed.TotalSeconds:F6} seconds");
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
        && (!settings.conditionalProfileGlobal || method.Body.Instructions.Count > 50)
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
