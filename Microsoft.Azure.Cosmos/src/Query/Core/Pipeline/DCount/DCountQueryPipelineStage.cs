// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.DCount
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Stage that is able to aggregate COUNT(DISTINCT) from multiple continuations and partitions.
    /// </summary>
    internal class DCountQueryPipelineStage : QueryPipelineStageBase
    {
        /// <summary>
        /// We need to keep track of whether the projection has the 'VALUE' keyword or an alias.
        /// </summary>
        private readonly DCountInfo info;

        /// <summary>
        /// This job of this class is to just keep a count.
        /// </summary>
        private long count;

        protected bool returnedFinalPage;

        /// <summary>
        /// Initializes a new instance of the DCountQueryPipelineStage class.
        /// </summary>
        /// <param name="source">The source component that will supply the local aggregates from multiple continuations and partitions.</param>
        /// <param name="count">The actual dcount that will be reported.</param>
        /// <param name="info">Metadata about the original dcount query that is elided in the rewritten query</param>
        /// <remarks>This constructor is private since there is some async initialization that needs to happen in CreateAsync().</remarks>
        public DCountQueryPipelineStage(
            IQueryPipelineStage source,
            long count,
            DCountInfo info)
            : base(source)
        {
            this.count = count;
            this.info = info;
        }

        public override async ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (this.returnedFinalPage)
            {
                return false;
            }

            double requestCharge = 0;
            IReadOnlyDictionary<string, string> additionalHeaders = null;
            while (await this.inputStage.MoveNextAsync(trace, cancellationToken))
            {
                TryCatch<QueryPage> tryGetPageFromSource = this.inputStage.Current;
                if (tryGetPageFromSource.Failed)
                {
                    this.Current = tryGetPageFromSource;
                    return true;
                }

                QueryPage sourcePage = tryGetPageFromSource.Result;

                requestCharge += sourcePage.RequestCharge;
                additionalHeaders = sourcePage.AdditionalHeaders;

                cancellationToken.ThrowIfCancellationRequested();
                this.count += sourcePage.Documents.Count;
            }

            List<CosmosElement> finalResult = new List<CosmosElement>();
            CosmosElement aggregationResult = this.GetFinalResult();
            if (aggregationResult != null)
            {
                finalResult.Add(aggregationResult);
            }

            QueryPage queryPage = new QueryPage(
                documents: finalResult,
                requestCharge: requestCharge,
                activityId: default,
                cosmosQueryExecutionInfo: default,
                distributionPlanSpec: default,
                disallowContinuationTokenMessage: default,
                additionalHeaders: additionalHeaders,
                state: default,
                streaming: default);

            this.Current = TryCatch<QueryPage>.FromResult(queryPage);
            this.returnedFinalPage = true;
            return true;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            DCountInfo info,
            CosmosElement continuationToken,
            MonadicCreatePipelineStage monadicCreatePipelineStage)
        {
            if (monadicCreatePipelineStage == null)
            {
                throw new ArgumentNullException(nameof(monadicCreatePipelineStage));
            }

            TryCatch<IQueryPipelineStage> tryCreateSource = monadicCreatePipelineStage(continuationToken);
            if (tryCreateSource.Failed)
            {
                return tryCreateSource;
            }

            DCountQueryPipelineStage stage = new DCountQueryPipelineStage(
                source: tryCreateSource.Result,
                count: 0,
                info: info);

            return TryCatch<IQueryPipelineStage>.FromResult(stage);
        }

        protected CosmosElement GetFinalResult()
        {
            return this.info.IsValueAggregate ?
                CosmosNumber64.Create(this.count) as CosmosElement :
                CosmosObject.Create(new Dictionary<string, CosmosElement>
                {
                    { this.info.DCountAlias, CosmosNumber64.Create(this.count) }
                });
        }
    }
}
