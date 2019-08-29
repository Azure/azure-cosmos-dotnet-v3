//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal sealed class CosmosClientSideRequestStatistics : IClientSideRequestStatistics
    {
        internal const int MaxSupplementalRequestsForToString = 10;

        internal DateTime requestStartTime;
        internal DateTime requestEndTime;

        private object lockObject = new object();

        internal List<StoreResponseStatistics> responseStatisticsList;
        internal List<StoreResponseStatistics> supplementalResponseStatisticsList;
        internal Dictionary<string, AddressResolutionStatistics> addressResolutionStatistics;

        [JsonIgnoreAttribute]
        public List<Uri> ContactedReplicas { get; set; }
        [JsonIgnoreAttribute]
        public HashSet<Uri> FailedReplicas { get; private set; }
        [JsonIgnoreAttribute]
        public HashSet<Uri> RegionsContacted { get; private set; }

        public CosmosClientSideRequestStatistics()
        {
            this.requestStartTime = DateTime.UtcNow;
            this.requestEndTime = DateTime.UtcNow;
            this.responseStatisticsList = new List<StoreResponseStatistics>();
            this.supplementalResponseStatisticsList = new List<StoreResponseStatistics>();
            this.addressResolutionStatistics = new Dictionary<string, AddressResolutionStatistics>();
            this.ContactedReplicas = new List<Uri>();
            this.FailedReplicas = new HashSet<Uri>();
            this.RegionsContacted = new HashSet<Uri>();
        }

        public TimeSpan RequestLatency
        {
            get
            {
                return this.requestEndTime - this.requestStartTime;
            }
        }

        public bool IsCpuOverloaded
        {
            get
            {
                foreach (StoreResponseStatistics responseStatistics in this.responseStatisticsList)
                {
                    if (responseStatistics.StoreResult.IsClientCpuOverloaded)
                    {
                        return true;
                    }
                }
                foreach (StoreResponseStatistics responseStatistics in this.supplementalResponseStatisticsList)
                {
                    if (responseStatistics.StoreResult.IsClientCpuOverloaded)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public void RecordResponse(DocumentServiceRequest request, StoreResult storeResult)
        {
            DateTime responseTime = DateTime.UtcNow;

            StoreResponseStatistics responseStatistics;
            responseStatistics.RequestResponseTime = responseTime;
            responseStatistics.StoreResult = storeResult;
            responseStatistics.RequestOperationType = request.OperationType;
            responseStatistics.RequestResourceType = request.ResourceType;

            Uri locationEndpoint = request.RequestContext.LocationEndpointToRoute;

            lock (this.lockObject)
            {
                if (responseTime > this.requestEndTime)
                {
                    this.requestEndTime = responseTime;
                }

                if (locationEndpoint != null)
                {
                    this.RegionsContacted.Add(locationEndpoint);
                }

                if (responseStatistics.RequestOperationType == OperationType.Head || responseStatistics.RequestOperationType == OperationType.HeadFeed)
                {
                    this.supplementalResponseStatisticsList.Add(responseStatistics);
                }
                else
                {
                    this.responseStatisticsList.Add(responseStatistics);
                }
            }
        }

        public string RecordAddressResolutionStart(Uri targetEndpoint)
        {
            string identifier = Guid.NewGuid().ToString();
            AddressResolutionStatistics resolutionStats = new AddressResolutionStatistics
            {
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.MaxValue,
                TargetEndpoint = targetEndpoint == null ? "<NULL>" : targetEndpoint.ToString()
            };

            lock (this.lockObject)
            {
                this.addressResolutionStatistics.Add(identifier, resolutionStats);
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
                if (!this.addressResolutionStatistics.ContainsKey(identifier))
                {
                    throw new ArgumentException("Identifier {0} does not exist. Please call start before calling end.", identifier);
                }

                if (responseTime > this.requestEndTime)
                {
                    this.requestEndTime = responseTime;
                }

                this.addressResolutionStatistics[identifier].EndTime = responseTime;
            }
        }

        internal struct StoreResponseStatistics
        {
            public DateTime RequestResponseTime;
            public StoreResult StoreResult;
            public ResourceType RequestResourceType;
            public OperationType RequestOperationType;

            public override string ToString()
            {
                return String.Format(CultureInfo.InvariantCulture, "ResponseTime: {0}, StoreResult: {1}, ResourceType: {2}, OperationType: {3}",
                    this.RequestResponseTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    this.StoreResult != null ? this.StoreResult.ToString() : string.Empty,
                    this.RequestResourceType, this.RequestOperationType);
            }
        }

        internal class AddressResolutionStatistics
        {
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string TargetEndpoint { get; set; }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "AddressResolution - StartTime: {0}, EndTime: {1}, TargetEndpoint: {2}",
                    this.StartTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    this.EndTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    this.TargetEndpoint);
            }
        }
    }
}
