// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using System.Net;
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
                  subStatusCode: (int)OpenTelemetryResponse.GetHeader(responseMessage)?.SubStatusCode,
                  activityId: OpenTelemetryResponse.GetHeader(responseMessage)?.ActivityId,
                  correlationId: OpenTelemetryResponse.GetHeader(responseMessage)?.CorrelatedActivityId)
        {
        }

        internal OpenTelemetryResponse(ResponseMessage responseMessage)
           : this(
                  statusCode: responseMessage.StatusCode,
                  requestCharge: OpenTelemetryResponse.GetHeader(responseMessage)?.RequestCharge,
                  responseContentLength: OpenTelemetryResponse.GetPayloadSize(responseMessage),
                  diagnostics: responseMessage.Diagnostics,
                  itemCount: OpenTelemetryResponse.GetHeader(responseMessage)?.ItemCount,
                  requestMessage: responseMessage.RequestMessage,
                  subStatusCode: (int)OpenTelemetryResponse.GetHeader(responseMessage)?.SubStatusCode,
                  activityId: OpenTelemetryResponse.GetHeader(responseMessage)?.ActivityId,
                  correlationId: OpenTelemetryResponse.GetHeader(responseMessage)?.CorrelatedActivityId,
                  operationType: responseMessage is QueryResponse ? Documents.OperationType.Query : Documents.OperationType.Invalid
                 )
        {
        }

        private OpenTelemetryResponse(
            HttpStatusCode statusCode, 
            double? requestCharge,
            string responseContentLength,
            CosmosDiagnostics diagnostics,
            string itemCount,
            RequestMessage requestMessage,
            int subStatusCode,
            string activityId,
            string correlationId,
            Documents.OperationType operationType = Documents.OperationType.Invalid)
            : base(requestMessage)
        {
            this.StatusCode = statusCode;
            this.RequestCharge = requestCharge;
            this.ResponseContentLength = responseContentLength;
            this.Diagnostics = diagnostics;
            this.ItemCount = itemCount; 
            this.SubStatusCode = subStatusCode;
            this.ActivityId = activityId;
            this.CorrelatedActivityId = correlationId;
            this.OperationType = operationType;
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

        private static Headers GetHeader(ResponseMessage responseMessage)
        {
            try
            {
                return responseMessage?.Headers;
            }
            catch
            {
                return null;
            }
        }

        private static Headers GetHeader(TransactionalBatchResponse responseMessage)
        {
            try
            {
                return responseMessage?.Headers;
            }
            catch
            {
                return null;
            }
        }
    }
}
