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
    /// surfaces <see cref="OperationCanceledException"/>. Concurrent callers that arrive while
    /// the first caller's task is still running observe its in-flight task via standard
    /// <c>AsyncCache</c> semantics. If the first caller then cancels, the <c>AsyncLazy</c>
    /// faults with OCE; <c>AsyncCache.GetAsync</c> catches and discards the OCE on the in-flight
    /// path and the second caller starts a fresh detached attempt. The two detached tasks then
    /// run concurrently against the same metadata read. The detached design's primary benefit
    /// for the second caller is therefore the <i>side-effects</i> (LocationCache region marking,
    /// session-container clearing) of the first task's still-running retry policy — not direct
    /// reuse of its eventual successful result.
    /// </para>
    ///
    /// <para>
    /// <b>Retry-policy invariant:</b> the supplied <see cref="IDocumentClientRetryPolicy"/>
    /// MUST be a per-call instance. <c>ShouldRetryAsync</c> is intentionally NOT invoked
    /// on either OCE termination path (deadline-trip during operation, deadline-trip during
    /// backoff). A policy with cross-call state would therefore observe the operation as
    /// "still in flight" after the executor returns. Today the only call sites
    /// (<c>ClientCollectionCache.GetByRidAsync</c>, <c>GetByNameAsync</c>) construct a fresh
    /// policy via <c>retryPolicyFactory.GetRequestPolicy()</c> per call, so this invariant
    /// holds. A future refactor that caches policies must preserve it or move the OCE
    /// termination paths through <c>ShouldRetryAsync</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Two overloads, one isolation primitive:</b>
    /// <list type="bullet">
    ///   <item><see cref="ExecuteAsync{T}(Func{CancellationToken, Task{T}}, IDocumentClientRetryPolicy, CancellationToken)"/>:
    ///   runs the operation inside a self-contained retry loop driven by the supplied
    ///   <see cref="IDocumentClientRetryPolicy"/>. Used by <c>ClientCollectionCache</c>
    ///   where there is no inner pipeline retry — the leaf operation
    ///   (<c>ReadCollectionAsync</c>) calls <c>storeModel.ProcessMessageAsync</c> directly
    ///   without an enclosing <c>BackoffRetryUtility</c>.</item>
    ///   <item><see cref="ExecuteDetachedAsync{T}(Func{CancellationToken, Task{T}}, CancellationToken)"/>:
    ///   provides only the detach + caller-CT-on-response-path isolation. Used by
    ///   call sites where the underlying operation already runs through the standard
    ///   request pipeline (<c>RequestInvokerHandler</c> → <c>BackoffRetryUtility</c> →
    ///   <c>ClientRetryPolicy</c>) and therefore has its own retry semantics. Wrapping
    ///   with this overload ensures the pipeline's retry decisions cannot be preempted
    ///   by caller cancellation without double-driving a retry loop on top of it.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>Java alignment:</b> Java's <c>BackoffRetryUtility.executeRetry</c> uses Reactor
    /// <c>Mono.retryWhen</c>, which is an <i>error-signal-only</i> operator —
    /// <c>policy.shouldRetry(e)</c> is invoked unconditionally on every <c>onError</c>;
    /// downstream cancellation (<c>cancel()</c>) is a separate signal that bypasses the
    /// retry operator entirely. Combined with <c>ClientRetryPolicy.shouldRetry(Exception)</c>
    /// taking no token, Java is structurally immune to the bug class this executor closes
    /// for .NET. For the PKRange path specifically, Java's
    /// <c>AsyncCacheNonBlocking.getAsync</c> additionally calls
    /// <c>Mono.fromFuture(supplier, suppressCancel: true)</c> so that caller-side disposal
    /// of the Reactor subscription cannot cancel the underlying <c>CompletableFuture</c>.
    /// On the .NET side, the <c>PartitionKeyRangeCache</c> public API surface takes no
    /// <see cref="CancellationToken"/> at any layer and its internal
    /// <c>BackoffRetryUtility&lt;T&gt;.ExecuteAsync</c> invocation uses the 2-arg overload
    /// (<see cref="CancellationToken.None"/>), so the same structural immunity holds without
    /// an explicit wrap. The <c>GatewayAccountReader</c> / <c>GlobalEndpointManager</c>
    /// account-discovery path is also already detached from caller CT (it observes only
    /// the SDK lifecycle CTS), mirroring Java's dedicated
    /// <c>GLOBAL_ENDPOINT_MANAGER_BOUNDED_ELASTIC</c> scheduler. The remaining alignment
    /// gap addressed by this type is therefore: (a) <c>ClientCollectionCache</c> via
    /// <see cref="ExecuteAsync{T}(Func{CancellationToken, Task{T}}, IDocumentClientRetryPolicy, CancellationToken)"/>,
    /// and (b) the query-plan gateway path
    /// (<c>QueryPlanRetriever.GetQueryPlanThroughGatewayAsync</c>) via
    /// <see cref="ExecuteDetachedAsync{T}(Func{CancellationToken, Task{T}}, CancellationToken)"/>.
    /// </para>
    ///
    /// <para>
    /// <b>Diagnostics caveat (known limitation):</b> the operation lambda captures the caller's
    /// <c>ITrace</c> and <c>ClientSideRequestStatistics</c> so that on the success path the
    /// caller observes a complete trace tree. After caller cancellation the detached task
    /// continues to mutate those same caller objects until the SDK-internal deadline trips or
    /// the retry policy decides to stop. Customers who serialize <c>CosmosDiagnostics</c>
    /// immediately after observing the <see cref="OperationCanceledException"/> may therefore
    /// see additional trace children/datums grow after the operation appears to have ended.
    /// <c>Trace.AddDatum</c> is internally locked, so this is not a tearing crash, but it does
    /// mean the diagnostics snapshot is not strictly bounded by the caller-visible operation
    /// lifetime. A future iteration should isolate the detached task into a child trace tree
    /// and merge only on success.
    /// </para>
    /// </summary>
    internal static class MetadataDetachedExecutor
    {
        /// <summary>
        /// Defensive upper bound on the number of attempts within a single detached
        /// invocation. Derivation: the dominant legitimate ceiling on <c>ShouldRetry=true</c>
        /// calls is <c>ClientRetryPolicy.MaxRetryCount = 120</c> (cross-region failover
        /// counter, see <c>ClientRetryPolicy.cs</c>). On top of that, the inner
        /// throttling/session/serviceUnavailable retry policies can contribute a small
        /// number of additional retries (<c>MaxServiceUnavailableRetryCount = 1</c>,
        /// <c>MaxSessionTokenRetryCount = 2</c>, plus the default
        /// <c>ResourceThrottleRetryPolicy</c> budget of ~9 retries). 200 = 120 + ~80
        /// headroom covers all legitimate stacked retry sequences. The cap is a hard guard
        /// against a misbehaving policy that returns <c>ShouldRetry=true</c> with
        /// <c>BackoffTime=TimeSpan.Zero</c> in a tight loop, which the time-based deadline
        /// alone cannot prevent without burning CPU. Note: a well-behaved
        /// <c>ClientRetryPolicy</c> trips its own 120-retry limit before this defensive
        /// cap is reached.
        /// </summary>
        internal const int MaxAttemptsHardCap = 200;

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

        internal static Task<T> ExecuteAsync<T>(
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

            // The retry loop is the operation handed to the detach primitive. The retry
            // loop runs entirely on the detached token; the caller's token is observed
            // only on the response path inside ExecuteDetachedAsync.
            return ExecuteDetachedAsync(
                operation: (detachedToken) => ExecuteRetryLoopAsync(operation, retryPolicy, detachedToken),
                internalDeadline: internalDeadline,
                callerCancellationToken: callerCancellationToken);
        }

        /// <summary>
        /// Detach-only variant for call sites whose underlying operation already provides
        /// retry semantics (e.g. invocations that flow through <c>RequestInvokerHandler</c>
        /// and therefore through <c>BackoffRetryUtility</c> + <c>ClientRetryPolicy</c>
        /// internally). The operation runs on a detached <see cref="CancellationToken"/>
        /// bounded only by the SDK-internal deadline; the caller's token is observed only
        /// on the response path via <see cref="Task.WhenAny(Task[])"/>. No outer retry loop
        /// is run — that responsibility belongs to the operation itself.
        /// </summary>
        /// <remarks>
        /// Mirrors Java's structural guarantee: Java's <c>BackoffRetryUtility.executeRetry</c>
        /// uses Reactor <c>Mono.retryWhen</c> (error-signal-only), and the public
        /// metadata APIs take no <c>CancellationToken</c>. Wrapping a .NET pipeline call
        /// with this method achieves the same outcome: the pipeline's internal retry policy
        /// cannot be preempted by caller cancellation.
        /// </remarks>
        internal static Task<T> ExecuteDetachedAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken callerCancellationToken)
        {
            return ExecuteDetachedAsync(operation, GetDefaultInternalDeadline(), callerCancellationToken);
        }

        internal static async Task<T> ExecuteDetachedAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            TimeSpan internalDeadline,
            CancellationToken callerCancellationToken)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (internalDeadline <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(internalDeadline),
                    "Internal deadline must be positive.");
            }

            callerCancellationToken.ThrowIfCancellationRequested();

            // Detached token: the operation (and any retry loop it drives internally)
            // are bound only by the SDK-internal deadline. The caller's token never
            // enters this scope so retry-policy decisions inside the operation cannot
            // be preempted by caller cancellation.
            CancellationTokenSource detachedCts = new CancellationTokenSource(internalDeadline);
            Task<T> detachedTask;
            try
            {
                detachedTask = operation(detachedCts.Token);
            }
            catch
            {
                // Synchronous throw from the operation factory. Dispose the CTS we just
                // created and re-throw; nothing to observe asynchronously.
                detachedCts.Dispose();
                throw;
            }

            if (detachedTask == null)
            {
                detachedCts.Dispose();
                throw new InvalidOperationException(
                    "MetadataDetachedExecutor operation returned a null Task.");
            }

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
            //
            // ExecuteSynchronously note: when the operation lambda completes synchronously
            // (e.g. Task.FromResult in a unit test), detachedTask may already be complete
            // by the time this ContinueWith is registered. The continuation then runs
            // inline on the current thread and detachedCts is disposed before control
            // returns below. That is safe because detachedCts.Token was passed by value
            // above; the running task captures the token, not the source.
            // Do NOT read detachedCts after this point.
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
            // task running so any internal retry policy can complete its decision and
            // its side-effects (LocationCache marking, session-container clearing, etc.)
            // take hold. We do NOT cancel detachedCts here.
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
                ExceptionDispatchInfo previousException = lastCapturedException;
                try
                {
                    return await operation(detachedToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    detachedToken.IsCancellationRequested
                    && previousException != null
                    && !(previousException.SourceException is OperationCanceledException))
                {
                    // Internal deadline tripped while the operation lambda was in flight
                    // (as opposed to during Task.Delay backoff). Surface the prior
                    // underlying failure rather than the deadline-induced OCE so callers
                    // see the failure mode that drove the retry, not a hard-deadline
                    // artifact. This preserves the design contract documented at the
                    // backoff catch below.
                    //
                    // Asymmetry note: when previousException is itself an OCE (i.e. the
                    // first attempt also surfaced OCE bound to detachedToken), this filter
                    // intentionally falls through to the general catch path below. There
                    // is no diagnostic gain to swapping one OCE for another, and the
                    // general path correctly funnels through the policy / hard-cap /
                    // backoff-catch termination logic.
                    previousException.Throw();
                    throw; // unreachable
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
                    Exception exceptionToThrow = shouldRetry?.ExceptionToThrow;
                    if (exceptionToThrow != null)
                    {
                        // Reference equality on typed Exception locals. Avoids going through
                        // object.ReferenceEquals, which the CDX1000 analyzer
                        // (DontConvertExceptionToObject) flags because it converts typed
                        // Exception references to object and loses the type information used
                        // by static analysis. Exception is a reference type, so no boxing
                        // occurs in either form; the analyzer concern is type-info loss.
                        if (exceptionToThrow == capturedException.SourceException)
                        {
                            capturedException.Throw();
                        }

                        throw exceptionToThrow;
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
                else
                {
                    // Defensive yield when retry policy returns BackoffTime <= TimeSpan.Zero.
                    // Bounds CPU and gives the threadpool a chance to schedule other work,
                    // limiting amplification of a misbehaving policy that returns
                    // ShouldRetry=true with zero backoff in a tight loop. The hard attempt
                    // cap (MaxAttemptsHardCap) is the ultimate guard; this yield reduces
                    // the rate of the burst while the cap is being approached.
                    await Task.Yield();
                }
            }
        }
    }
}
