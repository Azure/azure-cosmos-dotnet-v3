//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Alternative metadata-read execution model under exploration: run the metadata
    /// read on a fully detached, internally-bounded <see cref="CancellationToken"/>
    /// and observe the caller's <see cref="CancellationToken"/> on the response path
    /// only. This mirrors the Java SDK's <c>BackoffRetryUtility</c>, which never
    /// threads a caller cancellation signal into the retry pipeline.
    ///
    /// Compared with <see cref="MetadataRetryHelper"/> (which honors the caller token
    /// during retries and grants a single bounded grace window when cross-region
    /// failover is preempted), this executor:
    /// <list type="bullet">
    ///   <item>Never preempts cross-region failover. The retry policy always runs
    ///   to completion or to the SDK-internal deadline, regardless of caller CT.</item>
    ///   <item>Allows the caller to surface <see cref="OperationCanceledException"/>
    ///   immediately when their token trips, while the underlying metadata read
    ///   continues in the background and lands in <see cref="Common.AsyncCache{TKey, TValue}"/>
    ///   (via <c>AsyncLazy</c>) so a follow-up call observes the cached result.</item>
    ///   <item>Bounds the detached attempt by <see cref="DefaultInternalDeadline"/>
    ///   so a misbehaving retry policy cannot keep work in flight indefinitely.</item>
    /// </list>
    ///
    /// See <c>docs/internal/metadata-retry-detached-vs-grace.md</c> in this branch for
    /// the full comparison of the two approaches.
    /// </summary>
    internal static class MetadataDetachedExecutor
    {
        /// <summary>
        /// Defensive upper bound on the lifetime of the detached metadata read attempt.
        /// <see cref="ClientRetryPolicy"/> already caps cross-region attempts (one per
        /// preferred region), but a misconfigured policy that always returns
        /// <c>ShouldRetry=true</c> would otherwise leak background work.
        /// </summary>
        internal static readonly TimeSpan DefaultInternalDeadline = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Defensive upper bound on the number of attempts within a single detached
        /// invocation. Mirrors the cap in <see cref="MetadataRetryHelper"/>.
        /// </summary>
        private const int MaxAttemptsHardCap = 20;

        internal static Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            IDocumentClientRetryPolicy retryPolicy,
            CancellationToken callerCancellationToken)
        {
            return ExecuteAsync(operation, retryPolicy, DefaultInternalDeadline, callerCancellationToken);
        }

        internal static async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            IDocumentClientRetryPolicy retryPolicy,
            TimeSpan internalDeadline,
            CancellationToken callerCancellationToken)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (retryPolicy == null)
            {
                throw new ArgumentNullException(nameof(retryPolicy));
            }

            if (internalDeadline <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(internalDeadline),
                    "Internal deadline must be positive.");
            }

            callerCancellationToken.ThrowIfCancellationRequested();

            // Detached token: the retry-loop and underlying HTTP call are bound only by
            // the SDK-internal deadline. The caller's token never enters this scope so
            // ClientRetryPolicy decisions cannot be preempted by caller cancellation.
            CancellationTokenSource detachedCts = new CancellationTokenSource(internalDeadline);
            Task<T> detachedTask = ExecuteRetryLoopAsync(operation, retryPolicy, detachedCts.Token);

            // Always observe completion so faulted detached tasks do not surface as
            // unobserved-task exceptions if the caller cancels first.
            Task unused = detachedTask.ContinueWith(
                t => t.Exception,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

            try
            {
                if (!callerCancellationToken.CanBeCanceled)
                {
                    return await detachedTask.ConfigureAwait(false);
                }

                TaskCompletionSource<bool> cancelTcs = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                using (callerCancellationToken.Register(() => cancelTcs.TrySetResult(true)))
                {
                    Task winner = await Task.WhenAny(detachedTask, cancelTcs.Task).ConfigureAwait(false);

                    if (winner == detachedTask)
                    {
                        return await detachedTask.ConfigureAwait(false);
                    }
                }

                // Caller cancelled before the detached attempt completed. Leave the
                // detached task running so AsyncCache.GetAsync's compare-and-swap entry
                // remains valid for the next caller — this is the entire point of the
                // detached model. We do NOT cancel detachedCts here.
                DefaultTrace.TraceInformation(
                    "MetadataDetachedExecutor: caller token cancelled while detached metadata read in-flight; leaving background read to complete.");

                callerCancellationToken.ThrowIfCancellationRequested();
                throw new OperationCanceledException(callerCancellationToken);
            }
            finally
            {
                // Ownership note: detachedCts is intentionally NOT disposed in the
                // caller-cancellation path. It must outlive detachedTask. Schedule
                // disposal on completion so we do not leak the timer.
                Task disposeWhenDone = detachedTask.ContinueWith(
                    _ => detachedCts.Dispose(),
                    TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        private static async Task<T> ExecuteRetryLoopAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            IDocumentClientRetryPolicy retryPolicy,
            CancellationToken detachedToken)
        {
            int attemptCount = 0;
            while (true)
            {
                if (++attemptCount > MaxAttemptsHardCap)
                {
                    DefaultTrace.TraceError(
                        "MetadataDetachedExecutor: exceeded hard attempt cap ({0}). Surfacing last exception.",
                        MaxAttemptsHardCap);
                    throw new InvalidOperationException(
                        $"MetadataDetachedExecutor exceeded the defensive attempt cap of {MaxAttemptsHardCap}. " +
                        "This indicates a misconfigured retry policy that returns ShouldRetry=true indefinitely.");
                }

                ExceptionDispatchInfo capturedException;
                try
                {
                    return await operation(detachedToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    capturedException = ExceptionDispatchInfo.Capture(ex);
                }

                ShouldRetryResult shouldRetry;
                try
                {
                    shouldRetry = await retryPolicy
                        .ShouldRetryAsync(capturedException.SourceException, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception policyException)
                {
                    DefaultTrace.TraceWarning(
                        "MetadataDetachedExecutor: retry policy threw while evaluating exception {0}: {1}",
                        capturedException.SourceException.GetType().Name,
                        policyException.Message);
                    capturedException.Throw();
                    throw; // unreachable
                }

                if (shouldRetry == null || !shouldRetry.ShouldRetry)
                {
                    if (shouldRetry?.ExceptionToThrow != null)
                    {
                        if (shouldRetry.ExceptionToThrow == capturedException.SourceException)
                        {
                            capturedException.Throw();
                        }

                        throw shouldRetry.ExceptionToThrow;
                    }

                    capturedException.Throw();
                }

                if (shouldRetry.BackoffTime > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(shouldRetry.BackoffTime, detachedToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (detachedToken.IsCancellationRequested)
                    {
                        // Internal deadline reached during backoff. Surface the original
                        // exception so callers see the underlying failure mode rather
                        // than a deadline-induced OCE.
                        capturedException.Throw();
                    }
                }
            }
        }
    }
}
