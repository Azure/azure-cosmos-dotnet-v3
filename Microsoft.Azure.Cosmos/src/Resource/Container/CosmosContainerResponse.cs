//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;

    /// <summary>
    /// The cosmos container response
    /// </summary>
    public class CosmosContainerResponse : CosmosResponse<CosmosContainerSettings>
    {
        /// <summary>
        /// Create a <see cref="CosmosContainerResponse"/> as a no-op for mock testing
        /// </summary>
        public CosmosContainerResponse() : base()
        {

        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal CosmosContainerResponse(
            HttpStatusCode httpStatusCode,
            CosmosResponseMessageHeaders headers,
            CosmosContainerSettings cosmosContainerSettings,
            CosmosContainerCore container) : base(
                httpStatusCode,
                headers,
                cosmosContainerSettings)
        {
            this.Container = container;
        }

        /// <summary>
        /// The reference to the cosmos container. This allows additional operations on the container
        /// or for easy access to other references like Items, StoredProcedures, etc..
        /// </summary>
        public virtual CosmosContainerCore Container { get; private set; }

        /// <summary>
        /// Get <see cref="CosmosContainerCore"/> implicitly from <see cref="CosmosContainerResponse"/>
        /// </summary>
        /// <param name="response">CosmosContainerResponse</param>
        public static implicit operator CosmosContainerCore(CosmosContainerResponse response)
        {
            return response.Container;
        }
    }
}