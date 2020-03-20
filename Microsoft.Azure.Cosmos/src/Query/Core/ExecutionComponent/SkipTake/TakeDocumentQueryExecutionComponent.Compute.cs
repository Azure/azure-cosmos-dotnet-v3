//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.SkipTake
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
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal abstract partial class TakeDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private sealed class ComputeTakeDocumentQueryExecutionComponent : TakeDocumentQueryExecutionComponent
        {
            private ComputeTakeDocumentQueryExecutionComponent(IDocumentQueryExecutionComponent source, int takeCount)
                : base(source, takeCount)
            {
                // Work is done in the base class.
            }

            public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateAsync(
                int takeCount,
                CosmosElement requestContinuationToken,
                Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
            {
                if (takeCount < 0)
                {
                    throw new ArgumentException($"{nameof(takeCount)}: {takeCount} must be a non negative number.");
                }

                if (tryCreateSourceAsync == null)
                {
                    throw new ArgumentNullException(nameof(tryCreateSourceAsync));
                }

                TakeContinuationToken takeContinuationToken;
                if (requestContinuationToken != null)
                {
                    if (!TakeContinuationToken.TryParse(requestContinuationToken, out takeContinuationToken))
                    {
                        return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException($"Malformed {nameof(TakeContinuationToken)}: {requestContinuationToken}."));
                    }
                }
                else
                {
                    takeContinuationToken = new TakeContinuationToken(takeCount, sourceToken: null);
                }

                if (takeContinuationToken.TakeCount > takeCount)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                        new MalformedContinuationTokenException($"{nameof(TakeContinuationToken.TakeCount)} in {nameof(TakeContinuationToken)}: {requestContinuationToken}: {takeContinuationToken.TakeCount} can not be greater than the limit count in the query: {takeCount}."));
                }

                return (await tryCreateSourceAsync(takeContinuationToken.SourceToken))
                    .Try<IDocumentQueryExecutionComponent>((source) => new ComputeTakeDocumentQueryExecutionComponent(
                    source,
                    takeContinuationToken.TakeCount));
            }

            public override async Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                QueryResponseCore sourcePage = await base.DrainAsync(maxElements, token);
                if (!sourcePage.IsSuccess)
                {
                    return sourcePage;
                }

                List<CosmosElement> takedDocuments = sourcePage.CosmosElements.Take(this.takeCount).ToList();
                this.takeCount -= takedDocuments.Count;

                return QueryResponseCore.CreateSuccess(
                    result: takedDocuments,
                    continuationToken: null,
                    disallowContinuationTokenMessage: DocumentQueryExecutionComponentBase.UseCosmosElementContinuationTokenInstead,
                    activityId: sourcePage.ActivityId,
                    requestCharge: sourcePage.RequestCharge,
                    diagnostics: sourcePage.Diagnostics,
                    responseLengthBytes: sourcePage.ResponseLengthBytes);
            }

            public override CosmosElement GetCosmosElementContinuationToken()
            {
                if (this.IsDone)
                {
                    return default;
                }

                TakeContinuationToken takeContinuationToken = new TakeContinuationToken(
                    takeCount: this.takeCount,
                    sourceToken: this.Source.GetCosmosElementContinuationToken());
                return TakeContinuationToken.ToCosmosElement(takeContinuationToken);
            }

            public override bool TryGetFeedToken(
                string containerResourceId,
                SqlQuerySpec sqlQuerySpec,
                out QueryFeedToken feedToken)
            {
                if (this.IsDone)
                {
                    feedToken = null;
                    return true;
                }

                if (!this.Source.TryGetFeedToken(containerResourceId, sqlQuerySpec, out feedToken))
                {
                    feedToken = null;
                    return false;
                }

                if (feedToken is QueryFeedTokenInternal feedTokenInternal
                    && feedTokenInternal.QueryFeedToken is FeedTokenEPKRange tokenEPKRange)
                {
                    TakeContinuationToken takeContinuationToken = new TakeContinuationToken(
                        takeCount: this.takeCount,
                        sourceToken: this.Source.GetCosmosElementContinuationToken());

                    feedToken = new QueryFeedTokenInternal(FeedTokenEPKRange.Copy(
                            tokenEPKRange,
                            TakeContinuationToken.ToCosmosElement(takeContinuationToken).ToString()),
                            feedTokenInternal.QueryDefinition);
                }

                return true;
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