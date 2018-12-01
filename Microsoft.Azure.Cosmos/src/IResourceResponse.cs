//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Interface exposed for mocking purposes for the Azure Cosmos DB service.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    internal interface IResourceResponse<TResource> : IResourceResponseBase where TResource : CosmosResource, new()
    {
        /// <summary>
        /// Gets the resource returned in the response.
        /// </summary>
        /// <value>
        /// The resource returned in the response.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        TResource Resource { get; }
    }
}