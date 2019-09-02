//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos stored procedure response
    /// </summary>
    public class StoredProcedureResponse : Response<StoredProcedureProperties>
    {
        /// <summary>
        /// Create a <see cref="StoredProcedureResponse"/> as a no-op for mock testing
        /// </summary>
        protected StoredProcedureResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal StoredProcedureResponse(
           HttpStatusCode httpStatusCode,
           CosmosHeaders headers,
           StoredProcedureProperties storedProcedureProperties)
        {
            this.StatusCode = httpStatusCode;
            this.CosmosHeaders = headers;
            this.Resource = storedProcedureProperties;
        }

        /// <inheritdoc/>
        internal override CosmosHeaders CosmosHeaders { get; }

        /// <inheritdoc/>
        public override StoredProcedureProperties Resource { get; }

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
        /// Gets the token for use with session consistency requests from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The token for use with session consistency requests.
        /// </value>
        public virtual string SessionToken => this.CosmosHeaders?.GetHeaderValue<string>(HttpConstants.HttpHeaders.SessionToken);

        /// <summary>
        /// Get <see cref="StoredProcedureProperties"/> implicitly from <see cref="StoredProcedureResponse"/>
        /// </summary>
        /// <param name="response">CosmosUserDefinedFunctionResponse</param>
        public static implicit operator StoredProcedureProperties(StoredProcedureResponse response)
        {
            return response.Resource;
        }
    }
}