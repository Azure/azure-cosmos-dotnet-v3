//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Take
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Newtonsoft.Json;

    internal abstract partial class TakeQueryPipelineStage : QueryPipelineStageBase
    {
        private sealed class ClientTakeQueryPipelineStage : TakeQueryPipelineStage
        {
            private readonly TakeEnum takeEnum;

            private ClientTakeQueryPipelineStage(
                IQueryPipelineStage source,
                int takeCount,
                TakeEnum takeEnum)
                : base(source, takeCount)
            {
                this.takeEnum = takeEnum;
            }

            public static new TryCatch<IQueryPipelineStage> MonadicCreateLimitStage(
                int limitCount,
                CosmosElement requestContinuationToken,
                MonadicCreatePipelineStage monadicCreatePipelineStage)
            {
                if (limitCount < 0)
                {
                    throw new ArgumentException($"{nameof(limitCount)}: {limitCount} must be a non negative number.");
                }

                if (monadicCreatePipelineStage == null)
                {
                    throw new ArgumentNullException(nameof(monadicCreatePipelineStage));
                }

                LimitContinuationToken limitContinuationToken;
                if (requestContinuationToken != null)
                {
                    if (!LimitContinuationToken.TryParse(requestContinuationToken.ToString(), out limitContinuationToken))
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
                            new MalformedContinuationTokenException(
                                $"Malformed {nameof(LimitContinuationToken)}: {requestContinuationToken}."));
                    }
                }
                else
                {
                    limitContinuationToken = new LimitContinuationToken(limitCount, null);
                }

                if (limitContinuationToken.Limit > limitCount)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(
                        new MalformedContinuationTokenException(
                            $"{nameof(LimitContinuationToken.Limit)} in {nameof(LimitContinuationToken)}: {requestContinuationToken}: {limitContinuationToken.Limit} can not be greater than the limit count in the query: {limitCount}."));
                }

                CosmosElement sourceToken;
                if (limitContinuationToken.SourceToken != null)
                {
                    TryCatch<CosmosElement> tryParse = CosmosElement.Monadic.Parse(limitContinuationToken.SourceToken);
                    if (tryParse.Failed)
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
                            new MalformedContinuationTokenException(
                                message: $"Malformed {nameof(LimitContinuationToken)}: {requestContinuationToken}.",
                                innerException: tryParse.Exception));
                    }

                    sourceToken = tryParse.Result;
                }
                else
                {
                    sourceToken = null;
                }

                TryCatch<IQueryPipelineStage> tryCreateSource = monadicCreatePipelineStage(sourceToken);
                if (tryCreateSource.Failed)
                {
                    return tryCreateSource;
                }

                IQueryPipelineStage stage = new ClientTakeQueryPipelineStage(
                    tryCreateSource.Result,
                    limitContinuationToken.Limit,
                    TakeEnum.Limit);

                return TryCatch<IQueryPipelineStage>.FromResult(stage);
            }

            public static new TryCatch<IQueryPipelineStage> MonadicCreateTopStage(
                int topCount,
                CosmosElement requestContinuationToken,
                MonadicCreatePipelineStage monadicCreatePipelineStage)
            {
                if (topCount < 0)
                {
                    throw new ArgumentException($"{nameof(topCount)}: {topCount} must be a non negative number.");
                }

                if (monadicCreatePipelineStage == null)
                {
                    throw new ArgumentNullException(nameof(monadicCreatePipelineStage));
                }

                TopContinuationToken topContinuationToken;
                if (requestContinuationToken != null)
                {
                    if (!TopContinuationToken.TryParse(requestContinuationToken.ToString(), out topContinuationToken))
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
                            new MalformedContinuationTokenException(
                                $"Malformed {nameof(LimitContinuationToken)}: {requestContinuationToken}."));
                    }
                }
                else
                {
                    topContinuationToken = new TopContinuationToken(topCount, null);
                }

                if (topContinuationToken.Top > topCount)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(
                        new MalformedContinuationTokenException(
                            $"{nameof(TopContinuationToken.Top)} in {nameof(TopContinuationToken)}: {requestContinuationToken}: {topContinuationToken.Top} can not be greater than the top count in the query: {topCount}."));
                }

                CosmosElement sourceToken;
                if (topContinuationToken.SourceToken != null)
                {
                    TryCatch<CosmosElement> tryParse = CosmosElement.Monadic.Parse(topContinuationToken.SourceToken);
                    if (tryParse.Failed)
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
                            new MalformedContinuationTokenException(
                                message: $"{nameof(TopContinuationToken.SourceToken)} in {nameof(TopContinuationToken)}: {requestContinuationToken}: {topContinuationToken.SourceToken ?? "<null>"} was malformed.",
                                innerException: tryParse.Exception));
                    }

                    sourceToken = tryParse.Result;
                }
                else
                {
                    sourceToken = null;
                }

                TryCatch<IQueryPipelineStage> tryCreateSource = monadicCreatePipelineStage(sourceToken);
                if (tryCreateSource.Failed)
                {
                    return tryCreateSource;
                }

                IQueryPipelineStage stage = new ClientTakeQueryPipelineStage(
                    tryCreateSource.Result,
                    topContinuationToken.Top,
                    TakeEnum.Top);

                return TryCatch<IQueryPipelineStage>.FromResult(stage);
            }

            public override async ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (trace == null)
                {
                    throw new ArgumentNullException(nameof(trace));
                }

                if (this.ReturnedFinalPage || !await this.inputStage.MoveNextAsync(trace, cancellationToken))
                {
                    this.Current = default;
                    this.takeCount = 0;
                    return false;
                }

                TryCatch<QueryPage> tryGetSourcePage = this.inputStage.Current;
                if (tryGetSourcePage.Failed)
                {
                    this.Current = tryGetSourcePage;
                    return true;
                }

                QueryPage sourcePage = tryGetSourcePage.Result;

                List<CosmosElement> takedDocuments = sourcePage.Documents.Take(this.takeCount).ToList();
                this.takeCount -= takedDocuments.Count;

                QueryState state;
                if ((sourcePage.State != null) && (sourcePage.DisallowContinuationTokenMessage == null))
                {
                    string updatedContinuationToken = this.takeEnum switch
                    {
                        TakeEnum.Limit => new LimitContinuationToken(
                            limit: this.takeCount,
                            sourceToken: sourcePage.State?.Value.ToString()).ToString(),
                        TakeEnum.Top => new TopContinuationToken(
                            top: this.takeCount,
                            sourceToken: sourcePage.State?.Value.ToString()).ToString(),
                        _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(TakeEnum)}: {this.takeEnum}."),
                    };

                    state = new QueryState(CosmosElement.Parse(updatedContinuationToken));
                }
                else
                {
                    state = null;
                }

                QueryPage queryPage = new QueryPage(
                    documents: takedDocuments,
                    requestCharge: sourcePage.RequestCharge,
                    activityId: sourcePage.ActivityId,
                    cosmosQueryExecutionInfo: sourcePage.CosmosQueryExecutionInfo,
                    distributionPlanSpec: default,
                    disallowContinuationTokenMessage: sourcePage.DisallowContinuationTokenMessage,
                    additionalHeaders: sourcePage.AdditionalHeaders,
                    state: state,
                    streaming: sourcePage.Streaming);

                this.Current = TryCatch<QueryPage>.FromResult(queryPage);
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
