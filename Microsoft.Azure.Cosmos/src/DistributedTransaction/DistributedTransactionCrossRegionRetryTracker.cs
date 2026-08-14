// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Derives <see cref="DistributedTransactionConstants.CrossRegionRetryHeader"/> from the write regions
    /// a distributed write transaction idempotency token has been dispatched to.
    /// </summary>
    /// <remarks>
    /// Scoped to the token, not to a retry policy. ClientRetryPolicy can fail a request over to another
    /// write region within a single commit attempt, and the committer replays the same token through a new
    /// policy on any retriable non-abort response, so a policy-local counter would reset while the token
    /// lives on and under-report the crossing.
    ///
    /// Unsynchronized: a commit awaits one attempt at a time, and CrossRegionHedgingAvailabilityStrategy
    /// only hedges <see cref="ResourceType.Document"/>, so a transaction never runs as concurrent arms.
    /// </remarks>
    internal sealed class DistributedTransactionCrossRegionRetryTracker
    {
        internal const string PropertyKey = "DistributedTransactionCrossRegionRetryTracker";

        private string lastDispatchRegion;

        internal bool HasCrossedRegionBoundary { get; private set; }

        /// <summary>
        /// Records the region an imminent dispatch is pinned to, or no-ops when it cannot be resolved.
        /// </summary>
        /// <remarks>
        /// Records intent, so a send that fails after routing still counts as a visit. That over-reports
        /// True, the safe direction: the request may have reached the coordinator before failing.
        /// </remarks>
        internal void RecordDispatch(string regionName)
        {
            // Recording an unknown region would either invent a crossing or discard the last one observed.
            if (string.IsNullOrEmpty(regionName))
            {
                return;
            }

            if (this.lastDispatchRegion != null
                && !string.Equals(this.lastDispatchRegion, regionName, StringComparison.OrdinalIgnoreCase))
            {
                this.HasCrossedRegionBoundary = true;
            }

            this.lastDispatchRegion = regionName;
        }

        internal void ResetForNewToken()
        {
            this.lastDispatchRegion = null;
            this.HasCrossedRegionBoundary = false;
        }

        /// <summary>
        /// Stamps the header when <paramref name="request"/> carries a tracker; read transactions carry
        /// none and omit the header entirely.
        /// </summary>
        internal static void StampCrossRegionRetryHeader(DocumentServiceRequest request, string regionName)
        {
            if (request?.Properties == null
                || !request.Properties.TryGetValue(DistributedTransactionCrossRegionRetryTracker.PropertyKey, out object trackerObject)
                || trackerObject is not DistributedTransactionCrossRegionRetryTracker tracker)
            {
                return;
            }

            tracker.RecordDispatch(regionName);

            request.Headers[DistributedTransactionConstants.CrossRegionRetryHeader] =
                tracker.HasCrossedRegionBoundary ? bool.TrueString : bool.FalseString;
        }
    }
}
