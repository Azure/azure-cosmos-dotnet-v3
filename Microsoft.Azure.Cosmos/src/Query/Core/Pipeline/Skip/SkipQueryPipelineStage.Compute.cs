// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Skip
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

    internal abstract partial class SkipQueryPipelineStage : QueryPipelineStageBase
    {
        private sealed class ComputeSkipQueryPipelineStage : SkipQueryPipelineStage
        {
            private ComputeSkipQueryPipelineStage(IQueryPipelineStage source, CancellationToken cancellationToken, long skipCount)
                : base(source, cancellationToken, skipCount)
            {
                // Work is done in base constructor.
            }

            public static TryCatch<IQueryPipelineStage> MonadicCreate(
                int offsetCount,
                CosmosElement continuationToken,
                CancellationToken cancellationToken,
                MonadicCreatePipelineStage monadicCreatePipelineStage)
            {
                if (monadicCreatePipelineStage == null)
                {
                    throw new ArgumentNullException(nameof(monadicCreatePipelineStage));
                }

                OffsetContinuationToken offsetContinuationToken;
                if (continuationToken != null)
                {
                    (bool parsed, OffsetContinuationToken parsedToken) = OffsetContinuationToken.TryParse(continuationToken);
                    if (!parsed)
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
                            new MalformedContinuationTokenException($"Invalid {nameof(SkipQueryPipelineStage)}: {continuationToken}."));
                    }

                    offsetContinuationToken = parsedToken;
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

                TryCatch<IQueryPipelineStage> tryCreateSource = monadicCreatePipelineStage(offsetContinuationToken.SourceToken, cancellationToken);
                if (tryCreateSource.Failed)
                {
                    return tryCreateSource;
                }

                IQueryPipelineStage stage = new ComputeSkipQueryPipelineStage(
                    tryCreateSource.Result,
                    cancellationToken,
                    offsetContinuationToken.Offset);

                return TryCatch<IQueryPipelineStage>.FromResult(stage);
            }

            public override async ValueTask<bool> MoveNextAsync()
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                if (!await this.inputStage.MoveNextAsync())
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

                int numberOfDocumentsSkipped = sourcePage.Documents.Count() - documentsAfterSkip.Count();
                this.skipCount -= numberOfDocumentsSkipped;

                QueryState state;
                if (sourcePage.State == null)
                {
                    state = default;
                }
                else
                {
                    OffsetContinuationToken offsetContinuationToken = new OffsetContinuationToken(
                        offset: this.skipCount,
                        sourceToken: sourcePage.State.Value);

                    state = new QueryState(OffsetContinuationToken.ToCosmosElement(offsetContinuationToken));
                }

                QueryPage queryPage = new QueryPage(
                    documents: documentsAfterSkip,
                    requestCharge: sourcePage.RequestCharge,
                    activityId: sourcePage.ActivityId,
                    responseLengthInBytes: sourcePage.ResponseLengthInBytes,
                    cosmosQueryExecutionInfo: sourcePage.CosmosQueryExecutionInfo,
                    disallowContinuationTokenMessage: sourcePage.DisallowContinuationTokenMessage,
                    state: state);

                this.Current = TryCatch<QueryPage>.FromResult(queryPage);
                return true;
            }

            /// <summary>
            /// A OffsetContinuationToken is a composition of a source continuation token and how many items to skip from that source.
            /// </summary>
            private readonly struct OffsetContinuationToken
            {
                private static class ProperytNames
                {
                    public const string SkipCountProperty = "SkipCount";
                    public const string SourceTokenProperty = "SourceToken";
                }

                /// <summary>
                /// Initializes a new instance of the OffsetContinuationToken struct.
                /// </summary>
                /// <param name="offset">The number of items to skip in the query.</param>
                /// <param name="sourceToken">The continuation token for the source component of the query.</param>
                public OffsetContinuationToken(long offset, CosmosElement sourceToken)
                {
                    if ((offset < 0) || (offset > int.MaxValue))
                    {
                        throw new ArgumentOutOfRangeException(nameof(offset));
                    }

                    this.Offset = (int)offset;
                    this.SourceToken = sourceToken;
                }

                /// <summary>
                /// The number of items to skip in the query.
                /// </summary>
                public int Offset
                {
                    get;
                }

                /// <summary>
                /// Gets the continuation token for the source component of the query.
                /// </summary>
                public CosmosElement SourceToken
                {
                    get;
                }

                public static CosmosElement ToCosmosElement(OffsetContinuationToken offsetContinuationToken)
                {
                    Dictionary<string, CosmosElement> dictionary = new Dictionary<string, CosmosElement>()
                    {
                        {
                            OffsetContinuationToken.ProperytNames.SkipCountProperty,
                            CosmosNumber64.Create(offsetContinuationToken.Offset)
                        },
                        {
                            OffsetContinuationToken.ProperytNames.SourceTokenProperty,
                            offsetContinuationToken.SourceToken
                        }
                    };

                    return CosmosObject.Create(dictionary);
                }

                public static (bool parsed, OffsetContinuationToken offsetContinuationToken) TryParse(CosmosElement value)
                {
                    if (value == null)
                    {
                        return (false, default);
                    }

                    if (!(value is CosmosObject cosmosObject))
                    {
                        return (false, default);
                    }

                    if (!cosmosObject.TryGetValue(OffsetContinuationToken.ProperytNames.SkipCountProperty, out CosmosNumber offset))
                    {
                        return (false, default);
                    }

                    if (!cosmosObject.TryGetValue(OffsetContinuationToken.ProperytNames.SourceTokenProperty, out CosmosElement sourceToken))
                    {
                        return (false, default);
                    }

                    return (true, new OffsetContinuationToken(Number64.ToLong(offset.Value), sourceToken));
                }
            }
        }
    }
}
