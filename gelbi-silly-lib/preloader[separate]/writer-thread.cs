using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

/// <summary>
/// Simple thread, that writes to file
/// </summary>
public class WriterThread
{
  public static readonly List<WriterThread> threads = [];
  public readonly ConcurrentQueue<(object msg, bool newLine)> queue = new();
  public readonly AutoResetEvent signal = new(false);
  public Thread thread;
  public bool running = true;

  static WriterThread()
  {
    AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
  }

  static void CurrentDomain_ProcessExit(object sender, EventArgs e)
  {
    foreach (WriterThread thread in threads)
      thread.Close();
  }

  /// <summary>
  /// Constructs and starts writer thread
  /// </summary>
  /// <param name="path">Path of file to write to</param>
  /// <param name="isBackground">If <c>false</c>, process won't finish until thread is closed or finishes writing</param>
  public WriterThread(string path, bool isBackground = true)
  {
    try
    {
      (thread = new(() =>
      {
        StreamWriter writer = new(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        while (running)
        {
          signal.WaitOne();
          while (queue.TryDequeue(out (object msg, bool newLine) msg))
            if (msg.newLine)
              writer.WriteLine(msg.msg);
            else
              writer.Write(msg.msg);
          writer.Flush();
        }
        writer.Close();
      })
      { IsBackground = isBackground }).Start();
      threads.Add(this);
    }
    catch (Exception e)
    {
      LogError($"Failed to start WriterThread for path \"{path}\": {e}");
    }
  }

  /// <summary>
  /// Closes thread. Thread will process enqueued messages before closing
  /// </summary>
  public void Close()
  {
    running = false;
    signal.Set();
  }

  public void Write(object msg = null)
  {
    queue.Enqueue((msg?.ToString(), false));
    signal.Set();
  }

  public void WriteLine(object msg = null)
  {
    queue.Enqueue((msg?.ToString(), true));
    signal.Set();
  }
}