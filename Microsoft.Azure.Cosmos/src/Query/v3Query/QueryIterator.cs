//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class QueryIterator : FeedIteratorInternal
    {
        private static readonly IReadOnlyList<CosmosElement> EmptyPage = new List<CosmosElement>();

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
            string resourceLink,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool forcePassthrough,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            if (queryRequestOptions == null)
            {
                queryRequestOptions = new QueryRequestOptions();
            }

            CosmosQueryContextCore cosmosQueryContext = new CosmosQueryContextCore(
                client: client,
                resourceTypeEnum: Documents.ResourceType.Document,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                resourceLink: resourceLink,
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                correlatedActivityId: Guid.NewGuid());

            NetworkAttachedDocumentContainer networkAttachedDocumentContainer = new NetworkAttachedDocumentContainer(
                containerCore,
                client,
                queryRequestOptions);
            DocumentContainer documentContainer = new DocumentContainer(networkAttachedDocumentContainer);

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
                CosmosQueryExecutionContextFactory.Create(documentContainer, cosmosQueryContext, inputParameters, NoOpTrace.Singleton),
                queryRequestOptions.CosmosSerializationFormatOptions,
                queryRequestOptions,
                clientContext);
        }

        public override bool HasMoreResults => this.hasMoreResults;

        public override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            return this.ReadNextAsync(NoOpTrace.Singleton, cancellationToken);
        }

        public override async Task<ResponseMessage> ReadNextAsync(ITrace trace, CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            TryCatch<QueryPage> tryGetQueryPage;
            try
            {
                // This catches exception thrown by the pipeline and converts it to QueryResponse
                this.queryPipelineStage.SetCancellationToken(cancellationToken);
                if (!await this.queryPipelineStage.MoveNextAsync(trace))
                {
                    this.hasMoreResults = false;
                    return QueryResponse.CreateSuccess(
                        result: EmptyPage,
                        count: EmptyPage.Count,
                        responseLengthBytes: default,
                        serializationOptions: this.cosmosSerializationFormatOptions,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            continauationToken: default,
                            disallowContinuationTokenMessage: default,
                            this.cosmosQueryContext.ResourceTypeEnum,
                            this.cosmosQueryContext.ContainerResourceId)
                        {
                            RequestCharge = default,
                            ActivityId = Guid.Empty.ToString(),
                            SubStatusCode = Documents.SubStatusCodes.Unknown
                        },
                        trace: trace);
                }

                tryGetQueryPage = this.queryPipelineStage.Current;
            }
            catch (OperationCanceledException ex) when (!(ex is CosmosOperationCanceledException))
            {
                throw new CosmosOperationCanceledException(ex, new CosmosTraceDiagnostics(trace));
            }

            if (tryGetQueryPage.Succeeded)
            {
                if ((tryGetQueryPage.Result.State == null) && (tryGetQueryPage.Result.DisallowContinuationTokenMessage == null))
                {
                    this.hasMoreResults = false;
                }

                CosmosQueryResponseMessageHeaders headers = new CosmosQueryResponseMessageHeaders(
                    tryGetQueryPage.Result.State?.Value.ToString(),
                    tryGetQueryPage.Result.DisallowContinuationTokenMessage,
                    this.cosmosQueryContext.ResourceTypeEnum,
                    this.cosmosQueryContext.ContainerResourceId)
                {
                    RequestCharge = tryGetQueryPage.Result.RequestCharge,
                    ActivityId = tryGetQueryPage.Result.ActivityId,
                    SubStatusCode = Documents.SubStatusCodes.Unknown
                };

                foreach (KeyValuePair<string, string> kvp in tryGetQueryPage.Result.AdditionalHeaders)
                {
                    headers[kvp.Key] = kvp.Value;
                }

                return QueryResponse.CreateSuccess(
                    result: tryGetQueryPage.Result.Documents,
                    count: tryGetQueryPage.Result.Documents.Count,
                    responseLengthBytes: tryGetQueryPage.Result.ResponseLengthInBytes,
                    serializationOptions: this.cosmosSerializationFormatOptions,
                    responseHeaders: headers,
                    trace: trace);
            }

            CosmosException cosmosException = ExceptionToCosmosException.CreateFromException(tryGetQueryPage.Exception);
            if (!IsRetriableException(cosmosException))
            {
                this.hasMoreResults = false;
            }

            return QueryResponse.CreateFailure(
                statusCode: cosmosException.StatusCode,
                cosmosException: cosmosException,
                requestMessage: null,
                responseHeaders: CosmosQueryResponseMessageHeaders.ConvertToQueryHeaders(
                    cosmosException.Headers,
                    this.cosmosQueryContext.ResourceTypeEnum,
                    this.cosmosQueryContext.ContainerResourceId,
                    cosmosException.SubStatusCode,
                    cosmosException.ActivityId),
                trace: trace);
        }

        public override CosmosElement GetCosmosElementContinuationToken() => this.queryPipelineStage.Current.Result.State?.Value;

        protected override void Dispose(bool disposing)
        {
            this.queryPipelineStage.DisposeAsync();
            base.Dispose(disposing);
        }
    }
}