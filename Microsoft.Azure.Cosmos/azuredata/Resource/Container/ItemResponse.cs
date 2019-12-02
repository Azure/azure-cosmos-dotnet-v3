// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Cosmos
{
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos item response
    /// </summary>
    public class ItemResponse<T> : Response<T>
    {
        private readonly Response rawResponse;

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
        public virtual string Session
        {
            get
            {
                if (this.GetRawResponse().Headers.TryGetValue(HttpConstants.HttpHeaders.SessionToken, out string session))
                {
                    return session;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources. 
        /// </remarks>
        public virtual ETag? ETag => this.GetRawResponse().Headers.ETag;
    }
}
