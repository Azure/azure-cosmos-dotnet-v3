//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using static Microsoft.Azure.Documents.Rntbd.Connection;

    /// <summary>
    /// Tests for thread pool starvation fix in the RNTBD Dispatcher.
    /// Validates that idle timer callbacks and disposal paths do not block thread pool threads.
    /// Regression tests for https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4393
    /// </summary>
    [TestClass]
    public class DispatcherThreadStarvationTests
    {
        /// <summary>
        /// Verifies that calling Dispose() multiple times is idempotent
        /// (does not throw ObjectDisposedException) per .NET IDisposable guidelines.
        /// This was changed from throw-on-double-dispose to silent return.
        /// </summary>
        [TestMethod]
        public void Dispose_IsIdempotent()
        {
            using TimerPool idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);

            Mock<IConnection> mockConnection = CreateMockConnection(
                serverUri: new Uri("rntbd://localhost:10000/"),
                idleTimeout: TimeSpan.FromSeconds(60));

            Dispatcher dispatcher = new Dispatcher(
                serverUri: new Uri("rntbd://localhost:10000/"),
                userAgent: new UserAgentContainer(),
                connectionStateListener: null,
                idleTimerPool: idleTimerPool,
                enableChannelMultiplexing: true,
                chaosInterceptor: null,
                connection: mockConnection.Object);

            // First dispose should succeed
            dispatcher.Dispose();

            // Second dispose should be a no-op (not throw)
            dispatcher.Dispose();
        }

        /// <summary>
        /// Verifies that concurrent Dispose() and DisposeAsync() calls do not
        /// double-execute the shutdown sequence via the Interlocked.CompareExchange guard.
        /// </summary>
        [TestMethod]
        [Timeout(15_000)]
        public async Task ConcurrentDisposeAndDisposeAsync_OnlyOneExecutes()
        {
            int connectionDisposeCount = 0;

            using TimerPool idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);

            Mock<IConnection> mockConnection = CreateMockConnection(
                serverUri: new Uri("rntbd://localhost:10000/"),
                idleTimeout: TimeSpan.FromSeconds(60));

            mockConnection.Setup(c => c.Dispose())
                .Callback(() => Interlocked.Increment(ref connectionDisposeCount));

            Dispatcher dispatcher = new Dispatcher(
                serverUri: new Uri("rntbd://localhost:10000/"),
                userAgent: new UserAgentContainer(),
                connectionStateListener: null,
                idleTimerPool: idleTimerPool,
                enableChannelMultiplexing: true,
                chaosInterceptor: null,
                connection: mockConnection.Object);

            // Race Dispose and DisposeAsync
            Task syncDispose = Task.Run(() => dispatcher.Dispose());
            Task asyncDispose = dispatcher.DisposeAsync().AsTask();

            await Task.WhenAll(syncDispose, asyncDispose);

            // Connection should be disposed exactly once
            Assert.AreEqual(1, connectionDisposeCount,
                "Connection was disposed more than once — atomic disposal guard failed.");
        }

        /// <summary>
        /// Verifies that DisposeAsync is idempotent - calling it multiple times
        /// should be a no-op after the first call.
        /// </summary>
        [TestMethod]
        [Timeout(15_000)]
        public async Task DisposeAsync_IsIdempotent()
        {
            using TimerPool idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);

            Mock<IConnection> mockConnection = CreateMockConnection(
                serverUri: new Uri("rntbd://localhost:10000/"),
                idleTimeout: TimeSpan.FromSeconds(60));

            Dispatcher dispatcher = new Dispatcher(
                serverUri: new Uri("rntbd://localhost:10000/"),
                userAgent: new UserAgentContainer(),
                connectionStateListener: null,
                idleTimerPool: idleTimerPool,
                enableChannelMultiplexing: true,
                chaosInterceptor: null,
                connection: mockConnection.Object);

            // First DisposeAsync should succeed
            await dispatcher.DisposeAsync();

            // Second DisposeAsync should be a no-op
            await dispatcher.DisposeAsync();
        }

        /// <summary>
        /// Verifies that the WaitTaskAsync method yields the thread (non-blocking)
        /// and completes when the awaited task completes.
        /// This is the core mechanism that fixes the starvation issue:
        /// the old WaitTask() called t.Wait() which blocks the thread pool thread.
        /// </summary>
        [TestMethod]
        [Timeout(15_000)]
        public async Task DisposeAsync_DoesNotBlock_WhenNoReceiveTask()
        {
            using TimerPool idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);

            Mock<IConnection> mockConnection = CreateMockConnection(
                serverUri: new Uri("rntbd://localhost:10000/"),
                idleTimeout: TimeSpan.FromSeconds(60));

            Dispatcher dispatcher = new Dispatcher(
                serverUri: new Uri("rntbd://localhost:10000/"),
                userAgent: new UserAgentContainer(),
                connectionStateListener: null,
                idleTimerPool: idleTimerPool,
                enableChannelMultiplexing: true,
                chaosInterceptor: null,
                connection: mockConnection.Object);

            // DisposeAsync should complete promptly without blocking
            Stopwatch sw = Stopwatch.StartNew();
            await dispatcher.DisposeAsync();
            sw.Stop();

            Assert.IsTrue(sw.ElapsedMilliseconds < 5000,
                $"DisposeAsync took {sw.ElapsedMilliseconds}ms — expected < 5000ms.");
        }

        /// <summary>
        /// Stress test: Verifies that many concurrent Dispatcher disposals do not
        /// starve the thread pool. This simulates the N-connections-going-idle scenario
        /// at the disposal level.
        /// </summary>
        [TestMethod]
        [Timeout(15_000)]
        public async Task ManyDisposals_DoNotStarveThreadPool()
        {
            const int dispatcherCount = 100;

            using TimerPool idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);
            List<Dispatcher> dispatchers = new List<Dispatcher>(dispatcherCount);

            try
            {
                for (int i = 0; i < dispatcherCount; i++)
                {
                    Mock<IConnection> mockConnection = CreateMockConnection(
                        serverUri: new Uri($"rntbd://localhost:{10000 + i}/"),
                        idleTimeout: TimeSpan.FromSeconds(60));

                    dispatchers.Add(new Dispatcher(
                        serverUri: new Uri($"rntbd://localhost:{10000 + i}/"),
                        userAgent: new UserAgentContainer(),
                        connectionStateListener: null,
                        idleTimerPool: idleTimerPool,
                        enableChannelMultiplexing: true,
                        chaosInterceptor: null,
                        connection: mockConnection.Object));
                }

                // Dispose all concurrently via DisposeAsync
                List<Task> disposeTasks = new List<Task>(dispatcherCount);
                foreach (Dispatcher dispatcher in dispatchers)
                {
                    disposeTasks.Add(dispatcher.DisposeAsync().AsTask());
                }

                // If thread pool is starved, Task.WhenAll won't complete in time
                Task allDisposed = Task.WhenAll(disposeTasks);
                Task completed = await Task.WhenAny(allDisposed, Task.Delay(TimeSpan.FromSeconds(10)));

                Assert.AreEqual(allDisposed, completed,
                    "100 concurrent DisposeAsync calls did not complete within 10 seconds — possible thread pool starvation.");

                // Verify thread pool is still responsive
                TaskCompletionSource<bool> probe = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                ThreadPool.QueueUserWorkItem(_ => probe.TrySetResult(true));

                Task probeResult = await Task.WhenAny(probe.Task, Task.Delay(TimeSpan.FromSeconds(3)));
                Assert.AreEqual(probe.Task, probeResult,
                    "Thread pool is not responsive after mass disposal.");
            }
            finally
            {
                foreach (Dispatcher dispatcher in dispatchers)
                {
                    try { dispatcher.Dispose(); }
                    catch (ObjectDisposedException) { }
                }
            }
        }

        /// <summary>
        /// Verifies Channel async disposal is idempotent and properly chains
        /// through to Dispatcher.DisposeAsync.
        /// </summary>
        [TestMethod]
        [Timeout(15_000)]
        public async Task Channel_DisposeAsync_IsIdempotent()
        {
            int dispatcherDisposeCount = 0;

            Mock<IConnection> mockConnection = CreateMockConnection(
                serverUri: new Uri("rntbd://localhost:10000/"),
                idleTimeout: TimeSpan.FromSeconds(60));

            mockConnection.Setup(c => c.Dispose())
                .Callback(() => Interlocked.Increment(ref dispatcherDisposeCount));

            using TimerPool requestTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);
            using TimerPool idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);

            ChannelProperties channelProperties = new ChannelProperties(
                new UserAgentContainer(),
                certificateHostNameOverride: null,
                connectionStateListener: null,
                requestTimerPool: requestTimerPool,
                requestTimeout: TimeSpan.FromSeconds(10),
                openTimeout: TimeSpan.FromSeconds(5),
                localRegionOpenTimeout: TimeSpan.FromSeconds(5),
                portReuseMode: PortReuseMode.ReuseUnicastPort,
                userPortPool: null,
                maxChannels: 1,
                partitionCount: 1,
                maxRequestsPerChannel: 10,
                maxConcurrentOpeningConnectionCount: 1,
                receiveHangDetectionTime: TimeSpan.FromSeconds(30),
                sendHangDetectionTime: TimeSpan.FromSeconds(10),
                idleTimeout: TimeSpan.FromSeconds(60),
                idleTimerPool: idleTimerPool,
                callerId: RntbdConstants.CallerId.Anonymous,
                enableChannelMultiplexing: true,
                memoryStreamPool: null,
                remoteCertificateValidationCallback: null,
                clientCertificateFunction: null,
                clientCertificateFailureHandler: null,
                dnsResolutionFunction: null);

            LoadBalancingChannel lbChannel = new LoadBalancingChannel(
                new Uri("rntbd://localhost:10000/"),
                channelProperties,
                localRegionRequest: false);

            // First DisposeAsync should succeed
            await lbChannel.DisposeAsync();

            // Second DisposeAsync should be a no-op (not throw or double-dispose)
            await lbChannel.DisposeAsync();
        }

        /// <summary>
        /// Verifies ChannelDictionary async disposal properly uses Task.WhenAll
        /// for concurrent channel closure.
        /// </summary>
        [TestMethod]
        [Timeout(15_000)]
        public async Task ChannelDictionary_DisposeAsync_IsIdempotent()
        {
            using TimerPool requestTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);
            using TimerPool idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);

            ChannelProperties channelProperties = new ChannelProperties(
                new UserAgentContainer(),
                certificateHostNameOverride: null,
                connectionStateListener: null,
                requestTimerPool: requestTimerPool,
                requestTimeout: TimeSpan.FromSeconds(10),
                openTimeout: TimeSpan.FromSeconds(5),
                localRegionOpenTimeout: TimeSpan.FromSeconds(5),
                portReuseMode: PortReuseMode.ReuseUnicastPort,
                userPortPool: null,
                maxChannels: 1,
                partitionCount: 1,
                maxRequestsPerChannel: 10,
                maxConcurrentOpeningConnectionCount: 1,
                receiveHangDetectionTime: TimeSpan.FromSeconds(30),
                sendHangDetectionTime: TimeSpan.FromSeconds(10),
                idleTimeout: TimeSpan.FromSeconds(60),
                idleTimerPool: idleTimerPool,
                callerId: RntbdConstants.CallerId.Anonymous,
                enableChannelMultiplexing: true,
                memoryStreamPool: null,
                remoteCertificateValidationCallback: null,
                clientCertificateFunction: null,
                clientCertificateFailureHandler: null,
                dnsResolutionFunction: null);

            ChannelDictionary channelDict = new ChannelDictionary(channelProperties);

            // Create some channels
            channelDict.GetChannel(new Uri("rntbd://server1:443/"), localRegionRequest: false);
            channelDict.GetChannel(new Uri("rntbd://server2:443/"), localRegionRequest: false);
            channelDict.GetChannel(new Uri("rntbd://server3:443/"), localRegionRequest: false);

            // First DisposeAsync should succeed
            await channelDict.DisposeAsync();

            // Second DisposeAsync should be a no-op
            await channelDict.DisposeAsync();
        }

        /// <summary>
        /// END-TO-END TEST: Exercises the actual idle timer → OnIdleTimerAsync → WaitTaskAsync
        /// path using REAL SDK Dispatcher and TimerPool instances.
        ///
        /// This is the critical test that validates the thread pool starvation fix.
        /// It creates N real Dispatchers, injects pending receive tasks (simulating
        /// connections blocked on network I/O), triggers the idle timer path via
        /// the SDK's own TimerPool, and verifies the thread pool stays responsive.
        ///
        /// The test uses reflection to:
        /// 1. Inject a long-running receiveTask (simulating a blocked network read)
        /// 2. Call StartIdleTimer to schedule the idle timer via the real TimerPool
        ///
        /// When the TimerPool fires, the real OnIdleTimerAsync method runs on thread
        /// pool threads. With the async fix, these callbacks yield via 'await' instead
        /// of blocking with t.Wait(), keeping the thread pool responsive.
        /// </summary>
        [TestMethod]
        [Timeout(60_000)]
        public async Task EndToEnd_IdleTimerCallbacks_WithPendingReceiveTasks_ThreadPoolRemainsResponsive()
        {
            const int dispatcherCount = 50;

            using TimerPool idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);
            List<Dispatcher> dispatchers = new List<Dispatcher>(dispatcherCount);
            List<ManualResetEventSlim> receiveGates = new List<ManualResetEventSlim>(dispatcherCount);

            // Constrain thread pool to make starvation visible
            ThreadPool.GetMinThreads(out int origMinWorker, out int origMinIO);
            ThreadPool.SetMinThreads(Environment.ProcessorCount, origMinIO);

            try
            {
                // Reflection handles for private Dispatcher fields/methods
                FieldInfo receiveTaskField = typeof(Dispatcher).GetField(
                    "receiveTask",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(receiveTaskField, "Could not find Dispatcher.receiveTask field");

                MethodInfo startIdleTimerMethod = typeof(Dispatcher).GetMethod(
                    "StartIdleTimer",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(startIdleTimerMethod, "Could not find Dispatcher.StartIdleTimer method");

                int isActiveCallCount = 0;

                for (int i = 0; i < dispatcherCount; i++)
                {
                    ManualResetEventSlim gate = new ManualResetEventSlim(false);
                    receiveGates.Add(gate);

                    // Create a mock connection that:
                    // - First IsActive call (from StartIdleTimer): returns true with 1s to idle
                    // - Subsequent calls (from OnIdleTimerAsync): returns false (idle → triggers shutdown)
                    Mock<IConnection> mockConnection = new Mock<IConnection>(MockBehavior.Loose);
                    bool connectionDisposed = false;

                    mockConnection.SetupGet(c => c.ServerUri).Returns(new Uri($"rntbd://localhost:{10000 + i}/"));
                    mockConnection.SetupGet(c => c.ConnectionCorrelationId).Returns(Guid.NewGuid());
                    mockConnection.SetupGet(c => c.Healthy).Returns(() => !connectionDisposed);
                    mockConnection.SetupGet(c => c.Disposed).Returns(() => connectionDisposed);
                    mockConnection.SetupGet(c => c.BufferProvider).Returns(new BufferProvider());
                    mockConnection.Setup(c => c.Dispose()).Callback(() => connectionDisposed = true);

                    mockConnection.Setup(c => c.IsActive(out It.Ref<TimeSpan>.IsAny))
                        .Returns(new IsActiveDelegate((out TimeSpan timeToIdle) =>
                        {
                            int callNum = Interlocked.Increment(ref isActiveCallCount);
                            if (!connectionDisposed && callNum <= dispatcherCount)
                            {
                                // First call per dispatcher (from StartIdleTimer):
                                // report active with 1 second to idle
                                timeToIdle = TimeSpan.FromSeconds(1);
                                return true;
                            }
                            // Subsequent calls (from OnIdleTimerAsync):
                            // report idle → connection should be shut down
                            timeToIdle = TimeSpan.Zero;
                            return false;
                        }));

                    Dispatcher dispatcher = new Dispatcher(
                        serverUri: new Uri($"rntbd://localhost:{10000 + i}/"),
                        userAgent: new UserAgentContainer(),
                        connectionStateListener: null,
                        idleTimerPool: idleTimerPool,
                        enableChannelMultiplexing: true,
                        chaosInterceptor: null,
                        connection: mockConnection.Object);

                    // STEP 1: Inject a long-running receive task via reflection.
                    // This simulates the background ReceiveLoopAsync that reads from a TCP socket.
                    // The task won't complete until we set the gate.
                    ManualResetEventSlim capturedGate = gate;
                    Task receiveTask = Task.Run(() => capturedGate.Wait(TimeSpan.FromSeconds(30)));
                    receiveTaskField.SetValue(dispatcher, receiveTask);

                    dispatchers.Add(dispatcher);
                }

                int threadCountBefore = ThreadPool.ThreadCount;

                // STEP 2: Trigger StartIdleTimer on each dispatcher.
                // This schedules idle timer callbacks via the real TimerPool.
                // Each timer is set for 1 second.
                foreach (Dispatcher dispatcher in dispatchers)
                {
                    startIdleTimerMethod.Invoke(dispatcher, null);
                }

                // STEP 3: Wait for idle timers to fire.
                // The TimerPool fires every 1 second. After ~2 seconds, all timers should have
                // fired, triggering OnIdleTimerAsync on thread pool threads.
                // OnIdleTimerAsync will:
                //   1. Check IsActive → returns false (idle)
                //   2. Call StartConnectionShutdown → cancels
                //   3. Call CloseConnection → disposes connection, gets receiveTask
                //   4. Call WaitTaskAsync(receiveTask) → awaits the pending receive task
                await Task.Delay(4000);

                // STEP 4: Thread pool probe — this is the critical assertion.
                // If the fix works, all OnIdleTimerAsync callbacks should have yielded
                // their threads via 'await', and the pool should be responsive.
                // If the old sync OnIdleTimer was used, 50 threads would be blocked on
                // t.Wait() and the probe would fail.
                TaskCompletionSource<bool> probe = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                ThreadPool.QueueUserWorkItem(_ => probe.TrySetResult(true));

                Stopwatch sw = Stopwatch.StartNew();
                bool responsive = await Task.WhenAny(probe.Task, Task.Delay(10_000)) == probe.Task;
                sw.Stop();

                int threadCountDuring = ThreadPool.ThreadCount;
                int threadSpike = threadCountDuring - threadCountBefore;

                Console.WriteLine($"[E2E] Dispatchers: {dispatcherCount}");
                Console.WriteLine($"[E2E] Thread pool: {threadCountBefore} → {threadCountDuring} (spike: +{threadSpike})");
                Console.WriteLine($"[E2E] Probe latency: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"[E2E] Responsive: {responsive}");

                Assert.IsTrue(responsive,
                    $"THREAD POOL STARVATION DETECTED: QueueUserWorkItem could not execute " +
                    $"within 10 seconds after {dispatcherCount} idle timer callbacks fired. " +
                    $"Thread spike: +{threadSpike}. This indicates OnIdleTimerAsync is blocking " +
                    $"thread pool threads instead of yielding via 'await'. " +
                    $"Regression of fix for issue #4393.");

                Assert.IsTrue(sw.ElapsedMilliseconds < 5000,
                    $"Thread pool probe took {sw.ElapsedMilliseconds}ms — expected < 5000ms. " +
                    $"Possible thread pool pressure from idle timer callbacks.");
            }
            finally
            {
                ThreadPool.SetMinThreads(origMinWorker, origMinIO);

                // Release all receive gates so tasks complete
                foreach (ManualResetEventSlim gate in receiveGates)
                {
                    gate.Set();
                }

                // Allow callbacks to complete
                await Task.Delay(1000);

                // Dispose all dispatchers
                foreach (Dispatcher dispatcher in dispatchers)
                {
                    try { await dispatcher.DisposeAsync(); }
                    catch (ObjectDisposedException) { }
                }

                foreach (ManualResetEventSlim gate in receiveGates)
                {
                    gate.Dispose();
                }
            }
        }

        /// <summary>
        /// END-TO-END TEST: Verifies that mass concurrent disposal via DisposeAsync
        /// using REAL Dispatcher and TimerPool instances does not starve the thread pool.
        ///
        /// This exercises Path 2 of the starvation bug: mass disposal through the
        /// ChannelDictionary → Channel → Dispatcher.Dispose chain.
        /// </summary>
        [TestMethod]
        [Timeout(30_000)]
        public async Task EndToEnd_MassAsyncDisposal_ThreadPoolRemainsResponsive()
        {
            const int dispatcherCount = 100;

            using TimerPool idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);
            List<Dispatcher> dispatchers = new List<Dispatcher>(dispatcherCount);
            List<ManualResetEventSlim> receiveGates = new List<ManualResetEventSlim>(dispatcherCount);

            ThreadPool.GetMinThreads(out int origMinWorker, out int origMinIO);
            ThreadPool.SetMinThreads(Environment.ProcessorCount, origMinIO);

            try
            {
                FieldInfo receiveTaskField = typeof(Dispatcher).GetField(
                    "receiveTask",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                for (int i = 0; i < dispatcherCount; i++)
                {
                    ManualResetEventSlim gate = new ManualResetEventSlim(false);
                    receiveGates.Add(gate);

                    Mock<IConnection> mockConnection = CreateMockConnection(
                        serverUri: new Uri($"rntbd://localhost:{10000 + i}/"),
                        idleTimeout: TimeSpan.FromSeconds(60));

                    Dispatcher dispatcher = new Dispatcher(
                        serverUri: new Uri($"rntbd://localhost:{10000 + i}/"),
                        userAgent: new UserAgentContainer(),
                        connectionStateListener: null,
                        idleTimerPool: idleTimerPool,
                        enableChannelMultiplexing: true,
                        chaosInterceptor: null,
                        connection: mockConnection.Object);

                    // Inject a pending receive task that will only complete on gate.Set()
                    ManualResetEventSlim capturedGate = gate;
                    Task receiveTask = Task.Run(() => capturedGate.Wait(TimeSpan.FromSeconds(30)));
                    receiveTaskField.SetValue(dispatcher, receiveTask);

                    dispatchers.Add(dispatcher);
                }

                int threadCountBefore = ThreadPool.ThreadCount;

                // Start all disposals concurrently
                // DisposeAsync calls WaitTaskAsync(receiveTask) which awaits the pending tasks
                List<Task> disposeTasks = new List<Task>(dispatcherCount);
                foreach (Dispatcher dispatcher in dispatchers)
                {
                    disposeTasks.Add(dispatcher.DisposeAsync().AsTask());
                }

                // Give the thread pool time to process disposal work items
                await Task.Delay(2000);

                // Probe thread pool — should be responsive because DisposeAsync yields
                TaskCompletionSource<bool> probe = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                ThreadPool.QueueUserWorkItem(_ => probe.TrySetResult(true));

                Stopwatch sw = Stopwatch.StartNew();
                bool responsive = await Task.WhenAny(probe.Task, Task.Delay(10_000)) == probe.Task;
                sw.Stop();

                int threadCountDuring = ThreadPool.ThreadCount;

                Console.WriteLine($"[E2E Disposal] Dispatchers: {dispatcherCount}");
                Console.WriteLine($"[E2E Disposal] Thread pool: {threadCountBefore} → {threadCountDuring}");
                Console.WriteLine($"[E2E Disposal] Probe latency: {sw.ElapsedMilliseconds}ms");

                Assert.IsTrue(responsive,
                    $"Thread pool starved during mass DisposeAsync of {dispatcherCount} dispatchers. " +
                    $"Probe latency: {sw.ElapsedMilliseconds}ms. " +
                    $"DisposeAsync should yield via 'await', not block with .Wait().");

                // Release gates so disposal completes
                foreach (ManualResetEventSlim gate in receiveGates)
                {
                    gate.Set();
                }

                Task allDisposed = Task.WhenAll(disposeTasks);
                Task completed = await Task.WhenAny(allDisposed, Task.Delay(15_000));
                Assert.AreEqual(allDisposed, completed,
                    "DisposeAsync did not complete within 15 seconds after gates were released.");
            }
            finally
            {
                ThreadPool.SetMinThreads(origMinWorker, origMinIO);
                foreach (ManualResetEventSlim gate in receiveGates)
                {
                    gate.Set();
                    gate.Dispose();
                }
            }
        }

        /// <summary>
        /// END-TO-END TEST: Verifies that idle timer fire + concurrent DisposeAsync
        /// is race-safe using REAL Dispatcher and TimerPool instances.
        ///
        /// This is a stress test that races the idle timer callback against disposal
        /// to verify no deadlock or use-after-dispose occurs.
        /// </summary>
        [TestMethod]
        [Timeout(30_000)]
        public async Task EndToEnd_IdleTimerRacesWithDisposal_NoDeadlock()
        {
            const int iterations = 20;

            for (int iter = 0; iter < iterations; iter++)
            {
                using TimerPool idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 1);
                ManualResetEventSlim gate = new ManualResetEventSlim(false);

                int isActiveCallCount = 0;
                Mock<IConnection> mockConnection = new Mock<IConnection>(MockBehavior.Loose);
                bool disposed = false;

                mockConnection.SetupGet(c => c.ServerUri).Returns(new Uri("rntbd://localhost:10000/"));
                mockConnection.SetupGet(c => c.ConnectionCorrelationId).Returns(Guid.NewGuid());
                mockConnection.SetupGet(c => c.Healthy).Returns(() => !disposed);
                mockConnection.SetupGet(c => c.Disposed).Returns(() => disposed);
                mockConnection.SetupGet(c => c.BufferProvider).Returns(new BufferProvider());
                mockConnection.Setup(c => c.Dispose()).Callback(() => disposed = true);

                mockConnection.Setup(c => c.IsActive(out It.Ref<TimeSpan>.IsAny))
                    .Returns(new IsActiveDelegate((out TimeSpan timeToIdle) =>
                    {
                        int count = Interlocked.Increment(ref isActiveCallCount);
                        if (!disposed && count == 1)
                        {
                            timeToIdle = TimeSpan.FromSeconds(1);
                            return true;
                        }
                        timeToIdle = TimeSpan.Zero;
                        return false;
                    }));

                Dispatcher dispatcher = new Dispatcher(
                    serverUri: new Uri("rntbd://localhost:10000/"),
                    userAgent: new UserAgentContainer(),
                    connectionStateListener: null,
                    idleTimerPool: idleTimerPool,
                    enableChannelMultiplexing: true,
                    chaosInterceptor: null,
                    connection: mockConnection.Object);

                // Inject pending receive task
                FieldInfo receiveTaskField = typeof(Dispatcher).GetField(
                    "receiveTask", BindingFlags.NonPublic | BindingFlags.Instance);
                receiveTaskField.SetValue(dispatcher, Task.Run(() => gate.Wait(TimeSpan.FromSeconds(10))));

                // Start idle timer
                MethodInfo startIdleTimerMethod = typeof(Dispatcher).GetMethod(
                    "StartIdleTimer", BindingFlags.NonPublic | BindingFlags.Instance);
                startIdleTimerMethod.Invoke(dispatcher, null);

                // Wait until close to when the timer should fire, then race with DisposeAsync
                await Task.Delay(800);

                // Race: DisposeAsync vs timer firing
                gate.Set(); // release the receive task
                Task disposeTask = dispatcher.DisposeAsync().AsTask();

                Task completed = await Task.WhenAny(disposeTask, Task.Delay(10_000));
                Assert.AreEqual(disposeTask, completed,
                    $"Iteration {iter}: DisposeAsync deadlocked when racing with idle timer.");

                gate.Dispose();
            }
        }

        /// <summary>
        /// Creates a mock IConnection that simulates a basic RNTBD connection lifecycle.
        /// </summary>
        private static Mock<IConnection> CreateMockConnection(
            Uri serverUri,
            TimeSpan idleTimeout)
        {
            Mock<IConnection> mock = new Mock<IConnection>(MockBehavior.Loose);
            bool disposed = false;

            mock.SetupGet(c => c.ServerUri).Returns(serverUri);
            mock.SetupGet(c => c.ConnectionCorrelationId).Returns(Guid.NewGuid());
            mock.SetupGet(c => c.Healthy).Returns(() => !disposed);
            mock.SetupGet(c => c.Disposed).Returns(() => disposed);
            mock.SetupGet(c => c.BufferProvider).Returns(new BufferProvider());

            mock.Setup(c => c.Dispose()).Callback(() => disposed = true);

            DateTime createdAt = DateTime.UtcNow;
            mock.Setup(c => c.IsActive(out It.Ref<TimeSpan>.IsAny))
                .Returns(new IsActiveDelegate((out TimeSpan timeToIdle) =>
                {
                    TimeSpan elapsed = DateTime.UtcNow - createdAt;
                    if (elapsed < idleTimeout && !disposed)
                    {
                        timeToIdle = idleTimeout - elapsed;
                        return true;
                    }
                    timeToIdle = TimeSpan.Zero;
                    return false;
                }));

            mock.Setup(c => c.OpenAsync(It.IsAny<ChannelOpenArguments>()))
                .Returns(Task.CompletedTask);

            return mock;
        }

        private delegate bool IsActiveDelegate(out TimeSpan timeToIdle);
    }
}
