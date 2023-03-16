//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

    /// <summary>
    /// This is Sample applied to all network calls during an operation call
    /// </summary>
    internal class NetworkRequestSampler : IClientTelemetrySampler<StoreResponseStatistics, RequestInfo>
    {
        private readonly string DatabaseId;
        private readonly string ContainerId;

        private readonly ISampler<RequestInfo> TopNSampler;
        
        public NetworkRequestSampler(string databaseId, string containerId)
        {
            this.DatabaseId = databaseId;
            this.ContainerId = containerId;
            
            this.TopNSampler
                = new TopNSampler(ClientTelemetryOptions.NetworkTelemetrySampleSize);
        }

        /// <summary>
        /// Sampling implementation
        /// </summary>
        /// <param name="storeResponseStatistics">This is in input data</param>
        /// <param name="droppedRntbdRequestCount">Number of request skipped due to sampling</param>
        /// <param name="callback">call this to run logic on selected object</param>
        public void Sample(List<StoreResponseStatistics> storeResponseStatistics, out int droppedRntbdRequestCount, Action<StoreResponseStatistics, RequestInfo> callback)
        {
            droppedRntbdRequestCount = 0;
            foreach (StoreResponseStatistics storeStatistics in storeResponseStatistics)
            {
                if (NetworkRequestSampler.IsEligible(
                        statusCode: (int)storeStatistics.StoreResult.StatusCode,
                        subStatusCode: (int)storeStatistics.StoreResult.SubStatusCode,
                        latencyInMs: storeStatistics.RequestLatency))
                {
                    RequestInfo requestInfo = new RequestInfo()
                    {
                        DatabaseName = this.DatabaseId,
                        ContainerName = this.ContainerId,
                        Uri = storeStatistics.StoreResult.StorePhysicalAddress.ToString(),
                        StatusCode = (int)storeStatistics.StoreResult.StatusCode,
                        SubStatusCode = (int)storeStatistics.StoreResult.SubStatusCode,
                        Resource = storeStatistics.RequestResourceType.ToString(),
                        Operation = storeStatistics.RequestOperationType.ToString(),
                    };

                    if (this.TopNSampler.ShouldSample(requestInfo))
                    {
                        callback(storeStatistics, requestInfo);
                    }
                    else
                    {
                        droppedRntbdRequestCount++;
                    }
                }

            }
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
        public static bool IsEligible(int statusCode, int subStatusCode, TimeSpan latencyInMs)
        {
            return
                NetworkRequestSampler.IsStatusCodeNotExcluded(statusCode, subStatusCode) &&
                    (NetworkRequestSampler.IsUserOrServerError(statusCode) || latencyInMs >= ClientTelemetryOptions.NetworkLatencyThreshold);
        }

        private static bool IsUserOrServerError(int statusCode)
        {
            return statusCode >= 400 && statusCode <= 599;
        }

        private static bool IsStatusCodeNotExcluded(int statusCode, int subStatusCode)
        {
            return !(ClientTelemetryOptions.ExcludedStatusCodes.Contains(statusCode) && subStatusCode == 0);
        }
    }
}
