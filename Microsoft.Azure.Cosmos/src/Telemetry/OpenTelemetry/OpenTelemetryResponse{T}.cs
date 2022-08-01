// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;

    internal sealed class OpenTelemetryResponse<T> : OpenTelemetryAttributes
    {
        internal OpenTelemetryResponse(Response<T> responseMessage) 
            : base(responseMessage.RequestMessage)
        {
            this.StatusCode = responseMessage.StatusCode;
            this.RequestCharge = responseMessage.Headers?.RequestCharge;
            this.ResponseContentLength = responseMessage?.Headers?.ContentLength ?? OpenTelemetryAttributes.NotAvailable; 
            this.Diagnostics = responseMessage.Diagnostics;
            this.ItemCount = responseMessage.Headers?.ItemCount;
        }

        internal OpenTelemetryResponse(Response<DatabaseProperties> responseMessage)
            : base(responseMessage.RequestMessage)
        {
            this.StatusCode = responseMessage.StatusCode;
            this.RequestCharge = responseMessage.Headers?.RequestCharge;
            this.ResponseContentLength = responseMessage?.Headers?.ContentLength ?? OpenTelemetryAttributes.NotAvailable;
            this.Diagnostics = responseMessage.Diagnostics;
            this.ItemCount = responseMessage.Headers?.ItemCount;
            this.DatabaseName = responseMessage.Resource.Id;
        }
        
        internal OpenTelemetryResponse(Response<ContainerProperties> responseMessage)
            : base(responseMessage.RequestMessage)
        {
            this.StatusCode = responseMessage.StatusCode;
            this.RequestCharge = responseMessage.Headers?.RequestCharge;
            this.ResponseContentLength = responseMessage?.Headers?.ContentLength ?? OpenTelemetryAttributes.NotAvailable;
            this.Diagnostics = responseMessage.Diagnostics;
            this.ItemCount = responseMessage.Headers?.ItemCount;
            this.ContainerName = responseMessage.Resource.Id;
        }

        internal OpenTelemetryResponse(Response<ContainerProperties> responseMessage, string databaseName)
            : base(responseMessage.RequestMessage)
        {
            this.StatusCode = responseMessage.StatusCode;
            this.RequestCharge = responseMessage.Headers?.RequestCharge;
            this.ResponseContentLength = responseMessage?.Headers?.ContentLength ?? OpenTelemetryAttributes.NotAvailable;
            this.Diagnostics = responseMessage.Diagnostics;
            this.ItemCount = responseMessage.Headers?.ItemCount;
            this.DatabaseName = databaseName;
            this.ContainerName = responseMessage.Resource.Id;
        }

        internal OpenTelemetryResponse(FeedResponse<T> responseMessage)
           : base(responseMessage.RequestMessage)
        {
            this.StatusCode = responseMessage.StatusCode;
            this.RequestCharge = responseMessage.Headers?.RequestCharge;
            this.ResponseContentLength = responseMessage?.Headers?.ContentLength ?? OpenTelemetryAttributes.NotAvailable;
            this.Diagnostics = responseMessage.Diagnostics;
            this.ItemCount = responseMessage.Headers?.ItemCount;
        }
    }
}
