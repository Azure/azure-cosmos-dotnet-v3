//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;

    /// <summary>
    /// The cosmos container response
    /// </summary>
    public class ContainerResponse : Response<ContainerProperties>
    {
        /// <summary>
        /// Create a <see cref="ContainerResponse"/> as a no-op for mock testing
        /// </summary>
        public ContainerResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal ContainerResponse(
            HttpStatusCode httpStatusCode,
            CosmosResponseMessageHeaders headers,
            ContainerProperties containerProperties,
            CosmosContainer container)
            : base(
                httpStatusCode,
                headers,
                containerProperties)
        {
            this.Container = container;
        }

        /// <summary>
        /// The reference to the cosmos container. This allows additional operations on the container
        /// or for easy access to other references like Items, StoredProcedures, etc..
        /// </summary>
        public virtual CosmosContainer Container { get; private set; }

        /// <summary>
        /// Get <see cref="CosmosContainer"/> implicitly from <see cref="ContainerResponse"/>
        /// </summary>
        /// <param name="response">ContainerResponse</param>
        public static implicit operator CosmosContainer(ContainerResponse response)
        {
            return response.Container;
        }
    }
}