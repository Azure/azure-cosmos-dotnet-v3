//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Operations for reading/querying conflicts in a Azure Cosmos container.
    /// </summary>
    public abstract class CosmosConflicts
    {
        /// <summary>
        /// Delete a conflict from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKey">The partition key for the conflict.</param>
        /// <param name="conflict">The conflict to delete.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <seealso cref="ConflictProperties"/>
        public abstract Task<CosmosResponseMessage> DeleteConflictAsync(
            PartitionKey partitionKey,
            ConflictProperties conflict,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads the item that originated the conflict.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="cosmosConflict">The conflict for which we want to read the item.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The current state of the item associated with the conflict.</returns>
        /// <seealso cref="ConflictProperties"/>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<CosmosConflictProperties> conflictIterator = await cosmosConflicts.GetConflictsIterator();
        /// while (conflictIterator.HasMoreResults)
        /// {
        ///     foreach(CosmosConflictProperties item in await conflictIterator.FetchNextSetAsync())
        ///     {
        ///         MyClass intendedChanges = CosmosConflicts.ReadConflictContent<MyClass>(item);
        ///         ItemResponse<MyClass> currentState = await CosmosConflicts.ReadCurrentAsync<MyClass>(intendedChanges.MyPartitionKey, item);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<T>> ReadCurrentAsync<T>(
            PartitionKey partitionKey,
            ConflictProperties cosmosConflict,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads the content of the Conflict resource in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="cosmosConflict">The conflict for which we want to read the content of.</param>
        /// <returns>The content of the conflict.</returns>
        /// <seealso cref="ConflictProperties"/>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<ConflictProperties> conflictIterator = await conflicts.GetConflictsIterator();
        /// while (conflictIterator.HasMoreResults)
        /// {
        ///     foreach(ConflictProperties item in await conflictIterator.FetchNextSetAsync())
        ///     {
        ///         MyClass intendedChanges = conflicts.ReadConflictContent<MyClass>(item);
        ///         ItemResponse<MyClass> currentState = await conflicts.ReadCurrentAsync<MyClass>(intendedChanges.MyPartitionKey, item);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract T ReadConflictContent<T>(ConflictProperties cosmosConflict);

        /// <summary>
        /// Obtains an iterator to go through the <see cref="ConflictProperties"/> on an Azure Cosmos container.
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <returns>An iterator to go through the conflicts.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<ConflictProperties> conflictIterator = await conflicts.GetConflictsIterator();
        /// while (conflictIterator.HasMoreResults)
        /// {
        ///     foreach(ConflictProperties item in await conflictIterator.FetchNextSetAsync())
        ///     {
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<ConflictProperties> GetConflictsIterator(
            int? maxItemCount = null,
            string continuationToken = null);

        /// <summary>
        /// Gets an iterator to go through all the conflicts for the container as the original CosmosResponseMessage
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <returns>An iterator to go through the conflicts.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator conflictIterator = await conflicts.GetConflictsStreamIterator();
        /// while (conflictIterator.HasMoreResults)
        /// {
        ///     using (CosmosResponseMessage iterator = await feedIterator.FetchNextSetAsync())
        ///     {
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator GetConflictsStreamIterator(
            int? maxItemCount = null,
            string continuationToken = null);
    }
}