//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Unit tests for <see cref="MetadataDetachedExecutor"/>.
    ///
    /// These tests pin the behavior of the detached-cancellation execution model used by
    /// <c>ClientCollectionCache.GetByRidAsync</c> / <c>GetByNameAsync</c>. The executor
    /// runs metadata reads on a detached <see cref="CancellationToken"/> and observes the
    /// caller's token only on the response path so that <see cref="IDocumentClientRetryPolicy.ShouldRetryAsync"/>
    /// (specifically <c>ClientRetryPolicy</c>'s cross-region failover decision) is never
    /// preempted by caller-cancel.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class MetadataDetachedExecutorTests
    {
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_SucceedsFirstAttempt_NoRetry()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            int attempts = 0;

            int result = await MetadataDetachedExecutor.ExecuteAsync<int>(
                (_) =>
                {
                    attempts++;
                    return Task.FromResult(42);
                },
                policy.Object,
                CancellationToken.None);

            Assert.AreEqual(42, result);
            Assert.AreEqual(1, attempts);
            policy.Verify(
                p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_RetriesOnTransient_WhenPolicyAllows()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.Zero));

            int attempts = 0;
            int result = await MetadataDetachedExecutor.ExecuteAsync<int>(
                (_) =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        throw new DocumentClientException(
                            "transient",
                            HttpStatusCode.ServiceUnavailable,
                            SubStatusCodes.Unknown);
                    }

                    return Task.FromResult(7);
                },
                policy.Object,
                CancellationToken.None);

            Assert.AreEqual(7, result);
            Assert.AreEqual(2, attempts);
        }

        /// <summary>
        /// PRIMARY FIX: when the caller's token trips MID-FLIGHT (during the first
        /// attempt — analog of the control-plane HTTP timeout policy burning out
        /// against the unhealthy region), the retry policy still drives a successful
        /// cross-region failover because the operation runs on a detached token.
        /// The caller observes OCE; the second attempt completes on the detached token.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_CrossRegionRetryExecutes_EvenWhenCallerTokenCancelsMidFlight()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.Zero));

            using CancellationTokenSource cts = new CancellationTokenSource();
            int attempts = 0;
            TaskCompletionSource<bool> firstAttemptStarted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> firstAttemptCanFail = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<int> secondAttemptResult = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> secondAttemptStarted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            Task<int> caller = Task.Run(() => MetadataDetachedExecutor.ExecuteAsync<int>(
                async (ct) =>
                {
                    int attempt = Interlocked.Increment(ref attempts);
                    if (attempt == 1)
                    {
                        firstAttemptStarted.TrySetResult(true);
                        await firstAttemptCanFail.Task.ConfigureAwait(false);
                        Assert.IsFalse(ct.IsCancellationRequested,
                            "operation must receive the detached token, not the caller token");
                        throw new DocumentClientException(
                            "primary region down",
                            HttpStatusCode.ServiceUnavailable,
                            SubStatusCodes.Unknown);
                    }

                    secondAttemptStarted.TrySetResult(true);
                    Assert.IsFalse(ct.IsCancellationRequested,
                        "second-attempt token must remain non-cancelled despite caller cancellation");
                    int value = await secondAttemptResult.Task.ConfigureAwait(false);
                    return value;
                },
                policy.Object,
                cts.Token));

            await firstAttemptStarted.Task.ConfigureAwait(false);

            cts.Cancel();
            firstAttemptCanFail.TrySetResult(true);

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => caller);

            secondAttemptResult.TrySetResult(99);
            Task winner = await Task.WhenAny(secondAttemptStarted.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.AreSame(secondAttemptStarted.Task, winner,
                "the cross-region retry attempt must execute on the detached token after caller cancellation");
            Assert.AreEqual(2, Interlocked.CompareExchange(ref attempts, 0, 0),
                "the cross-region retry attempt must execute on the detached token after caller cancellation");
        }

        /// <summary>
        /// If the caller's token trips while the detached attempt is mid-flight, the
        /// caller surfaces OCE immediately. The detached task is allowed to keep running
        /// (verified via TaskCompletionSource — the operation lambda is not signalled
        /// via its token).
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_CallerCancelMidFlight_SurfacesOCE_DetachedTaskKeepsRunning()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            using CancellationTokenSource cts = new CancellationTokenSource();
            TaskCompletionSource<int> operationGate = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> operationStarted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> operationCompletedOnDetachedToken = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            Task<int> caller = MetadataDetachedExecutor.ExecuteAsync<int>(
                async (ct) =>
                {
                    operationStarted.TrySetResult(true);
                    int value = await operationGate.Task.ConfigureAwait(false);
                    Assert.IsFalse(ct.IsCancellationRequested,
                        "detached token must not flip when caller cancels");
                    operationCompletedOnDetachedToken.TrySetResult(true);
                    return value;
                },
                policy.Object,
                cts.Token);

            await operationStarted.Task.ConfigureAwait(false);
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => caller);

            operationGate.TrySetResult(123);

            Task winner = await Task.WhenAny(operationCompletedOnDetachedToken.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.AreSame(operationCompletedOnDetachedToken.Task, winner,
                "detached operation must run to completion after caller cancellation");
        }

        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_AlreadyCancelledCallerToken_ThrowsBeforeAnyOperation()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            int attempts = 0;
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => MetadataDetachedExecutor.ExecuteAsync<int>(
                    (_) =>
                    {
                        attempts++;
                        return Task.FromResult(0);
                    },
                    policy.Object,
                    cts.Token));

            Assert.AreEqual(0, attempts, "operation must not be invoked when caller token is already cancelled");
            policy.Verify(
                p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_CancellationTokenNone_SucceedsAndOperationReceivesNonCanceledToken()
        {
            // Note: the executor has a fast path for CancellationToken.None that skips the
            // Task.WhenAny scaffolding. That micro-optimization is not directly observable
            // from outside the type; this test pins the behavioral contract that
            // (1) CancellationToken.None as caller token completes successfully end-to-end,
            // and (2) the operation receives a token that is itself non-canceled at entry.
            // Functional regressions of the fast path are caught by the broader test suite.
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();

            int result = await MetadataDetachedExecutor.ExecuteAsync<int>(
                (ct) =>
                {
                    Assert.IsFalse(ct.IsCancellationRequested,
                        "detached token must not be already-cancelled at entry");
                    return Task.FromResult(11);
                },
                policy.Object,
                CancellationToken.None);

            Assert.AreEqual(11, result);
        }

        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_PolicyDeniesRetry_SurfacesOriginalException()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.NoRetry());

            DocumentClientException original = new DocumentClientException(
                "fatal",
                HttpStatusCode.Forbidden,
                SubStatusCodes.Unknown);

            DocumentClientException thrown = await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => MetadataDetachedExecutor.ExecuteAsync<int>(
                    (_) => throw original,
                    policy.Object,
                    CancellationToken.None));

            Assert.AreSame(original, thrown);
        }

        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_HonorsExceptionToThrow_FromPolicy()
        {
            DocumentClientException wrapped = new DocumentClientException(
                "wrapped",
                HttpStatusCode.RequestTimeout,
                SubStatusCodes.Unknown);

            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.NoRetry(wrapped));

            DocumentClientException thrown = await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => MetadataDetachedExecutor.ExecuteAsync<int>(
                    (_) => throw new DocumentClientException(
                        "inner", HttpStatusCode.ServiceUnavailable, SubStatusCodes.Unknown),
                    policy.Object,
                    CancellationToken.None));

            Assert.AreSame(wrapped, thrown);
        }

        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_PolicyThrows_SurfacesOriginalException()
        {
            DocumentClientException original = new DocumentClientException(
                "transient",
                HttpStatusCode.ServiceUnavailable,
                SubStatusCodes.Unknown);

            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("policy crashed"));

            DocumentClientException thrown = await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => MetadataDetachedExecutor.ExecuteAsync<int>(
                    (_) => throw original,
                    policy.Object,
                    CancellationToken.None));

            Assert.AreSame(original, thrown);
        }

        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_InternalDeadline_BoundsDetachedAttempt()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.FromSeconds(30)));

            // Operation always fails. Backoff is 30s but deadline is 200ms — we expect
            // the original exception to surface once the internal deadline trips during
            // the backoff delay.
            DocumentClientException firstFailure = new DocumentClientException(
                "transient",
                HttpStatusCode.ServiceUnavailable,
                SubStatusCodes.Unknown);

            DocumentClientException thrown = await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => MetadataDetachedExecutor.ExecuteAsync<int>(
                    (_) => throw firstFailure,
                    policy.Object,
                    TimeSpan.FromMilliseconds(200),
                    CancellationToken.None));

            Assert.AreSame(firstFailure, thrown);
        }

        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_HardAttemptCap_PreventsInfiniteSpin()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.Zero));

            int attempts = 0;
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => MetadataDetachedExecutor.ExecuteAsync<int>(
                    (_) =>
                    {
                        Interlocked.Increment(ref attempts);
                        throw new DocumentClientException(
                            "always",
                            HttpStatusCode.ServiceUnavailable,
                            SubStatusCodes.Unknown);
                    },
                    policy.Object,
                    CancellationToken.None));

            int observed = Interlocked.CompareExchange(ref attempts, 0, 0);
            Assert.AreEqual(MetadataDetachedExecutor.MaxAttemptsHardCap, observed,
                $"expected exactly {MetadataDetachedExecutor.MaxAttemptsHardCap} attempts before cap, observed {observed}");
        }

        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_FirstAttemptOCE_PolicyIsConsultedBeforeSurfacing()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.NoRetry());

            OperationCanceledException original = new OperationCanceledException("server-side cancel");

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => MetadataDetachedExecutor.ExecuteAsync<int>(
                    (_) => throw original,
                    policy.Object,
                    CancellationToken.None));

            policy.Verify(
                p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_NullOperation_Throws()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();

            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => MetadataDetachedExecutor.ExecuteAsync<int>(
                    null,
                    policy.Object,
                    CancellationToken.None));
        }

        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_NullRetryPolicy_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => MetadataDetachedExecutor.ExecuteAsync<int>(
                    (_) => Task.FromResult(0),
                    null,
                    CancellationToken.None));
        }

        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_NonPositiveDeadline_Throws()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();

            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                () => MetadataDetachedExecutor.ExecuteAsync<int>(
                    (_) => Task.FromResult(0),
                    policy.Object,
                    TimeSpan.Zero,
                    CancellationToken.None));

            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                () => MetadataDetachedExecutor.ExecuteAsync<int>(
                    (_) => Task.FromResult(0),
                    policy.Object,
                    TimeSpan.FromSeconds(-1),
                    CancellationToken.None));
        }

        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_NonZeroBackoff_HonoredBetweenAttempts()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.FromMilliseconds(150)));

            int attempts = 0;
            DateTime start = DateTime.UtcNow;
            int result = await MetadataDetachedExecutor.ExecuteAsync<int>(
                (_) =>
                {
                    attempts++;
                    if (attempts < 3)
                    {
                        throw new DocumentClientException(
                            "transient",
                            HttpStatusCode.ServiceUnavailable,
                            SubStatusCodes.Unknown);
                    }

                    return Task.FromResult(7);
                },
                policy.Object,
                CancellationToken.None);

            TimeSpan elapsed = DateTime.UtcNow - start;
            Assert.AreEqual(7, result);
            Assert.AreEqual(3, attempts);
            // Two backoffs of ~150ms each → 300ms minimum (allow generous slack for CI).
            Assert.IsTrue(elapsed >= TimeSpan.FromMilliseconds(250),
                $"expected backoff to add ≥250ms, observed {elapsed.TotalMilliseconds}ms");
        }

        /// <summary>
        /// Smoke test for the NETFX <see cref="SynchronizationContext"/> path: even when
        /// running under a single-threaded synchronization context, the executor must
        /// not deadlock and must surface the result. We exercise the inner
        /// <c>ExecuteAsync</c> directly (callers in <c>ClientCollectionCache</c> route through
        /// <c>TaskHelper.RunInlineIfNeededAsync</c> which Task.Run-wraps when SyncContext is
        /// non-null).
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_UnderSynchronizationContext_DoesNotDeadlock()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            SynchronizationContext previous = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(new SingleThreadSynchronizationContext());

                int result = await Task.Run(() => MetadataDetachedExecutor.ExecuteAsync<int>(
                    (_) => Task.FromResult(33),
                    policy.Object,
                    CancellationToken.None));

                Assert.AreEqual(33, result);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        /// <summary>
        /// Regression test for R2.4: when the SDK-internal hard deadline trips while the
        /// operation lambda is in flight (as opposed to during Task.Delay backoff), the
        /// surfaced exception must be the underlying retry-driving failure, not the
        /// deadline-induced OperationCanceledException. This protects the design contract
        /// that customers see the failure mode that drove the retry rather than a
        /// hard-deadline artifact, regardless of whether the deadline trips during backoff
        /// or during the operation call itself.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_DeadlineTripsDuringOperation_SurfacesUnderlyingException()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.Zero));

            int attempts = 0;
            DocumentClientException underlying = new DocumentClientException(
                "underlying-503",
                HttpStatusCode.ServiceUnavailable,
                SubStatusCodes.Unknown);

            DocumentClientException thrown = await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => MetadataDetachedExecutor.ExecuteAsync<int>(
                    async (ct) =>
                    {
                        attempts++;
                        if (attempts == 1)
                        {
                            // First attempt: surface the real underlying failure to drive a retry.
                            throw underlying;
                        }

                        // Second attempt: block on the detached token until the deadline trips,
                        // then throw OCE bound to the detached token. This mimics an HTTP call
                        // that observes the SDK-internal deadline mid-flight.
                        await Task.Delay(Timeout.Infinite, ct);
                        return 0;
                    },
                    policy.Object,
                    TimeSpan.FromMilliseconds(200),
                    CancellationToken.None));

            Assert.AreSame(underlying, thrown,
                "Expected the original DocumentClientException, not a deadline-induced OperationCanceledException.");
            Assert.IsTrue(attempts >= 2, $"expected ≥2 attempts, observed {attempts}");
        }

        /// <summary>
        /// Regression test for the ConfigurationManager upper-clamp. Without this clamp, a
        /// misconfigured AZURE_COSMOS_METADATA_DETACHED_HARD_DEADLINE_SECONDS value larger than
        /// roughly 49.7 days (uint.MaxValue-1 ms) would make every metadata read throw
        /// ArgumentOutOfRangeException from new CancellationTokenSource(TimeSpan), breaking the
        /// whole metadata path. The clamp keeps the resulting TimeSpan inside
        /// CancellationTokenSource's accepted domain.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public void GetMetadataDetachedHardDeadline_ClampsAbsurdlyLargeEnvVarValue()
        {
            string variable = ConfigurationManager.MetadataDetachedHardDeadlineInSeconds;
            string previous = Environment.GetEnvironmentVariable(variable);
            try
            {
                // 60 days in seconds: well over the 24h ceiling, and large enough to
                // exercise the CTS-overflow regime if the clamp were missing.
                Environment.SetEnvironmentVariable(variable, (60L * 24 * 60 * 60).ToString());

                TimeSpan deadline = ConfigurationManager.GetMetadataDetachedHardDeadline();

                Assert.IsTrue(
                    deadline <= TimeSpan.FromSeconds(ConfigurationManager.MaxMetadataDetachedHardDeadlineInSeconds),
                    $"deadline {deadline} must be clamped to <= {ConfigurationManager.MaxMetadataDetachedHardDeadlineInSeconds}s");

                // The clamped value must construct a CancellationTokenSource without throwing —
                // this is the actual bug the clamp prevents.
                using CancellationTokenSource cts = new CancellationTokenSource(deadline);
                Assert.IsFalse(cts.Token.IsCancellationRequested);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, previous);
            }
        }

        [TestMethod]
        [Owner("ntripician")]
        public void GetMetadataDetachedHardDeadline_ClampsTooSmallEnvVarValue()
        {
            string variable = ConfigurationManager.MetadataDetachedHardDeadlineInSeconds;
            string previous = Environment.GetEnvironmentVariable(variable);
            try
            {
                Environment.SetEnvironmentVariable(variable, "1");

                TimeSpan deadline = ConfigurationManager.GetMetadataDetachedHardDeadline();

                Assert.AreEqual(
                    TimeSpan.FromSeconds(ConfigurationManager.MinMetadataDetachedHardDeadlineInSeconds),
                    deadline);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, previous);
            }
        }

        private sealed class SingleThreadSynchronizationContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state)
            {
                ThreadPool.QueueUserWorkItem(_ => d(state));
            }
        }
    }
}
