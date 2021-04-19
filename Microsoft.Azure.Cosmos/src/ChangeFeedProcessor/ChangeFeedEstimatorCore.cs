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
        private readonly string processorName;
        private readonly ContainerInternal monitoredContainer;
        private readonly ContainerInternal leaseContainer;
        private readonly DocumentServiceLeaseContainer documentServiceLeaseContainer;

        public ChangeFeedEstimatorCore(
            string processorName,
            ContainerInternal monitoredContainer,
            ContainerInternal leaseContainer,
            DocumentServiceLeaseContainer documentServiceLeaseContainer)
        {
            this.processorName = processorName ?? throw new ArgumentNullException(nameof(processorName));
            this.leaseContainer = leaseContainer ?? throw new ArgumentNullException(nameof(leaseContainer));
            this.monitoredContainer = monitoredContainer ?? throw new ArgumentNullException(nameof(monitoredContainer));
            this.documentServiceLeaseContainer = documentServiceLeaseContainer;
        }

        public override FeedIterator<ChangeFeedProcessorState> GetCurrentStateIterator(ChangeFeedEstimatorRequestOptions changeFeedEstimatorRequestOptions = null)
        {
            return new ChangeFeedEstimatorIterator(
                this.processorName,
                this.monitoredContainer,
                this.leaseContainer,
                this.documentServiceLeaseContainer,
                changeFeedEstimatorRequestOptions);
        }
    }
}
