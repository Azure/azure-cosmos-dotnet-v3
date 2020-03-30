//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.SkipTake
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Documents;

    internal abstract partial class SkipDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private sealed class ComputeSkipDocumentQueryExecutionComponent : SkipDocumentQueryExecutionComponent
        {
            private ComputeSkipDocumentQueryExecutionComponent(IDocumentQueryExecutionComponent source, long skipCount)
                : base(source, skipCount)
            {
                // Work is done in base constructor.
            }

            public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateAsync(
                int offsetCount,
                CosmosElement continuationToken,
                Func<CosmosElement, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
            {
                if (tryCreateSourceAsync == null)
                {
                    throw new ArgumentNullException(nameof(tryCreateSourceAsync));
                }

                OffsetContinuationToken offsetContinuationToken;
                if (continuationToken != null)
                {
                    (bool parsed, OffsetContinuationToken parsedToken) = OffsetContinuationToken.TryParse(continuationToken);
                    if (!parsed)
                    {
                        return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException($"Invalid {nameof(SkipDocumentQueryExecutionComponent)}: {continuationToken}."));
                    }

                    offsetContinuationToken = parsedToken;
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
                    .Try<IDocumentQueryExecutionComponent>((source) => new ComputeSkipDocumentQueryExecutionComponent(
                    source,
                    offsetContinuationToken.Offset));
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

                return QueryResponseCore.CreateSuccess(
                    result: documentsAfterSkip,
                    continuationToken: null,
                    disallowContinuationTokenMessage: DocumentQueryExecutionComponentBase.UseCosmosElementContinuationTokenInstead,
                    activityId: sourcePage.ActivityId,
                    requestCharge: sourcePage.RequestCharge,
                    responseLengthBytes: sourcePage.ResponseLengthBytes);
            }

            public override CosmosElement GetCosmosElementContinuationToken()
            {
                if (this.IsDone)
                {
                    return default;
                }

                OffsetContinuationToken offsetContinuationToken = new OffsetContinuationToken(
                    offset: this.skipCount,
                    sourceToken: this.Source.GetCosmosElementContinuationToken());
                return OffsetContinuationToken.ToCosmosElement(offsetContinuationToken);
            }

            public override bool TryGetFeedToken(
                string containerResourceId,
                out FeedToken feedToken)
            {
                if (this.IsDone)
                {
                    feedToken = null;
                    return true;
                }

                if (!this.Source.TryGetFeedToken(containerResourceId, out feedToken))
                {
                    feedToken = null;
                    return false;
                }

                if (feedToken is FeedTokenEPKRange feedTokenInternal)
                {
                    feedToken = FeedTokenEPKRange.Copy(
                            feedTokenInternal,
                            OffsetContinuationToken.ToCosmosElement(new OffsetContinuationToken(
                                this.skipCount,
                                this.Source.GetCosmosElementContinuationToken())).ToString());
                }

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