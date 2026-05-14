//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Per-trace (per-operation) state backing the Hedging Detection API surface
    /// (<see cref="CosmosDiagnostics.HedgingStarted"/>,
    /// <see cref="CosmosDiagnostics.GetRequestedRegions"/>,
    /// <see cref="CosmosDiagnostics.GetRespondedRegions"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lives on <see cref="TraceSummary"/> so it is shared across the entire trace tree
    /// for a single operation. All mutators and accessors acquire the same private
    /// lock, matching the shared-lock pattern used cross-SDK (see internal-spec.md §3.5
    /// and SE-017 in side-effects.json).
    /// </para>
    /// <para>
    /// The new state is held here (not serialized into the trace tree as a
    /// trace datum) so that the JSON shape produced by
    /// <see cref="CosmosDiagnostics.ToString"/> is unchanged.
    /// </para>
    /// </remarks>
#if INTERNAL
    public
#else
    internal
#endif
    sealed class HedgingDetectionState
    {
        /// <summary>
        /// Well-known key used to stash a pending dispatch-reason override on
        /// <see cref="RequestMessage.Properties"/> (or
        /// <see cref="Microsoft.Azure.Documents.DocumentServiceRequestContext"/>'s
        /// properties bag) so an upstream site (e.g. ClientRetryPolicy or the hedging
        /// orchestrator) can signal the downstream dispatch site why the next attempt
        /// is happening. The value must be a <see cref="RequestedRegionReason"/>.
        /// </summary>
        internal const string DispatchReasonPropertyKey = "__CosmosDB_HedgingDetection_NextDispatchReason";

        private readonly object regionLock = new object();
        private List<RequestedRegion> requestedRegions;
        private List<string> respondedRegions;
        private bool hedgingStarted;

        /// <summary>
        /// Appends a dispatched-region entry. No-op if <paramref name="regionName"/> is
        /// null or empty. When <paramref name="reason"/> is
        /// <see cref="RequestedRegionReason.Hedging"/> the <c>HedgingStarted</c> flag is
        /// also flipped to <c>true</c>.
        /// </summary>
        internal void AppendRequested(string regionName, RequestedRegionReason reason)
        {
            if (string.IsNullOrEmpty(regionName))
            {
                return;
            }

            lock (this.regionLock)
            {
                if (this.requestedRegions == null)
                {
                    this.requestedRegions = new List<RequestedRegion>(capacity: 4);
                }

                this.requestedRegions.Add(new RequestedRegion(regionName, reason));

                if (reason == RequestedRegionReason.Hedging)
                {
                    this.hedgingStarted = true;
                }
            }
        }

        /// <summary>
        /// Appends a responded-region entry. No-op if <paramref name="regionName"/> is
        /// null or empty. Duplicates are allowed by design (see internal-spec §3.1
        /// "Duplicates ARE allowed and ARE expected").
        /// </summary>
        internal void AppendResponded(string regionName)
        {
            if (string.IsNullOrEmpty(regionName))
            {
                return;
            }

            lock (this.regionLock)
            {
                if (this.respondedRegions == null)
                {
                    this.respondedRegions = new List<string>(capacity: 4);
                }

                this.respondedRegions.Add(regionName);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if at least one <see cref="RequestedRegionReason.Hedging"/>
        /// entry has been appended for this operation.
        /// </summary>
        internal bool HedgingStarted
        {
            get
            {
                lock (this.regionLock)
                {
                    return this.hedgingStarted;
                }
            }
        }

        /// <summary>
        /// Returns a snapshot of the dispatched-region list. Snapshot is taken under the
        /// lock; the returned array is independent of subsequent mutations.
        /// </summary>
        internal IReadOnlyList<RequestedRegion> GetRequestedRegionsSnapshot()
        {
            lock (this.regionLock)
            {
                if (this.requestedRegions == null || this.requestedRegions.Count == 0)
                {
                    return Array.Empty<RequestedRegion>();
                }

                return this.requestedRegions.ToArray();
            }
        }

        /// <summary>
        /// Returns a snapshot of the responded-region list in arrival order. Duplicates
        /// preserved. Snapshot is taken under the lock; the returned array is independent
        /// of subsequent mutations.
        /// </summary>
        internal IReadOnlyList<string> GetRespondedRegionsSnapshot()
        {
            lock (this.regionLock)
            {
                if (this.respondedRegions == null || this.respondedRegions.Count == 0)
                {
                    return Array.Empty<string>();
                }

                return this.respondedRegions.ToArray();
            }
        }
    }
}
