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
            Headers headers,
            DatabaseProperties databaseProperties,
            Database database)
        {
            this.StatusCode = httpStatusCode;
            this.Headers = headers;
            this.Resource = databaseProperties;
            this.Database = database;
        }

        /// <summary>
        /// The reference to the cosmos database. 
        /// This allows additional operations for the database and easier access to the container operations
        /// </summary>
        public virtual Database Database { get; }

        /// <inheritdoc/>
        public override Headers Headers { get; }

        /// <inheritdoc/>
        public override DatabaseProperties Resource { get; }

        /// <inheritdoc/>
        public override HttpStatusCode StatusCode { get; }

        /// <inheritdoc/>
        public override double RequestCharge => this.Headers?.RequestCharge ?? 0;

        /// <inheritdoc/>
        public override string ActivityId => this.Headers?.ActivityId;

        /// <inheritdoc/>
        public override string ETag => this.Headers?.ETag;

        /// <inheritdoc/>
        internal override string MaxResourceQuota => this.Headers?.GetHeaderValue<string>(HttpConstants.HttpHeaders.MaxResourceQuota);

        /// <inheritdoc/>
        internal override string CurrentResourceQuotaUsage => this.Headers?.GetHeaderValue<string>(HttpConstants.HttpHeaders.CurrentResourceQuotaUsage);

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