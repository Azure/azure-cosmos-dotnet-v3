// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents;
    using Telemetry;

    internal sealed class OpenTelemetryResponse : OpenTelemetryAttributes
    {
        internal OpenTelemetryResponse(TransactionalBatchResponse responseMessage)
           : this(
                  statusCode: responseMessage.StatusCode,
                  requestCharge: OpenTelemetryResponse.GetHeader(responseMessage)?.RequestCharge,
                  responseContentLength: null,
                  diagnostics: responseMessage.Diagnostics,
                  itemCount: OpenTelemetryResponse.GetHeader(responseMessage)?.ItemCount,
                  requestMessage: null,
                  subStatusCode: OpenTelemetryResponse.GetHeader(responseMessage)?.SubStatusCode,
                  activityId: OpenTelemetryResponse.GetHeader(responseMessage)?.ActivityId,
                  correlationId: OpenTelemetryResponse.GetHeader(responseMessage)?.CorrelatedActivityId,
                  batchSize: responseMessage.GetBatchSize())
        {
        }

        internal OpenTelemetryResponse(ResponseMessage responseMessage, Func<SqlQuerySpec> querySpecFunc = null)
           : this(
                  statusCode: responseMessage.StatusCode,
                  requestCharge: OpenTelemetryResponse.GetHeader(responseMessage)?.RequestCharge,
                  responseContentLength: OpenTelemetryResponse.GetPayloadSize(responseMessage),
                  diagnostics: responseMessage.Diagnostics,
                  itemCount: OpenTelemetryResponse.GetHeader(responseMessage)?.ItemCount,
                  requestMessage: responseMessage.RequestMessage,
                  subStatusCode: OpenTelemetryResponse.GetHeader(responseMessage)?.SubStatusCode,
                  activityId: OpenTelemetryResponse.GetHeader(responseMessage)?.ActivityId,
                  correlationId: OpenTelemetryResponse.GetHeader(responseMessage)?.CorrelatedActivityId,
                  querySpecFunc: querySpecFunc)
        {
        }

        private OpenTelemetryResponse(
            HttpStatusCode statusCode, 
            double? requestCharge,
            string responseContentLength,
            CosmosDiagnostics diagnostics,
            string itemCount,
            RequestMessage requestMessage,
            Documents.SubStatusCodes? subStatusCode,
            string activityId,
            string correlationId,
            int? batchSize = null,
            Func<SqlQuerySpec> querySpecFunc = null)
            : base(requestMessage)
        {
            this.StatusCode = statusCode;
            this.RequestCharge = requestCharge;
            this.ResponseContentLength = responseContentLength;
            this.Diagnostics = diagnostics;
            this.ItemCount = itemCount; 
            this.SubStatusCode = (int)(subStatusCode ?? Documents.SubStatusCodes.Unknown);
            this.ActivityId = activityId;
            this.CorrelatedActivityId = correlationId;
            this.BatchSize = batchSize;
            if (querySpecFunc != null)
            {
                this.QuerySpec = querySpecFunc();
            }
        }

        private static string GetPayloadSize(ResponseMessage response)
        {
            if (response?.Content != null
                    && response.Content.CanSeek
                    && response.Content is MemoryStream)
            {
                return response.Content.Length.ToString();
            }
            return response?.Headers?.ContentLength;
        }

        private static Headers GetHeader(TransactionalBatchResponse responseMessage)
        {
            try
            {
                return responseMessage?.Headers;
            }
            catch (NotImplementedException ex)
            {
                DefaultTrace.TraceVerbose("Failed to get headers from TransactionalBatchResponse. Exception: {0}", ex);
                return null;
            }
        }

        private static Headers GetHeader(ResponseMessage responseMessage)
        {
            try
            {
                return responseMessage?.Headers;
            }
            catch (NotImplementedException ex)
            {
                DefaultTrace.TraceVerbose("Failed to get headers from ResponseMessage. Exception: {0}", ex);
                return null;
            }
        }
    }
}
