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
    public class ThroughputResponse : Response<ThroughputProperties>
    {
        /// <summary>
        /// Create a <see cref="ThroughputResponse"/> as a no-op for mock testing
        /// </summary>
        protected ThroughputResponse()
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
            ThroughputProperties throughputProperties)
        {
            this.StatusCode = httpStatusCode;
            this.Headers = headers;
            this.Resource = throughputProperties;
        }

        /// <inheritdoc/>
        public override Headers Headers { get; }

        /// <inheritdoc/>
        public override ThroughputProperties Resource { get; }

        /// <inheritdoc/>
        public override HttpStatusCode StatusCode { get; }

        /// <inheritdoc/>
        public override double RequestCharge => this.Headers?.RequestCharge ?? 0;

        /// <inheritdoc/>
        public override string ActivityId => this.Headers?.ActivityId;

        /// <inheritdoc/>
        public override string ETag => this.Headers?.ETag;

        /// <inheritdoc/>
        internal override string MaxResourceQuota => this.Headers?.GetHeaderValue<string>(HttpConstants.HttpHeaders.MaxResourceQuota);

        /// <inheritdoc/>
        internal override string CurrentResourceQuotaUsage => this.Headers?.GetHeaderValue<string>(HttpConstants.HttpHeaders.CurrentResourceQuotaUsage);

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
                    return Boolean.Parse(this.Headers.GetHeaderValue<string>(WFConstants.BackendHeaders.MinimumRUsForOffer));
                }
                return null;
            }
        }

        /// <summary>
        /// Get <see cref="ThroughputProperties"/> implicitly from <see cref="ThroughputResponse"/>
        /// </summary>
        /// <param name="response">Throughput response</param>
        public static implicit operator ThroughputProperties(ThroughputResponse response)
        {
            return response.Resource;
        }
    }
}