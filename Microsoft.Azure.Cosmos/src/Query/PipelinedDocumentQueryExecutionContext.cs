//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.ExecutionComponent;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// You can imagine the pipeline to be a directed acyclic graph where documents flow from multiple sources (the partitions) to a single sink (the client who calls on ExecuteNextAsync()).
    /// The pipeline will consist of individual implementations of <see cref="IDocumentQueryExecutionContext"/>. 
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
    /// Note that the pipeline will always have one of <see cref="OrderByDocumentQueryExecutionContext"/> or <see cref="ParallelDocumentQueryExecutionContext"/>,
    /// which both derive from <see cref="CrossPartitionQueryExecutionContext"/> as these are top level execution contexts.
    /// These top level execution contexts have <see cref="DocumentProducerTree"/> that are responsible for hitting the backend
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
    internal sealed class PipelinedDocumentQueryExecutionContext : CosmosQueryExecutionContext, IDocumentQueryExecutionContext
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

        /// <summary>
        /// Creates a PipelinedDocumentQueryExecutionContext.
        /// </summary>
        /// <param name="constructorParams">The parameters for constructing the base class.</param>
        /// <param name="collectionRid">The collection rid.</param>
        /// <param name="partitionedQueryExecutionInfo">The partitioned query execution info.</param>
        /// <param name="partitionKeyRanges">The partition key ranges.</param>
        /// <param name="initialPageSize">The initial page size.</param>
        /// <param name="requestContinuation">The request continuation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task to await on, which in turn returns a PipelinedDocumentQueryExecutionContext.</returns>
        public static async Task<IDocumentQueryExecutionContext> CreateDocumentQueryExecutionContextAsync(
            DocumentQueryExecutionContextBase.InitParams constructorParams,
            string collectionRid,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            List<PartitionKeyRange> partitionKeyRanges,
            int initialPageSize,
            string requestContinuation,
            CancellationToken cancellationToken)
        {
            DefaultTrace.TraceInformation(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}, CorrelatedActivityId: {1} | Pipelined~Context.CreateAsync",
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    constructorParams.CorrelatedActivityId));

            QueryInfo queryInfo = partitionedQueryExecutionInfo.QueryInfo;

            int actualPageSize = initialPageSize;
            if (queryInfo.HasGroupBy)
            {
                initialPageSize = int.MaxValue;
                constructorParams.FeedOptions.MaxItemCount = int.MaxValue;
            }

            Func<string, Task<IDocumentQueryExecutionComponent>> createOrderByComponentFunc = async (continuationToken) =>
            {
                CrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams = new CrossPartitionQueryExecutionContext.CrossPartitionInitParams(
                    collectionRid,
                    partitionedQueryExecutionInfo,
                    partitionKeyRanges,
                    initialPageSize,
                    continuationToken);

                return await OrderByDocumentQueryExecutionContext.CreateAsync(
                    constructorParams,
                    initParams,
                    cancellationToken);
            };

            Func<string, Task<IDocumentQueryExecutionComponent>> createParallelComponentFunc = async (continuationToken) =>
            {
                CrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams = new CrossPartitionQueryExecutionContext.CrossPartitionInitParams(
                    collectionRid,
                    partitionedQueryExecutionInfo,
                    partitionKeyRanges,
                    initialPageSize,
                    continuationToken);

                return await ParallelDocumentQueryExecutionContext.CreateAsync(
                    constructorParams,
                    initParams,
                    cancellationToken);
            };

            return (IDocumentQueryExecutionContext)(await PipelinedDocumentQueryExecutionContext.CreateHelperAsync(
                partitionedQueryExecutionInfo.QueryInfo,
                initialPageSize,
                requestContinuation,
                constructorParams.FeedOptions.EnableGroupBy,
                createOrderByComponentFunc,
                createParallelComponentFunc));
        }

        /// <summary>
        /// Creates a CosmosPipelinedItemQueryExecutionContext.
        /// </summary>
        /// <param name="constructorParams">The parameters for constructing the base class.</param>
        /// <param name="collectionRid">The collection rid.</param>
        /// <param name="partitionedQueryExecutionInfo">The partitioned query execution info.</param>
        /// <param name="partitionKeyRanges">The partition key ranges.</param>
        /// <param name="initialPageSize">The initial page size.</param>
        /// <param name="requestContinuation">The request continuation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task to await on, which in turn returns a CosmosPipelinedItemQueryExecutionContext.</returns>
        public static async Task<CosmosQueryExecutionContext> CreateAsync(
            CosmosQueryContext constructorParams,
            string collectionRid,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            List<PartitionKeyRange> partitionKeyRanges,
            int initialPageSize,
            string requestContinuation,
            CancellationToken cancellationToken)
        {
            DefaultTrace.TraceInformation(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}, CorrelatedActivityId: {1} | Pipelined~Context.CreateAsync",
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    constructorParams.CorrelatedActivityId));

            QueryInfo queryInfo = partitionedQueryExecutionInfo.QueryInfo;

            int actualPageSize = initialPageSize;
            if (queryInfo.HasGroupBy)
            {
                initialPageSize = int.MaxValue;
                constructorParams.QueryRequestOptions.MaxItemCount = int.MaxValue;
            }

            Func<string, Task<IDocumentQueryExecutionComponent>> createOrderByComponentFunc = async (continuationToken) =>
            {
                CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams = new CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams(
                    collectionRid,
                    partitionedQueryExecutionInfo,
                    partitionKeyRanges,
                    initialPageSize,
                    continuationToken);

                return await CosmosOrderByItemQueryExecutionContext.CreateAsync(
                    constructorParams,
                    initParams,
                    cancellationToken);
            };

            Func<string, Task<IDocumentQueryExecutionComponent>> createParallelComponentFunc = async (continuationToken) =>
            {
                CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams = new CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams(
                    collectionRid,
                    partitionedQueryExecutionInfo,
                    partitionKeyRanges,
                    initialPageSize,
                    continuationToken);

                return await CosmosParallelItemQueryExecutionContext.CreateAsync(
                    constructorParams,
                    initParams,
                    cancellationToken);
            };

            return (CosmosQueryExecutionContext)(await PipelinedDocumentQueryExecutionContext.CreateHelperAsync(
               partitionedQueryExecutionInfo.QueryInfo,
               initialPageSize,
               requestContinuation,
               constructorParams.QueryRequestOptions.EnableGroupBy,
               createOrderByComponentFunc,
               createParallelComponentFunc));
        }

        private static async Task<PipelinedDocumentQueryExecutionContext> CreateHelperAsync(
            QueryInfo queryInfo,
            int initialPageSize,
            string requestContinuation,
            bool allowGroupBy,
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
                        queryInfo.Aggregates,
                        queryInfo.GroupByAliasToAggregateType,
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
                        continuationToken,
                        createSourceCallback,
                        queryInfo.DistinctType);
                };
            }

            if (queryInfo.HasGroupBy)
            {
                if (!allowGroupBy)
                {
                    throw new ArgumentException("Cross Partition GROUP BY is not supported.");
                }

                Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback = createComponentFunc;
                createComponentFunc = async (continuationToken) =>
                {
                    return await GroupByDocumentQueryExecutionComponent.CreateAsync(
                        continuationToken,
                        createSourceCallback,
                        queryInfo.GroupByAliasToAggregateType,
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
                        queryInfo.Top.Value,
                        continuationToken,
                        createSourceCallback);
                };
            }

            return new PipelinedDocumentQueryExecutionContext(
                await createComponentFunc(requestContinuation), initialPageSize);
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
            QueryResponse feedResponse = await this.ExecuteNextAsync(token);
            return new DocumentFeedResponse<CosmosElement>(
                result: feedResponse.CosmosElements,
                count: feedResponse.Count,
                responseHeaders: feedResponse.Headers.CosmosMessageHeaders,
                useETagAsContinuation: false,
                queryMetrics: null,
                requestStats: feedResponse.RequestStatistics,
                disallowContinuationTokenMessage: feedResponse.QueryHeaders.DisallowContinuationTokenMessage,
                responseLengthBytes: feedResponse.ResponseLengthBytes);
        }

        /// <summary>
        /// Gets the next page of results from this context.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on that in turn returns a DoucmentFeedResponse of results.</returns>
        public override async Task<QueryResponse> ExecuteNextAsync(CancellationToken token)
        {
            try
            {
                QueryResponse queryResponse = await this.component.DrainAsync(this.actualPageSize, token);
                if (!queryResponse.IsSuccessStatusCode)
                {
                    this.component.Stop();
                    return queryResponse;
                }

                List<CosmosElement> dynamics = new List<CosmosElement>();
                foreach (CosmosElement element in queryResponse.CosmosElements)
                {
                    dynamics.Add(element);
                }

                return QueryResponse.CreateSuccess(
                    dynamics,
                    queryResponse.Count,
                    queryResponse.ResponseLengthBytes,
                    queryResponse.QueryHeaders.CloneKnownProperties(),
                    queryMetrics: queryResponse.queryMetrics);
            }
            catch (Exception)
            {
                this.component.Stop();
                throw;
            }
        }
    }
}
