// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;

    /// <summary>
    /// The cosmos throughput response
    /// </summary>
    public class ThroughputResponse : Response<ThroughputSettings>
    {
        /// <summary>
        /// Create a <see cref="ThroughputResponse"/> as a no-op for mock testing
        /// </summary>
        public ThroughputResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal ThroughputResponse(
            HttpStatusCode httpStatusCode,
            CosmosResponseMessageHeaders headers,
            ThroughputSettings throughputSettings,
            int? allowedMinThroughput)
            : base(
                httpStatusCode,
                headers,
                throughputSettings)
        {
            this.AllowedMinThroughput = allowedMinThroughput;
        }

        /// <summary>
        /// Gets minimum throughput in measurement of Requests-per-Unit in the Azure Cosmos service.
        /// </summary>
        public int? AllowedMinThroughput;
    }
}
