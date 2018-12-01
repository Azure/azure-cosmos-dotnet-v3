//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;

    /// <summary>
    /// The cosmos item response
    /// </summary>
    public class CosmosItemResponse<T> : CosmosResponse<T>
    {
        /// <summary>
        /// Create a <see cref="CosmosItemResponse{T}"/> as a no-op for mock testing
        /// </summary>
        public CosmosItemResponse() : base()
        {

        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the CosmosResponseMessage
        /// </summary>
        private CosmosItemResponse(CosmosResponseMessage cosmosResponse) : base(cosmosResponse)
        {

        }

        /// <summary>
        /// Gets the token for use with session consistency requests from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The token for use with session consistency requests.
        /// </value>
        public virtual string SessionToken => this.Headers.GetHeaderValue<string>(HttpConstants.HttpHeaders.SessionToken);

        /// <summary>
        /// Create the cosmos item response.
        /// Creates the response object, deserializes the
        /// HTTP content stream, and disposes of the HttpResponseMessage
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="CosmosResponseMessage"/> from the Cosmos DB service</param>
        /// <param name="jsonSerializer">The cosmos JSON serializer</param>
        internal static CosmosItemResponse<CustomResponseType> CreateResponse<CustomResponseType>(
            CosmosResponseMessage cosmosResponseMessage,
            CosmosJsonSerializer jsonSerializer)
        {
            return CosmosResponse<CosmosContainerSettings>
                .InitResponse<CosmosItemResponse<CustomResponseType>, CustomResponseType>(
                    (httpResponse) => new CosmosItemResponse<CustomResponseType>(cosmosResponseMessage),
                    jsonSerializer,
                    cosmosResponseMessage);
        }
    }
}