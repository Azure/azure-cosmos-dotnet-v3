// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System.Net;
    using Microsoft.Azure.Documents;

    internal class OpenTelemetryAttributes
    {
        internal const string NotAvailable = "information not available";

        /// <summary>
        /// For testing purpose only, to make initialization of this class easy 
        /// </summary>
        internal OpenTelemetryAttributes()
        {
        }

        internal OpenTelemetryAttributes(RequestMessage requestMessage, string containerName, string databaseName)
        {
            this.RequestContentLength = requestMessage?.Headers?.ContentLength ?? OpenTelemetryAttributes.NotAvailable;
            this.OperationType = requestMessage?.OperationType.ToOperationTypeString() ?? OpenTelemetryAttributes.NotAvailable;
            this.DatabaseName = requestMessage?.DatabaseId ?? databaseName ?? OpenTelemetryAttributes.NotAvailable;
            this.ContainerName = requestMessage?.ContainerId ?? containerName ?? OpenTelemetryAttributes.NotAvailable;
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
        /// DatabaseName
        /// </summary>
        internal string DatabaseName { get; set; }

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

        /// <summary>
        /// OperationType
        /// </summary>
        internal string OperationType { get; set; }
    }
}
