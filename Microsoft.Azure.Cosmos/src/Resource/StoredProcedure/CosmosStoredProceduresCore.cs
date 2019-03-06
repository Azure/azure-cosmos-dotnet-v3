//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;

    internal sealed class CosmosStoredProceduresImpl : CosmosStoredProcedures
    {
        private readonly CosmosContainer container;
        private readonly CosmosClient client;

        internal CosmosStoredProceduresImpl(CosmosContainer container)
        {
            this.container = container;
            this.client = container.Client;
        }

        public override Task<CosmosStoredProcedureResponse> CreateStoredProcedureAsync(
                    string id,
                    string body,
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(body))
            {
                throw new ArgumentNullException(nameof(body));
            }

            CosmosStoredProcedureSettings storedProcedureSettings = new CosmosStoredProcedureSettings();
            storedProcedureSettings.Id = id;
            storedProcedureSettings.Body = body;

            Task<CosmosResponseMessage> response = ExecUtils.ProcessResourceOperationStreamAsync(
                this.container.Database.Client,
                this.container.LinkUri,
                ResourceType.StoredProcedure,
                OperationType.Create,
                requestOptions,
                partitionKey: null,
                streamPayload: storedProcedureSettings.GetResourceStream(),
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return this.client.ResponseFactory.CreateStoredProcedureResponse(this[id], response);
        }

        public override CosmosResultSetIterator<CosmosStoredProcedureSettings> GetStoredProcedureIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<CosmosStoredProcedureSettings>(
                maxItemCount, 
                continuationToken, 
                null, 
                this.StoredProcedureFeedRequestExecutor);
        }

        public override CosmosStoredProcedure this[string id]
        {
            get
            {
                return new CosmosStoredProcedureImpl(this.container, id);
            }
        }

        private Task<CosmosQueryResponse<CosmosStoredProcedureSettings>> StoredProcedureFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            return ExecUtils.ProcessResourceOperationAsync<CosmosQueryResponse<CosmosStoredProcedureSettings>>(
                this.container.Database.Client,
                resourceUri,
                ResourceType.StoredProcedure,
                OperationType.ReadFeed,
                options,
                request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                response => this.client.ResponseFactory.CreateResultSetQueryResponse<CosmosStoredProcedureSettings>(response),
                cancellationToken);
        }
    }
}
