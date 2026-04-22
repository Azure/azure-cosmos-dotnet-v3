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
                .SetupSequence(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.RetryAfter(TimeSpan.Zero))
                .ReturnsAsync(ShouldRetryResult.NoRetry());

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

            int result = await MetadataRetryHelper.ExecuteAsync<int>(
                (_) =>
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
                MetadataRetryHelper.DefaultCrossRegionRetryGrace,
                cts.Token);

            Assert.AreEqual(100, result, "Cross-region retry should have executed despite cancelled caller token.");
            Assert.AreEqual(2, attempts, "Exactly one cross-region retry attempt should run on the grace token.");
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

                        // Second attempt: observes the grace token and will throw OCE when it expires.
                        await Task.Delay(TimeSpan.FromSeconds(30), ct);
                        return 0;
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
    }
}

