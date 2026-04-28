//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.FaultInjection;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Unit tests that isolate the Stage 4 Dispatcher fix.
    ///
    /// Strategy: Option 2B (per Stage 3.6 spec). The Dispatcher has an
    /// internal constructor accepting <see cref="IConnection"/>, which we
    /// exploit via <c>InternalsVisibleTo("Microsoft.Azure.Cosmos.Tests")</c>.
    /// Reach <c>WaitTask</c> (sync) and <c>WaitTaskAsync</c> (async fix)
    /// via reflection (both are private).
    ///
    /// These tests do NOT touch RNTBD, network, timers, or the
    /// emulator. They drive a pending <see cref="TaskCompletionSource{TResult}"/>
    /// and measure thread-pool behavior directly.
    /// </summary>
    [TestClass]
    public class DispatcherIdleTimerFixTests
    {
        private static readonly MethodInfo WaitTaskAsyncMethod = typeof(Dispatcher)
            .GetMethod("WaitTaskAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo WaitTaskMethod = typeof(Dispatcher)
            .GetMethod("WaitTask", BindingFlags.Instance | BindingFlags.NonPublic);

        private static Dispatcher BuildDispatcher()
        {
            Mock<IConnection> mock = new Mock<IConnection>();
            mock.SetupGet(c => c.ConnectionCorrelationId).Returns(Guid.NewGuid());
            mock.SetupGet(c => c.ServerUri).Returns(new Uri("tcp://localhost:10000"));

            ConstructorInfo ctor = typeof(Dispatcher).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new Type[]
                {
                    typeof(Uri),
                    typeof(global::Microsoft.Azure.Documents.UserAgentContainer),
                    typeof(IConnectionStateListener),
                    typeof(TimerPool),
                    typeof(bool),
                    typeof(IChaosInterceptor),
                    typeof(IConnection),
                },
                modifiers: null);
            Assert.IsNotNull(ctor, "internal Dispatcher(IConnection) ctor not found");

            return (Dispatcher)ctor.Invoke(new object[]
            {
                new Uri("tcp://localhost:10000"),
                null, // UserAgentContainer
                null, // IConnectionStateListener
                null, // TimerPool
                false, // enableChannelMultiplexing
                null, // IChaosInterceptor
                mock.Object,
            });
        }

        private static Task InvokeWaitTaskAsync(Dispatcher d, Task t, string desc)
        {
            return (Task)WaitTaskAsyncMethod.Invoke(d, new object[] { t, desc });
        }

        private static void InvokeWaitTask(Dispatcher d, Task t, string desc)
        {
            WaitTaskMethod.Invoke(d, new object[] { t, desc });
        }

        private sealed class ProbeResult
        {
            public long MaxLatencyMs;
            public int SampleCount;
        }

        private static (ProbeResult result, CancellationTokenSource cts, Task probeTask) StartProbe()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            ProbeResult result = new ProbeResult();
            Task probeTask = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    long queuedTicks = Stopwatch.GetTimestamp();
                    TaskCompletionSource<long> tcs = new TaskCompletionSource<long>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        long ranTicks = Stopwatch.GetTimestamp();
                        long ms = (ranTicks - queuedTicks) * 1000 / Stopwatch.Frequency;
                        tcs.TrySetResult(ms);
                    });
                    Task<long> winner = tcs.Task;
                    Task delay = Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
                    Task completed = await Task.WhenAny(winner, delay).ConfigureAwait(false);
                    long latency;
                    if (completed == winner)
                    {
                        latency = await winner.ConfigureAwait(false);
                    }
                    else
                    {
                        // Probe itself was starved beyond 2s — record as 2000ms.
                        latency = 2000;
                    }
                    Interlocked.Increment(ref result.SampleCount);
                    long prev;
                    do
                    {
                        prev = Interlocked.Read(ref result.MaxLatencyMs);
                        if (latency <= prev) break;
                    }
                    while (Interlocked.CompareExchange(ref result.MaxLatencyMs, latency, prev) != prev);

                    try { await Task.Delay(50, cts.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { }
                }
            });
            return (result, cts, probeTask);
        }

        [TestMethod]
        [Timeout(30_000)]
        public async Task WaitTaskAsync_PendingTask_DoesNotBlockThreadPoolThread()
        {
            // Saturate the thread pool with a small min thread count to make
            // any blocking behavior observable within seconds.
            ThreadPool.GetMinThreads(out int origMinW, out int origMinIO);
            try
            {
                int procs = Environment.ProcessorCount;
                ThreadPool.SetMinThreads(Math.Max(2, procs / 2), procs);

                Dispatcher dispatcher = BuildDispatcher();
                TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                // Saturate the pool with many concurrent WaitTaskAsync calls,
                // each awaiting the same pending TCS. Each call should yield
                // its thread back to the pool at the await point.
                int callers = procs * 4;
                Task[] callerTasks = new Task[callers];
                for (int i = 0; i < callers; i++)
                {
                    callerTasks[i] = Task.Run(() => InvokeWaitTaskAsync(dispatcher, tcs.Task, "unit-test-async"));
                }

                (ProbeResult result, CancellationTokenSource cts, Task probeTask) = StartProbe();

                await Task.Delay(TimeSpan.FromSeconds(1.5)).ConfigureAwait(false);

                long maxWithPending = Interlocked.Read(ref result.MaxLatencyMs);
                int samplesWithPending = result.SampleCount;

                tcs.TrySetResult(true);

                Task allCallers = Task.WhenAll(callerTasks);
                Task winner = await Task.WhenAny(allCallers, Task.Delay(TimeSpan.FromSeconds(2)))
                    .ConfigureAwait(false);

                cts.Cancel();
                try { await probeTask.ConfigureAwait(false); } catch { }

                Assert.AreSame(allCallers, winner,
                    "WaitTaskAsync callers did not complete within 2 s after the receive task was completed.");
                Assert.IsTrue(samplesWithPending >= 10,
                    $"probe ran only {samplesWithPending} samples; too few to judge.");
                Assert.IsTrue(maxWithPending < 200,
                    $"probe max latency {maxWithPending} ms while WaitTaskAsync awaited a pending task. " +
                    $"async path blocked a thread pool thread.");
            }
            finally
            {
                ThreadPool.SetMinThreads(origMinW, origMinIO);
            }
        }

        [TestMethod]
        [Timeout(30_000)]
        public async Task WaitTaskSync_PendingTask_DoesBlockThreadPoolThread()
        {
            // Companion test: demonstrates that the SYNC WaitTask (pre-fix
            // behavior) would block the calling thread pool thread until
            // the pending task completes. This shows the fix changed
            // observable thread-pool behavior.
            ThreadPool.GetMinThreads(out int origMinW, out int origMinIO);
            try
            {
                int procs = Environment.ProcessorCount;
                ThreadPool.SetMinThreads(Math.Max(2, procs / 2), procs);

                Dispatcher dispatcher = BuildDispatcher();
                TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                int callers = procs * 4;
                Task[] callerTasks = new Task[callers];
                for (int i = 0; i < callers; i++)
                {
                    callerTasks[i] = Task.Run(() => InvokeWaitTask(dispatcher, tcs.Task, "unit-test-sync"));
                }

                (ProbeResult result, CancellationTokenSource cts, Task probeTask) = StartProbe();

                await Task.Delay(TimeSpan.FromSeconds(1.5)).ConfigureAwait(false);

                long maxWithPending = Interlocked.Read(ref result.MaxLatencyMs);
                int samplesWithPending = result.SampleCount;

                tcs.TrySetResult(true);
                Task allCallers = Task.WhenAll(callerTasks);
                await Task.WhenAny(allCallers, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);

                cts.Cancel();
                try { await probeTask.ConfigureAwait(false); } catch { }

                // The sync path pins each caller thread. With `callers` >
                // min-thread count, the probe should be starved —
                // accept either high latency or a small sample count.
                bool starvationObserved = maxWithPending >= 200 || samplesWithPending < 10;
                Assert.IsTrue(starvationObserved,
                    $"sync WaitTask did not produce observable thread-pool starvation: " +
                    $"probe max={maxWithPending} ms samples={samplesWithPending}. Test is broken.");
            }
            finally
            {
                ThreadPool.SetMinThreads(origMinW, origMinIO);
            }
        }
    }
}
