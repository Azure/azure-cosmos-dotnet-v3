//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    // This class acts as a wrapper for environments that use SynchronizationContext.
    internal sealed class ConflictsInlineCore : ConflictsCore
    {
        internal ConflictsInlineCore(
            CosmosClientContext clientContext,
            ContainerInternal container)
            : base(
                  clientContext,
                  container)
        {
        }

        public override Task<ResponseMessage> DeleteAsync(
            ConflictProperties conflict,
            PartitionKey partitionKey,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(DeleteAsync),
                null,
                (diagnostics) => base.DeleteAsync(diagnostics, conflict, partitionKey, cancellationToken));
        }

        public override FeedIterator GetConflictQueryStreamIterator(
           string queryText = null,
           string continuationToken = null,
           QueryRequestOptions requestOptions = null)
        {
            return base.GetConflictQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator<T> GetConflictQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return base.GetConflictQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator GetConflictQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(
                this.ClientContext,
                base.GetConflictQueryStreamIteratorHelper(
                    queryDefinition,
                    continuationToken,
                    requestOptions));
        }

        public override FeedIterator<T> GetConflictQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(
                this.ClientContext,
                base.GetConflictQueryIteratorHelper<T>(
                    queryDefinition,
                    continuationToken,
                    requestOptions));
        }

        public override Task<ItemResponse<T>> ReadCurrentAsync<T>(
            ConflictProperties cosmosConflict,
            PartitionKey partitionKey,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadCurrentAsync),
                null,
                (diagnostics) => base.ReadCurrentAsync<T>(diagnostics, cosmosConflict, partitionKey, cancellationToken));
        }

        public override T ReadConflictContent<T>(ConflictProperties cosmosConflict)
        {
            return base.ReadConflictContent<T>(cosmosConflict);
        }
    }
}
