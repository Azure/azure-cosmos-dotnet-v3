//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.CosmosClientSideRequestStatistics;

    internal class PointOperationStatistics : CosmosDiagnostics
    {
        private static JsonSerializerSettings SerializerSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };
        public HttpStatusCode StatusCode { get; }
        public Documents.SubStatusCodes SubStatusCode { get; }
        public double RequestCharge { get; }
        public string ErrorMessage { get; }
        public HttpMethod Method { get; }
        public Uri RequestUri { get; }

        public DateTime requestStartTime { get; private set; }

        public DateTime requestEndTime { get; private set; }

        public List<StoreResponseStatistics> responseStatisticsList { get; private set; }

        public List<StoreResponseStatistics> supplementalResponseStatisticsList { get; private set; }

        public Dictionary<string, AddressResolutionStatistics> addressResolutionStatistics { get; private set; }

        public List<Uri> contactedReplicas { get; set; }

        public HashSet<Uri> failedReplicas { get; private set; }

        public HashSet<Uri> regionsContacted { get; private set; }

        public TimeSpan requestLatency { get; private set; }

        internal PointOperationStatistics(
            HttpStatusCode statusCode,
            Documents.SubStatusCodes subStatusCode,
            double requestCharge,
            string errorMessage,
            HttpMethod method,
            Uri requestUri,
            CosmosClientSideRequestStatistics clientSideRequestStatistics)
        {
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
            this.RequestCharge = requestCharge;
            this.ErrorMessage = errorMessage;
            this.Method = method;
            this.RequestUri = requestUri;
            if (clientSideRequestStatistics != null)
            {
                this.requestStartTime = clientSideRequestStatistics.requestStartTime;
                this.requestEndTime = clientSideRequestStatistics.requestEndTime;
                this.responseStatisticsList = clientSideRequestStatistics.responseStatisticsList;
                this.supplementalResponseStatisticsList = clientSideRequestStatistics.supplementalResponseStatisticsList;
                this.addressResolutionStatistics = clientSideRequestStatistics.addressResolutionStatistics;
                this.contactedReplicas = clientSideRequestStatistics.ContactedReplicas;
                this.failedReplicas = clientSideRequestStatistics.FailedReplicas;
                this.regionsContacted = clientSideRequestStatistics.RegionsContacted;
                this.requestLatency = clientSideRequestStatistics.RequestLatency;
            }
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
            return JsonConvert.SerializeObject(this, PointOperationStatistics.SerializerSettings);
        }
    }
}
