// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Aggregate
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Aggregate.Aggregators;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal abstract partial class AggregateDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private sealed class ClientAggregateDocumentQueryExecutionComponent : AggregateDocumentQueryExecutionComponent
        {
            private ClientAggregateDocumentQueryExecutionComponent(
                IDocumentQueryExecutionComponent source,
                SingleGroupAggregator singleGroupAggregator,
                bool isValueAggregateQuery)
                : base(source, singleGroupAggregator, isValueAggregateQuery)
            {
                // all the work is done in the base constructor.
            }

            public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateAsync(
                AggregateOperator[] aggregates,
                IReadOnlyDictionary<string, AggregateOperator?> aliasToAggregateType,
                IReadOnlyList<string> orderedAliases,
                bool hasSelectValue,
                string continuationToken,
                Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
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

                if (!tryCreateSingleGroupAggregator.Succeeded)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(tryCreateSingleGroupAggregator.Exception);
                }

                return (await tryCreateSourceAsync(continuationToken))
                    .Try<IDocumentQueryExecutionComponent>((source) =>
                    {
                        return new ClientAggregateDocumentQueryExecutionComponent(
                            source,
                            tryCreateSingleGroupAggregator.Result,
                            hasSelectValue);
                    });
            }

            public override async Task<QueryResponseCore> DrainAsync(
                int maxElements,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Note-2016-10-25-felixfan: Given what we support now, we should expect to return only 1 document.
                // Note-2019-07-11-brchon: We can return empty pages until all the documents are drained,
                // but then we will have to design a continuation token.

                double requestCharge = 0;
                long responseLengthBytes = 0;
                List<QueryPageDiagnostics> diagnosticsPages = new List<QueryPageDiagnostics>();
                while (!this.Source.IsDone)
                {
                    QueryResponseCore sourceResponse = await this.Source.DrainAsync(int.MaxValue, cancellationToken);
                    if (!sourceResponse.IsSuccess)
                    {
                        return sourceResponse;
                    }

                    requestCharge += sourceResponse.RequestCharge;
                    responseLengthBytes += sourceResponse.ResponseLengthBytes;
                    if (sourceResponse.Diagnostics != null)
                    {
                        diagnosticsPages.AddRange(sourceResponse.Diagnostics);
                    }

                    foreach (CosmosElement element in sourceResponse.CosmosElements)
                    {
                        RewrittenAggregateProjections rewrittenAggregateProjections = new RewrittenAggregateProjections(
                            this.isValueAggregateQuery,
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

                return QueryResponseCore.CreateSuccess(
                    result: finalResult,
                    continuationToken: null,
                    activityId: null,
                    disallowContinuationTokenMessage: null,
                    requestCharge: requestCharge,
                    diagnostics: diagnosticsPages,
                    pipelineDiagnostics: null,
                    responseLengthBytes: responseLengthBytes);
            }

            public override bool TryGetContinuationToken(out string state)
            {
                // Since we block until we get the final result the continuation token is always null.
                state = null;
                return true;
            }
        }
    }
}
