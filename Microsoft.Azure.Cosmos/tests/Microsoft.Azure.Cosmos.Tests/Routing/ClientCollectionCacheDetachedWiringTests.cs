//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Pins the wiring contract that <see cref="ClientCollectionCache.GetByRidAsync"/> and
    /// <see cref="ClientCollectionCache.GetByNameAsync"/> route through
    /// <see cref="MetadataDetachedExecutor"/> AND that they pass the executor-owned
    /// <i>detached</i> <see cref="CancellationToken"/> (NOT the caller's token) into the
    /// inner <c>ReadCollectionAsync</c> lambda.
    ///
    /// <para>
    /// A regression that reverts either lambda back to passing the caller's
    /// <c>cancellationToken</c> would silently reintroduce the original cross-region
    /// failover preemption bug (issue #5805): the unit-level
    /// <see cref="MetadataDetachedExecutorTests"/> would still pass because they exercise
    /// the executor directly with synthetic operations and never observe what the
    /// cache call sites actually pass through.
    /// </para>
    ///
    /// <para>
    /// Mechanism: hold the first <c>storeModel.ProcessMessageAsync</c> call on a gate,
    /// cancel the caller mid-flight, release the gate so the in-flight attempt fails
    /// transiently, and assert that the retry policy drives a SECOND
    /// <c>ProcessMessageAsync</c> invocation. With the correct (detached) wiring the
    /// inner <see cref="ClientCollectionCache.ReadCollectionAsync"/>'s top-of-method
    /// <see cref="CancellationToken.ThrowIfCancellationRequested"/> is harmless on the
    /// retry iteration; with broken (caller-passthrough) wiring it would fire and
    /// preempt the second attempt.
    /// </para>
    /// </summary>
    [TestClass]
    public class ClientCollectionCacheDetachedWiringTests
    {
        [TestMethod]
        [Owner("ntripician")]
        public Task GetByRidAsync_CallerCancelMidFlight_DetachedReadProceedsToRetry()
        {
            return this.RunWiringTestAsync(useByName: false);
        }

        [TestMethod]
        [Owner("ntripician")]
        public Task GetByNameAsync_CallerCancelMidFlight_DetachedReadProceedsToRetry()
        {
            return this.RunWiringTestAsync(useByName: true);
        }

        private async Task RunWiringTestAsync(bool useByName)
        {
            TaskCompletionSource<bool> firstCallGate = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            int processMessageInvocations = 0;

            Mock<IStoreModel> storeModelMock = new Mock<IStoreModel>();
            storeModelMock
                .Setup(s => s.ProcessMessageAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (DocumentServiceRequest _, CancellationToken __) =>
                {
                    int n = Interlocked.Increment(ref processMessageInvocations);
                    if (n == 1)
                    {
                        // Hold the first attempt until the test cancels the caller.
                        await firstCallGate.Task.ConfigureAwait(false);
                    }

                    // Throw a transient-style exception. The mocked retry policy below
                    // controls whether the executor retries, independent of exception type.
                    throw new TestTransientException();
                });

            Mock<ICosmosAuthorizationTokenProvider> tokenProviderMock = new Mock<ICosmosAuthorizationTokenProvider>();
            tokenProviderMock
                .Setup(t => t.GetUserAuthorizationTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<INameValueCollection>(),
                    It.IsAny<AuthorizationTokenType>(),
                    It.IsAny<ITrace>()))
                .Returns(new ValueTask<string>("test-token"));

            Mock<IDocumentClientRetryPolicy> retryPolicyMock = new Mock<IDocumentClientRetryPolicy>();
            retryPolicyMock
                .SetupSequence(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.Zero))   // after attempt 1: retry
                .ReturnsAsync(ShouldRetryResult.NoRetry());                  // after attempt 2: stop

            Mock<IRetryPolicyFactory> retryPolicyFactoryMock = new Mock<IRetryPolicyFactory>();
            retryPolicyFactoryMock
                .Setup(f => f.GetRequestPolicy())
                .Returns(retryPolicyMock.Object);

            TestableClientCollectionCache cache = new TestableClientCollectionCache(
                storeModel: storeModelMock.Object,
                tokenProvider: tokenProviderMock.Object,
                retryPolicy: retryPolicyFactoryMock.Object);

            using CancellationTokenSource callerCts = new CancellationTokenSource();
            ITrace trace = NoOpTrace.Singleton;

            Task<ContainerProperties> callerTask = useByName
                ? cache.InvokeGetByNameAsync("dbs/db/colls/coll", trace, callerCts.Token)
                : cache.InvokeGetByRidAsync("kjhsAA==", trace, callerCts.Token);

            // Wait for the first ProcessMessageAsync to be entered (gate is held).
            await SpinUntilAsync(
                predicate: () => Volatile.Read(ref processMessageInvocations) >= 1,
                timeout: TimeSpan.FromSeconds(10),
                description: "first ProcessMessageAsync invocation");

            // Cancel the caller mid-flight. With the correct detached wiring, this must NOT
            // preempt either the in-flight first attempt or the next retry iteration.
            callerCts.Cancel();

            // The caller observes OperationCanceledException via the executor's response-path
            // observer. This is the ONE side-effect the caller should see.
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => callerTask);

            // Release the held first attempt; it throws TestTransientException, the executor's
            // retry-policy decision (correctly invoked with CancellationToken.None) returns
            // RetryAfter(Zero), and the loop must enter a SECOND iteration of the operation
            // lambda. If the lambda were threaded with the (now-cancelled) caller token, the
            // top-of-method ThrowIfCancellationRequested in ReadCollectionAsync would fire
            // before storeModel is touched a second time and processMessageInvocations would
            // remain stuck at 1.
            firstCallGate.TrySetResult(true);

            await SpinUntilAsync(
                predicate: () => Volatile.Read(ref processMessageInvocations) >= 2,
                timeout: TimeSpan.FromSeconds(10),
                description: "second ProcessMessageAsync invocation (proves the inner lambda received the detached CT, not the caller's)");

            Assert.AreEqual(
                expected: 2,
                actual: Volatile.Read(ref processMessageInvocations),
                message: "ReadCollectionAsync should have run exactly twice; caller-cancel must not preempt the retry attempt.");
        }

        private static async Task SpinUntilAsync(Func<bool> predicate, TimeSpan timeout, string description)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (!predicate() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20).ConfigureAwait(false);
            }

            if (!predicate())
            {
                Assert.Fail($"Timed out waiting for: {description}");
            }
        }

        /// <summary>
        /// Test subclass that re-exposes the protected <see cref="ClientCollectionCache.GetByRidAsync"/>
        /// and <see cref="ClientCollectionCache.GetByNameAsync"/> entry points so the wiring
        /// can be exercised directly without going through the AsyncCache layer.
        /// </summary>
        private sealed class TestableClientCollectionCache : ClientCollectionCache
        {
            public TestableClientCollectionCache(
                IStoreModel storeModel,
                ICosmosAuthorizationTokenProvider tokenProvider,
                IRetryPolicyFactory retryPolicy)
                : base(
                    sessionContainer: new SessionContainer("testhost"),
                    storeModel: storeModel,
                    tokenProvider: tokenProvider,
                    retryPolicy: retryPolicy,
                    telemetryToServiceHelper: null,
                    enableAsyncCacheExceptionNoSharing: true)
            {
            }

            public Task<ContainerProperties> InvokeGetByRidAsync(
                string collectionRid,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return base.GetByRidAsync(
                    apiVersion: "2018-12-31",
                    collectionRid: collectionRid,
                    trace: trace,
                    clientSideRequestStatistics: null,
                    cancellationToken: cancellationToken);
            }

            public Task<ContainerProperties> InvokeGetByNameAsync(
                string resourceAddress,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return base.GetByNameAsync(
                    apiVersion: "2018-12-31",
                    resourceAddress: resourceAddress,
                    trace: trace,
                    clientSideRequestStatistics: null,
                    cancellationToken: cancellationToken);
            }
        }

        private sealed class TestTransientException : Exception
        {
            public TestTransientException()
                : base("simulated transient failure for ClientCollectionCacheDetachedWiringTests")
            {
            }
        }
    }
}
