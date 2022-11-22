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
    /// <![CDATA[ 
    ///  CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(accountEndpoint: endpoint, authKeyOrResourceToken: key);
    ///  CosmosClient cosmosClient = cosmosClientBuilder
    ///                                 .WithDistributingTracing(<instance of DistributedTracingOptions>)
    ///                                 .Build();
    ///  ]]>
    /// </code>
    /// </para>
    /// 
    /// <para>
    /// Configure it using <see cref="Microsoft.Azure.Cosmos.CosmosClientOptions"/>
    /// <code language="c#">
    /// <![CDATA[ 
    ///   CosmosClient cosmosClient = new CosmosClient(accountEndpoint: endpoint, authKeyOrResourceToken: key, new CosmosClientOptions
    ///     {
    ///         EnableDistributedTracing = true,
    ///         DistributedTracingOptions = <instance of DistributedTracingOptions>
    ///     });
    ///  ]]>
    /// </code>
    /// </para>
    /// 
    /// <para>
    /// Configure it using <see cref="Microsoft.Azure.Cosmos.RequestOptions"/>
    /// <code language="c#">
    /// <![CDATA[ 
    ///   CosmosClient cosmosClient = new CosmosClient(accountEndpoint: endpoint, authKeyOrResourceToken: key, new CosmosClientOptions
    ///     {
    ///         EnableDistributedTracing = true
    ///     });
    ///     
    ///   await cosmosClient.CreateDatabaseAsync(
    ///     id: "test", 
    ///     requestOptions: new Cosmos.RequestOptions() 
    ///     {
    ///         DistributedTracingOptions = new DistributedTracingOptions()
    ///         {
    ///             DiagnosticsLatencyThreshold = TimeSpan.FromMilliseconds(1);
    ///         }
    ///     });
    /// ]]>
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

        /// <summary>
        /// Latency Threshold to generate (<see cref="System.Diagnostics.Tracing.EventSource"/>) with Request diagnostics in distributing Tracing.
        /// </summary>
        /// <example>
        /// Enable trace generation for high latency requests. So it will generate traces only those requests which are taking more than 1ms to execute (Supported by <see cref="Microsoft.Azure.Cosmos.RequestOptions"/> and <see cref="Microsoft.Azure.Cosmos.CosmosClientOptions"/> both)
        /// <code language="c#">
        /// <![CDATA[ 
        /// new DistributedTracingOptions() 
        /// { 
        ///    DiagnosticsLatencyThreshold = TimeSpan.FromMilliseconds(1)
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>If it is not set then by default it will generate (<see cref="System.Diagnostics.Tracing.EventSource"/>) for query operation which are taking more than 500 ms and non-query operations taking more than 100 ms.</remarks>
        public TimeSpan? DiagnosticsLatencyThreshold { get; set; }
    }
}
