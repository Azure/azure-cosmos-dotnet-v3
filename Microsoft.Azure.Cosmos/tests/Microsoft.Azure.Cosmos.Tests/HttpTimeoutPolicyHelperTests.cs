//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class HttpTimeoutPolicyHelperTests
    {
        [TestMethod]
        public async Task ExecuteWithTimeoutAsync_ReturnsResultOnSuccess()
        {
            HttpTimeoutPolicy policy = HttpTimeoutPolicyDefault.Instance;

            string result = await HttpTimeoutPolicyHelper.ExecuteWithTimeoutAsync(
                policy,
                CancellationToken.None,
                executeAsync: (ct) => Task.FromResult("success"),
                shouldRetryOnResult: null,
                onException: (ex, isOutOfRetries, timeout) => ex);

            Assert.AreEqual("success", result);
        }

        [TestMethod]
        public async Task ExecuteWithTimeoutAsync_RetriesOnException_WhenOnExceptionReturns()
        {
            int attemptCount = 0;
            HttpTimeoutPolicy policy = HttpTimeoutPolicyDefault.Instance; // 3 attempts

            string result = await HttpTimeoutPolicyHelper.ExecuteWithTimeoutAsync(
                policy,
                CancellationToken.None,
                executeAsync: (ct) =>
                {
                    attemptCount++;
                    if (attemptCount < 3)
                    {
                        throw new HttpRequestException("transient failure");
                    }

                    return Task.FromResult("success after retries");
                },
                shouldRetryOnResult: null,
                onException: (ex, isOutOfRetries, timeout) =>
                {
                    // Return exception to stop, null to retry
                    return isOutOfRetries ? ex : null;
                });

            Assert.AreEqual("success after retries", result);
            Assert.AreEqual(3, attemptCount);
        }

        [TestMethod]
        public async Task ExecuteWithTimeoutAsync_ThrowsWhenOutOfRetries()
        {
            int attemptCount = 0;
            // Single-attempt policy
            HttpTimeoutPolicy policy = HttpTimeoutInferencePolicy.Create(TimeSpan.FromSeconds(5));

            InvalidOperationException thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await HttpTimeoutPolicyHelper.ExecuteWithTimeoutAsync<string>(
                    policy,
                    CancellationToken.None,
                    executeAsync: (ct) =>
                    {
                        attemptCount++;
                        throw new InvalidOperationException("always fails");
                    },
                    shouldRetryOnResult: null,
                    onException: (ex, isOutOfRetries, timeout) =>
                    {
                        return isOutOfRetries
                            ? new InvalidOperationException("out of retries: " + ex.Message)
                            : null;
                    });
            });

            Assert.AreEqual(1, attemptCount);
            Assert.IsTrue(thrown.Message.Contains("out of retries"));
        }

        [TestMethod]
        public async Task ExecuteWithTimeoutAsync_PropagatesUserCancellation()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
            {
                await HttpTimeoutPolicyHelper.ExecuteWithTimeoutAsync<string>(
                    HttpTimeoutPolicyDefault.Instance,
                    cts.Token,
                    executeAsync: (ct) => Task.FromResult("should not reach"),
                    shouldRetryOnResult: null,
                    onException: (ex, isOutOfRetries, timeout) => null);
            });
        }

        [TestMethod]
        public async Task ExecuteWithTimeoutAsync_PropagatesUserCancellation_DuringExecution()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            HttpTimeoutPolicy policy = HttpTimeoutPolicyDefault.Instance;

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
            {
                await HttpTimeoutPolicyHelper.ExecuteWithTimeoutAsync<string>(
                    policy,
                    cts.Token,
                    executeAsync: (ct) =>
                    {
                        // Simulate user cancellation during request
                        cts.Cancel();
                        throw new OperationCanceledException(cts.Token);
                    },
                    shouldRetryOnResult: null,
                    onException: (ex, isOutOfRetries, timeout) =>
                    {
                        Assert.Fail("onException should not be called for user cancellation");
                        return ex;
                    });
            });
        }

        [TestMethod]
        public async Task ExecuteWithTimeoutAsync_ShouldRetryOnResult_RetriesWhenTrue()
        {
            int attemptCount = 0;
            HttpTimeoutPolicy policy = HttpTimeoutPolicyDefault.Instance; // 3 attempts

            int result = await HttpTimeoutPolicyHelper.ExecuteWithTimeoutAsync(
                policy,
                CancellationToken.None,
                executeAsync: (ct) =>
                {
                    attemptCount++;
                    return Task.FromResult(attemptCount);
                },
                shouldRetryOnResult: (r) => r < 3, // retry while result < 3
                onException: (ex, isOutOfRetries, timeout) => ex);

            Assert.AreEqual(3, result);
            Assert.AreEqual(3, attemptCount);
        }

        [TestMethod]
        public async Task ExecuteWithTimeoutAsync_ShouldRetryOnResult_ReturnsLastResultWhenOutOfRetries()
        {
            int attemptCount = 0;
            HttpTimeoutPolicy policy = HttpTimeoutInferencePolicy.Create(TimeSpan.FromSeconds(5)); // 1 attempt only

            int result = await HttpTimeoutPolicyHelper.ExecuteWithTimeoutAsync(
                policy,
                CancellationToken.None,
                executeAsync: (ct) =>
                {
                    attemptCount++;
                    return Task.FromResult(-1);
                },
                shouldRetryOnResult: (r) => true, // always wants retry
                onException: (ex, isOutOfRetries, timeout) => ex);

            // Should return the result when out of retries, not loop forever
            Assert.AreEqual(-1, result);
            Assert.AreEqual(1, attemptCount);
        }

        [TestMethod]
        public async Task ExecuteWithTimeoutAsync_ShouldRetryOnResult_NullMeansNoRetry()
        {
            int attemptCount = 0;
            HttpTimeoutPolicy policy = HttpTimeoutPolicyDefault.Instance;

            string result = await HttpTimeoutPolicyHelper.ExecuteWithTimeoutAsync(
                policy,
                CancellationToken.None,
                executeAsync: (ct) =>
                {
                    attemptCount++;
                    return Task.FromResult("first attempt");
                },
                shouldRetryOnResult: null, // null means never retry on result
                onException: (ex, isOutOfRetries, timeout) => ex);

            Assert.AreEqual("first attempt", result);
            Assert.AreEqual(1, attemptCount);
        }

        [TestMethod]
        public async Task ExecuteWithTimeoutAsync_OnException_ReceivesCorrectIsOutOfRetries()
        {
            List<bool> isOutOfRetriesValues = new List<bool>();
            HttpTimeoutPolicy policy = HttpTimeoutPolicyDefault.Instance; // 3 attempts

            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
            {
                await HttpTimeoutPolicyHelper.ExecuteWithTimeoutAsync<string>(
                    policy,
                    CancellationToken.None,
                    executeAsync: (ct) =>
                    {
                        throw new HttpRequestException("fail");
                    },
                    shouldRetryOnResult: null,
                    onException: (ex, isOutOfRetries, timeout) =>
                    {
                        isOutOfRetriesValues.Add(isOutOfRetries);
                        return isOutOfRetries ? ex : null;
                    });
            });

            // 3-attempt policy: first 2 calls should have isOutOfRetries=false, last should be true
            Assert.AreEqual(3, isOutOfRetriesValues.Count);
            Assert.IsFalse(isOutOfRetriesValues[0]);
            Assert.IsFalse(isOutOfRetriesValues[1]);
            Assert.IsTrue(isOutOfRetriesValues[2]);
        }

        [TestMethod]
        public async Task ExecuteWithTimeoutAsync_OnException_ReceivesRequestTimeout()
        {
            TimeSpan capturedTimeout = TimeSpan.Zero;
            TimeSpan configuredTimeout = TimeSpan.FromSeconds(7);
            HttpTimeoutPolicy policy = HttpTimeoutInferencePolicy.Create(configuredTimeout);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await HttpTimeoutPolicyHelper.ExecuteWithTimeoutAsync<string>(
                    policy,
                    CancellationToken.None,
                    executeAsync: (ct) =>
                    {
                        throw new InvalidOperationException("fail");
                    },
                    shouldRetryOnResult: null,
                    onException: (ex, isOutOfRetries, timeout) =>
                    {
                        capturedTimeout = timeout;
                        return ex;
                    });
            });

            Assert.AreEqual(configuredTimeout, capturedTimeout);
        }

        [TestMethod]
        public async Task ExecuteWithTimeoutAsync_OnException_CanThrowDifferentException()
        {
            HttpTimeoutPolicy policy = HttpTimeoutInferencePolicy.Create(TimeSpan.FromSeconds(5));

            CosmosException thrown = await Assert.ThrowsExceptionAsync<CosmosException>(async () =>
            {
                await HttpTimeoutPolicyHelper.ExecuteWithTimeoutAsync<string>(
                    policy,
                    CancellationToken.None,
                    executeAsync: (ct) =>
                    {
                        throw new HttpRequestException("network error");
                    },
                    shouldRetryOnResult: null,
                    onException: (ex, isOutOfRetries, timeout) =>
                    {
                        // Transform exception type (like InferenceService does)
                        return new CosmosException(
                            message: "Transformed: " + ex.Message,
                            statusCode: System.Net.HttpStatusCode.RequestTimeout,
                            subStatusCode: 0,
                            activityId: string.Empty,
                            requestCharge: 0);
                    });
            });

            Assert.IsTrue(thrown.Message.Contains("Transformed"));
        }

        [TestMethod]
        public async Task ExecuteWithTimeoutAsync_LinkedTokenCancelledOnTimeout()
        {
            // Use a very short timeout policy
            HttpTimeoutPolicy policy = new TestTimeoutPolicy(
                new List<(TimeSpan, TimeSpan)> { (TimeSpan.FromMilliseconds(50), TimeSpan.Zero) });

            bool receivedCancelledException = false;

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await HttpTimeoutPolicyHelper.ExecuteWithTimeoutAsync<string>(
                    policy,
                    CancellationToken.None,
                    executeAsync: async (ct) =>
                    {
                        // Wait longer than the timeout
                        await Task.Delay(TimeSpan.FromSeconds(5), ct);
                        return "should not reach";
                    },
                    shouldRetryOnResult: null,
                    onException: (ex, isOutOfRetries, timeout) =>
                    {
                        if (ex is OperationCanceledException)
                        {
                            receivedCancelledException = true;
                        }

                        return new InvalidOperationException("timed out");
                    });
            });

            Assert.IsTrue(receivedCancelledException, "Should receive OperationCanceledException from timeout");
        }

        [TestMethod]
        public async Task ExecuteWithTimeoutAsync_DelaysBetweenRetries()
        {
            TimeSpan retryDelay = TimeSpan.FromMilliseconds(200);
            HttpTimeoutPolicy policy = new TestTimeoutPolicy(
                new List<(TimeSpan, TimeSpan)>
                {
                    (TimeSpan.FromSeconds(30), retryDelay), // first attempt: 200ms delay
                    (TimeSpan.FromSeconds(30), TimeSpan.Zero), // second attempt: no delay
                });

            int attemptCount = 0;
            DateTime firstAttemptTime = DateTime.MinValue;
            DateTime secondAttemptTime = DateTime.MinValue;

            string result = await HttpTimeoutPolicyHelper.ExecuteWithTimeoutAsync(
                policy,
                CancellationToken.None,
                executeAsync: (ct) =>
                {
                    attemptCount++;
                    if (attemptCount == 1)
                    {
                        firstAttemptTime = DateTime.UtcNow;
                        throw new HttpRequestException("first attempt fail");
                    }

                    secondAttemptTime = DateTime.UtcNow;
                    return Task.FromResult("second attempt success");
                },
                shouldRetryOnResult: null,
                onException: (ex, isOutOfRetries, timeout) =>
                {
                    return isOutOfRetries ? ex : null;
                });

            Assert.AreEqual("second attempt success", result);
            Assert.AreEqual(2, attemptCount);

            TimeSpan elapsed = secondAttemptTime - firstAttemptTime;
            Assert.IsTrue(elapsed >= TimeSpan.FromMilliseconds(150),
                $"Expected at least 150ms delay between retries, actual: {elapsed.TotalMilliseconds}ms");
        }

        [TestMethod]
        public async Task ExecuteWithTimeoutAsync_MixedExceptionsThenSuccess()
        {
            int attemptCount = 0;
            HttpTimeoutPolicy policy = HttpTimeoutPolicyDefault.Instance; // 3 attempts

            string result = await HttpTimeoutPolicyHelper.ExecuteWithTimeoutAsync(
                policy,
                CancellationToken.None,
                executeAsync: (ct) =>
                {
                    attemptCount++;
                    if (attemptCount == 1) throw new HttpRequestException("network error");
                    if (attemptCount == 2) throw new InvalidOperationException("transient error");
                    return Task.FromResult("recovered");
                },
                shouldRetryOnResult: null,
                onException: (ex, isOutOfRetries, timeout) =>
                {
                    return isOutOfRetries ? ex : null;
                });

            Assert.AreEqual("recovered", result);
            Assert.AreEqual(3, attemptCount);
        }

        /// <summary>
        /// A test-only timeout policy with configurable timeouts and delays.
        /// </summary>
        private class TestTimeoutPolicy : HttpTimeoutPolicy
        {
            private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> timeoutsAndDelays;

            public TestTimeoutPolicy(IReadOnlyList<(TimeSpan, TimeSpan)> timeoutsAndDelays)
            {
                this.timeoutsAndDelays = timeoutsAndDelays;
            }

            public override string TimeoutPolicyName => nameof(TestTimeoutPolicy);

            public override int TotalRetryCount => this.timeoutsAndDelays.Count;

            public override IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> GetTimeoutEnumerator()
            {
                return this.timeoutsAndDelays.GetEnumerator();
            }

            public override bool ShouldRetryBasedOnResponse(HttpMethod requestHttpMethod, HttpResponseMessage responseMessage)
            {
                return false;
            }
        }
    }
}
