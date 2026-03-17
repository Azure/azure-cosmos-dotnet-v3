//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ResourceThrottleRetryPolicyTests
    {
        private readonly List<TraceListener> existingListener = new List<TraceListener>();
        private SourceSwitch existingSourceSwitch;

        [TestInitialize]
        public void CaptureCurrentTraceConfiguration()
        {
            foreach (TraceListener listener in DefaultTrace.TraceSource.Listeners)
            {
                this.existingListener.Add(listener);
            }

            DefaultTrace.TraceSource.Listeners.Clear();
            this.existingSourceSwitch = DefaultTrace.TraceSource.Switch;
        }

        [TestCleanup]
        public void ResetTraceConfiguration()
        {
            DefaultTrace.TraceSource.Listeners.Clear();
            foreach (TraceListener listener in this.existingListener)
            {
                DefaultTrace.TraceSource.Listeners.Add(listener);
            }

            DefaultTrace.TraceSource.Switch = this.existingSourceSwitch;
        }

        [TestMethod]
        public async Task DoesNotSerializeExceptionOnTracingDisabled()
        {
            // No listeners
            ResourceThrottleRetryPolicy policy = new ResourceThrottleRetryPolicy(0);
            CustomException exception = new CustomException();
            await policy.ShouldRetryAsync(exception, default);
            Assert.AreEqual(0, exception.ToStringCount, "Exception was serialized");
        }

        [TestMethod]
        public async Task DoesSerializeExceptionOnTracingEnabled()
        {
            // Let the default trace listener
            DefaultTrace.TraceSource.Switch = new SourceSwitch("ClientSwitch", "Error");
            DefaultTrace.TraceSource.Listeners.Add(new DefaultTraceListener());
            ResourceThrottleRetryPolicy policy = new ResourceThrottleRetryPolicy(0);
            CustomException exception = new CustomException();
            await policy.ShouldRetryAsync(exception, default);
            Assert.AreEqual(1, exception.ToStringCount, "Exception was not serialized");
        }

        [TestMethod]
        public async Task MissingRetryAfterHeader_RespectsMaxWaitTime()
        {
            // maxWaitTime = 12 seconds, maxAttempts = 9
            // With 5-second default fallback, only 2 retries should fit within 12s (5+5=10 <= 12, 5+5+5=15 > 12)
            int maxAttempts = 9;
            int maxWaitTimeInSeconds = 12;
            ResourceThrottleRetryPolicy policy = new ResourceThrottleRetryPolicy(
                maxAttempts,
                maxWaitTimeInSeconds);

            int retryCount = 0;
            for (int i = 0; i < maxAttempts + 1; i++)
            {
                ResponseMessage throttledResponse = ResourceThrottleRetryPolicyTests.CreateThrottledResponseWithoutRetryAfter();
                ShouldRetryResult result = await policy.ShouldRetryAsync(throttledResponse, CancellationToken.None);

                if (result.ShouldRetry)
                {
                    retryCount++;
                    Assert.AreEqual(TimeSpan.FromSeconds(5), result.BackoffTime,
                        $"Retry {retryCount}: Expected 5-second fallback delay");
                }
                else
                {
                    break;
                }
            }

            // With 12s budget and 5s per retry: 5s + 5s = 10s (ok), 5s + 5s + 5s = 15s (exceeds 12s)
            Assert.AreEqual(2, retryCount,
                "Expected exactly 2 retries within 12-second budget with 5-second fallback delays");
        }

        [TestMethod]
        public async Task PresentRetryAfterHeader_UseServerProvidedDelay()
        {
            int maxAttempts = 9;
            int maxWaitTimeInSeconds = 30;
            ResourceThrottleRetryPolicy policy = new ResourceThrottleRetryPolicy(
                maxAttempts,
                maxWaitTimeInSeconds);

            TimeSpan serverRetryAfter = TimeSpan.FromMilliseconds(1000);
            ResponseMessage throttledResponse = ResourceThrottleRetryPolicyTests.CreateThrottledResponseWithRetryAfter(serverRetryAfter);

            ShouldRetryResult result = await policy.ShouldRetryAsync(throttledResponse, CancellationToken.None);

            Assert.IsTrue(result.ShouldRetry);
            Assert.AreEqual(serverRetryAfter, result.BackoffTime,
                "Should use server-provided RetryAfter delay");
        }

        [TestMethod]
        public async Task PresentRetryAfterHeader_RespectsMaxWaitTime()
        {
            int maxAttempts = 9;
            int maxWaitTimeInSeconds = 3;
            TimeSpan serverRetryAfter = TimeSpan.FromMilliseconds(2000);
            ResourceThrottleRetryPolicy policy = new ResourceThrottleRetryPolicy(
                maxAttempts,
                maxWaitTimeInSeconds);

            // First retry: cumulative = 2s <= 3s -> should retry
            ResponseMessage response1 = ResourceThrottleRetryPolicyTests.CreateThrottledResponseWithRetryAfter(serverRetryAfter);
            ShouldRetryResult result1 = await policy.ShouldRetryAsync(response1, CancellationToken.None);
            Assert.IsTrue(result1.ShouldRetry, "First retry should succeed (cumulative 2s <= 3s)");

            // Second retry: cumulative = 4s > 3s -> should NOT retry
            ResponseMessage response2 = ResourceThrottleRetryPolicyTests.CreateThrottledResponseWithRetryAfter(serverRetryAfter);
            ShouldRetryResult result2 = await policy.ShouldRetryAsync(response2, CancellationToken.None);
            Assert.IsFalse(result2.ShouldRetry, "Second retry should fail (cumulative 4s > 3s)");
        }

        [TestMethod]
        public async Task MissingRetryAfterHeader_RespectsMaxAttemptCount()
        {
            int maxAttempts = 2;
            int maxWaitTimeInSeconds = 60; // High enough to not be the limiting factor
            ResourceThrottleRetryPolicy policy = new ResourceThrottleRetryPolicy(
                maxAttempts,
                maxWaitTimeInSeconds);

            int retryCount = 0;
            for (int i = 0; i < maxAttempts + 2; i++)
            {
                ResponseMessage throttledResponse = ResourceThrottleRetryPolicyTests.CreateThrottledResponseWithoutRetryAfter();
                ShouldRetryResult result = await policy.ShouldRetryAsync(throttledResponse, CancellationToken.None);

                if (result.ShouldRetry)
                {
                    retryCount++;
                }
                else
                {
                    break;
                }
            }

            Assert.AreEqual(maxAttempts, retryCount,
                $"Expected exactly {maxAttempts} retries when limited by max attempt count");
        }

        [TestMethod]
        public async Task NonThrottledResponse_DoesNotRetry()
        {
            ResourceThrottleRetryPolicy policy = new ResourceThrottleRetryPolicy(
                maxAttemptCount: 9,
                maxWaitTimeInSeconds: 30);

            ResponseMessage okResponse = new ResponseMessage(HttpStatusCode.OK);
            ShouldRetryResult result = await policy.ShouldRetryAsync(okResponse, CancellationToken.None);

            Assert.IsFalse(result.ShouldRetry, "Should not retry on non-429 status code");
        }

        [TestMethod]
        public async Task MissingRetryAfterHeader_CumulativeDelayTracksActualFallbackDelay()
        {
            // Verify that with maxWaitTime = 6s, exactly 1 retry is allowed
            // (first retry: cumulative = 5s <= 6s, second: cumulative = 10s > 6s)
            int maxAttempts = 9;
            int maxWaitTimeInSeconds = 6;
            ResourceThrottleRetryPolicy policy = new ResourceThrottleRetryPolicy(
                maxAttempts,
                maxWaitTimeInSeconds);

            // First retry
            ResponseMessage response1 = ResourceThrottleRetryPolicyTests.CreateThrottledResponseWithoutRetryAfter();
            ShouldRetryResult result1 = await policy.ShouldRetryAsync(response1, CancellationToken.None);
            Assert.IsTrue(result1.ShouldRetry, "First retry should succeed (cumulative 5s <= 6s)");
            Assert.AreEqual(TimeSpan.FromSeconds(5), result1.BackoffTime);

            // Second retry should fail because cumulative would be 10s > 6s
            ResponseMessage response2 = ResourceThrottleRetryPolicyTests.CreateThrottledResponseWithoutRetryAfter();
            ShouldRetryResult result2 = await policy.ShouldRetryAsync(response2, CancellationToken.None);
            Assert.IsFalse(result2.ShouldRetry,
                "Second retry should fail because cumulative delay (10s) exceeds maxWaitTime (6s)");
        }

        private static ResponseMessage CreateThrottledResponseWithoutRetryAfter()
        {
            ResponseMessage response = new ResponseMessage((HttpStatusCode)429);
            // Do NOT set RetryAfterInMilliseconds header — simulating missing x-ms-retry-after-ms
            return response;
        }

        private static ResponseMessage CreateThrottledResponseWithRetryAfter(TimeSpan retryAfter)
        {
            ResponseMessage response = new ResponseMessage((HttpStatusCode)429);
            response.Headers.Set(
                HttpConstants.HttpHeaders.RetryAfterInMilliseconds,
                retryAfter.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return response;
        }

        private class CustomException : Exception
        {
            public int ToStringCount { get; private set; } = 0;

            public override string ToString()
            {
                ++this.ToStringCount;
                return string.Empty;
            }
        }
    }
}