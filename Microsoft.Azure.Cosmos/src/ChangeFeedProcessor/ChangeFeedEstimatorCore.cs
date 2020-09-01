//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;

    internal sealed class ChangeFeedEstimatorCore : ChangeFeedEstimator
    {
        private readonly Func<string, string, bool, FeedIterator> feedCreator;
        private readonly DocumentServiceLeaseContainer leaseContainer;

        public ChangeFeedEstimatorCore(
            DocumentServiceLeaseContainer leaseContainer,
            Func<string, string, bool, FeedIterator> feedCreator)
        {
            if (leaseContainer == null)
            {
                throw new ArgumentNullException(nameof(leaseContainer));
            }

            if (feedCreator == null)
            {
                throw new ArgumentNullException(nameof(feedCreator));
            }

            this.leaseContainer = leaseContainer;
            this.feedCreator = feedCreator;
        }

        public override FeedIterator<RemainingLeaseWork> GetRemainingLeaseWorkIterator(ChangeFeedEstimatorRequestOptions changeFeedEstimatorRequestOptions = null)
        {
            return new ChangeFeedEstimatorIterator(
                this.leaseContainer,
                this.feedCreator,
                changeFeedEstimatorRequestOptions);
        }
    }
}
