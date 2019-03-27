//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Monitoring;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement;

    /// <summary>
    /// Provides a flexible way to to create an instance of <see cref="ChangeFeedProcessor"/> with custom set of parameters.
    /// </summary>
    /// <example>
    /// <code language="C#">
    /// <![CDATA[
    /// // Observer.cs
    /// namespace Sample
    /// {
    ///     using System;
    ///     using System.Collections.Generic;
    ///     using System.Threading;
    ///     using System.Threading.Tasks;
    ///     using Microsoft.Azure.Documents;
    ///     using Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing;
    ///
    ///     class SampleObserver : IChangeFeedObserver
    ///     {
    ///         public Task CloseAsync(IChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
    ///         {
    ///             return Task.CompletedTask;  // Note: requires targeting .Net 4.6+.
    ///         }
    ///
    ///         public Task OpenAsync(IChangeFeedObserverContext context)
    ///         {
    ///             return Task.CompletedTask;
    ///         }
    ///
    ///         public Task ProcessChangesAsync(IChangeFeedObserverContext context, IReadOnlyList<Document> docs, CancellationToken cancellationToken)
    ///         {
    ///             Console.WriteLine("ProcessChangesAsync: partition {0}, {1} docs", context.PartitionKeyRangeId, docs.Count);
    ///             return Task.CompletedTask;
    ///         }
    ///     }
    /// }
    ///
    /// // Main.cs
    /// namespace Sample
    /// {
    ///     using System;
    ///     using System.Threading.Tasks;
    ///     using Microsoft.Azure.Cosmos.ChangeFeedProcessor;
    ///     using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Logging;
    ///
    ///     class ChangeFeedProcessorSample
    ///     {
    ///         public static void Run()
    ///         {
    ///             RunAsync().Wait();
    ///         }
    ///
    ///         static async Task RunAsync()
    ///         {
    ///             DocumentCollectionInfo feedCollectionInfo = new DocumentCollectionInfo()
    ///             {
    ///                 DatabaseName = "DatabaseName",
    ///                 CollectionName = "MonitoredCollectionName",
    ///                 Uri = new Uri("https://sampleservice.documents.azure.com:443/"),
    ///                 MasterKey = "-- the auth key"
    ///             };
    ///
    ///             DocumentCollectionInfo leaseCollectionInfo = new DocumentCollectionInfo()
    ///             {
    ///                 DatabaseName = "DatabaseName",
    ///                 CollectionName = "leases",
    ///                 Uri = new Uri("https://sampleservice.documents.azure.com:443/"),
    ///                 MasterKey = "-- the auth key"
    ///             };
    ///
    ///             var builder = new ChangeFeedProcessorBuilder();
    ///             var processor = await builder
    ///                 .WithHostName("SampleHost")
    ///                 .WithFeedCollection(feedCollectionInfo)
    ///                 .WithLeaseCollection(leaseCollectionInfo)
    ///                 .WithObserver<SampleObserver>()
    ///                 .BuildAsync();
    ///
    ///             await processor.StartAsync();
    ///
    ///             Console.WriteLine("Change Feed Processor started. Press <Enter> key to stop...");
    ///             Console.ReadLine();
    ///
    ///             await processor.StopAsync();
    ///         }
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public class ChangeFeedProcessorBuilder<T>
    {
        private const string InMemoryDefaultHostName = "InMemory";

        private readonly CosmosContainer initialCosmosContainer;
        private readonly Func<IReadOnlyList<T>, CancellationToken, Task> initialChangesDelegate;

        private ChangeFeedProcessorBuilderInstance<T> changeFeedProcessorBuilderInstance;

        internal string HostName
        {
            get => this.changeFeedProcessorBuilderInstance.HostName;
        }

        /// <summary>
        /// Gets the lease manager.
        /// </summary>
        /// <remarks>
        /// Internal for testing only, otherwise it would be private.
        /// </remarks>
        internal DocumentServiceLeaseStoreManager LeaseStoreManager
        {
            get => this.changeFeedProcessorBuilderInstance.LeaseStoreManager;
        }

        internal ChangeFeedProcessorBuilder(CosmosContainer cosmosContainer): base()
        {
            this.initialCosmosContainer = cosmosContainer;
            this.Reset();
        }

        internal ChangeFeedProcessorBuilder(CosmosContainer cosmosContainer, Func<IReadOnlyList<T>, CancellationToken, Task> onChangesDelegate)
            : this(cosmosContainer)
        {
            this.initialChangesDelegate = onChangesDelegate;
            this.changeFeedProcessorBuilderInstance.observerFactory = new ChangeFeedObserverFactoryCore<T>(onChangesDelegate);
        }

        /// <summary>
        /// Sets the Host name.
        /// </summary>
        /// <param name="hostName">Name to be used for the host. When using multiple hosts, each host must have a unique name.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithHostName(string hostName)
        {
            this.changeFeedProcessorBuilderInstance.HostName = hostName;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="ChangeFeedLeaseOptions"/> to be used by this instance of <see cref="ChangeFeedProcessor"/> to control how leases are maintained in a container.
        /// </summary>
        /// <remarks>
        /// This does not apply when using <see cref="WithInMemoryLeaseContainer"/>.
        /// </remarks>
        /// <param name="changeFeedLeaseOptions">The instance of <see cref="ChangeFeedLeaseOptions"/> to use.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithLeaseOptions(ChangeFeedLeaseOptions changeFeedLeaseOptions)
        {
            if (changeFeedLeaseOptions == null) throw new ArgumentNullException(nameof(changeFeedLeaseOptions));
            this.changeFeedProcessorBuilderInstance.changeFeedLeaseOptions = changeFeedLeaseOptions;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="ChangeFeedProcessorOptions"/> to be used by this instance of <see cref="ChangeFeedProcessor"/>.
        /// </summary>
        /// <param name="changeFeedProcessorOptions">The instance of <see cref="ChangeFeedProcessorOptions"/> to use.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithProcessorOptions(ChangeFeedProcessorOptions changeFeedProcessorOptions)
        {
            if (changeFeedProcessorOptions == null) throw new ArgumentNullException(nameof(changeFeedProcessorOptions));
            this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions = changeFeedProcessorOptions;
            return this;
        }

        /// <summary>
        /// Sets the Cosmos Container to hold the leases state
        /// </summary>
        /// <param name="leaseContainer"></param>
        /// <returns></returns>
        public ChangeFeedProcessorBuilder<T> WithCosmosLeaseContainer(CosmosContainer leaseContainer)
        {
            if (leaseContainer == null) throw new ArgumentNullException(nameof(leaseContainer));
            if (this.changeFeedProcessorBuilderInstance.leaseContainer != null) throw new InvalidOperationException("The builder already defined a lease container.");
            if (this.changeFeedProcessorBuilderInstance.LeaseStoreManager != null) throw new InvalidOperationException("The builder already defined a custom Lease Store Manager instance.");
            this.changeFeedProcessorBuilderInstance.leaseContainer = leaseContainer;
            return this;
        }

        /// <summary>
        /// Uses an in-memory container to maintain state of the leases
        /// </summary>
        /// <remarks>
        /// Using an in-memory container restricts the scaling capability to just the instance running the current processor.
        /// </remarks>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithInMemoryLeaseContainer()
        {
            if (this.changeFeedProcessorBuilderInstance.leaseContainer != null) throw new InvalidOperationException("The builder already defined a lease container.");
            if (this.changeFeedProcessorBuilderInstance.LeaseStoreManager != null) throw new InvalidOperationException("The builder already defined an in-memory lease container or a custom Lease Store Manager instance.");
            if (string.IsNullOrEmpty(this.HostName))
            {
                this.changeFeedProcessorBuilderInstance.HostName = ChangeFeedProcessorBuilder<T>.InMemoryDefaultHostName;
            }

            this.changeFeedProcessorBuilderInstance.LeaseStoreManager = new DocumentServiceLeaseStoreManagerInMemory();
            return this;
        }

        /// <summary>
        /// Sets the <see cref="PartitionLoadBalancingStrategy"/> to be used for partition load balancing
        /// </summary>
        /// <param name="strategy">The instance of <see cref="PartitionLoadBalancingStrategy"/> to use.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithPartitionLoadBalancingStrategy(PartitionLoadBalancingStrategy strategy)
        {
            if (strategy == null) throw new ArgumentNullException(nameof(strategy));
            this.changeFeedProcessorBuilderInstance.loadBalancingStrategy = strategy;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="PartitionProcessorFactory{T}"/> to be used to create <see cref="PartitionProcessor"/> for partition processing.
        /// </summary>
        /// <param name="partitionProcessorFactory">The instance of <see cref="PartitionProcessorFactory{T}"/> to use.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithPartitionProcessorFactory(PartitionProcessorFactory<T> partitionProcessorFactory)
        {
            if (partitionProcessorFactory == null) throw new ArgumentNullException(nameof(partitionProcessorFactory));
            this.changeFeedProcessorBuilderInstance.partitionProcessorFactory = partitionProcessorFactory;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="FeedProcessing.ChangeFeedObserverFactory{T}"/> to be used to generate <see cref="FeedProcessing.ChangeFeedObserver{T}"/>
        /// </summary>
        /// <param name="observerFactory">The instance of <see cref="FeedProcessing.ChangeFeedObserverFactory{T}"/> to use.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithObserverFactory(ChangeFeedObserverFactory<T> observerFactory)
        {
            if (observerFactory == null) throw new ArgumentNullException(nameof(observerFactory));
            if (this.changeFeedProcessorBuilderInstance.observerFactory != null) throw new InvalidOperationException("A listening mechanism was already defined, either by a previous call to WIthObserverFactory or through the delegate in the constructor.");
            this.changeFeedProcessorBuilderInstance.observerFactory = observerFactory;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="DocumentServiceLeaseStoreManager"/> to be used to manage leases.
        /// </summary>
        /// <param name="leaseStoreManager">The instance of <see cref="DocumentServiceLeaseStoreManager"/> to use.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithLeaseStoreManager(DocumentServiceLeaseStoreManager leaseStoreManager)
        {
            if (leaseStoreManager == null) throw new ArgumentNullException(nameof(leaseStoreManager));
            if (this.changeFeedProcessorBuilderInstance.leaseContainer != null) throw new InvalidOperationException("The builder already defined a lease container.");
            if (this.changeFeedProcessorBuilderInstance.LeaseStoreManager != null) throw new InvalidOperationException("The builder already defined an in-memory lease container or a custom Lease Store Manager instance.");
            this.changeFeedProcessorBuilderInstance.LeaseStoreManager = leaseStoreManager;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="HealthMonitor"/> to be used to monitor unhealthiness situation.
        /// </summary>
        /// <param name="healthMonitor">The instance of <see cref="HealthMonitor"/> to use.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithHealthMonitor(HealthMonitor healthMonitor)
        {
            if (healthMonitor == null) throw new ArgumentNullException(nameof(healthMonitor));
            this.changeFeedProcessorBuilderInstance.healthMonitor = healthMonitor;
            return this;
        }

        /// <summary>
        /// Builds a new instance of the <see cref="ChangeFeedProcessor"/> with the specified configuration.
        /// </summary>
        /// <returns>An instance of <see cref="ChangeFeedProcessor"/>.</returns>
        public async Task<ChangeFeedProcessor> BuildAsync()
        {
            ChangeFeedProcessor changeFeedProcessor =  await this.changeFeedProcessorBuilderInstance.BuildAsync().ConfigureAwait(false);
            this.Reset();
            return changeFeedProcessor;
        }

        /// <summary>
        /// Builds a new instance of the <see cref="RemainingWorkEstimator"/> to estimate pending work with the specified configuration.
        /// </summary>
        /// <returns>An instance of <see cref="RemainingWorkEstimator"/>.</returns>
        public async Task<RemainingWorkEstimator> BuildEstimatorAsync()
        {
            RemainingWorkEstimator remainingWorkEstimator = await this.changeFeedProcessorBuilderInstance.BuildEstimatorAsync().ConfigureAwait(false);
            this.Reset();
            return remainingWorkEstimator;
        }

        private void Reset()
        {
            this.changeFeedProcessorBuilderInstance = new ChangeFeedProcessorBuilderInstance<T>();
            this.changeFeedProcessorBuilderInstance.monitoredContainer = this.initialCosmosContainer;
            if (this.initialChangesDelegate != null)
            {
                this.changeFeedProcessorBuilderInstance.observerFactory = new ChangeFeedObserverFactoryCore<T>(this.initialChangesDelegate);
            }
        }
    }
}