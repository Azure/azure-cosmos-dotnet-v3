//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Performance benchmarks for the RNTBD Dispatcher thread starvation fix.
    /// Measures thread pool utilization and disposal throughput to validate
    /// that the async changes do not introduce performance regressions.
    /// Related to https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4393
    /// </summary>
    [TestClass]
    public class DispatcherPerformanceBenchmarks
    {
        /// <summary>
        /// Benchmarks concurrent DisposeAsync throughput for N dispatchers.
        /// Measures:
        /// - Total wall-clock time for concurrent disposal
        /// - Thread pool thread count stability
        /// - Thread pool responsiveness during disposal
        /// </summary>
        [TestMethod]
        [Timeout(30_000)]
        public async Task Benchmark_ConcurrentDisposeAsync_Throughput()
        {
            int[] dispatcherCounts = { 10, 50, 100, 200 };

            Console.WriteLine("=== Concurrent DisposeAsync Throughput Benchmark ===");
            Console.WriteLine($"{"Count",-8} {"Time (ms)",-12} {"Avg (ms)",-12} {"TP Threads",-12} {"TP Responsive",-15}");
            Console.WriteLine(new string('-', 60));

            foreach (int count in dispatcherCounts)
            {
                using TimerPool idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);
                List<Dispatcher> dispatchers = new List<Dispatcher>(count);

                for (int i = 0; i < count; i++)
                {
                    Mock<IConnection> mockConnection = CreateMockConnection(
                        new Uri($"rntbd://localhost:{10000 + i}/"));

                    dispatchers.Add(new Dispatcher(
                        serverUri: new Uri($"rntbd://localhost:{10000 + i}/"),
                        userAgent: new UserAgentContainer(),
                        connectionStateListener: null,
                        idleTimerPool: idleTimerPool,
                        enableChannelMultiplexing: true,
                        chaosInterceptor: null,
                        connection: mockConnection.Object));
                }

                int threadCountBefore = ThreadPool.ThreadCount;

                Stopwatch sw = Stopwatch.StartNew();
                List<Task> disposeTasks = new List<Task>(count);
                foreach (Dispatcher d in dispatchers)
                {
                    disposeTasks.Add(d.DisposeAsync().AsTask());
                }
                await Task.WhenAll(disposeTasks);
                sw.Stop();

                int threadCountAfter = ThreadPool.ThreadCount;

                // Verify thread pool responsiveness
                TaskCompletionSource<bool> probe = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                ThreadPool.QueueUserWorkItem(_ => probe.TrySetResult(true));
                bool responsive = await Task.WhenAny(probe.Task, Task.Delay(2000)) == probe.Task;

                Console.WriteLine($"{count,-8} {sw.ElapsedMilliseconds,-12} {(double)sw.ElapsedMilliseconds / count,-12:F2} {threadCountAfter,-12} {responsive,-15}");

                Assert.IsTrue(responsive,
                    $"Thread pool not responsive after disposing {count} dispatchers.");
                Assert.IsTrue(sw.ElapsedMilliseconds < 10000,
                    $"Disposing {count} dispatchers took {sw.ElapsedMilliseconds}ms — too slow.");
            }
        }

        /// <summary>
        /// Benchmarks sync Dispose vs async DisposeAsync to verify no
        /// significant performance regression from the async state machine overhead.
        /// </summary>
        [TestMethod]
        [Timeout(30_000)]
        public async Task Benchmark_SyncVsAsync_Dispose()
        {
            const int count = 100;
            const int iterations = 3;

            Console.WriteLine("=== Sync vs Async Dispose Benchmark ===");
            Console.WriteLine($"{"Method",-15} {"Iteration",-12} {"Time (ms)",-12} {"Avg/item (µs)",-15}");
            Console.WriteLine(new string('-', 55));

            for (int iter = 0; iter < iterations; iter++)
            {
                // Sync Dispose
                {
                    using TimerPool idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);
                    List<Dispatcher> dispatchers = CreateDispatchers(count, idleTimerPool);

                    Stopwatch sw = Stopwatch.StartNew();
                    foreach (Dispatcher d in dispatchers)
                    {
                        d.Dispose();
                    }
                    sw.Stop();
                    Console.WriteLine($"{"Sync",-15} {iter + 1,-12} {sw.ElapsedMilliseconds,-12} {(double)sw.ElapsedTicks / count / (Stopwatch.Frequency / 1_000_000),-15:F1}");
                }

                // Async DisposeAsync
                {
                    using TimerPool idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);
                    List<Dispatcher> dispatchers = CreateDispatchers(count, idleTimerPool);

                    Stopwatch sw = Stopwatch.StartNew();
                    List<Task> tasks = new List<Task>(count);
                    foreach (Dispatcher d in dispatchers)
                    {
                        tasks.Add(d.DisposeAsync().AsTask());
                    }
                    await Task.WhenAll(tasks);
                    sw.Stop();
                    Console.WriteLine($"{"Async",-15} {iter + 1,-12} {sw.ElapsedMilliseconds,-12} {(double)sw.ElapsedTicks / count / (Stopwatch.Frequency / 1_000_000),-15:F1}");
                }
            }
        }

        /// <summary>
        /// Benchmarks thread pool thread count stability during mass disposal.
        /// The async fix should NOT cause thread count spikes (which would indicate
        /// that DisposeAsync is scheduling excessive work items).
        /// </summary>
        [TestMethod]
        [Timeout(15_000)]
        public async Task Benchmark_ThreadPoolStability_DuringMassDisposal()
        {
            const int count = 200;

            Console.WriteLine("=== Thread Pool Stability During Mass Disposal ===");

            using TimerPool idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);
            List<Dispatcher> dispatchers = CreateDispatchers(count, idleTimerPool);

            ThreadPool.GetMinThreads(out int minWorker, out int minIO);
            ThreadPool.GetMaxThreads(out int maxWorker, out int maxIO);

            int threadCountBefore = ThreadPool.ThreadCount;
            int peakThreadCount = threadCountBefore;
            int pendingBefore = (int)ThreadPool.PendingWorkItemCount;

            // Monitor thread count during disposal
            CancellationTokenSource monitorCts = new CancellationTokenSource();
            Task monitorTask = Task.Run(async () =>
            {
                while (!monitorCts.IsCancellationRequested)
                {
                    int current = ThreadPool.ThreadCount;
                    int peak = Volatile.Read(ref peakThreadCount);
                    if (current > peak)
                    {
                        Interlocked.CompareExchange(ref peakThreadCount, current, peak);
                    }
                    await Task.Delay(10);
                }
            });

            Stopwatch sw = Stopwatch.StartNew();
            List<Task> disposeTasks = new List<Task>(count);
            foreach (Dispatcher d in dispatchers)
            {
                disposeTasks.Add(d.DisposeAsync().AsTask());
            }
            await Task.WhenAll(disposeTasks);
            sw.Stop();

            monitorCts.Cancel();
            try { await monitorTask; } catch (OperationCanceledException) { }

            int threadCountAfter = ThreadPool.ThreadCount;
            int pendingAfter = (int)ThreadPool.PendingWorkItemCount;

            Console.WriteLine($"Dispatchers:           {count}");
            Console.WriteLine($"Disposal time:         {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Threads before:        {threadCountBefore}");
            Console.WriteLine($"Threads after:         {threadCountAfter}");
            Console.WriteLine($"Peak threads:          {peakThreadCount}");
            Console.WriteLine($"Thread delta:          {threadCountAfter - threadCountBefore}");
            Console.WriteLine($"Pending WI before:     {pendingBefore}");
            Console.WriteLine($"Pending WI after:      {pendingAfter}");
            Console.WriteLine($"Min threads (w/io):    {minWorker}/{minIO}");
            Console.WriteLine($"Max threads (w/io):    {maxWorker}/{maxIO}");

            // The peak thread count should not spike dramatically
            // (pre-fix, it would spike by ~count due to blocking)
            int threadSpike = peakThreadCount - threadCountBefore;
            Console.WriteLine($"Thread spike:          {threadSpike} (should be << {count})");

            Assert.IsTrue(threadSpike < count / 2,
                $"Thread count spiked by {threadSpike} during disposal of {count} dispatchers. " +
                "This suggests blocking behavior — async disposal should not need extra threads.");
        }

        private static List<Dispatcher> CreateDispatchers(int count, TimerPool idleTimerPool)
        {
            List<Dispatcher> dispatchers = new List<Dispatcher>(count);
            for (int i = 0; i < count; i++)
            {
                Mock<IConnection> mockConnection = CreateMockConnection(
                    new Uri($"rntbd://localhost:{10000 + i}/"));

                dispatchers.Add(new Dispatcher(
                    serverUri: new Uri($"rntbd://localhost:{10000 + i}/"),
                    userAgent: new UserAgentContainer(),
                    connectionStateListener: null,
                    idleTimerPool: idleTimerPool,
                    enableChannelMultiplexing: true,
                    chaosInterceptor: null,
                    connection: mockConnection.Object));
            }
            return dispatchers;
        }

        private static Mock<IConnection> CreateMockConnection(Uri serverUri)
        {
            Mock<IConnection> mock = new Mock<IConnection>(MockBehavior.Loose);
            bool disposed = false;

            mock.SetupGet(c => c.ServerUri).Returns(serverUri);
            mock.SetupGet(c => c.ConnectionCorrelationId).Returns(Guid.NewGuid());
            mock.SetupGet(c => c.Healthy).Returns(() => !disposed);
            mock.SetupGet(c => c.Disposed).Returns(() => disposed);
            mock.SetupGet(c => c.BufferProvider).Returns(new BufferProvider());
            mock.Setup(c => c.Dispose()).Callback(() => disposed = true);

            mock.Setup(c => c.IsActive(out It.Ref<TimeSpan>.IsAny))
                .Returns(new IsActiveDelegate((out TimeSpan timeToIdle) =>
                {
                    timeToIdle = TimeSpan.Zero;
                    return !disposed;
                }));

            return mock;
        }

        private delegate bool IsActiveDelegate(out TimeSpan timeToIdle);
    }
}
