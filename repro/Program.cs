// Standalone repro for RNTBD Dispatcher thread pool starvation (Issue #4393)
// 
// This program simulates the exact blocking pattern from OnIdleTimer:
//   ContinueWith(callback) where callback calls t.Wait()
//
// Usage:
//   dotnet run -- sync    # Simulates the OLD (base) code — demonstrates starvation
//   dotnet run -- async   # Simulates the NEW (fix) code — shows no starvation
//   dotnet run -- both    # Runs both back-to-back (default)
//
// Expected results:
//   sync  → Thread pool probe FAILS (starvation) or takes many seconds
//   async → Thread pool probe succeeds instantly

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    const int ConnectionCount = 200;

    static async Task Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0].ToLower() : "both";

        // Constrain thread pool to make starvation visible faster
        ThreadPool.GetMinThreads(out int origMinWorker, out int origMinIO);
        ThreadPool.SetMinThreads(Environment.ProcessorCount, origMinIO);

        if (mode == "sync" || mode == "both")
        {
            Console.WriteLine("=== SYNC MODE (simulates base msdata/direct branch) ===");
            Console.WriteLine($"Connections: {ConnectionCount}");
            Console.WriteLine($"Thread pool min threads: {Environment.ProcessorCount}");
            Console.WriteLine();
            await RunTest(useSyncWait: true);
            Console.WriteLine();

            // Let thread pool recover before next test
            if (mode == "both")
            {
                Console.WriteLine("--- Waiting for thread pool recovery ---");
                await Task.Delay(3000);
                Console.WriteLine();
            }
        }

        if (mode == "async" || mode == "both")
        {
            Console.WriteLine("=== ASYNC MODE (simulates fix branch) ===");
            Console.WriteLine($"Connections: {ConnectionCount}");
            Console.WriteLine($"Thread pool min threads: {Environment.ProcessorCount}");
            Console.WriteLine();
            await RunTest(useSyncWait: false);
        }

        ThreadPool.SetMinThreads(origMinWorker, origMinIO);
    }

    static async Task RunTest(bool useSyncWait)
    {
        int threadCountBefore = ThreadPool.ThreadCount;
        int peakThreadCount = threadCountBefore;
        int callbacksStarted = 0;
        int callbacksCompleted = 0;

        // Simulate N receive tasks (the background receive loops waiting for network I/O)
        var receiveGates = new List<ManualResetEventSlim>(ConnectionCount);
        var receiveTasks = new List<Task>(ConnectionCount);
        for (int i = 0; i < ConnectionCount; i++)
        {
            var gate = new ManualResetEventSlim(false);
            receiveGates.Add(gate);
            receiveTasks.Add(Task.Run(() =>
            {
                gate.Wait(TimeSpan.FromSeconds(30));
            }));
        }

        // Simulate idle timers firing simultaneously — this is the core of the bug.
        // Each ContinueWith callback runs on a thread pool thread and needs to wait
        // for its receive task to complete.
        var timerTasks = new List<Task>(ConnectionCount);
        Stopwatch sw = Stopwatch.StartNew();

        for (int i = 0; i < ConnectionCount; i++)
        {
            int index = i;
            Task receiveTask = receiveTasks[index];

            Task timerTask;
            if (useSyncWait)
            {
                // BASE BRANCH: OnIdleTimer calls WaitTask → t.Wait()
                // This BLOCKS the thread pool thread until receiveTask completes
                timerTask = Task.Run(() =>
                {
                    Interlocked.Increment(ref callbacksStarted);
                    TrackPeakThreads(ref peakThreadCount);
                    receiveTask.Wait(); // ← THE BUG: blocks thread pool thread
                    Interlocked.Increment(ref callbacksCompleted);
                });
            }
            else
            {
                // FIX BRANCH: OnIdleTimerAsync calls WaitTaskAsync → await t
                // This YIELDS the thread pool thread back to the pool
                timerTask = Task.Run(async () =>
                {
                    Interlocked.Increment(ref callbacksStarted);
                    TrackPeakThreads(ref peakThreadCount);
                    await receiveTask; // ← THE FIX: yields thread pool thread
                    Interlocked.Increment(ref callbacksCompleted);
                });
            }
            timerTasks.Add(timerTask);
        }

        // Let callbacks start executing
        await Task.Delay(2000);

        long probeStartMs = sw.ElapsedMilliseconds;

        // THREAD POOL PROBE: Queue a trivial work item — if the pool is starved,
        // this won't execute within the timeout
        var probe = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ThreadPool.QueueUserWorkItem(_ => probe.TrySetResult(true));

        bool responsive = await Task.WhenAny(probe.Task, Task.Delay(10_000)) == probe.Task;
        long probeLatencyMs = sw.ElapsedMilliseconds - probeStartMs;
        int threadCountDuring = ThreadPool.ThreadCount;

        // Results
        Console.WriteLine($"Callbacks started:     {callbacksStarted}/{ConnectionCount}");
        Console.WriteLine($"Callbacks completed:   {callbacksCompleted}/{ConnectionCount}");
        Console.WriteLine($"Thread pool threads:   {threadCountBefore} → {threadCountDuring} (peak: {peakThreadCount})");
        Console.WriteLine($"Thread spike:          +{peakThreadCount - threadCountBefore}");
        Console.WriteLine($"Probe latency:         {probeLatencyMs}ms");

        if (!responsive)
        {
            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  ❌ THREAD POOL STARVATION DETECTED                         ║");
            Console.WriteLine("║  QueueUserWorkItem could not execute within 10 seconds.     ║");
            Console.WriteLine("║  This confirms the bug from issue #4393.                    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine($"✅ Thread pool remained responsive (probe latency: {probeLatencyMs}ms)");
        }

        // Cleanup — release all gates so tasks complete
        foreach (var gate in receiveGates) gate.Set();
        await Task.WhenAll(timerTasks);
        sw.Stop();

        Console.WriteLine($"Total time:            {sw.ElapsedMilliseconds}ms");
        foreach (var gate in receiveGates) gate.Dispose();
    }

    static void TrackPeakThreads(ref int peakThreadCount)
    {
        int current = ThreadPool.ThreadCount;
        int peak = Volatile.Read(ref peakThreadCount);
        while (current > peak)
        {
            int prev = Interlocked.CompareExchange(ref peakThreadCount, current, peak);
            if (prev == peak) break;
            peak = prev;
        }
    }
}
