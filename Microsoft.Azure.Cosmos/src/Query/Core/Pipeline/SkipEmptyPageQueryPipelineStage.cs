// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class SkipEmptyPageQueryPipelineStage : IQueryPipelineStage
    {
        private static readonly IReadOnlyList<CosmosElement> EmptyPage = new List<CosmosElement>();

        private readonly IQueryPipelineStage inputStage;
        private double cumulativeRequestCharge;
        private long cumulativeResponseLengthInBytes;
        private CancellationToken cancellationToken;
        private bool returnedFinalStats;

        public SkipEmptyPageQueryPipelineStage(IQueryPipelineStage inputStage, CancellationToken cancellationToken)
        {
            this.inputStage = inputStage ?? throw new ArgumentNullException(nameof(inputStage));
            this.cancellationToken = cancellationToken;
        }

        public TryCatch<QueryPage> Current { get; private set; }

        public ValueTask DisposeAsync() => this.inputStage.DisposeAsync();

        public async ValueTask<bool> MoveNextAsync()
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (!await this.inputStage.MoveNextAsync())
            {
                if (!this.returnedFinalStats)
                {
                    QueryPage queryPage = new QueryPage(
                        documents: EmptyPage,
                        requestCharge: this.cumulativeRequestCharge,
                        activityId: Guid.Empty.ToString(),
                        responseLengthInBytes: this.cumulativeResponseLengthInBytes,
                        cosmosQueryExecutionInfo: default,
                        disallowContinuationTokenMessage: default,
                        state: default);
                    this.cumulativeRequestCharge = 0;
                    this.cumulativeResponseLengthInBytes = 0;
                    this.returnedFinalStats = true;
                    this.Current = TryCatch<QueryPage>.FromResult(queryPage);
                    return true;
                }

                this.Current = default;
                return false;
            }

            TryCatch<QueryPage> tryGetSourcePage = this.inputStage.Current;
            if (tryGetSourcePage.Failed)
            {
                this.Current = tryGetSourcePage;
                return true;
            }

            QueryPage sourcePage = tryGetSourcePage.Result;
            if (sourcePage.Documents.Count == 0)
            {
                this.cumulativeRequestCharge += sourcePage.RequestCharge;
                this.cumulativeResponseLengthInBytes += sourcePage.ResponseLengthInBytes;
                if (sourcePage.State == null)
                {
                    QueryPage queryPage = new QueryPage(
                        documents: EmptyPage,
                        requestCharge: sourcePage.RequestCharge + this.cumulativeRequestCharge,
                        activityId: sourcePage.ActivityId,
                        responseLengthInBytes: sourcePage.ResponseLengthInBytes + this.cumulativeResponseLengthInBytes,
                        cosmosQueryExecutionInfo: sourcePage.CosmosQueryExecutionInfo,
                        disallowContinuationTokenMessage: sourcePage.DisallowContinuationTokenMessage,
                        state: default);
                    this.cumulativeRequestCharge = 0;
                    this.cumulativeResponseLengthInBytes = 0;
                    this.Current = TryCatch<QueryPage>.FromResult(queryPage);
                    return true;
                }

                return await this.MoveNextAsync();
            }

            QueryPage cumulativeQueryPage;
            if (this.cumulativeRequestCharge != 0)
            {
                cumulativeQueryPage = new QueryPage(
                    documents: sourcePage.Documents,
                    requestCharge: sourcePage.RequestCharge + this.cumulativeRequestCharge,
                    activityId: sourcePage.ActivityId,
                    responseLengthInBytes: sourcePage.ResponseLengthInBytes + this.cumulativeResponseLengthInBytes,
                    cosmosQueryExecutionInfo: sourcePage.CosmosQueryExecutionInfo,
                    disallowContinuationTokenMessage: sourcePage.DisallowContinuationTokenMessage,
                    state: sourcePage.State);
                this.cumulativeRequestCharge = 0;
                this.cumulativeResponseLengthInBytes = 0;
            }
            else
            {
                cumulativeQueryPage = sourcePage;
            }

            this.Current = TryCatch<QueryPage>.FromResult(cumulativeQueryPage);
            return true;
        }

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
        }
    }
}
