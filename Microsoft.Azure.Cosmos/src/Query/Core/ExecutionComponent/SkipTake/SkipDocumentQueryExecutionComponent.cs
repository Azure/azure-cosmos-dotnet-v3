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

    internal sealed class SkipDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private int skipCount;

        private SkipDocumentQueryExecutionComponent(IDocumentQueryExecutionComponent source, int skipCount)
            : base(source)
        {
            this.skipCount = skipCount;
        }

        public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateAsync(
            int offsetCount,
            string continuationToken,
            Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
        {
            if (tryCreateSourceAsync == null)
            {
                throw new ArgumentNullException(nameof(tryCreateSourceAsync));
            }

            OffsetContinuationToken offsetContinuationToken;
            if (continuationToken != null)
            {
                if (!OffsetContinuationToken.TryParse(continuationToken, out offsetContinuationToken))
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                        new MalformedContinuationTokenException($"Invalid {nameof(SkipDocumentQueryExecutionComponent)}: {continuationToken}."));
                }
            }
            else
            {
                offsetContinuationToken = new OffsetContinuationToken(offsetCount, null);
            }

            if (offsetContinuationToken.Offset > offsetCount)
            {
                return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                        new MalformedContinuationTokenException("offset count in continuation token can not be greater than the offsetcount in the query."));
            }

            return (await tryCreateSourceAsync(offsetContinuationToken.SourceToken))
                .Try<IDocumentQueryExecutionComponent>((source) => new SkipDocumentQueryExecutionComponent(
                source,
                offsetContinuationToken.Offset));
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
            IReadOnlyList<CosmosElement> documentsAfterSkip = sourcePage.CosmosElements.Skip(this.skipCount).ToList();

            int numberOfDocumentsSkipped = sourcePage.CosmosElements.Count() - documentsAfterSkip.Count();
            this.skipCount -= numberOfDocumentsSkipped;
            string updatedContinuationToken = null;

            if (sourcePage.DisallowContinuationTokenMessage == null)
            {
                if (!this.TryGetContinuationToken(out updatedContinuationToken))
                {
                    throw new InvalidOperationException($"Failed to get state for {nameof(SkipDocumentQueryExecutionComponent)}.");
                }
            }

            return QueryResponseCore.CreateSuccess(
                    result: documentsAfterSkip,
                    continuationToken: updatedContinuationToken,
                    disallowContinuationTokenMessage: sourcePage.DisallowContinuationTokenMessage,
                    activityId: sourcePage.ActivityId,
                    requestCharge: sourcePage.RequestCharge,
                    diagnostics: sourcePage.Diagnostics,
                    responseLengthBytes: sourcePage.ResponseLengthBytes);
        }

        public override bool TryGetContinuationToken(out string state)
        {
            if (!this.IsDone)
            {
                if (this.Source.TryGetContinuationToken(out string sourceState))
                {
                    state = new OffsetContinuationToken(
                        this.skipCount,
                        sourceState).ToString();
                    return true;
                }
                else
                {
                    state = null;
                    return false;
                }
            }
            else
            {
                state = null;
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

            FeedTokenInternal feedTokenInternal = feedToken as FeedTokenInternal;
            feedTokenInternal.UpdateContinuation(new OffsetContinuationToken(
                    this.skipCount,
                    feedTokenInternal.GetContinuation()).ToString());
            return true;
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
            /// Tries to parse out the OffsetContinuationToken.
            /// </summary>
            /// <param name="value">The value to parse from.</param>
            /// <param name="offsetContinuationToken">The result of parsing out the token.</param>
            /// <returns>Whether or not the LimitContinuationToken was successfully parsed out.</returns>
            public static bool TryParse(string value, out OffsetContinuationToken offsetContinuationToken)
            {
                offsetContinuationToken = default;
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
                    DefaultTrace.TraceWarning($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)} Invalid continuation token {value} for offset~Component: {ex}");
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