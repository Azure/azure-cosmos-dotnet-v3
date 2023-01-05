﻿//------------------------------------------------------------
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
            ContainerInternal container)
        {
            this.cosmosQueryContext = cosmosQueryContext ?? throw new ArgumentNullException(nameof(cosmosQueryContext));
            this.queryPipelineStage = cosmosQueryExecutionContext ?? throw new ArgumentNullException(nameof(cosmosQueryExecutionContext));
            this.cosmosSerializationFormatOptions = cosmosSerializationFormatOptions;
            this.requestOptions = requestOptions;
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.hasMoreResults = true;
            this.correlatedActivityId = correlatedActivityId;

            this.container = container;
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

            Guid correlatedActivityId = Guid.NewGuid();
            CosmosQueryContextCore cosmosQueryContext = new CosmosQueryContextCore(
                client: client,
                resourceTypeEnum: Documents.ResourceType.Document,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                resourceLink: resourceLink,
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                useSystemPrefix: QueryIterator.IsSystemPrefixExpected(queryRequestOptions),
                correlatedActivityId: correlatedActivityId);

            NetworkAttachedDocumentContainer networkAttachedDocumentContainer = new NetworkAttachedDocumentContainer(
                containerCore,
                client,
                correlatedActivityId,
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
                                clientContext,
                                correlatedActivityId,
                                containerCore);
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
                clientContext,
                correlatedActivityId,
                containerCore);
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
                    responseLengthBytes: tryGetQueryPage.Result.ResponseLengthInBytes,
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

        public override CosmosElement GetCosmosElementContinuationToken() => this.queryPipelineStage.Current.Result.State?.Value;

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