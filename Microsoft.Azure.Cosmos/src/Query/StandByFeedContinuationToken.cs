//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System.Collections.Generic;
    using System;
    using System.Linq;
    using Newtonsoft.Json;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;

    /// <summary>
    /// Stand by continuation token representing a contiguous read over all the ranges with continuation state across all ranges.
    /// </summary>
    internal class StandByFeedContinuationToken
    {
        private Queue<CompositeContinuationToken> compositeContinuationTokens;
        private CompositeContinuationToken currentToken;

        public string NextToken => this.currentToken.Token;

        public string MinInclusiveRange => this.currentToken.Range.Min;

        public string MaxExclusiveRange => this.currentToken.Range.Max;

        public StandByFeedContinuationToken()
        {
        }

        public StandByFeedContinuationToken(IReadOnlyList<Documents.PartitionKeyRange> keyRanges)
        {
            if (keyRanges == null) throw new ArgumentNullException(nameof(keyRanges));
            if (keyRanges.Count == 0) throw new ArgumentOutOfRangeException(nameof(keyRanges));

            this.InitializeCompositeTokens(this.BuildCompositeTokens(keyRanges));
        }

        public StandByFeedContinuationToken(string initialStandByFeedContinuationToken)
        {
            this.InitializeCompositeTokens(this.BuildCompositeTokens(initialStandByFeedContinuationToken));
        }

        public string PushCurrentToBack() {

            this.compositeContinuationTokens.Dequeue();
            string continuationToken = this.PushRangeWithToken(this.MinInclusiveRange, this.MaxExclusiveRange, this.NextToken);
            this.currentToken = this.compositeContinuationTokens.Peek();
            return continuationToken;
        }

        public string PushRangeWithToken(
            string min, 
            string max, 
            string token)
        {
            this.compositeContinuationTokens.Enqueue(StandByFeedContinuationToken.BuildTokenForRange(min, max, token));
            return this.ToString();
        }

        public string UpdateCurrentToken(string localContinuationToken)
        {
            this.currentToken.Token = localContinuationToken?.Replace("\"", string.Empty);
            return this.ToString();
        }

        public void HandleSplit(IReadOnlyList<Documents.PartitionKeyRange> keyRanges)
        {
            // Update current
            Documents.PartitionKeyRange firstRange = keyRanges[0];
            this.currentToken.Range = new Documents.Routing.Range<string>(firstRange.MinInclusive, firstRange.MaxExclusive, true, false);
            // Add children
            foreach (Documents.PartitionKeyRange keyRange in keyRanges.Skip(1))
            {
                this.PushRangeWithToken(keyRange.MinInclusive, keyRange.MaxExclusive, string.Empty);
            }
        }

        public async Task InitializeCompositeTokens(
            string containerRid,
            PartitionKeyRangeCache pkRangeCache,
            bool forceRefresh = false)
        {
            if (this.compositeContinuationTokens.Count == 0 || forceRefresh)
            {
                // Initialize composite token with all the ranges
                IReadOnlyList<Documents.PartitionKeyRange> allRanges = await pkRangeCache.TryGetOverlappingRangesAsync(containerRid, new Documents.Routing.Range<string>(
                    Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                    Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                    true,
                    false));

                this.InitializeCompositeTokens(this.BuildCompositeTokens(allRanges));
            }
        }

        public async Task<string> GetPartitionKeyRangeIdForCurrentState(
            string containerRid, 
            PartitionKeyRangeCache pkRangeCache)
        {
            IReadOnlyList<Documents.PartitionKeyRange> keyRanges = await this.GetPartitionKeyRangesForCurrentState(containerRid, pkRangeCache);

            if (keyRanges.Count > 1)
            {
                // Original range contains now more than 1 Key Range due to a split
                // Push the rest and update the current range
                this.HandleSplit(keyRanges);
            }

            return keyRanges[0].Id;
        }

        /// <summary>
        /// Only called when we received a split as response.
        /// </summary>
        /// <returns>true if we were able to detect the new ranges</returns>
        public async Task<bool> HandleRequestSplit(
            string containerRid,
            PartitionKeyRangeCache pkRangeCache)
        {
            IReadOnlyList<Documents.PartitionKeyRange> keyRanges = await this.GetPartitionKeyRangesForCurrentState(containerRid, pkRangeCache, true);

            if (keyRanges.Count > 0)
            {
                this.HandleSplit(keyRanges);
                return true;
            }

            return false;
        }

        internal new string ToString() => JsonConvert.SerializeObject(this.compositeContinuationTokens.ToList());

        private IEnumerable<CompositeContinuationToken> BuildCompositeTokens(IReadOnlyList<Documents.PartitionKeyRange> keyRanges)
        {
            foreach (Documents.PartitionKeyRange keyRange in keyRanges)
            {
                yield return StandByFeedContinuationToken.BuildTokenForRange(keyRange.MinInclusive, keyRange.MaxExclusive, string.Empty);
            }
        }

        private IEnumerable<CompositeContinuationToken> BuildCompositeTokens(string initialContinuationToken)
        {
            if (string.IsNullOrEmpty(initialContinuationToken))
            {
                yield break;
            }

            List<CompositeContinuationToken> deserializedToken;
            try
            {
                deserializedToken = JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(initialContinuationToken);
            }
            catch
            {
                throw new FormatException("Provided token has an invalid format");
            }
            foreach (CompositeContinuationToken token in deserializedToken)
            {
                yield return token;
            }
        }

        private void InitializeCompositeTokens(IEnumerable<CompositeContinuationToken> tokens)
        {
            this.compositeContinuationTokens = new Queue<CompositeContinuationToken>();

            foreach (CompositeContinuationToken token in tokens)
            {
                this.compositeContinuationTokens.Enqueue(token);
            }

            if (this.compositeContinuationTokens.Count > 0)
            {
                this.currentToken = this.compositeContinuationTokens.Peek();
            }
        }

        private async Task<IReadOnlyList<Documents.PartitionKeyRange>> GetPartitionKeyRangesForCurrentState(
            string containerRid, 
            PartitionKeyRangeCache pkRangeCache,
            bool forceRefresh = false)
        {
            IReadOnlyList<Documents.PartitionKeyRange> keyRanges = await this.GetCurrentPartitionKeyRanges(containerRid, pkRangeCache, forceRefresh);

            if (keyRanges.Count == 0)
            {
                throw new ArgumentOutOfRangeException("RequestContinuation", $"Token contains invalid or stale range {this.MinInclusiveRange}-{this.MaxExclusiveRange}.");
            }

            return keyRanges;
        }

        private async Task<IReadOnlyList<Documents.PartitionKeyRange>> GetCurrentPartitionKeyRanges(
            string containerRid, 
            PartitionKeyRangeCache pkRangeCache,
            bool forceRefresh = false)
        {
            return await pkRangeCache.TryGetOverlappingRangesAsync(
                    containerRid, 
                    new Documents.Routing.Range<string>(
                    this.MinInclusiveRange,
                    this.MaxExclusiveRange,
                    true,
                    false),
                    forceRefresh);
        }

        internal static CompositeContinuationToken BuildTokenForRange(string min, string max, string token)
        {
            return new CompositeContinuationToken() {
                Range = new Documents.Routing.Range<string>(min, max, true, false),
                Token = token
            };
        }
    }
}
