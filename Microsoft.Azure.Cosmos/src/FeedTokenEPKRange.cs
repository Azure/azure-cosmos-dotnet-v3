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
    using Newtonsoft.Json;

    [JsonConverter(typeof(FeedTokenInternalConverter))]
    internal sealed class FeedTokenEPKRange : FeedTokenInternal
    {
        internal readonly Queue<CompositeContinuationToken> CompositeContinuationTokens;
        internal readonly Documents.Routing.Range<string> CompleteRange;
        private CompositeContinuationToken currentToken;
        private string initialNotModifiedRange;

        private FeedTokenEPKRange(
            string containerRid)
            : base(containerRid)
        {
            this.CompositeContinuationTokens = new Queue<CompositeContinuationToken>();
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

        public FeedTokenEPKRange(
            string containerRid,
            IReadOnlyList<Documents.PartitionKeyRange> keyRanges)
            : this(containerRid)
        {
            if (keyRanges == null)
            {
                throw new ArgumentNullException(nameof(keyRanges));
            }

            if (keyRanges.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(keyRanges));
            }

            this.CompleteRange = new Documents.Routing.Range<string>(keyRanges[0].MinInclusive, keyRanges[keyRanges.Count - 1].MaxExclusive, true, false);
            foreach (Documents.PartitionKeyRange keyRange in keyRanges)
            {
                this.CompositeContinuationTokens.Enqueue(FeedTokenEPKRange.CreateCompositeContinuationTokenForRange(keyRange.MinInclusive, keyRange.MaxExclusive, null));
            }

            this.currentToken = this.CompositeContinuationTokens.Peek();
        }

        public FeedTokenEPKRange(
            string containerRid,
            Documents.PartitionKeyRange keyRange)
            : this(containerRid)
        {
            if (keyRange == null)
            {
                throw new ArgumentNullException(nameof(keyRange));
            }

            this.CompleteRange = new Documents.Routing.Range<string>(keyRange.MinInclusive, keyRange.MaxExclusive, true, false);
            this.CompositeContinuationTokens.Enqueue(FeedTokenEPKRange.CreateCompositeContinuationTokenForRange(keyRange.MinInclusive, keyRange.MaxExclusive, null));

            this.currentToken = this.CompositeContinuationTokens.Peek();
        }

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

            request.Properties[HandlerConstants.StartEpkString] = this.currentToken.Range.Min;
            request.Properties[HandlerConstants.EndEpkString] = this.currentToken.Range.Max;
        }

        public override string GetContinuation() => this.currentToken.Token;

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public override void UpdateContinuation(string continuationToken)
        {
            this.currentToken.Token = continuationToken;
            this.MoveToNextToken();
        }

        public override async Task<bool> ShouldRetryAsync(
            ContainerCore containerCore,
            ResponseMessage responseMessage,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (responseMessage.IsSuccessStatusCode)
            {
                this.initialNotModifiedRange = null;
                return false;
            }

            if (responseMessage.StatusCode == HttpStatusCode.NotModified
                && this.CompositeContinuationTokens.Count > 1)
            {
                if (this.initialNotModifiedRange == null)
                {
                    this.initialNotModifiedRange = this.currentToken.Range.Min;
                    return true;
                }

                return !this.initialNotModifiedRange.Equals(this.currentToken.Range.Min, StringComparison.OrdinalIgnoreCase);
            }

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
