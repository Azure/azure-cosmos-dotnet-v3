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

    [TestClass]
    public class WebExceptionRetryPolicyTests
    {
        // A WebException with ConnectFailure is treated as retriable by
        // WebExceptionUtility.IsWebExceptionRetriable (connection was never established).
        private static Exception CreateRetriableWebException()
        {
            return new WebException("retriable", WebExceptionStatus.ConnectFailure);
        }

        [TestMethod]
        public async Task FirstRetriableWebException_IsRetriedWithoutDelay()
        {
            WebExceptionRetryPolicy policy = new WebExceptionRetryPolicy();

            ShouldRetryResult result = await policy.ShouldRetryAsync(
                CreateRetriableWebException(),
                CancellationToken.None);

            Assert.IsTrue(result.ShouldRetry, "The first retriable web exception should be retried.");
            Assert.AreEqual(TimeSpan.Zero, result.BackoffTime, "The first retry should not be penalised with a delay.");
        }

        [TestMethod]
        public async Task NonRetriableException_ReturnsNoRetryImmediately()
        {
            WebExceptionRetryPolicy policy = new WebExceptionRetryPolicy();

            ShouldRetryResult result = await policy.ShouldRetryAsync(
                new InvalidOperationException("not retriable"),
                CancellationToken.None);

            Assert.IsFalse(result.ShouldRetry, "A non-retriable exception must not be retried.");
        }

        [TestMethod]
        public async Task RetryBudget_StillRetriesWhenWithinBudget()
        {
            // 5s elapsed is well within the 30s budget, so the second attempt should still retry.
            WebExceptionRetryPolicy policy = new WebExceptionRetryPolicy(() => TimeSpan.FromSeconds(5));

            ShouldRetryResult first = await policy.ShouldRetryAsync(
                CreateRetriableWebException(),
                CancellationToken.None);
            Assert.IsTrue(first.ShouldRetry);
            Assert.AreEqual(TimeSpan.Zero, first.BackoffTime);

            ShouldRetryResult second = await policy.ShouldRetryAsync(
                CreateRetriableWebException(),
                CancellationToken.None);

            Assert.IsTrue(second.ShouldRetry, "Within the budget the policy should keep retrying.");
            Assert.AreEqual(TimeSpan.FromSeconds(1), second.BackoffTime, "Backoff should be the initial 1s within budget.");
        }

        [TestMethod]
        public async Task RetryBudget_IsHonoredAfterElapsedExceedsBudget()
        {
            // Core regression: with 65s elapsed the 30s total budget is exhausted, so the
            // second attempt must stop retrying.
            //
            // Before the fix the policy used Elapsed.Seconds (the modulo-60 component), so 65s
            // was read as 5s -> remaining = 30 - 5 = 25 > 0 -> the policy incorrectly kept
            // retrying past its 30s budget. Using Elapsed.TotalSeconds reads 65s ->
            // remaining = 30 - 65 = -35 <= 0 -> NoRetry, honoring the budget.
            WebExceptionRetryPolicy policy = new WebExceptionRetryPolicy(() => TimeSpan.FromSeconds(65));

            // First retry is never penalised and never checks the budget.
            ShouldRetryResult first = await policy.ShouldRetryAsync(
                CreateRetriableWebException(),
                CancellationToken.None);
            Assert.IsTrue(first.ShouldRetry);

            // Second attempt evaluates the budget, which is now exhausted.
            ShouldRetryResult second = await policy.ShouldRetryAsync(
                CreateRetriableWebException(),
                CancellationToken.None);

            Assert.IsFalse(second.ShouldRetry, "The 30s retry budget must be honored once total elapsed time exceeds it.");
        }

        [TestMethod]
        public async Task RetryBudget_IsHonoredAtExactBudgetBoundary()
        {
            // At exactly the 30s budget remaining = 30 - 30 = 0, which is not > 0, so the
            // policy must stop retrying on the boundary.
            WebExceptionRetryPolicy policy = new WebExceptionRetryPolicy(() => TimeSpan.FromSeconds(30));

            ShouldRetryResult first = await policy.ShouldRetryAsync(
                CreateRetriableWebException(),
                CancellationToken.None);
            Assert.IsTrue(first.ShouldRetry);

            ShouldRetryResult second = await policy.ShouldRetryAsync(
                CreateRetriableWebException(),
                CancellationToken.None);

            Assert.IsFalse(second.ShouldRetry, "The retry budget must be honored exactly at the boundary.");
        }

        [TestMethod]
        public void InternalConstructor_NullElapsedTimeDelegate_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => new WebExceptionRetryPolicy(getElapsedTime: null));
        }

        [TestMethod]
        public async Task RetryBudget_BackoffDoublesAcrossRetriesWithinBudget()
        {
            // Elapsed stays at 1s so the 30s budget is never exhausted; backoff should
            // double on each penalised attempt (1s -> 2s -> 4s), each capped by remaining budget.
            WebExceptionRetryPolicy policy = new WebExceptionRetryPolicy(() => TimeSpan.FromSeconds(1));

            ShouldRetryResult first = await policy.ShouldRetryAsync(
                CreateRetriableWebException(),
                CancellationToken.None);
            Assert.IsTrue(first.ShouldRetry);
            Assert.AreEqual(TimeSpan.Zero, first.BackoffTime, "First retry is never penalised.");

            ShouldRetryResult second = await policy.ShouldRetryAsync(
                CreateRetriableWebException(),
                CancellationToken.None);
            Assert.IsTrue(second.ShouldRetry);
            Assert.AreEqual(TimeSpan.FromSeconds(1), second.BackoffTime);

            ShouldRetryResult third = await policy.ShouldRetryAsync(
                CreateRetriableWebException(),
                CancellationToken.None);
            Assert.IsTrue(third.ShouldRetry);
            Assert.AreEqual(TimeSpan.FromSeconds(2), third.BackoffTime);

            ShouldRetryResult fourth = await policy.ShouldRetryAsync(
                CreateRetriableWebException(),
                CancellationToken.None);
            Assert.IsTrue(fourth.ShouldRetry);
            Assert.AreEqual(TimeSpan.FromSeconds(4), fourth.BackoffTime);
        }

        [TestMethod]
        public void ElapsedSeconds_VersusTotalSeconds_DocumentsBudgetBug()
        {
            // Documents the root cause of the bug class: TimeSpan.Seconds is the modulo-60
            // component, whereas TimeSpan.TotalSeconds is the full elapsed duration. Using
            // the former for a wall-clock budget under-counts elapsed time past 60s.
            TimeSpan elapsed = TimeSpan.FromSeconds(65);

            Assert.AreEqual(5, elapsed.Seconds, "TimeSpan.Seconds wraps at 60 (65 -> 5).");
            Assert.AreEqual(65, (int)elapsed.TotalSeconds, "TimeSpan.TotalSeconds reflects the full elapsed duration.");
        }
    }
}
