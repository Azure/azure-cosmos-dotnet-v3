// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Data.Cosmos
{
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos item response
    /// </summary>
    public class ItemResponse<T> : Response<T>
    {
        private readonly Response rawResponse;
        private readonly Headers cosmosHeaders;

        /// <summary>
        /// Create a <see cref="ItemResponse{T}"/> as a no-op for mock testing
        /// </summary>
        protected ItemResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the CosmosResponseMessage
        /// </summary>
        internal ItemResponse(
            Response response,
            T item)
        {
            this.rawResponse = response;
            this.Value = item;
            ResponseMessage responseMessage = response as ResponseMessage;
            if (responseMessage != null)
            {
                this.cosmosHeaders = responseMessage.CosmosHeaders;
            }
        }

        /// <inheritdoc/>
        public override Response GetRawResponse() => this.rawResponse;

        /// <inheritdoc/>
        public override T Value { get; }

        /// <summary>
        /// Gets the session token for the current request.
        /// </summary>
        /// <value>
        /// The session token is used in Session consistency.
        /// </value>
        public virtual string Session => this.cosmosHeaders?.Session;

        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in request units.
        /// </value>
        public virtual double RequestCharge => this.cosmosHeaders?.RequestCharge ?? 0;

        /// <summary>
        /// Gets the activity ID for the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        public virtual string ActivityId => this.cosmosHeaders?.ActivityId;

        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources. 
        /// </remarks>
        public virtual string ETag => this.cosmosHeaders?.ETag;

        internal virtual string MaxResourceQuota => this.cosmosHeaders?.GetHeaderValue<string>(HttpConstants.HttpHeaders.MaxResourceQuota);

        internal virtual string CurrentResourceQuotaUsage => this.cosmosHeaders?.GetHeaderValue<string>(HttpConstants.HttpHeaders.CurrentResourceQuotaUsage);

    }
}
