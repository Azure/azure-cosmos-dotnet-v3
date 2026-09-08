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
    /// </remarks>
    internal sealed class DistributedTransactionDispatchTracker
    {
        private readonly object stateLock = new object();
        private string originalDispatchRegion;
        private int dispatchCount;
        private bool hasUnresolvedDispatch;
        private bool isCrossRegionRedirect;

        /// <summary>
        /// Derived rather than assigned: the headers are read after <see cref="RecordDispatch"/> has counted
        /// the imminent dispatch, so the first one still has to report false.
        /// </summary>
        internal bool IsRetry
        {
            get
            {
                lock (this.stateLock)
                {
                    return this.dispatchCount > 1;
                }
            }
        }

        /// <summary>
        /// Sticky for the lifetime of the token. A dispatch lost in flight may still have reached the
        /// coordinator, so failing back to the original region does not clear the signal.
        /// </summary>
        internal bool IsCrossRegionRedirect
        {
            get
            {
                lock (this.stateLock)
                {
                    return this.isCrossRegionRedirect;
                }
            }
        }

        /// <summary>
        /// Records the region an imminent dispatch is pinned to.
        /// </summary>
        /// <remarks>
        /// Records intent rather than delivery: a failed dispatch may still have reached the coordinator,
        /// so it counts, over-reporting in the safe direction. Transport resends that retry in place go
        /// uncounted, but those never reached the coordinator.
        /// </remarks>
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
        internal void StampDispatchHeaders(DocumentServiceRequest request, string regionName)
        {
            if (request == null)
            {
                return;
            }

            (bool IsRetry, bool IsCrossRegionRedirect) dispatchSignals = this.RecordDispatch(regionName);

            request.Headers[DistributedTransactionConstants.IsDtxRetry] =
                dispatchSignals.IsRetry ? bool.TrueString : bool.FalseString;
            request.Headers[DistributedTransactionConstants.IsDtxCrossRegionRedirect] =
                dispatchSignals.IsCrossRegionRedirect ? bool.TrueString : bool.FalseString;
        }
    }
}
