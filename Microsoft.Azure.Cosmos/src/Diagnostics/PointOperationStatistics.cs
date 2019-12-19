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
    using System.Web;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.CosmosClientSideRequestStatistics;

    internal class PointOperationStatistics : CosmosDiagnostics
    {
        public string ActivityId { get; }
        public HttpStatusCode StatusCode { get; }
        public Documents.SubStatusCodes SubStatusCode { get; }
        public double RequestCharge { get; }
        public string ErrorMessage { get; }
        public HttpMethod Method { get; }
        public Uri RequestUri { get; }
        public string RequestSessionToken { get; }
        public string ResponseSessionToken { get; }
        public CosmosClientSideRequestStatistics ClientSideRequestStatistics { get; }

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
            this.ClientSideRequestStatistics = clientSideRequestStatistics;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, CosmosDiagnosticsContext.JsonSerializerSettings);
        }
    }
}
