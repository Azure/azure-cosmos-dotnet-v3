//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
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
        internal CosmosDatabaseResponse(
            HttpStatusCode httpStatusCode,
            CosmosResponseMessageHeaders headers,
            CosmosDatabaseSettings cosmosDatabaseSettings,
            CosmosDatabase database) : base(
                httpStatusCode, 
                headers, 
                cosmosDatabaseSettings)
        {
            this.Database = database;
        }

        /// <summary>
        /// The reference to the cosmos database. 
        /// This allows additional operations for the database and easier access to the container operations
        /// </summary>
        public virtual CosmosDatabase Database { get; private set; }

        /// <summary>
        /// Get <see cref="CosmosDatabase"/> implicitly from <see cref="CosmosDatabaseResponse"/>
        /// </summary>
        /// <param name="response">CosmosDatabaseResponse</param>
        public static implicit operator CosmosDatabase(CosmosDatabaseResponse response)
        {
            return response.Database;
        }
    }
}