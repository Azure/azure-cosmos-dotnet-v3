//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class BackoffRetryUtility<T>
    {
        public const string ExceptionSourceToIgnoreForIgnoreForRetry = "BackoffRetryUtility";

        public static Task<T> ExecuteAsync(
            Func<Task<T>> callbackMethod,
            IRetryPolicy retryPolicy,
            CancellationToken cancellationToken = default,
            Action<Exception> preRetryCallback = null)
        {
            return ExecuteRetryAsync(
                () => callbackMethod(),
                (Exception exception, CancellationToken token) => retryPolicy.ShouldRetryAsync(exception, cancellationToken),
                null,
                TimeSpan.Zero,
                cancellationToken,
                preRetryCallback);
        }

        public static Task<T> ExecuteAsync<TPolicyArg1>(
            Func<TPolicyArg1, Task<T>> callbackMethod,
            IRetryPolicy<TPolicyArg1> retryPolicy,
            CancellationToken cancellationToken = default,
            Action<Exception> preRetryCallback = null)
        {
            TPolicyArg1 policyArg1 = retryPolicy.InitialArgumentValue;

            return ExecuteRetryAsync(
                () => callbackMethod(policyArg1),
                async (exception, token) =>
                {
                    ShouldRetryResult<TPolicyArg1> result = await retryPolicy.ShouldRetryAsync(exception, cancellationToken);
                    policyArg1 = result.PolicyArg1;
                    return result;
                },
                null,
                TimeSpan.Zero,
                cancellationToken,
                preRetryCallback);
        }

        public static Task<T> ExecuteAsync(
            Func<Task<T>> callbackMethod,
            IRetryPolicy retryPolicy,
            Func<Task<T>> inBackoffAlternateCallbackMethod,
            TimeSpan minBackoffForInBackoffCallback,
            CancellationToken cancellationToken = default,
            Action<Exception> preRetryCallback = null)
        {
            Func<Task<T>> inBackoffAlternateCallbackMethodAsync = null;
            if (inBackoffAlternateCallbackMethod != null)
            {
                inBackoffAlternateCallbackMethodAsync = () => inBackoffAlternateCallbackMethod();
            }

            return ExecuteRetryAsync(
                () => callbackMethod(),
                (Exception exception, CancellationToken token) => retryPolicy.ShouldRetryAsync(exception, cancellationToken),
                inBackoffAlternateCallbackMethodAsync,
                minBackoffForInBackoffCallback,
                cancellationToken,
                preRetryCallback);
        }

        public static Task<T> ExecuteAsync<TPolicyArg1>(
            Func<TPolicyArg1, Task<T>> callbackMethod,
            IRetryPolicy<TPolicyArg1> retryPolicy,
            Func<TPolicyArg1, Task<T>> inBackoffAlternateCallbackMethod,
            TimeSpan minBackoffForInBackoffCallback,
            CancellationToken cancellationToken = default,
            Action<Exception> preRetryCallback = null)
        {
            TPolicyArg1 policyArg1 = retryPolicy.InitialArgumentValue;

            Func<Task<T>> inBackoffAlternateCallbackMethodAsync = null;
            if (inBackoffAlternateCallbackMethod != null)
            {
                inBackoffAlternateCallbackMethodAsync = () => inBackoffAlternateCallbackMethod(policyArg1);
            }

            return ExecuteRetryAsync(
                () => callbackMethod(policyArg1),
                async (exception, token) =>
                {
                    ShouldRetryResult<TPolicyArg1> result = await retryPolicy.ShouldRetryAsync(exception, cancellationToken);
                    policyArg1 = result.PolicyArg1;
                    return result;
                },
                inBackoffAlternateCallbackMethodAsync,
                minBackoffForInBackoffCallback,
                cancellationToken,
                preRetryCallback);
        }

        internal static async Task<T> ExecuteRetryAsync(
            Func<Task<T>> callbackMethod,
            Func<Exception, CancellationToken, Task<ShouldRetryResult>> callShouldRetry,
            Func<Task<T>> inBackoffAlternateCallbackMethod,
            TimeSpan minBackoffForInBackoffCallback,
            CancellationToken cancellationToken,
            Action<Exception> preRetryCallback = null)
        {
            while (true)
            {
                ExceptionDispatchInfo exception;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    return await callbackMethod();
                }
                catch (Exception ex)
                {
                    await Task.Yield();
                    exception = ExceptionDispatchInfo.Capture(ex);
                }

                // Don't retry if caller specified cancellation token was signaled.
                // Note that we can't simply key off of OperationCancelledException
                // here as this can be thrown independent of caller's CancellationToken
                // being signaled. For example, WinFab throws OperationCancelledException
                // when it gets E_ABORT from native code.
                cancellationToken.ThrowIfCancellationRequested();

                ShouldRetryResult result = await callShouldRetry(exception.SourceException, cancellationToken);

                result.ThrowIfDoneTrying(exception);

                TimeSpan backoffTime = result.BackoffTime;
                if (inBackoffAlternateCallbackMethod != null && result.BackoffTime >= minBackoffForInBackoffCallback)
                {
                    Stopwatch stopwatch = new Stopwatch();
                    try
                    {
                        stopwatch.Start();
                        return await inBackoffAlternateCallbackMethod();
                    }
                    catch (Exception)
                    {
                        stopwatch.Stop();
                    }

                    backoffTime = result.BackoffTime > stopwatch.Elapsed ? result.BackoffTime - stopwatch.Elapsed : TimeSpan.Zero;
                }

                preRetryCallback?.Invoke(exception.SourceException);

                if (backoffTime != TimeSpan.Zero)
                {
                    await Task.Delay(backoffTime, cancellationToken);
                }
            }
        }
    }
}