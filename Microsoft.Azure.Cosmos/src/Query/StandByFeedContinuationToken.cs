//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System.Collections.Generic;
    using System.Text;
    using System;

    /// <summary>
    /// Stand by continuation token representing a contiguous read over all the ranges with continuation state across all ranges.
    /// </summary>
    /// <remarks>
    /// Format is {range}:{token}|{range}:{token}|
    /// Where {range} is {min},{max}
    /// </remarks>
    internal class StandByFeedContinuationToken
    {
        private const string RangeSeparator = ",";
        private const string HeaderSeparator = ":";
        private const string TokenSeparator = "|";

        private string initialMinInclusive;
        private string standByFeedContinuationToken;

        public StandByFeedContinuationToken(IReadOnlyList<Documents.PartitionKeyRange> keyRanges)
        {
            if (keyRanges == null) throw new ArgumentNullException(nameof(keyRanges));
            if (keyRanges.Count == 0) throw new ArgumentOutOfRangeException(nameof(keyRanges));

            StringBuilder compositeToken = new StringBuilder();
            foreach (Documents.PartitionKeyRange keyRange in keyRanges)
            {
                compositeToken.Append(StandByFeedContinuationToken.Format(keyRange.MinInclusive, keyRange.MaxExclusive, string.Empty));
            }

            this.standByFeedContinuationToken = compositeToken.ToString();
            this.PopNewToken();
        }

        public StandByFeedContinuationToken(string initialStandByFeedContinuationToken)
        {
            if (string.IsNullOrEmpty(initialStandByFeedContinuationToken)) throw new ArgumentNullException(nameof(initialStandByFeedContinuationToken));

            this.standByFeedContinuationToken = initialStandByFeedContinuationToken;
            this.PopNewToken();
        }

        public string PushCurrentToBack() => this.PushRangeWithToken(this.MinInclusiveRange, this.MaxInclusiveRange, this.NextToken);

        public string PushRangeWithToken(string minInclusive, string maxInclusive, string localContinuationToken)
        {
            StringBuilder compositeToken = new StringBuilder(this.standByFeedContinuationToken);
            compositeToken.Append(StandByFeedContinuationToken.Format(minInclusive, maxInclusive, localContinuationToken));
            this.standByFeedContinuationToken = compositeToken.ToString();
            return this.standByFeedContinuationToken;
        }

        public string NextToken { get; private set; }

        public string MinInclusiveRange { get; private set; }

        public string MaxInclusiveRange { get; private set; }

        public string UpdateCurrentToken(string localContinuationToken)
        {
            this.NextToken = localContinuationToken?.Replace("\"", string.Empty);
            return StandByFeedContinuationToken.Format(this.MinInclusiveRange, this.MaxInclusiveRange, this.NextToken) + this.standByFeedContinuationToken;
        }

        public bool IsLoopCompleted => this.initialMinInclusive.Equals(this.MinInclusiveRange, StringComparison.OrdinalIgnoreCase);

        public void PopNewToken()
        {
            int rangeIndex = this.standByFeedContinuationToken.IndexOf(StandByFeedContinuationToken.RangeSeparator);
            int headerIndex = this.standByFeedContinuationToken.IndexOf(StandByFeedContinuationToken.HeaderSeparator, rangeIndex);
            int separatorIndex = this.standByFeedContinuationToken.IndexOf(StandByFeedContinuationToken.TokenSeparator, headerIndex);
            this.MinInclusiveRange = this.standByFeedContinuationToken.Substring(0, rangeIndex);
            this.MaxInclusiveRange = this.standByFeedContinuationToken.Substring(rangeIndex + 1, headerIndex - rangeIndex - 1);
            this.NextToken = this.standByFeedContinuationToken.Substring(headerIndex + 1, separatorIndex - headerIndex - 1);
            this.standByFeedContinuationToken = this.standByFeedContinuationToken.Remove(0, separatorIndex + 1);
            if (this.initialMinInclusive == null)
            {
                this.initialMinInclusive = this.MinInclusiveRange;
            }
        }

        private static string Format(string minIncluive, string maxInclusive, string token)
        {
            return $"{minIncluive}{StandByFeedContinuationToken.RangeSeparator}{maxInclusive}{StandByFeedContinuationToken.HeaderSeparator}{token}{StandByFeedContinuationToken.TokenSeparator}";
        }
    }
}
