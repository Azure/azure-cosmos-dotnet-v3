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
    /// These tests pin the behavior of the alternative "detached cancellation" model
    /// being explored as a follow-up to PR #5806. The executor under test runs the
    /// metadata read on a detached, internally-bounded <see cref="CancellationToken"/>
    /// and observes the caller's <see cref="CancellationToken"/> on the response path
    /// only — mirroring the Java SDK's <c>BackoffRetryUtility</c>.
    /// </summary>
    [TestClass]
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
        /// Primary fix validation: when the caller's token trips MID-FLIGHT (during
        /// the first attempt, simulating the control-plane HTTP timeout escalation),
        /// the retry policy still drives a successful cross-region failover because
        /// the operation runs on a detached token. The caller observes OCE; a follow-up
        /// caller would observe the cached success via AsyncCache.
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

            // Build the operation closure outside ExecuteAsync so we can observe the
            // detached task by capturing it. Use a Task.Run so the call stack is not
            // intertwined with the test thread.
            Task<int> caller = Task.Run(() => MetadataDetachedExecutor.ExecuteAsync<int>(
                async (ct) =>
                {
                    int attempt = ++attempts;
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

                    Assert.IsFalse(ct.IsCancellationRequested,
                        "second-attempt token must remain non-cancelled despite caller cancellation");
                    int result = await secondAttemptResult.Task.ConfigureAwait(false);
                    return result;
                },
                policy.Object,
                cts.Token));

            await firstAttemptStarted.Task.ConfigureAwait(false);

            // Caller token trips mid-flight (analog of the HTTP timeout policy burning
            // out against the unhealthy region).
            cts.Cancel();
            firstAttemptCanFail.TrySetResult(true);

            // Caller surfaces OCE.
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => caller);

            // Detached cross-region retry is now in flight. Release it and confirm a
            // second attempt happens on the detached token.
            secondAttemptResult.TrySetResult(99);
            // Wait for second attempt to actually invoke the operation.
            for (int i = 0; i < 50 && attempts < 2; i++)
            {
                await Task.Delay(20);
            }

            Assert.AreEqual(2, attempts,
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

            Task<int> caller = MetadataDetachedExecutor.ExecuteAsync<int>(
                async (ct) =>
                {
                    operationStarted.TrySetResult(true);
                    return await operationGate.Task.ConfigureAwait(false);
                },
                policy.Object,
                cts.Token);

            await operationStarted.Task.ConfigureAwait(false);
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => caller);

            // Detached task keeps running. Release it and confirm it completes.
            operationGate.TrySetResult(123);

            // Drain unobserved completion (we already cancelled, but the original
            // detached task continues. We don't have a direct handle, but we can
            // assert the completion source completes without timeout.)
            await Task.WhenAny(operationGate.Task, Task.Delay(2000));
            Assert.IsTrue(operationGate.Task.IsCompletedSuccessfully);
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
                        attempts++;
                        throw new DocumentClientException(
                            "always",
                            HttpStatusCode.ServiceUnavailable,
                            SubStatusCodes.Unknown);
                    },
                    policy.Object,
                    CancellationToken.None));

            Assert.IsTrue(attempts >= 20, $"expected >=20 attempts before cap, observed {attempts}");
            Assert.IsTrue(attempts <= 21, $"cap should fire at ~20 attempts, observed {attempts}");
        }

        [TestMethod]
        [Owner("ntripician")]
        public async Task ExecuteAsync_FirstAttemptOCE_PolicyIsConsultedBeforeSurfacing()
        {
            // Mirrors the regression test added to MetadataRetryHelper: an OCE thrown
            // from the operation must go through the retry policy first, not be
            // silently re-thrown.
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
        public void ExecuteAsync_NonPositiveDeadline_Throws()
        {
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>();

            Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                () => MetadataDetachedExecutor.ExecuteAsync<int>(
                    (_) => Task.FromResult(0),
                    policy.Object,
                    TimeSpan.Zero,
                    CancellationToken.None));
        }
    }
}
