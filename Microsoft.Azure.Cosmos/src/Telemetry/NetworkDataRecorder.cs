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

        public void Record(List<StoreResponseStatistics> storeResponseStatistics, params string[] otherInfo)
        {
            foreach (StoreResponseStatistics storeStatistics in storeResponseStatistics)
            {
                if (NetworkDataRecorder
                        .IsStatusCodeNotExcluded((int)storeStatistics.StoreResult.StatusCode, (int)storeStatistics.StoreResult.SubStatusCode))
                {
                    if (NetworkDataRecorder.IsErrored(storeStatistics))
                    {
                        RequestInfo requestInfo = this.CreateRequestInfo(storeStatistics, otherInfo);
                        LongConcurrentHistogram latencyHist = this.RequestInfoErrorBucket.GetOrAdd(requestInfo, x => new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                                  ClientTelemetryOptions.RequestLatencyMax,
                                                                  ClientTelemetryOptions.RequestLatencyPrecision));
                        latencyHist.RecordValue(storeStatistics.RequestLatency.Ticks);

                    }
                    else
                    {
                        RequestInfo requestInfo = this.CreateRequestInfo(storeStatistics, otherInfo);
                        LongConcurrentHistogram latencyHist = this.RequestInfoHighLatencyBucket.GetOrAdd(requestInfo, x => new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                                  ClientTelemetryOptions.RequestLatencyMax,
                                                                  ClientTelemetryOptions.RequestLatencyPrecision));
                        latencyHist.RecordValue(storeStatistics.RequestLatency.Ticks);
                    }
                }
            }
        }

        public List<RequestInfo> GetErroredRequests()
        {
            ConcurrentDictionary<RequestInfo, LongConcurrentHistogram> requestInfoErrorList 
                = Interlocked.Exchange(ref this.RequestInfoErrorBucket, new ConcurrentDictionary<RequestInfo, LongConcurrentHistogram>());

            List<RequestInfo> requestInfoList = new List<RequestInfo>();
            foreach (KeyValuePair<RequestInfo, LongConcurrentHistogram> entry in requestInfoErrorList)
            {
                MetricInfo metricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                metricInfo.SetAggregators(entry.Value, ClientTelemetryOptions.TicksToMsFactor);

                RequestInfo payloadForLatency = entry.Key;
                payloadForLatency.Metrics.Add(metricInfo);

                requestInfoList.Add(payloadForLatency);
            }

            return DataSampler.SampleByP99(requestInfoList);
        }
        
        private List<RequestInfo> GetHighLatencyRequests()
        {
            ConcurrentDictionary<RequestInfo, LongConcurrentHistogram> requestInfoHighLatencyList 
                = Interlocked.Exchange(ref this.RequestInfoHighLatencyBucket, new ConcurrentDictionary<RequestInfo, LongConcurrentHistogram>());

            List<RequestInfo> requestInfoList = new List<RequestInfo>();
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
                
                requestInfoList.Add(payloadForLatency);
            }

            return DataSampler.SampleByCount(requestInfoList);
        }

        private RequestInfo CreateRequestInfo(StoreResponseStatistics storeResponseStatistic, params string[] otherInfo)
        {
            return new RequestInfo()
                {
                    DatabaseName = otherInfo[0],
                    ContainerName = otherInfo[1],
                    Uri = storeResponseStatistic.StoreResult.StorePhysicalAddress.ToString(),
                    StatusCode = (int)storeResponseStatistic.StoreResult.StatusCode,
                    SubStatusCode = (int)storeResponseStatistic.StoreResult.SubStatusCode,
                    Resource = storeResponseStatistic.RequestResourceType.ToString(),
                    Operation = storeResponseStatistic.RequestOperationType.ToString(),
                };

        }

        public List<RequestInfo> GetRequests()
        {
            List<RequestInfo> requestInfoList = new List<RequestInfo>();
            requestInfoList.AddRange(this.GetErroredRequests());
            requestInfoList.AddRange(this.GetHighLatencyRequests());
            return requestInfoList;
        }

        /// <summary>
        /// This method will return true if the request is failed with User or Server Exception and not excluded from telemetry.
        /// This method will return true if the request latency is more than the threshold.
        /// otherwise return false
        /// </summary>
        /// <returns>true/false</returns>
        private static bool IsErrored(StoreResponseStatistics storeStatistics)
        {
            return NetworkDataRecorder.IsUserOrServerError((int)storeStatistics.StoreResult.StatusCode);
        }

        internal static bool IsHighLatency(double latency)
        {
            return
                latency >= ClientTelemetryOptions.NetworkLatencyThreshold.TotalMilliseconds;
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
