//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System.Collections.Generic;
    using System;
    using System.Linq;
    using Newtonsoft.Json;

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

        public bool HasRange => this.currentToken != null;

        public StandByFeedContinuationToken(IReadOnlyList<Documents.PartitionKeyRange> keyRanges)
        {
            if (keyRanges == null) throw new ArgumentNullException(nameof(keyRanges));
            if (keyRanges.Count == 0) throw new ArgumentOutOfRangeException(nameof(keyRanges));

            this.InitializeCompositeTokens(this.BuildCompositeTokens(keyRanges));
        }

        public StandByFeedContinuationToken(string initialStandByFeedContinuationToken)
        {
            if (string.IsNullOrEmpty(initialStandByFeedContinuationToken)) throw new ArgumentNullException(nameof(initialStandByFeedContinuationToken));

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

        public void RemoveCurrent()
        {
            this.compositeContinuationTokens.Dequeue();
            if (this.compositeContinuationTokens.Count == 0)
            {
                this.currentToken = null;
            }
            else
            {
                this.currentToken = this.compositeContinuationTokens.Peek();
            }
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
            foreach (CompositeContinuationToken token in JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(initialContinuationToken))
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

            this.currentToken = this.compositeContinuationTokens.Peek();
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
