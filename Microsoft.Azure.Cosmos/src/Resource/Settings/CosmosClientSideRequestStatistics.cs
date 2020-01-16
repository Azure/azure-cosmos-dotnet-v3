//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal sealed class CosmosClientSideRequestStatistics : CosmosDiagnosticWriter, IClientSideRequestStatistics 
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
            this.WriteJsonObject(sb);
            return sb.ToString();
        }

        internal override void WriteJsonObject(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"RequestStartTimeUtc\":\"");
            stringBuilder.Append(this.RequestStartTimeUtc);
            stringBuilder.Append("\",\"RequestEndTimeUtc\":\"");
            stringBuilder.Append(this.RequestEndTimeUtc);
            stringBuilder.Append("\"");

            if (this.ResponseStatisticsList.Any())
            {
                stringBuilder.Append(",\"ResponseStatisticsList\":[");
                foreach (StoreResponseStatistics stat in this.ResponseStatisticsList)
                {
                    stat.WriteJsonObject(stringBuilder);
                    stringBuilder.Append(",");
                }

                if (this.ResponseStatisticsList.Count > 0)
                {
                    stringBuilder.Length -= 1;
                }

                stringBuilder.Append("]");
            }

            if (this.SupplementalResponseStatisticsList.Any())
            {
                stringBuilder.Append(",\"SupplementalResponseStatisticsListLast10\":[");
                foreach (StoreResponseStatistics stat in this.SupplementalResponseStatisticsListLast10)
                {
                    stat.WriteJsonObject(stringBuilder);
                    stringBuilder.Append(",");
                }

                if (stringBuilder[stringBuilder.Length - 1] == ',')
                {
                    stringBuilder.Length--;
                }
                stringBuilder.Append("]");
            }

            if (this.EndpointToAddressResolutionStatistics.Any())
            {
                stringBuilder.Append(",\"EndpointToAddressResolutionStatistics\":[");
                foreach (KeyValuePair<string, AddressResolutionStatistics> keyValuePair in this.EndpointToAddressResolutionStatistics)
                {
                    stringBuilder.Append("{\"");
                    stringBuilder.Append(keyValuePair.Key);
                    stringBuilder.Append("\":\"");
                    stringBuilder.Append(keyValuePair.Value);
                    stringBuilder.Append("\"},");
                }

                if (stringBuilder[stringBuilder.Length - 1] == ',')
                {
                    stringBuilder.Length--;
                }
                stringBuilder.Append("]");
            }

            if (this.ContactedReplicas.Any())
            {
                stringBuilder.Append(",\"ContactedReplicas\":[");
                foreach (Uri replica in this.ContactedReplicas)
                {
                    stringBuilder.Append("\"");
                    stringBuilder.Append(replica.OriginalString);
                    stringBuilder.Append("\",");
                }

                if (stringBuilder[stringBuilder.Length - 1] == ',')
                {
                    stringBuilder.Length--;
                }
                stringBuilder.Append("]");
            }

            if (this.FailedReplicas.Any())
            {
                stringBuilder.Append(",\"FailedReplicas\":[");
                foreach (Uri replica in this.FailedReplicas)
                {
                    stringBuilder.Append("\"");
                    stringBuilder.Append(replica.OriginalString);
                    stringBuilder.Append("\",");
                }

                if (stringBuilder[stringBuilder.Length - 1] == ',')
                {
                    stringBuilder.Length--;
                }

                stringBuilder.Append("]");
            }

            if (this.RegionsContacted.Any())
            {
                stringBuilder.Append(",\"RegionsContacted\":[");
                foreach (Uri region in this.RegionsContacted)
                {
                    stringBuilder.Append("\"");
                    stringBuilder.Append(region.OriginalString);
                    stringBuilder.Append("\",");
                }

                if (stringBuilder[stringBuilder.Length - 1] == ',')
                {
                    stringBuilder.Length--;
                }

                stringBuilder.Append("]");
            }

            stringBuilder.Append(",\"RequestLatency\":\"");
            stringBuilder.Append(this.RequestLatency);
            stringBuilder.Append("\",\"IsCpuOverloaded\":\"");
            stringBuilder.Append(this.IsCpuOverloaded);
            stringBuilder.Append("\"}");
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
            public StoreResult StoreResult;

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

            public void WriteJsonObject(StringBuilder stringBuilder)
            {
                stringBuilder.Append("{\"RequestResponseTime\":\"");
                stringBuilder.Append(this.RequestResponseTime);
                stringBuilder.Append("\",\"RequestResourceType\":\"");
                stringBuilder.Append(this.RequestResourceType);
                stringBuilder.Append("\",\"RequestOperationType\":\"");
                stringBuilder.Append(this.RequestOperationType);
                if (this.StoreResult != null)
                {
                    stringBuilder.Append("\",\"StoreResult\":\"");
                    this.StoreResult.AppendToBuilder(stringBuilder);
                }
                
                stringBuilder.Append("\"}");
            }
        }

        internal class AddressResolutionStatistics : CosmosDiagnosticWriter
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

            internal override void WriteJsonObject(StringBuilder stringBuilder)
            {
                stringBuilder.Append("{\"StartTime\":\"");
                stringBuilder.Append(this.StartTime);
                stringBuilder.Append("\",\"EndTime\":\"");
                stringBuilder.Append(this.EndTime);
                stringBuilder.Append("\",\"TargetEndpoint\":\"");
                stringBuilder.Append(this.TargetEndpoint);
                stringBuilder.Append("\"}");
            }
        }
    }
}
