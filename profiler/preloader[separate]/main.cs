using gelbi_silly_lib;
using gelbi_silly_lib.Converter;
using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil;
using MonoMod.Utils;
using profiler_preloader;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace profiler;

public class MethodStats(object method)
{
  public object method = method;
  public string name = method switch
  {
    MethodDefinition methodDefinition => methodDefinition.GetFullSimpleName(),
    MethodBase methodBase => methodBase.GetFullSimpleName(),
    _ => method.ToString()
  };
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
      GSLPUtils.OnExit += OnExit;
    }

    public static void OnExit()
    {
      running = false;
      worker.Join();

      LogT($"Saving profiler data");
      try
      {
        StreamWriter profilerDataStream = new(outputPath, false);
        string unit;
        double modifier;
        if (Patcher.settings.profileMemory)
        {
          unit = "(MB)";
          overhead = 0.0;
          modifier = 1.0 / 0x100000;
        }
        else
        {
          unit = "(ms)";
          modifier = 1000.0 / Stopwatch.Frequency;
        }
        profilerDataStream.WriteLine($"Method\tavg{unit}\tmin{unit}\tmax{unit}\ttotal{unit}\tinvokes");
        StringBuilder sb = new(256);
        foreach (MethodStats stats in methodStats)
        {
          if (stats.invokeCounter == 0)
            continue;
          double totalTime = (stats.totalTime - overhead * stats.invokeCounter) * modifier;
          profilerDataStream.WriteLine(sb.Clear().Append(stats.method switch
          {
            MethodBase methodBase => methodBase.GetSimpleName(),
            MethodDefinition methodDefinition => methodDefinition.GetSimpleName(),
            _ => stats.method.ToString()
          }).Append('\t').Append((totalTime / stats.invokeCounter).ToString("0.######", CultureInfo.InvariantCulture))
            .Append('\t').Append((Math.Max(0.0, stats.minTime - overhead) * modifier).ToString("0.######", CultureInfo.InvariantCulture))
            .Append('\t').Append(((stats.maxTime - overhead) * modifier).ToString("0.######", CultureInfo.InvariantCulture))
            .Append('\t').Append(totalTime.ToString("0.######", CultureInfo.InvariantCulture))
            .Append('\t').Append(stats.invokeCounter).ToString());
        }
        profilerDataStream.Flush();
        profilerDataStream.Close();
      }
      catch (Exception ei)
      {
        Log($"[Error] Failed to save profiler data: {ei}");
      }
    }

    public static void Loop()
    {
      while (running || !queue.IsEmpty)
      {
        while (queue.TryDequeue(out ProfilerEntry entry))
        {
          MethodStats stats = methodStats[entry.id];
          if (trackedMethods.Contains(entry.id))
          {
            string msg = $"[Tracked] ({entry.duration * 1000.0 / Stopwatch.Frequency:0}ms) {stats.method switch
            {
              MethodBase methodBase => methodBase.GetSimpleName(),
              MethodDefinition methodDefinition => methodDefinition.GetSimpleName(),
              _ => stats.method.ToString()
            }}";
            Console.WriteLine(msg);
            GSLLog.GLog(msg);
          }
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
  public static Dictionary<MethodBase, int> profiledMethods = [];
  public static List<MethodStats> methodStats = [];
  public static HashSet<int> trackedMethods = [];
  public static MethodDefinition GetMonoUsedSizeLong = AssemblyDefinition.ReadAssembly("RainWorld_Data\\Managed\\UnityEngine.CoreModule.dll").MainModule
    .GetType("UnityEngine.Profiling.Profiler").FindMethod("GetMonoUsedSizeLong");
  public static int index = 0;
  public static double frequencyModifier = 1000.0 / Stopwatch.Frequency, overhead = Stopwatch.Frequency == 10000000 ? 0.24 : 0.0; // doing nothing still measures >21ns of overhead?

  public static int GetID(MethodBase method) => profiledMethods.TryGetValue(method, out int id) ? id : (profiledMethods[method] = index++);

  public static void Log(string str) => Logger.WriteLine(str);

  public static void LogT(string str) => Logger.WriteLine(DateTime.Now.ToString("[HH:mm:ss] ") + str);

  public static void End(int id, long start, long end) => Processor.queue.Enqueue(new(id, end - start));

  public static void AddTrackedMethod(string name)
  {
    bool added = false;
    for (int i = 0; i < methodStats.Count; ++i)
      if (methodStats[i].name == name)
      {
        trackedMethods.Add(i);
        added = true;
      }
    if (!added)
      throw new ArgumentException("Couldn't find profiled method with given name. See profilerLog.txt for methods that are profiled. Search is only performed for pattern 'namespace.declaringType.name'");
  }
}

public class ProfilerSettings : BaseSavedDataHandler
{
  public bool profileGlobal = false, conditionalProfileGlobal = true, profileMods = false, profileModInit = false, profileMemory = false, profileThreads = false;

  public ProfilerSettings(string filename) : base(filename) { }

  public ProfilerSettings(string[] nestedFolders, string filename) : base(nestedFolders, filename) { }

  public override void BaseLoad()
  {
    data.TryUpdateValueWithType("profileGlobal", ref profileGlobal);
    data.TryUpdateValueWithType("conditionalProfileGlobal", ref conditionalProfileGlobal);
    data.TryUpdateValueWithType("profileMods", ref profileMods);
    data.TryUpdateValueWithType("profileModInit", ref profileModInit);
    data.TryUpdateValueWithType("profileMemory", ref profileMemory);
  }
}

public static class Patcher
{
  public const string customLogsPath = "customLogs\\", profilerLogPath = $"{customLogsPath}profilerLog.txt", profilerDataPath = $"{customLogsPath}profilerData.txt";

  public static int patchedMethodCounter = 0, errorCounter = 0, allPatchedMethodCounter = 0;
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
    if (settings.profileThreads)
      ThreadPatcher.Apply();
  }

  public static IEnumerable<string> TargetDLLs
  {
    get
    {
      if (!settings.profileGlobal)
        yield break;
      foreach (string assemblyPath in Directory.GetFiles("RainWorld_Data\\Managed", "*.dll"))
      {
        string assemblyName = Path.GetFileName(assemblyPath);
        if (!DefinitionPatcher.assembliesToSkip.Contains(assemblyName))
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
    //MethodPatcher.PatchMethods([
    //  typeof(DMDEmitDynamicMethodGenerator).GetMethod("_Generate", BFlags.anyDeclaredInstance),
    //  //typeof(MonoMod.Utils.Extensions).GetMethod("GetPatchFullName", BFlags.anyDeclaredStatic, null, [typeof(MemberReference)], null)
    //  //typeof(MonoMod.Utils.Extensions).GetMethod("GetID", BFlags.anyDeclaredStatic, null, [typeof(MethodReference), typeof(string), typeof(string), typeof(bool), typeof(bool)], null),
    //  //typeof(ReflectionHelper).GetMethod("GetRuntimeHashedFullName", BFlags.anyDeclaredStatic, null, [typeof(AssemblyNameReference)], null),
    //  //typeof(ILGeneratorShimExt).GetMethod("DynEmit", BFlags.anyDeclaredStatic, null, [typeof(ILGenerator), typeof(object[])], null)
    //]);
    //MethodPatcher.PatchMethods([
    //  ImplMonoMod.ReflectionHelper__ResolveReflection,
    //  //ImplMonoMod.GetMemberName,
    //  //ImplMonoMod.Extensions_GetPatchFullName,
    //  //ImplMonoMod.ILGeneratorShimExt_DynEmit,
    //  //ImplMonoMod.Extensions_Is,
    //]);
  }

  public static void Patch(AssemblyDefinition asm)
  {
    patchedMethodCounter = 0;
    Profiler.LogT($"Patching methods of {asm.Name.Name}");
    Stopwatch stopwatch = Stopwatch.StartNew();
    DefinitionPatcher.PatchAssembly(asm);
    Profiler.LogT($"Finished patching {patchedMethodCounter} methods in {stopwatch.Elapsed.TotalSeconds:F6} seconds");
  }
}