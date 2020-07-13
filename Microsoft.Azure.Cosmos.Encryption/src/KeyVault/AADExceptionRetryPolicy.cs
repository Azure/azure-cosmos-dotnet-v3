//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Identity;

    internal sealed class AADExceptionRetryPolicy : IRetryPolicy
    {
        private int maxRetries;
        private int retriesAttempted;
        private TimeSpan retryBackoffTime;

        public AADExceptionRetryPolicy(TimeSpan retryInterval, int retryCount)
        {
            this.maxRetries = retryCount;
            this.retryBackoffTime = retryInterval;
        }

        public override Task<ShouldRetryResult> ShouldRetryAsync(Exception exception, CancellationToken cancellationToken)
        {
            // TODO/FIXME the "System.NullReferenceException" here is a bug in ADAL library,will move it out once we move to Fixed Version of ADAL  lib.
            if (( exception is AuthenticationFailedException || WebExceptionUtility.IsWebExceptionRetriable(exception) || exception is System.NullReferenceException) &&
                this.retriesAttempted < this.maxRetries && !cancellationToken.IsCancellationRequested)
            {
                this.retriesAttempted++;
                DefaultTrace.TraceInformation("AADExceptionRetryPolicy will retry. Retries Attempted = {0}. Backoff time = {1} ms.  Exception = {2}.", this.retriesAttempted, this.retryBackoffTime, exception.ToString());
                return Task.FromResult(ShouldRetryResult.RetryAfter(this.retryBackoffTime));
            }
            else
            {
                DefaultTrace.TraceInformation("AADExceptionRetryPolicy will not retry. Retries Attempted = {0}. Exception = {1}.", this.retriesAttempted, exception.ToString());
                return Task.FromResult(ShouldRetryResult.NoRetry());
            }
        }
    }
}
