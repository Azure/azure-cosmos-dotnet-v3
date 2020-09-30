//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed;

    internal sealed class ChangeFeedEstimatorCore : ChangeFeedEstimator
    {
        private readonly string processorName;
        private readonly ContainerInternal monitoredContainer;
        private readonly ContainerInternal leaseContainer;

        public ChangeFeedEstimatorCore(
            string processorName,
            ContainerInternal monitoredContainer,
            ContainerInternal leaseContainer)
        {
            this.processorName = processorName ?? throw new ArgumentNullException(nameof(processorName));
            this.leaseContainer = leaseContainer ?? throw new ArgumentNullException(nameof(leaseContainer));
            this.monitoredContainer = monitoredContainer ?? throw new ArgumentNullException(nameof(monitoredContainer));
        }

        public override FeedIterator<ChangeFeedProcessorState> GetCurrentStateIterator(ChangeFeedEstimatorRequestOptions changeFeedEstimatorRequestOptions = null)
        {
            return new ChangeFeedEstimatorIterator(
                this.processorName,
                this.monitoredContainer,
                this.leaseContainer,
                changeFeedEstimatorRequestOptions);
        }
    }
}
