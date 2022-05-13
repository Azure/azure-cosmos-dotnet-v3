// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Documents;

    internal sealed class OpenTelemetryResponse
    {
        public OpenTelemetryResponse(ResponseMessage message)
        {
            this.StatusCode = message.StatusCode;
            this.RequestCharge = message.Headers?.RequestCharge;
            this.RequestContentLength = message.RequestMessage.Headers?.ContentLength;
            this.ResponseContentLength = message.Headers?.ContentLength;
            this.ContainerName = message.RequestMessage.ContainerId;
            this.Diagnostics = message.Diagnostics;
            //TODO: ItemCount needs to be added
        }

        /// <summary>
        /// StatusCode
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// RequestCharge
        /// </summary>
        public double? RequestCharge { get; }

        /// <summary>
        /// RequestLength
        /// </summary>
        public string RequestContentLength { get; }

        /// <summary>
        /// ResponseLength
        /// </summary>
        public string ResponseContentLength { get; }

        /// <summary>
        /// ContainerName
        /// </summary>
        public string ContainerName { get; }

        /// <summary>
        /// ItemCount
        /// </summary>
        public string ItemCount { get; }

        /// <summary>
        /// ItemCount
        /// </summary>
        public CosmosDiagnostics Diagnostics { get; }

    }
}
