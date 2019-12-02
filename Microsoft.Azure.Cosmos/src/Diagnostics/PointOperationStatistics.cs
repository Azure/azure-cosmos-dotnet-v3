//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.CosmosClientSideRequestStatistics;

    internal class PointOperationStatistics : CosmosDiagnostics, ICosmosDiagnosticsJsonWriter
    {
        private static JsonSerializerSettings SerializerSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        public string ActivityId { get; }
        public HttpStatusCode StatusCode { get; }
        public Documents.SubStatusCodes SubStatusCode { get; }
        public double RequestCharge { get; }
        public string ErrorMessage { get; }
        public HttpMethod Method { get; }
        public Uri RequestUri { get; }
        public string RequestSessionToken { get; }
        public string ResponseSessionToken { get; }

        public DateTime requestStartTimeUtc { get; private set; }

        public DateTime? requestEndTimeUtc { get; private set; }

        public List<StoreResponseStatistics> responseStatisticsList { get; private set; }

        public List<StoreResponseStatistics> supplementalResponseStatisticsList { get; private set; }

        public Dictionary<string, AddressResolutionStatistics> addressResolutionStatistics { get; private set; }

        public List<Uri> contactedReplicas { get; set; }

        public HashSet<Uri> failedReplicas { get; private set; }

        public HashSet<Uri> regionsContacted { get; private set; }

        public TimeSpan requestLatency { get; private set; }

        internal PointOperationStatistics(
            string activityId,
            HttpStatusCode statusCode,
            Documents.SubStatusCodes subStatusCode,
            double requestCharge,
            string errorMessage,
            HttpMethod method,
            Uri requestUri,
            string requestSessionToken,
            string responseSessionToken,
            CosmosClientSideRequestStatistics clientSideRequestStatistics)
        {
            this.ActivityId = activityId;
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
            this.RequestCharge = requestCharge;
            this.ErrorMessage = errorMessage;
            this.Method = method;
            this.RequestUri = requestUri;
            this.RequestSessionToken = requestSessionToken;
            this.ResponseSessionToken = responseSessionToken;
            if (clientSideRequestStatistics != null)
            {
                this.requestStartTimeUtc = clientSideRequestStatistics.requestStartTime;
                this.requestEndTimeUtc = clientSideRequestStatistics.requestEndTime;
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

        public void AppendJson(StringBuilder stringBuilder)
        {
            stringBuilder.Append(this.ToString());
        }
    }
}
