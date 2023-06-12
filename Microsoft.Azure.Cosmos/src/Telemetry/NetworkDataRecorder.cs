//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

    internal class NetworkDataRecorder
    {
        private ConcurrentDictionary<RequestInfo, LongConcurrentHistogram> RequestInfoHighLatencyBucket 
            = new ConcurrentDictionary<RequestInfo, LongConcurrentHistogram>();
        private ConcurrentDictionary<RequestInfo, LongConcurrentHistogram> RequestInfoErrorBucket
            = new ConcurrentDictionary<RequestInfo, LongConcurrentHistogram>();

        public void Record(List<StoreResponseStatistics> storeResponseStatistics, string databaseId, string containerId)
        {
            foreach (StoreResponseStatistics storeStatistics in storeResponseStatistics)
            {
                if (NetworkDataRecorder.IsStatusCodeNotExcluded((int)storeStatistics.StoreResult.StatusCode, (int)storeStatistics.StoreResult.SubStatusCode))
                {
                    if (NetworkDataRecorder.IsUserOrServerError((int)storeStatistics.StoreResult.StatusCode))
                    {
                        RequestInfo requestInfo = this.CreateRequestInfo(storeStatistics, databaseId, containerId);
                        LongConcurrentHistogram latencyHist = this.RequestInfoErrorBucket.GetOrAdd(requestInfo, x => new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                                  ClientTelemetryOptions.RequestLatencyMax,
                                                                  ClientTelemetryOptions.RequestLatencyPrecision));
                        latencyHist.RecordValue(storeStatistics.RequestLatency.Ticks);

                    }
                    else
                    {
                        RequestInfo requestInfo = this.CreateRequestInfo(storeStatistics, databaseId, containerId);
                        LongConcurrentHistogram latencyHist = this.RequestInfoHighLatencyBucket.GetOrAdd(requestInfo, x => new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                                  ClientTelemetryOptions.RequestLatencyMax,
                                                                  ClientTelemetryOptions.RequestLatencyPrecision));
                        latencyHist.RecordValue(storeStatistics.RequestLatency.Ticks);
                    }
                }
            }
        }

        internal void GetErroredRequests(List<RequestInfo> requestInfoList)
        {
            ConcurrentDictionary<RequestInfo, LongConcurrentHistogram> requestInfoErrorList 
                = Interlocked.Exchange(ref this.RequestInfoErrorBucket, new ConcurrentDictionary<RequestInfo, LongConcurrentHistogram>());

            List<RequestInfo> allRequests = new List<RequestInfo>();
            foreach (KeyValuePair<RequestInfo, LongConcurrentHistogram> entry in requestInfoErrorList)
            {
                MetricInfo metricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                metricInfo.SetAggregators(entry.Value, ClientTelemetryOptions.TicksToMsFactor);

                RequestInfo payloadForLatency = entry.Key;
                payloadForLatency.Metrics.Add(metricInfo);

                allRequests.Add(payloadForLatency);
            }
            
            requestInfoList.AddRange(DataSampler.OrderAndSample(allRequests, DataSampleCountComparer.Instance));
        }
        
        internal void GetHighLatencyRequests(List<RequestInfo> requestInfoList)
        {
            ConcurrentDictionary<RequestInfo, LongConcurrentHistogram> requestInfoHighLatencyList 
                = Interlocked.Exchange(ref this.RequestInfoHighLatencyBucket, new ConcurrentDictionary<RequestInfo, LongConcurrentHistogram>());

            List<RequestInfo> allRequests = new List<RequestInfo>();
            foreach (KeyValuePair<RequestInfo, LongConcurrentHistogram> entry in requestInfoHighLatencyList)
            {
                MetricInfo metricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                metricInfo.SetAggregators(entry.Value, ClientTelemetryOptions.TicksToMsFactor);

                // Don't record if p99 latency is less than threshold
                if (!NetworkDataRecorder.IsHighLatency(metricInfo.Percentiles[ClientTelemetryOptions.Percentile99]))
                {
                    continue;
                }
                
                RequestInfo payloadForLatency = entry.Key;
                payloadForLatency.Metrics.Add(metricInfo);

                allRequests.Add(payloadForLatency);
            }

            requestInfoList.AddRange(DataSampler.OrderAndSample(allRequests, DataLatencyComparer.Instance));
        }

        internal RequestInfo CreateRequestInfo(StoreResponseStatistics storeResponseStatistic, string databaseId, string containerId)
        {
            return new RequestInfo()
                {
                    DatabaseName = databaseId,
                    ContainerName = containerId,
                    Uri = storeResponseStatistic.StoreResult?.StorePhysicalAddress?.ToString(),
                    StatusCode = (int)storeResponseStatistic.StoreResult?.StatusCode,
                    SubStatusCode = (int)storeResponseStatistic.StoreResult?.SubStatusCode,
                    Resource = storeResponseStatistic.RequestResourceType.ToString(),
                    Operation = storeResponseStatistic.RequestOperationType.ToString(),
                };
        }

        public List<RequestInfo> GetRequests()
        {
            List<RequestInfo> requestInfoList = new List<RequestInfo>();
            this.GetErroredRequests(requestInfoList);
            this.GetHighLatencyRequests(requestInfoList);
            
            return requestInfoList;
        }

        internal static bool IsHighLatency(double latency)
        {
            return
                latency >= ClientTelemetryOptions.NetworkLatencyThreshold.TotalMilliseconds;
        }

        /// <summary>
        /// This method will return true if the request is failed with User or Server Exception and not excluded from telemetry.
        /// otherwise return false
        /// </summary>
        /// <returns>true/false</returns>
        internal static bool IsUserOrServerError(int statusCode)
        {
            return statusCode >= 400 && statusCode <= 599;
        }

        internal static bool IsStatusCodeNotExcluded(int statusCode, int subStatusCode)
        {
            return !(ClientTelemetryOptions.ExcludedStatusCodes.Contains(statusCode) && subStatusCode == 0);
        }
    }
}
