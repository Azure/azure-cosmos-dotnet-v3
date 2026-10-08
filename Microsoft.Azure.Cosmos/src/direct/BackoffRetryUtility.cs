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

        /// <remarks>
        /// On an exception if this methiod is not attempting another retry it throws the last exception encountered. Not an Aggregation Exception of All Exceptions Encountered.
        /// </remarks>
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
        /// <remarks>
        /// On an exception if this methiod is not attempting another retry it throws the last exception encountered. Not an Aggregation Exception of All Exceptions Encountered.
        /// </remarks>
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
                try
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
                        // this Yield is to "reset" the stack to avoid stack overflows in Framework
                        // and to keep the total size of the StackTrace down if we fail
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
                            DefaultTrace.TraceInformation("Failed inBackoffAlternateCallback with {0}, proceeding with retry. Time taken: {1}ms", ex.Message, elapsed.TotalMilliseconds);
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
                catch
                {
                    // if we're going to completely fail, we want to toss all the async continuation
                    // stack frames so we don't have a gigantic stack trace (which has serious performance
                    // implications)
                    await Task.Yield();

                    throw;
                }
            }
        }

        /// <summary>
        /// Exceptionless retry loop. The <paramref name="callbackMethod"/> returns
        /// <see cref="Result{T}"/> instead of throwing. Exceptions carried inside the
        /// result are passed to the <paramref name="retryPolicy"/> to decide whether
        /// to retry.
        /// </summary>
        /// <remarks>
        /// Mirrors the SDK-specific behavior: does not check cancellationToken between
        /// capturing an error and evaluating the retry policy.
        /// </remarks>
        public static async Task<Res<T>> TryExecuteAsync(
            Func<Task<Res<T>>> callbackMethod,
            IRetryPolicy retryPolicy,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Res.FromException<T>(new OperationCanceledException(cancellationToken));
                }

                Res<T> result;
                Exception escapedException = null;
                try
                {
                    result = await callbackMethod();
                }
                catch (Exception ex)
                {
                    // Not every layer below this loop is exceptionless yet. Address resolution in
                    // particular still throws (for example InvalidPartitionException when the
                    // collection no longer exists, or GoneException when no primary replica is
                    // present). Capture those the same way the throwing loop does so the retry
                    // policy still sees them; otherwise they escape unretried and an intermediate
                    // status reaches the caller instead of the terminal status the retry would
                    // have resolved to.
                    //
                    // The Yield resets the stack to avoid deep async continuation chains on retry.
                    await Task.Yield();

                    escapedException = ex;
                    result = Res.FromException<T>(ex);
                }

                if (result.IsSuccess)
                {
                    return result;
                }

                ShouldRetryResult shouldRetry;
                try
                {
                    shouldRetry = await retryPolicy.ShouldRetryAsync(result.Exception, cancellationToken);
                }
                catch (Exception retryPolicyException)
                {
                    return Res.FromException<T>(retryPolicyException);
                }

                if (escapedException != null)
                {
                    // Reported after the policy resolves so retryable is the policy's real verdict
                    // rather than a guess. A retryable escape is the dangerous kind: before this
                    // loop caught it the policy never ran, so the client could receive an
                    // intermediate status instead of the terminal one.
                    ExceptionlessEscapeTrace.TraceEscape(
                        ExceptionlessEscapeTrace.BackoffRetryUtilityLoop,
                        escapedException,
                        shouldRetry.ShouldRetry);
                }

                if (shouldRetry.ShouldIgnoreException.HasValue && shouldRetry.ShouldIgnoreException.Value)
                {
                    DefaultTrace.TraceInformation("Ignoring exception since ShouldIgnoreException is true. Exception: {0}", result.Exception.Message);
                    return Res.Success<T>(default);
                }

                if (!shouldRetry.ShouldRetry)
                {
                    if (shouldRetry.ExceptionToThrow != null)
                    {
                        return Res.FromException<T>(shouldRetry.ExceptionToThrow);
                    }

                    return result;
                }

                if (shouldRetry.BackoffTime != TimeSpan.Zero)
                {
                    Exception delayException = await Res.Wrap(Task.Delay(shouldRetry.BackoffTime, cancellationToken));
                    if (delayException != null)
                    {
                        return Res.FromException<T>(delayException);
                    }
                }
            }
        }
    }
}
