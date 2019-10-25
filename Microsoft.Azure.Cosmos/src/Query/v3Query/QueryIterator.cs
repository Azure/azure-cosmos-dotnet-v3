//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;

    internal class QueryIterator : FeedIterator
    {
        private readonly CosmosQueryExecutionContextFactory cosmosQueryExecutionContext;
        private readonly CosmosSerializationFormatOptions cosmosSerializationFormatOptions;

        private QueryIterator(
            CosmosQueryExecutionContextFactory cosmosQueryExecutionContext,
            CosmosSerializationFormatOptions cosmosSerializationFormatOptions)
        {
            if (cosmosQueryExecutionContext == null)
            {
                throw new ArgumentNullException(nameof(cosmosQueryExecutionContext));
            }

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

            CosmosQueryContext context = new CosmosQueryContextCore(
                client: client,
                queryRequestOptions: queryRequestOptions,
                resourceTypeEnum: Documents.ResourceType.Document,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                resourceLink: resourceLink,
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                correlatedActivityId: Guid.NewGuid());

            CosmosQueryExecutionContextFactory.InputParameters inputParams = new CosmosQueryExecutionContextFactory.InputParameters()
            {
                SqlQuerySpec = sqlQuerySpec,
                InitialUserContinuationToken = continuationToken,
                MaxBufferedItemCount = queryRequestOptions.MaxBufferedItemCount,
                MaxConcurrency = queryRequestOptions.MaxConcurrency,
                MaxItemCount = queryRequestOptions.MaxItemCount,
                PartitionKey = queryRequestOptions.PartitionKey,
                Properties = queryRequestOptions.Properties,
                PartitionedQueryExecutionInfo = partitionedQueryExecutionInfo
            };

            return new QueryIterator(
                new CosmosQueryExecutionContextFactory(
                    cosmosQueryContext: context,
                    inputParameters: inputParams),
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
                CosmosQueryContext cosmosQueryContext = this.cosmosQueryExecutionContext.CosmosQueryContext;
                QueryAggregateDiagnostics diagnostics = new QueryAggregateDiagnostics(responseCore.diagnostics);
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

        internal override bool TryGetContinuationToken(out string state)
        {
            return this.cosmosQueryExecutionContext.TryGetContinuationToken(out state);
        }
    }
}
