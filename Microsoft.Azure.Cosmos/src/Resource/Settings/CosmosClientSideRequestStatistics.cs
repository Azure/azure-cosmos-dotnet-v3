//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal sealed class CosmosClientSideRequestStatistics : IClientSideRequestStatistics
    {
        internal const int MaxSupplementalRequestsForToString = 10;

        private readonly object lockObject = new object();

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

        internal DateTime RequestStartTimeUtc { get; }

        internal DateTime? RequestEndTimeUtc { get; private set; }

        public List<StoreResponseStatistics> ResponseStatisticsList { get; }

        public List<StoreResponseStatistics> SupplementalResponseStatisticsList { get; }

        /// <summary>
        /// Only take last 10 responses from SupplementalResponseStatisticsList list
        /// This has potential of having large number of entries. 
        /// Since this is for establishing consistency, we can make do with the last responses to paint a meaningful picture.
        /// </summary>
        private IEnumerable<StoreResponseStatistics> SupplementalResponseStatisticsListLast10
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

        public Dictionary<string, AddressResolutionStatistics> EndpointToAddressResolutionStatistics { get; }

        public List<Uri> ContactedReplicas { get; set; }

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

        public void RecordRequest(DocumentServiceRequest request)
        {
        }

        public void RecordResponse(DocumentServiceRequest request, StoreResult storeResult)
        {
            DateTime responseTime = DateTime.UtcNow;
            StoreResponseStatistics responseStatistics = new StoreResponseStatistics(responseTime, storeResult, request.ResourceType, request.OperationType);
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
            AddressResolutionStatistics resolutionStats = new AddressResolutionStatistics(startTime: DateTime.UtcNow, endTime: DateTime.MaxValue, targetEndpoint: targetEndpoint == null ? "<NULL>" : targetEndpoint.ToString());
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
            StringBuilder stringBuilder = new StringBuilder();
            StringWriter sw = new StringWriter(stringBuilder);
            using (JsonWriter jsonWriter = new JsonTextWriter(sw))
            {
                this.WriteJsonObject(jsonWriter);
            }

            return stringBuilder.ToString();
        }

        internal void WriteJsonObject(JsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException(nameof(jsonWriter));
            }

            //need to lock in case of concurrent operations. this should be extremely rare since ToString()
            //should only be called at the end of request.
            lock (this.lockObject)
            {
                jsonWriter.WriteStartObject();

                //first trace request start time, as well as total non-head/headfeed requests made.
                string endTime = this.RequestEndTimeUtc.HasValue ? this.RequestEndTimeUtc.Value.ToString("o", CultureInfo.InvariantCulture) : "Not set";
                int regionsContacted = this.RegionsContacted.Count == 0 ? 1 : this.RegionsContacted.Count;

                jsonWriter.WritePropertyName("RequestStartTimeUtc");
                jsonWriter.WriteValue(this.RequestStartTimeUtc.ToString("o", CultureInfo.InvariantCulture));

                jsonWriter.WritePropertyName("RequestEndTimeUtc");
                jsonWriter.WriteValue(endTime);

                jsonWriter.WritePropertyName("RequestLatency");
                jsonWriter.WriteValue(this.RequestLatency);

                jsonWriter.WritePropertyName("IsCpuOverloaded");
                jsonWriter.WriteValue(this.IsCpuOverloaded);

                jsonWriter.WritePropertyName("NumberRegionsAttempted");
                jsonWriter.WriteValue(regionsContacted);

                jsonWriter.WritePropertyName("ResponseStatisticsList");
                jsonWriter.WriteStartArray();

                // take all responses here - this should be limited in number and each one contains relevant information.
                foreach (StoreResponseStatistics item in this.ResponseStatisticsList)
                {
                    item.WriteJsonObject(jsonWriter);
                }

                jsonWriter.WriteEndArray();

                jsonWriter.WritePropertyName("AddressResolutionStatistics");
                jsonWriter.WriteStartArray();

                // take all responses here - this should be limited in number and each one is important.
                foreach (AddressResolutionStatistics item in this.EndpointToAddressResolutionStatistics.Values)
                {
                    item.WriteJsonObject(jsonWriter);
                }

                jsonWriter.WriteEndArray();

                // only take last 10 responses from this list - this has potential of having large number of entries. 
                // since this is for establishing consistency, we can make do with the last responses to paint a meaningful picture.
                int supplementalResponseStatisticsListCount = this.SupplementalResponseStatisticsList?.Count ?? 0;
                int initialIndex = Math.Max(supplementalResponseStatisticsListCount - CosmosClientSideRequestStatistics.MaxSupplementalRequestsForToString, 0);

                if (initialIndex != 0)
                {
                    jsonWriter.WritePropertyName("SupplementalResponseStatisticsCount");
                    jsonWriter.WriteValue($"  -- Displaying only the last {CosmosClientSideRequestStatistics.MaxSupplementalRequestsForToString} head/headfeed requests. Total head/headfeed requests: {supplementalResponseStatisticsListCount}");
                }

                jsonWriter.WritePropertyName("SupplementalResponseStatistics");
                jsonWriter.WriteStartArray();

                for (int i = initialIndex; i < supplementalResponseStatisticsListCount; i++)
                {
                    this.SupplementalResponseStatisticsList[i].WriteJsonObject(jsonWriter);
                }

                jsonWriter.WriteEndArray();

                this.AppendJsonUriListToBuilder(
                    "FailedReplicas",
                    this.FailedReplicas,
                    jsonWriter);

                this.AppendJsonUriListToBuilder(
                    "RegionsContacted",
                    this.RegionsContacted,
                    jsonWriter);

                this.AppendJsonUriListToBuilder(
                    "ContactedReplicas",
                    this.ContactedReplicas,
                    jsonWriter);

                jsonWriter.WriteEndObject();
            }
        }

        private void AppendJsonUriListToBuilder(
            string listName,
            IEnumerable<Uri> uris,
            JsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException(nameof(jsonWriter));
            }

            jsonWriter.WritePropertyName(listName);
            jsonWriter.WriteStartArray();

            foreach (Uri uri in uris)
            {
                jsonWriter.WriteValue(uri);
            }

            jsonWriter.WriteEndArray();
        }

        public void AppendToBuilder(StringBuilder stringBuilder)
        {
            throw new NotImplementedException();
        }

        internal readonly struct StoreResponseStatistics
        {
            public readonly DateTime RequestResponseTime;
            public readonly StoreResult StoreResult;
            public readonly ResourceType RequestResourceType;
            public readonly OperationType RequestOperationType;

            public StoreResponseStatistics(DateTime requestResponseTime, StoreResult storeResult, ResourceType resourceType, OperationType operationType)
            {
                this.RequestResponseTime = requestResponseTime;
                this.StoreResult = storeResult;
                this.RequestResourceType = resourceType;
                this.RequestOperationType = operationType;
            }

            public override string ToString()
            {
                StringBuilder stringBuilder = new StringBuilder();
                this.AppendToBuilder(stringBuilder);
                return stringBuilder.ToString();
            }

            public void AppendJsonToBuilder(JsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException(nameof(jsonWriter));
                }

                jsonWriter.WriteStartObject();

                jsonWriter.WritePropertyName("ResponseTime");
                jsonWriter.WriteValue(this.RequestResponseTime.ToString("o", CultureInfo.InvariantCulture));

                jsonWriter.WritePropertyName("ResourceType");
                jsonWriter.WriteValue(this.RequestResourceType);

                jsonWriter.WritePropertyName("OperationType");
                jsonWriter.WriteValue(this.RequestOperationType);

                if (this.StoreResult != null)
                {
                    jsonWriter.WritePropertyName("StoreResult");
                    jsonWriter.WriteValue(this.StoreResult.ToString());
                }

                jsonWriter.WriteEndObject();
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

            public void WriteJsonObject(JsonWriter jsonWriter)
            {
                jsonWriter.WriteStartObject();

                jsonWriter.WritePropertyName("ResponseTime");
                jsonWriter.WriteValue(this.RequestResponseTime);

                jsonWriter.WritePropertyName("ResourceType");
                jsonWriter.WriteValue(this.RequestResourceType);

                jsonWriter.WritePropertyName("OperationType");
                jsonWriter.WriteValue(this.RequestOperationType);

                if (this.StoreResult != null)
                {
                    jsonWriter.WritePropertyName("StoreResult");
                    jsonWriter.WriteValue(this.StoreResult.ToString());
                }

                jsonWriter.WriteEndObject();
            }
        }

        internal sealed class AddressResolutionStatistics
        {
            public AddressResolutionStatistics(DateTime startTime, DateTime endTime, string targetEndpoint)
            {
                this.StartTime = startTime;
                this.EndTime = endTime;
                this.TargetEndpoint = targetEndpoint;
            }

            public DateTime StartTime { get; }
            public DateTime EndTime { get; set; }
            public string TargetEndpoint { get; }

            public void WriteJsonObject(JsonWriter jsonWriter)
            {
                jsonWriter.WriteStartObject();

                jsonWriter.WritePropertyName("StartTime");
                jsonWriter.WriteValue(this.StartTime);

                jsonWriter.WritePropertyName("EndTime");
                jsonWriter.WriteValue(this.EndTime);

                jsonWriter.WritePropertyName("TargetEndpoint");
                jsonWriter.WriteValue(this.TargetEndpoint);

                jsonWriter.WriteEndObject();
            }
        }
    }
}
