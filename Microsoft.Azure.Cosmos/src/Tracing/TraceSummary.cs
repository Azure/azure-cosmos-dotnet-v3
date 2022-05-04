// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
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
        private HashSet<(string, Uri)> regionContactedInternal = new HashSet<(string, Uri)>();

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

                lock (this.regionContactedInternal)
                {
                    this.regionContactedInternal.UnionWith(clientSideRequestStatisticsTraceDatum.RegionsContacted);
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
            if (this.regionContactedInternal == null)
            {
                this.regionContactedInternal = new HashSet<(string, Uri)>();
            }
            lock (this.regionContactedInternal)
            {
                this.regionContactedInternal.Add((Convert.ToString(regionName), locationEndpoint));
            }
        }

        /// <summary>
        ///  The Consolidated Region contacted Information of this and children nodes for an <see cref="ITrace"/>
        /// </summary>
        /// <returns>The value of regions contacted list</returns>
        public IReadOnlyList<(string, Uri)> GetRegionsContacted()
        {
            lock (this.regionContactedInternal)
            {
                return this.regionContactedInternal.ToList();
            }
        }

    }
}
