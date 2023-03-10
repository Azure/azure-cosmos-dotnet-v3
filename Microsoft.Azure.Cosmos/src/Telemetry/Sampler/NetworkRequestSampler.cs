//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Sampler
{
    using System;
    using System.Collections.Generic;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

    internal sealed class NetworkRequestSampler : IClientTelemetrySampler<StoreResponseStatistics>
    {
        private readonly int TopNElement = 0;
        private readonly List<int> ExcludedStatusCodes;
        
        public NetworkRequestSampler(int topN, List<int> excludedStatusCodes)
        {
            this.TopNElement = topN;
            this.ExcludedStatusCodes = excludedStatusCodes;
        }
        
        public bool ShouldSample(StoreResponseStatistics storetatistics)
        {
            return this.IsEligible((int)storetatistics.StoreResult.StatusCode, (int)storetatistics.StoreResult.SubStatusCode, storetatistics.RequestLatency);
           
        }
        
        /// <summary>
        /// This method will return true if the request is failed with User or Server Exception and not excluded from telemetry.
        /// This method will return true if the request latency is more than the threshold.
        /// otherwise return false
        /// </summary>
        /// <param name="statusCode"></param>
        /// <param name="subStatusCode"></param>
        /// <param name="latencyInMs"></param>
        /// <returns>true/false</returns>
        private bool IsEligible(int statusCode, int subStatusCode, TimeSpan latencyInMs)
        {
            return
                this.IsStatusCodeNotExcluded(statusCode, subStatusCode) &&
                    (NetworkRequestSampler.IsUserOrServerError(statusCode) || latencyInMs >= ClientTelemetryOptions.NetworkLatencyThreshold);
        }

        private static bool IsUserOrServerError(int statusCode)
        {
            return statusCode >= 400 && statusCode <= 599;
        }

        private bool IsStatusCodeNotExcluded(int statusCode, int subStatusCode)
        {
            return !(this.ExcludedStatusCodes.Contains(statusCode) && subStatusCode == 0);
        }

    }
}
