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
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.CosmosClientSideRequestStatistics;

    internal class PointOperationStatistics : CosmosDiagnosticWriter
    {
        public string ActivityId { get; }
        public HttpStatusCode StatusCode { get; }
        public SubStatusCodes SubStatusCode { get; }
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
            SubStatusCodes subStatusCode,
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
            StringBuilder stringBuilder = new StringBuilder();
            this.WriteJsonObject(stringBuilder);
            return stringBuilder.ToString();
        }

        internal override void WriteJsonObject(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"Id\":\"PointOperationStatistics\",\"ActivityId\":\"");
            stringBuilder.Append(this.ActivityId);
            stringBuilder.Append("\",\"StatusCode\":\"");
            stringBuilder.Append((int)this.StatusCode);
            stringBuilder.Append("\",\"SubStatusCode\":\"");
            stringBuilder.Append((int)this.SubStatusCode);
            stringBuilder.Append("\",\"RequestCharge\":\"");
            stringBuilder.Append(this.RequestCharge);
            stringBuilder.Append("\",\"ErrorMessage\":");
            stringBuilder.Append(JsonConvert.SerializeObject(this.ErrorMessage));
            stringBuilder.Append(",\"RequestUri\":\"");
            stringBuilder.Append(this.RequestUri);
            stringBuilder.Append("\",\"RequestSessionToken\":\"");
            stringBuilder.Append(this.RequestSessionToken);
            stringBuilder.Append("\",\"ResponseSessionToken\":\"");
            stringBuilder.Append(this.ResponseSessionToken);
            if (this.ClientSideRequestStatistics != null)
            {
                stringBuilder.Append("\",\"ClientRequestStats\":");
                this.ClientSideRequestStatistics.WriteJsonObject(stringBuilder);
            }
            else
            {
                stringBuilder.Append("\"");
            }

            stringBuilder.Append("}");
        }
    }
}
