//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal class QueryIterator : FeedIterator
    {
        private readonly CosmosQueryContext cosmosQueryContext;
        private readonly CosmosQueryExecutionContext cosmosQueryExecutionContext;
        private readonly CosmosSerializationFormatOptions cosmosSerializationFormatOptions;

        private QueryIterator(
            CosmosQueryContext cosmosQueryContext,
            CosmosQueryExecutionContext cosmosQueryExecutionContext,
            CosmosSerializationFormatOptions cosmosSerializationFormatOptions)
        {
            if (cosmosQueryContext == null)
            {
                throw new ArgumentNullException(nameof(cosmosQueryContext));
            }

            if (cosmosQueryExecutionContext == null)
            {
                throw new ArgumentNullException(nameof(cosmosQueryExecutionContext));
            }

            this.cosmosQueryContext = cosmosQueryContext;
            this.cosmosQueryExecutionContext = cosmosQueryExecutionContext;
            this.cosmosSerializationFormatOptions = cosmosSerializationFormatOptions;
        }

        public static QueryIterator Create(
            CosmosQueryClient client,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
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

            CosmosQueryExecutionContextFactory.InputParameters inputParameters = new CosmosQueryExecutionContextFactory.InputParameters(
                sqlQuerySpec: sqlQuerySpec,
                initialUserContinuationToken: continuationToken,
                maxConcurrency: queryRequestOptions.MaxConcurrency,
                maxItemCount: queryRequestOptions.MaxItemCount,
                maxBufferedItemCount: queryRequestOptions.MaxBufferedItemCount,
                responseContinuationTokenLimitInKb: queryRequestOptions.ResponseContinuationTokenLimitInKb,
                partitionKey: queryRequestOptions.PartitionKey,
                properties: queryRequestOptions.Properties,
                partitionedQueryExecutionInfo: partitionedQueryExecutionInfo,
                executionEnvironment: queryRequestOptions.ExecutionEnvironment);

            CosmosQueryExecutionContextWithNameCacheStaleRetry cosmosQueryExecutionContextWithNameCacheStaleRetry = new CosmosQueryExecutionContextWithNameCacheStaleRetry(
                cosmosQueryContext: cosmosQueryContext,
                cosmosQueryExecutionContextFactory: () =>
                {
                    // Query Iterator requires that the creation of the query context is defered until the user calls ReadNextAsync
                    AsyncLazy<TryCatch<CosmosQueryExecutionContext>> lazyTryCreateCosmosQueryExecutionContext = new AsyncLazy<TryCatch<CosmosQueryExecutionContext>>(valueFactory: (cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return CosmosQueryExecutionContextFactory.TryCreateAsync(cosmosQueryContext, inputParameters, cancellationToken);
                    });
                    LazyCosmosQueryExecutionContext lazyCosmosQueryExecutionContext = new LazyCosmosQueryExecutionContext(lazyTryCreateCosmosQueryExecutionContext);
                    return lazyCosmosQueryExecutionContext;
                });

            return new QueryIterator(
                cosmosQueryContext,
                cosmosQueryExecutionContextWithNameCacheStaleRetry,
                queryRequestOptions.CosmosSerializationFormatOptions);
        }

        public override bool HasMoreResults => !this.cosmosQueryExecutionContext.IsDone;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // This catches exception thrown by the pipeline and converts it to QueryResponse
            ResponseMessage response;
            try
            {
                QueryResponseCore responseCore = await this.cosmosQueryExecutionContext.ExecuteNextAsync(cancellationToken);
                CosmosQueryContext cosmosQueryContext = this.cosmosQueryContext;
                QueryAggregateDiagnostics diagnostics = new QueryAggregateDiagnostics(responseCore.Diagnostics);
                QueryResponse queryResponse;
                if (responseCore.IsSuccess)
                {
                    queryResponse = QueryResponse.CreateSuccess(
                        result: responseCore.CosmosElements,
                        count: responseCore.CosmosElements.Count,
                        responseLengthBytes: responseCore.ResponseLengthBytes,
                        diagnostics: diagnostics,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            responseCore.ContinuationToken,
                            responseCore.DisallowContinuationTokenMessage,
                            cosmosQueryContext.ResourceTypeEnum,
                            cosmosQueryContext.ContainerResourceId)
                        {
                            RequestCharge = responseCore.RequestCharge,
                            ActivityId = responseCore.ActivityId
                        });
                }
                else
                {
                    queryResponse = QueryResponse.CreateFailure(
                        statusCode: responseCore.StatusCode,
                        error: null,
                        errorMessage: responseCore.ErrorMessage,
                        requestMessage: null,
                        diagnostics: diagnostics,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            responseCore.ContinuationToken,
                            responseCore.DisallowContinuationTokenMessage,
                            cosmosQueryContext.ResourceTypeEnum,
                            cosmosQueryContext.ContainerResourceId)
                        {
                            RequestCharge = responseCore.RequestCharge,
                            ActivityId = responseCore.ActivityId
                        });
                }

                queryResponse.CosmosSerializationOptions = this.cosmosSerializationFormatOptions;

                response = queryResponse;
            }
            catch (Documents.DocumentClientException exception)
            {
                response = exception.ToCosmosResponseMessage(request: null);
            }
            catch (CosmosException exception)
            {
                response = exception.ToCosmosResponseMessage(request: null);
            }
            catch (AggregateException ae)
            {
                response = TransportHandler.AggregateExceptionConverter(ae, null);
                if (response == null)
                {
                    throw;
                }
            }

            return response;
        }

        internal bool TryGetContinuationToken(out string state)
        {
            return this.cosmosQueryExecutionContext.TryGetContinuationToken(out state);
        }
    }
}
