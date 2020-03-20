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
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;

    internal sealed class QueryIterator : FeedIteratorInternal
    {
        private readonly CosmosQueryContext cosmosQueryContext;
        private readonly CosmosQueryExecutionContext cosmosQueryExecutionContext;
        private readonly CosmosSerializationFormatOptions cosmosSerializationFormatOptions;
        private readonly RequestOptions requestOptions;
        private readonly SqlQuerySpec initialSqlQuerySpec;

        private QueryIterator(
            CosmosQueryContext cosmosQueryContext,
            CosmosQueryExecutionContext cosmosQueryExecutionContext,
            CosmosSerializationFormatOptions cosmosSerializationFormatOptions,
            RequestOptions requestOptions,
            SqlQuerySpec initialSqlQuerySpec)
        {
            this.cosmosQueryContext = cosmosQueryContext ?? throw new ArgumentNullException(nameof(cosmosQueryContext));
            this.cosmosQueryExecutionContext = cosmosQueryExecutionContext ?? throw new ArgumentNullException(nameof(cosmosQueryExecutionContext));
            this.cosmosSerializationFormatOptions = cosmosSerializationFormatOptions;
            this.requestOptions = requestOptions;
            this.initialSqlQuerySpec = initialSqlQuerySpec;
        }

        public static QueryIterator Create(
            CosmosQueryClient client,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            IQueryFeedToken queryFeedToken,
            QueryRequestOptions queryRequestOptions,
            Uri resourceLink,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            if (queryRequestOptions == null)
            {
                queryRequestOptions = new QueryRequestOptions();
            }

            CosmosQueryContext cosmosQueryContext = new CosmosQueryContextCore(
                client: client,
                queryRequestOptions: queryRequestOptions,
                resourceTypeEnum: Documents.ResourceType.Document,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                resourceLink: resourceLink,
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                correlatedActivityId: Guid.NewGuid());

            CosmosElement requestContinuationToken;
            switch (queryRequestOptions.ExecutionEnvironment.GetValueOrDefault(ExecutionEnvironment.Client))
            {
                case ExecutionEnvironment.Client:
                    if (continuationToken != null)
                    {
                        if (!CosmosElement.TryParse(continuationToken, out requestContinuationToken))
                        {
                            return new QueryIterator(
                                cosmosQueryContext,
                                new QueryExecutionContextWithException(
                                    new MalformedContinuationTokenException(
                                        $"Malformed Continuation Token: {requestContinuationToken}")),
                                queryRequestOptions.CosmosSerializationFormatOptions,
                                queryRequestOptions,
                                sqlQuerySpec);
                        }
                    }
                    else if (queryFeedToken != null
                        && queryFeedToken.GetContinuation() != null)
                    {
                        if (!CosmosElement.TryParse(queryFeedToken.GetContinuation(), out requestContinuationToken))
                        {
                            return new QueryIterator(
                                cosmosQueryContext,
                                new QueryExecutionContextWithException(
                                    new MalformedContinuationTokenException(
                                        $"Malformed Continuation Token: {queryFeedToken.GetContinuation()}")),
                                queryRequestOptions.CosmosSerializationFormatOptions,
                                queryRequestOptions,
                                sqlQuerySpec);
                        }
                    }
                    else
                    {
                        requestContinuationToken = null;
                    }
                    break;

                case ExecutionEnvironment.Compute:
                    requestContinuationToken = queryRequestOptions.CosmosElementContinuationToken;
                    if (requestContinuationToken == null
                        && queryFeedToken != null
                        && queryFeedToken.GetContinuation() != null)
                    {
                        if (!CosmosElement.TryParse(queryFeedToken.GetContinuation(), out requestContinuationToken))
                        {
                            return new QueryIterator(
                                cosmosQueryContext,
                                new QueryExecutionContextWithException(
                                    new MalformedContinuationTokenException(
                                        $"Malformed Continuation Token: {queryFeedToken.GetContinuation()}")),
                                queryRequestOptions.CosmosSerializationFormatOptions,
                                queryRequestOptions,
                                sqlQuerySpec);
                        }
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(ExecutionEnvironment)}: {queryRequestOptions.ExecutionEnvironment.Value}.");
            }

            CosmosQueryExecutionContextFactory.InputParameters inputParameters = new CosmosQueryExecutionContextFactory.InputParameters(
                sqlQuerySpec: sqlQuerySpec,
                initialUserContinuationToken: requestContinuationToken,
                initialFeedToken: queryFeedToken,
                maxConcurrency: queryRequestOptions.MaxConcurrency,
                maxItemCount: queryRequestOptions.MaxItemCount,
                maxBufferedItemCount: queryRequestOptions.MaxBufferedItemCount,
                partitionKey: queryRequestOptions.PartitionKey,
                properties: queryRequestOptions.Properties,
                partitionedQueryExecutionInfo: partitionedQueryExecutionInfo,
                executionEnvironment: queryRequestOptions.ExecutionEnvironment,
                returnResultsInDeterministicOrder: queryRequestOptions.ReturnResultsInDeterministicOrder,
                testInjections: queryRequestOptions.TestSettings);

            return new QueryIterator(
                cosmosQueryContext,
                CosmosQueryExecutionContextFactory.Create(cosmosQueryContext, inputParameters),
                queryRequestOptions.CosmosSerializationFormatOptions,
                queryRequestOptions,
                inputParameters.SqlQuerySpec);
        }

        public override bool HasMoreResults => !this.cosmosQueryExecutionContext.IsDone;

#if PREVIEW
        public override
#else
        internal
#endif
        QueryFeedToken FeedToken
        {
            get
            {
                if (this.cosmosQueryExecutionContext.TryGetFeedToken(
                    this.cosmosQueryContext.ContainerResourceId,
                    this.initialSqlQuerySpec,
                    out QueryFeedToken feedToken))
                {
                    return feedToken;
                }

                return null;
            }
        }

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnostics = CosmosDiagnosticsContext.Create(this.requestOptions);
            using (diagnostics.CreateOverallScope("QueryReadNextAsync"))
            {
                // This catches exception thrown by the pipeline and converts it to QueryResponse
                QueryResponseCore responseCore = await this.cosmosQueryExecutionContext.ExecuteNextAsync(cancellationToken);
                CosmosQueryContext cosmosQueryContext = this.cosmosQueryContext;

                foreach (QueryPageDiagnostics queryPage in responseCore.Diagnostics)
                {
                    diagnostics.AddDiagnosticsInternal(queryPage);
                }

                QueryResponse queryResponse;
                if (responseCore.IsSuccess)
                {
                    queryResponse = QueryResponse.CreateSuccess(
                        result: responseCore.CosmosElements,
                        count: responseCore.CosmosElements.Count,
                        responseLengthBytes: responseCore.ResponseLengthBytes,
                        diagnostics: diagnostics,
                        serializationOptions: this.cosmosSerializationFormatOptions,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            responseCore.ContinuationToken,
                            responseCore.DisallowContinuationTokenMessage,
                            cosmosQueryContext.ResourceTypeEnum,
                            cosmosQueryContext.ContainerResourceId)
                        {
                            RequestCharge = responseCore.RequestCharge,
                            ActivityId = responseCore.ActivityId,
                            SubStatusCode = responseCore.SubStatusCode ?? Documents.SubStatusCodes.Unknown
                        });
                }
                else
                {
                    queryResponse = QueryResponse.CreateFailure(
                        statusCode: responseCore.StatusCode,
                        cosmosException: responseCore.CosmosException,
                        requestMessage: null,
                        diagnostics: diagnostics,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            responseCore.ContinuationToken,
                            responseCore.DisallowContinuationTokenMessage,
                            cosmosQueryContext.ResourceTypeEnum,
                            cosmosQueryContext.ContainerResourceId)
                        {
                            RequestCharge = responseCore.RequestCharge,
                            ActivityId = responseCore.ActivityId,
                            SubStatusCode = responseCore.SubStatusCode ?? Documents.SubStatusCodes.Unknown
                        });
                }

                return queryResponse;
            }
        }

        public override CosmosElement GetCosmsoElementContinuationToken()
        {
            return this.cosmosQueryExecutionContext.GetCosmosElementContinuationToken();
        }
    }
}
