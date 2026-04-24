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
    /// Unit tests for <see cref="MetadataRetryHelper"/>.
    ///
    /// These tests reproduce the control-plane metadata retry defect described in
    /// PR #5787 — when a caller's cancellation token trips during the control-plane
    /// HTTP timeout escalation against an unhealthy region, the cross-region retry
    /// that <see cref="ClientRetryPolicy"/> would otherwise execute is preempted.
    ///
    /// Each test is written so that the pre-fix implementation (TaskHelper.InlineIfPossible
    /// → BackoffRetryUtility) would FAIL the assertion, while the fixed implementation
    /// (MetadataRetryHelper) passes.
    /// </summary>
    [TestClass]
    public class MetadataRetryHelperTests
    {
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_SucceedsFirstAttempt_NoRetry()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            int attempts = 0;

            int result = await MetadataRetryHelper.ExecuteAsync<int>(
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

        /// <summary>
        /// Exercises the classic retry loop: exception, policy says retry, second attempt
        /// succeeds. No cancellation involved.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_RetriesOnTransient_WhenPolicyAllows()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.Zero));

            int attempts = 0;

            int result = await MetadataRetryHelper.ExecuteAsync<int>(
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
        /// REPRO / FIX VERIFICATION: the caller's cancellation token is already cancelled
        /// at the time the first attempt throws a 503. The retry policy says "retry"
        /// (cross-region failover). The fix must still execute one cross-region attempt
        /// against the detached grace token.
        ///
        /// Pre-fix behavior (BackoffRetryUtility): throws OperationCanceledException
        /// without consulting the policy, or consults the policy but then immediately
        /// honors the cancelled token before the retry attempt. Either way, attempts == 1.
        ///
        /// Post-fix behavior (MetadataRetryHelper): attempts == 2 and the second attempt
        /// returns successfully.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_CrossRegionRetryExecutes_EvenWhenCallerTokenCancelled()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.Zero));

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            int attempts = 0;
            CancellationToken secondAttemptToken = default;

            int result = await MetadataRetryHelper.ExecuteAsync<int>(
                (ct) =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        throw new DocumentClientException(
                            "503 from unhealthy region",
                            HttpStatusCode.ServiceUnavailable,
                            SubStatusCodes.Unknown);
                    }

                    // Capture the token observed by the grace attempt so the detached-token
                    // contract can be asserted below.
                    secondAttemptToken = ct;
                    return Task.FromResult(100);
                },
                policy.Object,
                MetadataRetryHelper.DefaultCrossRegionRetryGrace,
                cts.Token);

            Assert.AreEqual(100, result, "Cross-region retry should have executed despite cancelled caller token.");
            Assert.AreEqual(2, attempts, "Exactly one cross-region retry attempt should run on the grace token.");

            // Pin the detached-grace-token contract: the grace attempt MUST NOT receive the
            // caller's already-cancelled token. A future refactor that accidentally passes
            // cancellationToken (instead of graceCts.Token) would silently reintroduce the
            // defect this helper was built to fix — this assertion catches that regression.
            Assert.IsFalse(
                secondAttemptToken.IsCancellationRequested,
                "Grace attempt must receive a fresh, non-cancelled token decoupled from the caller's cancelled token.");
            Assert.AreNotEqual(
                cts.Token,
                secondAttemptToken,
                "Grace attempt token must not be the caller's cancellation token.");
        }

        /// <summary>
        /// Verifies the bounded grace: if the caller's token is cancelled AND the cross-region
        /// retry attempt itself also fails with a retriable error, the helper does not loop
        /// indefinitely. It grants at most one grace window and then surfaces the original
        /// exception (not the grace-timeout exception).
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_GraceIsBounded_SurfacesOriginalExceptionOnSecondFailure()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.Zero));

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            int attempts = 0;

            DocumentClientException thrown = await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => MetadataRetryHelper.ExecuteAsync<int>(
                    (_) =>
                {
                    attempts++;
                        throw new DocumentClientException(
                            $"503 attempt {attempts}",
                            HttpStatusCode.ServiceUnavailable,
                            SubStatusCodes.Unknown);
                    },
                    policy.Object,
                    MetadataRetryHelper.DefaultCrossRegionRetryGrace,
                    cts.Token));

            Assert.AreEqual(2, attempts, "Exactly one grace-window retry should run; then original is surfaced.");
            StringAssert.Contains(thrown.Message, "503 attempt 1");
        }

        /// <summary>
        /// Caller token cancelled and the retry policy says "no retry" (e.g. 404, throttling exhausted).
        /// The helper must surface the original exception, not OperationCanceledException.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_PolicyDeniesRetry_SurfacesOriginalExceptionEvenWhenCancelled()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.NoRetry());

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            int attempts = 0;

            DocumentClientException thrown = await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => MetadataRetryHelper.ExecuteAsync<int>(
                    (_) =>
                {
                    attempts++;
                        throw new DocumentClientException(
                            "404",
                            HttpStatusCode.NotFound,
                            SubStatusCodes.Unknown);
                    },
                    policy.Object,
                    MetadataRetryHelper.DefaultCrossRegionRetryGrace,
                    cts.Token));

            Assert.AreEqual(1, attempts);
            StringAssert.Contains(thrown.Message, "404");
        }

        /// <summary>
        /// If the grace window is zero and the caller token is cancelled, the helper must
        /// not attempt a cross-region retry. Original exception is surfaced on the first failure.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_ZeroGrace_DoesNotRetryAfterCancellation()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.Zero));

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            int attempts = 0;

            DocumentClientException thrown = await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => MetadataRetryHelper.ExecuteAsync<int>(
                    (_) =>
                {
                    attempts++;
                        throw new DocumentClientException(
                            "503",
                            HttpStatusCode.ServiceUnavailable,
                            SubStatusCodes.Unknown);
                    },
                    policy.Object,
                    TimeSpan.Zero,
                    cts.Token));

            Assert.AreEqual(1, attempts);
            StringAssert.Contains(thrown.Message, "503");
        }

        /// <summary>
        /// If the grace window itself expires while the cross-region attempt is still in flight,
        /// the helper surfaces the ORIGINAL underlying exception (not the grace-timeout OCE).
        /// This prevents leaking internal cancellation semantics to callers.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_GraceExpires_SurfacesOriginalException()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.Zero));

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            int attempts = 0;

            DocumentClientException thrown = await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => MetadataRetryHelper.ExecuteAsync<int>(
                    async (ct) =>
                {
                    attempts++;
                        if (attempts == 1)
                        {
                            throw new DocumentClientException(
                                "original 503",
                                HttpStatusCode.ServiceUnavailable,
                                SubStatusCodes.Unknown);
                        }

                        // Second attempt: observes the grace token and completes deterministically
                        // only when the grace CTS fires. Using TaskCompletionSource + ct.Register
                        // avoids the long Task.Delay worst-case on slow CI runners.
                        TaskCompletionSource<int> tcs = new TaskCompletionSource<int>(
                            TaskCreationOptions.RunContinuationsAsynchronously);
                        using (ct.Register(() => tcs.TrySetCanceled(ct)))
                        {
                            return await tcs.Task.ConfigureAwait(false);
                        }
                    },
                    policy.Object,
                    TimeSpan.FromMilliseconds(100),
                    cts.Token));

            Assert.AreEqual(2, attempts);
            StringAssert.Contains(thrown.Message, "original 503");
        }

        /// <summary>
        /// COMPANION REPRO: exercises the legacy <c>TaskHelper.InlineIfPossible</c> +
        /// <c>BackoffRetryUtility</c> path that <c>ClientCollectionCache</c> used prior to the
        /// fix. It demonstrates that, under the same conditions where the fixed
        /// <see cref="MetadataRetryHelper"/> succeeds with a cross-region retry
        /// (<see cref="ExecuteAsync_CrossRegionRetryExecutes_EvenWhenCallerTokenCancelled"/>),
        /// the legacy path throws <see cref="OperationCanceledException"/> without ever
        /// executing the cross-region attempt that the retry policy requested.
        ///
        /// This test is the canonical proof of Defect A and ensures that if anyone
        /// reverts the fix by calling <c>TaskHelper.InlineIfPossible</c> for a metadata
        /// read in <c>ClientCollectionCache</c>, the bug surfaces here.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task LegacyBackoffRetryUtility_CrossRegionRetryIsPreempted_ByCancelledCallerToken()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.Zero));

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            int attempts = 0;

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => TaskHelper.InlineIfPossible(
                    () =>
                    {
                        attempts++;
                        if (attempts == 1)
                        {
                            throw new DocumentClientException(
                                "503 from unhealthy region",
                                HttpStatusCode.ServiceUnavailable,
                                SubStatusCodes.Unknown);
                        }

                        return Task.FromResult(100);
                    },
                    policy.Object,
                    cts.Token));

            Assert.IsTrue(
                attempts <= 1,
                $"Legacy BackoffRetryUtility path preempts cross-region retry once caller token is cancelled — " +
                $"expected at most one attempt (possibly zero if ThrowIfCancellationRequested fires first), got {attempts}. " +
                $"If this starts asserting on attempts==2, someone improved the shared retry utility to consult the policy " +
                $"before honoring cancellation — in that case the bespoke MetadataRetryHelper may be removable.");
        }

        /// <summary>
        /// Regression guard: when the very first operation attempt throws
        /// <see cref="OperationCanceledException"/> (caller token already cancelled and the
        /// inner HTTP call honors it immediately), the helper must still consult the retry
        /// policy before surfacing the OCE. If the policy returns NoRetry, the OCE is
        /// surfaced. This test pins the "policy is consulted FIRST" invariant.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_FirstAttemptOCE_PolicyIsConsultedBeforeSurfacing()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.NoRetry());

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            int attempts = 0;

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => MetadataRetryHelper.ExecuteAsync<int>(
                    (ct) =>
                    {
                        attempts++;
                        ct.ThrowIfCancellationRequested();
                        return Task.FromResult(42);
                    },
                    policy.Object,
                    cts.Token));

            Assert.AreEqual(1, attempts);
            policy.Verify(
                p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce,
                "Retry policy must be consulted even on first-attempt OCE. If not, the helper's core invariant is broken.");
        }

        /// <summary>
        /// Regression guard for finding #5: when <see cref="ShouldRetryResult.NoRetry(Exception)"/>
        /// is invoked with a policy-specified translated exception, the helper must surface
        /// THAT exception (matching <c>BackoffRetryUtility.ThrowIfDoneTrying</c>), not the
        /// original captured exception. If this invariant is broken, retry policies that rewrite
        /// error types (e.g. <c>NonRetriableInvalidPartitionExceptionRetryPolicy</c> translating
        /// a gone/invalid-partition into a <c>NotFoundException</c>) would silently diverge in
        /// behavior when wired through <see cref="MetadataRetryHelper"/>.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_PolicySpecifiesExceptionToThrow_SurfacesTranslatedException()
        {
            InvalidOperationException translated = new InvalidOperationException("policy-translated");
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.NoRetry(translated));

            int attempts = 0;

            InvalidOperationException thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => MetadataRetryHelper.ExecuteAsync<int>(
                    (_) =>
                    {
                        attempts++;
                        throw new DocumentClientException(
                            "original 503",
                            HttpStatusCode.ServiceUnavailable,
                            SubStatusCodes.Unknown);
                    },
                    policy.Object,
                    CancellationToken.None));

            Assert.AreEqual(1, attempts);
            Assert.AreSame(
                translated,
                thrown,
                "Helper must surface ShouldRetryResult.ExceptionToThrow (policy-translated), not the original exception.");
        }

        /// <summary>
        /// Regression guard: if a retry policy always returns <c>ShouldRetry=true</c>, the
        /// defensive hard cap (<c>MaxAttemptsHardCap</c> = 20) must fire and surface an
        /// <see cref="InvalidOperationException"/> rather than spinning indefinitely.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_ExceedsMaxAttemptsHardCap_ThrowsInvalidOperationException()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.Zero));

            int attempts = 0;

            InvalidOperationException thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => MetadataRetryHelper.ExecuteAsync<int>(
                    (_) =>
                    {
                        attempts++;
                        throw new Exception("always fail");
                    },
                    policy.Object,
                    CancellationToken.None));

            Assert.AreEqual(20, attempts);
            StringAssert.Contains(thrown.Message, "defensive attempt cap");
        }

        /// <summary>
        /// Regression guard: when <c>ShouldRetryAsync</c> itself throws (e.g. a bug in the
        /// retry policy), the helper must surface the ORIGINAL operation exception rather than
        /// the policy's internal exception. The policy error is traced but not propagated.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_PolicyThrowsDuringShouldRetry_SurfacesOriginalException()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("policy internal error"));

            int attempts = 0;

            DocumentClientException thrown = await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => MetadataRetryHelper.ExecuteAsync<int>(
                    (_) =>
                    {
                        attempts++;
                        throw new DocumentClientException(
                            "original 503",
                            HttpStatusCode.ServiceUnavailable,
                            SubStatusCodes.Unknown);
                    },
                    policy.Object,
                    CancellationToken.None));

            Assert.AreEqual(1, attempts);
            StringAssert.Contains(thrown.Message, "original 503");
        }

        /// <summary>
        /// Negative grace windows are invalid and must be rejected at the API boundary rather
        /// than silently collapsed to "disabled". This prevents subtle misuse.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_NegativeGrace_Throws()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();

            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                () => MetadataRetryHelper.ExecuteAsync<int>(
                    (ct) => Task.FromResult(1),
                    policy.Object,
                    TimeSpan.FromSeconds(-1),
                    CancellationToken.None));
        }
    }
}

