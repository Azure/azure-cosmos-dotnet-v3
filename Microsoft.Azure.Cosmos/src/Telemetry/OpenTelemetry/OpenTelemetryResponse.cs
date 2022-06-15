// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Documents;

    internal sealed class OpenTelemetryResponse : OpenTelemetryAttributes
    {
        internal OpenTelemetryResponse(ResponseMessage message)
        {
            this.StatusCode = message.StatusCode;
            this.RequestCharge = message.Headers?.RequestCharge;
            this.RequestContentLength = message.RequestMessage?.Headers?.ContentLength;
            this.ResponseContentLength = message.Headers?.ContentLength;
            this.ContainerName = message.RequestMessage?.ContainerId;
            this.Diagnostics = message.Diagnostics;
            //TODO: ItemCount needs to be added
        }
    }
}
