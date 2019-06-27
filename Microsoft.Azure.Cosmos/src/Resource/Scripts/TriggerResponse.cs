//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos trigger response
    /// </summary>
    public class TriggerResponse : Response<TriggerProperties>
    {
        /// <summary>
        /// Create a <see cref="TriggerResponse"/> as a no-op for mock testing
        /// </summary>
        protected TriggerResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal TriggerResponse(
           HttpStatusCode httpStatusCode,
           Headers headers,
           TriggerProperties triggerProperties)
        {
            this.StatusCode = httpStatusCode;
            this.Headers = headers;
            this.Resource = triggerProperties;
        }

        /// <inheritdoc/>
        public override Headers Headers { get; }

        /// <inheritdoc/>
        public override TriggerProperties Resource { get; }

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
        /// Get <see cref="TriggerProperties"/> implicitly from <see cref="TriggerResponse"/>
        /// </summary>
        /// <param name="response">CosmosUserDefinedFunctionResponse</param>
        public static implicit operator TriggerProperties(TriggerResponse response)
        {
            return response.Resource;
        }
    }
}