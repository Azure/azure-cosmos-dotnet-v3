// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using Telemetry;

    internal sealed class OpenTelemetryResponse<T> : OpenTelemetryAttributes
    {
        internal OpenTelemetryResponse(Response<T> responseMessage)
                : this(
                  statusCode: responseMessage.StatusCode,
                  requestCharge: responseMessage.Headers?.RequestCharge,
                  responseContentLength: responseMessage?.Headers?.ContentLength,
                  diagnostics: responseMessage.Diagnostics,
                  itemCount: responseMessage.Headers?.ItemCount,
                  databaseName: null,
                  containerName: null,
                  requestMessage: responseMessage.RequestMessage)
        {
        }

        internal OpenTelemetryResponse(Response<DatabaseProperties> responseMessage)
            : this(
                  statusCode: responseMessage.StatusCode,
                  requestCharge: responseMessage.Headers?.RequestCharge,
                  responseContentLength: responseMessage?.Headers?.ContentLength,
                  diagnostics: responseMessage.Diagnostics,
                  itemCount: responseMessage.Headers?.ItemCount,
                  databaseName: responseMessage.Resource?.Id,
                  containerName: null,
                  requestMessage: responseMessage.RequestMessage)
        {
        }

        internal OpenTelemetryResponse(Response<ContainerProperties> responseMessage, string databaseName)
            : this(
                  statusCode: responseMessage.StatusCode,
                  requestCharge: responseMessage.Headers?.RequestCharge,
                  responseContentLength: responseMessage?.Headers?.ContentLength,
                  diagnostics: responseMessage.Diagnostics,
                  itemCount: responseMessage.Headers?.ItemCount,
                  databaseName: databaseName,
                  containerName: responseMessage.Resource?.Id,
                  requestMessage: responseMessage.RequestMessage)
        {
        }

        internal OpenTelemetryResponse(FeedResponse<T> responseMessage, string containerName, string databaseName)
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
