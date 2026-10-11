using BepInEx;
using BepInEx.Bootstrap;
using gelbi_silly_lib.Debugging;
using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.ReflectionUtils;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Text;

namespace gelbi_silly_lib;

public static class IssueResolver
{
  public static Exception latestException = null;

  internal static void ApplyHooks()
  {
    new Hook(typeof(UnityEngine.StackTraceUtility).GetMethod("ExtractStringFromExceptionInternal", BFlags.anyDeclaredStatic), StackTraceUtility_ExtractStringFromExceptionInternal);
    DetourUtils.newDetour(typeof(Chainloader).Assembly.GetType("BepInEx.Bootstrap.Chainloader+<>c").GetMethod("<Start>b__32_12", BFlags.anyDeclaredInstance), Chainloader_b__32_12); // dependency requires specific version
    On.RainWorld.Update += RainWorld_Update;
    On.RainWorldSteamManager.GetWorkshopDownloadProgress += RainWorldSteamManager_GetWorkshopDownloadProgress; // invalid update fix

    TrackedIssue.AddParser([typeof(AbstractWorldEntity), typeof(AbstractCreature), typeof(AbstractPhysicalObject)], obj =>
    {
      AbstractWorldEntity e = (AbstractWorldEntity)obj;
      StringBuilder sb = new StringBuilder().Append("room: ");
      if (e.world == null)
        sb.Append("\\ null world");
      else if (e.Room is not AbstractRoom room)
        sb.Append($"\\ invalid room at index {e.pos.room}");
      else
        sb.Append(room.name).Append(" <nodes: ").Append(room.nodes.Length).Append("; abstractNode: ").Append(e.pos.abstractNode).Append(">");
      return sb.ToString();
    });
    TrackedIssue.AddParser(typeof(Room), obj => $"room: {(((Room)obj)?.abstractRoom is AbstractRoom room ? room.name : "\\ null abstractRoom")}");
  }

  public static HashSet<Steamworks.PublishedFileId_t> invalidWorkshopItems = [];
  public static double RainWorldSteamManager_GetWorkshopDownloadProgress(On.RainWorldSteamManager.orig_GetWorkshopDownloadProgress orig, RainWorldSteamManager self)
  {
    for (int i = self.currentlyDownloading.Count - 1; i >= 0; --i)
      if ((Steamworks.SteamUGC.GetItemState(self.currentlyDownloading[i]) & 8) == 0)
      {
        invalidWorkshopItems.Add(self.currentlyDownloading[i]);
        self.currentlyDownloading.RemoveAt(i);
      }
    return orig(self);
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
      LogError($"Failed to process exception captured by UnityEngine: {ei}\nOriginally thrown exception: {e.Message}:\n{e.StackTrace}");
    }
  }

  static void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
  {
    orig(self);
    TrackedIssue.UpdateNoIssues();
  }

  static string Chainloader_b__32_12(object self, BepInDependency s)
  {
    Version v = s.MinimumVersion;
    if (v.Major == 0 && v.Minor == 0 && v.Build <= 0 && v.Revision <= 0)
      return s.DependencyGUID;
    string altName = PluginUtils.pluginMap.TryGetValue(s.DependencyGUID, out PluginData plugin) ? $"[{plugin.metadata.Name}]" : "";
    GSLLog.GLog($"! Missing newer version of {s.DependencyGUID}{altName}, some plugins require version {v} or newer and are disabled");
    Notification.AddTemporaryNotification(typeof(Plugin), "Dependency requires update", $"Code of some mod is disabled, as it requires newer version of one\nof its dependencies. Resubscribe to or update:\n{s.DependencyGUID}{altName} {plugin?.metadata.Version}->{v}");
    return $"{s.DependencyGUID} (v{v} or newer)";
  }
}