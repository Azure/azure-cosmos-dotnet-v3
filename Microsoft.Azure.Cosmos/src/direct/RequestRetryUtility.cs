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
                        DefaultTrace.TraceInformation("Failed inBackoffAlternateCallback with {0}, proceeding with retry. Time taken: {1}ms", ex.ToString(), stopwatch.ElapsedMilliseconds);
                    }

                    backoffTime = shouldRetry.BackoffTime > stopwatch.Elapsed ? shouldRetry.BackoffTime - stopwatch.Elapsed : TimeSpan.Zero;
                }

                if (backoffTime != TimeSpan.Zero)
                {
                    await Task.Delay(backoffTime, cancellationToken);
                }
            }
        }
    }
}
