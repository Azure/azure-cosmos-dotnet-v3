// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System.Net;

    internal class OpenTelemetryAttributes
    {
        /// <summary>
        /// For testing purpose only, to make initialization of this class easy 
        /// </summary>
        internal OpenTelemetryAttributes()
        {
        }

        internal OpenTelemetryAttributes(RequestMessage requestMessage)
        {
            this.RequestContentLength = requestMessage?.Headers?.ContentLength;
        }

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
        /// ItemCount
        /// </summary>
        internal string ItemCount { get; set; }

        /// <summary>
        /// ItemCount
        /// </summary>
        internal CosmosDiagnostics Diagnostics { get; set; }

        /// <summary>
        /// SubStatusCode
        /// </summary>
        internal int SubStatusCode { get; set; }
        
        /// <summary>
        /// ActivityId
        /// </summary>
        internal string ActivityId { get; set; }

        /// <summary>
        /// ActivityId
        /// </summary>
        internal string CorrelationId { get; set; }

        /// <summary>
        /// OperationType
        /// </summary>
        internal string OperationType { get; set; }
    }
}
