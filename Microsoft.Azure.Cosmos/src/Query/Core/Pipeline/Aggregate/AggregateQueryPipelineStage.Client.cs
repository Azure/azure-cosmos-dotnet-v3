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

    internal abstract partial class AggregateQueryPipelineStage : QueryPipelineStageBase
    {
        private sealed class ClientAggregateQueryPipelineStage : AggregateQueryPipelineStage
        {
            private ClientAggregateQueryPipelineStage(
                IQueryPipelineStage source,
                SingleGroupAggregator singleGroupAggregator,
                bool isValueAggregateQuery)
                : base(source, singleGroupAggregator, isValueAggregateQuery)
            {
                // all the work is done in the base constructor.
            }

            public static async Task<TryCatch<IQueryPipelineStage>> TryCreateAsync(
                IReadOnlyList<AggregateOperator> aggregates,
                IReadOnlyDictionary<string, AggregateOperator?> aliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                bool hasSelectValue,
                CosmosElement continuationToken,
                Func<CosmosElement, Task<TryCatch<IQueryPipelineStage>>> tryCreateSourceAsync)
            {
                if (tryCreateSourceAsync == null)
                {
                    throw new ArgumentNullException(nameof(tryCreateSourceAsync));
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

                TryCatch<IQueryPipelineStage> tryCreateSource = await tryCreateSourceAsync(continuationToken);
                if (tryCreateSource.Failed)
                {
                    return tryCreateSource;
                }

                ClientAggregateQueryPipelineStage stage = new ClientAggregateQueryPipelineStage(
                    tryCreateSource.Result,
                    tryCreateSingleGroupAggregator.Result,
                    hasSelectValue);

                return TryCatch<IQueryPipelineStage>.FromResult(stage);
            }

            protected override async Task<TryCatch<QueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Note-2016-10-25-felixfan: Given what we support now, we should expect to return only 1 document.
                // Note-2019-07-11-brchon: We can return empty pages until all the documents are drained,
                // but then we will have to design a continuation token.

                double requestCharge = 0;
                long responseLengthBytes = 0;
                while (this.inputStage.HasMoreResults)
                {
                    await this.inputStage.MoveNextAsync();
                    TryCatch<QueryPage> tryGetPageFromSource = this.inputStage.Current;

                    if (tryGetPageFromSource.Failed)
                    {
                        return tryGetPageFromSource;
                    }

                    QueryPage sourcePage = tryGetPageFromSource.Result;

                    requestCharge += sourcePage.RequestCharge;
                    responseLengthBytes += sourcePage.ResponseLengthInBytes;

                    foreach (CosmosElement element in sourcePage.Documents)
                    {
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

                return TryCatch<QueryPage>.FromResult(queryPage);
            }
        }
    }
}
