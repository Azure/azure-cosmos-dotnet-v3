// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// This class contains configuration which can be set as part of <see cref="Microsoft.Azure.Cosmos.CosmosClientOptions"/> and <see cref="Microsoft.Azure.Cosmos.RequestOptions"/>
    /// <br></br><br></br>
    /// <b> Configure it using <see cref="Microsoft.Azure.Cosmos.Fluent.CosmosClientBuilder"/></b>
    /// <code>
    ///  CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(accountEndpoint: endpoint, authKeyOrResourceToken: key);
    ///  CosmosClient cosmosClient = cosmosClientBuilder
    ///                                 .WithDistributingTracing(instance of DistributedTracingOptions)
    ///                                 .Build();
    /// </code>
    /// <br></br><br></br>
    /// <b> Configure it using <see cref="Microsoft.Azure.Cosmos.CosmosClientOptions"/></b>
    /// <code>
    ///  CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(accountEndpoint: endpoint, authKeyOrResourceToken: key);
    ///  CosmosClient cosmosClient = cosmosClientBuilder
    ///                                 .Build();
    ///  cosmosClient.ClientOptions.DistributedTracingOptions = new DistributedTracingOptions();
    /// </code>
    /// <br></br><br></br>
    /// <b> Configure it using <see cref="Microsoft.Azure.Cosmos.RequestOptions"/></b>
    /// <code>
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
    /// </summary>
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
        /// If it is not set then by default it will generate (<see cref="System.Diagnostics.Tracing.EventSource"/>) for query operation which are taking more than 500 ms and non-query operations taking more than 100 ms.
        /// <br></br><br></br>
        /// <b> Enable trace generation for high latency requests. So it will generate traces only those requests which are taking more than 1ms to execute (Supported by <see cref="Microsoft.Azure.Cosmos.RequestOptions"/> and <see cref="Microsoft.Azure.Cosmos.CosmosClientOptions"/> both)</b>
        /// <code>
        /// new DistributedTracingOptions() 
        /// { 
        ///    DiagnosticsLatencyThreshold = TimeSpan.FromMilliseconds(1)
        /// }
        /// </code>
        /// </summary>
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
        /// Enable it, if you want to generate (<see cref="System.Diagnostics.Tracing.EventSource"/>) containing request diagnostics string for all the operations.
        /// If EnableDiagnosticsTraceForAllRequests is enabled then, it won't honour <see cref="DiagnosticsLatencyThreshold"/> configuration to generate diagnostic traces.
        /// <br></br><br></br>
        /// <b> Enable trace generation with request diagnostics for all requests (<see cref="Microsoft.Azure.Cosmos.RequestOptions"/> doesn't support this property))</b>
        /// <code>
        /// new DistributedTracingOptions() 
        /// { 
        ///    EnableDiagnosticsTraceForAllRequests = true
        /// }
        /// </code>
        /// </summary>
        /// <remarks>This is NOT supported in RequestOptions</remarks>
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
