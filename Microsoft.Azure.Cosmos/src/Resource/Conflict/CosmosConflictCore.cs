//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal class CosmosConflictCore : CosmosConflict
    {
        private readonly CosmosContainerCore container;
        private readonly CosmosClientContext clientContext;

        protected internal CosmosConflictCore(
            CosmosClientContext clientContext,
            CosmosContainerCore container,
            string conflictId)
        {
            this.clientContext = clientContext;
            this.container = container;
            this.Id = conflictId;
            this.LinkUri = clientContext.CreateLink(
                 parentLink: container.LinkUri.OriginalString,
                 uriPathSegment: Paths.StoredProceduresPathSegment,
                 id: conflictId);
        }

        public override string Id { get; }

        internal Uri LinkUri { get; }

        public override Task<CosmosConflictResponse> DeleteAsync(object partitionKey, CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.LinkUri,
                resourceType: Documents.ResourceType.Conflict,
                operationType: Documents.OperationType.Delete,
                requestOptions: null,
                cosmosContainerCore: this.container,
                partitionKey: partitionKey,
                streamPayload: null,
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateConflictResponse(this, response);
        }
    }
}