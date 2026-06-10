// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    /// <summary>
    /// The total count of failed requests for an <see cref="ITrace"/>.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif 
    class TraceSummary
    {
        /// <summary>
        ///  The total count of failed requests for an <see cref="ITrace"/>
        /// </summary>
        private int failedRequestCount = 0;

        /// <summary>
        ///  The increment of failed requests with thread safe for an <see cref="ITrace"/>
        /// </summary>
        public void IncrementFailedCount()
        {
            Interlocked.Increment(ref this.failedRequestCount);
        }

        /// <summary>
        ///  The return the count of failed requests for an <see cref="ITrace"/>
        /// </summary>
        /// <returns>The value of failed requests count</returns>
        public int GetFailedCount()
        {
            return this.failedRequestCount;
        }

        /// <summary>
        /// Consolidated Region contacted Information of this and children nodes
        /// </summary>
        private readonly HashSet<(string, Uri)> regionContactedInternal = new HashSet<(string, Uri)>();

        /// <summary>
        /// Consolidated Region contacted Information of this and children nodes
        /// </summary>
        public IReadOnlyList<(string, Uri)> RegionsContacted
        {
            get
            {
                lock (this.regionContactedInternal)
                {
                    return this.regionContactedInternal.ToList();
                }
            }
        }

        /// <summary>
        /// Update region contacted information to this node
        /// </summary>
        /// <param name="traceDatum"></param>
        public void UpdateRegionContacted(TraceDatum traceDatum)
        {
            if (traceDatum is ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
            {
                if (clientSideRequestStatisticsTraceDatum.RegionsContacted == null ||
                            clientSideRequestStatisticsTraceDatum.RegionsContacted.Count == 0)
                {
                    return;
                }

                // RegionsContacted is a raw HashSet that may be mutated concurrently by
                // Direct-package store-reader paths (e.g. under cross-region read hedging).
                // Snapshot it before UnionWith to avoid `InvalidOperationException:
                // Collection was modified` during enumeration.
                IReadOnlyList<(string, Uri)> regionsContactedSnapshot =
                    ConcurrentCollectionSnapshot.SnapshotCollection(clientSideRequestStatisticsTraceDatum.RegionsContacted);

                lock (this.regionContactedInternal)
                {
                    this.regionContactedInternal.UnionWith(regionsContactedSnapshot);
                }
            }
        }

        /// <summary>
        /// Add region contacted information to this node
        /// </summary>
        /// <param name="regionName"></param>
        /// <param name="locationEndpoint"></param>
        public void AddRegionContacted(string regionName, Uri locationEndpoint)
        {
            lock (this.regionContactedInternal)
            {
                this.regionContactedInternal.Add((regionName, locationEndpoint));
            }
        }

        /// <summary>
        /// Per-operation state backing the Hedging Detection API surface (HedgingStarted /
        /// GetRequestedRegions / GetRespondedRegions on <see cref="CosmosDiagnostics"/>).
        /// </summary>
        /// <remarks>
        /// Lives on <see cref="TraceSummary"/> so that the entire trace tree for a single
        /// operation shares one state instance. Populated at orchestrator dispatch sites
        /// and response-handling sites; never serialized into the trace tree.
        /// </remarks>
        public HedgingDetectionState HedgingDetectionState { get; } = new HedgingDetectionState();

    }
}