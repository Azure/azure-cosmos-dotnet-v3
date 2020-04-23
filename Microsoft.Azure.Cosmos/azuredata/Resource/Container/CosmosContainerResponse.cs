//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    /// <summary>
    /// The cosmos container response
    /// </summary>
    public class CosmosContainerResponse : Response<CosmosContainerProperties>
    {
        private readonly Response rawResponse;

        /// <summary>
        /// Create a <see cref="CosmosContainerResponse"/> as a no-op for mock testing
        /// </summary>
        protected CosmosContainerResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal CosmosContainerResponse(
            Response response,
            CosmosContainerProperties containerProperties,
            CosmosContainer container)
        {
            this.rawResponse = response;
            this.Value = containerProperties;
            this.Container = container;
        }

        /// <summary>
        /// The reference to the cosmos container. This allows additional operations on the container
        /// or for easy access to other references like Items, StoredProcedures, etc..
        /// </summary>
        public virtual CosmosContainer Container { get; private set; }

        /// <inheritdoc/>
        public override CosmosContainerProperties Value { get; }

        /// <inheritdoc/>
        public override Response GetRawResponse() => this.rawResponse;

        /// <summary>
        /// Get <see cref="Cosmos.CosmosContainer"/> implicitly from <see cref="CosmosContainerResponse"/>
        /// </summary>
        /// <param name="response">ContainerResponse</param>
        public static implicit operator CosmosContainer(CosmosContainerResponse response)
        {
            return response.Container;
        }
    }
}