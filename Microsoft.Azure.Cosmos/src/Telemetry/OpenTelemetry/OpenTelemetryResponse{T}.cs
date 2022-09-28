// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Telemetry;

    internal sealed class OpenTelemetryResponse<T> : OpenTelemetryAttributes
    {
        internal OpenTelemetryResponse(Response<T> responseMessage) 
            : base(responseMessage.RequestMessage)
        {
            this.StatusCode = responseMessage.StatusCode;
            this.RequestCharge = responseMessage.Headers?.RequestCharge;
            this.ResponseContentLength = responseMessage?.Headers?.ContentLength ?? OpenTelemetryAttributes.NotAvailable; 
            this.Diagnostics = responseMessage.Diagnostics;
            this.ItemCount = responseMessage.Headers?.ItemCount ?? OpenTelemetryAttributes.NotAvailable;
        }

        internal OpenTelemetryResponse(FeedResponse<T> responseMessage)
           : base(responseMessage.RequestMessage)
        {
            this.StatusCode = responseMessage.StatusCode;
            this.RequestCharge = responseMessage.Headers?.RequestCharge;
            this.ResponseContentLength = responseMessage?.Headers?.ContentLength ?? OpenTelemetryAttributes.NotAvailable;
            this.Diagnostics = responseMessage.Diagnostics;
            this.ItemCount = responseMessage.Headers?.ItemCount ?? OpenTelemetryAttributes.NotAvailable;
        }

        internal OpenTelemetryResponse(Response<DatabaseProperties> responseMessage)
        : base(responseMessage.RequestMessage)
        {
            this.StatusCode = responseMessage.StatusCode;
            this.RequestCharge = responseMessage.Headers?.RequestCharge;
            this.ResponseContentLength = responseMessage?.Headers?.ContentLength ?? OpenTelemetryAttributes.NotAvailable;
            this.Diagnostics = responseMessage.Diagnostics;
            this.ItemCount = responseMessage.Headers?.ItemCount ?? OpenTelemetryAttributes.NotAvailable;
            this.DatabaseName = responseMessage.Resource?.Id ?? OpenTelemetryAttributes.NotAvailable;
        }

        internal OpenTelemetryResponse(Response<ContainerProperties> responseMessage)
            : base(responseMessage.RequestMessage)
        {
            this.StatusCode = responseMessage.StatusCode;
            this.RequestCharge = responseMessage.Headers?.RequestCharge;
            this.ResponseContentLength = responseMessage?.Headers?.ContentLength ?? OpenTelemetryAttributes.NotAvailable;
            this.Diagnostics = responseMessage.Diagnostics;
            this.ItemCount = responseMessage.Headers?.ItemCount ?? OpenTelemetryAttributes.NotAvailable;
            this.ContainerName = responseMessage.Resource?.Id ?? OpenTelemetryAttributes.NotAvailable;
        }

        internal OpenTelemetryResponse(Response<ContainerProperties> responseMessage, string databaseName)
            : base(responseMessage.RequestMessage)
        {
            this.StatusCode = responseMessage.StatusCode;
            this.RequestCharge = responseMessage.Headers?.RequestCharge;
            this.ResponseContentLength = responseMessage?.Headers?.ContentLength ?? OpenTelemetryAttributes.NotAvailable;
            this.Diagnostics = responseMessage.Diagnostics;
            this.ItemCount = responseMessage.Headers?.ItemCount ?? OpenTelemetryAttributes.NotAvailable;
            this.DatabaseName = databaseName ?? OpenTelemetryAttributes.NotAvailable;
            this.ContainerName = responseMessage.Resource?.Id ?? OpenTelemetryAttributes.NotAvailable;
        }
    }
}
