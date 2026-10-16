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
    public class GatewayRetryWithRetryPolicyTests
    {
        private const int DefaultWaitTimeInSeconds = 30;
        private const int MaximumBackoffTimeInMilliseconds = 1000;

        private static DocumentClientException CreateRetryWithException()
        {
            return new DocumentClientException(
                "retry with",
                (HttpStatusCode)StatusCodes.RetryWith,
                SubStatusCodes.Unknown);
        }

        [TestMethod]
        public async Task RetryWithException_WithinBudget_IsRetriedWithBackoff()
        {
            // 0ms elapsed -> the full 30s budget is available, so a 449 should be retried.
            GatewayRetryWithRetryPolicy policy = new GatewayRetryWithRetryPolicy(
                DefaultWaitTimeInSeconds,
                () => TimeSpan.Zero);

            ShouldRetryResult result = await policy.ShouldRetryAsync(
                CreateRetryWithException(),
                CancellationToken.None);

            Assert.IsTrue(result.ShouldRetry, "A 449 (RetryWith) response within the budget must be retried.");
            Assert.IsTrue(result.BackoffTime > TimeSpan.Zero, "A 449 retry must apply a non-zero backoff.");
            Assert.IsTrue(
                result.BackoffTime <= TimeSpan.FromMilliseconds(MaximumBackoffTimeInMilliseconds),
                "The backoff must never exceed the maximum backoff cap.");
        }

        [TestMethod]
        [DataRow((int)HttpStatusCode.Gone, DisplayName = "410 Gone is not a RetryWith")]
        [DataRow((int)HttpStatusCode.TooManyRequests, DisplayName = "429 throttling is not a RetryWith")]
        [DataRow((int)HttpStatusCode.NotFound, DisplayName = "404 is not a RetryWith")]
        public async Task NonRetryWithDocumentClientException_ReturnsNoRetry(int statusCode)
        {
            GatewayRetryWithRetryPolicy policy = new GatewayRetryWithRetryPolicy(
                DefaultWaitTimeInSeconds,
                () => TimeSpan.Zero);

            DocumentClientException exception = new DocumentClientException(
                "not a retry with",
                (HttpStatusCode)statusCode,
                SubStatusCodes.Unknown);

            ShouldRetryResult result = await policy.ShouldRetryAsync(exception, CancellationToken.None);

            Assert.IsFalse(result.ShouldRetry, "Only 449 (RetryWith) responses must be retried by this policy.");
        }

        [TestMethod]
        public async Task NonDocumentClientException_ReturnsNoRetry()
        {
            GatewayRetryWithRetryPolicy policy = new GatewayRetryWithRetryPolicy(
                DefaultWaitTimeInSeconds,
                () => TimeSpan.Zero);

            ShouldRetryResult result = await policy.ShouldRetryAsync(
                new InvalidOperationException("unrelated"),
                CancellationToken.None);

            Assert.IsFalse(result.ShouldRetry, "An unrelated exception must not be retried.");
        }

        [TestMethod]
        public async Task RetryBudget_IsHonoredWhenElapsedExceedsBudget()
        {
            // 31s elapsed against a 30s budget -> remaining < 0 -> the policy must stop retrying.
            GatewayRetryWithRetryPolicy policy = new GatewayRetryWithRetryPolicy(
                DefaultWaitTimeInSeconds,
                () => TimeSpan.FromSeconds(31));

            ShouldRetryResult result = await policy.ShouldRetryAsync(
                CreateRetryWithException(),
                CancellationToken.None);

            Assert.IsFalse(result.ShouldRetry, "The retry budget must be honored once elapsed time exceeds it.");
        }

        [TestMethod]
        public async Task RetryBudget_IsHonoredAtExactBoundary()
        {
            // remaining = 30s - 30s = 0, which is not > 0, so the policy must stop on the boundary.
            GatewayRetryWithRetryPolicy policy = new GatewayRetryWithRetryPolicy(
                DefaultWaitTimeInSeconds,
                () => TimeSpan.FromSeconds(DefaultWaitTimeInSeconds));

            ShouldRetryResult result = await policy.ShouldRetryAsync(
                CreateRetryWithException(),
                CancellationToken.None);

            Assert.IsFalse(result.ShouldRetry, "The retry budget must be honored exactly at the boundary.");
        }

        [TestMethod]
        public async Task Backoff_GrowsAcrossRetriesAndIsCappedAtMaximum()
        {
            // Elapsed stays at 0 so the budget is never the limiting factor; the backoff should
            // grow (roughly doubling: 10 -> 20 -> 40 -> ... ms) and saturate at the 1000ms cap.
            // A small random salt (0-4ms) is added per attempt, so exact equality is not asserted;
            // instead each backoff is bounded below by the (non-salted) doubling schedule and above
            // by that schedule plus the salt, and is always capped at the maximum.
            GatewayRetryWithRetryPolicy policy = new GatewayRetryWithRetryPolicy(
                DefaultWaitTimeInSeconds,
                () => TimeSpan.Zero);

            int expectedBaseBackoff = 10;
            TimeSpan previousBackoff = TimeSpan.Zero;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                ShouldRetryResult result = await policy.ShouldRetryAsync(
                    CreateRetryWithException(),
                    CancellationToken.None);

                Assert.IsTrue(result.ShouldRetry, "Within the budget every 449 must be retried.");

                int expectedCappedBase = Math.Min(expectedBaseBackoff, MaximumBackoffTimeInMilliseconds);
                Assert.IsTrue(
                    result.BackoffTime >= TimeSpan.FromMilliseconds(expectedCappedBase),
                    $"Attempt {attempt}: backoff {result.BackoffTime.TotalMilliseconds}ms should be at least the scheduled {expectedCappedBase}ms.");
                Assert.IsTrue(
                    result.BackoffTime <= TimeSpan.FromMilliseconds(MaximumBackoffTimeInMilliseconds),
                    $"Attempt {attempt}: backoff must never exceed the {MaximumBackoffTimeInMilliseconds}ms cap.");

                if (expectedBaseBackoff < MaximumBackoffTimeInMilliseconds)
                {
                    Assert.IsTrue(
                        result.BackoffTime >= previousBackoff,
                        $"Attempt {attempt}: backoff should not decrease while still ramping up.");
                }

                previousBackoff = result.BackoffTime;
                expectedBaseBackoff = Math.Min(expectedBaseBackoff * 2, MaximumBackoffTimeInMilliseconds);
            }

            // After enough doublings the schedule has saturated at the cap.
            Assert.AreEqual(
                TimeSpan.FromMilliseconds(MaximumBackoffTimeInMilliseconds),
                previousBackoff,
                "After saturating, the backoff should equal the maximum (no salt headroom remains under the cap).");
        }

        [TestMethod]
        public async Task Backoff_NeverExceedsRemainingBudget()
        {
            // Only 7ms remain in the budget; the backoff must be clamped to the remaining time.
            int waitTimeInSeconds = 1;
            GatewayRetryWithRetryPolicy policy = new GatewayRetryWithRetryPolicy(
                waitTimeInSeconds,
                () => TimeSpan.FromMilliseconds((waitTimeInSeconds * 1000) - 7));

            ShouldRetryResult result = await policy.ShouldRetryAsync(
                CreateRetryWithException(),
                CancellationToken.None);

            Assert.IsTrue(result.ShouldRetry, "With remaining budget the 449 must still be retried.");
            Assert.IsTrue(
                result.BackoffTime <= TimeSpan.FromMilliseconds(7),
                "The backoff must never exceed the remaining budget.");
        }
    }
}
