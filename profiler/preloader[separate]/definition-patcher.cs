using gelbi_silly_lib.MonoModUtils;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace profiler;

/// <summary>
/// Allows to patch method definitions in order to profile them
/// </summary>
public static class DefinitionPatcher
{
  public static int currentIndent = 2;
  public static MethodReference Profiler_End, ref_getMeasure;
  public static TypeReference ref_long, asyncType, iteratorType;

  public static void PatchMethod(MethodDefinition method)
  {
    MethodBody body = method.Body;
    ILProcessor il = body.GetILProcessor();

    Profiler.methodStats.Add(new(method));
    int id = Profiler.index++;

    VariableDefinition localTime = new(ref_long);
    body.Variables.Add(localTime);
    body.InitLocals = true;

    il.InsertBefore(body.Instructions[0], il.Create(OpCodes.Stloc, localTime));
    il.InsertBefore(body.Instructions[0], il.Create(OpCodes.Call, ref_getMeasure));

    foreach (Instruction i in body.Instructions)
      if (i.Operand is Instruction && i.OpCode.OperandType == OperandType.ShortInlineBrTarget)
        i.OpCode = i.OpCode.ToLongOp();
    foreach (Instruction i in (Instruction[])[.. body.Instructions])
      if (i.OpCode == OpCodes.Ret || i.OpCode == OpCodes.Throw)
      {
        il.InsertAfter(i, il.Create(i.OpCode));
        il.InsertAfter(i, il.Create(OpCodes.Call, Profiler_End));
        il.InsertAfter(i, il.Create(OpCodes.Call, ref_getMeasure));
        il.InsertAfter(i, il.Create(OpCodes.Ldloc, localTime));

        i.OpCode = OpCodes.Ldc_I4;
        i.Operand = id;
      }
  }

  public static void PatchAssembly(AssemblyDefinition asm)
  {
    foreach (ModuleDefinition module in asm.Modules)
    {
      Profiler_End = module.ImportReference(typeof(Profiler).GetMethod(nameof(Profiler.End)));
      ref_getMeasure = Patcher.settings.profileMemory ? module.ImportReference(Profiler.GetMonoUsedSizeLong) : module.ImportReference(((Delegate)Stopwatch.GetTimestamp).Method);
      ref_long = module.ImportReference(typeof(long));
      asyncType = module.ImportReference(typeof(AsyncStateMachineAttribute));
      iteratorType = module.ImportReference(typeof(IteratorStateMachineAttribute));
      foreach (TypeDefinition type in module.Types)
        PatchType(type);
    }
  }

  public static readonly HashSet<string> namespacesToSkip = [
      "System", "On", "IL", "Microsoft", "Rewired", "MS", "Sony", "Stove", "ObjCRuntimeInternal",
      "XamMac", "Internal", "AssetBundles", "profiler" ],
    assembliesToSkip = [
      "System.Core.dll", "MonoMod.Utils.dll", "Mono.Cecil.dll", "Mono.Cecil.Mdb.dll", "Mono.Cecil.Pdb.dll",
      "Mono.Cecil.Rocks.dll", "MonoMod.RuntimeDetour.dll", "Mono.Security.dll", "System.Configuration.dll",
      "System.Xml.dll", "Rewired_Windows.dll", "Rewired_Core.dll" ];

  public static readonly Dictionary<string, HashSet<string>> skippedMethods = new() { // mainly for compatibility with mods
    { "ObjectsPage", ["Refresh"] },
    { "MoreSlugcats.SlugNPCAI", ["Move"] },
    { "Player", ["StomachGlowLightColor"] },
    { "PlayerGraphics", [".ctor"] },
    { "Spider", ["ConsiderPrey"] },
    { "AbstractCreatureAI", ["AbstractBehavior"] }, // entity pack
  };

  public static bool IsSkippedNamespace(string name)
  {
    if (name == null)
      return true;
    foreach (string skippedNamespace in namespacesToSkip)
      if (name.StartsWith(skippedNamespace))
        return true;
    return false;
  }

  public static void PatchType(TypeDefinition type)
  {
    if (IsSkippedNamespace(type.Namespace) || type.Name.StartsWith("<"))
      return;

    Profiler.Log(new string(' ', currentIndent) + type);
    currentIndent += 2;
    foreach (TypeDefinition nestedType in type.NestedTypes)
      PatchType(nestedType);
    currentIndent -= 2;

    foreach (MethodDefinition method in type.Methods)
    {
      if (!method.HasBody
        || Patcher.settings.conditionalProfileGlobal && method.Body.Instructions.Count <= 30
        || skippedMethods.TryGetValue(method.DeclaringType.FullName, out HashSet<string> methodNames) && methodNames.Contains(method.Name)
        || method.CustomAttributes.Any(a => a.AttributeType == asyncType || a.AttributeType == iteratorType))
        continue;

      Profiler.Log($"{new string(' ', currentIndent)}| {method.GetSimpleName()}");
      try
      {
        PatchMethod(method);
      }
      catch (Exception e)
      {
        Profiler.Log($"Failed to patch {method.GetSimpleName()}: {e}");
        ++Patcher.errorCounter;
      }

      ++Patcher.allPatchedMethodCounter;
      ++Patcher.patchedMethodCounter;
    }
  }
}