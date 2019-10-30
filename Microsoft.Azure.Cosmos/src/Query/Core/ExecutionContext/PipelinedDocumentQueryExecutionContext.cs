//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.ExecutionComponent;
    using Microsoft.Azure.Documents.Collections;
    using PartitionKeyRange = Documents.PartitionKeyRange;

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
        /// <param name="queryContext">The parameters for constructing the base class.</param>
        /// <param name="initParams">The initial parameters</param>
        /// <param name="requestContinuationToken">The request continuation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task to await on, which in turn returns a CosmosPipelinedItemQueryExecutionContext.</returns>
        public static async Task<CosmosQueryExecutionContext> CreateAsync(
            CosmosQueryContext queryContext,
            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams,
            string requestContinuationToken,
            CancellationToken cancellationToken)
        {
            if (queryContext == null)
            {
                throw new ArgumentNullException(nameof(initParams));
            }

            cancellationToken.ThrowIfCancellationRequested();

            TryCatch<PipelinedDocumentQueryExecutionContext> tryCreateMonad = await PipelinedDocumentQueryExecutionContext.TryCreateAsync(
                queryContext,
                initParams,
                requestContinuationToken,
                cancellationToken);

            if (!tryCreateMonad.Succeeded)
            {
                throw queryContext.QueryClient.CreateBadRequestException(tryCreateMonad.Exception.ToString());
            }

            return tryCreateMonad.Result;
        }

        public static async Task<TryCatch<PipelinedDocumentQueryExecutionContext>> TryCreateAsync(
            CosmosQueryContext queryContext,
            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams,
            string requestContinuationToken,
            CancellationToken cancellationToken)
        {
            if (queryContext == null)
            {
                throw new ArgumentNullException(nameof(initParams));
            }

            cancellationToken.ThrowIfCancellationRequested();

            QueryInfo queryInfo = initParams.PartitionedQueryExecutionInfo.QueryInfo;

            int initialPageSize = initParams.InitialPageSize;
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

            async Task<TryCatch<IDocumentQueryExecutionComponent>> tryCreateOrderByComponentAsync(string continuationToken)
            {
                return (await CosmosOrderByItemQueryExecutionContext.TryCreateAsync(
                    queryContext,
                    initParams,
                    continuationToken,
                    cancellationToken)).Try<IDocumentQueryExecutionComponent>(component => component);
            }

            async Task<TryCatch<IDocumentQueryExecutionComponent>> tryCreateParallelComponentAsync(string continuationToken)
            {
                return (await CosmosParallelItemQueryExecutionContext.TryCreateAsync(
                    queryContext,
                    initParams,
                    continuationToken,
                    cancellationToken)).Try<IDocumentQueryExecutionComponent>(component => component);
            }

            Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreatePipelineAsync;
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
                Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync = tryCreatePipelineAsync;
                tryCreatePipelineAsync = async (continuationToken) =>
                {
                    return (await AggregateDocumentQueryExecutionComponent.TryCreateAsync(
                        queryInfo.Aggregates,
                        queryInfo.GroupByAliasToAggregateType,
                        queryInfo.HasSelectValue,
                        continuationToken,
                        tryCreateSourceAsync)).Try<IDocumentQueryExecutionComponent>(x => x);
                };
            }

            if (queryInfo.HasDistinct)
            {
                Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync = tryCreatePipelineAsync;
                tryCreatePipelineAsync = async (continuationToken) =>
                {
                    return (await DistinctDocumentQueryExecutionComponent.TryCreateAsync(
                        continuationToken,
                        tryCreateSourceAsync,
                        queryInfo.DistinctType)).Try<IDocumentQueryExecutionComponent>(x => x);
                };
            }

            if (queryInfo.HasGroupBy)
            {
                Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync = tryCreatePipelineAsync;
                tryCreatePipelineAsync = async (continuationToken) =>
                {
                    return (await GroupByDocumentQueryExecutionComponent.TryCreateAsync(
                        continuationToken,
                        tryCreateSourceAsync,
                        queryInfo.GroupByAliasToAggregateType,
                        queryInfo.HasSelectValue)).Try<IDocumentQueryExecutionComponent>(x => x);
                };
            }

            if (queryInfo.HasOffset)
            {
                Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync = tryCreatePipelineAsync;
                tryCreatePipelineAsync = async (continuationToken) =>
                {
                    return (await SkipDocumentQueryExecutionComponent.TryCreateAsync(
                        queryInfo.Offset.Value,
                        continuationToken,
                        tryCreateSourceAsync)).Try<IDocumentQueryExecutionComponent>(x => x);
                };
            }

            if (queryInfo.HasLimit)
            {
                Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync = tryCreatePipelineAsync;
                tryCreatePipelineAsync = async (continuationToken) =>
                {
                    return (await TakeDocumentQueryExecutionComponent.TryCreateLimitDocumentQueryExecutionComponentAsync(
                        queryInfo.Limit.Value,
                        continuationToken,
                        tryCreateSourceAsync)).Try<IDocumentQueryExecutionComponent>(x => x);
                };
            }

            if (queryInfo.HasTop)
            {
                Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync = tryCreatePipelineAsync;
                tryCreatePipelineAsync = async (continuationToken) =>
                {
                    return (await TakeDocumentQueryExecutionComponent.TryCreateTopDocumentQueryExecutionComponentAsync(
                        queryInfo.Top.Value,
                        continuationToken,
                        tryCreateSourceAsync)).Try<IDocumentQueryExecutionComponent>(x => x);
                };
            }

            return (await tryCreatePipelineAsync(requestContinuationToken))
                .Try((source) => new PipelinedDocumentQueryExecutionContext(source, initialPageSize));
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
                }

                return queryResponse;
            }
            catch (Exception)
            {
                this.component.Stop();
                throw;
            }
        }
    }
}
