// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using Microsoft.Azure.Documents;

    internal sealed class ClientSideRequestStatisticsTraceDatum : TraceDatum, IClientSideRequestStatistics
    {
        private readonly object lockObject = new object();
        private readonly long clientSideRequestStatisticsCreateTime;

        private long? firstStartRequestTimestamp;
        private long? lastStartRequestTimestamp;
        private long cumulativeEstimatedDelayDueToRateLimitingInStopwatchTicks = 0;
        private bool received429ResponseSinceLastStartRequest = false;

        public ClientSideRequestStatisticsTraceDatum(DateTime startTime)
        {
            this.RequestStartTimeUtc = startTime;
            this.RequestEndTimeUtc = null;
            this.EndpointToAddressResolutionStatistics = new Dictionary<string, AddressResolutionStatistics>();
            this.RecordRequestHashCodeToStartTime = new Dictionary<int, DateTime>();
            this.ContactedReplicas = new List<Uri>();
            this.StoreResponseStatisticsList = new List<StoreResponseStatistics>();
            this.FailedReplicas = new HashSet<Uri>();
            this.RegionsContacted = new HashSet<Uri>();
            this.clientSideRequestStatisticsCreateTime = Stopwatch.GetTimestamp();
        }

        public DateTime RequestStartTimeUtc { get; }

        public DateTime? RequestEndTimeUtc { get; set; }

        public Dictionary<string, AddressResolutionStatistics> EndpointToAddressResolutionStatistics { get; }

        private Dictionary<int, DateTime> RecordRequestHashCodeToStartTime { get; }

        public List<Uri> ContactedReplicas { get; set; }

        public List<StoreResponseStatistics> StoreResponseStatisticsList { get; }

        public HashSet<Uri> FailedReplicas { get; }

        public HashSet<Uri> RegionsContacted { get; }

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
            lock (this.lockObject)
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
            }

            this.RecordRequestHashCodeToStartTime[request.GetHashCode()] = DateTime.UtcNow;
        }

        public void RecordResponse(DocumentServiceRequest request, StoreResult storeResult)
        {
            // One DocumentServiceRequest can map to multiple store results
            DateTime? startDateTime = null;
            if (this.RecordRequestHashCodeToStartTime.TryGetValue(request.GetHashCode(), out DateTime startRequestTime))
            {
                startDateTime = startRequestTime;
            }
            else
            {
                Debug.Fail("DocumentServiceRequest start time not recorded");
            }

            DateTime responseTime = DateTime.UtcNow;
            Uri locationEndpoint = request.RequestContext.LocationEndpointToRoute;
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

            lock (this.lockObject)
            {
                if (!this.RequestEndTimeUtc.HasValue || responseTime > this.RequestEndTimeUtc)
                {
                    this.RequestEndTimeUtc = responseTime;
                }

                if (locationEndpoint != null)
                {
                    this.RegionsContacted.Add(locationEndpoint);
                }

                this.StoreResponseStatisticsList.Add(responseStatistics);

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

            lock (this.lockObject)
            {
                this.EndpointToAddressResolutionStatistics.Add(identifier, resolutionStats);
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
            lock (this.lockObject)
            {
                if (!this.EndpointToAddressResolutionStatistics.ContainsKey(identifier))
                {
                    throw new ArgumentException("Identifier {0} does not exist. Please call start before calling end.", identifier);
                }

                if (!this.RequestEndTimeUtc.HasValue || responseTime > this.RequestEndTimeUtc)
                {
                    this.RequestEndTimeUtc = responseTime;
                }

                AddressResolutionStatistics start = this.EndpointToAddressResolutionStatistics[identifier];

                this.EndpointToAddressResolutionStatistics[identifier] = new AddressResolutionStatistics(
                    start.StartTime,
                    responseTime,
                    start.TargetEndpoint);
            }
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
    }
}
