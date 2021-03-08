// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using Newtonsoft.Json;

    internal sealed class ClientConfigurationTraceDatum : TraceDatum
    {
        public ClientConfigurationTraceDatum(CosmosClientContext cosmosClientContext, DateTime startTime)
        {
            this.ClientCreatedDateTimeUtc = startTime;
            this.UserAgentContainer = cosmosClientContext.DocumentClient.ConnectionPolicy.UserAgentContainer;
            this.GatewayConnectionConfig = new GatewayConnectionConfig(cosmosClientContext.DocumentClient.ConnectionPolicy.MaxConnectionLimit,
                                                                       cosmosClientContext.DocumentClient.ConnectionPolicy.RequestTimeout,
                                                                       cosmosClientContext.ClientOptions.WebProxy);

            this.RntbdConnectionConfig = cosmosClientContext.DocumentClient.RecordTcpSettings(this);

            this.OtherConnectionConfig = new OtherConnectionConfig(cosmosClientContext.DocumentClient.ConnectionPolicy.EnableEndpointDiscovery,
                                                cosmosClientContext.ClientOptions.AllowBulkExecution);

            this.ConsistencyConfig = new ConsistencyConfig(cosmosClientContext.ClientOptions.ConsistencyLevel,
                                                    cosmosClientContext.DocumentClient.ConnectionPolicy.UseMultipleWriteLocations,
                                                    cosmosClientContext.ClientOptions.ApplicationPreferredRegions);
        }

        public DateTime ClientCreatedDateTimeUtc { get; }

        public GatewayConnectionConfig GatewayConnectionConfig { get; }

        public RntbdConnectionConfig RntbdConnectionConfig { get; }

        public OtherConnectionConfig OtherConnectionConfig { get; }

        public ConsistencyConfig ConsistencyConfig { get; }

        internal readonly UserAgentContainer UserAgentContainer;

        internal override void Accept(ITraceDatumVisitor traceDatumVisitor)
        {
            traceDatumVisitor.Visit(this);
        }
    }

    internal class GatewayConnectionConfig
    {
        public GatewayConnectionConfig(
            int maxConnectionLimit,
            TimeSpan requestTimeout,
            IWebProxy webProxy)
        {
            this.MaxConnectionLimit = maxConnectionLimit;
            this.RequestTimeout = (int)requestTimeout.TotalSeconds;
            this.IsWebProxyConfigured = webProxy != null;
            this.lazyString = new Lazy<string>(() => string.Format(CultureInfo.InvariantCulture,
                                "(cps:{0}, rto:{1}, p:{2})",
                                maxConnectionLimit,
                                (int)requestTimeout.TotalSeconds,
                                webProxy != null));
            this.lazyJsonString = new Lazy<string>(() => JsonConvert.SerializeObject(this));
        }

        public int MaxConnectionLimit { get; }
        public int RequestTimeout { get; }
        public bool IsWebProxyConfigured { get; }

        private readonly Lazy<string> lazyString;
        private readonly Lazy<string> lazyJsonString;

        public override string ToString()
        {
            return this.lazyString.Value;
        }

        public string ToJsonString()
        {
            return this.lazyJsonString.Value;
        }
    }

    internal class RntbdConnectionConfig
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
            this.lazyString = new Lazy<string>(() => string.Format(CultureInfo.InvariantCulture,
                                "(cto: {0}, icto: {1}, mrpc: {2}, mcpe: {3}, erd: {4})",
                                connectionTimeout,
                                idleConnectionTimeout,
                                maxRequestsPerChannel,
                                maxRequestsPerEndpoint,
                                tcpEndpointRediscovery));
            this.lazyJsonString = new Lazy<string>(() => JsonConvert.SerializeObject(this));
        }

        public int ConnectionTimeout { get; }
        public int IdleConnectionTimeout { get; }
        public int MaxRequestsPerChannel { get; }
        public int MaxRequestsPerEndpoint { get; }
        public bool TcpEndpointRediscovery { get; }

        private readonly Lazy<string> lazyString;
        private readonly Lazy<string> lazyJsonString;

        public override string ToString()
        {
            return this.lazyString.Value;
        }

        public string ToJsonString()
        {
            return this.lazyJsonString.Value;
        }
    }

    internal class OtherConnectionConfig
    {
        public OtherConnectionConfig(
            bool enableEndpointDiscovery,
            bool allowBulkExecution)
        {
            this.EnableEndpointDiscovery = enableEndpointDiscovery;
            this.AllowBulkExecution = allowBulkExecution;
            this.lazyString = new Lazy<string>(() => string.Format(CultureInfo.InvariantCulture,
                                 "(ed:{0}, be:{1})",
                                 enableEndpointDiscovery,
                                 allowBulkExecution));
            this.lazyJsonString = new Lazy<string>(() => JsonConvert.SerializeObject(this));
        }

        public bool EnableEndpointDiscovery { get; }
        public bool AllowBulkExecution { get; }

        private readonly Lazy<string> lazyString;
        private readonly Lazy<string> lazyJsonString;

        public override string ToString()
        {
            return this.lazyString.Value;
        }

        public string ToJsonString()
        {
            return this.lazyJsonString.Value;
        }
    }

    internal class ConsistencyConfig
    {
        public ConsistencyConfig(
            ConsistencyLevel? consistencyLevel,
            bool multipleWriteLocationsEnabled,
            IReadOnlyList<string> preferredRegions)
        {
            this.ConsistencyLevel = consistencyLevel.GetValueOrDefault();
            this.MultipleWriteLocationsEnabled = multipleWriteLocationsEnabled;
            this.PreferredRegions = preferredRegions;
            this.lazyString = new Lazy<string>(() => string.Format(CultureInfo.InvariantCulture,
                                "(consistency: {0}, mm: {1}, prgns:[{2}])",
                                consistencyLevel.GetValueOrDefault(),
                                multipleWriteLocationsEnabled,
                                ConsistencyConfig.PreferredRegionsInternal(preferredRegions)));
            this.lazyJsonString = new Lazy<string>(() => JsonConvert.SerializeObject(this));
        }

        public ConsistencyLevel ConsistencyLevel { get; }
        public bool MultipleWriteLocationsEnabled { get; }
        public IReadOnlyList<string> PreferredRegions { get; }

        private readonly Lazy<string> lazyString;
        private readonly Lazy<string> lazyJsonString;

        public override string ToString()
        {
            return this.lazyString.Value;
        }

        public string ToJsonString()
        {
            return this.lazyJsonString.Value;
        }

        private static string PreferredRegionsInternal(IReadOnlyList<string> applicationPreferredRegions)
        {
            if (applicationPreferredRegions == null)
            {
                return string.Empty;
            }

            return string.Join(", ", applicationPreferredRegions);
        }
    }
}
