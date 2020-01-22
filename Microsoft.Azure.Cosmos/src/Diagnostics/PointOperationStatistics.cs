//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
            StringWriter sw = new StringWriter(stringBuilder);
            using (JsonWriter jsonWriter = new JsonTextWriter(sw))
            {
                this.WriteJsonObject(jsonWriter);
            }

            return stringBuilder.ToString();
        }

        internal override void WriteJsonObject(JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("Id");
            jsonWriter.WriteValue("PointOperationStatistics");

            jsonWriter.WritePropertyName("ActivityId");
            jsonWriter.WriteValue(this.ActivityId);

            jsonWriter.WritePropertyName("StatusCode");
            jsonWriter.WriteValue((int)this.StatusCode);

            jsonWriter.WritePropertyName("SubStatusCode");
            jsonWriter.WriteValue((int)this.SubStatusCode);

            jsonWriter.WritePropertyName("RequestCharge");
            jsonWriter.WriteValue(this.RequestCharge);

            jsonWriter.WritePropertyName("RequestUri");
            jsonWriter.WriteValue(this.RequestUri);

            if (!string.IsNullOrEmpty(this.ErrorMessage))
            {
                jsonWriter.WritePropertyName("ErrorMessage");
                jsonWriter.WriteValue(this.ErrorMessage);
            }

            jsonWriter.WritePropertyName("RequestSessionToken");
            jsonWriter.WriteValue(this.RequestSessionToken);

            jsonWriter.WritePropertyName("ResponseSessionToken");
            jsonWriter.WriteValue(this.ResponseSessionToken);

            if (this.ClientSideRequestStatistics != null)
            {
                jsonWriter.WritePropertyName("ClientRequestStats");
                this.ClientSideRequestStatistics.WriteJsonObject(jsonWriter);
            }

            jsonWriter.WriteEndObject();
        }
    }
}
