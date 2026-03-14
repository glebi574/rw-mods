using gelbi_silly_lib.Debugging;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.IO;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

public static class IssueResolver
{
  public static Exception latestException = null;

  internal static void ApplyHooks()
  {
    IL.RainWorld.HandleLog += RainWorld_HandleLog;
    On.RainWorld.Update += RainWorld_Update;
  }

  static void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
  {
    try
    {
      orig(self);
      TrackedIssue.UpdateNoIssues();
    }
    catch (Exception e)
    {
      latestException = e;
      TrackedIssue.Update(e);
      throw;
    }
  }

  static void RainWorld_HandleLog(ILContext il)
  {
    ILCursor c = new(il);

    if (c.TryGotoNext(i => i.OpCode == OpCodes.Ldstr))
      c.EmitDelegate(() =>
      {
        if (latestException == null)
          return;
        File.AppendAllText("exceptionLog.txt", DebuggerUtils.GetSimplifiedException(latestException) + "\n! <original> ");
        GSLLog.GLog($"{GSLLog.TimeLabel()} [unhandled exception] {latestException}\n");
      });
  }
}