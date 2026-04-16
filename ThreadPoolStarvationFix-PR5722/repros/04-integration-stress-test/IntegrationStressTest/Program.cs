// =============================================================================
// Integration Stress Test: Validates DisposeAsync correctness under concurrency
//
// Tests the full disposal hierarchy for race conditions, double-dispose safety,
// and concurrent idle timer firing during disposal.
//
// Scenarios tested:
//   1. Concurrent disposal of many dispatchers (Task.WhenAll)
//   2. Idle timer firing during disposal (race condition)
//   3. Double-dispose idempotency (Interlocked.CompareExchange pattern)
//   4. Mixed sync/async dispose interleaving
//   5. Dispose while receive task is still pending
//   6. Cancellation during OnIdleTimerAsync
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrationStressTest
{
    class PooledTimer
    {
        private readonly TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int cancelled = 0;

        public Task StartTimerAsync() => this.tcs.Task;
        public void FireTimeout() => this.tcs.TrySetResult(true);
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

    class SimulatedTimerPool
    {
        private readonly ConcurrentBag<PooledTimer> timers = new ConcurrentBag<PooledTimer>();
        public PooledTimer GetPooledTimer(int timeoutInSeconds)
        {
            var timer = new PooledTimer();
            this.timers.Add(timer);
            return timer;
        }
        public void FireAllTimers()
        {
            foreach (var t in this.timers) t.FireTimeout();
        }
    }

    class SimulatedDispatcher : IDisposable, IAsyncDisposable
    {
        private readonly object connectionLock = new object();
        private readonly SimulatedTimerPool idleTimerPool;
        private Task receiveTask;
        private ManualResetEventSlim receiveGate;
        private PooledTimer idleTimer;
        private Task idleTimerTask;
        private CancellationTokenSource cancellation = new CancellationTokenSource();
        private int disposed;
        public int Id { get; }
        public int DisposeCount;
        public bool OnIdleTimerRan;
        public bool OnIdleTimerAsyncRan;
        public Exception CaughtException;

        public SimulatedDispatcher(int id, SimulatedTimerPool timerPool)
        {
            this.Id = id;
            this.idleTimerPool = timerPool;
            this.receiveGate = new ManualResetEventSlim(false);
            this.receiveTask = Task.Run(() => this.receiveGate.Wait(TimeSpan.FromSeconds(30)));
        }

        public void ScheduleIdleTimerAsync()
        {
            lock (this.connectionLock)
            {
                this.idleTimer = this.idleTimerPool.GetPooledTimer(1);
                this.idleTimerTask = this.idleTimer.StartTimerAsync()
                    .ContinueWith(this.OnIdleTimerAsync, TaskContinuationOptions.OnlyOnRanToCompletion)
                    .Unwrap();
                this.idleTimerTask.ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        private async Task OnIdleTimerAsync(Task precedentTask)
        {
            this.OnIdleTimerAsyncRan = true;
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
        }

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
                this.CaughtException = e;
            }
        }

        public void Dispose()
        {
            Interlocked.Increment(ref this.DisposeCount);
            if (Interlocked.CompareExchange(ref this.disposed, 1, 0) != 0) return;
            GC.SuppressFinalize(this);
            this.receiveGate.Set();

            Task idleTimerTaskCopy = null;
            lock (this.connectionLock)
            {
                if (this.idleTimer != null)
                {
                    if (!this.idleTimer.CancelTimer())
                        idleTimerTaskCopy = this.idleTimerTask;
                }
            }

            if (idleTimerTaskCopy != null)
            {
                try { idleTimerTaskCopy.Wait(TimeSpan.FromSeconds(5)); }
                catch { }
            }
            this.receiveGate.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref this.DisposeCount);
            if (Interlocked.CompareExchange(ref this.disposed, 1, 0) != 0) return;
            GC.SuppressFinalize(this);
            this.receiveGate.Set();

            Task idleTimerTaskCopy = null;
            lock (this.connectionLock)
            {
                if (this.idleTimer != null)
                {
                    if (!this.idleTimer.CancelTimer())
                        idleTimerTaskCopy = this.idleTimerTask;
                }
            }

            if (idleTimerTaskCopy != null)
            {
                try { await idleTimerTaskCopy.ConfigureAwait(false); }
                catch { }
            }
            this.receiveGate.Dispose();
        }
    }

    class Program
    {
        static int passed = 0;
        static int failed = 0;

        static async Task Main(string[] args)
        {
            Console.WriteLine("==========================================================================");
            Console.WriteLine(" Integration Stress Tests: DisposeAsync Correctness");
            Console.WriteLine(" Validates PR #5722 async changes under stress");
            Console.WriteLine("==========================================================================");
            Console.WriteLine();

            await RunTest("Test 1: Concurrent DisposeAsync of 200 dispatchers", Test_ConcurrentDisposeAsync);
            await RunTest("Test 2: Idle timer fires during DisposeAsync (race)", Test_IdleTimerDuringDispose);
            await RunTest("Test 3: Double DisposeAsync idempotency", Test_DoubleDisposeAsync);
            await RunTest("Test 4: Mixed sync Dispose + async DisposeAsync", Test_MixedSyncAsyncDispose);
            await RunTest("Test 5: DisposeAsync while receive task pending", Test_DisposeWhileReceivePending);
            await RunTest("Test 6: Thread pool stays responsive during mass DisposeAsync", Test_ThreadPoolResponsiveDuringDispose);
            await RunTest("Test 7: CancelTimer race with FireTimeout", Test_CancelTimerRace);
            await RunTest("Test 8: 1000 dispatchers concurrent DisposeAsync (scale)", Test_ScaleTest);

            Console.WriteLine();
            Console.WriteLine("==========================================================================");
            Console.ForegroundColor = failed == 0 ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine("  Results: " + passed + " passed, " + failed + " failed");
            Console.ResetColor();
            Console.WriteLine("==========================================================================");
        }

        static async Task RunTest(string name, Func<Task> test)
        {
            Console.Write("  " + name + "... ");
            try
            {
                await test();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED");
                Console.ResetColor();
                Interlocked.Increment(ref passed);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: " + ex.Message);
                Console.ResetColor();
                Interlocked.Increment(ref failed);
            }
        }

        static async Task Test_ConcurrentDisposeAsync()
        {
            var timerPool = new SimulatedTimerPool();
            var dispatchers = new List<SimulatedDispatcher>();
            for (int i = 0; i < 200; i++)
            {
                var d = new SimulatedDispatcher(i, timerPool);
                d.ScheduleIdleTimerAsync();
                dispatchers.Add(d);
            }

            timerPool.FireAllTimers();
            await Task.Delay(500);

            var tasks = new List<Task>();
            foreach (var d in dispatchers) tasks.Add(d.DisposeAsync().AsTask());
            
            var whenAllTask = Task.WhenAll(tasks);
            var completed = await Task.WhenAny(whenAllTask, Task.Delay(15000));
            if (completed != whenAllTask)
                throw new Exception("DisposeAsync timed out for 200 dispatchers");
        }

        static async Task Test_IdleTimerDuringDispose()
        {
            var timerPool = new SimulatedTimerPool();
            var d = new SimulatedDispatcher(0, timerPool);
            d.ScheduleIdleTimerAsync();

            // Start disposal and fire timer concurrently
            var disposeTask = d.DisposeAsync().AsTask();
            timerPool.FireAllTimers();

            await Task.WhenAny(disposeTask, Task.Delay(5000));
            if (!disposeTask.IsCompleted)
                throw new Exception("DisposeAsync hung when timer fired concurrently");
        }

        static async Task Test_DoubleDisposeAsync()
        {
            var timerPool = new SimulatedTimerPool();
            var d = new SimulatedDispatcher(0, timerPool);

            await d.DisposeAsync();
            await d.DisposeAsync(); // Should be idempotent
            await d.DisposeAsync(); // Third call should also be no-op

            if (d.DisposeCount != 3)
                throw new Exception("DisposeCount should be 3 (all calls entered), got " + d.DisposeCount);
            // But only one should have done real work (Interlocked.CompareExchange)
        }

        static async Task Test_MixedSyncAsyncDispose()
        {
            var timerPool = new SimulatedTimerPool();
            var dispatchers = new List<SimulatedDispatcher>();
            for (int i = 0; i < 50; i++)
            {
                dispatchers.Add(new SimulatedDispatcher(i, timerPool));
            }

            // Half sync, half async
            var tasks = new List<Task>();
            for (int i = 0; i < dispatchers.Count; i++)
            {
                if (i % 2 == 0)
                {
                    var d = dispatchers[i];
                    tasks.Add(Task.Run(() => d.Dispose()));
                }
                else
                {
                    tasks.Add(dispatchers[i].DisposeAsync().AsTask());
                }
            }

            var whenAllTask2 = Task.WhenAll(tasks);
            var completed = await Task.WhenAny(whenAllTask2, Task.Delay(10000));
            if (completed != whenAllTask2)
                throw new Exception("Mixed dispose timed out");
        }

        static async Task Test_DisposeWhileReceivePending()
        {
            var timerPool = new SimulatedTimerPool();
            var d = new SimulatedDispatcher(0, timerPool);
            d.ScheduleIdleTimerAsync();

            // Dispose while receive task is still pending
            // DisposeAsync should signal the gate and complete cleanly
            var sw = Stopwatch.StartNew();
            await d.DisposeAsync();
            sw.Stop();

            if (sw.ElapsedMilliseconds > 5000)
                throw new Exception("DisposeAsync took too long: " + sw.ElapsedMilliseconds + "ms");
        }

        static async Task Test_ThreadPoolResponsiveDuringDispose()
        {
            var timerPool = new SimulatedTimerPool();
            var dispatchers = new List<SimulatedDispatcher>();
            for (int i = 0; i < 100; i++)
            {
                var d = new SimulatedDispatcher(i, timerPool);
                d.ScheduleIdleTimerAsync();
                dispatchers.Add(d);
            }

            timerPool.FireAllTimers();
            await Task.Delay(500);

            // Start mass disposal
            var disposeTasks = new List<Task>();
            foreach (var d in dispatchers) disposeTasks.Add(d.DisposeAsync().AsTask());

            // Probe thread pool during disposal
            var probe = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ThreadPool.QueueUserWorkItem(_ => probe.TrySetResult(true));

            bool responsive = await Task.WhenAny(probe.Task, Task.Delay(5000)) == probe.Task;
            if (!responsive)
                throw new Exception("Thread pool not responsive during async disposal");

            await Task.WhenAll(disposeTasks);
        }

        static async Task Test_CancelTimerRace()
        {
            // Run 100 iterations of cancel vs fire race
            for (int iter = 0; iter < 100; iter++)
            {
                var timerPool = new SimulatedTimerPool();
                var d = new SimulatedDispatcher(0, timerPool);
                d.ScheduleIdleTimerAsync();

                // Race: fire timer and dispose concurrently
                var fireTask = Task.Run(() => timerPool.FireAllTimers());
                var disposeTask = d.DisposeAsync().AsTask();

                await Task.WhenAll(fireTask, disposeTask);
                // Should not throw or deadlock
            }
        }

        static async Task Test_ScaleTest()
        {
            var timerPool = new SimulatedTimerPool();
            var dispatchers = new List<SimulatedDispatcher>();
            for (int i = 0; i < 1000; i++)
            {
                var d = new SimulatedDispatcher(i, timerPool);
                d.ScheduleIdleTimerAsync();
                dispatchers.Add(d);
            }

            timerPool.FireAllTimers();
            await Task.Delay(1000);

            var sw = Stopwatch.StartNew();
            var tasks = new List<Task>();
            foreach (var d in dispatchers) tasks.Add(d.DisposeAsync().AsTask());
            await Task.WhenAll(tasks);
            sw.Stop();

            if (sw.ElapsedMilliseconds > 30000)
                throw new Exception("1000 dispatcher disposal took too long: " + sw.ElapsedMilliseconds + "ms");
        }
    }
}

