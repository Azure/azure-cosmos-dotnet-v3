// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Take
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Newtonsoft.Json;

    internal abstract partial class TakeQueryPipelineStage : QueryPipelineStageBase
    {
        private sealed class ComputeTakeQueryPipelineStage : TakeQueryPipelineStage
        {
            private ComputeTakeQueryPipelineStage(
                IQueryPipelineStage source,
                int takeCount)
                : base(source, takeCount)
            {
                // Work is done in the base class.
            }

            public static TryCatch<IQueryPipelineStage> MonadicCreateLimitStage(
                int takeCount,
                CosmosElement requestContinuationToken,
                MonadicCreatePipelineStage monadicCreatePipelineStage) => ComputeTakeQueryPipelineStage.MonadicCreate(
                    takeCount,
                    requestContinuationToken,
                    monadicCreatePipelineStage);

            public static TryCatch<IQueryPipelineStage> MonadicCreateTopStage(
                int takeCount,
                CosmosElement requestContinuationToken,
                MonadicCreatePipelineStage monadicCreatePipelineStage) => ComputeTakeQueryPipelineStage.MonadicCreate(
                    takeCount,
                    requestContinuationToken,
                    monadicCreatePipelineStage);

            private static TryCatch<IQueryPipelineStage> MonadicCreate(
                int takeCount,
                CosmosElement requestContinuationToken,
                MonadicCreatePipelineStage monadicCreatePipelineStage)
            {
                if (takeCount < 0)
                {
                    throw new ArgumentException($"{nameof(takeCount)}: {takeCount} must be a non negative number.");
                }

                if (monadicCreatePipelineStage == null)
                {
                    throw new ArgumentNullException(nameof(monadicCreatePipelineStage));
                }

                TakeContinuationToken takeContinuationToken;
                if (requestContinuationToken != null)
                {
                    if (!TakeContinuationToken.TryParse(requestContinuationToken, out takeContinuationToken))
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
                            new MalformedContinuationTokenException(
                                $"Malformed {nameof(TakeContinuationToken)}: {requestContinuationToken}."));
                    }
                }
                else
                {
                    takeContinuationToken = new TakeContinuationToken(takeCount, sourceToken: null);
                }

                if (takeContinuationToken.TakeCount > takeCount)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(
                        new MalformedContinuationTokenException(
                            $"{nameof(TakeContinuationToken.TakeCount)} in {nameof(TakeContinuationToken)}: {requestContinuationToken}: {takeContinuationToken.TakeCount} can not be greater than the limit count in the query: {takeCount}."));
                }

                TryCatch<IQueryPipelineStage> tryCreateSource = monadicCreatePipelineStage(takeContinuationToken.SourceToken);
                if (tryCreateSource.Failed)
                {
                    return tryCreateSource;
                }

                IQueryPipelineStage stage = new ComputeTakeQueryPipelineStage(
                    tryCreateSource.Result,
                    takeContinuationToken.TakeCount);

                return TryCatch<IQueryPipelineStage>.FromResult(stage);
            }

            protected override async Task<TryCatch<QueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await this.inputStage.MoveNextAsync();
                TryCatch<QueryPage> tryGetSourcePage = this.inputStage.Current;
                if (tryGetSourcePage.Failed)
                {
                    return tryGetSourcePage;
                }

                QueryPage sourcePage = tryGetSourcePage.Result;

                List<CosmosElement> takedDocuments = sourcePage.Documents.Take(this.takeCount).ToList();
                this.takeCount -= takedDocuments.Count;

                QueryState queryState;
                if (sourcePage.State != null)
                {
                    TakeContinuationToken takeContinuationToken = new TakeContinuationToken(
                        takeCount: this.takeCount,
                        sourceToken: sourcePage.State.Value);
                    queryState = new QueryState(TakeContinuationToken.ToCosmosElement(takeContinuationToken));
                }
                else
                {
                    queryState = default;
                }

                QueryPage queryPage = new QueryPage(
                    documents: takedDocuments,
                    requestCharge: sourcePage.RequestCharge,
                    activityId: sourcePage.ActivityId,
                    responseLengthInBytes: sourcePage.ResponseLengthInBytes,
                    cosmosQueryExecutionInfo: sourcePage.CosmosQueryExecutionInfo,
                    disallowContinuationTokenMessage: sourcePage.DisallowContinuationTokenMessage,
                    state: queryState);

                return TryCatch<QueryPage>.FromResult(queryPage);
            }

            private readonly struct TakeContinuationToken
            {
                public static class PropertyNames
                {
                    public const string SourceToken = "SourceToken";
                    public const string TakeCount = "TakeCount";
                }

                public TakeContinuationToken(long takeCount, CosmosElement sourceToken)
                {
                    if ((takeCount < 0) || (takeCount > int.MaxValue))
                    {
                        throw new ArgumentException($"{nameof(takeCount)} must be a non negative number.");
                    }

                    this.TakeCount = (int)takeCount;
                    this.SourceToken = sourceToken;
                }

                public int TakeCount { get; }

                public CosmosElement SourceToken { get; }

                public static CosmosElement ToCosmosElement(TakeContinuationToken takeContinuationToken)
                {
                    Dictionary<string, CosmosElement> dictionary = new Dictionary<string, CosmosElement>()
                    {
                        {
                            TakeContinuationToken.PropertyNames.SourceToken,
                            takeContinuationToken.SourceToken
                        },
                        {
                            TakeContinuationToken.PropertyNames.TakeCount,
                            CosmosNumber64.Create(takeContinuationToken.TakeCount)
                        },
                    };

                    return CosmosObject.Create(dictionary);
                }

                public static bool TryParse(CosmosElement value, out TakeContinuationToken takeContinuationToken)
                {
                    if (value == null)
                    {
                        throw new ArgumentNullException(nameof(value));
                    }

                    if (!(value is CosmosObject continuationToken))
                    {
                        takeContinuationToken = default;
                        return false;
                    }

                    if (!continuationToken.TryGetValue(TakeContinuationToken.PropertyNames.TakeCount, out CosmosNumber takeCount))
                    {
                        takeContinuationToken = default;
                        return false;
                    }

                    if (!continuationToken.TryGetValue(TakeContinuationToken.PropertyNames.SourceToken, out CosmosElement sourceToken))
                    {
                        takeContinuationToken = default;
                        return false;
                    }

                    takeContinuationToken = new TakeContinuationToken(Number64.ToLong(takeCount.Value), sourceToken);
                    return true;
                }
            }
        }
    }
}
