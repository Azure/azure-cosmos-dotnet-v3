// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Routing;
    using Newtonsoft.Json;

    [JsonConverter(typeof(FeedTokenInternalConverter))]
    internal sealed class FeedTokenEPKRange : FeedTokenInternal
    {
        internal readonly Queue<CompositeContinuationToken> CompositeContinuationTokens;
        internal readonly Documents.Routing.Range<string> CompleteRange;
        private CompositeContinuationToken currentToken;
        private string initialNoResultsRange;
        private HashSet<string> doneRanges;

        private FeedTokenEPKRange(
            string containerRid)
            : base(containerRid)
        {
            this.CompositeContinuationTokens = new Queue<CompositeContinuationToken>();
            this.doneRanges = new HashSet<string>();
        }

        private FeedTokenEPKRange(
            string containerRid,
            CompositeContinuationToken compositeContinuationTokenByPartitionKeyRangeId)
        : this(containerRid)
        {
            if (compositeContinuationTokenByPartitionKeyRangeId == null)
            {
                throw new ArgumentNullException(nameof(compositeContinuationTokenByPartitionKeyRangeId));
            }

            this.CompleteRange = compositeContinuationTokenByPartitionKeyRangeId.Range;
            this.CompositeContinuationTokens.Enqueue(compositeContinuationTokenByPartitionKeyRangeId);

            this.currentToken = this.CompositeContinuationTokens.Peek();
        }

        public static FeedTokenEPKRange Copy(
            FeedTokenEPKRange feedTokenEPKRange,
            string continuationToken)
        {
            return new FeedTokenEPKRange(
                feedTokenEPKRange.ContainerRid,
                feedTokenEPKRange.CompleteRange,
                continuationToken);
        }

        public FeedTokenEPKRange(
            string containerRid,
            IReadOnlyList<Documents.Routing.Range<string>> ranges,
            string continuationToken)
            : this(containerRid)
        {
            if (ranges == null)
            {
                throw new ArgumentNullException(nameof(ranges));
            }

            if (ranges.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ranges));
            }

            this.CompleteRange = new Documents.Routing.Range<string>(ranges[0].Min, ranges[ranges.Count - 1].Max, true, false);
            foreach (Documents.Routing.Range<string> range in ranges)
            {
                this.CompositeContinuationTokens.Enqueue(FeedTokenEPKRange.CreateCompositeContinuationTokenForRange(range.Min, range.Max, continuationToken));
            }

            this.currentToken = this.CompositeContinuationTokens.Peek();
        }

        public FeedTokenEPKRange(
            string containerRid,
            Documents.Routing.Range<string> completeRange,
            string continuationToken)
            : this(containerRid)
        {
            if (completeRange == null)
            {
                throw new ArgumentNullException(nameof(completeRange));
            }

            this.CompleteRange = completeRange;
            this.CompositeContinuationTokens.Enqueue(FeedTokenEPKRange.CreateCompositeContinuationTokenForRange(completeRange.Min, completeRange.Max, continuationToken));

            this.currentToken = this.CompositeContinuationTokens.Peek();
        }

        /// <summary>
        /// Used for deserialization only
        /// </summary>
        public FeedTokenEPKRange(
            string containerRid,
            Documents.Routing.Range<string> completeRange,
            IReadOnlyList<CompositeContinuationToken> deserializedTokens)
            : this(containerRid)
        {
            if (deserializedTokens == null)
            {
                throw new ArgumentNullException(nameof(deserializedTokens));
            }

            if (completeRange == null)
            {
                throw new ArgumentNullException(nameof(completeRange));
            }

            if (deserializedTokens.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deserializedTokens));
            }

            this.CompleteRange = completeRange;

            foreach (CompositeContinuationToken token in deserializedTokens)
            {
                this.CompositeContinuationTokens.Enqueue(token);
            }

            this.currentToken = this.CompositeContinuationTokens.Peek();
        }

        public override void EnrichRequest(RequestMessage request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // in case EPK has already been set
            if (!request.Properties.ContainsKey(HandlerConstants.StartEpkString))
            {
                request.Properties[HandlerConstants.StartEpkString] = this.currentToken.Range.Min;
                request.Properties[HandlerConstants.EndEpkString] = this.currentToken.Range.Max;
            }
        }

        public override string GetContinuation() => this.currentToken.Token;

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public override void UpdateContinuation(string continuationToken)
        {
            if (continuationToken == null)
            {
                // Queries and normal ReadFeed can signal termination by CT null, not NotModified
                // Change Feed never lands here, as it always provides a CT
                // Consider current range done, if this FeedToken contains multiple ranges due to splits, all of them need to be considered done
                this.doneRanges.Add(this.currentToken.Range.Min);
            }

            this.currentToken.Token = continuationToken;
            this.MoveToNextToken();
        }

        public override Task<List<Documents.Routing.Range<string>>> GetAffectedRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition)
        {
            return Task.FromResult(this.CompositeContinuationTokens.Select(token => token.Range).ToList());
        }

        public override async Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Documents.PartitionKeyRange> partitionKeyRanges = await routingMapProvider.TryGetOverlappingRangesAsync(containerRid, this.CompleteRange, forceRefresh: false);
            return partitionKeyRanges.Select(partitionKeyRange => partitionKeyRange.Id);
        }

        public override void ValidateContainer(string containerRid)
        {
            if (!string.IsNullOrEmpty(this.ContainerRid) &&
                this.ContainerRid != containerRid)
            {
                throw new ArgumentException(string.Format(ClientResources.FeedToken_InvalidFeedTokenForContainer, this.ContainerRid, containerRid));
            }
        }

        /// <summary>
        /// The concept of Done is only for Query and ReadFeed. Change Feed is never done, it is an infinite stream.
        /// </summary>
        public override bool IsDone => this.doneRanges.Count == this.CompositeContinuationTokens.Count;

        public override async Task<bool> ShouldRetryAsync(
            ContainerCore containerCore,
            ResponseMessage responseMessage,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (responseMessage.IsSuccessStatusCode)
            {
                this.initialNoResultsRange = null;
                return false;
            }

            // If the current response is NotModified (ChangeFeed), try and skip to a next one
            if (responseMessage.StatusCode == HttpStatusCode.NotModified
                && this.CompositeContinuationTokens.Count > 1)
            {
                if (this.initialNoResultsRange == null)
                {
                    this.initialNoResultsRange = this.currentToken.Range.Min;
                    return true;
                }

                return !this.initialNoResultsRange.Equals(this.currentToken.Range.Min, StringComparison.OrdinalIgnoreCase);
            }

            // Split handling
            bool partitionSplit = responseMessage.StatusCode == HttpStatusCode.Gone
                && (responseMessage.Headers.SubStatusCode == Documents.SubStatusCodes.PartitionKeyRangeGone || responseMessage.Headers.SubStatusCode == Documents.SubStatusCodes.CompletingSplit);
            if (partitionSplit)
            {
                Routing.PartitionKeyRangeCache partitionKeyRangeCache = await containerCore.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
                IReadOnlyList<Documents.PartitionKeyRange> resolvedRanges = await this.TryGetOverlappingRangesAsync(partitionKeyRangeCache, this.currentToken.Range.Min, this.currentToken.Range.Max, forceRefresh: true);
                if (resolvedRanges.Count > 0)
                {
                    this.HandleSplit(resolvedRanges);
                }

                return true;
            }

            return false;
        }

        public override IReadOnlyList<FeedToken> Scale()
        {
            if (this.CompositeContinuationTokens.Count <= 1)
            {
                return new List<FeedToken>();
            }

            return this.CompositeContinuationTokens.Select(token => new FeedTokenEPKRange(this.ContainerRid, token)).ToList();
        }

        public static bool TryParseInstance(string toStringValue, out FeedToken feedToken)
        {
            try
            {
                feedToken = JsonConvert.DeserializeObject<FeedTokenEPKRange>(toStringValue);
                return true;
            }
            catch
            {
                feedToken = null;
                return false;
            }
        }

        internal static CompositeContinuationToken CreateCompositeContinuationTokenForRange(
            string minInclusive,
            string maxExclusive,
            string token)
        {
            return new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>(minInclusive, maxExclusive, true, false),
                Token = token
            };
        }

        private void MoveToNextToken()
        {
            CompositeContinuationToken recentToken = this.CompositeContinuationTokens.Dequeue();
            this.CompositeContinuationTokens.Enqueue(recentToken);
            this.currentToken = this.CompositeContinuationTokens.Peek();

            // In a Query / ReadFeed not Change Feed, skip ranges that are done to avoid requests
            while (!this.IsDone &&
                this.doneRanges.Contains(this.currentToken.Range.Min))
            {
                this.MoveToNextToken();
            }
        }

        private void HandleSplit(IReadOnlyList<Documents.PartitionKeyRange> keyRanges)
        {
            if (keyRanges == null) throw new ArgumentNullException(nameof(keyRanges));

            // Update current
            Documents.PartitionKeyRange firstRange = keyRanges[0];
            this.currentToken.Range = new Documents.Routing.Range<string>(firstRange.MinInclusive, firstRange.MaxExclusive, true, false);
            // Add children
            foreach (Documents.PartitionKeyRange keyRange in keyRanges.Skip(1))
            {
                this.CompositeContinuationTokens.Enqueue(FeedTokenEPKRange.CreateCompositeContinuationTokenForRange(keyRange.MinInclusive, keyRange.MaxExclusive, this.currentToken.Token));
            }
        }

        private async Task<IReadOnlyList<Documents.PartitionKeyRange>> TryGetOverlappingRangesAsync(
            Routing.PartitionKeyRangeCache partitionKeyRangeCache,
            string min,
            string max,
            bool forceRefresh = false)
        {
            IReadOnlyList<Documents.PartitionKeyRange> keyRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                this.ContainerRid,
                new Documents.Routing.Range<string>(
                    min,
                    max,
                    isMaxInclusive: false,
                    isMinInclusive: true),
                forceRefresh);

            if (keyRanges.Count == 0)
            {
                throw new ArgumentOutOfRangeException("RequestContinuation", $"Token contains invalid range {min}-{max}");
            }

            return keyRanges;
        }
    }
}
