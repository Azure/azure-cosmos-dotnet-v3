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
    public class DatabaseResponse : Response<DatabaseProperties>
    {
        /// <summary>
        /// Create a <see cref="DatabaseResponse"/> as a no-op for mock testing
        /// </summary>
        public DatabaseResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal DatabaseResponse(
            HttpStatusCode httpStatusCode,
            CosmosResponseMessageHeaders headers,
            DatabaseProperties databaseProperties,
            CosmosDatabase database)
            : base(
                httpStatusCode, 
                headers, 
                databaseProperties)
        {
            this.Database = database;
        }

        /// <summary>
        /// The reference to the cosmos database. 
        /// This allows additional operations for the database and easier access to the container operations
        /// </summary>
        public virtual CosmosDatabase Database { get; private set; }

        /// <summary>
        /// Get <see cref="CosmosDatabase"/> implicitly from <see cref="DatabaseResponse"/>
        /// </summary>
        /// <param name="response">DatabaseResponse</param>
        public static implicit operator CosmosDatabase(DatabaseResponse response)
        {
            return response.Database;
        }
    }
}