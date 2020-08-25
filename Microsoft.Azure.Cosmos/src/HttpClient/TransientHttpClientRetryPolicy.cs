//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Resource.CosmosExceptions;

    internal sealed class TransientHttpClientRetryPolicy : IRetryPolicy
    {
        // total wait time in seconds to retry. should be max of primary reconfigrations/replication wait duration etc
        private const int WaitTimeInSeconds = 30;
        private const int InitialBackoffSeconds = 1;
        private const int BackoffMultiplier = 2;

        private readonly DateTime startDateTimeUtc;
        private readonly Func<HttpMethod> getHttpMethod;
        private readonly TimeSpan gatewayRequestTimeout;
        private int attemptCount = 1;

        // Don't penalise first retry with delay.
        private int currentBackoffSeconds = TransientHttpClientRetryPolicy.InitialBackoffSeconds;

        public TransientHttpClientRetryPolicy(
            Func<HttpMethod> getHttpMethod,
            TimeSpan gatewayRequestTimeout)
        {
            this.startDateTimeUtc = DateTime.UtcNow;
            this.getHttpMethod = getHttpMethod;
            this.gatewayRequestTimeout = gatewayRequestTimeout;
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (!this.IsExceptionTransientRetriable(exception, cancellationToken))
            {
                // Have caller propagate original exception.
                return Task.FromResult(ShouldRetryResult.NoRetry());
            }

            TimeSpan backOffTime = TimeSpan.FromSeconds(0);
            // Don't penalise first retry with delay.
            if (this.attemptCount++ > 1)
            {
                int remainingSeconds = TransientHttpClientRetryPolicy.WaitTimeInSeconds - (int)(DateTime.UtcNow - this.startDateTimeUtc).TotalSeconds;
                if (remainingSeconds <= 0)
                {
                    ShouldRetryResult shouldRetry = this.GetShouldRetryFromException(exception);
                    return Task.FromResult(shouldRetry);
                }

                backOffTime = TimeSpan.FromSeconds(Math.Min(this.currentBackoffSeconds, remainingSeconds));
                this.currentBackoffSeconds *= TransientHttpClientRetryPolicy.BackoffMultiplier;
            }

            DefaultTrace.TraceWarning("Received retriable web exception, will retry, {0}", exception);

            return Task.FromResult(ShouldRetryResult.RetryAfter(backOffTime));
        }

        public bool IsExceptionTransientRetriable(
            Exception exception,
            CancellationToken cancellationToken)
        {
            return (!cancellationToken.IsCancellationRequested && exception is OperationCanceledException) ||
                   (exception is WebException webException && (this.getHttpMethod() == HttpMethod.Get || WebExceptionUtility.IsWebExceptionRetriable(webException)));
        }

        private ShouldRetryResult GetShouldRetryFromException(
            Exception exception)
        {
            if (exception is OperationCanceledException operationCanceledException)
            {
                // throw timeout if the cancellationToken is not canceled (i.e. httpClient timed out)
                string message =
                    $"GatewayStoreClient Request Timeout. Start Time Utc:{this.startDateTimeUtc}; Total Duration:{(DateTime.UtcNow - this.startDateTimeUtc).TotalMilliseconds} Ms; Http Client Timeout:{this.gatewayRequestTimeout.TotalMilliseconds} Ms;";
                return ShouldRetryResult.NoRetry(CosmosExceptionFactory.CreateRequestTimeoutException(
                    message,
                    innerException: operationCanceledException));
            }

            return ShouldRetryResult.NoRetry();
        }
    }
}