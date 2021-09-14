namespace Cosmos.Samples.CustomTimeoutRetry
{
    using Microsoft.Azure.Cosmos;
    using Polly;
    using Polly.Timeout;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Retryable encapsulates the logic for retrying atomic, time-bound Cosmos DB SDK operations
    /// </summary>
    internal static class Retryable
    {
        private static readonly Random _random = new Random(Environment.TickCount);

        public static async Task<Response<TItem>> ExecuteAsync<TItem>(
            Func<CancellationToken, Task<Response<TItem>>> retryableFunc,
            TimeSpan perAttemptTimeout,
            int retries,
            Action<CosmosDiagnostics, Func<TimeSpan, bool>> diagnosticsFunc,
            CancellationToken ct = default)
        {
            // we keep track of all attempts, some timed-out attempts may eventually
            //  resolve with a result or error which we can use
            List<Attempt<TItem>> attempts = new List<Attempt<TItem>>();

            while (true)
            {
                // do we have any prior timed-out attempts that have completed since we last checked?
                Attempt<TItem>? attempt = attempts.FirstOrDefault(att => att.Status == AttemptStatus.TimedOutSucceeded ||
                                                                         att.Status == AttemptStatus.TimedOutFailed);

                if (attempt != null)
                {
                    if (attempt.Status == AttemptStatus.TimedOutSucceeded)
                    {
                        // prior attempt succeeded, no need to try any more... just use this result

                        Debug.WriteLine("**********");
                        Debug.WriteLine("Prior attempt completed successfully; using that result.");
                        Debug.WriteLine("**********");

                        Debug.Assert(attempt.Result != null);

                        return attempt.Result;
                    }
                    else
                    {
                        // prior attempt failed, no need to try any more... just use this error

                        Debug.Assert(attempt.Status == AttemptStatus.TimedOutFailed);

                        Debug.WriteLine("**********");
                        Debug.WriteLine("Prior attempt completed and failed; re-throwing that exception.");
                        Debug.WriteLine("**********");

                        Debug.Assert(attempt.Exception != null);

                        ExceptionDispatchInfo.Capture(attempt.Exception).Throw();
                    }
                }

                // try a new attempt

                attempt = new Attempt<TItem>();

                attempts.Add(attempt);

                Debug.WriteLine("**********");
                Debug.WriteLine($"Starting attempt #{attempts.Count}.");
                Debug.WriteLine("**********");

                await ExecuteAttemptAsync(attempt, retryableFunc, perAttemptTimeout, diagnosticsFunc, ct);

                if (attempt.Status == AttemptStatus.Succeeded)
                {
                    // attempt succeeded within timeout, use this result

                    Debug.WriteLine("**********");
                    Debug.WriteLine("Current attempt completed successfully; using that result.");
                    Debug.WriteLine("**********");

                    Debug.Assert(attempt.Result != null);

                    return attempt.Result;
                }
                else if (attempt.Status == AttemptStatus.Failed)
                {
                    Debug.Assert(attempt.Exception != null);

                    if (attempt.Exception is CosmosException ce)
                    {
                        if (attempts.Count > retries)
                        {
                            // final attempt failed within timeout, use this error

                            Debug.WriteLine("**********");
                            Debug.WriteLine($"Final attempt failed; re-throwing that exception.");
                            Debug.WriteLine("**********");

                            ExceptionDispatchInfo.Capture(attempt.Exception).Throw();
                        }

                        // https://docs.microsoft.com/en-us/azure/cosmos-db/sql/troubleshoot-dot-net-sdk?tabs=diagnostics-v3#common-error-status-codes-

                        TimeSpan? retryAfter = ce.RetryAfter;

                        if (retryAfter.HasValue)   // TODO: handles 449?
                        {
                            // we want to honor any RetryAfter response headers from the server

                            Debug.WriteLine("**********");
                            Debug.WriteLine($"Current attempt failed; using RetryAfter value from Cosmos response header ({retryAfter.Value.TotalMilliseconds} milliseconds).");
                            Debug.WriteLine("**********");
                        }
                        else
                        {
                            // no RetryAfter from server... if this is a retryable error, let's pause then retry

                            switch (ce.StatusCode)
                            {
                                case HttpStatusCode.RequestTimeout:
                                case HttpStatusCode.Gone:
                                case HttpStatusCode.TooManyRequests:
                                case HttpStatusCode.InternalServerError:
                                case HttpStatusCode.ServiceUnavailable:

                                    retryAfter = GetJitter();

                                    Debug.WriteLine("**********");
                                    Debug.WriteLine($"Current attempt failed; Cosmos DB service response code is retryable. Retrying after {retryAfter.Value.TotalMilliseconds} milliseconds.");
                                    Debug.WriteLine("**********");

                                    break;
                            }
                        }

                        if (retryAfter != null)
                        {
                            await Task.Delay(retryAfter.Value, ct);
                            continue;
                        }
                    }

                    // current attempt failed within timeout, use this error

                    Debug.WriteLine("**********");
                    Debug.WriteLine($"Current attempt failed; re-throwing that exception.");
                    Debug.WriteLine("**********");

                    ExceptionDispatchInfo.Capture(attempt.Exception).Throw();
                }
                else
                {
                    if (attempts.Count > retries)
                    {
                        // final attempt timed out, throw TimeoutException

                        Debug.WriteLine("**********");
                        Debug.WriteLine($"Final attempt timed out; raising TimeoutException to caller.");
                        Debug.WriteLine("**********");

                        throw new TimeoutException();
                    }
                    else
                    {
                        // current attempt timed out, pause then retry

                        TimeSpan jitter = GetJitter();

                        Debug.WriteLine("**********");
                        Debug.WriteLine($"Current attempt timed out; retrying after {jitter.TotalMilliseconds} milliseconds.");
                        Debug.WriteLine("**********");

                        await Task.Delay(jitter, ct);
                    }
                }
            }
        }

        private static TimeSpan GetJitter()
        {
            // TODO: do we want exponential backoff here too?
            //  many customer scenarios require "fail fast" which seems counter to behavior of EB

            return TimeSpan.FromMilliseconds(1000 * _random.NextDouble());
        }

        private static async Task ExecuteAttemptAsync<TItem>(
            Attempt<TItem> attempt,
            Func<CancellationToken, Task<Response<TItem>>> func,
            TimeSpan timeout,
            Action<CosmosDiagnostics, Func<TimeSpan, bool>> diagnosticsFunc,
            CancellationToken token = default)
        {
            // Polly has elegant timeout policy handling with support for out-of-band resolution, etc.
            //  https://github.com/App-vNext/Polly/wiki/Timeout

            AsyncTimeoutPolicy? policy = GetPolicy(timeout, attempt, diagnosticsFunc);

            try
            {
                // invoke the timed operation, will throw TimeoutRejectedException upon timeout
                Response<TItem>? response = await policy.ExecuteAsync((ctxt, ct) => func(ct), new Context(), token);

                // no timeout, woooooo
                attempt.Result = response;
                attempt.Exception = null;
                attempt.Status = AttemptStatus.Succeeded;

                // timeout should be configured at P99 latency (or slightly higher) for targeted operations
                //  so, if elapsed time is 90% or greater of configured timeout, we log diagnostics
                diagnosticsFunc(
                    response.Diagnostics, elapsed => (elapsed.TotalSeconds * 100d / timeout.TotalSeconds) >= 90d);
            }
            catch (TimeoutRejectedException)
            {
                // let the caller know we timed out (might want to retry)
                attempt.Result = default;
                attempt.Exception = default;
                attempt.Status = AttemptStatus.TimedOutUnresolved;
            }
            catch (Exception ex)
            {
                // no timeout, but our timed operation is sad :-(
                attempt.Result = default;
                attempt.Exception = ex;
                attempt.Status = AttemptStatus.Failed;

                // don't forget your diagnostics
                if (ex is CosmosException ce)
                {
                    diagnosticsFunc(ce.Diagnostics, _ => true /* always log on error */);
                }
            }
        }

        private static AsyncTimeoutPolicy GetPolicy<TItem>(
            TimeSpan timeout, Attempt<TItem> attempt, Action<CosmosDiagnostics, Func<TimeSpan, bool>> diagnosticsFunc)
        {
            Task OnTimeout(Context context, TimeSpan timeoutValue, Task originalTask)
            {
                // TODO:
                // a few notes here:
                //
                //  we don't await or return the originalTask, because doing so would defeat the timeout itself, by forcing the policy to wait for it to fully complete
                //   see the note here https://github.com/App-vNext/Polly/wiki/Timeout under "What happens to the timed-out delegate?" -> "Pessimistic Timeout"
                //
                //  ContinueWith() can cause annoying call stacks etc. but we need a way to attach cleanup code to run immediately after the timed-out (but perhaps not completed) task
                //   this is the recommended pattern with Polly but if we can find a better approach I'm happy to go with it
                //

                originalTask.ContinueWith(t =>
                {
                    Task<Response<TItem>>? responseTask = (Task<Response<TItem>>)t;

                    attempt.OnTimeout(
                        responseTask, diagnostics => diagnosticsFunc(diagnostics, _ => true /* always log timed-out diagnostics */));

                    return attempt;
                });

                return Task.CompletedTask;
            }

            // need pessimistic strategy because we need to wait for CosmosClient to (eventually) handle task cancellation
            //  in optimistic mode you don't get access to the original task (and therefore can't harvest the diagnostics)

            return Policy.TimeoutAsync(timeout, TimeoutStrategy.Pessimistic, OnTimeout);
        }
    }
}
