//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;

    internal class ChangeFeedProcessorBuilderCore<T>: ChangeFeedProcessorBuilder
    {
        private const string InMemoryDefaultHostName = "InMemory";
        private const string EstimatorDefaultHostName = "Estimator";

        private readonly Func<IReadOnlyList<T>, CancellationToken, Task> initialChangesDelegate;
        private readonly Func<long, CancellationToken, Task> initialEstimateDelegate;

        internal ChangeFeedProcessorOptions changeFeedProcessorOptions;
        internal ChangeFeedLeaseOptions changeFeedLeaseOptions;
        internal ChangeFeedObserverFactory<T> observerFactory = null;
        internal CosmosContainer monitoredContainer;
        internal CosmosContainer leaseContainer;
        internal string InstanceName;
        internal DocumentServiceLeaseStoreManager LeaseStoreManager;

        private TimeSpan? estimatorPeriod = null;
        private string databaseResourceId;
        private string collectionResourceId;
        private bool isBuilt;

        private bool IsBuildingEstimator => this.initialEstimateDelegate != null;

        private ChangeFeedProcessorBuilderCore(CosmosContainer cosmosContainer)
        {
            this.monitoredContainer = cosmosContainer;
        }

        public ChangeFeedProcessorBuilderCore(
            CosmosContainer cosmosContainer, 
            Func<IReadOnlyList<T>, CancellationToken, Task> onChangesDelegate)
            : this(cosmosContainer)
        {
            this.initialChangesDelegate = onChangesDelegate;
            this.observerFactory = new ChangeFeedObserverFactoryCore<T>(onChangesDelegate);
        }

        public ChangeFeedProcessorBuilderCore(
            CosmosContainer cosmosContainer, 
            Func<long, CancellationToken, Task> estimateDelegate, 
            TimeSpan? estimatorPeriod = null)
            : this(cosmosContainer)
        {
            this.initialEstimateDelegate = estimateDelegate;
            this.estimatorPeriod = estimatorPeriod;
        }

        public override ChangeFeedProcessorBuilder WithInstanceName(string instanceName)
        {
            this.InstanceName = instanceName;
            return this;
        }

        public override ChangeFeedProcessorBuilder WithWorkflowName(string workflowName)
        {
            this.changeFeedLeaseOptions = this.changeFeedLeaseOptions ?? new ChangeFeedLeaseOptions();
            this.changeFeedLeaseOptions.LeasePrefix = workflowName;
            return this;
        }

        public override ChangeFeedProcessorBuilder WithLeaseConfiguration(TimeSpan? acquireInterval = null, TimeSpan? expirationInterval = null, TimeSpan? renewInterval = null)
        {
            this.changeFeedLeaseOptions = this.changeFeedLeaseOptions ?? new ChangeFeedLeaseOptions();
            this.changeFeedLeaseOptions.LeaseRenewInterval = renewInterval ?? ChangeFeedLeaseOptions.DefaultRenewInterval;
            this.changeFeedLeaseOptions.LeaseAcquireInterval = acquireInterval ?? ChangeFeedLeaseOptions.DefaultAcquireInterval;
            this.changeFeedLeaseOptions.LeaseExpirationInterval = expirationInterval ?? ChangeFeedLeaseOptions.DefaultExpirationInterval;
            return this;
        }

        public override ChangeFeedProcessorBuilder WithFeedPollDelay(TimeSpan feedPollDelay)
        {
            if (feedPollDelay == null) throw new ArgumentNullException(nameof(feedPollDelay));

            this.changeFeedProcessorOptions = this.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorOptions.FeedPollDelay = feedPollDelay;
            return this;
        }

        internal override ChangeFeedProcessorBuilder WithStartFromBeginning()
        {
            this.changeFeedProcessorOptions = this.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorOptions.StartFromBeginning = true;
            return this;
        }

        public override ChangeFeedProcessorBuilder WithContinuation(string startContinuation)
        {
            this.changeFeedProcessorOptions = this.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorOptions.StartContinuation = startContinuation;
            return this;
        }

        public override ChangeFeedProcessorBuilder WithStartTime(DateTime startTime)
        {
            if (startTime == null) throw new ArgumentNullException(nameof(startTime));

            this.changeFeedProcessorOptions = this.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorOptions.StartTime = startTime;
            return this;
        }

        public override ChangeFeedProcessorBuilder WithMaxItems(int maxItemCount)
        {
            if (maxItemCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxItemCount));

            this.changeFeedProcessorOptions = this.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorOptions.MaxItemCount = maxItemCount;
            return this;
        }

        public override ChangeFeedProcessorBuilder WithCosmosLeaseContainer(CosmosContainer leaseContainer)
        {
            if (leaseContainer == null) throw new ArgumentNullException(nameof(leaseContainer));
            if (this.leaseContainer != null) throw new InvalidOperationException("The builder already defined a lease container.");
            if (this.LeaseStoreManager != null) throw new InvalidOperationException("The builder already defined an in-memory lease container instance.");

            this.leaseContainer = leaseContainer;
            return this;
        }

        public override ChangeFeedProcessorBuilder WithInMemoryLeaseContainer()
        {
            if (this.leaseContainer != null) throw new InvalidOperationException("The builder already defined a lease container.");
            if (this.LeaseStoreManager != null) throw new InvalidOperationException("The builder already defined an in-memory lease container instance.");

            if (string.IsNullOrEmpty(this.InstanceName))
            {
                this.InstanceName = ChangeFeedProcessorBuilderCore<T>.InMemoryDefaultHostName;
            }

            this.LeaseStoreManager = new DocumentServiceLeaseStoreManagerInMemory();
            return this;
        }

        public override ChangeFeedProcessor Build()
        {
            if (this.isBuilt)
            {
                throw new InvalidOperationException("This builder instance has already been used to build a processor. Create a new instance to build another.");
            }

            if (IsBuildingEstimator)
            {
                this.InstanceName = ChangeFeedProcessorBuilderCore<T>.EstimatorDefaultHostName;
            }

            if (this.monitoredContainer == null)
            {
                throw new InvalidOperationException(nameof(this.monitoredContainer) + " was not specified");
            }

            if (this.leaseContainer == null && this.LeaseStoreManager == null)
            {
                throw new InvalidOperationException($"Defining the lease store by WithCosmosLeaseContainer, WithInMemoryLeaseContainer, or WithLeaseStoreManager is required.");
            }

            if (this.observerFactory == null && this.initialEstimateDelegate == null)
            {
                throw new InvalidOperationException("Observer or Estimation delegate need to be specified.");
            }

            if (this.observerFactory != null && this.InstanceName == null)
            {
                // Processor requires Instace Name
                throw new InvalidOperationException("Instance name was not specified");
            }

            if (this.changeFeedLeaseOptions?.LeasePrefix == null)
            {
                throw new InvalidOperationException("Workflow name was not specified using WithWorkflowName");
            }

            this.InitializeDefaultOptions();
            this.InitializeCollectionPropertiesForBuild();

            ChangeFeedProcessor builtInstance;
            if (this.observerFactory != null)
            {
                builtInstance = new ChangeFeedProcessorCore<T>(this.LeaseStoreManager, this.leaseContainer, this.GetLeasePrefix(), this.InstanceName, this.changeFeedLeaseOptions, this.changeFeedProcessorOptions, this.observerFactory, this.monitoredContainer);
            }
            else
            {
                builtInstance = new ChangeFeedEstimatorCore(this.LeaseStoreManager, this.leaseContainer, this.GetLeasePrefix(), this.InstanceName, this.initialEstimateDelegate, this.estimatorPeriod, this.monitoredContainer);
            }

            this.isBuilt = true;
            return builtInstance;
        }

        private string GetLeasePrefix()
        {
            string optionsPrefix = this.changeFeedLeaseOptions.LeasePrefix ?? string.Empty;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1}_{2}_{3}",
                optionsPrefix,
                this.monitoredContainer.Client.Configuration.AccountEndPoint.Host,
                this.databaseResourceId,
                this.collectionResourceId);
        }

        private void InitializeCollectionPropertiesForBuild()
        {
            var containerLinkSegments = this.monitoredContainer.LinkUri.OriginalString.Split('/');
            this.databaseResourceId = containerLinkSegments[2];
            this.collectionResourceId = containerLinkSegments[4];
        }

        private void InitializeDefaultOptions()
        {
            this.changeFeedProcessorOptions = this.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedLeaseOptions = this.changeFeedLeaseOptions ?? new ChangeFeedLeaseOptions();
        }
    }
}
