//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Client
{
    /// <summary>
    /// Interface exposed for mocking purposes for the Azure Cosmos DB service.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    interface IResourceResponse<TResource> : IResourceResponseBase where TResource : Resource, new()
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