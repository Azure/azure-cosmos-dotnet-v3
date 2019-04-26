//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
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
    public abstract class CosmosConflict
    {
        /// <summary>
        /// The Id of the Cosmos conflict
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Deletes the current conflict instance
        /// </summary>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns></returns>
        public abstract Task<CosmosConflictResponse> DeleteAsync(object partitionKey, CancellationToken cancellationToken = default(CancellationToken));
    }
}