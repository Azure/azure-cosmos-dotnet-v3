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
    /// Executes a metadata-cache read on a fully detached, internally-bounded
    /// <see cref="CancellationToken"/> while observing the caller's <see cref="CancellationToken"/>
    /// only on the response path.
    ///
    /// <para>
    /// <b>Why this exists:</b> Cosmos.Direct's <c>BackoffRetryUtility&lt;T&gt;.ExecuteAsync</c>
    /// (which is the canonical retry-loop helper used by <c>TaskHelper.InlineIfPossible</c>)
    /// calls <c>cancellationToken.ThrowIfCancellationRequested()</c> at the top of every loop
    /// iteration, BEFORE consulting <see cref="IDocumentClientRetryPolicy.ShouldRetryAsync"/>.
    /// When a caller passes a <see cref="CancellationToken"/> with a deadline only slightly
    /// larger than the SDK's control-plane HTTP-timeout-policy ladder (~36 s for
    /// <c>HttpTimeoutPolicyControlPlaneRetriableHotPath</c>), the cross-region failover
    /// decision is preempted and the customer surfaces <c>CosmosOperationCanceledException</c>
    /// instead of a successful failover to the next preferred region.
    /// </para>
    ///
    /// <para>
    /// <b>What this does:</b>
    /// <list type="bullet">
    ///   <item>The retry loop and underlying HTTP request execute on a detached
    ///   <see cref="CancellationTokenSource"/> bounded only by an SDK-internal hard deadline,
    ///   so <see cref="ClientRetryPolicy"/> always gets a chance to make its cross-region
    ///   decision. The caller's <see cref="CancellationToken"/> never enters this scope.</item>
    ///   <item>The caller's <see cref="CancellationToken"/> is observed only on the response
    ///   path via <see cref="Task.WhenAny(Task[])"/>. When the caller cancels mid-flight, the
    ///   caller surfaces <see cref="OperationCanceledException"/> immediately while the detached
    ///   task continues to completion. Side-effects of the retry policy (LocationCache region
    ///   marking, <c>ClearingSessionContainerClientRetryPolicy</c> session clearing, HTTP
    ///   connection-pool warming) accrue and benefit subsequent callers.</item>
    ///   <item>A defensive hard deadline (default
    ///   <see cref="ConfigurationManager.DefaultMetadataDetachedHardDeadlineInSeconds"/> seconds, configurable via
    ///   <c>AZURE_COSMOS_METADATA_DETACHED_HARD_DEADLINE_SECONDS</c>) and a defensive attempt
    ///   cap of <see cref="MaxAttemptsHardCap"/> guarantee that a misbehaving
    ///   <see cref="IDocumentClientRetryPolicy"/> cannot leak background work indefinitely.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>AsyncCache interaction caveat:</b> when the caller cancels, the <see cref="Task{T}"/>
    /// returned by <see cref="ExecuteAsync{T}(Func{CancellationToken, Task{T}}, IDocumentClientRetryPolicy, CancellationToken)"/>
    /// surfaces <see cref="OperationCanceledException"/>. If this task is wrapped by
    /// <c>AsyncCache&lt;TKey, TValue&gt;</c>, the corresponding <c>AsyncLazy</c> entry is
    /// removed from the cache (per <c>AsyncCache.GetAsync</c>'s catch-and-remove on the inserter
    /// thread). Subsequent callers therefore start a fresh fetch. Concurrent callers arriving
    /// BEFORE the first caller cancels DO reuse the in-flight detached task via standard
    /// AsyncCache semantics. The primary value of the detached design is that the retry
    /// policy is allowed to run to completion — its side-effects (region marking, session
    /// clearing) outlive the canceled lazy.
    /// </para>
    /// </summary>
    internal static class MetadataDetachedExecutor
    {
        /// <summary>
        /// Defensive upper bound on the number of attempts within a single detached
        /// invocation. Derivation: <c>ClientRetryPolicy</c> issues at most one cross-region
        /// attempt per preferred region (default <c>MaxNumberOfPreferredLocations = 5</c>);
        /// each region may also drive in-region retries and backoff bursts (≤10 in worst-case
        /// throttling scenarios). 5 × 10 = 50 covers all legitimate retry sequences with
        /// generous headroom. The cap is a hard guard against a misbehaving policy that
        /// returns <c>ShouldRetry=true</c> with <c>BackoffTime=TimeSpan.Zero</c> in a tight
        /// loop, which the time-based deadline alone cannot prevent without burning CPU.
        /// </summary>
        internal const int MaxAttemptsHardCap = 50;

        /// <summary>
        /// Returns the configured SDK-internal hard deadline for the detached attempt.
        /// Reads from <see cref="ConfigurationManager.MetadataDetachedHardDeadlineInSeconds"/>;
        /// see <see cref="ConfigurationManager.GetMetadataDetachedHardDeadline"/> for the default
        /// value and derivation.
        /// </summary>
        internal static TimeSpan GetDefaultInternalDeadline()
        {
            return ConfigurationManager.GetMetadataDetachedHardDeadline();
        }

        internal static Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            IDocumentClientRetryPolicy retryPolicy,
            CancellationToken callerCancellationToken)
        {
            return ExecuteAsync(
                operation,
                retryPolicy,
                GetDefaultInternalDeadline(),
                callerCancellationToken);
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
            // unobserved-task exceptions if the caller cancels first. (Canceled tasks
            // do not raise UnobservedTaskException, so we only need OnlyOnFaulted here.)
            Task unused = detachedTask.ContinueWith(
                t => t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            // Schedule disposal of the internal CTS once the detached task has run to
            // completion (success, fault, or cancel). This intentionally outlives the
            // caller-cancellation path; the CTS must remain valid while the detached
            // operation is still observing detachedCts.Token.
            Task disposeWhenDone = detachedTask.ContinueWith(
                _ => detachedCts.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            // Fast path: caller did not provide a cancellable token. Skip the WhenAny
            // scaffolding and the registration allocations entirely.
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

            // Caller cancelled before the detached attempt completed. Leave the detached
            // task running so ClientRetryPolicy can complete its cross-region decision and
            // its side-effects (LocationCache marking, session-container clearing) take hold.
            // We do NOT cancel detachedCts here.
            DefaultTrace.TraceInformation(
                "MetadataDetachedExecutor: caller token cancelled while detached metadata read in-flight; leaving background read to complete.");

            // Honor the caller token. ThrowIfCancellationRequested is preferred over
            // constructing a fresh OCE: it correctly carries the cancelled token and is
            // recognized by Cosmos's diagnostics layer.
            callerCancellationToken.ThrowIfCancellationRequested();

            // Defensive — should be unreachable because cancelTcs only completes when the
            // caller token transitions to cancelled.
            throw new OperationCanceledException(callerCancellationToken);
        }

        private static async Task<T> ExecuteRetryLoopAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            IDocumentClientRetryPolicy retryPolicy,
            CancellationToken detachedToken)
        {
            int attemptCount = 0;
            ExceptionDispatchInfo lastCapturedException = null;
            while (true)
            {
                if (++attemptCount > MaxAttemptsHardCap)
                {
                    string lastExceptionType = lastCapturedException?.SourceException.GetType().Name ?? "<none>";
                    string lastExceptionMessage = lastCapturedException?.SourceException.Message ?? "<none>";
                    DefaultTrace.TraceError(
                        "MetadataDetachedExecutor: exceeded hard attempt cap ({0}). Last exception: {1}: {2}",
                        MaxAttemptsHardCap,
                        lastExceptionType,
                        lastExceptionMessage);
                    throw new InvalidOperationException(
                        $"MetadataDetachedExecutor exceeded the defensive attempt cap of {MaxAttemptsHardCap}. " +
                        "This indicates a misconfigured retry policy that returns ShouldRetry=true indefinitely.",
                        lastCapturedException?.SourceException);
                }

                ExceptionDispatchInfo capturedException;
                try
                {
                    return await operation(detachedToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    capturedException = ExceptionDispatchInfo.Capture(ex);
                    lastCapturedException = capturedException;
                }

                ShouldRetryResult shouldRetry;
                try
                {
                    // Pass CancellationToken.None: the retry policy must not be canceled
                    // mid-decision. This is the entire point of the detached design.
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
                        if (object.ReferenceEquals(shouldRetry.ExceptionToThrow, capturedException.SourceException))
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
