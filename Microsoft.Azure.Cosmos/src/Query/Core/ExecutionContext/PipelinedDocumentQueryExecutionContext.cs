//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Distinct;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.GroupBy;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.SkipTake;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// You can imagine the pipeline to be a directed acyclic graph where documents flow from multiple sources (the partitions) to a single sink (the client who calls on ExecuteNextAsync()).
    /// The pipeline will consist of individual implementations of <see cref="CosmosQueryExecutionContext"/>. 
    /// Every member of the pipeline has a source of documents (another member of the pipeline or an actual partition),
    /// a method of draining documents (DrainAsync()) from said source, and a flag for whether that member of the pipeline is completely drained.
    /// <para>
    /// The following is a diagram of the pipeline:
    ///     +--------------------------+    +--------------------------+    +--------------------------+
    ///     |                          |    |                          |    |                          |
    ///     | Document Producer Tree 0 |    | Document Producer Tree 1 |    | Document Producer Tree N |
    ///     |                          |    |                          |    |                          |
    ///     +--------------------------+    +--------------------------+    +--------------------------+
    ///                   |                               |                               |           
    ///                    \                              |                              /
    ///                     \                             |                             /
    ///                      +---------------------------------------------------------+
    ///                      |                                                         |
    ///                      |   Parallel / Order By Document Query Execution Context  |
    ///                      |                                                         |
    ///                      +---------------------------------------------------------+
    ///                                                   |
    ///                                                   |
    ///                                                   |
    ///                         +---------------------------------------------------+
    ///                         |                                                   |
    ///                         |    Aggregate Document Query Execution Component   |
    ///                         |                                                   |
    ///                         +---------------------------------------------------+
    ///                                                   |
    ///                                                   |
    ///                                                   |
    ///                             +------------------------------------------+
    ///                             |                                          |
    ///                             |  Top Document Query Execution Component  |
    ///                             |                                          |
    ///                             +------------------------------------------+
    ///                                                   |
    ///                                                   |
    ///                                                   |
    ///                                    +-----------------------------+
    ///                                    |                             |
    ///                                    |            Client           |
    ///                                    |                             |
    ///                                    +-----------------------------+
    /// </para>    
    /// <para>
    /// This class is responsible for constructing the pipelined described.
    /// Note that the pipeline will always have one of <see cref="CosmosOrderByItemQueryExecutionContext"/> or <see cref="CosmosParallelItemQueryExecutionContext"/>,
    /// which both derive from <see cref="CosmosCrossPartitionQueryExecutionContext"/> as these are top level execution contexts.
    /// These top level execution contexts have <see cref="ItemProducerTree"/> that are responsible for hitting the backend
    /// and will optionally feed into <see cref="AggregateDocumentQueryExecutionComponent"/> and <see cref="TakeDocumentQueryExecutionComponent"/>.
    /// How these components are picked is based on <see cref="PartitionedQueryExecutionInfo"/>,
    /// which is a serialized form of this class and serves as a blueprint for construction.
    /// </para>
    /// <para>
    /// Once the pipeline is constructed the client(sink of the graph) calls ExecuteNextAsync() which calls on DrainAsync(),
    /// which by definition grabs documents from the parent component of the pipeline.
    /// This bubbles down until you reach a component that has a DocumentProducer that fetches a document from the backend.
    /// </para>
    /// </summary>
