//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    /// <summary>
    /// The cosmos database response
    /// </summary>
    public class DatabaseResponse : Response<CosmosDatabaseProperties>
    {
        private readonly Response rawResponse;

        /// <summary>
        /// Create a <see cref="DatabaseResponse"/> as a no-op for mock testing
        /// </summary>
        protected DatabaseResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal DatabaseResponse(
            Response response,
            CosmosDatabaseProperties databaseProperties,
            CosmosDatabase database)
        {
            this.rawResponse = response;
            this.Value = databaseProperties;
            this.Database = database;
        }

        /// <summary>
        /// The reference to the cosmos database. 
        /// This allows additional operations for the database and easier access to the container operations
        /// </summary>
        public virtual CosmosDatabase Database { get; }

        /// <inheritdoc/>
        public override CosmosDatabaseProperties Value { get; }

        /// <inheritdoc/>
        public override Response GetRawResponse() => this.rawResponse;

        /// <summary>
        /// Get <see cref="Cosmos.CosmosDatabase"/> implicitly from <see cref="DatabaseResponse"/>
        /// </summary>
        /// <param name="response">DatabaseResponse</param>
        public static implicit operator CosmosDatabase(DatabaseResponse response)
        {
            return response.Database;
        }
    }
}