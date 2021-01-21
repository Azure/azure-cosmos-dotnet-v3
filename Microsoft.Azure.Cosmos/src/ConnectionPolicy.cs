//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Net.Http;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;

    /// <summary>
    /// Represents the connection policy associated with a DocumentClient to connect to the Azure Cosmos DB service.
    /// </summary>
    internal sealed class ConnectionPolicy
    {
        private const int defaultRequestTimeout = 10;
        // defaultMediaRequestTimeout is based upon the blob client timeout and the retry policy.
        private const int defaultMediaRequestTimeout = 300;
        private const int defaultMaxConcurrentFanoutRequests = 32;
        private const int defaultMaxConcurrentConnectionLimit = 50;

        internal UserAgentContainer UserAgentContainer;
        private static ConnectionPolicy defaultPolicy;

        private Protocol connectionProtocol;
        private ObservableCollection<string> preferredLocations;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionPolicy"/> class to connect to the Azure Cosmos DB service.
        /// </summary>
        public ConnectionPolicy()
        {
            this.connectionProtocol = Protocol.Https;
            this.RequestTimeout = TimeSpan.FromSeconds(ConnectionPolicy.defaultRequestTimeout);
            this.MediaRequestTimeout = TimeSpan.FromSeconds(ConnectionPolicy.defaultMediaRequestTimeout);
            this.ConnectionMode = ConnectionMode.Gateway;
            this.MaxConcurrentFanoutRequests = defaultMaxConcurrentFanoutRequests;
            this.MediaReadMode = MediaReadMode.Buffered;
            this.UserAgentContainer = new UserAgentContainer();
            this.preferredLocations = new ObservableCollection<string>();
            this.EnableEndpointDiscovery = true;
            this.MaxConnectionLimit = defaultMaxConcurrentConnectionLimit;
            this.RetryOptions = new RetryOptions();
            this.EnableReadRequestsFallback = null;
        }

        /// <summary>
        /// Automatically populates the <see cref="PreferredLocations"/> for geo-replicated database accounts in the Azure Cosmos DB service,
        /// based on the current region that the client is running in.
        /// </summary>
        /// <param name="location">The current region that this client is running in. E.g. "East US" </param>
        public void SetCurrentLocation(string location)
        {
            if (!RegionProximityUtil.SourceRegionToTargetRegionsRTTInMs.ContainsKey(location))
            {
                throw new ArgumentException("Current location is not a valid Azure region.");
            }

            List<string> proximityBasedPreferredLocations = RegionProximityUtil.GeneratePreferredRegionList(location);

            if (proximityBasedPreferredLocations != null)
            {
                this.preferredLocations.Clear();
                foreach (string preferredLocation in proximityBasedPreferredLocations)
                {
                    this.preferredLocations.Add(preferredLocation);
                }
            }
        }

        public void SetPreferredLocations(IReadOnlyList<string> regions)
        {
            if (regions == null)
            {
                throw new ArgumentNullException(nameof(regions));
            }

            this.preferredLocations.Clear();
            foreach (string preferredLocation in regions)
            {
                this.preferredLocations.Add(preferredLocation);
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of concurrent fanout requests sent to the Azure Cosmos DB service.
        /// </summary>
        /// <value>Default value is 32.</value>
        internal int MaxConcurrentFanoutRequests
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the request timeout in seconds when connecting to the Azure Cosmos DB service.
        /// The number specifies the time to wait for response to come back from network peer.
        /// </summary>
        /// <value>Default value is 10 seconds.</value>
        public TimeSpan RequestTimeout
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the media request timeout in seconds when connecting to the Azure Cosmos DB service.
        /// The number specifies the time to wait for response to come back from network peer for attachment content (a.k.a. media) operations.
        /// </summary>
        /// <value>
        /// Default value is 300 seconds.
        /// </value>
        public TimeSpan MediaRequestTimeout
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the connection mode used by the client when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Default value is <see cref="Cosmos.ConnectionMode.Gateway"/>
        /// </value>
        /// <remarks>
        /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/documentdb/documentdb-performance-tips#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        public ConnectionMode ConnectionMode
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the attachment content (a.k.a. media) download mode when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Default value is <see cref="Cosmos.MediaReadMode.Buffered"/>.
        /// </value>
        public MediaReadMode MediaReadMode
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the connection protocol when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Default value is <see cref="Protocol.Https"/>.
        /// </value>
        /// <remarks>
        /// This setting is not used when <see cref="ConnectionMode"/> is set to <see cref="Cosmos.ConnectionMode.Gateway"/>.
        /// Gateway mode only supports HTTPS.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/documentdb/documentdb-performance-tips#use-tcp">Connection policy: Use the TCP protocol</see>.
        /// </remarks>
        public Protocol ConnectionProtocol
        {
            get
            {
                return this.connectionProtocol;
            }

            set
            {
                if (value != Protocol.Https && value != Protocol.Tcp)
                {
                    throw new ArgumentOutOfRangeException("value");
                }

                this.connectionProtocol = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to allow for reads to go to multiple regions configured on an account of Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Default value is null.
        /// </value>
        /// <remarks>
        /// If this property is not set, the default is true for all Consistency Levels other than Bounded Staleness,
        /// The default is false for Bounded Staleness.
        /// This property only has effect if the following conditions are satisifed:
        /// 1. <see cref="EnableEndpointDiscovery"/> is true
        /// 2. the Azure Cosmos DB account has more than one region
        /// </remarks>
        public bool? EnableReadRequestsFallback
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the flag to enable address cache refresh on connection reset notification.
        /// </summary>
        /// <value>
        /// The default value is false
        /// </value>
        public bool EnableTcpConnectionEndpointRediscovery
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the default connection policy used to connect to the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Refer to the default values for the individual properties of <see cref="ConnectionPolicy"/> that determine the default connection policy.
        /// </value>
        public static ConnectionPolicy Default
        {
            get
            {
                if (ConnectionPolicy.defaultPolicy == null)
                {
                    ConnectionPolicy.defaultPolicy = new ConnectionPolicy();
                }
                return ConnectionPolicy.defaultPolicy;
            }
        }

        /// <summary>
        /// A suffix to be added to the default user-agent for the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        public string UserAgentSuffix
        {
            get
            {
                return this.UserAgentContainer.Suffix;
            }
            set
            {
                this.UserAgentContainer.Suffix = value;
            }
        }

        /// <summary>
        /// Gets and sets the preferred locations (regions) for geo-replicated database accounts in the Azure Cosmos DB service.
        /// For example, "East US" as the preferred location.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <see cref="EnableEndpointDiscovery"/> is true and the value of this property is non-empty,
        /// the SDK uses the locations in the collection in the order they are specified to perform operations,
        /// otherwise if the value of this property is not specified,
        /// the SDK uses the write region as the preferred location for all operations.
        /// </para>
        /// <para>
        /// If <see cref="EnableEndpointDiscovery"/> is set to false, the value of this property is ignored.
        /// </para>
        /// </remarks>
        public Collection<string> PreferredLocations
        {
            get
            {
                return this.preferredLocations;
            }
        }

        /// <summary>
        /// Gets or sets the flag to enable endpoint discovery for geo-replicated database accounts in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// When the value of this property is true, the SDK will automatically discover the
        /// current write and read regions to ensure requests are sent to the correct region
        /// based on the regions specified in the <see cref="PreferredLocations"/> property.
        /// <value>Default value is true indicating endpoint discovery is enabled.</value>
        /// </remarks>
        public bool EnableEndpointDiscovery
        {
            get;
            set;
        }

        public bool EnablePartitionLevelFailover
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the flag to enable writes on any locations (regions) for geo-replicated database accounts in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// When the value of this property is true, the SDK will direct write operations to
        /// available writable locations of geo-replicated database account. Writable locations
        /// are ordered by <see cref="PreferredLocations"/> property. Setting the property value
        /// to true has no effect until <see cref="AccountProperties.EnableMultipleWriteLocations"/> 
        /// is also set to true.
        /// <value>Default value is false indicating that writes are only directed to
        /// first region in <see cref="PreferredLocations"/> property.</value>
        /// </remarks>
        public bool UseMultipleWriteLocations
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the maximum number of concurrent connections allowed for the target
        /// service endpoint in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// This setting is only applicable in Gateway mode.
        /// </remarks>
        /// <value>Default value is 50.</value>
        public int MaxConnectionLimit
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the <see cref="RetryOptions"/> associated
        /// with the <see cref="DocumentClient"/> in the Azure Cosmos DB service.
        /// </summary>
        /// <seealso cref="DocumentClient"/>
        /// <seealso cref="ConnectionPolicy"/>
        /// <seealso cref="RetryOptions"/>
        /// <example>
        /// The example below creates a new <see cref="DocumentClient"/> and sets the <see cref="ConnectionPolicy"/>
        /// using the <see cref="RetryOptions"/> property.
        /// <para>
        /// <see cref="Cosmos.RetryOptions.MaxRetryAttemptsOnThrottledRequests"/> is set to 3, so in this case, if a request operation is rate limited by exceeding the reserved 
        /// throughput for the collection, the request operation retries 3 times before throwing the exception to the application.
        /// <see cref="Cosmos.RetryOptions.MaxRetryWaitTimeInSeconds"/> is set to 60, so in this case if the cumulative retry 
        /// wait time in seconds since the first request exceeds 60 seconds, the exception is thrown.
        /// </para>
        /// <code language="c#">
        /// <![CDATA[
        /// ConnectionPolicy connectionPolicy = new ConnectionPolicy();
        /// connectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests = 3;
        /// connectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds = 60;
        ///
        /// DocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key", connectionPolicy);
        /// ]]>
        /// </code>
        /// </example>
        /// <value>
        /// If this property is not set, the SDK uses the default retry policy that has <see cref="Cosmos.RetryOptions.MaxRetryAttemptsOnThrottledRequests"/>
        /// set to 9 and <see cref="Cosmos.RetryOptions.MaxRetryWaitTimeInSeconds"/> set to 30 seconds.
        /// </value>
        /// <remarks>
        /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/documentdb/documentdb-performance-tips#429">Handle rate limiting/request rate too large</see>.
        /// </remarks>
        public RetryOptions RetryOptions
        {
            get;
            set;
        }

        /// <summary>
        /// (Direct/TCP) Controls the amount of idle time after which unused connections are closed.
        /// </summary>
        /// <value>
        /// By default, idle connections are kept open indefinitely. Value must be greater than or equal to 10 minutes. Recommended values are between 20 minutes and 24 hours.
        /// </value>
        /// <remarks>
        /// Mainly useful for sparse infrequent access to a large database account.
        /// </remarks>
        public TimeSpan? IdleTcpConnectionTimeout
        {
            get;
            set;
        }

        /// <summary>
        /// (Direct/TCP) Controls the amount of time allowed for trying to establish a connection.
        /// </summary>
        /// <value>
        /// The default timeout is 5 seconds. Recommended values are greater than or equal to 5 seconds.
        /// </value>
        /// <remarks>
        /// When the time elapses, the attempt is cancelled and an error is returned. Longer timeouts will delay retries and failures.
        /// </remarks>
        public TimeSpan? OpenTcpConnectionTimeout
        {
            get;
            set;
        }

        /// <summary>
        /// (Direct/TCP) Controls the number of requests allowed simultaneously over a single TCP connection. When more requests are in flight simultaneously, the direct/TCP client will open additional connections.
        /// </summary>
        /// <value>
        /// The default settings allow 30 simultaneous requests per connection.
        /// Do not set this value lower than 4 requests per connection or higher than 50-100 requests per connection. 
        /// The former can lead to a large number of connections to be created. 
        /// The latter can lead to head of line blocking, high latency and timeouts.
        /// </value>
        /// <remarks>
        /// Applications with a very high degree of parallelism per connection, with large requests or responses, or with very tight latency requirements might get better performance with 8-16 requests per connection.
        /// </remarks>
        public int? MaxRequestsPerTcpConnection
        {
            get;
            set;
        }

        /// <summary>
        /// (Direct/TCP) Controls the maximum number of TCP connections that may be opened to each Cosmos DB back-end.
        /// Together with MaxRequestsPerTcpConnection, this setting limits the number of requests that are simultaneously sent to a single Cosmos DB back-end(MaxRequestsPerTcpConnection x MaxTcpConnectionPerEndpoint).
        /// </summary>
        /// <value>
        /// The default value is 65,535. Value must be greater than or equal to 16.
        /// </value>
        public int? MaxTcpConnectionsPerEndpoint
        {
            get;
            set;
        }

        /// <summary>
        /// (Direct/TCP) Controls the client port reuse policy used by the transport stack.
        /// </summary>
        /// <value>
        /// The default value is PortReuseMode.ReuseUnicastPort.
        /// </value>
        public PortReuseMode? PortReuseMode
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a delegate to use to obtain an HttpClient instance to be used for HTTPS communication.
        /// </summary>
        public Func<HttpClient> HttpClientFactory
        {
            get;
            set;
        }

        /// <summary>
        /// (Direct/TCP) This is an advanced setting that controls the number of TCP connections that will be opened eagerly to each Cosmos DB back-end.
        /// </summary>
        /// <value>
        /// Default value is 1. Applications with extreme performance requirements can set this value to 2.
        /// </value>
        /// <remarks>
        /// This setting must be used with caution. When used improperly, it can lead to client machine ephemeral port exhaustion <see href="https://docs.microsoft.com/en-us/azure/load-balancer/load-balancer-outbound-connections">Azure SNAT port exhaustion</see>.
        /// </remarks>
        internal int? MaxTcpPartitionCount
        {
            get;
            set;
        }

        /// <summary>
        /// GlobalEndpointManager will subscribe to this event if user updates the preferredLocations list in the Azure Cosmos DB service.
        /// </summary>
        internal event NotifyCollectionChangedEventHandler PreferenceChanged
        {
            add
            {
                this.preferredLocations.CollectionChanged += value;
            }
            remove
            {
                this.preferredLocations.CollectionChanged -= value;
            }
        }

        internal RetryWithConfiguration GetRetryWithConfiguration()
        {
            return this.RetryOptions?.GetRetryWithConfiguration();
        }
    }
}