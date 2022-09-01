﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using Telemetry;

    internal sealed class OpenTelemetryResponse : OpenTelemetryAttributes
    {
        internal OpenTelemetryResponse(ResponseMessage responseMessage) 
            : base(responseMessage.RequestMessage)
        {
            this.StatusCode = responseMessage.StatusCode;
            this.RequestCharge = responseMessage.Headers?.RequestCharge;
            this.ResponseContentLength = OpenTelemetryResponse.GetPayloadSize(responseMessage);
            this.Diagnostics = responseMessage.Diagnostics;
            this.ItemCount = responseMessage.Headers?.ItemCount ?? OpenTelemetryAttributes.NotAvailable;
        }

        /// <summary>
        /// No request message in TransactionalBatchResponse
        /// </summary>
        /// <param name="responseMessage"></param>
        internal OpenTelemetryResponse(TransactionalBatchResponse responseMessage)
           : base(null)
        {
            // TODO: Add Request Information in TransactionalBatchResponse
            this.StatusCode = responseMessage.StatusCode;
            this.RequestCharge = responseMessage.Headers?.RequestCharge;
            this.Diagnostics = responseMessage.Diagnostics;
            this.ItemCount = responseMessage.Headers?.ItemCount ?? OpenTelemetryAttributes.NotAvailable;
        }

        private static string GetPayloadSize(ResponseMessage response)
        {
            if (response?.Content != null
                    && response.Content.CanSeek
                    && response.Content is MemoryStream)
            {
                return response.Content.Length.ToString();
            }
            return response?.Headers?.ContentLength ?? OpenTelemetryAttributes.NotAvailable;
        }
    }
}
