//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class QueryIterator : FeedIteratorInternal
    {
        private static readonly string CorrelatedActivityIdKeyName = "Query Correlated ActivityId";
        private static readonly IReadOnlyList<CosmosElement> EmptyPage = new List<CosmosElement>();

        private readonly CosmosQueryContextCore cosmosQueryContext;
        private readonly IQueryPipelineStage queryPipelineStage;
        private readonly CosmosSerializationFormatOptions cosmosSerializationFormatOptions;
        private readonly RequestOptions requestOptions;
        private readonly CosmosClientContext clientContext;
        private readonly Guid correlatedActivityId;

        private bool hasMoreResults;

        private QueryIterator(
            CosmosQueryContextCore cosmosQueryContext,
            IQueryPipelineStage cosmosQueryExecutionContext,
            CosmosSerializationFormatOptions cosmosSerializationFormatOptions,
            RequestOptions requestOptions,
            CosmosClientContext clientContext,
            Guid correlatedActivityId,
            ContainerInternal container,
            SqlQuerySpec sqlQuerySpec)
        {
            this.cosmosQueryContext = cosmosQueryContext ?? throw new ArgumentNullException(nameof(cosmosQueryContext));
            this.queryPipelineStage = cosmosQueryExecutionContext ?? throw new ArgumentNullException(nameof(cosmosQueryExecutionContext));
            this.cosmosSerializationFormatOptions = cosmosSerializationFormatOptions;
            this.requestOptions = requestOptions;
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.hasMoreResults = true;
            this.correlatedActivityId = correlatedActivityId;

            this.container = container;

            this.SetupInfoForTelemetry(
                databaseName: container?.Database?.Id,
                operationName: OpenTelemetryConstants.Operations.QueryItems,
                operationType: Documents.OperationType.Query,
                querySpec: sqlQuerySpec,
                operationMetricsOptions: requestOptions?.OperationMetricsOptions,
                networkMetricOptions: requestOptions?.NetworkMetricsOptions);
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
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            Documents.ResourceType resourceType)
        {
            if (queryRequestOptions == null)
            {
                queryRequestOptions = new QueryRequestOptions();
            }

            Guid correlatedActivityId = Guid.NewGuid();
            CosmosQueryContextCore cosmosQueryContext = new CosmosQueryContextCore(
                client: client,
                resourceTypeEnum: resourceType,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                resourceLink: resourceLink,
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                useSystemPrefix: QueryIterator.IsSystemPrefixExpected(queryRequestOptions),
                correlatedActivityId: correlatedActivityId);

            ICosmosDistributedQueryClient distributedQueryClient = new CosmosDistributedQueryClient(
                clientContext,
                resourceLink,
                correlatedActivityId);

            NetworkAttachedDocumentContainer networkAttachedDocumentContainer = new NetworkAttachedDocumentContainer(
                containerCore,
                client,
                distributedQueryClient,
                correlatedActivityId,
                queryRequestOptions,
                resourceType: resourceType);
            DocumentContainer documentContainer = new DocumentContainer(networkAttachedDocumentContainer);

            CosmosElement requestContinuationToken;
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
                        clientContext,
                        correlatedActivityId,
                        containerCore,
                        sqlQuerySpec);
                }

                requestContinuationToken = tryParse.Result;
            }
            else
            {
                requestContinuationToken = null;
            }

            CosmosQueryExecutionContextFactory.InputParameters inputParameters = CosmosQueryExecutionContextFactory.InputParameters.Create(
                sqlQuerySpec: sqlQuerySpec,
                initialUserContinuationToken: requestContinuationToken,
                initialFeedRange: feedRangeInternal,
                maxConcurrency: queryRequestOptions.MaxConcurrency,
                maxItemCount: queryRequestOptions.MaxItemCount,
                maxBufferedItemCount: queryRequestOptions.MaxBufferedItemCount,
                partitionKey: queryRequestOptions.PartitionKey,
                properties: queryRequestOptions.Properties,
                partitionedQueryExecutionInfo: partitionedQueryExecutionInfo,
                returnResultsInDeterministicOrder: queryRequestOptions.ReturnResultsInDeterministicOrder,
                enableOptimisticDirectExecution: queryRequestOptions.EnableOptimisticDirectExecution,
                isNonStreamingOrderByQueryFeatureDisabled: queryRequestOptions.IsNonStreamingOrderByQueryFeatureDisabled,
                enableDistributedQueryGatewayMode: queryRequestOptions.EnableDistributedQueryGatewayMode,
                testInjections: queryRequestOptions.TestSettings);

            return new QueryIterator(
                cosmosQueryContext,
                CosmosQueryExecutionContextFactory.Create(documentContainer, cosmosQueryContext, inputParameters, NoOpTrace.Singleton),
                queryRequestOptions.CosmosSerializationFormatOptions,
                queryRequestOptions,
                clientContext,
                correlatedActivityId,
                containerCore,
                sqlQuerySpec);
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

            // If Correlated Id already exists and is different, add a new one in comma separated list
            // Scenario: A new iterator is created with same ContinuationToken and Trace 
            if (trace.Data.TryGetValue(QueryIterator.CorrelatedActivityIdKeyName, out object correlatedActivityIds))
            {
                List<string> correlatedIdList = correlatedActivityIds.ToString().Split(',').ToList();
                if (!correlatedIdList.Contains(this.correlatedActivityId.ToString()))
                {
                    correlatedIdList.Add(this.correlatedActivityId.ToString());
                    trace.AddOrUpdateDatum(QueryIterator.CorrelatedActivityIdKeyName,
                                            string.Join(",", correlatedIdList));
                }
            }
            else
            {
                trace.AddDatum(QueryIterator.CorrelatedActivityIdKeyName, this.correlatedActivityId.ToString());
            }

            TryCatch<QueryPage> tryGetQueryPage;
            try
            {
                // This catches exception thrown by the pipeline and converts it to QueryResponse
                if (!await this.queryPipelineStage.MoveNextAsync(trace, cancellationToken))
                {
                    this.hasMoreResults = false;
                    return QueryResponse.CreateSuccess(
                        result: EmptyPage,
                        count: EmptyPage.Count,
                        serializationOptions: this.cosmosSerializationFormatOptions,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            continauationToken: default,
                            disallowContinuationTokenMessage: default,
                            this.cosmosQueryContext.ResourceTypeEnum,
                            this.cosmosQueryContext.ContainerResourceId)
                        {
                            RequestCharge = default,
                            ActivityId = this.correlatedActivityId.ToString(),
                            SubStatusCode = Documents.SubStatusCodes.Unknown
                        },
                        trace: trace);
                }

                tryGetQueryPage = this.queryPipelineStage.Current;
            }
            catch (OperationCanceledException ex) when (!(ex is CosmosOperationCanceledException))
            {
                throw new CosmosOperationCanceledException(ex, trace);
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
                    CorrelatedActivityId = this.correlatedActivityId.ToString(),
                    SubStatusCode = Documents.SubStatusCodes.Unknown
                };

                foreach (KeyValuePair<string, string> kvp in tryGetQueryPage.Result.AdditionalHeaders)
                {
                    headers[kvp.Key] = kvp.Value;
                }

                return QueryResponse.CreateSuccess(
                    result: tryGetQueryPage.Result.Documents,
                    count: tryGetQueryPage.Result.Documents.Count,
                    serializationOptions: this.cosmosSerializationFormatOptions,
                    responseHeaders: headers,
                    trace: trace);
            }

            if (!ExceptionToCosmosException.TryCreateFromException(
                tryGetQueryPage.Exception, 
                trace,
                out CosmosException cosmosException))
            {
                throw tryGetQueryPage.Exception;
            }

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

        protected override void Dispose(bool disposing)
        {
            this.queryPipelineStage.DisposeAsync();
            base.Dispose(disposing);
        }

        internal static bool IsSystemPrefixExpected(QueryRequestOptions queryRequestOptions)
        {
            if (queryRequestOptions == null || queryRequestOptions.Properties == null)
            {
                return false;
            }

            if (queryRequestOptions.Properties.TryGetValue("x-ms-query-disableSystemPrefix", out object objDisableSystemPrefix) &&
                bool.TryParse(objDisableSystemPrefix.ToString(), out bool disableSystemPrefix))
            {
                return !disableSystemPrefix;
            }

            return false;
        }
    }
}