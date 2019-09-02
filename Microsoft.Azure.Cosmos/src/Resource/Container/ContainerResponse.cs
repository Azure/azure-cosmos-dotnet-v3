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
            CosmosHeaders headers,
            ContainerProperties containerProperties,
            Container container)
        {
            this.StatusCode = httpStatusCode;
            this.CosmosHeaders = headers;
            this.Resource = containerProperties;
            this.Container = container;
        }

        /// <summary>
        /// The reference to the cosmos container. This allows additional operations on the container
        /// or for easy access to other references like Items, StoredProcedures, etc..
        /// </summary>
        public virtual Container Container { get; private set; }

        /// <inheritdoc/>
        internal override CosmosHeaders CosmosHeaders { get; }

        /// <inheritdoc/>
        public override ContainerProperties Resource { get; }

        /// <inheritdoc/>
        public override HttpStatusCode StatusCode { get; }

        /// <inheritdoc/>
        public override double RequestCharge => this.CosmosHeaders?.RequestCharge ?? 0;

        /// <inheritdoc/>
        public override string ActivityId => this.CosmosHeaders?.ActivityId;

        /// <inheritdoc/>
        public override string ETag => this.CosmosHeaders?.ETag;

        /// <inheritdoc/>
        internal override string MaxResourceQuota => this.CosmosHeaders?.GetHeaderValue<string>(HttpConstants.HttpHeaders.MaxResourceQuota);

        /// <inheritdoc/>
        internal override string CurrentResourceQuotaUsage => this.CosmosHeaders?.GetHeaderValue<string>(HttpConstants.HttpHeaders.CurrentResourceQuotaUsage);

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