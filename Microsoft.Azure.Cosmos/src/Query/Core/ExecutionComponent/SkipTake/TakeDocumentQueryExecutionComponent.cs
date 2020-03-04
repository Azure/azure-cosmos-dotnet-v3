//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.SkipTake
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Newtonsoft.Json;

    internal sealed class TakeDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private readonly TakeEnum takeEnum;
        private int takeCount;

        private TakeDocumentQueryExecutionComponent(
            IDocumentQueryExecutionComponent source,
            int takeCount,
            TakeEnum takeEnum)
            : base(source)
        {
            this.takeCount = takeCount;
            this.takeEnum = takeEnum;
        }

        public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateLimitDocumentQueryExecutionComponentAsync(
            int limitCount,
            string continuationToken,
            Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
        {
            if (tryCreateSourceAsync == null)
            {
                throw new ArgumentNullException(nameof(tryCreateSourceAsync));
            }

            LimitContinuationToken limitContinuationToken;
            if (continuationToken != null)
            {
                if (!LimitContinuationToken.TryParse(continuationToken, out limitContinuationToken))
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                        new MalformedContinuationTokenException($"Malformed {nameof(LimitContinuationToken)}: {continuationToken}."));
                }
            }
            else
            {
                limitContinuationToken = new LimitContinuationToken(limitCount, null);
            }

            if (limitContinuationToken.Limit > limitCount)
            {
                return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                    new MalformedContinuationTokenException($"{nameof(LimitContinuationToken.Limit)} in {nameof(LimitContinuationToken)}: {continuationToken}: {limitContinuationToken.Limit} can not be greater than the limit count in the query: {limitCount}."));
            }

            if (limitCount < 0)
            {
                return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                        new MalformedContinuationTokenException($"{nameof(limitCount)}: {limitCount} must be a non negative number."));
            }

            return (await tryCreateSourceAsync(limitContinuationToken.SourceToken))
                .Try<IDocumentQueryExecutionComponent>((source) => new TakeDocumentQueryExecutionComponent(
                source,
                limitContinuationToken.Limit,
                TakeEnum.Limit));
        }

        public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateTopDocumentQueryExecutionComponentAsync(
            int topCount,
            string continuationToken,
            Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
        {
            if (tryCreateSourceAsync == null)
            {
                throw new ArgumentNullException(nameof(tryCreateSourceAsync));
            }

            TopContinuationToken topContinuationToken;
            if (continuationToken != null)
            {
                if (!TopContinuationToken.TryParse(continuationToken, out topContinuationToken))
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                        new MalformedContinuationTokenException($"Malformed {nameof(LimitContinuationToken)}: {continuationToken}."));
                }
            }
            else
            {
                topContinuationToken = new TopContinuationToken(topCount, null);
            }

            if (topContinuationToken.Top > topCount)
            {
                return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                    new MalformedContinuationTokenException($"{nameof(TopContinuationToken.Top)} in {nameof(TopContinuationToken)}: {continuationToken}: {topContinuationToken.Top} can not be greater than the top count in the query: {topCount}."));
            }

            if (topCount < 0)
            {
                return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                        new MalformedContinuationTokenException($"{nameof(topCount)}: {topCount} must be a non negative number."));
            }

            return (await tryCreateSourceAsync(topContinuationToken.SourceToken))
                .Try<IDocumentQueryExecutionComponent>((source) => new TakeDocumentQueryExecutionComponent(
                source,
                topContinuationToken.Top,
                TakeEnum.Top));
        }

        public override bool IsDone
        {
            get
            {
                return this.Source.IsDone || this.takeCount <= 0;
            }
        }

        public override async Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            QueryResponseCore results = await base.DrainAsync(maxElements, token);
            if (!results.IsSuccess)
            {
                return results;
            }

            List<CosmosElement> takedDocuments = results.CosmosElements.Take(this.takeCount).ToList();
            this.takeCount -= takedDocuments.Count;

            string updatedContinuationToken = null;
            if (results.DisallowContinuationTokenMessage == null)
            {
                if (!this.TryGetContinuationToken(out updatedContinuationToken))
                {
                    throw new InvalidOperationException($"Failed to get state for {nameof(TakeDocumentQueryExecutionComponent)}.");
                }
            }

            return QueryResponseCore.CreateSuccess(
                    result: takedDocuments,
                    continuationToken: updatedContinuationToken,
                    disallowContinuationTokenMessage: results.DisallowContinuationTokenMessage,
                    activityId: results.ActivityId,
                    requestCharge: results.RequestCharge,
                    diagnostics: results.Diagnostics,
                    responseLengthBytes: results.ResponseLengthBytes);
        }

        public override bool TryGetContinuationToken(out string state)
        {
            if (!this.IsDone)
            {
                if (this.Source.TryGetContinuationToken(out string sourceState))
                {
                    TakeContinuationToken takeContinuationToken;
                    switch (this.takeEnum)
                    {
                        case TakeEnum.Limit:
                            takeContinuationToken = new LimitContinuationToken(
                                this.takeCount,
                                sourceState);
                            break;

                        case TakeEnum.Top:
                            takeContinuationToken = new TopContinuationToken(
                                this.takeCount,
                                sourceState);
                            break;

                        default:
                            throw new ArgumentException($"Unknown {nameof(TakeEnum)}: {this.takeEnum}");
                    }

                    state = takeContinuationToken.ToString();
                    return true;
                }
                else
                {
                    state = default;
                    return false;
                }
            }
            else
            {
                state = default;
                return true;
            }
        }

        public override bool TryGetFeedToken(out FeedToken feedToken)
        {
            if (this.IsDone)
            {
                feedToken = null;
                return true;
            }

            if (!this.Source.TryGetFeedToken(out feedToken))
            {
                feedToken = null;
                return false;
            }

            FeedTokenEPKRange feedTokenInternal = feedToken as FeedTokenEPKRange;
            TakeContinuationToken takeContinuationToken;
            switch (this.takeEnum)
            {
                case TakeEnum.Limit:
                    takeContinuationToken = new LimitContinuationToken(
                        this.takeCount,
                        feedTokenInternal.GetContinuation());
                    break;

                case TakeEnum.Top:
                    takeContinuationToken = new TopContinuationToken(
                        this.takeCount,
                        feedTokenInternal.GetContinuation());
                    break;

                default:
                    throw new ArgumentException($"Unknown {nameof(TakeEnum)}: {this.takeEnum}");
            }

            feedToken = FeedTokenEPKRange.Copy(
                    feedTokenInternal,
                    takeContinuationToken.ToString());
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
                catch (JsonException ex)
                {
                    DefaultTrace.TraceWarning(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} Invalid continuation token {1} for Top~Component: {2}",
                        DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                        value,
                        ex.Message));
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