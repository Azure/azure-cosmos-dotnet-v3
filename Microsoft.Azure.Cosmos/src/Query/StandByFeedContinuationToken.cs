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
    using System.Diagnostics;

    /// <summary>
    /// Stand by continuation token representing a contiguous read over all the ranges with continuation state across all ranges.
    /// </summary>
    /// <remarks>
    /// The StandByFeed token represents the state of continuation tokens across all Partition Key Ranges and can be used to sequentially read the Change Feed for each range while maintaining a global state by serializing the values (and allowing deserialization).
    /// </remarks>
    internal class StandByFeedContinuationToken
    {
        private readonly string containerRid;
        private readonly Func<string, Documents.Routing.Range<string>, bool, Task<IReadOnlyList<Documents.PartitionKeyRange>>> pkRangeCacheDelegate;
        private readonly string inputContinuationToken;

        private Queue<CompositeContinuationToken> compositeContinuationTokens;
        private CompositeContinuationToken currentToken;

        public StandByFeedContinuationToken(
            string containerRid,
            string initialStandByFeedContinuationToken,
            Func<string, Documents.Routing.Range<string>, bool, Task<IReadOnlyList<Documents.PartitionKeyRange>>> pkRangeCacheDelegate)
        {
            if (string.IsNullOrWhiteSpace(containerRid)) throw new ArgumentNullException(nameof(containerRid));
            if (pkRangeCacheDelegate == null) throw new ArgumentNullException(nameof(pkRangeCacheDelegate));

            this.containerRid = containerRid;
            this.pkRangeCacheDelegate = pkRangeCacheDelegate;
            this.inputContinuationToken = initialStandByFeedContinuationToken;
        }

        public async Task<Tuple<CompositeContinuationToken, string>> GetCurrentToken(bool forceRefresh = false)
        {
            await this.EnsureInitializedAsync();
            Debug.Assert(this.compositeContinuationTokens != null);
            IReadOnlyList<Documents.PartitionKeyRange> resolvedRanges = await this.TryGetOverlappingRangesAsync(this.currentToken.Range, forceRefresh: forceRefresh);
            if (resolvedRanges.Count > 1)
            {
                this.HandleSplit(resolvedRanges);
            }

            return new Tuple<CompositeContinuationToken, string>(this.currentToken, resolvedRanges[0].Id);
        }

        public async Task MoveToNextTokenAsync()
        {
            await this.EnsureInitializedAsync();

            CompositeContinuationToken recentToken = this.compositeContinuationTokens.Dequeue();
            this.compositeContinuationTokens.Enqueue(recentToken);
            this.currentToken = this.compositeContinuationTokens.Peek();
        }

        public new string ToString()
        {
            Debug.Assert(this.compositeContinuationTokens != null);
            if (this.compositeContinuationTokens == null)
            {
                return null;
            }

            return JsonConvert.SerializeObject(this.compositeContinuationTokens.ToList());
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
                this.compositeContinuationTokens.Enqueue(new CompositeContinuationToken()
                {
                    Range = new Documents.Routing.Range<string>(keyRange.MinInclusive, keyRange.MaxExclusive, true, false),
                    Token = this.currentToken.Token
                });
            }
        }

        private async Task EnsureInitializedAsync()
        {
            if (this.compositeContinuationTokens == null)
            {
                IEnumerable<CompositeContinuationToken> tokens = await this.BuildCompositeTokens(this.inputContinuationToken);

                this.InitializeCompositeTokens(tokens);

                Debug.Assert(this.compositeContinuationTokens.Count > 0);
            }
        }

        private async Task<IEnumerable<CompositeContinuationToken>> BuildCompositeTokens(string initialContinuationToken)
        {
            if (string.IsNullOrEmpty(initialContinuationToken))
            {
                // Initialize composite token with all the ranges
                IReadOnlyList<Documents.PartitionKeyRange> allRanges = await this.pkRangeCacheDelegate(
                        this.containerRid,
                        new Documents.Routing.Range<string>(
                            Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                            Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                            isMinInclusive: true,
                            isMaxInclusive: false),
                        false);

                Debug.Assert(allRanges.Count != 0);
                return allRanges.Select(e => new CompositeContinuationToken()
                {
                    Range = new Documents.Routing.Range<string>(e.MinInclusive, e.MaxExclusive, isMinInclusive: true, isMaxInclusive: false),
                    Token = string.Empty,
                });
            }

            try
            {
                return JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(initialContinuationToken);
            }
            catch(Exception ex)
            {
                throw new ArgumentOutOfRangeException("Provided token has an invalid format", ex);
            }
        }

        private void InitializeCompositeTokens(IEnumerable<CompositeContinuationToken> tokens)
        {
            this.compositeContinuationTokens = new Queue<CompositeContinuationToken>();

            foreach (CompositeContinuationToken token in tokens)
            {
                this.compositeContinuationTokens.Enqueue(token);
            }

            this.currentToken = this.compositeContinuationTokens.Peek();
        }

        private async Task<IReadOnlyList<Documents.PartitionKeyRange>> TryGetOverlappingRangesAsync(
            Documents.Routing.Range<string> targetRange,
            bool forceRefresh = false)
        {
            Debug.Assert(targetRange != null);

            IReadOnlyList<Documents.PartitionKeyRange> keyRanges = await this.pkRangeCacheDelegate(
                this.containerRid,
                new Documents.Routing.Range<string>(
                    targetRange.Min,
                    targetRange.Max,
                    isMaxInclusive: true,
                    isMinInclusive: false),
                forceRefresh);

            if (keyRanges.Count == 0)
            {
                throw new ArgumentOutOfRangeException("RequestContinuation", $"Token contains invalid range {targetRange.Min}-{targetRange.Max}");
            }

            return keyRanges;
        }
    }
}
