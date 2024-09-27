//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;

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
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(DeleteAsync),
                containerName: null,
                databaseName: null,
                operationType: Documents.OperationType.Delete,
                requestOptions: null,
                task: (trace) => base.DeleteAsync(conflict, partitionKey, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.DeleteConflict, (response) => new OpenTelemetryResponse(response)));
        }

        public override FeedIterator GetConflictQueryStreamIterator(
          string queryText = null,
          string continuationToken = null,
          QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetConflictQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator<T> GetConflictQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetConflictQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator GetConflictQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetConflictQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator<T> GetConflictQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetConflictQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override Task<ItemResponse<T>> ReadCurrentAsync<T>(
            ConflictProperties cosmosConflict,
            PartitionKey partitionKey,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadCurrentAsync),
                containerName: null,
                databaseName: null,
                operationType: Documents.OperationType.Read,
                requestOptions: null,
                task: (trace) => base.ReadCurrentAsync<T>(cosmosConflict, partitionKey, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadConflict, (response) => new OpenTelemetryResponse<T>(response)));
        }

        public override T ReadConflictContent<T>(ConflictProperties cosmosConflict)
        {
            return base.ReadConflictContent<T>(cosmosConflict);
        }
    }
}
