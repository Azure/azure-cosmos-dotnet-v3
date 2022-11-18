// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// It contains configuration which can be set as part of <see cref="Microsoft.Azure.Cosmos.CosmosClientOptions"/> and <see cref="Microsoft.Azure.Cosmos.RequestOptions"/>
    /// </summary>
    /// <example>
    /// <para>
    /// Configure it using <see cref="Microsoft.Azure.Cosmos.Fluent.CosmosClientBuilder"/>
    /// <code language="c#">
    ///  CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(accountEndpoint: endpoint, authKeyOrResourceToken: key);
    ///  CosmosClient cosmosClient = cosmosClientBuilder
    ///                                 .WithDistributingTracing(instance of DistributedTracingOptions)
    ///                                 .Build();
    /// </code>
    /// </para>
    /// 
    /// <para>
    /// Configure it using <see cref="Microsoft.Azure.Cosmos.CosmosClientOptions"/>
    /// <code language="c#">
    ///  CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(accountEndpoint: endpoint, authKeyOrResourceToken: key);
    ///  CosmosClient cosmosClient = cosmosClientBuilder
    ///                                 .Build();
    ///  cosmosClient.ClientOptions.DistributedTracingOptions = new DistributedTracingOptions();
    /// </code>
    /// </para>
    /// 
    /// <para>
    /// Configure it using <see cref="Microsoft.Azure.Cosmos.RequestOptions"/>
    /// <code language="c#">
    ///  CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(accountEndpoint: endpoint, authKeyOrResourceToken: key);
    ///  CosmosClient cosmosClient = cosmosClientBuilder
    ///                                 .Build();
    ///  cosmosClient.CreateDatabaseAsync(
    ///     id: "test", 
    ///     requestOptions: new Cosmos.RequestOptions() 
    ///     {
    ///         DistributedTracingOptions = new DistributedTracingOptions()
    ///         {
    ///             DiagnosticsLatencyThreshold = TimeSpan.FromMilliseconds(1);
    ///         }
    ///     });
    /// </code>
    /// </para>
    /// </example>
#if PREVIEW
    public
#else
    internal
#endif
        sealed class DistributedTracingOptions
    {
        /// <summary>
        /// Default Latency threshold for other than query Operation
        /// </summary>
        internal static readonly TimeSpan DefaultCrudLatencyThreshold = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Default Latency threshold for QUERY operation
        /// </summary>
        internal static readonly TimeSpan DefaultQueryTimeoutThreshold = TimeSpan.FromMilliseconds(500);
        
        private bool enableDiagnosticsTraceForAllRequests;
        private TimeSpan? diagnosticsLatencyThreshold;

        /// <summary>
        /// Latency Threshold to generate (<see cref="System.Diagnostics.Tracing.EventSource"/>) with Request diagnostics in distributing Tracing.<br></br>
        /// </summary>
        /// <example>
        /// Enable trace generation for high latency requests. So it will generate traces only those requests which are taking more than 1ms to execute (Supported by <see cref="Microsoft.Azure.Cosmos.RequestOptions"/> and <see cref="Microsoft.Azure.Cosmos.CosmosClientOptions"/> both)
        /// <code language="c#">
        /// new DistributedTracingOptions() 
        /// { 
        ///    DiagnosticsLatencyThreshold = TimeSpan.FromMilliseconds(1)
        /// }
        /// </code>
        /// </example>
        /// <remarks>If it is not set then by default it will generate (<see cref="System.Diagnostics.Tracing.EventSource"/>) for query operation which are taking more than 500 ms and non-query operations taking more than 100 ms and this can not be configured if <see cref="Microsoft.Azure.Cosmos.DistributedTracingOptions.EnableDiagnosticsTraceForAllRequests"/> is enabled.</remarks>
        public TimeSpan? DiagnosticsLatencyThreshold
        {
            get => this.diagnosticsLatencyThreshold;
            set
            {
                if (this.EnableDiagnosticsTraceForAllRequests)
                {
                    throw new ArgumentException("EnableDiagnosticsTraceForAllRequests can not be true along with DiagnosticsLatencyThreshold.");
                }
                
                this.diagnosticsLatencyThreshold = value;
            }
        }

        /// <summary>
        /// When enabled, it generates (<see cref="System.Diagnostics.Tracing.EventSource"/>) containing request diagnostics string for all the operations.
        /// </summary>
        /// <example>
        /// <code language="c#">
        /// new DistributedTracingOptions() 
        /// { 
        ///    EnableDiagnosticsTraceForAllRequests = true
        /// }
        /// </code>
        /// </example>
        /// <remarks>This is NOT supported in RequestOptions. <see cref="EnableDiagnosticsTraceForAllRequests"/> cannot be enabled along with <see cref="DiagnosticsLatencyThreshold"/> configuration.</remarks>
        public bool EnableDiagnosticsTraceForAllRequests
        {
            get => this.enableDiagnosticsTraceForAllRequests;
            set
            {
                if (value && this.DiagnosticsLatencyThreshold != null)
                {
                    throw new ArgumentException("EnableDiagnosticsTraceForAllRequests can not be true along with DiagnosticsLatencyThreshold.");
                }
                
                this.enableDiagnosticsTraceForAllRequests = value;
            }
        }
    }
}
