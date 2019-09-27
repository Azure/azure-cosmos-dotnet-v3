//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Newtonsoft.Json;

    internal sealed class SkipDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private int skipCount;

        private SkipDocumentQueryExecutionComponent(IDocumentQueryExecutionComponent source, int skipCount)
            : base(source)
        {
            this.skipCount = skipCount;
        }

        public static async Task<SkipDocumentQueryExecutionComponent> CreateAsync(
            int offsetCount,
            string continuationToken,
            Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback)
        {
            OffsetContinuationToken offsetContinuationToken;
            if (continuationToken != null)
            {
                offsetContinuationToken = OffsetContinuationToken.Parse(continuationToken);
            }
            else
            {
                offsetContinuationToken = new OffsetContinuationToken(offsetCount, null);
            }

            if (offsetContinuationToken.Offset > offsetCount)
            {
                throw new ArgumentException("offset count in continuation token can not be greater than the offsetcount in the query.");
            }

            return new SkipDocumentQueryExecutionComponent(
                await createSourceCallback(offsetContinuationToken.SourceToken),
                offsetContinuationToken.Offset);
        }

        public override bool IsDone
        {
            get
            {
                return this.Source.IsDone;
            }
        }

        public override async Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            QueryResponseCore sourcePage = await base.DrainAsync(maxElements, token);
            if (!sourcePage.IsSuccess)
            {
                return sourcePage;
            }

            // skip the documents but keep all the other headers
            List<CosmosElement> documentsAfterSkip = sourcePage.CosmosElements.Skip(this.skipCount).ToList();

            int numberOfDocumentsSkipped = sourcePage.CosmosElements.Count - documentsAfterSkip.Count;
            this.skipCount -= numberOfDocumentsSkipped;
            string updatedContinuationToken = null;

            if (sourcePage.DisallowContinuationTokenMessage == null)
            {
                if (!this.IsDone)
                {
                    updatedContinuationToken = new OffsetContinuationToken(
                        this.skipCount,
                        sourcePage.ContinuationToken).ToString();
                }
            }

            return QueryResponseCore.CreateSuccess(
                    result: documentsAfterSkip,
                    continuationToken: updatedContinuationToken,
                    disallowContinuationTokenMessage: sourcePage.DisallowContinuationTokenMessage,
                    activityId: sourcePage.ActivityId,
                    requestCharge: sourcePage.RequestCharge,
                    queryMetricsText: sourcePage.QueryMetricsText,
                    queryMetrics: sourcePage.QueryMetrics,
                    requestStatistics: sourcePage.RequestStatistics,
                    responseLengthBytes: sourcePage.ResponseLengthBytes);
        }

        /// <summary>
        /// A OffsetContinuationToken is a composition of a source continuation token and how many items to skip from that source.
        /// </summary>
        private struct OffsetContinuationToken
        {
            /// <summary>
            /// Initializes a new instance of the OffsetContinuationToken struct.
            /// </summary>
            /// <param name="offset">The number of items to skip in the query.</param>
            /// <param name="sourceToken">The continuation token for the source component of the query.</param>
            public OffsetContinuationToken(int offset, string sourceToken)
            {
                if (offset < 0)
                {
                    throw new ArgumentException($"{nameof(offset)} must be a non negative number.");
                }

                this.Offset = offset;
                this.SourceToken = sourceToken;
            }

            /// <summary>
            /// The number of items to skip in the query.
            /// </summary>
            [JsonProperty("offset")]
            public int Offset
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
            /// Parses the OffsetContinuationToken from it's string form.
            /// </summary>
            /// <param name="value">The string form to parse from.</param>
            /// <returns>The parsed OffsetContinuationToken.</returns>
            public static OffsetContinuationToken Parse(string value)
            {
                OffsetContinuationToken result;
                if (!TryParse(value, out result))
                {
                    throw new ArgumentException($"Invalid OffsetContinuationToken: {value}");
                }
                else
                {
                    return result;
                }
            }

            /// <summary>
            /// Tries to parse out the OffsetContinuationToken.
            /// </summary>
            /// <param name="value">The value to parse from.</param>
            /// <param name="offsetContinuationToken">The result of parsing out the token.</param>
            /// <returns>Whether or not the LimitContinuationToken was successfully parsed out.</returns>
            public static bool TryParse(string value, out OffsetContinuationToken offsetContinuationToken)
            {
                offsetContinuationToken = default(OffsetContinuationToken);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                try
                {
                    offsetContinuationToken = JsonConvert.DeserializeObject<OffsetContinuationToken>(value);
                    return true;
                }
                catch (JsonException ex)
                {
                    DefaultTrace.TraceWarning($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)} Invalid continuation token {value} for offset~Component, exception: {ex}");
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