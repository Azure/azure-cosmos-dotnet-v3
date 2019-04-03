//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System.Collections.Generic;
    using System.Text;
    using System;

    internal class StandByFeedContinuationToken
    {
        private const string RangeSeparator = ",";
        private const string HeaderSeparator = ":";
        private const string TokenSeparator = "|";

        private string initialMinInclusive;
        private string standByFeedContinuationToken;
        private string currentLocalContinuationToken;
        private string currentMinInclusive;
        private string currentMaxInclusive;

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

        public string PushCurrentToBack() => this.PushRangeWithToken(this.currentMinInclusive, this.currentMaxInclusive, this.currentLocalContinuationToken);

        public string PushRangeWithToken(string minInclusive, string maxInclusive, string localContinuationToken)
        {
            StringBuilder compositeToken = new StringBuilder(this.standByFeedContinuationToken);
            compositeToken.Append(StandByFeedContinuationToken.Format(minInclusive, maxInclusive, localContinuationToken));
            this.standByFeedContinuationToken = compositeToken.ToString();
            return this.standByFeedContinuationToken;
        }

        public string NextToken => this.currentLocalContinuationToken;

        public string MinInclusiveRange => this.currentMinInclusive;

        public string MaxInclusiveRange => this.currentMaxInclusive;

        public string UpdateCurrentToken(string localContinuationToken)
        {
            this.currentLocalContinuationToken = localContinuationToken?.Replace("\"", string.Empty);
            return StandByFeedContinuationToken.Format(this.currentMinInclusive, this.currentMaxInclusive, this.currentLocalContinuationToken) + this.standByFeedContinuationToken;
        }

        public bool IsLoopCompleted => this.initialMinInclusive.Equals(this.currentMinInclusive, StringComparison.OrdinalIgnoreCase);

        public void PopNewToken()
        {
            int rangeIndex = this.standByFeedContinuationToken.IndexOf(StandByFeedContinuationToken.RangeSeparator);
            int headerIndex = this.standByFeedContinuationToken.IndexOf(StandByFeedContinuationToken.HeaderSeparator, rangeIndex);
            int separatorIndex = this.standByFeedContinuationToken.IndexOf(StandByFeedContinuationToken.TokenSeparator, headerIndex);
            this.currentMinInclusive = this.standByFeedContinuationToken.Substring(0, rangeIndex);
            this.currentMaxInclusive = this.standByFeedContinuationToken.Substring(rangeIndex + 1, headerIndex - rangeIndex - 1);
            this.currentLocalContinuationToken = this.standByFeedContinuationToken.Substring(headerIndex + 1, separatorIndex - headerIndex - 1);
            this.standByFeedContinuationToken = this.standByFeedContinuationToken.Remove(0, separatorIndex + 1);
            if (this.initialMinInclusive == null)
            {
                this.initialMinInclusive = this.currentMinInclusive;
            }
        }

        private static string Format(string minIncluive, string maxInclusive, string token)
        {
            return $"{minIncluive}{StandByFeedContinuationToken.RangeSeparator}{maxInclusive}{StandByFeedContinuationToken.HeaderSeparator}{token}{StandByFeedContinuationToken.TokenSeparator}";
        }
    }
}
