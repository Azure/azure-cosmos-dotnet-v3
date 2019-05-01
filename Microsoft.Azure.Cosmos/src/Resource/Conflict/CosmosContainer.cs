//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    public abstract partial class CosmosContainer
    {
        /// <summary>
        /// Delete a conflict from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="id">The conflict id.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <seealso cref="CosmosConflict"/>
        public abstract Task<CosmosResponseMessage> DeleteConflictAsync(
            object partitionKey,
            string id,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads the item that originated the conflict.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="cosmosConflict">The conflict for which we want to read the item.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <seealso cref="CosmosConflict"/>
        public abstract Task<CosmosResponseMessage> ReadConflictItemAsync(
            object partitionKey,
            CosmosConflict cosmosConflict,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
