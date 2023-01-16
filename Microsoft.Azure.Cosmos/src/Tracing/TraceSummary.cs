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
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

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
        /// List of all HTTP requests and its statistics
        /// </summary>
        public IReadOnlyList<HttpResponseStatistics> HttpResponseStatistics => this.httpResponseStatisticsInternal;

        /// <summary>
        /// List of all TCP requests and its statistics
        /// </summary>
        public IReadOnlyList<StoreResponseStatistics> StoreResponseStatistics => this.storeResponseStatisticsInternal;

        private readonly List<HttpResponseStatistics> httpResponseStatisticsInternal = new List<HttpResponseStatistics>();
        private readonly List<StoreResponseStatistics> storeResponseStatisticsInternal = new List<StoreResponseStatistics>();

        /// <summary>
        /// Consolidates HTTP and Store response statistics
        /// </summary>
        /// <param name="clientSideRequestStatisticsTraceDatum"></param>
        internal void UpdateNetworkStatistics(ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
        {
            if ((clientSideRequestStatisticsTraceDatum.HttpResponseStatisticsList == null ||
                        clientSideRequestStatisticsTraceDatum.HttpResponseStatisticsList.Count == 0) &&
                        (clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList == null ||
                            clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList.Count == 0))
            {
                return;
            }

            lock (this.httpResponseStatisticsInternal)
            {
                this.httpResponseStatisticsInternal.AddRange(clientSideRequestStatisticsTraceDatum.HttpResponseStatisticsList);
            }

            lock (this.storeResponseStatisticsInternal)
            {
                this.storeResponseStatisticsInternal.AddRange(clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList);
            }
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
        /// Update region contacted information in the Summary
        /// </summary>
        /// <param name="clientSideRequestStatisticsTraceDatum"></param>
        internal void UpdateRegionContacted(ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
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

        internal void UpdateSummary(ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
        {
            this.UpdateNetworkStatistics(clientSideRequestStatisticsTraceDatum);
            this.UpdateRegionContacted(clientSideRequestStatisticsTraceDatum);
        }

    }
}