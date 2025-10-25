using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

public static class LogWrapper
{
  static Action<object> m_LogInfo = Console.WriteLine,
    m_LogMessage = Console.WriteLine,
    m_LogWarning = Console.WriteLine,
    m_LogError = Console.WriteLine,
    m_LogFatal = Console.WriteLine,
    m_LogDebug = Console.WriteLine;

  public static void SetImplementation(Action<object> i_LogInfo, Action<object> i_LogMessage, Action<object> i_LogWarning, Action<object> i_LogError, Action<object> i_LogFatal, Action<object> i_LogDebug)
  {
    m_LogInfo = i_LogInfo;
    m_LogMessage = i_LogMessage;
    m_LogWarning = i_LogWarning;
    m_LogDebug = i_LogDebug;
    m_LogFatal = i_LogFatal;
    m_LogDebug = i_LogDebug;
  }

  public static void LogInfo(object msg)
  {
    m_LogInfo(msg);
  }

  public static void LogMessage(object msg)
  {
    m_LogMessage(msg);
  }

  public static void LogWarning(object msg)
  {
    m_LogWarning(msg);
  }

  public static void LogError(object msg)
  {
    m_LogError(msg);
  }

  public static void LogFatal(object msg)
  {
    m_LogFatal(msg);
  }

  public static void LogDebug(object msg)
  {
    m_LogDebug(msg);
  }
}

public static class Patcher
{
  public static IEnumerable<string> TargetDLLs
  {
    get
    {
      LogInfo("gelbi-silly-lib-preloader ♥");
      typeof(RuntimeDetourManager).RunClassConstructor();
      typeof(SavedDataManager).RunClassConstructor();
      yield return "";
    }
  }

  public static void Patch(AssemblyDefinition asm)
  {

  }
}
