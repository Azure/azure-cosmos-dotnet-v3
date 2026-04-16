// =============================================================================
// SDK Code-Based Repro for RNTBD Dispatcher Thread Pool Starvation (Issue #4393)
//
// This repro uses the ACTUAL code patterns from the Cosmos DB .NET SDK's RNTBD
// transport layer — specifically the Dispatcher, Channel, and TimerPool classes.
//
// The Dispatcher.OnIdleTimer callback is the root cause: it runs on a thread pool
// thread (via ContinueWith) and calls WaitTask(receiveTask) which does t.Wait(),
// blocking the thread until the receive task completes.
//
// When many connections go idle simultaneously, N idle timer callbacks fire,
// each consuming and blocking a thread pool thread, causing starvation.
//
// This repro faithfully reproduces the internal class structure and call chain:
//   TimerPool.OnTimer -> PooledTimer.FireTimeout -> ContinueWith(OnIdleTimer)
//     -> OnIdleTimer -> WaitTask -> t.Wait() [BLOCKS]
//
// Source files referenced (msdata/direct branch):
//   - Dispatcher.cs: OnIdleTimer (line 525), WaitTask (line 661), ScheduleIdleTimer (line 579)
//   - TimerPool.cs: OnTimer callback, PooledTimer.FireTimeout
//   - Channel.cs: Dispose -> dispatcher.Dispose -> WaitTask
//   - ChannelDictionary.cs: Dispose -> foreach channel.Close()
//
// Usage:
//   dotnet run -- before    # Simulates msdata/direct base branch (sync WaitTask)
//   dotnet run -- after     # Simulates fix branch (async WaitTaskAsync)
//   dotnet run -- both      # Runs both back-to-back (default)
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadPoolStarvationRepro
{
    /// <summary>
    /// Reproduces PooledTimer from Microsoft.Azure.Documents.PooledTimer.
    /// The real PooledTimer uses a TaskCompletionSource that completes when
    /// FireTimeout is called by the TimerPool's background Timer callback.
    /// </summary>
    class PooledTimer
    {
        private readonly TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int cancelled = 0;

        public TimeSpan Timeout { get; set; }

        public PooledTimer(int timeoutInSeconds)
        {
            this.Timeout = TimeSpan.FromSeconds(timeoutInSeconds);
        }

        public Task StartTimerAsync()
        {
            return this.tcs.Task;
        }

        public void FireTimeout()
        {
            this.tcs.TrySetResult(true);
        }

        public bool CancelTimer()
        {
            if (Interlocked.CompareExchange(ref this.cancelled, 1, 0) == 0)
            {
                this.tcs.TrySetCanceled();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Simplified TimerPool - in the real SDK, this fires periodically
    /// and calls FireTimeout on any PooledTimers that have expired.
    /// </summary>
    class SimulatedTimerPool
    {
        private readonly ConcurrentBag<PooledTimer> timers = new ConcurrentBag<PooledTimer>();

        public PooledTimer GetPooledTimer(int timeoutInSeconds)
        {
            var timer = new PooledTimer(timeoutInSeconds);
            this.timers.Add(timer);
            return timer;
        }

        public void FireAllTimers()
        {
            foreach (var timer in this.timers)
            {
                timer.FireTimeout();
            }
        }
    }

    /// <summary>
    /// Faithful reproduction of Microsoft.Azure.Documents.Rntbd.Dispatcher.
    /// Implements EXACT blocking pattern from base msdata/direct branch
    /// and EXACT async fix from PR #5722 branch.
    /// </summary>
    class SimulatedDispatcher : IDisposable, IAsyncDisposable
    {
        private readonly object connectionLock = new object();
        private readonly SimulatedTimerPool idleTimerPool;
        private readonly bool useAsyncPath;

        private Task receiveTask;
        private ManualResetEventSlim receiveGate;

        private PooledTimer idleTimer;
        private Task idleTimerTask;
        private CancellationTokenSource cancellation = new CancellationTokenSource();

        public int ThreadsBlocked;
        public int CallbacksStarted;
        public int CallbacksCompleted;

        private int disposed;
        public int Id { get; }

        public SimulatedDispatcher(int id, SimulatedTimerPool timerPool, bool useAsyncPath)
        {
            this.Id = id;
            this.idleTimerPool = timerPool;
            this.useAsyncPath = useAsyncPath;

            this.receiveGate = new ManualResetEventSlim(false);
            this.receiveTask = Task.Run(() =>
            {
                this.receiveGate.Wait(TimeSpan.FromSeconds(60));
            });
        }

        /// <summary>
        /// Mirrors Dispatcher.ScheduleIdleTimer (Dispatcher.cs line 579-592).
        /// </summary>
        public void ScheduleIdleTimer()
        {
            lock (this.connectionLock)
            {
                this.idleTimer = this.idleTimerPool.GetPooledTimer(1);

                if (this.useAsyncPath)
                {
                    this.idleTimerTask = this.idleTimer.StartTimerAsync()
                        .ContinueWith(this.OnIdleTimerAsync, TaskContinuationOptions.OnlyOnRanToCompletion)
                        .Unwrap();
                }
                else
                {
                    this.idleTimerTask = this.idleTimer.StartTimerAsync()
                        .ContinueWith(this.OnIdleTimer, TaskContinuationOptions.OnlyOnRanToCompletion);
                }

                this.idleTimerTask.ContinueWith(
                    failedTask =>
                    {
                        Console.Error.WriteLine(
                            "[Dispatcher " + this.Id + "] Idle timer callback failed: " + failedTask.Exception?.InnerException?.Message);
                    },
                    TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        /// <summary>
        /// EXACT reproduction of Dispatcher.OnIdleTimer (Dispatcher.cs lines 525-576).
        /// THE BUG: WaitTask -> t.Wait() blocks thread pool thread.
        /// </summary>
        private void OnIdleTimer(Task precedentTask)
        {
            Interlocked.Increment(ref this.CallbacksStarted);

            Task receiveTaskCopy = null;

            lock (this.connectionLock)
            {
                if (this.cancellation.IsCancellationRequested)
                {
                    return;
                }

                this.cancellation.Cancel();
                receiveTaskCopy = this.receiveTask;
                this.idleTimer = null;
                this.idleTimerTask = null;
            }

            Interlocked.Increment(ref this.ThreadsBlocked);
            this.WaitTask(receiveTaskCopy, "receive loop");
            Interlocked.Decrement(ref this.ThreadsBlocked);

            Interlocked.Increment(ref this.CallbacksCompleted);
        }

        /// <summary>
        /// EXACT reproduction of Dispatcher.OnIdleTimerAsync (PR #5722, Dispatcher.cs lines 567-618).
        /// THE FIX: WaitTaskAsync -> await t yields thread pool thread.
        /// </summary>
        private async Task OnIdleTimerAsync(Task precedentTask)
        {
            Interlocked.Increment(ref this.CallbacksStarted);

            Task receiveTaskCopy = null;

            lock (this.connectionLock)
            {
                if (this.cancellation.IsCancellationRequested)
                {
                    return;
                }

                this.cancellation.Cancel();
                receiveTaskCopy = this.receiveTask;
                this.idleTimer = null;
                this.idleTimerTask = null;
            }

            await this.WaitTaskAsync(receiveTaskCopy, "receive loop").ConfigureAwait(false);

            Interlocked.Increment(ref this.CallbacksCompleted);
        }

        /// <summary>
        /// EXACT reproduction of Dispatcher.WaitTask (Dispatcher.cs lines 661-682).
        /// t.Wait() blocks the calling thread.
        /// </summary>
        private void WaitTask(Task t, string description)
        {
            if (t == null) return;
            try
            {
                Debug.Assert(!Monitor.IsEntered(this.connectionLock));
                t.Wait();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("[Dispatcher " + this.Id + "] " + description + " failed: " + e.Message);
            }
        }

        /// <summary>
        /// EXACT reproduction of Dispatcher.WaitTaskAsync (PR #5722, Dispatcher.cs lines 732-752).
        /// await t yields the thread back to the pool.
        /// </summary>
        private async Task WaitTaskAsync(Task t, string description)
        {
            if (t == null) return;
            try
            {
                Debug.Assert(!Monitor.IsEntered(this.connectionLock));
                await t.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("[Dispatcher " + this.Id + "] " + description + " failed: " + e.Message);
            }
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref this.disposed, 1, 0) != 0) return;
            this.receiveGate.Set();
            this.receiveGate.Dispose();

            Task idleTimerTaskCopy = null;
            lock (this.connectionLock)
            {
                if (this.idleTimer != null)
                {
                    if (!this.idleTimer.CancelTimer())
                    {
                        idleTimerTaskCopy = this.idleTimerTask;
                    }
                }
            }
            this.WaitTask(idleTimerTaskCopy, "idle timer");
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref this.disposed, 1, 0) != 0) return;
            this.receiveGate.Set();
            this.receiveGate.Dispose();

            Task idleTimerTaskCopy = null;
            lock (this.connectionLock)
            {
                if (this.idleTimer != null)
                {
                    if (!this.idleTimer.CancelTimer())
                    {
                        idleTimerTaskCopy = this.idleTimerTask;
                    }
                }
            }
            await this.WaitTaskAsync(idleTimerTaskCopy, "idle timer").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Simulates ChannelDictionary holding N channels.
    /// </summary>
    class SimulatedChannelDictionary
    {
        private readonly List<SimulatedDispatcher> dispatchers = new List<SimulatedDispatcher>();
        private readonly SimulatedTimerPool timerPool;

        public SimulatedChannelDictionary(int channelCount, bool useAsyncPath)
        {
            this.timerPool = new SimulatedTimerPool();

            for (int i = 0; i < channelCount; i++)
            {
                var dispatcher = new SimulatedDispatcher(i, this.timerPool, useAsyncPath);
                dispatcher.ScheduleIdleTimer();
                this.dispatchers.Add(dispatcher);
            }
        }

        public IReadOnlyList<SimulatedDispatcher> Dispatchers => this.dispatchers;

        public void FireAllIdleTimers()
        {
            this.timerPool.FireAllTimers();
        }

        public void Dispose()
        {
            foreach (var d in this.dispatchers) d.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            var tasks = new List<Task>(this.dispatchers.Count);
            foreach (var d in this.dispatchers) tasks.Add(d.DisposeAsync().AsTask());
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    class Program
    {
        const int ConnectionCount = 200;
        const int ProbeTimeoutMs = 10_000;

        static async Task Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0].ToLower() : "both";

            Console.WriteLine("==========================================================================");
            Console.WriteLine(" RNTBD Dispatcher Thread Pool Starvation Repro (SDK Code-Based)");
            Console.WriteLine(" Issue: https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4393");
            Console.WriteLine(" PR:    https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5722");
            Console.WriteLine("==========================================================================");
            Console.WriteLine();
            Console.WriteLine("Environment: .NET " + Environment.Version + ", " + Environment.OSVersion.Platform);
            Console.WriteLine("Processor count: " + Environment.ProcessorCount);
            Console.WriteLine("Simulated RNTBD connections: " + ConnectionCount);
            Console.WriteLine();

            ThreadPool.GetMinThreads(out int origMinWorker, out int origMinIO);
            ThreadPool.SetMinThreads(Environment.ProcessorCount, origMinIO);

            if (mode == "before" || mode == "both")
            {
                await RunTest(useAsyncPath: false, label: "BEFORE FIX (msdata/direct base branch)");
                if (mode == "both")
                {
                    Console.WriteLine("\n--- Waiting 5s for thread pool recovery ---\n");
                    await Task.Delay(5000);
                }
            }

            if (mode == "after" || mode == "both")
            {
                await RunTest(useAsyncPath: true, label: "AFTER FIX (PR #5722 branch)");
            }

            ThreadPool.SetMinThreads(origMinWorker, origMinIO);
        }

        static async Task RunTest(bool useAsyncPath, string label)
        {
            Console.WriteLine("=== " + label + " ===");
            Console.WriteLine();

            int threadCountBefore = ThreadPool.ThreadCount;
            var sw = Stopwatch.StartNew();

            var channelDict = new SimulatedChannelDictionary(ConnectionCount, useAsyncPath);

            Console.WriteLine("Firing " + ConnectionCount + " idle timers simultaneously...");
            channelDict.FireAllIdleTimers();

            await Task.Delay(2000);

            int totalStarted = 0, totalCompleted = 0, totalBlocked = 0;
            foreach (var d in channelDict.Dispatchers)
            {
                totalStarted += d.CallbacksStarted;
                totalCompleted += d.CallbacksCompleted;
                totalBlocked += d.ThreadsBlocked;
            }

            long probeStartMs = sw.ElapsedMilliseconds;
            var probe = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ThreadPool.QueueUserWorkItem(_ => probe.TrySetResult(true));

            bool responsive = await Task.WhenAny(probe.Task, Task.Delay(ProbeTimeoutMs)) == probe.Task;
            long probeLatencyMs = sw.ElapsedMilliseconds - probeStartMs;
            int threadCountDuring = ThreadPool.ThreadCount;

            Console.WriteLine("  OnIdleTimer callbacks started:  " + totalStarted + "/" + ConnectionCount);
            Console.WriteLine("  OnIdleTimer callbacks completed:" + totalCompleted + "/" + ConnectionCount);
            Console.WriteLine("  Threads currently blocked:      " + totalBlocked);
            Console.WriteLine("  Thread pool threads:            " + threadCountBefore + " -> " + threadCountDuring);
            Console.WriteLine("  Thread pool spike:              +" + (threadCountDuring - threadCountBefore));
            Console.WriteLine("  Probe latency:                  " + probeLatencyMs + "ms");
            Console.WriteLine();

            if (!responsive)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  THREAD POOL STARVATION DETECTED");
                Console.WriteLine("  QueueUserWorkItem could not execute within 10 seconds.");
                Console.WriteLine("  Root cause: Dispatcher.OnIdleTimer -> WaitTask -> t.Wait()");
                Console.WriteLine("  Each callback blocks a thread pool thread indefinitely.");
                Console.WriteLine("  This matches the production dump from issue #4393.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  Thread pool remained responsive (probe latency: " + probeLatencyMs + "ms)");
                if (useAsyncPath)
                {
                    Console.WriteLine("  OnIdleTimerAsync yields threads via 'await' instead of blocking.");
                }
                Console.ResetColor();
            }
            Console.WriteLine();

            if (useAsyncPath)
            {
                await channelDict.DisposeAsync();
            }
            else
            {
                await Task.Run(() => channelDict.Dispose());
            }

            sw.Stop();
            Console.WriteLine("  Total time: " + sw.ElapsedMilliseconds + "ms");
            Console.WriteLine();
        }
    }
}
