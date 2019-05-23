//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos item response
    /// </summary>
    public class ItemResponse<T> : Response<T>
    {
        /// <summary>
        /// Create a <see cref="ItemResponse{T}"/> as a no-op for mock testing
        /// </summary>
        public ItemResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the CosmosResponseMessage
        /// </summary>
        internal ItemResponse(
            HttpStatusCode httpStatusCode,
            CosmosResponseMessageHeaders headers,
            T item)
            : base(
                httpStatusCode,
                headers,
                item)
        {
        }

        /// <summary>
        /// Gets the token for use with session consistency requests from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The token for use with session consistency requests.
        /// </value>
        public virtual string SessionToken => this.Headers.GetHeaderValue<string>(HttpConstants.HttpHeaders.SessionToken);
    }
}