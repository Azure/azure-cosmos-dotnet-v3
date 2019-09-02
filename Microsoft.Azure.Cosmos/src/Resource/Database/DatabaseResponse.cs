//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos database response
    /// </summary>
    public class DatabaseResponse : Response<DatabaseProperties>
    {
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
            HttpStatusCode httpStatusCode,
            CosmosHeaders headers,
            DatabaseProperties databaseProperties,
            Database database)
        {
            this.StatusCode = httpStatusCode;
            this.CosmosHeaders = headers;
            this.Resource = databaseProperties;
            this.Database = database;
        }

        /// <summary>
        /// The reference to the cosmos database. 
        /// This allows additional operations for the database and easier access to the container operations
        /// </summary>
        public virtual Database Database { get; }

        /// <inheritdoc/>
        internal override CosmosHeaders CosmosHeaders { get; }

        /// <inheritdoc/>
        public override DatabaseProperties Resource { get; }

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
        /// Get <see cref="Cosmos.Database"/> implicitly from <see cref="DatabaseResponse"/>
        /// </summary>
        /// <param name="response">DatabaseResponse</param>
        public static implicit operator Database(DatabaseResponse response)
        {
            return response.Database;
        }
    }
}