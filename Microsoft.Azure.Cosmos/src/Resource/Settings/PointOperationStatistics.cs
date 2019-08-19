//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.CosmosClientSideRequestStatistics;
    using static Microsoft.Azure.Documents.IClientSideRequestStatistics;

    internal class PointOperationStatistics : BaseStatistics
    {
        private object lockObject = new object();

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? requestStartTime { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? requestEndTime { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TimeSpan? requestLatency { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? directFlowStartTime { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? directFlowEndTime { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TimeSpan? directFlowLatency { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TimeSpan costOfFetchingPK { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<StoreResponseStatistics> responseStatisticsList { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<StoreResponseStatistics> supplementalResponseStatisticsList { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, AddressResolutionStatistics> addressResolutionStatistics { get; private set; }

        internal List<Uri> contactedReplicas { get; private set; }

        internal HashSet<Uri> failedReplicas { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public HashSet<Uri> regionsContacted { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string operationType { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public long? requestPayloadinBytes { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public long? responsePayloadinBytes { get; private set; }

        public void RecordDirectResponse(
            DocumentServiceResponse response,
            RequestMessage requestMessage)
        {
            if (this.requestEndTime != null && this.requestStartTime != null)
            {
                this.requestLatency = this.requestEndTime.Value - this.requestStartTime.Value;
            }

            if (response.RequestStats != null)
            {
                this.directFlowStartTime = ((CosmosClientSideRequestStatistics)response.RequestStats).requestStartTime;
                this.directFlowEndTime = ((CosmosClientSideRequestStatistics)response.RequestStats).requestEndTime;
                this.responseStatisticsList = ((CosmosClientSideRequestStatistics)response.RequestStats).responseStatisticsList;
                this.supplementalResponseStatisticsList = ((CosmosClientSideRequestStatistics)response.RequestStats).supplementalResponseStatisticsList;
                this.addressResolutionStatistics = ((CosmosClientSideRequestStatistics)response.RequestStats).addressResolutionStatistics;
                this.contactedReplicas = response.RequestStats.ContactedReplicas;
                this.failedReplicas = response.RequestStats.FailedReplicas;
                this.regionsContacted = response.RequestStats.RegionsContacted;
                this.directFlowLatency = response.RequestStats.RequestLatency;
            }

            this.customHandlerLatency = requestMessage.requestDiagnosticContext.customHandlerLatency;
            this.operationType = requestMessage.OperationType.ToString();
            this.requestPayloadinBytes = requestMessage.Content?.Length;
            this.responsePayloadinBytes = response.ResponseBody?.Length;
            this.connectivityMode = ConnectionMode.Direct.ToString();
        }

        public PointOperationStatistics()
        {
            this.requestStartTime = DateTime.UtcNow;
            this.requestEndTime = DateTime.UtcNow;
        }

        public void RecordGateWayResponse(DocumentServiceResponse response, RequestMessage requestMessage)
        {
            Uri locationEndpoint = requestMessage.DocumentServiceRequest.RequestContext.LocationEndpointToRoute;
            lock (this.lockObject)
            {
                if (locationEndpoint != null)
                {
                    if (this.regionsContacted == null)
                    {
                        this.regionsContacted = new HashSet<Uri>();
                    }
                    this.regionsContacted.Add(locationEndpoint);
                }
            }

            this.customHandlerLatency = requestMessage.requestDiagnosticContext.customHandlerLatency;
            this.requestLatency = this.requestEndTime.Value - this.requestStartTime.Value;
            this.operationType = requestMessage.OperationType.ToString();
            this.requestPayloadinBytes = requestMessage.Content?.Length;
            this.responsePayloadinBytes = response.ResponseBody?.Length;
            this.connectivityMode = ConnectionMode.Gateway.ToString();
        }

        public override string ToString()
        {
            if (this.supplementalResponseStatisticsList != null)
            {
                int supplementalResponseStatisticsListCount = this.supplementalResponseStatisticsList.Count;
                int countToRemove = Math.Max(supplementalResponseStatisticsListCount - CosmosClientSideRequestStatistics.MaxSupplementalRequestsForToString, 0);
                if (countToRemove > 0)
                {
                    this.supplementalResponseStatisticsList.RemoveRange(0, countToRemove);
                }
            }
            return JsonConvert.SerializeObject(this);
        }
    }
}
