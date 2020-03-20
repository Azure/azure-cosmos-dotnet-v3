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
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Newtonsoft.Json;

    internal abstract partial class TakeDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private sealed class ClientTakeDocumentQueryExecutionComponent : TakeDocumentQueryExecutionComponent
        {
            private readonly TakeEnum takeEnum;

            private ClientTakeDocumentQueryExecutionComponent(IDocumentQueryExecutionComponent source, int takeCount, TakeEnum takeEnum)
                : base(source, takeCount)
            {
                this.takeEnum = takeEnum;
            }

            public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateLimitDocumentQueryExecutionComponentAsync(
                int limitCount,
                CosmosElement requestContinuationToken,
                Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
            {
                if (limitCount < 0)
                {
                    throw new ArgumentException($"{nameof(limitCount)}: {limitCount} must be a non negative number.");
                }

                if (tryCreateSourceAsync == null)
                {
                    throw new ArgumentNullException(nameof(tryCreateSourceAsync));
                }

                LimitContinuationToken limitContinuationToken;
                if (requestContinuationToken != null)
                {
                    if (!LimitContinuationToken.TryParse(requestContinuationToken.ToString(), out limitContinuationToken))
                    {
                        return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException($"Malformed {nameof(LimitContinuationToken)}: {requestContinuationToken}."));
                    }
                }
                else
                {
                    limitContinuationToken = new LimitContinuationToken(limitCount, null);
                }

                if (limitContinuationToken.Limit > limitCount)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                        new MalformedContinuationTokenException($"{nameof(LimitContinuationToken.Limit)} in {nameof(LimitContinuationToken)}: {requestContinuationToken}: {limitContinuationToken.Limit} can not be greater than the limit count in the query: {limitCount}."));
                }

                CosmosElement sourceToken;
                if (limitContinuationToken.SourceToken != null)
                {
                    if (!CosmosElement.TryParse(limitContinuationToken.SourceToken, out sourceToken))
                    {
                        return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException($"Malformed {nameof(LimitContinuationToken)}: {requestContinuationToken}."));
                    }
                }
                else
                {
                    sourceToken = null;
                }

                return (await tryCreateSourceAsync(sourceToken))
                    .Try<IDocumentQueryExecutionComponent>((source) => new ClientTakeDocumentQueryExecutionComponent(
                    source,
                    limitContinuationToken.Limit,
                    TakeEnum.Limit));
            }

            public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateTopDocumentQueryExecutionComponentAsync(
                int topCount,
                CosmosElement requestContinuationToken,
                Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
            {
                if (topCount < 0)
                {
                    throw new ArgumentException($"{nameof(topCount)}: {topCount} must be a non negative number.");
                }

                if (tryCreateSourceAsync == null)
                {
                    throw new ArgumentNullException(nameof(tryCreateSourceAsync));
                }

                TopContinuationToken topContinuationToken;
                if (requestContinuationToken != null)
                {
                    if (!TopContinuationToken.TryParse(requestContinuationToken.ToString(), out topContinuationToken))
                    {
                        return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException($"Malformed {nameof(LimitContinuationToken)}: {requestContinuationToken}."));
                    }
                }
                else
                {
                    topContinuationToken = new TopContinuationToken(topCount, null);
                }

                if (topContinuationToken.Top > topCount)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                        new MalformedContinuationTokenException($"{nameof(TopContinuationToken.Top)} in {nameof(TopContinuationToken)}: {requestContinuationToken}: {topContinuationToken.Top} can not be greater than the top count in the query: {topCount}."));
                }

                CosmosElement sourceToken;
                if (topContinuationToken.SourceToken != null)
                {
                    if (!CosmosElement.TryParse(topContinuationToken.SourceToken, out sourceToken))
                    {
                        return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException($"{nameof(TopContinuationToken.SourceToken)} in {nameof(TopContinuationToken)}: {requestContinuationToken}: {topContinuationToken.SourceToken} was malformed."));
                    }
                }
                else
                {
                    sourceToken = null;
                }

                return (await tryCreateSourceAsync(sourceToken))
                    .Try<IDocumentQueryExecutionComponent>((source) => new ClientTakeDocumentQueryExecutionComponent(
                    source,
                    topContinuationToken.Top,
                    TakeEnum.Top));
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

                string updatedContinuationToken;
                if (!this.IsDone && (sourcePage.DisallowContinuationTokenMessage == null))
                {
                    switch (this.takeEnum)
                    {
                        case TakeEnum.Limit:
                            updatedContinuationToken = new LimitContinuationToken(
                                limit: this.takeCount,
                                sourceToken: sourcePage.ContinuationToken).ToString();
                            break;

                        case TakeEnum.Top:
                            updatedContinuationToken = new TopContinuationToken(
                                top: this.takeCount,
                                sourceToken: sourcePage.ContinuationToken).ToString();
                            break;

                        default:
                            throw new ArgumentOutOfRangeException($"Unknown {nameof(TakeEnum)}: {this.takeEnum}.");
                    }
                }
                else
                {
                    updatedContinuationToken = null;
                }

                return QueryResponseCore.CreateSuccess(
                    result: takedDocuments,
                    continuationToken: updatedContinuationToken,
                    disallowContinuationTokenMessage: sourcePage.DisallowContinuationTokenMessage,
                    activityId: sourcePage.ActivityId,
                    requestCharge: sourcePage.RequestCharge,
                    responseLengthBytes: sourcePage.ResponseLengthBytes);
            }

            public override CosmosElement GetCosmosElementContinuationToken()
            {
                throw new NotImplementedException();
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
                    TakeContinuationToken takeContinuationToken;
                    switch (this.takeEnum)
                    {
                        case TakeEnum.Limit:
                            takeContinuationToken = new LimitContinuationToken(
                                this.takeCount,
                                tokenEPKRange.GetContinuation());
                            break;

                        case TakeEnum.Top:
                            takeContinuationToken = new TopContinuationToken(
                                this.takeCount,
                                tokenEPKRange.GetContinuation());
                            break;

                        default:
                            throw new ArgumentException($"Unknown {nameof(TakeEnum)}: {this.takeEnum}");
                    }

                    feedToken = new QueryFeedTokenInternal(FeedTokenEPKRange.Copy(
                            tokenEPKRange,
                            takeContinuationToken.ToString()),
                    feedTokenInternal.QueryDefinition);
                }

                return true;
            }

            private enum TakeEnum
            {
                Limit,
                Top
            }

            private abstract class TakeContinuationToken
            {
            }

            /// <summary>
            /// A LimitContinuationToken is a composition of a source continuation token and how many items we have left to drain from that source.
            /// </summary>
            private sealed class LimitContinuationToken : TakeContinuationToken
            {
                /// <summary>
                /// Initializes a new instance of the LimitContinuationToken struct.
                /// </summary>
                /// <param name="limit">The limit to the number of document drained for the remainder of the query.</param>
                /// <param name="sourceToken">The continuation token for the source component of the query.</param>
                public LimitContinuationToken(int limit, string sourceToken)
                {
                    if (limit < 0)
                    {
                        throw new ArgumentException($"{nameof(limit)} must be a non negative number.");
                    }

                    this.Limit = limit;
                    this.SourceToken = sourceToken;
                }

                /// <summary>
                /// Gets the limit to the number of document drained for the remainder of the query.
                /// </summary>
                [JsonProperty("limit")]
                public int Limit
                {
                    get;
                }

                /// <summary>
                /// Gets the continuation token for the source component of the query.
                /// </summary>
                [JsonProperty("sourceToken")]
                public string SourceToken
                {
                    get;
                }

                /// <summary>
                /// Tries to parse out the LimitContinuationToken.
                /// </summary>
                /// <param name="value">The value to parse from.</param>
                /// <param name="limitContinuationToken">The result of parsing out the token.</param>
                /// <returns>Whether or not the LimitContinuationToken was successfully parsed out.</returns>
                public static bool TryParse(string value, out LimitContinuationToken limitContinuationToken)
                {
                    limitContinuationToken = default;
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return false;
                    }

                    try
                    {
                        limitContinuationToken = JsonConvert.DeserializeObject<LimitContinuationToken>(value);
                        return true;
                    }
                    catch (JsonException)
                    {
                        return false;
                    }
                }

                /// <summary>
                /// Gets the string version of the continuation token that can be passed in a response header.
                /// </summary>
                /// <returns>The string version of the continuation token that can be passed in a response header.</returns>
                public override string ToString()
                {
                    return JsonConvert.SerializeObject(this);
                }
            }

            /// <summary>
            /// A TopContinuationToken is a composition of a source continuation token and how many items we have left to drain from that source.
            /// </summary>
            private sealed class TopContinuationToken : TakeContinuationToken
            {
                /// <summary>
                /// Initializes a new instance of the TopContinuationToken struct.
                /// </summary>
                /// <param name="top">The limit to the number of document drained for the remainder of the query.</param>
                /// <param name="sourceToken">The continuation token for the source component of the query.</param>
                public TopContinuationToken(int top, string sourceToken)
                {
                    this.Top = top;
                    this.SourceToken = sourceToken;
                }

                /// <summary>
                /// Gets the limit to the number of document drained for the remainder of the query.
                /// </summary>
                [JsonProperty("top")]
                public int Top
                {
                    get;
                }

                /// <summary>
                /// Gets the continuation token for the source component of the query.
                /// </summary>
                [JsonProperty("sourceToken")]
                public string SourceToken
                {
                    get;
                }

                /// <summary>
                /// Tries to parse out the TopContinuationToken.
                /// </summary>
                /// <param name="value">The value to parse from.</param>
                /// <param name="topContinuationToken">The result of parsing out the token.</param>
                /// <returns>Whether or not the TopContinuationToken was successfully parsed out.</returns>
                public static bool TryParse(string value, out TopContinuationToken topContinuationToken)
                {
                    topContinuationToken = default;
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return false;
                    }

                    try
                    {
                        topContinuationToken = JsonConvert.DeserializeObject<TopContinuationToken>(value);
                        return true;
                    }
                    catch (JsonException)
                    {
                        return false;
                    }
                }

                /// <summary>
                /// Gets the string version of the continuation token that can be passed in a response header.
                /// </summary>
                /// <returns>The string version of the continuation token that can be passed in a response header.</returns>
                public override string ToString()
                {
                    return JsonConvert.SerializeObject(this);
                }
            }
        }
    }
}