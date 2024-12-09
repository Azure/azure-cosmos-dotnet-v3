// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;

    internal sealed class ClientSideRequestStatisticsTraceDatum : TraceDatum, IClientSideRequestStatistics
    {
        private static readonly IReadOnlyDictionary<string, AddressResolutionStatistics> EmptyEndpointToAddressResolutionStatistics = new Dictionary<string, AddressResolutionStatistics>();
        private static readonly IReadOnlyList<StoreResponseStatistics> EmptyStoreResponseStatistics = new List<StoreResponseStatistics>();
        private static readonly IReadOnlyList<HttpResponseStatistics> EmptyHttpResponseStatistics = new List<HttpResponseStatistics>();
        private readonly List<(PartitionAddressInformation existing, PartitionAddressInformation newInfo)> partitionAddressInformationRefreshes = new List<(PartitionAddressInformation existing, PartitionAddressInformation newInfo)>();

        internal static readonly string HttpRequestRegionNameProperty = "regionName";

        private readonly object requestEndTimeLock = new object();
        private readonly Dictionary<string, AddressResolutionStatistics> endpointToAddressResolutionStats;
        private readonly List<StoreResponseStatistics> storeResponseStatistics;
        private readonly List<HttpResponseStatistics> httpResponseStatistics;

        private IReadOnlyDictionary<string, AddressResolutionStatistics> shallowCopyOfEndpointToAddressResolutionStatistics = null;
        private IReadOnlyList<StoreResponseStatistics> shallowCopyOfStoreResponseStatistics = null;
        private IReadOnlyList<HttpResponseStatistics> shallowCopyOfHttpResponseStatistics = null;
        private SystemUsageHistory systemUsageHistory = null;

        public ClientSideRequestStatisticsTraceDatum(DateTime startTime, ITrace trace)
        {
            this.RequestStartTimeUtc = startTime;
            this.RequestEndTimeUtc = null;
            this.endpointToAddressResolutionStats = new Dictionary<string, AddressResolutionStatistics>();
            this.ContactedReplicas = new List<TransportAddressUri>();
            this.storeResponseStatistics = new List<StoreResponseStatistics>();
            this.FailedReplicas = new HashSet<TransportAddressUri>();
            this.RegionsContacted = new HashSet<(string, Uri)>();
            this.httpResponseStatistics = new List<HttpResponseStatistics>();
            this.Trace = trace;
        }

        public DateTime RequestStartTimeUtc { get; }

        public DateTime? RequestEndTimeUtc { get; private set; }

        public IReadOnlyDictionary<string, AddressResolutionStatistics> EndpointToAddressResolutionStatistics
        {
            get
            {
                if (this.endpointToAddressResolutionStats.Count == 0)
                {
                    return ClientSideRequestStatisticsTraceDatum.EmptyEndpointToAddressResolutionStatistics;
                }

                lock (this.endpointToAddressResolutionStats)
                {
                    this.shallowCopyOfEndpointToAddressResolutionStatistics ??= new Dictionary<string, AddressResolutionStatistics>(this.endpointToAddressResolutionStats);
                    return this.shallowCopyOfEndpointToAddressResolutionStatistics;
                }
            }
        }

        public List<TransportAddressUri> ContactedReplicas { get; set; }

        public HashSet<TransportAddressUri> FailedReplicas { get; }

        public HashSet<(string, Uri)> RegionsContacted { get; }

        public ITrace Trace { get; private set; }

        public TraceSummary TraceSummary => this.Trace?.Summary;

        public IReadOnlyList<StoreResponseStatistics> StoreResponseStatisticsList
        {
            get
            {
                if (this.storeResponseStatistics.Count == 0)
                {
                    return ClientSideRequestStatisticsTraceDatum.EmptyStoreResponseStatistics;
                }

                lock (this.storeResponseStatistics)
                {
                    this.shallowCopyOfStoreResponseStatistics ??= new List<StoreResponseStatistics>(this.storeResponseStatistics);
                    return this.shallowCopyOfStoreResponseStatistics;
                }
            }
        }

        public IReadOnlyList<HttpResponseStatistics> HttpResponseStatisticsList
        {
            get
            {
                if (this.httpResponseStatistics.Count == 0)
                {
                    return ClientSideRequestStatisticsTraceDatum.EmptyHttpResponseStatistics;
                }

                lock (this.httpResponseStatistics)
                {
                    this.shallowCopyOfHttpResponseStatistics ??= new List<HttpResponseStatistics>(this.httpResponseStatistics);
                    return this.shallowCopyOfHttpResponseStatistics;
                }
            }
        }

        public TimeSpan? RequestLatency
        {
            get
            {
                if (this.RequestEndTimeUtc.HasValue)
                {
                    return this.RequestEndTimeUtc.Value - this.RequestStartTimeUtc;
                }

                return null;
            }
        }

        public bool? IsCpuHigh => this.systemUsageHistory?.IsCpuHigh;

        public bool? IsCpuThreadStarvation => this.systemUsageHistory?.IsCpuThreadStarvation;

        public void RecordRequest(DocumentServiceRequest request)
        {
        }

        public void RecordAddressCachRefreshContent(
            PartitionAddressInformation existingInfo,
            PartitionAddressInformation newInfo)
        {
            lock (this.partitionAddressInformationRefreshes)
            {
                this.partitionAddressInformationRefreshes.Add((existingInfo, newInfo));
            }
        }

        public void WriteAddressCachRefreshContent(IJsonWriter jsonWriter)
        {
            if (this.partitionAddressInformationRefreshes.Count == 0)
            {
                return;
            }

            lock (this.partitionAddressInformationRefreshes)
            {
                if (this.partitionAddressInformationRefreshes.Count == 0)
                {
                    return;
                }

                jsonWriter.WriteFieldName("ForceAddressRefresh");

                jsonWriter.WriteArrayStart();
                foreach ((PartitionAddressInformation existing, PartitionAddressInformation newInfo) in this.partitionAddressInformationRefreshes)
                {
                    // Avoid printing the same list twice if the cache update did not change any values
                    if (ClientSideRequestStatisticsTraceDatum.IsSamePartitionAddressInformation(existing, newInfo))
                    {
                        jsonWriter.WriteObjectStart();
                        jsonWriter.WriteFieldName("No change to cache");
                        jsonWriter.WriteArrayStart();
                        foreach (AddressInformation addressInformation in existing.AllAddresses)
                        {
                            jsonWriter.WriteStringValue(addressInformation.PhysicalUri);
                        }

                        jsonWriter.WriteArrayEnd();
                        jsonWriter.WriteObjectEnd();
                    }
                    else
                    {
                        jsonWriter.WriteObjectStart();
                        jsonWriter.WriteFieldName("Original");
                        jsonWriter.WriteArrayStart();
                        foreach (AddressInformation addressInformation in existing.AllAddresses)
                        {
                            jsonWriter.WriteStringValue(addressInformation.PhysicalUri);
                        }

                        jsonWriter.WriteArrayEnd();

                        jsonWriter.WriteFieldName("New");
                        jsonWriter.WriteArrayStart();
                        foreach (AddressInformation addressInformation in newInfo.AllAddresses)
                        {
                            jsonWriter.WriteStringValue(addressInformation.PhysicalUri);
                        }
                        jsonWriter.WriteArrayEnd();
                        jsonWriter.WriteObjectEnd();
                    }
                }

                jsonWriter.WriteArrayEnd();
            }
        }

        private static bool IsSamePartitionAddressInformation(
            PartitionAddressInformation info1,
            PartitionAddressInformation info2)
        {
            if (info1 == null && info2 == null)
            {
                return true;
            }

            if (info1 != null && info2 == null)
            {
                return false;
            }

            if (info1 == null && info2 != null)
            {
                return false;
            }

            if (info1.AllAddresses.Count != info2.AllAddresses.Count)
            {
                return false;
            }

            // Assumes both lists are in the same order.
            for (int i = 0; i < info1.AllAddresses.Count; i++)
            {
                AddressInformation info1AddressInfo = info1.AllAddresses[i];
                AddressInformation info2AddressInfo = info2.AllAddresses[i];
                if (info1AddressInfo.Protocol != info2AddressInfo.Protocol
                    || info1AddressInfo.IsPrimary != info2AddressInfo.IsPrimary
                    || info1AddressInfo.IsPublic != info2AddressInfo.IsPublic
                    || info1AddressInfo.PhysicalUri != info2AddressInfo.PhysicalUri)
                {
                    return false;
                }
            }

            return true;
        }

        public void RecordResponse(
            DocumentServiceRequest request,
            StoreResult storeResult,
            DateTime startTimeUtc,
            DateTime endTimeUtc)
        {
            this.UpdateRequestEndTime(endTimeUtc);
            Uri locationEndpoint = request.RequestContext.LocationEndpointToRoute;
            string regionName = request.RequestContext.RegionName;
            StoreResponseStatistics responseStatistics = new StoreResponseStatistics(
                startTimeUtc,
                endTimeUtc,
                storeResult,
                request.ResourceType,
                request.OperationType,
                request.Headers[HttpConstants.HttpHeaders.SessionToken],
                locationEndpoint, 
                regionName);

            lock (this.storeResponseStatistics)
            {
                if (locationEndpoint != null)
                {
                    this.TraceSummary?.AddRegionContacted(regionName, locationEndpoint);
                }

                if (responseStatistics.StoreResult != null && !((HttpStatusCode)responseStatistics.StoreResult.StatusCode).IsSuccess()
                    && !(responseStatistics.StoreResult.StatusCode == StatusCodes.NotFound && responseStatistics.StoreResult.SubStatusCode == SubStatusCodes.Unknown)
                    && !(responseStatistics.StoreResult.StatusCode == StatusCodes.Conflict && responseStatistics.StoreResult.SubStatusCode == SubStatusCodes.Unknown)
                    && !(responseStatistics.StoreResult.StatusCode == StatusCodes.PreconditionFailed && responseStatistics.StoreResult.SubStatusCode == SubStatusCodes.Unknown))
                {
                    if (this.TraceSummary != null)
                    {
                        this.TraceSummary.IncrementFailedCount();
                    }
                }
                
                // Reset the shallow copy
                this.shallowCopyOfStoreResponseStatistics = null;
                this.storeResponseStatistics.Add(responseStatistics);
            }
        }

        public void RecordException(
            DocumentServiceRequest request,
            Exception exception,
            DateTime startTimeUtc,
            DateTime endTimeUtc)
        {
            this.UpdateRequestEndTime(endTimeUtc);
        }

        public string RecordAddressResolutionStart(Uri targetEndpoint)
        {
            string identifier = Guid.NewGuid().ToString();
            AddressResolutionStatistics resolutionStats = new AddressResolutionStatistics(
                startTime: DateTime.UtcNow,
                endTime: DateTime.MaxValue,
                targetEndpoint: targetEndpoint == null ? "<NULL>" : targetEndpoint.ToString());

            lock (this.endpointToAddressResolutionStats)
            {
                // Reset the shallow copy
                this.shallowCopyOfEndpointToAddressResolutionStatistics = null;
                this.endpointToAddressResolutionStats.Add(identifier, resolutionStats);
            }

            return identifier;
        }

        public void RecordAddressResolutionEnd(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return;
            }

            DateTime responseTime = DateTime.UtcNow;
            this.UpdateRequestEndTime(responseTime);

            lock (this.endpointToAddressResolutionStats)
            {
                if (!this.endpointToAddressResolutionStats.ContainsKey(identifier))
                {
                    throw new ArgumentException("Identifier {0} does not exist. Please call start before calling end.", identifier);
                }

                AddressResolutionStatistics start = this.endpointToAddressResolutionStats[identifier];

                // Reset the shallow copy
                this.shallowCopyOfEndpointToAddressResolutionStatistics = null;
                this.endpointToAddressResolutionStats[identifier] = new AddressResolutionStatistics(
                    start.StartTime,
                    responseTime,
                    start.TargetEndpoint);
            }
        }

        public void RecordHttpResponse(HttpRequestMessage request,
                                       HttpResponseMessage response,
                                       ResourceType resourceType,
                                       DateTime requestStartTimeUtc)
        {
            DateTime requestEndTimeUtc = DateTime.UtcNow;
            this.UpdateRequestEndTime(requestEndTimeUtc);

            lock (this.httpResponseStatistics)
            {
                Uri locationEndpoint = request.RequestUri;
                object regionName = null;
                if (request.Properties != null && 
                        request.Properties.TryGetValue(HttpRequestRegionNameProperty, out regionName))
                {
                    this.TraceSummary.AddRegionContacted(Convert.ToString(regionName), locationEndpoint);
                }

                this.shallowCopyOfHttpResponseStatistics = null;
                this.httpResponseStatistics.Add(new HttpResponseStatistics(requestStartTimeUtc,
                                                                           requestEndTimeUtc,
                                                                           request.RequestUri,
                                                                           request.Method,
                                                                           resourceType,
                                                                           response,
                                                                           exception: null,
                                                                           region: Convert.ToString(regionName)));
            }
        }

        public void RecordHttpException(HttpRequestMessage request,
                                       Exception exception,
                                       ResourceType resourceType,
                                       DateTime requestStartTimeUtc)
        {
            DateTime requestEndTimeUtc = DateTime.UtcNow;
            this.UpdateRequestEndTime(requestEndTimeUtc);

            lock (this.httpResponseStatistics)
            {
                Uri locationEndpoint = request.RequestUri;

                object regionName = null;
                if (request.Properties != null &&
                        request.Properties.TryGetValue(HttpRequestRegionNameProperty, out regionName))
                {
                    this.TraceSummary.AddRegionContacted(Convert.ToString(regionName), locationEndpoint);
                }

                this.shallowCopyOfHttpResponseStatistics = null;
                this.httpResponseStatistics.Add(new HttpResponseStatistics(requestStartTimeUtc,
                                                                           requestEndTimeUtc,
                                                                           request.RequestUri,
                                                                           request.Method,
                                                                           resourceType,
                                                                           responseMessage: null,
                                                                           exception: exception,
                                                                           region: Convert.ToString(regionName)));
            }
        }

        private DateTime UpdateRequestEndTime(DateTime requestEndTimeUtc)
        {
            lock (this.requestEndTimeLock)
            {
                if (!this.RequestEndTimeUtc.HasValue || requestEndTimeUtc > this.RequestEndTimeUtc)
                {
                    this.RequestEndTimeUtc = requestEndTimeUtc;
                }

                this.UpdateSystemUsage();
            }

            return requestEndTimeUtc;
        }

        public void UpdateSystemUsage()
        {
#if !INTERNAL
            if (this.systemUsageHistory == null ||
                this.systemUsageHistory.Values.Count == 0 ||
                this.systemUsageHistory.LastTimestamp + DiagnosticsHandlerHelper.DiagnosticsRefreshInterval < DateTime.UtcNow)
            {
                this.systemUsageHistory = DiagnosticsHandlerHelper.GetInstance().GetDiagnosticsSystemHistory();
            }
#endif
        }

        internal override void Accept(ITraceDatumVisitor traceDatumVisitor)
        {
            traceDatumVisitor.Visit(this);
        }

        public void AppendToBuilder(StringBuilder stringBuilder)
        {
            throw new NotImplementedException();
        }

        public readonly struct AddressResolutionStatistics
        {
            public AddressResolutionStatistics(
                DateTime startTime,
                DateTime endTime,
                string targetEndpoint)
            {
                this.StartTime = startTime;
                this.EndTime = endTime;
                this.TargetEndpoint = targetEndpoint ?? throw new ArgumentNullException(nameof(startTime));
            }

            public DateTime StartTime { get; }
            public DateTime? EndTime { get; }
            public string TargetEndpoint { get; }
        }

        public sealed class StoreResponseStatistics
        {
            public StoreResponseStatistics(
                DateTime? requestStartTime,
                DateTime requestResponseTime,
                StoreResult storeResult,
                ResourceType resourceType,
                OperationType operationType,
                string requestSessionToken,
                Uri locationEndpoint,
                string region)
            {
                this.RequestStartTime = requestStartTime;
                this.RequestResponseTime = requestResponseTime;
                this.StoreResult = storeResult;
                this.RequestResourceType = resourceType;
                this.RequestOperationType = operationType;
                this.RequestSessionToken = requestSessionToken;
                this.LocationEndpoint = locationEndpoint;
                this.IsSupplementalResponse = operationType == OperationType.Head || operationType == OperationType.HeadFeed;
                this.Region = region;
            }

            public string Region { get; }
            public DateTime? RequestStartTime { get; }
            public DateTime RequestResponseTime { get; }
            public StoreResult StoreResult { get; }
            public ResourceType RequestResourceType { get; }
            public OperationType RequestOperationType { get; }
            public string RequestSessionToken { get; }
            public Uri LocationEndpoint { get; }
            public bool IsSupplementalResponse { get; }
            public TimeSpan RequestLatency => this.RequestResponseTime - this.RequestStartTime.GetValueOrDefault();
        }

        public readonly struct HttpResponseStatistics
        {
            public HttpResponseStatistics(
                DateTime requestStartTime,
                DateTime requestEndTime,
                Uri requestUri,
                HttpMethod httpMethod,
                ResourceType resourceType,
                HttpResponseMessage responseMessage,
                Exception exception,
                string region)
            {
                this.RequestStartTime = requestStartTime;
                this.Duration = requestEndTime - requestStartTime;
                this.HttpResponseMessage = responseMessage;
                this.Exception = exception;
                this.ResourceType = resourceType;
                this.HttpMethod = httpMethod;
                this.RequestUri = requestUri;
                this.Region = region;
                this.ResponseContentLength = responseMessage?.Content?.Headers?.ContentLength;
                if (responseMessage != null)
                {
                    Headers headers = new Headers(GatewayStoreClient.ExtractResponseHeaders(responseMessage));
                    this.ActivityId = headers.ActivityId ?? System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString();
                }
                else
                {
                    this.ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString();
                }
            }

            public string Region { get; }
            public DateTime RequestStartTime { get; }
            public TimeSpan Duration { get; }
            public HttpResponseMessage HttpResponseMessage { get; }
            public Exception Exception { get; }
            public ResourceType ResourceType { get; }
            public HttpMethod HttpMethod { get; }
            public Uri RequestUri { get; }
            public string ActivityId { get; }
            public long? ResponseContentLength { get; }
        }
    }
}
