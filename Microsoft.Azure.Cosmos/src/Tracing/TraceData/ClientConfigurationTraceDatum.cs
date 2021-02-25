// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;

    internal sealed class ClientConfigurationTraceDatum : TraceDatum
    {
        public ClientConfigurationTraceDatum(CosmosClientContext cosmosClientContext, DateTime startTime)
        {
            this.ClientCreatedDateTimeUtc = startTime;
            this.RecordClientConfig(cosmosClientContext);
        }

        public DateTime ClientCreatedDateTimeUtc { get; }

        public int NumberOfClients { get; set; }

        public string UserAgent { get; set; }

        public GatewayConnectionConfig GatewayConnectionConfig { get; set; }

        public RntbdConnectionConfig RntbdConnectionConfig { get; set; }

        public OtherConnectionConfig OtherConnectionConfig { get; set; }

        public ConsistencyConfig ConsistencyConfig { get; set; }

        internal override void Accept(ITraceDatumVisitor traceDatumVisitor)
        {
            traceDatumVisitor.Visit(this);
        }

        private void RecordClientConfig(CosmosClientContext cosmosClientContext)
        {
            this.GatewayConnectionConfig = new GatewayConnectionConfig(cosmosClientContext.DocumentClient.ConnectionPolicy.MaxConnectionLimit,
                                                                       cosmosClientContext.DocumentClient.ConnectionPolicy.RequestTimeout,
                                                                       cosmosClientContext.ClientOptions.WebProxy);

            cosmosClientContext.DocumentClient.RecordTcpSettings(this);

            this.OtherConnectionConfig = new OtherConnectionConfig(cosmosClientContext.DocumentClient.ConnectionPolicy.EnableEndpointDiscovery,
                                                cosmosClientContext.ClientOptions.AllowBulkExecution);
            
            this.ConsistencyConfig = new ConsistencyConfig(cosmosClientContext.ClientOptions.ConsistencyLevel,
                                                    cosmosClientContext.DocumentClient.ConnectionPolicy.UseMultipleWriteLocations,
                                                    cosmosClientContext.ClientOptions.ApplicationPreferredRegions);

            this.NumberOfClients = CosmosClient.numberOfClients;
            this.UserAgent = this.UserAgent;
        }
    }

    internal struct GatewayConnectionConfig
    {
        public GatewayConnectionConfig(
            int maxConnectionLimit,
            TimeSpan requestTimeout,
            IWebProxy webProxy)
        {
            this.MaxConnectionLimit = maxConnectionLimit;
            this.RequestTimeout = (int)requestTimeout.TotalSeconds;
            this.IsWebProxyConfigured = webProxy != null;
        }

        public int MaxConnectionLimit { get; }
        public int RequestTimeout { get; }
        public bool IsWebProxyConfigured { get; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                                "(cps:{0}, rto:{1}, p:{2})",
                                this.MaxConnectionLimit,
                                this.RequestTimeout,
                                this.IsWebProxyConfigured);
        }
    }

    internal struct RntbdConnectionConfig
    {
        public RntbdConnectionConfig(
            int connectionTimeout,
            int idleConnectionTimeout,
            int maxRequestsPerChannel,
            int maxRequestsPerEndpoint,
            bool tcpEndpointRediscovery)
        {
            this.ConnectionTimeout = connectionTimeout;
            this.IdleConnectionTimeout = idleConnectionTimeout;
            this.MaxRequestsPerChannel = maxRequestsPerChannel;
            this.MaxRequestsPerEndpoint = maxRequestsPerEndpoint;
            this.TcpEndpointRediscovery = tcpEndpointRediscovery;
        }

        public int ConnectionTimeout { get; }
        public int IdleConnectionTimeout { get; }
        public int MaxRequestsPerChannel { get; }
        public int MaxRequestsPerEndpoint { get; }
        public bool TcpEndpointRediscovery { get; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                                "(cto: {0}, icto: {1}, mrpc: {2}, mcpe: {3}, erd: {4})",
                                this.ConnectionTimeout,
                                this.IdleConnectionTimeout,
                                this.MaxRequestsPerChannel,
                                this.MaxRequestsPerEndpoint,
                                this.TcpEndpointRediscovery);
        }
    }

    internal struct OtherConnectionConfig
    {
        public OtherConnectionConfig(
            bool enableEndpointDiscovery,
            bool allowBulkExecution)
        {
            this.EnableEndpointDiscovery = enableEndpointDiscovery;
            this.AllowBulkExecution = allowBulkExecution;
        }

        public bool EnableEndpointDiscovery { get; }
        public bool AllowBulkExecution { get; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                                 "(ed:{0}, be:{1})",
                                 this.EnableEndpointDiscovery,
                                 this.AllowBulkExecution);
        }
    }

    internal struct ConsistencyConfig
    {
        public ConsistencyConfig(
            ConsistencyLevel? consistencyLevel,
            bool multipleWriteLocationsEnabled,
            IReadOnlyList<string> preferredRegions)
        {
            this.ConsistencyLevel = consistencyLevel.GetValueOrDefault();
            this.MultipleWriteLocationsEnabled = multipleWriteLocationsEnabled;
            this.PreferredRegions = preferredRegions;
        }

        public ConsistencyLevel ConsistencyLevel { get; }
        public bool MultipleWriteLocationsEnabled { get; }
        public IReadOnlyList<string> PreferredRegions { get; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                                "(consistency: {0}, mm: {1}, prgns:[{2}])",
                                this.ConsistencyLevel,
                                this.MultipleWriteLocationsEnabled,
                                this.PreferredRegionsInternal(this.PreferredRegions));
        }

        private string PreferredRegionsInternal(IReadOnlyList<string> applicationPreferredRegions)
        {
            if (applicationPreferredRegions == null)
            {
                return string.Empty;
            }

            return string.Join(", ", applicationPreferredRegions);
        }
    }
}
