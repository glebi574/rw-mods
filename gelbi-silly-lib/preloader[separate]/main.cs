using Mono.Cecil;
using System;
using System.Collections.Generic;

namespace gelbi_silly_lib;

public static class Patcher
{
  public static IEnumerable<string> TargetDLLs
  {
    get
    {
      Console.WriteLine("gelbi-silly-lib-preloader ♥");
      System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(RuntimeDetourManagerInternal).TypeHandle);
      yield return "";
    }
  }

  public static void Patch(AssemblyDefinition asm)
  {

  }
}
