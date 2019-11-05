//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
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
            if (component == null)
            {
                throw new ArgumentNullException($"{nameof(component)} can not be null.");
            }

            if (actualPageSize < 0)
            {
                throw new ArgumentException($"{nameof(actualPageSize)} can not be negative.");
            }

            this.component = component;
            this.actualPageSize = actualPageSize;
        }

        /// <summary>
        /// Gets a value indicating whether this execution context is done draining documents.
        /// </summary>
        public override bool IsDone
        {
            get
            {
                return this.component.IsDone;
            }
        }

        public override bool TryGetContinuationToken(out string state)
        {
            return this.component.TryGetContinuationToken(out state);
        }

        /// <summary>
        /// Creates a CosmosPipelinedItemQueryExecutionContext.
        /// </summary>
        /// <param name="executionEnvironment">The environment to execute on.</param>
        /// <param name="queryContext">The parameters for constructing the base class.</param>
        /// <param name="initParams">The initial parameters</param>
        /// <param name="requestContinuationToken">The request continuation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task to await on, which in turn returns a CosmosPipelinedItemQueryExecutionContext.</returns>
        public static async Task<CosmosQueryExecutionContext> CreateAsync(
            ExecutionEnvironment executionEnvironment,
            CosmosQueryContext queryContext,
            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams,
            string requestContinuationToken,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            QueryInfo queryInfo = initParams.PartitionedQueryExecutionInfo.QueryInfo;
            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams parameters = initParams;
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
                    maxBufferedItemCount: initParams.MaxBufferedItemCount);
            }

            Func<string, Task<IDocumentQueryExecutionComponent>> createOrderByComponentFunc = async (continuationToken) =>
            {
                return await CosmosOrderByItemQueryExecutionContext.CreateAsync(
                    queryContext,
                    initParams,
                    continuationToken,
                    cancellationToken);
            };

            Func<string, Task<IDocumentQueryExecutionComponent>> createParallelComponentFunc = async (continuationToken) =>
            {
                return await CosmosParallelItemQueryExecutionContext.CreateAsync(
                    queryContext,
                    initParams,
                    continuationToken,
                    cancellationToken);
            };

            int actualPageSize = initParams.MaxItemCount.GetValueOrDefault(1000);

            return (CosmosQueryExecutionContext)await PipelinedDocumentQueryExecutionContext.CreateHelperAsync(
                executionEnvironment,
                queryContext.QueryClient,
                initParams.PartitionedQueryExecutionInfo.QueryInfo,
                actualPageSize,
                requestContinuationToken,
                createOrderByComponentFunc,
                createParallelComponentFunc);
        }

        private static async Task<PipelinedDocumentQueryExecutionContext> CreateHelperAsync(
            ExecutionEnvironment executionEnvironment,
            CosmosQueryClient queryClient,
            QueryInfo queryInfo,
            int actualPageSize,
            string requestContinuation,
            Func<string, Task<IDocumentQueryExecutionComponent>> createOrderByQueryExecutionContext,
            Func<string, Task<IDocumentQueryExecutionComponent>> createParallelQueryExecutionContext)
        {
            Func<string, Task<IDocumentQueryExecutionComponent>> createComponentFunc;
            if (queryInfo.HasOrderBy)
            {
                createComponentFunc = createOrderByQueryExecutionContext;
            }
            else
            {
                createComponentFunc = createParallelQueryExecutionContext;
            }

            if (queryInfo.HasAggregates && !queryInfo.HasGroupBy)
            {
                Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback = createComponentFunc;
                createComponentFunc = async (continuationToken) =>
                {
                    return await AggregateDocumentQueryExecutionComponent.CreateAsync(
                        executionEnvironment,
                        queryClient,
                        queryInfo.Aggregates,
                        queryInfo.GroupByAliasToAggregateType,
                        queryInfo.GroupByAliases,
                        queryInfo.HasSelectValue,
                        continuationToken,
                        createSourceCallback);
                };
            }

            if (queryInfo.HasDistinct)
            {
                Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback = createComponentFunc;
                createComponentFunc = async (continuationToken) =>
                {
                    return await DistinctDocumentQueryExecutionComponent.CreateAsync(
                        queryClient,
                        continuationToken,
                        createSourceCallback,
                        queryInfo.DistinctType);
                };
            }

            if (queryInfo.HasGroupBy)
            {
                Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback = createComponentFunc;
                createComponentFunc = async (continuationToken) =>
                {
                    return await GroupByDocumentQueryExecutionComponent.CreateAsync(
                        executionEnvironment,
                        queryClient,
                        continuationToken,
                        createSourceCallback,
                        queryInfo.GroupByAliasToAggregateType,
                        queryInfo.GroupByAliases,
                        queryInfo.HasSelectValue);
                };
            }

            if (queryInfo.HasOffset)
            {
                Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback = createComponentFunc;
                createComponentFunc = async (continuationToken) =>
                {
                    return await SkipDocumentQueryExecutionComponent.CreateAsync(
                        queryInfo.Offset.Value,
                        continuationToken,
                        createSourceCallback);
                };
            }

            if (queryInfo.HasLimit)
            {
                Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback = createComponentFunc;
                createComponentFunc = async (continuationToken) =>
                {
                    return await TakeDocumentQueryExecutionComponent.CreateLimitDocumentQueryExecutionComponentAsync(
                        queryClient,
                        queryInfo.Limit.Value,
                        continuationToken,
                        createSourceCallback);
                };
            }

            if (queryInfo.HasTop)
            {
                Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback = createComponentFunc;
                createComponentFunc = async (continuationToken) =>
                {
                    return await TakeDocumentQueryExecutionComponent.CreateTopDocumentQueryExecutionComponentAsync(
                        queryClient,
                        queryInfo.Top.Value,
                        continuationToken,
                        createSourceCallback);
                };
            }

            return new PipelinedDocumentQueryExecutionContext(
                await createComponentFunc(requestContinuation),
                actualPageSize);
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

                string updatedContinuationToken;
                if (queryResponse.DisallowContinuationTokenMessage == null)
                {
                    if (queryResponse.ContinuationToken != null)
                    {
                        updatedContinuationToken = new PipelineContinuationTokenV0(queryResponse.ContinuationToken).ToString();
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
                    diagnostics: queryResponse.Diagnostics,
                    responseLengthBytes: queryResponse.ResponseLengthBytes);
            }
            catch (Exception)
            {
                this.component.Stop();
                throw;
            }
        }
    }
}
