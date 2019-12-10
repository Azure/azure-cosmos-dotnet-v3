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
            StringBuilder stringBuilder = new StringBuilder();
            this.AppendJsonToBuilder(stringBuilder);
            return stringBuilder.ToString();
        }

        public void AppendJsonToBuilder(StringBuilder stringBuilder)
        {
            if (stringBuilder == null)
            {
                throw new ArgumentNullException(nameof(stringBuilder));
            }

            string errorMessage = string.Empty;
            if (this.ErrorMessage != null)
            {
                errorMessage = HttpUtility.JavaScriptStringEncode(this.ErrorMessage);
            }

            stringBuilder.Append($"{{\"ActivityId\":\"{this.ActivityId}\"");
            stringBuilder.Append($",\"StatusCode\":\"{(int)this.StatusCode}\"");
            stringBuilder.Append($",\"SubStatusCode\":\"{this.SubStatusCode.ToString()}\"");
            stringBuilder.Append($",\"RequestCharge\":\"{this.RequestCharge}\"");
            stringBuilder.Append($",\"ErrorMessage\":\"{errorMessage}\"");
            stringBuilder.Append($",\"Method\":\"{this.Method?.ToString() ?? "null"}\"");
            stringBuilder.Append($",\"RequestUri\":\"{this.RequestUri?.ToString() ?? "null"}\"");
            stringBuilder.Append($",\"RequestSessionToken\":\"{this.RequestSessionToken}\"");
            stringBuilder.Append($",\"ResponseSessionToken\":\"{this.ResponseSessionToken}\"");
            if (this.ClientSideRequestStatistics != null)
            {
                stringBuilder.Append(",");
                this.ClientSideRequestStatistics.AppendJsonToBuilder(stringBuilder);
            }

            stringBuilder.Append("}");
        }
    }
}
