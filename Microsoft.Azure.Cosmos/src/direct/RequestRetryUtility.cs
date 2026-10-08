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

    internal static class RequestRetryUtility
    {
        public static Task<IRetriableResponse> ProcessRequestAsync<TInitialArguments, TRequest, IRetriableResponse>(
            Func<TInitialArguments, Task<IRetriableResponse>> executeAsync,
            Func<TRequest> prepareRequest,
            IRequestRetryPolicy<TInitialArguments, TRequest, IRetriableResponse> policy,
            CancellationToken cancellationToken)
        {
            return RequestRetryUtility.ProcessRequestAsync<TRequest, IRetriableResponse>(
                executeAsync: () => executeAsync(policy.ExecuteContext),
                prepareRequest: prepareRequest,
                policy: policy,
                cancellationToken: cancellationToken
                );
        }

        public static Task<IRetriableResponse> ProcessRequestAsync<TInitialArguments, TRequest, IRetriableResponse>(
            Func<TInitialArguments, Task<IRetriableResponse>> executeAsync,
            Func<TRequest> prepareRequest,
            IRequestRetryPolicy<TInitialArguments, TRequest, IRetriableResponse> policy,
            Func<TInitialArguments, Task<IRetriableResponse>> inBackoffAlternateCallbackMethod,
            TimeSpan minBackoffForInBackoffCallback,
            CancellationToken cancellationToken)
        {
            if (inBackoffAlternateCallbackMethod != null)
            {
                return RequestRetryUtility.ProcessRequestAsync<TRequest, IRetriableResponse>(
                    executeAsync: () => executeAsync(policy.ExecuteContext),
                    prepareRequest: prepareRequest,
                    policy: policy,
                    cancellationToken: cancellationToken,
                    inBackoffAlternateCallbackMethod: () => inBackoffAlternateCallbackMethod(policy.ExecuteContext),
                    minBackoffForInBackoffCallback: minBackoffForInBackoffCallback
                    );
            }

            return RequestRetryUtility.ProcessRequestAsync<TRequest, IRetriableResponse>(
                executeAsync: () => executeAsync(policy.ExecuteContext),
                prepareRequest: prepareRequest,
                policy: policy,
                cancellationToken: cancellationToken
                );
        }

        public static async Task<IRetriableResponse> ProcessRequestAsync<TRequest, IRetriableResponse>(
            Func<Task<IRetriableResponse>> executeAsync,
            Func<TRequest> prepareRequest,
            IRequestRetryPolicy<TRequest, IRetriableResponse> policy,
            CancellationToken cancellationToken,
            Func<Task<IRetriableResponse>> inBackoffAlternateCallbackMethod = null,
            TimeSpan? minBackoffForInBackoffCallback = null)
        {
            while (true)
            {
                try
                {
                    IRetriableResponse response = default(IRetriableResponse);
                    Exception exception = null;
                    ExceptionDispatchInfo capturedException = null;
                    TRequest request = default(TRequest);
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        request = prepareRequest();
                        policy.OnBeforeSendRequest(request);

                        response = await executeAsync();
                    }
                    catch (Exception ex)
                    {
                        // this Yield is to "reset" the stack to avoid stack overflows in Framework
                        // and to keep the total size of the StackTrace down if we fail
                        await Task.Yield();

                        capturedException = ExceptionDispatchInfo.Capture(ex);
                        exception = capturedException.SourceException;
                    }

                    ShouldRetryResult shouldRetry = null;
                    Debug.Assert(response != null || exception != null);
                    if (!policy.TryHandleResponseSynchronously(request, response, exception, out shouldRetry))
                    {
                        shouldRetry = await policy.ShouldRetryAsync(request, response, exception, cancellationToken);
                    }

                    if (!shouldRetry.ShouldRetry)
                    {
                        if (capturedException != null || shouldRetry.ExceptionToThrow != null)
                        {
                            shouldRetry.ThrowIfDoneTrying(capturedException);
                        }

                        return response;
                    }

                    TimeSpan backoffTime = shouldRetry.BackoffTime;
                    if (inBackoffAlternateCallbackMethod != null && backoffTime >= minBackoffForInBackoffCallback.Value)
                    {
                        Stopwatch stopwatch = new Stopwatch();
                        try
                        {
                            stopwatch.Start();
                            IRetriableResponse inBackoffResponse = await inBackoffAlternateCallbackMethod();
                            stopwatch.Stop();
                            ShouldRetryResult shouldRetryInBackOff = null;
                            Debug.Assert(inBackoffResponse != null);
                            if (!policy.TryHandleResponseSynchronously(
                                request: request,
                                response: inBackoffResponse,
                                exception: null,
                                shouldRetryResult: out shouldRetryInBackOff))
                            {
                                shouldRetryInBackOff = await policy.ShouldRetryAsync(
                                    request: request,
                                    response: inBackoffResponse,
                                    exception: null,
                                    cancellationToken: cancellationToken);
                            }

                            if (!shouldRetryInBackOff.ShouldRetry)
                            {
                                return inBackoffResponse;
                            }

                            DefaultTrace.TraceInformation("Failed inBackoffAlternateCallback with response, proceeding with retry. Time taken: {0}ms", stopwatch.ElapsedMilliseconds);
                        }
                        catch (Exception ex)
                        {
                            stopwatch.Stop();
                            DefaultTrace.TraceInformation("Failed inBackoffAlternateCallback with {0}, proceeding with retry. Time taken: {1}ms", ex.Message, stopwatch.ElapsedMilliseconds);
                        }

                        backoffTime = shouldRetry.BackoffTime > stopwatch.Elapsed ? shouldRetry.BackoffTime - stopwatch.Elapsed : TimeSpan.Zero;
                    }

                    if (backoffTime != TimeSpan.Zero)
                    {
                        await Task.Delay(backoffTime, cancellationToken);
                    }

                    // if we're going to retry, force an additional async continuation so we don't have a gigantic
                    // stack built up by all these retries
                    await Task.Yield();
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
        /// Exceptionless retry loop with context-aware execute delegate.
        /// Unwraps <paramref name="policy"/>.ExecuteContext and delegates to the
        /// 2-type-param overload.
        /// </summary>
        public static Task<Res<IRetriableResponse>> TryProcessRequestAsync<TInitialArguments, TRequest, IRetriableResponse>(
            Func<TInitialArguments, Task<Res<IRetriableResponse>>> executeAsync,
            Func<TRequest> prepareRequest,
            IRequestRetryPolicy<TInitialArguments, TRequest, IRetriableResponse> policy,
            CancellationToken cancellationToken)
        {
            return RequestRetryUtility.TryProcessRequestAsync<TRequest, IRetriableResponse>(
                executeAsync: () => executeAsync(policy.ExecuteContext),
                prepareRequest: prepareRequest,
                policy: policy,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Exceptionless retry loop. The <paramref name="executeAsync"/> returns
        /// <see cref="Result{T}"/> instead of throwing. Exceptions carried inside the
        /// result are passed to the <paramref name="policy"/> to decide whether to retry.
        /// </summary>
        public static async Task<Res<IRetriableResponse>> TryProcessRequestAsync<TRequest, IRetriableResponse>(
            Func<Task<Res<IRetriableResponse>>> executeAsync,
            Func<TRequest> prepareRequest,
            IRequestRetryPolicy<TRequest, IRetriableResponse> policy,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Res.FromException<IRetriableResponse>(new OperationCanceledException(cancellationToken));
                }

                TRequest request = prepareRequest();
                policy.OnBeforeSendRequest(request);

                Res<IRetriableResponse> result;
                Exception escapedException = null;
                try
                {
                    result = await executeAsync();
                }
                catch (Exception ex)
                {
                    // Not every layer below this loop is exceptionless yet. Address resolution in
                    // particular still throws (for example InvalidPartitionException when the
                    // collection no longer exists). Capture those the same way the throwing loop
                    // does so the retry policy still sees them; otherwise they escape unretried and
                    // an intermediate status such as 410/NameCacheIsStale reaches the caller instead
                    // of the terminal status the retry would have resolved to.
                    //
                    // The Yield resets the stack to avoid deep async continuation chains on retry.
                    await Task.Yield();

                    escapedException = ex;
                    result = Res.FromException<IRetriableResponse>(ex);
                }

                IRetriableResponse response = result.IsSuccess ? result.Value : default;
                Exception exception = result.Exception;

                ShouldRetryResult shouldRetry;
                if (!policy.TryHandleResponseSynchronously(request, response, exception, out shouldRetry))
                {
                    try
                    {
                        shouldRetry = await policy.ShouldRetryAsync(request, response, exception, cancellationToken);
                    }
                    catch (Exception retryPolicyException)
                    {
                        return Res.FromException<IRetriableResponse>(retryPolicyException);
                    }
                }

                if (escapedException != null)
                {
                    // Reported after the policy resolves so retryable is the policy's real verdict
                    // rather than a guess. A retryable escape is the dangerous kind: before this
                    // loop caught it the policy never ran, so the client could receive an
                    // intermediate status instead of the terminal one.
                    ExceptionlessEscapeTrace.TraceEscape(
                        ExceptionlessEscapeTrace.RequestRetryUtilityLoop,
                        escapedException,
                        shouldRetry.ShouldRetry);
                }

                if (!shouldRetry.ShouldRetry)
                {
                    if (exception != null || shouldRetry.ExceptionToThrow != null)
                    {
                        Exception exToReturn = shouldRetry.ExceptionToThrow ?? exception;
                        return Res.FromException<IRetriableResponse>(exToReturn);
                    }

                    return result;
                }

                if (shouldRetry.BackoffTime != TimeSpan.Zero)
                {
                    Exception delayException = await Res.Wrap(Task.Delay(shouldRetry.BackoffTime, cancellationToken));
                    if (delayException != null)
                    {
                        return Res.FromException<IRetriableResponse>(delayException);
                    }
                }
            }
        }
    }
}
