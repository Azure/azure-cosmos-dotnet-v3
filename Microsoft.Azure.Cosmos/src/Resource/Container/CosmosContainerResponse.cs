//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net.Http;
    using System.Threading.Tasks;

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
        private CosmosContainerResponse(
            CosmosResponseMessage cosmosResponse, 
            CosmosContainer container) : base(cosmosResponse)
        {
            this.Container = container;
        }

        /// <summary>
        /// The reference to the cosmos container. This allows additional operations on the container
        /// or for easy access to other references like Items, StoredProcedures, etc..
        /// </summary>
        public virtual CosmosContainer Container { get; private set; }

        /// <summary>
        /// Get <see cref="CosmosContainer"/> implicitly from <see cref="CosmosContainerResponse"/>
        /// </summary>
        /// <param name="response">CosmosContainerResponse</param>
        public static implicit operator CosmosContainer(CosmosContainerResponse response)
        {
            return response.Container;
        }

        /// <summary>
        /// Create the cosmos container response.
        /// Creates the response object, deserializes the
        /// http content stream, and disposes of the HttpResponseMessage
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="CosmosResponseMessage"/> from the Cosmos DB service</param>
        /// <param name="jsonSerializer">The cosmos json serializer</param>
        /// <param name="container">The cosmos container</param>
        internal static CosmosContainerResponse CreateResponse(
            CosmosResponseMessage cosmosResponseMessage,
            CosmosJsonSerializer jsonSerializer,
            CosmosContainer container)
        {
            return CosmosResponse<CosmosContainerSettings>
                .InitResponse<CosmosContainerResponse, CosmosContainerSettings>(
                    (httpResponse) => new CosmosContainerResponse(cosmosResponseMessage, container),
                    jsonSerializer,
                    cosmosResponseMessage);
        }
    }
}