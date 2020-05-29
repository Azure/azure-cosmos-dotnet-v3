//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;

    internal sealed class QueryIterator : FeedIteratorBase
    {
        private readonly CosmosQueryContextCore cosmosQueryContext;
        private readonly CosmosQueryExecutionContext cosmosQueryExecutionContext;
        private readonly CosmosSerializationFormatOptions cosmosSerializationFormatOptions;
        private readonly CosmosClientContext clientContext;

        private QueryIterator(
            CosmosQueryContextCore cosmosQueryContext,
            CosmosQueryExecutionContext cosmosQueryExecutionContext,
            CosmosSerializationFormatOptions cosmosSerializationFormatOptions,
            QueryRequestOptions requestOptions,
            CosmosClientContext clientContext)
        {
            this.cosmosQueryContext = cosmosQueryContext ?? throw new ArgumentNullException(nameof(cosmosQueryContext));
            this.cosmosQueryExecutionContext = cosmosQueryExecutionContext ?? throw new ArgumentNullException(nameof(cosmosQueryExecutionContext));
            this.cosmosSerializationFormatOptions = cosmosSerializationFormatOptions;
            this.RequestOptions = requestOptions;
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
        }

        public static QueryIterator Create(
            CosmosQueryClient client,
            CosmosClientContext clientContext,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            FeedRangeInternal feedRangeInternal,
            QueryRequestOptions queryRequestOptions,
            Uri resourceLink,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool forcePassthrough,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            if (queryRequestOptions == null)
            {
                queryRequestOptions = new QueryRequestOptions();
            }

            CosmosDiagnosticsContext queryPipelineCreationDiagnostics = clientContext.CreateDiagnosticContext(
                nameof(QueryIterator),
                queryRequestOptions);

            CosmosQueryContextCore cosmosQueryContext = new CosmosQueryContextCore(
                client: client,
                clientContext: clientContext,
                queryRequestOptions: queryRequestOptions,
                resourceTypeEnum: Documents.ResourceType.Document,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                resourceLink: resourceLink,
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                diagnosticsContext: queryPipelineCreationDiagnostics,
                correlatedActivityId: Guid.NewGuid());

            CosmosElement requestContinuationToken;
            switch (queryRequestOptions.ExecutionEnvironment.GetValueOrDefault(ExecutionEnvironment.Client))
            {
                case ExecutionEnvironment.Client:
                    if (continuationToken != null)
                    {
                        TryCatch<CosmosElement> tryParse = CosmosElement.Monadic.Parse(continuationToken);
                        if (tryParse.Failed)
                        {
                            return new QueryIterator(
                                cosmosQueryContext,
                                new QueryExecutionContextWithException(
                                    new MalformedContinuationTokenException(
                                        message: $"Malformed Continuation Token: {continuationToken}",
                                        innerException: tryParse.Exception)),
                                queryRequestOptions.CosmosSerializationFormatOptions,
                                queryRequestOptions,
                                clientContext);
                        }

                        requestContinuationToken = tryParse.Result;
                    }
                    else
                    {
                        requestContinuationToken = null;
                    }
                    break;

                case ExecutionEnvironment.Compute:
                    requestContinuationToken = queryRequestOptions.CosmosElementContinuationToken;
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(ExecutionEnvironment)}: {queryRequestOptions.ExecutionEnvironment.Value}.");
            }

            CosmosQueryExecutionContextFactory.InputParameters inputParameters = new CosmosQueryExecutionContextFactory.InputParameters(
                sqlQuerySpec: sqlQuerySpec,
                initialUserContinuationToken: requestContinuationToken,
                initialFeedRange: feedRangeInternal,
                maxConcurrency: queryRequestOptions.MaxConcurrency,
                maxItemCount: queryRequestOptions.MaxItemCount,
                maxBufferedItemCount: queryRequestOptions.MaxBufferedItemCount,
                partitionKey: queryRequestOptions.PartitionKey,
                properties: queryRequestOptions.Properties,
                partitionedQueryExecutionInfo: partitionedQueryExecutionInfo,
                executionEnvironment: queryRequestOptions.ExecutionEnvironment,
                returnResultsInDeterministicOrder: queryRequestOptions.ReturnResultsInDeterministicOrder,
                forcePassthrough: forcePassthrough,
                testInjections: queryRequestOptions.TestSettings);

            return new QueryIterator(
                cosmosQueryContext,
                CosmosQueryExecutionContextFactory.Create(cosmosQueryContext, inputParameters),
                queryRequestOptions.CosmosSerializationFormatOptions,
                queryRequestOptions,
                clientContext);
        }

        public override bool HasMoreResults => !this.cosmosQueryExecutionContext.IsDone;

        internal override RequestOptions RequestOptions { get; }

        internal async Task<ResponseMessage> ReadNextAsync(
            CancellationToken cancellationToken = default)
        {
            using (CosmosDiagnosticsContext diagnostics = this.clientContext.CreateDiagnosticContext(
                nameof(QueryIterator),
                this.RequestOptions))
            {
                return await this.ReadNextAsync(
                    diagnostics,
                    cancellationToken);
            }
        }

        internal override async Task<ResponseMessage> ReadNextAsync(
            CosmosDiagnosticsContext diagnostics,
            CancellationToken cancellationToken)
        {
            // This catches exception thrown by the pipeline and converts it to QueryResponse
            QueryResponseCore responseCore = await this.cosmosQueryExecutionContext.ExecuteNextAsync(cancellationToken);

            // This swaps the diagnostics in the context. This shows all the page reads between the previous ReadNextAsync and the current ReadNextAsync
            diagnostics.AddDiagnosticsInternal(this.cosmosQueryContext.GetAndResetDiagnostics());

            if (responseCore.IsSuccess)
            {
                return QueryResponse.CreateSuccess(
                    result: responseCore.CosmosElements,
                    count: responseCore.CosmosElements.Count,
                    responseLengthBytes: responseCore.ResponseLengthBytes,
                    diagnostics: diagnostics,
                    serializationOptions: this.cosmosSerializationFormatOptions,
                    responseHeaders: new CosmosQueryResponseMessageHeaders(
                        responseCore.ContinuationToken,
                        responseCore.DisallowContinuationTokenMessage,
                        this.cosmosQueryContext.ResourceTypeEnum,
                        this.cosmosQueryContext.ContainerResourceId)
                    {
                        RequestCharge = responseCore.RequestCharge,
                        ActivityId = responseCore.ActivityId,
                        SubStatusCode = responseCore.SubStatusCode ?? Documents.SubStatusCodes.Unknown
                    });
            }

            if (responseCore.CosmosException != null)
            {
                return responseCore.CosmosException.ToCosmosResponseMessage(null);
            }

            return QueryResponse.CreateFailure(
                statusCode: responseCore.StatusCode,
                cosmosException: responseCore.CosmosException,
                requestMessage: null,
                diagnostics: diagnostics,
                responseHeaders: new CosmosQueryResponseMessageHeaders(
                    responseCore.ContinuationToken,
                    responseCore.DisallowContinuationTokenMessage,
                    this.cosmosQueryContext.ResourceTypeEnum,
                    this.cosmosQueryContext.ContainerResourceId)
                {
                    RequestCharge = responseCore.RequestCharge,
                    ActivityId = responseCore.ActivityId,
                    SubStatusCode = responseCore.SubStatusCode ?? Documents.SubStatusCodes.Unknown,
                });
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            return this.cosmosQueryExecutionContext.GetCosmosElementContinuationToken();
        }
    }
}