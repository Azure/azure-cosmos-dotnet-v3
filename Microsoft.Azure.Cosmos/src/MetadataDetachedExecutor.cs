//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
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
    /// larger than the SDK's control-plane HTTP-timeout-policy ladder (~72 s per region for
    /// <c>HttpTimeoutPolicyControlPlaneRetriableHotPath</c> — (1 s, 0) → (5 s, 1 s) → (65 s, 0),
    /// i.e. 71 s of request timeouts plus the 1 s inter-attempt delay), the cross-region failover
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
    ///   decision. The caller's <see cref="CancellationToken"/> never enters this scope. The
    ///   retry loop itself is the canonical <c>BackoffRetryUtility&lt;T&gt;.ExecuteAsync</c>
    ///   (via <c>TaskHelper.InlineIfPossible</c>); because it runs on the detached token, its
    ///   iteration-top <c>ThrowIfCancellationRequested</c> can only trip at the SDK-internal
    ///   hard deadline, never at the caller's deadline — which is the entire fix.</item>
    ///   <item>The caller's <see cref="CancellationToken"/> is observed only on the response
    ///   path via <see cref="Task.WhenAny(Task[])"/>. When the caller cancels mid-flight, the
    ///   caller surfaces <see cref="OperationCanceledException"/> immediately while the detached
    ///   task continues to completion. Side-effects of the retry policy (LocationCache region
    ///   marking, <c>ClearingSessionContainerClientRetryPolicy</c> session clearing, HTTP
    ///   connection-pool warming) accrue and benefit subsequent callers.</item>
    ///   <item>A defensive hard deadline (default
    ///   <see cref="ConfigurationManager.DefaultMetadataDetachedHardDeadlineInSeconds"/> seconds, configurable via
    ///   <c>AZURE_COSMOS_METADATA_DETACHED_HARD_DEADLINE_SECONDS</c>) guarantees that a
    ///   misbehaving <see cref="IDocumentClientRetryPolicy"/> cannot leak background work
    ///   indefinitely: once the deadline trips, <c>BackoffRetryUtility</c>'s top-of-loop and
    ///   backoff <c>Task.Delay</c> both observe the detached token and surface
    ///   <see cref="OperationCanceledException"/>, terminating the loop.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>Why <c>BackoffRetryUtility</c> rather than a hand-rolled loop:</b> earlier revisions
    /// of this type forked <c>BackoffRetryUtility</c> into a private retry loop solely to invoke
    /// <c>ShouldRetryAsync</c> with <see cref="CancellationToken.None"/> instead of the detached
    /// token. That distinction is not load-bearing: the side-effect the loop exists to protect —
    /// cross-region failover marking in <c>ClientRetryPolicy.ShouldRetryOnEndpointFailureAsync</c>
    /// → <c>GlobalEndpointManager.RefreshLocationAsync</c> — takes no <see cref="CancellationToken"/>
    /// at all, and the detached token only ever cancels at the SDK-internal hard deadline (300 s
    /// by default), long after any legitimate <c>ShouldRetryAsync</c> decision completes. Passing
    /// the detached token is therefore equivalent to <see cref="CancellationToken.None"/> in
    /// practice while keeping a single canonical retry implementation, and it makes the
    /// collection-cache path symmetric with the query-plan path
    /// (<see cref="ExecuteDetachedAsync{T}(Func{CancellationToken, Task{T}}, CancellationToken)"/>),
    /// which already drives <c>BackoffRetryUtility</c> on the detached token. This also matches
    /// Java, where <c>shouldRetry(Exception)</c> takes no token at any layer.
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
    /// reuse of its eventual successful result. Under broad concurrent caller cancellation this
    /// weakens the <c>AsyncCache</c> dedup and multiple background reads for the same metadata
    /// can run at once; <see cref="LiveDetachedBackgroundReads"/> surfaces that amplification in
    /// telemetry. Bounding the fan-out (single-flight / concurrency cap) is tracked as follow-up
    /// work; see the PR thread on this type.
    /// </para>
    ///
    /// <para>
    /// <b>Retry-policy invariant:</b> the supplied <see cref="IDocumentClientRetryPolicy"/>
    /// SHOULD be a per-call instance. Today the only call sites
    /// (<c>ClientCollectionCache.GetByRidAsync</c>, <c>GetByNameAsync</c>) construct a fresh
    /// policy via <c>retryPolicyFactory.GetRequestPolicy()</c> per call. Because the detached
    /// retry loop runs to a terminal decision (success, policy <c>NoRetry</c>, or the hard
    /// deadline) before the task completes, the policy is never observed mid-flight after the
    /// executor returns its result on the response path.
    /// </para>
    ///
    /// <para>
    /// <b>Two overloads, one isolation primitive:</b>
    /// <list type="bullet">
    ///   <item><see cref="ExecuteAsync{T}(Func{CancellationToken, Task{T}}, IDocumentClientRetryPolicy, CancellationToken)"/>:
    ///   drives the supplied <see cref="IDocumentClientRetryPolicy"/> through
    ///   <c>BackoffRetryUtility</c> on the detached token. Used by <c>ClientCollectionCache</c>
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
    /// <see cref="ExecuteAsync{T}(Func{CancellationToken, Task{T}}, IDocumentClientRetryPolicy, CancellationToken)"/>
    /// is implemented in terms of <see cref="ExecuteDetachedAsync{T}(Func{CancellationToken, Task{T}}, CancellationToken)"/>,
    /// so there is exactly one detach primitive in the codebase.
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
        /// Number of detached metadata reads that are currently still running in the
        /// background <i>after</i> their caller cancelled (i.e. the caller already surfaced
        /// <see cref="OperationCanceledException"/> on the response path while the detached
        /// task was left to complete its retry/failover decision). This is an observability
        /// signal for the known fan-out behavior documented on this type: under concurrent
        /// caller cancellation (e.g. a regional outage where many caller tokens trip on the
        /// same HTTP-ladder boundary) the <c>AsyncCache</c> dedup is weakened and multiple
        /// background reads for the same metadata can run at once. Surfacing a live count in
        /// traces makes that amplification visible in production without changing behavior.
        /// </summary>
        private static long liveDetachedBackgroundReads;

        /// <summary>
        /// Current count of caller-abandoned detached metadata reads still running in the
        /// background. See <see cref="liveDetachedBackgroundReads"/>.
        /// </summary>
        internal static long LiveDetachedBackgroundReads => Interlocked.Read(ref liveDetachedBackgroundReads);

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

            // The retry loop is the operation handed to the detach primitive. The canonical
            // BackoffRetryUtility (via TaskHelper.InlineIfPossible) drives the retry policy
            // entirely on the detached token, so its iteration-top ThrowIfCancellationRequested
            // can only trip at the SDK-internal hard deadline, never at the caller's deadline.
            // The caller's token is observed only on the response path inside
            // ExecuteDetachedAsync. See the type <summary> for why the detached token (rather
            // than CancellationToken.None) is passed to ShouldRetryAsync.
            return ExecuteDetachedAsync(
                operation: (detachedToken) => TaskHelper.InlineIfPossible(
                    () => operation(detachedToken),
                    retryPolicy,
                    detachedToken),
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

            // Cancel/complete race: Task.WhenAny may have selected the cancellation TCS at
            // the same instant the detached task completed successfully. Prefer returning the
            // valid result over discarding it and surfacing OCE. (Status check is used rather
            // than IsCompletedSuccessfully, which is unavailable on netstandard2.0.)
            if (detachedTask.Status == TaskStatus.RanToCompletion)
            {
                return detachedTask.GetAwaiter().GetResult();
            }

            // Caller cancelled before the detached attempt completed. Leave the detached
            // task running so any internal retry policy can complete its decision and
            // its side-effects (LocationCache marking, session-container clearing, etc.)
            // take hold. We do NOT cancel detachedCts here.
            //
            // Track the now-orphaned background read for observability: increment on
            // abandonment, decrement when it eventually completes. This makes the documented
            // fan-out-under-concurrent-cancellation behavior visible in production telemetry.
            long liveCount = Interlocked.Increment(ref liveDetachedBackgroundReads);
            Task unusedDecrement = detachedTask.ContinueWith(
                _ => Interlocked.Decrement(ref liveDetachedBackgroundReads),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            DefaultTrace.TraceInformation(
                "MetadataDetachedExecutor: caller token cancelled while detached metadata read in-flight; leaving background read to complete. Live caller-abandoned detached reads: {0}.",
                liveCount);

            // Honor the caller token. ThrowIfCancellationRequested is preferred over
            // constructing a fresh OCE: it correctly carries the cancelled token and is
            // recognized by Cosmos's diagnostics layer.
            callerCancellationToken.ThrowIfCancellationRequested();

            // Defensive — should be unreachable because cancelTcs only completes when the
            // caller token transitions to cancelled.
            throw new OperationCanceledException(callerCancellationToken);
        }
    }
}
