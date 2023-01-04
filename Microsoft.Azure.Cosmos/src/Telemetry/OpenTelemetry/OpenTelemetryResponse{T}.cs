// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Diagnostics;
    using System.Net;
    using Telemetry;

    internal sealed class OpenTelemetryResponse<T> : OpenTelemetryAttributes
    {
        internal OpenTelemetryResponse(FeedResponse<T> responseMessage)
        : this(
               statusCode: responseMessage.StatusCode,
               requestCharge: responseMessage.Headers?.RequestCharge,
               responseContentLength: responseMessage?.Headers?.ContentLength,
               diagnostics: responseMessage.Diagnostics,
               itemCount: responseMessage.Headers?.ItemCount,
               requestMessage: responseMessage.RequestMessage,
               subStatusCode: (int)responseMessage.Headers?.SubStatusCode,
               activityId: responseMessage.Headers?.ActivityId,
               correlatedActivityId: responseMessage.Headers?.CorrelatedActivityId,
               operationType: responseMessage is QueryResponse<T> ? Documents.OperationType.Query.ToString() : null)
        {
        }

        internal OpenTelemetryResponse(Response<T> responseMessage)
           : this(
                  statusCode: responseMessage.StatusCode,
                  requestCharge: responseMessage.Headers?.RequestCharge,
                  responseContentLength: responseMessage?.Headers?.ContentLength,
                  diagnostics: responseMessage.Diagnostics,
                  itemCount: responseMessage.Headers?.ItemCount,
                  requestMessage: responseMessage.RequestMessage,
                  subStatusCode: (int)responseMessage.Headers?.SubStatusCode,
                  activityId: responseMessage.Headers?.ActivityId,
                  correlatedActivityId: responseMessage.Headers?.CorrelatedActivityId,
                  operationType: responseMessage is QueryResponse ? Documents.OperationType.Query.ToString() : null)
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
           string correlatedActivityId,
           string operationType)
           : base(requestMessage)
        {
            this.StatusCode = statusCode;
            this.RequestCharge = requestCharge;
            this.ResponseContentLength = responseContentLength;
            this.Diagnostics = diagnostics;
            this.ItemCount = itemCount;
            this.SubStatusCode = subStatusCode;
            this.ActivityId = activityId;
            this.CorrelatedActivityId = correlatedActivityId;
            this.OperationType = operationType;
        }
    }
}
