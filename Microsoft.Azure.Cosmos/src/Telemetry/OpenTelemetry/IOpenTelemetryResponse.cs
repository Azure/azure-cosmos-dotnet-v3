// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// IOpenTelemetryResponse
    /// </summary>
    public interface IOpenTelemetryResponse
    {
        /// <summary>
        /// StatusCode
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// RequestCharge
        /// </summary>
        public double RequestCharge { get; }

        /// <summary>
        /// RequestLength
        /// </summary>
        public long RequestLength { get; }

        /// <summary>
        /// ResponseLength
        /// </summary>
        public long ResponseLength { get; }

    }
}
