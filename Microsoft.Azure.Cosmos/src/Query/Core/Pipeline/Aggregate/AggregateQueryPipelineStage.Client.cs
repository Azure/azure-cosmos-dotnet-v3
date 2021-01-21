// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate.Aggregators;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal abstract partial class AggregateQueryPipelineStage : QueryPipelineStageBase
    {
        private sealed class ClientAggregateQueryPipelineStage : AggregateQueryPipelineStage
        {
            private ClientAggregateQueryPipelineStage(
                IQueryPipelineStage source,
                SingleGroupAggregator singleGroupAggregator,
                bool isValueAggregateQuery,
                CancellationToken cancellationToken)
                : base(source, singleGroupAggregator, isValueAggregateQuery, cancellationToken)
            {
                // all the work is done in the base constructor.
            }

            public static TryCatch<IQueryPipelineStage> MonadicCreate(
                IReadOnlyList<AggregateOperator> aggregates,
                IReadOnlyDictionary<string, AggregateOperator?> aliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                bool hasSelectValue,
                CosmosElement continuationToken,
                CancellationToken cancellationToken,
                MonadicCreatePipelineStage monadicCreatePipelineStage)
            {
                if (monadicCreatePipelineStage == null)
                {
                    throw new ArgumentNullException(nameof(monadicCreatePipelineStage));
                }

                TryCatch<SingleGroupAggregator> tryCreateSingleGroupAggregator = SingleGroupAggregator.TryCreate(
                    aggregates,
                    aliasToAggregateType,
                    orderedAliases,
                    hasSelectValue,
                    continuationToken: null);
                if (tryCreateSingleGroupAggregator.Failed)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(tryCreateSingleGroupAggregator.Exception);
                }

                TryCatch<IQueryPipelineStage> tryCreateSource = monadicCreatePipelineStage(continuationToken, cancellationToken);
                if (tryCreateSource.Failed)
                {
                    return tryCreateSource;
                }

                ClientAggregateQueryPipelineStage stage = new ClientAggregateQueryPipelineStage(
                    tryCreateSource.Result,
                    tryCreateSingleGroupAggregator.Result,
                    hasSelectValue,
                    cancellationToken);

                return TryCatch<IQueryPipelineStage>.FromResult(stage);
            }

            public override async ValueTask<bool> MoveNextAsync(ITrace trace)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                if (trace == null)
                {
                    throw new ArgumentNullException(nameof(trace));
                }

                if (this.returnedFinalPage)
                {
                    return false;
                }

                // Note-2016-10-25-felixfan: Given what we support now, we should expect to return only 1 document.
                // Note-2019-07-11-brchon: We can return empty pages until all the documents are drained,
                // but then we will have to design a continuation token.

                double requestCharge = 0;
                long responseLengthBytes = 0;
                while (await this.inputStage.MoveNextAsync(trace))
                {
                    TryCatch<QueryPage> tryGetPageFromSource = this.inputStage.Current;
                    if (tryGetPageFromSource.Failed)
                    {
                        this.Current = tryGetPageFromSource;
                        return true;
                    }

                    QueryPage sourcePage = tryGetPageFromSource.Result;

                    requestCharge += sourcePage.RequestCharge;
                    responseLengthBytes += sourcePage.ResponseLengthInBytes;

                    foreach (CosmosElement element in sourcePage.Documents)
                    {
                        this.cancellationToken.ThrowIfCancellationRequested();

                        RewrittenAggregateProjections rewrittenAggregateProjections = new RewrittenAggregateProjections(
                            this.isValueQuery,
                            element);
                        this.singleGroupAggregator.AddValues(rewrittenAggregateProjections.Payload);
                    }
                }

                List<CosmosElement> finalResult = new List<CosmosElement>();
                CosmosElement aggregationResult = this.singleGroupAggregator.GetResult();
                if (aggregationResult != null)
                {
                    finalResult.Add(aggregationResult);
                }

                QueryPage queryPage = new QueryPage(
                    documents: finalResult,
                    requestCharge: requestCharge,
                    activityId: default,
                    responseLengthInBytes: responseLengthBytes,
                    cosmosQueryExecutionInfo: default,
                    disallowContinuationTokenMessage: default,
                    state: default);

                this.Current = TryCatch<QueryPage>.FromResult(queryPage);
                this.returnedFinalPage = true;
                return true;
            }
        }
    }
}
