//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This is the conflicting resource resulting from a concurrent async operation in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// On rare occasions, during an async operation (insert, replace and delete), a version conflict may occur on a resource.
    /// The conflicting resource is persisted as a Conflict resource.  
    /// Inspecting Conflict resources will allow you to determine which operations and resources resulted in conflicts.
    /// </remarks>
    public abstract class CosmosConflict : CosmosIdentifier
    {
        /// <summary>
        /// Gets the operation that resulted in the conflict in the Azure Cosmos DB service.
        /// </summary>
        public abstract OperationKind OperationKind { get; }

        /// <summary>
        /// Gets the type of the conflicting resource in the Azure Cosmos DB service.
        /// </summary>
        public abstract Type ResourceType { get; }

        /// <summary>
        /// Deletes the current conflict instance
        /// </summary>
        /// <param name="requestOptions"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override Task<CosmosUserDefinedFunctionResponse> DeleteAsync(
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}