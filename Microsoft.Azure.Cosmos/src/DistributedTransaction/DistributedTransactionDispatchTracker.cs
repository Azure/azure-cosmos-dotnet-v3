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
    ///
    /// Unsynchronized: a commit awaits one attempt at a time, and CrossRegionHedgingAvailabilityStrategy
    /// only hedges <see cref="ResourceType.Document"/>, so a transaction never runs as concurrent arms.
    /// </remarks>
    internal sealed class DistributedTransactionDispatchTracker
    {
        internal const string PropertyKey = "DistributedTransactionDispatchTracker";

        private string originalDispatchRegion;
        private int dispatchCount;

        /// <summary>
        /// Derived rather than assigned: the headers are read after <see cref="RecordDispatch"/> has counted
        /// the imminent dispatch, so the first one still has to report false.
        /// </summary>
        internal bool IsRetry => this.dispatchCount > 1;

        /// <summary>
        /// Sticky for the lifetime of the token. A dispatch lost in flight may still have reached the
        /// coordinator, so failing back to the original region does not clear the signal.
        /// </summary>
        internal bool IsCrossRegionRedirect { get; private set; }

        /// <summary>
        /// Records the region an imminent dispatch is pinned to.
        /// </summary>
        /// <remarks>
        /// Records intent, so a send that fails after routing still counts as a dispatch. That over-reports
        /// true, the safe direction: the request may have reached the coordinator before failing.
        /// </remarks>
        internal void RecordDispatch(string regionName)
        {
            this.dispatchCount++;

            // An unresolvable region cannot place this dispatch, but the dispatch itself still happened.
            if (string.IsNullOrEmpty(regionName))
            {
                return;
            }

            if (this.originalDispatchRegion == null)
            {
                this.originalDispatchRegion = regionName;
            }
            else if (!string.Equals(this.originalDispatchRegion, regionName, StringComparison.OrdinalIgnoreCase))
            {
                this.IsCrossRegionRedirect = true;
            }
        }

        internal void ResetForNewToken()
        {
            this.originalDispatchRegion = null;
            this.dispatchCount = 0;
            this.IsCrossRegionRedirect = false;
        }

        /// <summary>
        /// Stamps both headers when <paramref name="request"/> carries a tracker; read transactions carry
        /// none and omit the headers entirely.
        /// </summary>
        internal static void StampDispatchHeaders(DocumentServiceRequest request, string regionName)
        {
            if (request?.Properties == null
                || !request.Properties.TryGetValue(DistributedTransactionDispatchTracker.PropertyKey, out object trackerObject)
                || trackerObject is not DistributedTransactionDispatchTracker tracker)
            {
                return;
            }

            tracker.RecordDispatch(regionName);

            request.Headers[DistributedTransactionConstants.IsDtxRetry] =
                tracker.IsRetry ? bool.TrueString : bool.FalseString;
            request.Headers[DistributedTransactionConstants.IsDtxCrossRegionRedirect] =
                tracker.IsCrossRegionRedirect ? bool.TrueString : bool.FalseString;
        }
    }
}
