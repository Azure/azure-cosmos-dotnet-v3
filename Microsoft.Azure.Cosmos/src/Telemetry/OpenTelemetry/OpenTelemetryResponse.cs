// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Documents;

    internal sealed class OpenTelemetryResponse : OpenTelemetryAttributes
    {
        internal OpenTelemetryResponse(ResponseMessage responseMessage) 
            : base(responseMessage.RequestMessage)
        {
            this.StatusCode = responseMessage.StatusCode;
            this.RequestCharge = responseMessage.Headers?.RequestCharge;
            this.ResponseContentLength = OpenTelemetryResponse.GetPayloadSize(responseMessage);
            this.Diagnostics = responseMessage.Diagnostics;
            this.ItemCount = responseMessage.Headers?.ItemCount;
        }

        /// <summary>
        /// No request message in TransactionalBatchresponse
        /// </summary>
        /// <param name="responseMessage"></param>
        internal OpenTelemetryResponse(TransactionalBatchResponse responseMessage)
           : base(null)
        {
            // TODO: Add Request Information in TransactionalBatchResponse
            this.StatusCode = responseMessage.StatusCode;
            this.RequestCharge = responseMessage.Headers?.RequestCharge;
            this.Diagnostics = responseMessage.Diagnostics;
            //TODO: ItemCount needs to be added
        }

        private static string GetPayloadSize(ResponseMessage response)
        {
            if (response?.Content != null
                    && response.Content.CanRead
                    && response.Content is MemoryStream)
            {
                return Convert.ToString(response.Content.Length);
            }
            return response?.Headers?.ContentLength ?? "NA";
        }
    }
}
