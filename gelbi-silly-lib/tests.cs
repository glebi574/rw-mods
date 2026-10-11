using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil;
using MonoMod.Utils;
using System;
using System.IO;
using System.Threading.Tasks;

namespace gelbi_silly_lib;

static internal class _Tests
{
  public static void Run()
  {
    try
    {
      //MemberNameParsers();
    }
    catch (Exception e)
    {
      GSLLog.GLog(e);
    }
  }

  public static void MemberNameParsers()
  {
    Parallel.ForEach(Directory.EnumerateFiles("RainWorld_Data\\Managed", "*.dll")/*["RainWorld_Data\\Managed\\System.Web.dll"]*/, assembly =>
    {
      if (Path.GetFileNameWithoutExtension(assembly) != "Unity.Burst")
        foreach (TypeDefinition type in AssemblyDefinition.ReadAssembly(assembly).MainModule.AllTypes)
          foreach (MethodDefinition method in type.Methods)
          {
            string definitionName = method.GetSimpleName(), methodName = method.ResolveReflection()?.GetSimpleName();
            if (methodName != null && definitionName != methodName)
              GSLLog.GLog($"? {definitionName} | {methodName}");
          }
    });
  }
}