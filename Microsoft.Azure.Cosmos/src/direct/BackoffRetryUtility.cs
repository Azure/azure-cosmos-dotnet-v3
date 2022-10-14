//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// Utility to retry operations based off retry policies.
    /// </summary>
    /// <remarks>
    /// This implementation differs from the one in SharedFiles on the fact that it does not check the cancellationToken status between capturing an error and evaluating the retry policy.
    /// Ref: https://msdata.visualstudio.com/CosmosDB/_git/CosmosDB/pullrequest/550056
    /// </remarks>
    internal static class BackoffRetryUtility<T>
    {
        public const string ExceptionSourceToIgnoreForIgnoreForRetry = "BackoffRetryUtility";

        public static Task<T> ExecuteAsync(
            Func<Task<T>> callbackMethod,
            IRetryPolicy retryPolicy,
            CancellationToken cancellationToken = default(CancellationToken),
            Action<Exception> preRetryCallback = null)
        {
            return ExecuteRetryAsync<object, object>(
                callbackMethod,
                callbackMethodWithParam: null,
                callbackMethodWithPolicy: null,
                param: default(object),
                retryPolicy,
                retryPolicyWithArg: null,
                inBackoffAlternateCallbackMethod: null,
                inBackoffAlternateCallbackMethodWithPolicy: null,
                minBackoffForInBackoffCallback: TimeSpan.Zero,
                cancellationToken,
                preRetryCallback);
        }

        public static Task<T> ExecuteAsync<TParam>(
            Func<TParam, CancellationToken, Task<T>> callbackMethod,
            IRetryPolicy retryPolicy,
            TParam param,
            CancellationToken cancellationToken,
            Action<Exception> preRetryCallback = null)
        {
            return ExecuteRetryAsync<TParam, object>(
                callbackMethod: null,
                callbackMethodWithParam: callbackMethod,
                callbackMethodWithPolicy: null,
                param,
                retryPolicy,
                retryPolicyWithArg: null,
                inBackoffAlternateCallbackMethod: null,
                inBackoffAlternateCallbackMethodWithPolicy: null,
                minBackoffForInBackoffCallback: TimeSpan.Zero,
                cancellationToken,
                preRetryCallback);
        }

        public static Task<T> ExecuteAsync<TPolicyArg1>(
            Func<TPolicyArg1, Task<T>> callbackMethod,
            IRetryPolicy<TPolicyArg1> retryPolicy,
            CancellationToken cancellationToken = default(CancellationToken),
            Action<Exception> preRetryCallback = null)
        {
            return ExecuteRetryAsync<object, TPolicyArg1>(
                callbackMethod: null,
                callbackMethodWithParam: null,
                callbackMethodWithPolicy: callbackMethod,
                param: null,
                retryPolicy: null,
                retryPolicyWithArg: retryPolicy,
                inBackoffAlternateCallbackMethod: null,
                inBackoffAlternateCallbackMethodWithPolicy: null,
                minBackoffForInBackoffCallback: TimeSpan.Zero,
                cancellationToken,
                preRetryCallback);
        }

        public static Task<T> ExecuteAsync(
            Func<Task<T>> callbackMethod,
            IRetryPolicy retryPolicy,
            Func<Task<T>> inBackoffAlternateCallbackMethod,
            TimeSpan minBackoffForInBackoffCallback,
            CancellationToken cancellationToken = default(CancellationToken),
            Action<Exception> preRetryCallback = null)
        {
            return ExecuteRetryAsync<object, object>(
                callbackMethod,
                callbackMethodWithParam: null,
                callbackMethodWithPolicy: null,
                param: default(object),
                retryPolicy,
                retryPolicyWithArg: null,
                inBackoffAlternateCallbackMethod,
                inBackoffAlternateCallbackMethodWithPolicy: null,
                minBackoffForInBackoffCallback,
                cancellationToken,
                preRetryCallback);
        }

        public static Task<T> ExecuteAsync<TPolicyArg1>(
            Func<TPolicyArg1, Task<T>> callbackMethod,
            IRetryPolicy<TPolicyArg1> retryPolicy,
            Func<TPolicyArg1, Task<T>> inBackoffAlternateCallbackMethod,
            TimeSpan minBackoffForInBackoffCallback,
            CancellationToken cancellationToken = default(CancellationToken),
            Action<Exception> preRetryCallback = null)
        {
            return ExecuteRetryAsync<object, TPolicyArg1>(
                callbackMethod: null,
                callbackMethodWithParam: null,
                callbackMethodWithPolicy: callbackMethod,
                param: null,
                retryPolicy: null,
                retryPolicyWithArg: retryPolicy,
                inBackoffAlternateCallbackMethod: null,
                inBackoffAlternateCallbackMethodWithPolicy: inBackoffAlternateCallbackMethod,
                minBackoffForInBackoffCallback,
                cancellationToken,
                preRetryCallback);
        }

        /// <summary>
        /// Common implementation that handles all the different possible configurations.
        /// </summary>
        private static async Task<T> ExecuteRetryAsync<TParam, TPolicy>(
            Func<Task<T>> callbackMethod,
            Func<TParam, CancellationToken, Task<T>> callbackMethodWithParam,
            Func<TPolicy, Task<T>> callbackMethodWithPolicy,
            TParam param,
            IRetryPolicy retryPolicy,
            IRetryPolicy<TPolicy> retryPolicyWithArg,
            Func<Task<T>> inBackoffAlternateCallbackMethod,
            Func<TPolicy, Task<T>> inBackoffAlternateCallbackMethodWithPolicy,
            TimeSpan minBackoffForInBackoffCallback,
            CancellationToken cancellationToken,
            Action<Exception> preRetryCallback)
        {
            TPolicy policyArg1;
            if (retryPolicyWithArg != null)
            {
                policyArg1 = retryPolicyWithArg.InitialArgumentValue;
            }
            else
            {
                policyArg1 = default;
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ExceptionDispatchInfo exception;
                try
                {
                    if (callbackMethod != null)
                    {
                        return await callbackMethod();
                    }
                    else if (callbackMethodWithParam != null)
                    {
                        return await callbackMethodWithParam(param, cancellationToken);
                    }

                    return await callbackMethodWithPolicy(policyArg1);
                }
                catch (Exception ex)
                {
                    await Task.Yield();
                    exception = ExceptionDispatchInfo.Capture(ex);
                }

                ShouldRetryResult result;
                if (retryPolicyWithArg != null)
                {
                    ShouldRetryResult<TPolicy> resultWithPolicy = await retryPolicyWithArg.ShouldRetryAsync(exception.SourceException, cancellationToken);

                    policyArg1 = resultWithPolicy.PolicyArg1;
                    result = resultWithPolicy;
                }
                else
                {
                    result = await retryPolicy.ShouldRetryAsync(exception.SourceException, cancellationToken);
                }

                result.ThrowIfDoneTrying(exception);

                TimeSpan backoffTime = result.BackoffTime;
                bool hasBackoffAlternateCallback = inBackoffAlternateCallbackMethod != null || inBackoffAlternateCallbackMethodWithPolicy != null;

                if (hasBackoffAlternateCallback && result.BackoffTime >= minBackoffForInBackoffCallback)
                {
                    ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                    TimeSpan elapsed;
                    try
                    {
                        if (inBackoffAlternateCallbackMethod != null)
                        {
                            return await inBackoffAlternateCallbackMethod();
                        }

                        return await inBackoffAlternateCallbackMethodWithPolicy(policyArg1);
                    }
                    catch (Exception ex)
                    {
                        elapsed = stopwatch.Elapsed;
                        DefaultTrace.TraceInformation("Failed inBackoffAlternateCallback with {0}, proceeding with retry. Time taken: {1}ms", ex.ToString(), elapsed.TotalMilliseconds);
                    }

                    backoffTime = result.BackoffTime > elapsed ? result.BackoffTime - elapsed : TimeSpan.Zero;
                }

                if (preRetryCallback != null)
                {
                    preRetryCallback(exception.SourceException);
                }

                if (backoffTime != TimeSpan.Zero)
                {
                    await Task.Delay(backoffTime, cancellationToken);
                }
            }
        }
    }
}
