using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace gelbi_silly_lib;

public static class Patcher
{
  public const string PLUGIN_GUID = "0gelbi.silly-lib", PLUGIN_NAME = "gelbi's Silly Lib", PLUGIN_VERSION = "1.1.0";

  public static void Initialize(params Type[] modules)
  {
    Stopwatch start = Stopwatch.StartNew(), moduleStart = Stopwatch.StartNew();
    GSLLog.GLog();
    Console.WriteLine("gelbi-silly-lib-preloader ♥");
    foreach (Type module in modules)
    {
      moduleStart.Restart();
      module.RunClassConstructor();
      GSLLog.GLog($"Initialized {module.Name} in {moduleStart.ElapsedMilliseconds}ms");
    }
    GSLLog.GLog($"Finished silly features initialization in {start.ElapsedMilliseconds}ms\n");
  }

  public static IEnumerable<string> TargetDLLs
  {
    get
    {
      Initialize([
        typeof(OptimizedImplementation.SillyOptimizations),
        typeof(SavedDataManager),
        typeof(GSLSettings),
        typeof(RuntimeDetourManager),
        typeof(WriterThread),
        typeof(GSLLog),
        typeof(GSLPUtils),
        typeof(AssemblyMap),
        typeof(AssemblyUtils),
        typeof(PluginUtils),
      ]);

      if (GSLSettings.instance.disableEOS)
        yield return "com.playeveryware.eos.core.dll";
    }
  }

  public static void EmptyMethods(TypeDefinition type)
  {
    foreach (MethodDefinition method in type.Methods)
      if (method.HasBody && method.ReturnType.MetadataType == MetadataType.Void)
      {
        method.Body.Instructions.Clear();
        method.Body.Instructions.Add(method.Body.GetILProcessor().Create(OpCodes.Ret));
      }
  }

  public static void Patch(AssemblyDefinition asm)
  {
    Stopwatch start = Stopwatch.StartNew();
    switch (asm.Name.Name)
    {
      case "com.playeveryware.eos.core":
        EmptyMethods(asm.MainModule.GetType("PlayEveryWare.EpicOnlineServices.EOSManager"));
        EmptyMethods(asm.MainModule.GetType("PlayEveryWare.EpicOnlineServices.EOSManager/EOSSingleton"));
        break;
    }
    GSLLog.GLog($"Finished patching {asm.Name.Name} in {start.ElapsedMilliseconds}ms");
  }
}