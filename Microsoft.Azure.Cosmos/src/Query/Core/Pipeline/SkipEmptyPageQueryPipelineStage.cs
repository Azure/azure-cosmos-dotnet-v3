// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class SkipEmptyPageQueryPipelineStage : IQueryPipelineStage
    {
        private readonly IQueryPipelineStage inputStage;
        private double cumulativeRequestCharge;
        private long cumulativeResponseLengthInBytes;

        public SkipEmptyPageQueryPipelineStage(IQueryPipelineStage inputStage)
        {
            this.inputStage = inputStage ?? throw new ArgumentNullException(nameof(inputStage));
        }

        public TryCatch<QueryPage> Current { get; private set; }

        public ValueTask DisposeAsync() => this.inputStage.DisposeAsync();

        public async ValueTask<bool> MoveNextAsync()
        {
            await this.inputStage.MoveNextAsync();
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
    }
}
