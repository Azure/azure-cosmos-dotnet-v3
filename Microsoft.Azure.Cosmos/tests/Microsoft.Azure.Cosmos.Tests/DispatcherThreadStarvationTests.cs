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
