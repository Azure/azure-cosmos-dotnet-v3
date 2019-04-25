//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    internal class CosmosConflictCore : CosmosConflict
    {
        private readonly CosmosContainerCore container;

        protected internal CosmosConflictCore(
            CosmosContainerCore container,
            string conflictId)
        {
            this.Id = conflictId;
            this.container = container;
            base.Initialize(
               client: container.Client,
               parentLink: container.LinkUri.OriginalString,
               uriPathSegment: Documents.Paths.ConflictsPathSegment);
        }

        public override string Id { get; }

        public override Task<CosmosConflictResponse> DeleteAsync(object partitionKey, CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = ExecUtils.ProcessResourceOperationStreamAsync(
                client: this.Client,
                resourceUri: this.LinkUri,
                resourceType: Documents.ResourceType.Conflict,
                operationType: Documents.OperationType.Delete,
                requestOptions: null,
                cosmosContainerCore: this.container,
                partitionKey: partitionKey,
                streamPayload: null,
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return this.Client.ResponseFactory.CreateConflictResponse(this, response);
        }
    }
}