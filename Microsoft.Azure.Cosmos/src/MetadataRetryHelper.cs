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
    /// Retry helper for internal metadata (control-plane) reads such as
    /// <see cref="Routing.ClientCollectionCache"/>.<c>ReadCollectionAsync</c>.
    ///
    /// The shared <c>BackoffRetryUtility</c> evaluates the caller's
    /// <see cref="CancellationToken"/> before consulting <c>ShouldRetryAsync</c>. If the
    /// caller's timeout trips during the control-plane HTTP timeout escalation
    /// (0.5s → 5s → 30s ≈ 36s) for an unhealthy region, the cross-region failover
    /// that <c>ClientRetryPolicy</c> would otherwise execute is preempted and the
    /// operation surfaces an <see cref="OperationCanceledException"/>.
    ///
    /// This helper runs a bounded retry loop that always consults the retry policy
    /// on exception and, when the caller's token has already been cancelled, grants
    /// a small bounded grace window so the cross-region failover attempt can run.
    /// The grace window is intentionally short — the goal is best-effort availability,
    /// not unbounded timeout extension. If the retry attempt does not complete within
    /// the grace window, the original exception is rethrown.
    ///
    /// Note on the grace bound: the grace <see cref="CancellationTokenSource"/> controls
    /// when the grace attempt may START (via <c>ThrowIfCancellationRequested</c> and
    /// propagation into the operation lambda). If the underlying operation does not
    /// observe the grace token (e.g. the store model ignores cancellation), the in-flight
    /// call may exceed the grace window. Callers relying on a strict upper bound should
    /// ensure the operation honors its <see cref="CancellationToken"/>.
    /// </summary>
    internal static class MetadataRetryHelper
    {
        /// <summary>
        /// Maximum additional time granted to a metadata read after the caller's
        /// cancellation token has tripped so that cross-region retry can execute.
        /// </summary>
        internal static readonly TimeSpan DefaultCrossRegionRetryGrace = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Defensive upper bound on the number of attempts the helper makes within a single
        /// <see cref="ExecuteAsync{T}(Func{CancellationToken, Task{T}}, IDocumentClientRetryPolicy, TimeSpan, CancellationToken)"/>
        /// invocation. <see cref="ClientRetryPolicy"/> and peers already bound their own retry
        /// counts, but a misconfigured policy that always returns <c>ShouldRetry=true</c> would
        /// otherwise spin this loop indefinitely.
        /// </summary>
        private const int MaxAttemptsHardCap = 20;

        internal static Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            IDocumentClientRetryPolicy retryPolicy,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(operation, retryPolicy, DefaultCrossRegionRetryGrace, cancellationToken);
        }

        internal static async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            IDocumentClientRetryPolicy retryPolicy,
            TimeSpan crossRegionRetryGrace,
            CancellationToken cancellationToken)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (retryPolicy == null)
            {
                throw new ArgumentNullException(nameof(retryPolicy));
            }

            if (crossRegionRetryGrace < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(crossRegionRetryGrace),
                    "Cross-region retry grace must not be negative.");
            }

            // Contract: the operation lambda MUST honor the CancellationToken passed to it
            // and MUST NOT close over any outer CancellationToken. On a grace retry attempt
            // this will be a fresh, bounded-lifetime token decoupled from the caller's
            // (already cancelled) token. Closing over the outer token re-introduces the
            // defect this helper is designed to fix.
            bool graceAttempted = false;
            int attemptCount = 0;
            while (true)
            {
                if (++attemptCount > MaxAttemptsHardCap)
                {
                    DefaultTrace.TraceError(
                        "MetadataRetryHelper: exceeded hard attempt cap ({0}). Surfacing last exception.",
                        MaxAttemptsHardCap);
                    throw new InvalidOperationException(
                        $"MetadataRetryHelper exceeded the defensive attempt cap of {MaxAttemptsHardCap}. " +
                        "This indicates a misconfigured retry policy that returns ShouldRetry=true indefinitely.");
                }

                ExceptionDispatchInfo capturedException;
                try
                {
                    return await operation(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    capturedException = ExceptionDispatchInfo.Capture(ex);
                }

                // Always consult the retry policy BEFORE honoring caller cancellation.
                // This is the correctness fix: BackoffRetryUtility honors cancellation
                // first, which silently swallows cross-region failover decisions for
                // metadata reads when the caller's timeout trips during the HTTP
                // timeout policy escalation.
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
                        "MetadataRetryHelper: retry policy threw while evaluating exception {0}: {1}",
                        capturedException.SourceException.GetType().Name,
                        policyException.Message);
                    capturedException.Throw();
                    throw; // unreachable
                }

                if (shouldRetry == null || !shouldRetry.ShouldRetry)
                {
                    // Honor ShouldRetryResult.ExceptionToThrow if the policy has specified a
                    // wrapper/translated exception (matches BackoffRetryUtility.ThrowIfDoneTrying).
                    if (shouldRetry?.ExceptionToThrow != null)
                    {
                        throw shouldRetry.ExceptionToThrow;
                    }

                    capturedException.Throw();
                }

                // If the caller has not cancelled, honor any backoff and continue the loop.
                if (!cancellationToken.IsCancellationRequested)
                {
                    if (shouldRetry.BackoffTime > TimeSpan.Zero)
                    {
                        try
                        {
                            await Task.Delay(shouldRetry.BackoffTime, cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // Caller cancelled during backoff. Fall through — we will
                            // attempt one bounded cross-region retry below.
                        }
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        continue;
                    }
                }

                // Caller token is cancelled. Grant a single bounded grace window for
                // a cross-region retry so availability-critical failover is not
                // silently preempted. The grace token is passed to the operation so
                // the underlying HTTP call is not itself preempted by the caller's
                // already-cancelled token. Subsequent cancellations collapse to the
                // original exception so we do not extend the caller's timeout unbounded.
                if (graceAttempted || crossRegionRetryGrace <= TimeSpan.Zero)
                {
                    DefaultTrace.TraceInformation(
                        "MetadataRetryHelper: caller token cancelled; cross-region retry grace already used or disabled. Surfacing original exception.");
                    capturedException.Throw();
                }

                graceAttempted = true;
                DefaultTrace.TraceInformation(
                    "MetadataRetryHelper: caller token cancelled; granting {0}ms grace for one cross-region metadata retry.",
                    (int)crossRegionRetryGrace.TotalMilliseconds);

                using (CancellationTokenSource graceCts = new CancellationTokenSource(crossRegionRetryGrace))
                {
                    try
                    {
                        return await ExecuteSingleAttemptWithGraceAsync(
                            operation,
                            shouldRetry.BackoffTime,
                            graceCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (graceCts.IsCancellationRequested)
                    {
                        // Grace window expired before the cross-region attempt completed.
                        // Surface the original failure rather than the grace-timeout.
                        DefaultTrace.TraceWarning(
                            "MetadataRetryHelper: grace window ({0}ms) expired during cross-region retry. Surfacing original exception.",
                            (int)crossRegionRetryGrace.TotalMilliseconds);
                        capturedException.Throw();
                        throw; // unreachable
                    }
                    catch (Exception graceException)
                    {
                        // Cross-region attempt itself failed. Surface the ORIGINAL exception so
                        // callers see the pre-failover failure mode, not the grace-region failure.
                        DefaultTrace.TraceWarning(
                            "MetadataRetryHelper: grace-region retry failed with {0}: {1}. Surfacing original exception.",
                            graceException.GetType().Name,
                            graceException.Message);
                        capturedException.Throw();
                        throw; // unreachable
                    }
                }
            }
        }

        private static async Task<T> ExecuteSingleAttemptWithGraceAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            TimeSpan backoff,
            CancellationToken graceToken)
        {
            if (backoff > TimeSpan.Zero)
            {
                await Task.Delay(backoff, graceToken).ConfigureAwait(false);
            }

            graceToken.ThrowIfCancellationRequested();
            return await operation(graceToken).ConfigureAwait(false);
        }
    }
}
