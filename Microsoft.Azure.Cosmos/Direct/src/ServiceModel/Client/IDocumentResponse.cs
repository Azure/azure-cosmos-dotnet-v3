//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Client
{
    /// <summary>
    /// Interface exposed for mocking purposes for the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    interface IDocumentResponse<TDocument> : IResourceResponseBase
    {
        /// <summary>
        /// Gets the document returned in the response.
        /// </summary>
        /// <value>
        /// The document returned in the response.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        TDocument Document { get; }
    }
}