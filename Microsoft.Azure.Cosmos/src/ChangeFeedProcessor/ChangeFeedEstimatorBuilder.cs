//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;

    /// <summary>
    /// Provides a flexible way to create an instance of <see cref="ChangeFeedEstimator"/> with custom set of parameters.
    /// </summary>
    public class ChangeFeedEstimatorBuilder
    {
        private readonly ContainerInternal monitoredContainer;
        private readonly ChangeFeedProcessor changeFeedProcessor;
        private readonly ChangeFeedLeaseOptions changeFeedLeaseOptions;

        private ContainerInternal leaseContainer;
        private string InstanceName;
        private DocumentServiceLeaseStoreManager LeaseStoreManager;
        private string monitoredContainerRid;
        private bool isBuilt;

        internal ChangeFeedEstimatorBuilder(
            string processorName,
            ContainerInternal container)
        {
            this.changeFeedLeaseOptions = new ChangeFeedLeaseOptions();
            this.changeFeedLeaseOptions.LeasePrefix = processorName;
            this.monitoredContainer = container;
        }

        /// <summary>
        /// Sets the Cosmos Container to hold the leases state
        /// </summary>
        /// <param name="leaseContainer">Instance of a Cosmos Container to hold the leases.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedEstimatorBuilder WithLeaseContainer(Container leaseContainer)
        {
            if (leaseContainer == null) throw new ArgumentNullException(nameof(leaseContainer));
            if (this.leaseContainer != null) throw new InvalidOperationException("The builder already defined a lease container.");
            if (this.LeaseStoreManager != null) throw new InvalidOperationException("The builder already defined an in-memory lease container instance.");

            this.leaseContainer = (ContainerInternal)leaseContainer;
            return this;
        }

        /// <summary>
        /// Builds a new instance of the <see cref="ChangeFeedEstimator"/> with the specified configuration.
        /// </summary>
        /// <returns>An instance of <see cref="ChangeFeedEstimator"/>.</returns>
        public ChangeFeedEstimator Build()
        {
            if (this.isBuilt)
            {
                throw new InvalidOperationException("This builder instance has already been used to build a processor. Create a new instance to build another.");
            }

            if (this.monitoredContainer == null)
            {
                throw new InvalidOperationException(nameof(this.monitoredContainer) + " was not specified");
            }

            if (this.leaseContainer == null && this.LeaseStoreManager == null)
            {
                throw new InvalidOperationException($"Defining the lease store by WithLeaseContainer or WithInMemoryLeaseContainer is required.");
            }

            if (this.changeFeedLeaseOptions.LeasePrefix == null)
            {
                throw new InvalidOperationException("Processor name not specified during creation.");
            }

            Func<string, string, bool, FeedIterator> feedCreator = (string partitionKeyRangeId, string continuationToken, bool startFromBeginning) =>
            {
                return ResultSetIteratorUtils.BuildResultSetIterator(
                    partitionKeyRangeId: partitionKeyRangeId,
                    continuationToken: continuationToken,
                    maxItemCount: 1,
                    container: this.monitoredContainer,
                    startTime: null,
                    startFromBeginning: string.IsNullOrEmpty(continuationToken));
            };

            ChangeFeedEstimator estimator = new ChangeFeedEstimatorCore(
               this.documentServiceLeaseStoreManager.LeaseContainer,
               feedCreator,
               this.monitoredContainer.ClientContext.Client.ClientOptions?.GatewayModeMaxConnectionLimit ?? 1);

            this.isBuilt = true;
            return estimator;
        }
    }
}
