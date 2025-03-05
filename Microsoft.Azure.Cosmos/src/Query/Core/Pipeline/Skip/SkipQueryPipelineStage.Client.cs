// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Skip
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Newtonsoft.Json;

    internal abstract partial class SkipQueryPipelineStage : QueryPipelineStageBase
    {
        private sealed class ClientSkipQueryPipelineStage : SkipQueryPipelineStage
        {
            private ClientSkipQueryPipelineStage(
                IQueryPipelineStage source,
                long skipCount)
                : base(source, skipCount)
            {
                // Work is done in base constructor.
            }

            public static new TryCatch<IQueryPipelineStage> MonadicCreate(
                int offsetCount,
                CosmosElement continuationToken,
                MonadicCreatePipelineStage monadicCreatePipelineStage)
            {
                if (monadicCreatePipelineStage == null)
                {
                    throw new ArgumentNullException(nameof(monadicCreatePipelineStage));
                }

                OffsetContinuationToken offsetContinuationToken;
                if (continuationToken != null)
                {
                    if (!OffsetContinuationToken.TryParse(continuationToken.ToString(), out offsetContinuationToken))
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
                            new MalformedContinuationTokenException(
                                $"Invalid {nameof(SkipQueryPipelineStage)}: {continuationToken}."));
                    }
                }
                else
                {
                    offsetContinuationToken = new OffsetContinuationToken(offsetCount, null);
                }

                if (offsetContinuationToken.Offset > offsetCount)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(
                        new MalformedContinuationTokenException(
                            "offset count in continuation token can not be greater than the offsetcount in the query."));
                }

                CosmosElement sourceToken;
                if (offsetContinuationToken.SourceToken != null)
                {
                    TryCatch<CosmosElement> tryParse = CosmosElement.Monadic.Parse(offsetContinuationToken.SourceToken);
                    if (tryParse.Failed)
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
                            new MalformedContinuationTokenException(
                                message: $"source token: '{offsetContinuationToken.SourceToken ?? "<null>"}' is not valid.",
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

                IQueryPipelineStage stage = new ClientSkipQueryPipelineStage(
                    tryCreateSource.Result,
                    offsetContinuationToken.Offset);

                return TryCatch<IQueryPipelineStage>.FromResult(stage);
            }

            public override async ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (trace == null)
                {
                    throw new ArgumentNullException(nameof(trace));
                }

                if (!await this.inputStage.MoveNextAsync(trace, cancellationToken))
                {
                    this.Current = default;
                    return false;
                }

                TryCatch<QueryPage> tryGetSourcePage = this.inputStage.Current;
                if (tryGetSourcePage.Failed)
                {
                    this.Current = tryGetSourcePage;
                    return true;
                }

                QueryPage sourcePage = tryGetSourcePage.Result;

                // Skip the documents but keep all the other headers
                IReadOnlyList<CosmosElement> documentsAfterSkip = sourcePage.Documents.Skip(this.skipCount).ToList();

                int numberOfDocumentsSkipped = sourcePage.Documents.Count - documentsAfterSkip.Count;
                this.skipCount -= numberOfDocumentsSkipped;

                Debug.Assert(this.skipCount >= 0, $"{nameof(SkipQueryPipelineStage)} Assert!", "this.skipCount should be greater than or equal to 0");

                QueryState state;
                if ((sourcePage.State != null) && (sourcePage.DisallowContinuationTokenMessage == null))
                {
                    string token = new OffsetContinuationToken(
                        offset: this.skipCount,
                        sourceToken: sourcePage.State?.Value.ToString()).ToString();
                    state = new QueryState(CosmosElement.Parse(token));
                }
                else
                {
                    state = null;
                }

                QueryPage queryPage = new QueryPage(
                    documents: documentsAfterSkip,
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

            /// <summary>
            /// A OffsetContinuationToken is a composition of a source continuation token and how many items to skip from that source.
            /// </summary>
            private readonly struct OffsetContinuationToken
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
                public int Offset { get; }

                /// <summary>
                /// Gets the continuation token for the source component of the query.
                /// </summary>
                [JsonProperty("sourceToken")]
                public string SourceToken { get; }

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
