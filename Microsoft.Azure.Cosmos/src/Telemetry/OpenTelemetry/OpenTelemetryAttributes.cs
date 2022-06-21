// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;

    internal class OpenTelemetryAttributes
    {
        /// <summary>
        /// StatusCode
        /// </summary>
        internal HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// RequestCharge
        /// </summary>
        internal double? RequestCharge { get; set; }

        /// <summary>
        /// RequestLength
        /// </summary>
        internal string RequestContentLength { get; set; }

        /// <summary>
        /// ResponseLength
        /// </summary>
        internal string ResponseContentLength { get; set; }

        /// <summary>
        /// ContainerName
        /// </summary>
        internal string ContainerName { get; set; }

        /// <summary>
        /// ItemCount
        /// </summary>
        internal string ItemCount { get; set; }

        /// <summary>
        /// ItemCount
        /// </summary>
        internal CosmosDiagnostics Diagnostics { get; set; }
    }
}
