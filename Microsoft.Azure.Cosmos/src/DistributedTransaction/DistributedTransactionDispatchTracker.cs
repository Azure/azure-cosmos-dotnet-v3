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
        private bool hasUnresolvedDispatch;

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
        /// Records intent: a send that fails after routing still counts, because it may have reached the
        /// coordinator first. So a failure before the request leaves the process (authorization, store
        /// proxy resolution) is counted anyway, over-reporting in the safe direction. Conversely a gateway
        /// resend after a retriable WebException is not counted, because CosmosHttpClientCore retries in
        /// place without re-entering <see cref="ClientRetryPolicy.OnBeforeSendRequest"/>; that
        /// under-reports, but only for connection failures that never reached the coordinator.
        /// </remarks>
        internal void RecordDispatch(string regionName)
        {
            this.dispatchCount++;

            // A region goes unnamed when the endpoint is absent from the account topology: before the
            // first account refresh populates it, or when endpoint discovery is disabled. The dispatch
            // still happened, so it has to count.
            if (string.IsNullOrEmpty(regionName))
            {
                this.hasUnresolvedDispatch = true;
                return;
            }

            if (this.originalDispatchRegion == null)
            {
                this.originalDispatchRegion = regionName;

                // An earlier dispatch went somewhere this client could not name, so this region cannot be
                // trusted as the origin. Report a crossing rather than hide one that may have happened.
                if (this.hasUnresolvedDispatch)
                {
                    this.IsCrossRegionRedirect = true;
                }
            }
            else if (!string.Equals(this.originalDispatchRegion, regionName, StringComparison.OrdinalIgnoreCase))
            {
                this.IsCrossRegionRedirect = true;
            }
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
