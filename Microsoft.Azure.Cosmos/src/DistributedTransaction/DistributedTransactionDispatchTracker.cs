// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Derives <see cref="DistributedTransactionConstants.IsDtxRetry"/> and
    /// <see cref="DistributedTransactionConstants.IsDtxCrossRegionRedirect"/> from the dispatch history of a
    /// distributed write transaction idempotency token.
    /// </summary>
    /// <remarks>
    /// Scoped to the token, not to a retry policy. ClientRetryPolicy can fail a request over to another
    /// write region within a single commit attempt, and the committer replays the same token through a new
    /// policy on any retriable non-abort response, so policy-local state would reset while the token lives
    /// on and under-report both signals.
    /// Token rotation creates a new tracker.
    /// The committer and retry handler serialize DTX attempts; DTX is not hedged.
    /// </remarks>
    internal sealed class DistributedTransactionDispatchTracker
    {
        private readonly object stateLock = new object();
        private string originalDispatchRegion;
        private int dispatchCount;
        private bool hasUnresolvedDispatch;
        private bool isCrossRegionRedirect;

        /// <summary>
        /// Records the region an imminent dispatch is pinned to.
        /// </summary>
        /// <remarks>
        /// Records intent rather than delivery: a failed dispatch may still have reached the coordinator,
        /// so it counts, over-reporting in the safe direction. Lower-level transport resends do not
        /// record a new dispatch. Once set, the cross-region signal stays true for this token.
        /// The locked update defines recording order, not completion or network arrival order.
        /// </remarks>
        /// <param name="regionName">
        /// The account-topology region key, or null/empty if unresolved. Comparison ignores casing,
        /// but does not trim whitespace or resolve aliases.
        /// </param>
        /// <returns>A consistent snapshot of both dispatch signals after recording this dispatch.</returns>
        internal (bool IsRetry, bool IsCrossRegionRedirect) RecordDispatch(string regionName)
        {
            lock (this.stateLock)
            {
                this.dispatchCount++;

                // An unresolved first dispatch establishes no origin. Any unresolved retry may have crossed
                // regions, so conservatively latch the redirect signal.
                if (string.IsNullOrEmpty(regionName))
                {
                    this.hasUnresolvedDispatch = true;
                    this.isCrossRegionRedirect |= this.dispatchCount > 1;
                    return (this.dispatchCount > 1, this.isCrossRegionRedirect);
                }

                if (this.originalDispatchRegion == null)
                {
                    this.originalDispatchRegion = regionName;

                    // An earlier dispatch went somewhere this client could not name, so this region cannot be
                    // trusted as the origin. Report a crossing rather than hide one that may have happened.
                    if (this.hasUnresolvedDispatch)
                    {
                        this.isCrossRegionRedirect = true;
                    }
                }
                else if (!string.Equals(this.originalDispatchRegion, regionName, StringComparison.OrdinalIgnoreCase))
                {
                    this.isCrossRegionRedirect = true;
                }

                return (this.dispatchCount > 1, this.isCrossRegionRedirect);
            }
        }

        /// <summary>
        /// Stamps both headers for the next dispatch.
        /// </summary>
        /// <remarks>
        /// Callers must exclusively own the request and headers through sending; sequential reuse is allowed.
        /// Concurrent calls require separate requests and header collections. Header writes are not atomic.
        /// Recording precedes header writes; exceptions propagate without undoing the dispatch or earlier writes.
        /// </remarks>
        internal void StampDispatchHeaders(DocumentServiceRequest request, string regionName)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            (bool IsRetry, bool IsCrossRegionRedirect) dispatchSignals = this.RecordDispatch(regionName);

            request.Headers[DistributedTransactionConstants.IsDtxRetry] =
                dispatchSignals.IsRetry ? bool.TrueString : bool.FalseString;
            request.Headers[DistributedTransactionConstants.IsDtxCrossRegionRedirect] =
                dispatchSignals.IsCrossRegionRedirect ? bool.TrueString : bool.FalseString;
        }
    }
}
