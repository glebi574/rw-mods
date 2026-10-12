using gelbi_silly_lib;
using gelbi_silly_lib.ReflectionUtils;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace profiler_preloader;

public static class ThreadPatcher
{
  public static List<WeakReference> threads = [];
  public static FieldInfo threadMethod = typeof(Thread).GetField("m_Delegate", BFlags.anyDeclaredInstance),
    threadStart = typeof(Thread).Assembly.GetType("System.Threading.ThreadHelper").GetField("_start", BFlags.anyDeclaredInstance);
  public delegate void wawa_d(Thread self, ref int stackMark);
  public static void Thread_Start(wawa_d orig, Thread self, ref int stackMark)
  {
    threads.Add(new(self));
    orig(self, ref stackMark);
  }

  public static void Apply()
  {
    new Hook(typeof(Thread).GetMethod("Start", BFlags.anyDeclaredInstance, null, [typeof(Thread).Assembly.GetType("System.Threading.StackCrawlMark").MakeByRefType()], null), Thread_Start);
    new Thread(() =>
    {
      while (true)
      {
        Thread.Sleep(5000);
        threads.RemoveAll(e => !e.IsAlive || !(e.Target as Thread).IsAlive);
        if (threads.Count <= 0)
          continue;
        GSLLog.GLog("----------Threads---------");
        foreach (WeakReference thread in threads)
        {
          object m_Delegate = threadMethod.GetValue(thread.Target as Thread);
          if (m_Delegate == null)
          {
            GSLLog.GLog("T\\");
            continue;
          }
          if (m_Delegate is MulticastDelegate multicast)
          {
            GSLLog.GLog($"T: {(threadStart.GetValue(multicast.Target) as Delegate)?.Method.GetFullSimpleName()}");
            continue;
          }
          GSLLog.GLog($"T' {m_Delegate}");
        }
        GSLLog.GLog("--------------------------");
      }
    }).Start();
  }
}