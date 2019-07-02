//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos container response
    /// </summary>
    public class ContainerResponse : Response<ContainerProperties>
    {
        /// <summary>
        /// Create a <see cref="ContainerResponse"/> as a no-op for mock testing
        /// </summary>
        protected ContainerResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal ContainerResponse(
            HttpStatusCode httpStatusCode,
            Headers headers,
            ContainerProperties containerProperties,
            Container container)
        {
            this.StatusCode = httpStatusCode;
            this.Headers = headers;
            this.Resource = containerProperties;
            this.Container = container;
        }

        /// <summary>
        /// The reference to the cosmos container. This allows additional operations on the container
        /// or for easy access to other references like Items, StoredProcedures, etc..
        /// </summary>
        public virtual Container Container { get; private set; }

        /// <inheritdoc/>
        public override Headers Headers { get; }

        /// <inheritdoc/>
        public override ContainerProperties Resource { get; }

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
        /// Get <see cref="Cosmos.Container"/> implicitly from <see cref="ContainerResponse"/>
        /// </summary>
        /// <param name="response">ContainerResponse</param>
        public static implicit operator Container(ContainerResponse response)
        {
            return response.Container;
        }
    }
}