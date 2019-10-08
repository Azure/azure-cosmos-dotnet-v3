// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Data.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos throughput response
    /// </summary>
    public class ThroughputResponse : Response<ThroughputProperties>
    {
        private readonly Response rawResponse;

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
            Response response,
            ThroughputProperties throughputProperties)
        {
            this.rawResponse = response;
            this.Value = throughputProperties;
        }

        /// <inheritdoc/>
        public override Response GetRawResponse() => this.rawResponse;

        /// <inheritdoc/>
        public override ThroughputProperties Value { get; }

        /// <summary>
        /// Gets minimum throughput in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        public int? MinThroughput
        {
            get
            {
                if (this.rawResponse != null
                    && this.rawResponse.Headers.TryGetValue(WFConstants.BackendHeaders.MinimumRUsForOffer, out string minThroughput))
                {
                    return int.Parse(minThroughput);
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
                if (this.rawResponse != null
                    && this.rawResponse.Headers.TryGetValue(WFConstants.BackendHeaders.OfferReplacePending, out string offerReplacePending))
                {
                    return Boolean.Parse(offerReplacePending);
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
            return response.Value;
        }
    }
}