//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a shared retry loop for HTTP requests governed by an <see cref="HttpTimeoutPolicy"/>.
    /// Handles timeout enumerator management, linked cancellation token creation,
    /// user cancellation propagation, out-of-retries detection, and inter-retry delays.
    /// </summary>
    internal static class HttpTimeoutPolicyHelper
    {
        /// <summary>
        /// Executes a request with timeout policy retry semantics.
        /// </summary>
        /// <typeparam name="TResult">The type of result returned by the request.</typeparam>
        /// <param name="timeoutPolicy">The timeout policy defining per-attempt timeouts and retry delays.</param>
        /// <param name="cancellationToken">Cancellation token for user-initiated cancellation.</param>
        /// <param name="executeAsync">
        /// Function to execute the request. Receives a cancellation token that is linked to both
        /// the caller's cancellation token and the per-attempt timeout.
        /// </param>
        /// <param name="shouldRetryOnResult">
        /// Optional function to determine if a successful result should trigger a retry
        /// (e.g., response-based retry for certain HTTP status codes).
        /// Return true to retry, false to return the result. Only called when retries are available.
        /// Pass null to never retry on a successful result.
        /// </param>
        /// <param name="onException">
        /// Action called when an exception occurs (after user cancellation is already handled).
        /// Receives the exception, whether retries are exhausted, and the per-attempt request timeout.
        /// To retry, return normally. To abort, throw an appropriate exception from within the action.
        /// </param>
        internal static async Task<TResult> ExecuteWithTimeoutAsync<TResult>(
            HttpTimeoutPolicy timeoutPolicy,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<TResult>> executeAsync,
            Func<TResult, bool> shouldRetryOnResult,
            Action<Exception, bool, TimeSpan> onException)
        {
            IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> timeoutEnumerator =
                timeoutPolicy.GetTimeoutEnumerator();
            timeoutEnumerator.MoveNext();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                (TimeSpan requestTimeout, TimeSpan delayForNextRequest) = timeoutEnumerator.Current;

                using CancellationTokenSource linkedCts =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(requestTimeout);

                try
                {
                    TResult result = await executeAsync(linkedCts.Token);

                    if (shouldRetryOnResult != null && shouldRetryOnResult(result))
                    {
                        bool isOutOfRetries = !timeoutEnumerator.MoveNext();
                        if (isOutOfRetries)
                        {
                            return result;
                        }
                    }
                    else
                    {
                        return result;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    bool isOutOfRetries = !timeoutEnumerator.MoveNext();
                    onException(e, isOutOfRetries, requestTimeout);
                    // If onException returns normally, retry the request
                }

                if (delayForNextRequest != TimeSpan.Zero)
                {
                    await Task.Delay(delayForNextRequest);
                }
            }
        }
    }
}
