// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class SkipEmptyPageQueryPipelineStage : IQueryPipelineStage
    {
        private static readonly IReadOnlyList<CosmosElement> EmptyPage = new List<CosmosElement>();

        private readonly IQueryPipelineStage inputStage;
        private double cumulativeRequestCharge;
        private IReadOnlyDictionary<string, string> cumulativeAdditionalHeaders;
        private bool returnedFinalStats;

        public SkipEmptyPageQueryPipelineStage(IQueryPipelineStage inputStage)
        {
            this.inputStage = inputStage ?? throw new ArgumentNullException(nameof(inputStage));
        }

        public TryCatch<QueryPage> Current { get; private set; }

        public ValueTask DisposeAsync() => this.inputStage.DisposeAsync();

        public async ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            for (int documentCount = 0; documentCount == 0;)
            {
                if (!await this.inputStage.MoveNextAsync(trace, cancellationToken))
                {
                    if (!this.returnedFinalStats)
                    {
                        QueryPage queryPage = new QueryPage(
                            documents: EmptyPage,
                            requestCharge: this.cumulativeRequestCharge,
                            activityId: Guid.Empty.ToString(),
                            cosmosQueryExecutionInfo: default,
                            distributionPlanSpec: default,
                            disallowContinuationTokenMessage: default,
                            additionalHeaders: this.cumulativeAdditionalHeaders,
                            state: default,
                            streaming: null);
                        this.cumulativeRequestCharge = 0;
                        this.cumulativeAdditionalHeaders = null;
                        this.returnedFinalStats = true;
                        this.Current = TryCatch<QueryPage>.FromResult(queryPage);
                        return true;
                    }

                    this.Current = default;
                    return false;
                }

                // if we are here then it means the inputStage told us there's more pages
                // so we tell the same thing to our consumer
                TryCatch<QueryPage> tryGetSourcePage = this.inputStage.Current;
                if (tryGetSourcePage.Failed)
                {
                    this.Current = tryGetSourcePage;
                    return true;
                }

                QueryPage sourcePage = tryGetSourcePage.Result;
                documentCount = sourcePage.Documents.Count;
                if (documentCount == 0)
                {
                    if (sourcePage.State == null)
                    {
                        QueryPage queryPage = new QueryPage(
                            documents: EmptyPage,
                            requestCharge: sourcePage.RequestCharge + this.cumulativeRequestCharge,
                            activityId: sourcePage.ActivityId,
                            cosmosQueryExecutionInfo: sourcePage.CosmosQueryExecutionInfo,
                            distributionPlanSpec: default,
                            disallowContinuationTokenMessage: sourcePage.DisallowContinuationTokenMessage,
                            additionalHeaders: sourcePage.AdditionalHeaders,
                            state: default,
                            streaming: sourcePage.Streaming);
                        this.cumulativeRequestCharge = 0;
                        this.cumulativeAdditionalHeaders = null;
                        this.Current = TryCatch<QueryPage>.FromResult(queryPage);
                        return true;
                    }

                    this.cumulativeRequestCharge += sourcePage.RequestCharge;
                    this.cumulativeAdditionalHeaders = sourcePage.AdditionalHeaders;
                }
                else
                {
                    QueryPage cumulativeQueryPage;
                    if (this.cumulativeRequestCharge != 0)
                    {
                        cumulativeQueryPage = new QueryPage(
                            documents: sourcePage.Documents,
                            requestCharge: sourcePage.RequestCharge + this.cumulativeRequestCharge,
                            activityId: sourcePage.ActivityId,
                            cosmosQueryExecutionInfo: sourcePage.CosmosQueryExecutionInfo,
                            distributionPlanSpec: default,
                            disallowContinuationTokenMessage: sourcePage.DisallowContinuationTokenMessage,
                            additionalHeaders: sourcePage.AdditionalHeaders,
                            state: sourcePage.State,
                            streaming: sourcePage.Streaming);
                        this.cumulativeRequestCharge = 0;
                        this.cumulativeAdditionalHeaders = null;
                    }
                    else
                    {
                        cumulativeQueryPage = sourcePage;
                    }

                    this.Current = TryCatch<QueryPage>.FromResult(cumulativeQueryPage);
                }
            }

            return true;
        }
    }
}
