// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Net;
    using Microsoft.Azure.Cosmos.Query.Core;

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
            if (requestMessage != null)
            {
                this.OperationType = requestMessage.OperationType;
                this.ResourceType = requestMessage.ResourceType;
            }
            else
            {
                this.OperationType = Documents.OperationType.Invalid;
            }
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
        /// ResourceType
        /// </summary>
        internal Documents.ResourceType? ResourceType { get; set; }

        /// <summary>
        /// Batch Size
        /// </summary>
        internal int? BatchSize { get; set; }

        /// <summary>
        /// Query Spec with Query Text and Parameters
        /// </summary>
        internal SqlQuerySpec QuerySpec { get; set; }
    }
}
