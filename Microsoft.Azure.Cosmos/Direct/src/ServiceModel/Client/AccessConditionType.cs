//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Client
{
    /// <summary>
    /// Specifies the set of <see cref="AccessCondition"/> types that can be used for operations in the Azure Cosmos DB service. 
    /// </summary>
    /// <seealso cref="AccessCondition"/>
    /// <seealso cref="RequestOptions"/>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    enum AccessConditionType
    {
        /// <summary>
        /// Check if the resource's ETag value matches the ETag value performed.
        /// </summary>
        /// <remarks>
        /// Used for optimistic concurrency control, e.g., replace the document only if the ETag is identical to the one 
        /// included in the request to avoid lost updates.
        /// </remarks>
        IfMatch,

        /// <summary>
        /// Check if the resource's ETag value does not match ETag value performed.
        /// </summary>
        /// <remarks>
        /// Used for caching scenarios to reduce network traffic, e.g., return the document in the payload only if the ETag 
        /// has changed from the one in the request.
        /// </remarks>
        IfNoneMatch
    }
}
