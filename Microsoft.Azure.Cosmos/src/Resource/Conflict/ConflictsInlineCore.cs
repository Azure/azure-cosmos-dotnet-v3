//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    // This class acts as a wrapper for environments that use SynchronizationContext.
    internal sealed class ConflictsInlineCore : Conflicts
    {
        private readonly ConflictsCore conflicts;

        internal ConflictsInlineCore(ConflictsCore conflicts)
        {
            this.conflicts = conflicts ?? throw new ArgumentNullException(nameof(conflicts));
        }

        public override Task<ResponseMessage> DeleteAsync(
            ConflictProperties conflict,
            PartitionKey partitionKey,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.conflicts.DeleteAsync(conflict, partitionKey, cancellationToken));
        }

        public override FeedIterator GetConflictQueryStreamIterator(
           string queryText = null,
           string continuationToken = null,
           QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(this.conflicts.GetConflictQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator<T> GetConflictQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.conflicts.GetConflictQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator GetConflictQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(this.conflicts.GetConflictQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator<T> GetConflictQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.conflicts.GetConflictQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override Task<ItemResponse<T>> ReadCurrentAsync<T>(
            ConflictProperties cosmosConflict,
            PartitionKey partitionKey,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.conflicts.ReadCurrentAsync<T>(cosmosConflict, partitionKey, cancellationToken));
        }

        public override T ReadConflictContent<T>(ConflictProperties cosmosConflict)
        {
            return this.conflicts.ReadConflictContent<T>(cosmosConflict);
        }
    }
}
