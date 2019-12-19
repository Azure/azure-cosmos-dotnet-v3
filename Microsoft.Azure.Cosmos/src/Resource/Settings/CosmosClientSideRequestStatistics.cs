//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal sealed class CosmosClientSideRequestStatistics : IClientSideRequestStatistics
    {
        internal const int MaxSupplementalRequestsForToString = 10;

        private object lockObject = new object();

        public CosmosClientSideRequestStatistics()
        {
            this.RequestStartTimeUtc = DateTime.UtcNow;
            this.RequestEndTimeUtc = null;
            this.ResponseStatisticsList = new List<StoreResponseStatistics>();
            this.SupplementalResponseStatisticsList = new List<StoreResponseStatistics>();
            this.EndpointToAddressResolutionStatistics = new Dictionary<string, AddressResolutionStatistics>();
            this.ContactedReplicas = new List<Uri>();
            this.FailedReplicas = new HashSet<Uri>();
            this.RegionsContacted = new HashSet<Uri>();
        }

        [JsonProperty(PropertyName = "RequestStartTimeUtc")]
        internal DateTime RequestStartTimeUtc { get; }

        [JsonProperty(PropertyName = "RequestEndTimeUtc")]
        internal DateTime? RequestEndTimeUtc { get; private set; }

        [JsonProperty(PropertyName = "ResponseStatisticsList")]
        public List<StoreResponseStatistics> ResponseStatisticsList { get; private set; }

        [JsonIgnore]
        public List<StoreResponseStatistics> SupplementalResponseStatisticsList { get; internal set; }

        /// <summary>
        /// Only take last 10 responses from SupplementalResponseStatisticsList list
        /// This has potential of having large number of entries. 
        /// Since this is for establishing consistency, we can make do with the last responses to paint a meaningful picture.
        /// </summary>
        [JsonProperty(PropertyName = "SupplementalResponseStatisticsListLast10")]
        public IEnumerable<StoreResponseStatistics> SupplementalResponseStatisticsListLast10
        {
            get
            {
                if (this.SupplementalResponseStatisticsList == null)
                {
                    return default;
                }

                return this.SupplementalResponseStatisticsList.Skip(Math.Max(0, this.SupplementalResponseStatisticsList.Count - 10));
            }
        }

        [JsonProperty(PropertyName = "EndpointToAddressResolutionStatistics")]
        public Dictionary<string, AddressResolutionStatistics> EndpointToAddressResolutionStatistics { get; private set; }

        [JsonProperty(PropertyName = "ContactedReplicas")]
        public List<Uri> ContactedReplicas { get; set; }

        [JsonProperty(PropertyName = "FailedReplicas")]
        public HashSet<Uri> FailedReplicas { get; private set; }

        [JsonProperty(PropertyName = "RegionsContacted")]
        public HashSet<Uri> RegionsContacted { get; private set; }

        [JsonProperty(PropertyName = "RequestLatency")]
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

        [JsonProperty(PropertyName = "IsCpuOverloaded")]
        public bool IsCpuOverloaded
        {
            get
            {
                foreach (StoreResponseStatistics responseStatistics in this.ResponseStatisticsList)
                {
                    if (responseStatistics.StoreResult.IsClientCpuOverloaded)
                    {
                        return true;
                    }
                }

                if (this.SupplementalResponseStatisticsList != null)
                {
                    foreach (StoreResponseStatistics responseStatistics in this.SupplementalResponseStatisticsList)
                    {
                        if (responseStatistics.StoreResult != null
                            && responseStatistics.StoreResult.IsClientCpuOverloaded)
                        {
                            return true;
                        }
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
                if (!this.RequestEndTimeUtc.HasValue || responseTime > this.RequestEndTimeUtc)
                {
                    this.RequestEndTimeUtc = responseTime;
                }

                if (locationEndpoint != null)
                {
                    this.RegionsContacted.Add(locationEndpoint);
                }

                if (responseStatistics.RequestOperationType == OperationType.Head || responseStatistics.RequestOperationType == OperationType.HeadFeed)
                {
                    this.SupplementalResponseStatisticsList.Add(responseStatistics);
                }
                else
                {
                    this.ResponseStatisticsList.Add(responseStatistics);
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

                this.EndpointToAddressResolutionStatistics[identifier].EndTime = responseTime;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            this.AppendToBuilder(sb);
            return sb.ToString();
        }

        public void AppendToBuilder(StringBuilder stringBuilder)
        {
            if (stringBuilder == null)
            {
                throw new ArgumentNullException(nameof(stringBuilder));
            }

            //need to lock in case of concurrent operations. this should be extremely rare since ToString()
            //should only be called at the end of request.
            lock (this.lockObject)
            {
                stringBuilder.AppendLine();

                //first trace request start time, as well as total non-head/headfeed requests made.
                string endTime = this.RequestEndTimeUtc.HasValue ? this.RequestEndTimeUtc.Value.ToString("o", CultureInfo.InvariantCulture) : "Not set";
                stringBuilder.AppendFormat(
                   CultureInfo.InvariantCulture,
                   "RequestStartTime: {0}, RequestEndTime: {1},  Number of regions attempted:{2}",
                   this.RequestStartTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                   endTime,
                   this.RegionsContacted.Count == 0 ? 1 : this.RegionsContacted.Count);
                stringBuilder.AppendLine();

                //take all responses here - this should be limited in number and each one contains relevant information.
                foreach (StoreResponseStatistics item in this.ResponseStatisticsList)
                {
                    item.AppendToBuilder(stringBuilder);
                    stringBuilder.AppendLine();
                }

                //take all responses here - this should be limited in number and each one is important.
                foreach (AddressResolutionStatistics item in this.EndpointToAddressResolutionStatistics.Values)
                {
                    item.AppendToBuilder(stringBuilder);
                    stringBuilder.AppendLine();
                }

                //only take last 10 responses from this list - this has potential of having large number of entries. 
                //since this is for establishing consistency, we can make do with the last responses to paint a meaningful picture.
                int supplementalResponseStatisticsListCount = this.SupplementalResponseStatisticsList.Count;
                int initialIndex = Math.Max(supplementalResponseStatisticsListCount - CosmosClientSideRequestStatistics.MaxSupplementalRequestsForToString, 0);

                if (initialIndex != 0)
                {
                    stringBuilder.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "  -- Displaying only the last {0} head/headfeed requests. Total head/headfeed requests: {1}",
                        CosmosClientSideRequestStatistics.MaxSupplementalRequestsForToString,
                        supplementalResponseStatisticsListCount);
                    stringBuilder.AppendLine();
                }

                for (int i = initialIndex; i < supplementalResponseStatisticsListCount; i++)
                {
                    this.SupplementalResponseStatisticsList[i].AppendToBuilder(stringBuilder);
                    stringBuilder.AppendLine();
                }
            }
        }

        internal struct StoreResponseStatistics
        {
            public DateTime RequestResponseTime;
            public ResourceType RequestResourceType;
            public OperationType RequestOperationType;

            [JsonIgnore]
            public StoreResult StoreResult;
            public string StoreResultString
            {
                get
                {
                    if (this.StoreResult == null)
                    {
                        return null;
                    }

                    StringBuilder stringBuilder = new StringBuilder();
                    this.StoreResult.AppendToBuilder(stringBuilder);
                    return stringBuilder.ToString();
                }
            }

            public override string ToString()
            {
                StringBuilder stringBuilder = new StringBuilder();
                this.AppendToBuilder(stringBuilder);
                return stringBuilder.ToString();
            }

            public void AppendToBuilder(StringBuilder stringBuilder)
            {
                if (stringBuilder == null)
                {
                    throw new ArgumentNullException(nameof(stringBuilder));
                }

                stringBuilder.Append($"ResponseTime: {this.RequestResponseTime.ToString("o", CultureInfo.InvariantCulture)}, ");

                stringBuilder.Append("StoreResult: ");
                if (this.StoreResult != null)
                {
                    this.StoreResult.AppendToBuilder(stringBuilder);
                }

                stringBuilder.AppendFormat(
                    CultureInfo.InvariantCulture,
                    ", ResourceType: {0}, OperationType: {1}",
                    this.RequestResourceType,
                    this.RequestOperationType);
            }
        }

        internal class AddressResolutionStatistics
        {
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string TargetEndpoint { get; set; }

            public override string ToString()
            {
                StringBuilder stringBuilder = new StringBuilder();
                this.AppendToBuilder(stringBuilder);
                return stringBuilder.ToString();
            }

            public void AppendToBuilder(StringBuilder stringBuilder)
            {
                if (stringBuilder == null)
                {
                    throw new ArgumentNullException(nameof(stringBuilder));
                }

                stringBuilder
                    .Append($"AddressResolution - StartTime: {this.StartTime.ToString("o", CultureInfo.InvariantCulture)}, ")
                    .Append($"EndTime: {this.EndTime.ToString("o", CultureInfo.InvariantCulture)}, ")
                    .Append("TargetEndpoint: ")
                    .Append(this.TargetEndpoint);
            }
        }
    }
}
