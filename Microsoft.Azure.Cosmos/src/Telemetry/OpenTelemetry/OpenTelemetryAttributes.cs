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
        /// CorrelatedActivityId
        /// </summary>
        internal string CorrelatedActivityId { get; set; }

        /// <summary>
        /// OperationType
        /// </summary>
        internal Documents.OperationType OperationType { get; set; }

        /// <summary>
        /// Batch Size
        /// </summary>
        internal int? BatchSize { get; set; }

        /// <summary>
        /// Gets or sets the operation type for batch operations. 
        /// Will have a value for homogeneous batch operations and will be null for heterogeneous batch operations.
        /// 
        /// Operation name should be prepended with BATCH for homogeneous operations, or be just BATCH for heterogeneous operations.
        /// </summary>
        /// <example>
        /// For example, if you have a batch of homogeneous operations like Read:
        /// <code>
        /// var recorder = new OpenTelemetryCoreRecorder();
        /// recorder.BatchOperationName = Documents.OperationType.Read; // Homogeneous batch
        /// string operationName = "BATCH." + recorder.BatchOperationName; // Results in "BATCH.Read"
        /// </code>
        /// 
        /// For a batch of heterogeneous operations:
        /// <code>
        /// var recorder = new OpenTelemetryCoreRecorder();
        /// recorder.BatchOperationName = null; // Heterogeneous batch
        /// string operationName = "BATCH"; // No specific operation type
        /// </code>
        /// </example>
        internal Documents.OperationType? BatchOperationName { get; set; }
    }
}
