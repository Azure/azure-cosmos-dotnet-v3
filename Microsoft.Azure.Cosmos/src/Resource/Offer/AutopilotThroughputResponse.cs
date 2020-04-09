// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos throughput response
    /// </summary>
    #if INTERNAL
    public
#else
    internal
#endif
    class AutopilotThroughputResponse : Response<AutopilotThroughputProperties>
    {
        /// <summary>
        /// Create a <see cref="ThroughputResponse"/> as a no-op for mock testing
        /// </summary>
        protected AutopilotThroughputResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal AutopilotThroughputResponse(
            HttpStatusCode httpStatusCode,
            Headers headers,
            AutopilotThroughputProperties throughputProperties,
            CosmosDiagnostics diagnostics)
        {
            this.StatusCode = httpStatusCode;
            this.Headers = headers;
            this.Resource = throughputProperties;
            this.Diagnostics = diagnostics;
        }

        /// <inheritdoc/>
        public override Headers Headers { get; }

        /// <inheritdoc/>
        public override AutopilotThroughputProperties Resource { get; }

        /// <inheritdoc/>
        public override HttpStatusCode StatusCode { get; }

        /// <inheritdoc/>
        public override CosmosDiagnostics Diagnostics { get; }

        /// <inheritdoc/>
        public override double RequestCharge => this.Headers?.RequestCharge ?? 0;

        /// <inheritdoc/>
        public override string ActivityId => this.Headers?.ActivityId;

        /// <inheritdoc/>
        public override string ETag => this.Headers?.ETag;

        /// <summary>
        /// Gets minimum throughput in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        public int? MinThroughput
        {
            get
            {
                if (this.Headers?.GetHeaderValue<string>(WFConstants.BackendHeaders.MinimumRUsForOffer) != null)
                {
                    return int.Parse(this.Headers.GetHeaderValue<string>(WFConstants.BackendHeaders.MinimumRUsForOffer));
                }
                return null;
            }
        }

        /// <summary>
        /// Gets the status whether offer replace is successful or pending.
        /// </summary>
        public bool? IsReplacePending
        {
            get
            {
                if (this.Headers.GetHeaderValue<string>(WFConstants.BackendHeaders.OfferReplacePending) != null)
                {
                    return Boolean.Parse(this.Headers.GetHeaderValue<string>(WFConstants.BackendHeaders.OfferReplacePending));
                }
                return null;
            }
        }

        /// <summary>
        /// Get <see cref="ThroughputProperties"/> implicitly from <see cref="ThroughputResponse"/>
        /// </summary>
        /// <param name="response">Throughput response</param>
        public static implicit operator AutopilotThroughputProperties(AutopilotThroughputResponse response)
        {
            return response.Resource;
        }
    }
}
