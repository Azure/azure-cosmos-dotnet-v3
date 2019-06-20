//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Globalization;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    /// <summary>
    /// The Cosmos query request options
    /// </summary>
    public class FeedRequestOptions : RequestOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum number of items to be returned in the enumeration operation.
        /// </value> 
        /// <remarks>
        /// Used for query pagination.
        /// '-1' Used for dynamic page size.
        /// This is a maximum. Query can return 0 items in the page.
        /// </remarks>
        public int? MaxItemCount { get; set; }

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="RequestMessage"/></param>
        internal override void PopulateRequestOptions(RequestMessage request)
        {
            // Flow the pageSize only when we are not doing client eval
            if (this.MaxItemCount.HasValue)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.PageSize, this.MaxItemCount.ToString());
            }

            base.PopulateRequestOptions(request);
        }
    }
}