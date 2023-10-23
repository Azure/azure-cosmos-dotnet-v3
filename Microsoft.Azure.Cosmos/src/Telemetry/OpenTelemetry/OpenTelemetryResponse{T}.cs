﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Telemetry;

    internal sealed class OpenTelemetryResponse<T> : OpenTelemetryAttributes
    {
        internal OpenTelemetryResponse(FeedResponse<T> responseMessage)
        : this(
               statusCode: responseMessage.StatusCode,
               requestCharge: OpenTelemetryResponse<T>.GetHeader(responseMessage)?.RequestCharge,
               responseContentLength: OpenTelemetryResponse<T>.GetHeader(responseMessage)?.ContentLength,
               diagnostics: responseMessage.Diagnostics,
               itemCount: OpenTelemetryResponse<T>.GetHeader(responseMessage)?.ItemCount,
               requestMessage: responseMessage.RequestMessage,
               subStatusCode: OpenTelemetryResponse<T>.GetHeader(responseMessage)?.SubStatusCode,
               activityId: OpenTelemetryResponse<T>.GetHeader(responseMessage)?.ActivityId,
               correlatedActivityId: OpenTelemetryResponse<T>.GetHeader(responseMessage)?.CorrelatedActivityId,
               operationType: responseMessage is QueryResponse<T> ? Documents.OperationType.Query : Documents.OperationType.Invalid)
        {
        }

        internal OpenTelemetryResponse(Response<T> responseMessage)
           : this(
                  statusCode: responseMessage.StatusCode,
                  requestCharge: OpenTelemetryResponse<T>.GetHeader(responseMessage)?.RequestCharge,
                  responseContentLength: OpenTelemetryResponse<T>.GetHeader(responseMessage)?.ContentLength,
                  diagnostics: responseMessage.Diagnostics,
                  itemCount: OpenTelemetryResponse<T>.GetHeader(responseMessage)?.ItemCount,
                  requestMessage: responseMessage.RequestMessage,
                  subStatusCode: OpenTelemetryResponse<T>.GetHeader(responseMessage)?.SubStatusCode,
                  activityId: OpenTelemetryResponse<T>.GetHeader(responseMessage)?.ActivityId,
                  correlatedActivityId: OpenTelemetryResponse<T>.GetHeader(responseMessage)?.CorrelatedActivityId,
                  operationType: responseMessage is QueryResponse ? Documents.OperationType.Query : Documents.OperationType.Invalid)
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
           string correlatedActivityId,
           Documents.OperationType operationType)
           : base(requestMessage)
        {
            this.StatusCode = statusCode;
            this.RequestCharge = requestCharge;
            this.ResponseContentLength = responseContentLength;
            this.Diagnostics = diagnostics;
            this.ItemCount = itemCount;
            this.SubStatusCode = (int)(subStatusCode ?? Documents.SubStatusCodes.Unknown);
            this.ActivityId = activityId;
            this.CorrelatedActivityId = correlatedActivityId;
            this.OperationType = operationType;
        }

        private static Headers GetHeader(FeedResponse<T> responseMessage)
        {
            try
            {
                return responseMessage?.Headers;
            }
            catch (NotImplementedException ex)
            {
                DefaultTrace.TraceWarning("Failed to get headers from FeedResponse<T>. Exception: {0}", ex);
                return null;
            }
        }

        private static Headers GetHeader(Response<T> responseMessage)
        {
            try
            {
                return responseMessage?.Headers;
            }
            catch (NotImplementedException ex)
            {
                DefaultTrace.TraceWarning("Failed to get headers from Response<T>. Exception: {0}", ex);
                return null;
            }
        }
    }
}