#nullable enable
    internal sealed class PipelinedDocumentQueryExecutionContext : CosmosQueryExecutionContext
    {
        /// <summary>
        /// The root level component that all calls will be forwarded to.
        /// </summary>
        private readonly IDocumentQueryExecutionComponent component;

        /// <summary>
        /// The actual page size to drain.
        /// </summary>
        private readonly int actualPageSize;

        /// <summary>
        /// Initializes a new instance of the PipelinedDocumentQueryExecutionContext class.
        /// </summary>
        /// <param name="component">The root level component that all calls will be forwarded to.</param>
        /// <param name="actualPageSize">The actual page size to drain.</param>
        private PipelinedDocumentQueryExecutionContext(
            IDocumentQueryExecutionComponent component,
            int actualPageSize)
        {
            this.component = component ?? throw new ArgumentNullException($"{nameof(component)} can not be null.");
            this.actualPageSize = (actualPageSize < 0) ? throw new ArgumentOutOfRangeException($"{nameof(actualPageSize)} can not be negative.") : actualPageSize;
        }

        /// <summary>
        /// Gets a value indicating whether this execution context is done draining documents.
        /// </summary>
        public override bool IsDone => this.component.IsDone;

        public static async Task<TryCatch<CosmosQueryExecutionContext>> TryCreateAsync(
            ExecutionEnvironment executionEnvironment,
            CosmosQueryContext queryContext,
            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams,
            CosmosElement requestContinuationToken,
            CancellationToken cancellationToken)
        {
            if (queryContext == null)
            {
                throw new ArgumentNullException(nameof(initParams));
            }

            cancellationToken.ThrowIfCancellationRequested();

            QueryInfo queryInfo = initParams.PartitionedQueryExecutionInfo.QueryInfo;

            int initialPageSize = initParams.InitialPageSize;
            if (queryInfo.HasGroupBy)
            {
                // The query will block until all groupings are gathered so we might as well speed up the process.
                initParams = new CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams(
                    sqlQuerySpec: initParams.SqlQuerySpec,
                    collectionRid: initParams.CollectionRid,
                    partitionedQueryExecutionInfo: initParams.PartitionedQueryExecutionInfo,
                    partitionKeyRanges: initParams.PartitionKeyRanges,
                    initialPageSize: int.MaxValue,
                    maxConcurrency: initParams.MaxConcurrency,
                    maxItemCount: int.MaxValue,
                    maxBufferedItemCount: initParams.MaxBufferedItemCount,
                    returnResultsInDeterministicOrder: true,
                    testSettings: initParams.TestSettings);
            }

            Task<TryCatch<IDocumentQueryExecutionComponent>> tryCreateOrderByComponentAsync(CosmosElement continuationToken)
            {
                return CosmosOrderByItemQueryExecutionContext.TryCreateAsync(
                    queryContext,
                    initParams,
                    continuationToken,
                    cancellationToken);
            }

            Task<TryCatch<IDocumentQueryExecutionComponent>> tryCreateParallelComponentAsync(CosmosElement continuationToken)
            {
                return CosmosParallelItemQueryExecutionContext.TryCreateAsync(
                    queryContext,
                    initParams,
                    continuationToken,
                    cancellationToken);
            }

            Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreatePipelineAsync;
            if (queryInfo.HasOrderBy)
            {
                tryCreatePipelineAsync = tryCreateOrderByComponentAsync;
            }
            else
            {
                tryCreatePipelineAsync = tryCreateParallelComponentAsync;
            }

            if (queryInfo.HasAggregates && !queryInfo.HasGroupBy)
            {
                Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync = tryCreatePipelineAsync;
                tryCreatePipelineAsync = (continuationToken) =>
                {
                    return AggregateDocumentQueryExecutionComponent.TryCreateAsync(
                        executionEnvironment,
                        queryInfo.Aggregates,
                        queryInfo.GroupByAliasToAggregateType,
                        queryInfo.GroupByAliases,
                        queryInfo.HasSelectValue,
                        continuationToken,
                        tryCreateSourceAsync);
                };
            }

            if (queryInfo.HasDistinct)
            {
                Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync = tryCreatePipelineAsync;
                tryCreatePipelineAsync = (continuationToken) =>
                {
                    return DistinctDocumentQueryExecutionComponent.TryCreateAsync(
                        executionEnvironment,
                        continuationToken,
                        tryCreateSourceAsync,
                        queryInfo.DistinctType);
                };
            }

            if (queryInfo.HasGroupBy)
            {
                Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync = tryCreatePipelineAsync;
                tryCreatePipelineAsync = (continuationToken) =>
                {
                    return GroupByDocumentQueryExecutionComponent.TryCreateAsync(
                        executionEnvironment,
                        continuationToken,
                        tryCreateSourceAsync,
                        queryInfo.GroupByAliasToAggregateType,
                        queryInfo.GroupByAliases,
                        queryInfo.HasSelectValue);
                };
            }

            if (queryInfo.Offset.HasValue)
            {
                Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync = tryCreatePipelineAsync;
                tryCreatePipelineAsync = (continuationToken) =>
                {
                    return SkipDocumentQueryExecutionComponent.TryCreateAsync(
                        executionEnvironment,
                        queryInfo.Offset.Value,
                        continuationToken,
                        tryCreateSourceAsync);
                };
            }

            if (queryInfo.Limit.HasValue)
            {
                Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync = tryCreatePipelineAsync;
                tryCreatePipelineAsync = (continuationToken) =>
                {
                    return TakeDocumentQueryExecutionComponent.TryCreateLimitDocumentQueryExecutionComponentAsync(
                        executionEnvironment,
                        queryInfo.Limit.Value,
                        continuationToken,
                        tryCreateSourceAsync);
                };
            }

            if (queryInfo.Top.HasValue)
            {
                Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync = tryCreatePipelineAsync;
                tryCreatePipelineAsync = (continuationToken) =>
                {
                    return TakeDocumentQueryExecutionComponent.TryCreateTopDocumentQueryExecutionComponentAsync(
                        executionEnvironment,
                        queryInfo.Top.Value,
                        continuationToken,
                        tryCreateSourceAsync);
                };
            }

            return (await tryCreatePipelineAsync(requestContinuationToken))
                .Try<CosmosQueryExecutionContext>((source) => new PipelinedDocumentQueryExecutionContext(source, initialPageSize));
        }

        public static async Task<TryCatch<CosmosQueryExecutionContext>> TryCreatePassthroughAsync(
            ExecutionEnvironment executionEnvironment,
            CosmosQueryContext queryContext,
            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams,
            CosmosElement requestContinuationToken,
            CancellationToken cancellationToken)
        {
            if (queryContext == null)
            {
                throw new ArgumentNullException(nameof(queryContext));
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Modify query plan
            PartitionedQueryExecutionInfo passThroughQueryInfo = new PartitionedQueryExecutionInfo()
            {
                QueryInfo = new QueryInfo()
                {
                    Aggregates = null,
                    DistinctType = DistinctQueryType.None,
                    GroupByAliases = null,
                    GroupByAliasToAggregateType = null,
                    GroupByExpressions = null,
                    HasSelectValue = false,
                    Limit = null,
                    Offset = null,
                    OrderBy = null,
                    OrderByExpressions = null,
                    RewrittenQuery = null,
                    Top = null,
                },
                QueryRanges = initParams.PartitionedQueryExecutionInfo.QueryRanges,
            };

            initParams = new CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams(
                sqlQuerySpec: initParams.SqlQuerySpec,
                collectionRid: initParams.CollectionRid,
                partitionedQueryExecutionInfo: passThroughQueryInfo,
                partitionKeyRanges: initParams.PartitionKeyRanges,
                initialPageSize: initParams.MaxItemCount.GetValueOrDefault(1000),
                maxConcurrency: initParams.MaxConcurrency,
                maxItemCount: initParams.MaxItemCount,
                maxBufferedItemCount: initParams.MaxBufferedItemCount,
                returnResultsInDeterministicOrder: initParams.ReturnResultsInDeterministicOrder,
                testSettings: initParams.TestSettings);

            // Return a parallel context, since we still want to be able to handle splits and concurrency / buffering.
            return (await CosmosParallelItemQueryExecutionContext.TryCreateAsync(
                queryContext,
                initParams,
                requestContinuationToken,
                cancellationToken))
                .Try<CosmosQueryExecutionContext>((source) => new PipelinedDocumentQueryExecutionContext(
                    source,
                    initParams.InitialPageSize));
        }

        /// <summary>
        /// Disposes of this context.
        /// </summary>
        public override void Dispose()
        {
            this.component.Dispose();
        }

        /// <summary>
        /// Gets the next page of results from this context.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on that in turn returns a DoucmentFeedResponse of results.</returns>
        public async Task<DocumentFeedResponse<CosmosElement>> ExecuteNextFeedResponseAsync(CancellationToken token)
        {
            QueryResponseCore feedResponse = await this.ExecuteNextAsync(token);
            return new DocumentFeedResponse<CosmosElement>(
                result: feedResponse.CosmosElements,
                count: feedResponse.CosmosElements.Count,
                responseHeaders: new DictionaryNameValueCollection(),
                useETagAsContinuation: false,
                queryMetrics: null,
                requestStats: null,
                disallowContinuationTokenMessage: feedResponse.DisallowContinuationTokenMessage,
                responseLengthBytes: feedResponse.ResponseLengthBytes);
        }

        /// <summary>
        /// Gets the next page of results from this context.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on that in turn returns a DoucmentFeedResponse of results.</returns>
        public override async Task<QueryResponseCore> ExecuteNextAsync(CancellationToken token)
        {
            try
            {
                QueryResponseCore queryResponse = await this.component.DrainAsync(this.actualPageSize, token);
                if (!queryResponse.IsSuccess)
                {
                    this.component.Stop();
                    return queryResponse;
                }

                string? updatedContinuationToken;
                if (queryResponse.DisallowContinuationTokenMessage == null)
                {
                    if (queryResponse.ContinuationToken != null)
                    {
                        updatedContinuationToken = new PipelineContinuationTokenV0(CosmosElement.Parse(queryResponse.ContinuationToken)).ToString();
                    }
                    else
                    {
                        updatedContinuationToken = null;
                    }
                }
                else
                {
                    updatedContinuationToken = null;
                }

                return QueryResponseCore.CreateSuccess(
                    result: queryResponse.CosmosElements,
                    continuationToken: updatedContinuationToken,
                    disallowContinuationTokenMessage: queryResponse.DisallowContinuationTokenMessage,
                    activityId: queryResponse.ActivityId,
                    requestCharge: queryResponse.RequestCharge,
                    responseLengthBytes: queryResponse.ResponseLengthBytes);
            }
            catch (Exception)
            {
                this.component.Stop();
                throw;
            }
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            return this.component.GetCosmosElementContinuationToken();
        }
    }
}
