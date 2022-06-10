/ ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;

    internal class OpenTelemetryResponseCore
    {
        /// <summary>
        /// StatusCode
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// RequestCharge
        /// </summary>
        public double? RequestCharge { get; set; }

        /// <summary>
        /// RequestLength
        /// </summary>
        public string RequestContentLength { get; set; }

        /// <summary>
        /// ResponseLength
        /// </summary>
        public string ResponseContentLength { get; set; }

        /// <summary>
        /// ContainerName
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// ItemCount
        /// </summary>
        public string ItemCount { get; set; }

        /// <summary>
        /// ItemCount
        /// </summary>
        public CosmosDiagnostics Diagnostics { get; set; }
    }
}
