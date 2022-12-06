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
                  requestCharge: responseMessage.Headers?.RequestCharge,
                  responseContentLength: null,
                  diagnostics: responseMessage.Diagnostics,
                  itemCount: responseMessage.Headers?.ItemCount,
                  requestMessage: null,
                  subStatusCode: (int)responseMessage.Headers?.SubStatusCode)
        {
        }

        internal OpenTelemetryResponse(ResponseMessage responseMessage)
           : this(
                  statusCode: responseMessage.StatusCode,
                  requestCharge: responseMessage.Headers?.RequestCharge,
                  responseContentLength: OpenTelemetryResponse.GetPayloadSize(responseMessage),
                  diagnostics: responseMessage.Diagnostics,
                  itemCount: responseMessage.Headers?.ItemCount,
                  requestMessage: responseMessage.RequestMessage,
                  subStatusCode: (int)responseMessage.Headers?.SubStatusCode)
        {
        }

        private OpenTelemetryResponse(
            HttpStatusCode statusCode, 
            double? requestCharge,
            string responseContentLength,
            CosmosDiagnostics diagnostics,
            string itemCount,
            RequestMessage requestMessage,
            int subStatusCode)
            : base(requestMessage)
        {
            this.StatusCode = statusCode;
            this.RequestCharge = requestCharge;
            this.ResponseContentLength = responseContentLength;
            this.Diagnostics = diagnostics;
            this.ItemCount = itemCount; 
            this.SubStatusCode = subStatusCode;
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
    }
}
