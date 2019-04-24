//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class CosmosConflictsCore : CosmosConflicts
    {
        private readonly CosmosContainerCore container;
        private readonly CosmosClient client;

        public CosmosConflictsCore(CosmosContainerCore container)
        {
            this.container = container;
            this.client = container.Client;
        }

        public override CosmosResultSetIterator<CosmosConflictSettings> GetConflictsIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<CosmosConflictSettings>(
                maxItemCount,
                continuationToken,
                null,
                this.ContainerFeedRequestExecutor);
        }

        public override CosmosConflict this[string id] => new CosmosConflictCore(this.container, id);

        private Task<CosmosQueryResponse<CosmosConflictSettings>> ContainerFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Debug.Assert(state == null);

            return ExecUtils.ProcessResourceOperationAsync<CosmosQueryResponse<CosmosConflictSettings>>(
                this.container.Database.Client,
                this.container.LinkUri,
                Documents.ResourceType.Conflict,
                Documents.OperationType.ReadFeed,
                options,
                request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                response => this.client.ResponseFactory.CreateResultSetQueryResponse<CosmosConflictSettings>(response),
                cancellationToken);
        }
    }
}
