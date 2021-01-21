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
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate.Aggregators;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;

    internal abstract partial class DCountQueryPipelineStage : QueryPipelineStageBase
    {
        private sealed class ClientDCountQueryPipelineStage : DCountQueryPipelineStage
        {
            private ClientDCountQueryPipelineStage(
                IQueryPipelineStage source,
                long count,
                DCountInfo info,
                CancellationToken cancellationToken)
                : base(source, count, info, cancellationToken)
            {
                // all the work is done in the base constructor.
            }

            public static TryCatch<IQueryPipelineStage> MonadicCreate(
                DCountInfo info,
                CosmosElement continuationToken,
                CancellationToken cancellationToken,
                MonadicCreatePipelineStage monadicCreatePipelineStage)
            {
                if (monadicCreatePipelineStage == null)
                {
                    throw new ArgumentNullException(nameof(monadicCreatePipelineStage));
                }

                TryCatch<IQueryPipelineStage> tryCreateSource = monadicCreatePipelineStage(continuationToken, cancellationToken);
                if (tryCreateSource.Failed)
                {
                    return tryCreateSource;
                }

                ClientDCountQueryPipelineStage stage = new ClientDCountQueryPipelineStage(
                    source: tryCreateSource.Result,
                    count: 0,
                    info: info,
                    cancellationToken: cancellationToken);

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

                    this.cancellationToken.ThrowIfCancellationRequested();
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
