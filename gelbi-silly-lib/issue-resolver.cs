using gelbi_silly_lib.Debugging;
using gelbi_silly_lib.ReflectionUtils;
using MonoMod.RuntimeDetour;
using System;

namespace gelbi_silly_lib;

public static class IssueResolver
{
  public static Exception latestException = null;

  internal static void ApplyHooks()
  {
    new Hook(typeof(UnityEngine.StackTraceUtility).GetMethod("ExtractStringFromExceptionInternal", BFlags.anyDeclaredStatic), StackTraceUtility_ExtractStringFromExceptionInternal);
    On.RainWorld.Update += RainWorld_Update;
  }

  public delegate void ExtractStringFromExceptionInternal_d(object exceptiono, out string message, out string stackTrace);
  static void StackTraceUtility_ExtractStringFromExceptionInternal(ExtractStringFromExceptionInternal_d orig, object exceptiono, out string message, out string stackTrace)
  {
    orig(exceptiono, out message, out stackTrace);
    if (exceptiono is not Exception e)
      return;
    string originalMessage = message, originalStackTrace = stackTrace;
    try
    {
      stackTrace = $"\n! <original> {message}\n{stackTrace}";
      message = DebuggerUtils.GetSimplifiedException(e);
      TrackedIssue.Update(e, "[unhandled exception]");
    }
    catch (Exception ei)
    {
      message = originalMessage;
      stackTrace = originalStackTrace;
      LogError($"Failed to process exception captured by UnityEngine: {ei}");
    }
  }

  static void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
  {
    orig(self);
    TrackedIssue.UpdateNoIssues();
  }
}