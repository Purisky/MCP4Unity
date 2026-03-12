using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace MCP4Unity.Editor
{
    [InitializeOnLoad]
    internal static class EditorMainThread
    {
        private static readonly int MainThreadId;
        private static readonly ConcurrentQueue<Action> WorkQueue = new();

        static EditorMainThread()
        {
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
            EditorApplication.update += DrainQueue;
        }

        private const int ScheduleTimeoutMs = 25000;

        public static Task<T> RunAsync<T>(Func<Task<T>> asyncFunc, CancellationToken ct = default)
        {
            if (Thread.CurrentThread.ManagedThreadId == MainThreadId)
                return asyncFunc();

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            var timeoutTimer = new Timer(_ =>
            {
                tcs.TrySetException(new TimeoutException(
                    $"EditorMainThread.RunAsync: work item was not drained within {ScheduleTimeoutMs}ms. " +
                    "The editor main thread may be blocked (modal dialog, domain reload, compilation)."));
            }, null, ScheduleTimeoutMs, Timeout.Infinite);

            WorkQueue.Enqueue(() =>
            {
                timeoutTimer.Dispose();

                if (ct.IsCancellationRequested)
                {
                    tcs.TrySetCanceled();
                    return;
                }

                try
                {
                    Task<T> task = asyncFunc();
                    task.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            tcs.TrySetException(t.Exception!.InnerExceptions);
                        else if (t.IsCanceled)
                            tcs.TrySetCanceled();
                        else
                            tcs.TrySetResult(t.Result);
                    }, TaskScheduler.Default);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            WakeEditor();
            return tcs.Task;
        }

        public static Task<T> Run<T>(Func<T> func, CancellationToken ct = default)
        {
            return RunAsync(() => Task.FromResult(func()), ct);
        }

        private static void DrainQueue()
        {
            // 编译时暂停 MCP 处理，避免性能冲突
            if (EditorApplication.isCompiling)
                return;

            if (WorkQueue.IsEmpty)
                return;

            while (WorkQueue.TryDequeue(out Action work))
            {
                try
                {
                    work();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }

            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

#if UNITY_EDITOR_WIN
        private const uint WM_NULL = 0x0000;
        private const int MCP_WAKE_TIMER_ID = 0x4D43;
        private const int WakeIntervalMs = 200;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // SetTimer on a foreign thread's window injects WM_TIMER into that thread's message pump,
        // forcing Unity to process messages even when the editor is unfocused/background.
        [DllImport("user32.dll", SetLastError = true)]
        private static extern UIntPtr SetTimer(IntPtr hWnd, UIntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool KillTimer(IntPtr hWnd, UIntPtr uIDEvent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        private static Timer _wakeTimer;
        private static readonly object _wakeTimerLock = new();

        private static void WakeEditor()
        {
            var hwnd = Process.GetCurrentProcess().MainWindowHandle;
            if (hwnd != IntPtr.Zero)
                PostMessage(hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            EnsureWakeTimerRunning();
        }

        private static void EnsureWakeTimerRunning()
        {
            lock (_wakeTimerLock)
            {
                if (_wakeTimer != null)
                    return;

                _wakeTimer = new Timer(WakeTimerTick, null, WakeIntervalMs, WakeIntervalMs);
            }
        }

        private static void WakeTimerTick(object _)
        {
            if (WorkQueue.IsEmpty)
            {
                lock (_wakeTimerLock)
                {
                    _wakeTimer?.Dispose();
                    _wakeTimer = null;
                }

                var h = Process.GetCurrentProcess().MainWindowHandle;
                if (h != IntPtr.Zero)
                    KillTimer(h, (UIntPtr)MCP_WAKE_TIMER_ID);
                return;
            }

            var hwnd = Process.GetCurrentProcess().MainWindowHandle;
            if (hwnd == IntPtr.Zero)
                return;

            SetTimer(hwnd, (UIntPtr)MCP_WAKE_TIMER_ID, (uint)WakeIntervalMs, IntPtr.Zero);
            InvalidateRect(hwnd, IntPtr.Zero, false);
            PostMessage(hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
        }
#else
        private static void WakeEditor() { }
#endif
    }
}
