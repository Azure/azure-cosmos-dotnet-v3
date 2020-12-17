// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.DCount
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate.Aggregators;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;

    /// <summary>
    /// Stage that is able to aggregate COUNT(DISTINCT) from multiple continuations and partitions.
    /// </summary>
    internal abstract partial class DCountQueryPipelineStage : QueryPipelineStageBase
    {
        /// <summary>
        /// This class does just keeps a count.
        /// </summary>
        private readonly IAggregator countAggregator;

        /// <summary>
        /// We need to keep track of whether the projection has the 'VALUE' keyword or an alias.
        /// </summary>
        private readonly DCountInfo info;

        protected bool returnedFinalPage;

        /// <summary>
        /// Initializes a new instance of the DCountQueryPipelineStage class.
        /// </summary>
        /// <param name="source">The source component that will supply the local aggregates from multiple continuations and partitions.</param>
        /// <param name="countAggregator">The count aggregator that we will feed results into.</param>
        /// <param name="info">Metadata about the original dcount query that is elided in the rewritten query</param>
        /// <param name="cancellationToken">The cancellation token for cooperative yeilding.</param>
        /// <remarks>This constructor is private since there is some async initialization that needs to happen in CreateAsync().</remarks>
        public DCountQueryPipelineStage(
            IQueryPipelineStage source,
            IAggregator countAggregator,
            DCountInfo info,
            CancellationToken cancellationToken)
            : base(source, cancellationToken)
        {
            this.countAggregator = countAggregator ?? throw new ArgumentNullException(nameof(countAggregator));
            this.info = info;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            ExecutionEnvironment executionEnvironment,
            DCountInfo info,
            CosmosElement continuationToken,
            CancellationToken cancellationToken,
            MonadicCreatePipelineStage monadicCreatePipelineStage) => executionEnvironment switch
            {
                ExecutionEnvironment.Client => ClientDCountQueryPipelineStage.MonadicCreate(
                    info,
                    continuationToken,
                    cancellationToken,
                    monadicCreatePipelineStage),
                ExecutionEnvironment.Compute => ComputeDCountQueryPipelineStage.MonadicCreate(
                    info,
                    continuationToken,
                    cancellationToken,
                    monadicCreatePipelineStage),
                _ => throw new ArgumentException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}."),
            };

        protected CosmosElement GetFinalResult() => this.info.IsValueAggregate ?
            this.countAggregator.GetResult() :
            CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                { this.info.DCountAlias, this.countAggregator.GetResult() }
            });
    }
}
