// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos throughput response
    /// </summary>
    public class ThroughputResponse : Response<int?>
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
            Headers headers,
            int? throughput)
            : base(
                httpStatusCode,
                headers,
                throughput)
        {
        }

        /// <summary>
        /// Gets the provisioned throughput in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        public int? Throughput
        {
            get
            {
                return this.Resource;
            }
        }

        /// <summary>
        /// Gets minimum throughput in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        public int? MinThroughput
        {
            get
            {
                if (Headers.GetHeaderValue<string>(WFConstants.BackendHeaders.MinimumRUsForOffer) != null)
                {
                    return int.Parse(Headers.GetHeaderValue<string>(WFConstants.BackendHeaders.MinimumRUsForOffer));
                }
                return null;
            }
        }

        /// <summary>
        /// Gets the status whether offer replace is successful or pending.
        /// </summary>
        public bool? IsOfferReplacePending
        {
            get
            {
                if (Headers.GetHeaderValue<string>(WFConstants.BackendHeaders.OfferReplacePending) != null)
                {
                    return Boolean.Parse(Headers.GetHeaderValue<string>(WFConstants.BackendHeaders.MinimumRUsForOffer));
                }
                return null;
            }
        }
    }
}