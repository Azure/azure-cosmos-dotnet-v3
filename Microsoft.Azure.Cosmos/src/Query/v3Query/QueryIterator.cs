//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Documents;

    internal sealed class QueryIterator : FeedIteratorInternal
    {
        private readonly CosmosQueryContextCore cosmosQueryContext;
        private readonly IQueryPipelineStage queryPipelineStage;
        private readonly CosmosSerializationFormatOptions cosmosSerializationFormatOptions;
        private readonly RequestOptions requestOptions;
        private readonly CosmosClientContext clientContext;

        private bool hasMoreResults;

        private QueryIterator(
            CosmosQueryContextCore cosmosQueryContext,
            IQueryPipelineStage cosmosQueryExecutionContext,
            CosmosSerializationFormatOptions cosmosSerializationFormatOptions,
            RequestOptions requestOptions,
            CosmosClientContext clientContext)
        {
            this.cosmosQueryContext = cosmosQueryContext ?? throw new ArgumentNullException(nameof(cosmosQueryContext));
            this.queryPipelineStage = cosmosQueryExecutionContext ?? throw new ArgumentNullException(nameof(cosmosQueryExecutionContext));
            this.cosmosSerializationFormatOptions = cosmosSerializationFormatOptions;
            this.requestOptions = requestOptions;
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.hasMoreResults = true;
        }

        public static QueryIterator Create(
            ContainerCore containerCore,
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

            CosmosDiagnosticsContext queryPipelineCreationDiagnostics = CosmosDiagnosticsContext.Create(queryRequestOptions);

            CosmosQueryContextCore cosmosQueryContext = new CosmosQueryContextCore(
                client: client,
                queryRequestOptions: queryRequestOptions,
                resourceTypeEnum: Documents.ResourceType.Document,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                resourceLink: resourceLink,
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                diagnosticsContext: queryPipelineCreationDiagnostics,
                correlatedActivityId: Guid.NewGuid());

            DocumentContainer documentContainer = new NetworkAttachedDocumentContainer(containerCore, cosmosQueryContext);

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
                                new FaultedQueryPipelineStage(
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
                CosmosQueryExecutionContextFactory.Create(documentContainer, cosmosQueryContext, inputParameters),
                queryRequestOptions.CosmosSerializationFormatOptions,
                queryRequestOptions,
                clientContext);
        }

        public override bool HasMoreResults => this.hasMoreResults;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnostics = CosmosDiagnosticsContext.Create(this.requestOptions);
            using (diagnostics.GetOverallScope())
            {
                TryCatch<QueryPage> tryGetQueryPage;
                try
                {
                    // This catches exception thrown by the pipeline and converts it to QueryResponse
                    await this.queryPipelineStage.MoveNextAsync();
                    tryGetQueryPage = this.queryPipelineStage.Current;
                }
                catch (OperationCanceledException ex) when (!(ex is CosmosOperationCanceledException))
                {
                    throw new CosmosOperationCanceledException(ex, diagnostics);
                }
                finally
                {
                    // This swaps the diagnostics in the context.
                    // This shows all the page reads between the previous ReadNextAsync and the current ReadNextAsync
                    diagnostics.AddDiagnosticsInternal(this.cosmosQueryContext.GetAndResetDiagnostics());
                }

                if (tryGetQueryPage.Succeeded)
                {
                    return QueryResponse.CreateSuccess(
                        result: tryGetQueryPage.Result.Documents,
                        count: tryGetQueryPage.Result.Documents.Count,
                        responseLengthBytes: tryGetQueryPage.Result.ResponseLengthInBytes,
                        diagnostics: diagnostics,
                        serializationOptions: this.cosmosSerializationFormatOptions,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            (tryGetQueryPage.Result.State?.Value as CosmosString)?.Value,
                            tryGetQueryPage.Result.DisallowContinuationTokenMessage,
                            this.cosmosQueryContext.ResourceTypeEnum,
                            this.cosmosQueryContext.ContainerResourceId)
                        {
                            RequestCharge = tryGetQueryPage.Result.RequestCharge,
                            ActivityId = tryGetQueryPage.Result.ActivityId,
                            SubStatusCode = Documents.SubStatusCodes.Unknown
                        });
                }

                CosmosException cosmosException = ExceptionToCosmosException.CreateFromException(tryGetQueryPage.Exception);
                SubStatusCodes subStatusCode;
                if (Enum.IsDefined(typeof(SubStatusCodes), cosmosException.SubStatusCode))
                {
                    subStatusCode = (SubStatusCodes)cosmosException.SubStatusCode;
                }
                else
                {
                    subStatusCode = Documents.SubStatusCodes.Unknown;
                }

                return QueryResponse.CreateFailure(
                    statusCode: cosmosException.StatusCode,
                    cosmosException: cosmosException,
                    requestMessage: null,
                    diagnostics: diagnostics,
                    responseHeaders: new CosmosQueryResponseMessageHeaders(
                        continauationToken: default,
                        disallowContinuationTokenMessage: default,
                        this.cosmosQueryContext.ResourceTypeEnum,
                        this.cosmosQueryContext.ContainerResourceId)
                    {
                        RequestCharge = cosmosException.RequestCharge,
                        ActivityId = cosmosException.ActivityId,
                        SubStatusCode = subStatusCode,
                    });
            }
        }

        public override CosmosElement GetCosmosElementContinuationToken() => this.queryPipelineStage.Current.Result.State.Value;

        protected override void Dispose(bool disposing)
        {
            this.queryPipelineStage.DisposeAsync();
            base.Dispose(disposing);
        }
    }
}
