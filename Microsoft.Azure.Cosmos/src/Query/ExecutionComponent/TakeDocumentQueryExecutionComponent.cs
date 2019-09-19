//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
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
            if (takeCount < 0)
            {
                throw new ArgumentException($"{nameof(takeCount)} must be a non negative number.");
            }

            this.takeCount = takeCount;
            this.takeEnum = takeEnum;
        }

        public static async Task<TakeDocumentQueryExecutionComponent> CreateLimitDocumentQueryExecutionComponentAsync(
            CosmosQueryClient queryClient,
            int limitCount,
            string continuationToken,
            Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback)
        {
            if (queryClient == null)
            {
                throw new ArgumentNullException(nameof(queryClient));
            }

            LimitContinuationToken limitContinuationToken;
            if (continuationToken != null)
            {
                limitContinuationToken = LimitContinuationToken.Parse(queryClient, continuationToken);
            }
            else
            {
                limitContinuationToken = new LimitContinuationToken(limitCount, null);
            }

            if (limitContinuationToken.Limit > limitCount)
            {
                throw queryClient.CreateBadRequestException($"limit count in continuation token: {limitContinuationToken.Limit} can not be greater than the limit count in the query: {limitCount}.");
            }

            return new TakeDocumentQueryExecutionComponent(
                await createSourceCallback(limitContinuationToken.SourceToken),
                limitContinuationToken.Limit,
                TakeEnum.Limit);
        }

        public static async Task<TakeDocumentQueryExecutionComponent> CreateTopDocumentQueryExecutionComponentAsync(
            CosmosQueryClient queryClient,
            int topCount,
            string continuationToken,
            Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback)
        {
            if (queryClient == null)
            {
                throw new ArgumentNullException(nameof(queryClient));
            }

            TopContinuationToken topContinuationToken;
            if (continuationToken != null)
            {
                topContinuationToken = TopContinuationToken.Parse(queryClient, continuationToken);
            }
            else
            {
                topContinuationToken = new TopContinuationToken(topCount, null);
            }

            if (topContinuationToken.Top > topCount)
            {
                throw queryClient.CreateBadRequestException($"top count in continuation token: {topContinuationToken.Top} can not be greater than the top count in the query: {topCount}.");
            }

            return new TakeDocumentQueryExecutionComponent(
                await createSourceCallback(topContinuationToken.SourceToken),
                topContinuationToken.Top,
                TakeEnum.Top);
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
                if (!this.IsDone)
                {
                    string sourceContinuation = results.ContinuationToken;
                    TakeContinuationToken takeContinuationToken;
                    switch (this.takeEnum)
                    {
                        case TakeEnum.Limit:
                            takeContinuationToken = new LimitContinuationToken(
                                this.takeCount,
                                sourceContinuation);
                            break;

                        case TakeEnum.Top:
                            takeContinuationToken = new TopContinuationToken(
                                this.takeCount,
                                sourceContinuation);
                            break;

                        default:
                            throw new ArgumentException($"Unknown {nameof(TakeEnum)}: {this.takeEnum}");
                    }

                    updatedContinuationToken = takeContinuationToken.ToString();
                }
            }

            return QueryResponseCore.CreateSuccess(
                    result: takedDocuments,
                    continuationToken: updatedContinuationToken,
                    disallowContinuationTokenMessage: results.DisallowContinuationTokenMessage,
                    activityId: results.ActivityId,
                    requestCharge: results.RequestCharge,
                    queryMetricsText: results.QueryMetricsText,
                    queryMetrics: results.QueryMetrics,
                    requestStatistics: results.RequestStatistics,
                    responseLengthBytes: results.ResponseLengthBytes);
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
            /// Parses the LimitContinuationToken from it's string form.
            /// </summary>
            /// <param name="queryClient">The query client</param>
            /// <param name="value">The string form to parse from.</param>
            /// <returns>The parsed LimitContinuationToken.</returns>
            public static LimitContinuationToken Parse(CosmosQueryClient queryClient, string value)
            {
                LimitContinuationToken result;
                if (!TryParse(value, out result))
                {
                    throw queryClient.CreateBadRequestException($"Invalid LimitContinuationToken: {value}");
                }
                else
                {
                    return result;
                }
            }

            /// <summary>
            /// Tries to parse out the LimitContinuationToken.
            /// </summary>
            /// <param name="value">The value to parse from.</param>
            /// <param name="limitContinuationToken">The result of parsing out the token.</param>
            /// <returns>Whether or not the LimitContinuationToken was successfully parsed out.</returns>
            public static bool TryParse(string value, out LimitContinuationToken limitContinuationToken)
            {
                limitContinuationToken = default(LimitContinuationToken);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                try
                {
                    limitContinuationToken = JsonConvert.DeserializeObject<LimitContinuationToken>(value);
                    return true;
                }
                catch (JsonException ex)
                {
                    DefaultTrace.TraceWarning(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} Invalid continuation token {1} for limit~Component, exception: {2}",
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
            /// Parses the TopContinuationToken from it's string form.
            /// </summary>
            /// <param name="queryClient">The query client</param>
            /// <param name="value">The string form to parse from.</param>
            /// <returns>The parsed TopContinuationToken.</returns>
            public static TopContinuationToken Parse(CosmosQueryClient queryClient, string value)
            {
                TopContinuationToken result;
                if (!TryParse(value, out result))
                {
                    throw queryClient.CreateBadRequestException($"Invalid TopContinuationToken: {value}");
                }
                else
                {
                    return result;
                }
            }

            /// <summary>
            /// Tries to parse out the TopContinuationToken.
            /// </summary>
            /// <param name="value">The value to parse from.</param>
            /// <param name="topContinuationToken">The result of parsing out the token.</param>
            /// <returns>Whether or not the TopContinuationToken was successfully parsed out.</returns>
            public static bool TryParse(string value, out TopContinuationToken topContinuationToken)
            {
                topContinuationToken = default(TopContinuationToken);
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
                        "{0} Invalid continuation token {1} for Top~Component, exception: {2}",
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