using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using System;
using System.IO;

namespace gelbi_silly_lib;

public static class LogWrapper
{
  static bool ManualLogSourceImplemented = false;
  static Action<object>
    m_LogInfo = Console.WriteLine,
    m_LogMessage = Console.WriteLine,
    m_LogWarning = Console.WriteLine,
    m_LogError = Console.WriteLine,
    m_LogFatal = Console.WriteLine,
    m_LogDebug = Console.WriteLine;

  public static void SetImplementation(Action<object> i_LogInfo, Action<object> i_LogMessage, Action<object> i_LogWarning,
    Action<object> i_LogError, Action<object> i_LogFatal, Action<object> i_LogDebug, bool ManualLogSourceImplemented)
  {
    m_LogInfo = i_LogInfo;
    m_LogMessage = i_LogMessage;
    m_LogWarning = i_LogWarning;
    m_LogError = i_LogError;
    m_LogFatal = i_LogFatal;
    m_LogDebug = i_LogDebug;
    LogWrapper.ManualLogSourceImplemented = ManualLogSourceImplemented;
  }

  public static void LogInfo(object msg)
  {
    m_LogInfo(msg);
    GSLLog.GLog("[Info] " + msg);
  }

  public static void LogMessage(object msg)
  {
    m_LogMessage(msg);
    GSLLog.GLog("[Message] " + msg);
  }

  public static void LogWarning(object msg)
  {
    m_LogWarning(msg);
    GSLLog.GLog("[Warning] " + msg);
  }

  public static void LogError(object msg)
  {
    m_LogError(msg);
    if (!ManualLogSourceImplemented)
      GSLLog.GLog("[Error] " + msg);
  }

  public static void LogFatal(object msg)
  {
    m_LogFatal(msg);
    GSLLog.GLog("[Fatal] " + msg);
  }

  public static void LogDebug(object msg)
  {
    m_LogDebug(msg);
    GSLLog.GLog("[Debug] " + msg);
  }
}

/// <summary>
/// Logger for gslLog.txt
/// </summary>
public static class GSLLog
{
  public static readonly WriterThread writer;

  static GSLLog()
  {
    Directory.CreateDirectory("customLogs");
    writer = new("customLogs/gslLog.txt", false);
    GLog($"{Patcher.PLUGIN_NAME} {Patcher.PLUGIN_VERSION}");
    GLog(DateTime.Now.ToString("dd.MM.yy HH:mm:ss"));
    new Hook(typeof(ManualLogSource).GetMethod("LogError"), ManualLogSource_LogError);
  }

  public static string TimeLabel() => DateTime.Now.ToString("[HH:mm:ss]");

  public static void GLog(object msg = null) => writer.WriteLine(msg);

  public static void ManualLogSource_LogError(Action<ManualLogSource, object> orig, ManualLogSource self, object data)
  {
    orig(self, data);
    writer.WriteLine($"{TimeLabel()} [Error:\t{self.SourceName}] {data}");
  }
}