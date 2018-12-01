//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading.Tasks;

    /// <summary>
    /// The cosmos database response
    /// </summary>
    public class CosmosDatabaseResponse : CosmosResponse<CosmosDatabaseSettings>
    {
        /// <summary>
        /// Create a <see cref="CosmosDatabaseResponse"/> as a no-op for mock testing
        /// </summary>
        public CosmosDatabaseResponse() : base()
        {

        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        private CosmosDatabaseResponse(
            CosmosResponseMessage cosmosResponse,
            CosmosDatabase database) : base(cosmosResponse)
        {
            this.Database = database;
        }

        /// <summary>
        /// The reference to the cosmos database. 
        /// This allows additional operations for the database and easier access to the container operations
        /// </summary>
        public virtual CosmosDatabase Database { get; private set; }

        /// <summary>
        /// Get <see cref="CosmosDatabase"/> implictly from <see cref="CosmosDatabaseResponse"/>
        /// </summary>
        /// <param name="response">CosmosDatabaseResponse</param>
        public static implicit operator CosmosDatabase(CosmosDatabaseResponse response)
        {
            return response.Database;
        }

        /// <summary>
        /// Create the cosmos database response.
        /// Creates the response object, deserializes the
        /// http content stream, and disposes of the HttpResponseMessage
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="CosmosResponseMessage"/> from the Cosmos DB service</param>
        /// <param name="jsonSerializer">The cosmos json serializer</param>
        /// <param name="database">The cosmos database</param>
        internal static CosmosDatabaseResponse CreateResponse(
            CosmosResponseMessage cosmosResponseMessage,
            CosmosJsonSerializer jsonSerializer,
            CosmosDatabase database)
        {
            return CosmosResponse<CosmosDatabaseSettings>
                .InitResponse<CosmosDatabaseResponse, CosmosDatabaseSettings>(
                    (httpResponse) => new CosmosDatabaseResponse(cosmosResponseMessage, database),
                    jsonSerializer,
                    cosmosResponseMessage);
        }
    }
}