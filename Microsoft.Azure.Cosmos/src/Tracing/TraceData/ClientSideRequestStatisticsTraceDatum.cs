// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Text;
    using Microsoft.Azure.Documents;

    internal sealed class ClientSideRequestStatisticsTraceDatum : TraceDatum, IClientSideRequestStatistics
    {
        private static readonly IReadOnlyDictionary<string, AddressResolutionStatistics> EmptyEndpointToAddressResolutionStatistics = new Dictionary<string, AddressResolutionStatistics>();
        private static readonly IReadOnlyList<StoreResponseStatistics> EmptyStoreResponseStatistics = new List<StoreResponseStatistics>();
        private static readonly IReadOnlyList<HttpResponseStatistics> EmptyHttpResponseStatistics = new List<HttpResponseStatistics>();

        private readonly object requestEndTimeLock = new object();
        private readonly long clientSideRequestStatisticsCreateTime;
        private readonly Dictionary<string, AddressResolutionStatistics> endpointToAddressResolutionStats;
        private readonly Dictionary<int, DateTime> recordRequestHashCodeToStartTime;
        private readonly List<StoreResponseStatistics> storeResponseStatistics;
        private readonly List<HttpResponseStatistics> httpResponseStatistics;

        private IReadOnlyDictionary<string, AddressResolutionStatistics> shallowCopyOfEndpointToAddressResolutionStatistics = null;
        private IReadOnlyList<StoreResponseStatistics> shallowCopyOfStoreResponseStatistics = null;
        private IReadOnlyList<HttpResponseStatistics> shallowCopyOfHttpResponseStatistics = null;

        private long? firstStartRequestTimestamp;
        private long? lastStartRequestTimestamp;
        private long cumulativeEstimatedDelayDueToRateLimitingInStopwatchTicks = 0;
        private bool received429ResponseSinceLastStartRequest = false;

        public ClientSideRequestStatisticsTraceDatum(DateTime startTime)
        {
            this.RequestStartTimeUtc = startTime;
            this.RequestEndTimeUtc = null;
            this.endpointToAddressResolutionStats = new Dictionary<string, AddressResolutionStatistics>();
            this.recordRequestHashCodeToStartTime = new Dictionary<int, DateTime>();
            this.ContactedReplicas = new List<Uri>();
            this.storeResponseStatistics = new List<StoreResponseStatistics>();
            this.FailedReplicas = new HashSet<Uri>();
            this.RegionsContactedWithName = new HashSet<(string, Uri)>();
            this.clientSideRequestStatisticsCreateTime = Stopwatch.GetTimestamp();
            this.httpResponseStatistics = new List<HttpResponseStatistics>();
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

        public List<Uri> ContactedReplicas { get; set; }

        public HashSet<Uri> FailedReplicas { get; }

        public HashSet<Uri> RegionsContacted { get; }

        public HashSet<(string, Uri)> RegionsContactedWithName { get; }

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

        public TimeSpan RequestLatency
        {
            get
            {
                if (this.RequestEndTimeUtc.HasValue)
                {
                    return this.RequestEndTimeUtc.Value - this.RequestStartTimeUtc;
                }

                return TimeSpan.MaxValue;
            }
        }

        public bool IsCpuOverloaded { get; private set; } = false;

        public TimeSpan EstimatedClientDelayFromRateLimiting => TimeSpan.FromSeconds(this.cumulativeEstimatedDelayDueToRateLimitingInStopwatchTicks / (double)Stopwatch.Frequency);

        public TimeSpan EstimatedClientDelayFromAllCauses
        {
            get
            {
                if (!this.lastStartRequestTimestamp.HasValue || !this.firstStartRequestTimestamp.HasValue)
                {
                    return TimeSpan.Zero;
                }

                // Stopwatch ticks are not equivalent to DateTime ticks
                long clientDelayInStopWatchTicks = this.lastStartRequestTimestamp.Value - this.firstStartRequestTimestamp.Value;
                return TimeSpan.FromSeconds(clientDelayInStopWatchTicks / (double)Stopwatch.Frequency);
            }
        }

        public void RecordRequest(DocumentServiceRequest request)
        {
            lock (this.recordRequestHashCodeToStartTime)
            {
                long timestamp = Stopwatch.GetTimestamp();
                if (this.received429ResponseSinceLastStartRequest)
                {
                    long lastTimestamp = this.lastStartRequestTimestamp ?? this.clientSideRequestStatisticsCreateTime;
                    this.cumulativeEstimatedDelayDueToRateLimitingInStopwatchTicks += timestamp - lastTimestamp;
                }

                if (!this.firstStartRequestTimestamp.HasValue)
                {
                    this.firstStartRequestTimestamp = timestamp;
                }

                this.lastStartRequestTimestamp = timestamp;
                this.received429ResponseSinceLastStartRequest = false;

                this.recordRequestHashCodeToStartTime[request.GetHashCode()] = DateTime.UtcNow;
            }
        }

        public void RecordResponse(DocumentServiceRequest request, StoreResult storeResult)
        {
            // One DocumentServiceRequest can map to multiple store results
            DateTime? startDateTime = null;
            lock (this.recordRequestHashCodeToStartTime)
            {
                if (this.recordRequestHashCodeToStartTime.TryGetValue(request.GetHashCode(), out DateTime startRequestTime))
                {
                    startDateTime = startRequestTime;
                }
                else
                {
                    Debug.Fail("DocumentServiceRequest start time not recorded");
                }
            }

            DateTime responseTime = this.GetAndUpdateRequestEndTime();
            Uri locationEndpoint = request.RequestContext.LocationEndpointToRoute;
            string regionName = request.RequestContext.RegionName;
            StoreResponseStatistics responseStatistics = new StoreResponseStatistics(
                startDateTime,
                responseTime,
                storeResult,
                request.ResourceType,
                request.OperationType,
                locationEndpoint);

            if (storeResult?.IsClientCpuOverloaded ?? false)
            {
                this.IsCpuOverloaded = true;
            }

            lock (this.storeResponseStatistics)
            {
                if (locationEndpoint != null)
                {
                    this.RegionsContactedWithName.Add((regionName, locationEndpoint));
                }

                // Reset the shallow copy
                this.shallowCopyOfStoreResponseStatistics = null;
                this.storeResponseStatistics.Add(responseStatistics);

                if (!this.received429ResponseSinceLastStartRequest &&
                    storeResult.StatusCode == StatusCodes.TooManyRequests)
                {
                    this.received429ResponseSinceLastStartRequest = true;
                }
            }
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

            DateTime responseTime = this.GetAndUpdateRequestEndTime();

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
            lock (this.httpResponseStatistics)
            {
                this.shallowCopyOfHttpResponseStatistics = null;
                DateTime requestEndTimeUtc = this.GetAndUpdateRequestEndTime();
                this.httpResponseStatistics.Add(new HttpResponseStatistics(requestStartTimeUtc,
                                                                           requestEndTimeUtc,
                                                                           request.RequestUri,
                                                                           request.Method,
                                                                           resourceType,
                                                                           response,
                                                                           exception: null));
            }
        }

        public void RecordHttpException(HttpRequestMessage request,
                                       Exception exception,
                                       ResourceType resourceType,
                                       DateTime requestStartTimeUtc)
        {
            lock (this.httpResponseStatistics)
            {
                this.shallowCopyOfHttpResponseStatistics = null;
                DateTime requestEndTimeUtc = this.GetAndUpdateRequestEndTime();
                this.httpResponseStatistics.Add(new HttpResponseStatistics(requestStartTimeUtc,
                                                                           requestEndTimeUtc,
                                                                           request.RequestUri,
                                                                           request.Method,
                                                                           resourceType,
                                                                           responseMessage: null,
                                                                           exception: exception));
            }
        }

        private DateTime GetAndUpdateRequestEndTime()
        {
            DateTime requestEndTimeUtc = DateTime.UtcNow;
            lock (this.requestEndTimeLock)
            {
                if (!this.RequestEndTimeUtc.HasValue || requestEndTimeUtc > this.RequestEndTimeUtc)
                {
                    this.RequestEndTimeUtc = requestEndTimeUtc;
                }
            }

            return requestEndTimeUtc;
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
                Uri locationEndpoint)
            {
                this.RequestStartTime = requestStartTime;
                this.RequestResponseTime = requestResponseTime;
                this.StoreResult = storeResult;
                this.RequestResourceType = resourceType;
                this.RequestOperationType = operationType;
                this.LocationEndpoint = locationEndpoint;
                this.IsSupplementalResponse = operationType == OperationType.Head || operationType == OperationType.HeadFeed;
            }

            public DateTime? RequestStartTime { get; }
            public DateTime RequestResponseTime { get; }
            public StoreResult StoreResult { get; }
            public ResourceType RequestResourceType { get; }
            public OperationType RequestOperationType { get; }
            public Uri LocationEndpoint { get; }
            public bool IsSupplementalResponse { get; }
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
                Exception exception)
            {
                this.RequestStartTime = requestStartTime;
                this.RequestEndTime = requestEndTime;
                this.HttpResponseMessage = responseMessage;
                this.Exception = exception;
                this.ResourceType = resourceType;
                this.HttpMethod = httpMethod;
                this.RequestUri = requestUri;

                if (responseMessage != null)
                {
                    Headers headers = new Headers(GatewayStoreClient.ExtractResponseHeaders(responseMessage));
                    this.ActivityId = headers.ActivityId ?? Trace.CorrelationManager.ActivityId.ToString();
                }
                else
                {
                    this.ActivityId = Trace.CorrelationManager.ActivityId.ToString();
                }
            }

            public DateTime RequestStartTime { get; }
            public DateTime RequestEndTime { get; }
            public HttpResponseMessage HttpResponseMessage { get; }
            public Exception Exception { get; }
            public ResourceType ResourceType { get; }
            public HttpMethod HttpMethod { get; }
            public Uri RequestUri { get; }
            public string ActivityId { get; }
        }
    }
}
