//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Sampler
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Telemetry.Models;

    internal sealed class NetworkRequestSampler : IClientTelemetrySampler<RequestInfo>
    {
        private readonly int NetworkFailuresCount;
        private readonly ISet<RequestInfo> TempStorage;
        
        public NetworkRequestSampler(int maxNumberOfFailures)
        {
            this.NetworkFailuresCount = maxNumberOfFailures;
            this.TempStorage = new HashSet<RequestInfo>();
        }
        
        public bool ShouldSample(RequestInfo requestInfo)
        {
            if (requestInfo == null)
            {
                return false;
            }
            
            if (this.TempStorage.Count < this.NetworkFailuresCount)
            {
                return this.TempStorage.Add(requestInfo);
            }
            else
            {
                bool isAdded = this.TempStorage.Add(requestInfo);
                if (isAdded)
                {
                    this.TempStorage.Remove(requestInfo);
                }
                return !isAdded;
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

        public void Dispose()
        {
            this.TempStorage.Clear();
        }
    }
}
