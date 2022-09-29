// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using Telemetry;

    internal sealed class OpenTelemetryResponse<T> : OpenTelemetryAttributes
    {
        internal OpenTelemetryResponse(FeedResponse<T> responseMessage, string containerName = null, string databaseName = null)
        : this(
               statusCode: responseMessage.StatusCode,
               requestCharge: responseMessage.Headers?.RequestCharge,
               responseContentLength: responseMessage?.Headers?.ContentLength,
               diagnostics: responseMessage.Diagnostics,
               itemCount: responseMessage.Headers?.ItemCount,
               databaseName: databaseName,
               containerName: containerName,
               requestMessage: responseMessage.RequestMessage)
        {
        }

        internal OpenTelemetryResponse(Response<T> responseMessage, string containerName = null, string databaseName = null)
           : this(
                  statusCode: responseMessage.StatusCode,
                  requestCharge: responseMessage.Headers?.RequestCharge,
                  responseContentLength: responseMessage?.Headers?.ContentLength,
                  diagnostics: responseMessage.Diagnostics,
                  itemCount: responseMessage.Headers?.ItemCount,
                  databaseName: databaseName,
                  containerName: containerName,
                  requestMessage: responseMessage.RequestMessage)
        {
        }

        private OpenTelemetryResponse(
           HttpStatusCode statusCode,
           double? requestCharge,
           string responseContentLength,
           CosmosDiagnostics diagnostics,
           string itemCount,
           string databaseName,
           string containerName,
           RequestMessage requestMessage)
           : base(requestMessage)
        {
            this.StatusCode = statusCode;
            this.RequestCharge = requestCharge;
            this.ResponseContentLength = responseContentLength ?? OpenTelemetryAttributes.NotAvailable;
            this.Diagnostics = diagnostics;
            this.ItemCount = itemCount ?? OpenTelemetryAttributes.NotAvailable;

            if (string.IsNullOrEmpty(this.DatabaseName))
            {
                this.DatabaseName = databaseName ?? OpenTelemetryAttributes.NotAvailable;
            }
            if (string.IsNullOrEmpty(this.ContainerName))
            {
                this.ContainerName = containerName ?? OpenTelemetryAttributes.NotAvailable;
            }
        }
    }
}
