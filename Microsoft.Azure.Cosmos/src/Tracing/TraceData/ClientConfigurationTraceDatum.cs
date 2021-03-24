// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;

    internal sealed class ClientConfigurationTraceDatum : TraceDatum
    {
        public ClientConfigurationTraceDatum(CosmosClientContext cosmosClientContext, DateTime startTime)
        {
            this.ClientCreatedDateTimeUtc = startTime;
            this.UserAgentContainer = cosmosClientContext.DocumentClient.ConnectionPolicy.UserAgentContainer;
            this.GatewayConnectionConfig = new GatewayConnectionConfig(cosmosClientContext.ClientOptions.GatewayModeMaxConnectionLimit,
                                                                       cosmosClientContext.ClientOptions.RequestTimeout,
                                                                       cosmosClientContext.ClientOptions.WebProxy,
                                                                       cosmosClientContext.ClientOptions.HttpClientFactory);

            this.RntbdConnectionConfig = cosmosClientContext.DocumentClient.RecordTcpSettings(this);

            this.OtherConnectionConfig = new OtherConnectionConfig(cosmosClientContext.ClientOptions.LimitToEndpoint,
                                                cosmosClientContext.ClientOptions.AllowBulkExecution);

            this.ConsistencyConfig = new ConsistencyConfig(cosmosClientContext.ClientOptions.ConsistencyLevel,
                                                    cosmosClientContext.ClientOptions.ApplicationPreferredRegions);

            this.cachedNumberOfClientCreated = CosmosClient.numberOfClientsCreated;
            this.cachedUserAgentString = this.UserAgentContainer.UserAgent;
            this.cachedSerializedJson = this.GetSerializedDatum();
        }

        public DateTime ClientCreatedDateTimeUtc { get; }

        public GatewayConnectionConfig GatewayConnectionConfig { get; }

        public RntbdConnectionConfig RntbdConnectionConfig { get; }

        public OtherConnectionConfig OtherConnectionConfig { get; }

        public ConsistencyConfig ConsistencyConfig { get; }

        public ReadOnlyMemory<byte> SerializedJson
        {
            get
            {
                if ((this.cachedUserAgentString != this.UserAgentContainer.UserAgent) ||
                    (this.cachedNumberOfClientCreated != CosmosClient.numberOfClientsCreated))
                {
                    this.cachedNumberOfClientCreated = CosmosClient.numberOfClientsCreated;
                    this.cachedUserAgentString = this.UserAgentContainer.UserAgent;
                    this.cachedSerializedJson = this.GetSerializedDatum();
                }

                return this.cachedSerializedJson;
            }
        }

        internal readonly UserAgentContainer UserAgentContainer;

        private ReadOnlyMemory<byte> cachedSerializedJson;
        private int cachedNumberOfClientCreated;
        private string cachedUserAgentString;

        internal override void Accept(ITraceDatumVisitor traceDatumVisitor)
        {
            traceDatumVisitor.Visit(this);
        }

        private ReadOnlyMemory<byte> GetSerializedDatum()
        {
            IJsonWriter jsonTextWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            jsonTextWriter.WriteObjectStart();

            jsonTextWriter.WriteFieldName("Client Created Time Utc");
            jsonTextWriter.WriteStringValue(this.ClientCreatedDateTimeUtc.ToString("o", CultureInfo.InvariantCulture));

            jsonTextWriter.WriteFieldName("NumberOfClientsCreated");
            jsonTextWriter.WriteNumber64Value(this.cachedNumberOfClientCreated);
            jsonTextWriter.WriteFieldName("User Agent");
            jsonTextWriter.WriteStringValue(this.cachedUserAgentString);

            jsonTextWriter.WriteFieldName("ConnectionConfig");
            jsonTextWriter.WriteObjectStart();

            jsonTextWriter.WriteFieldName("gw");
            jsonTextWriter.WriteStringValue(this.GatewayConnectionConfig.ToString());
            jsonTextWriter.WriteFieldName("rntbd");
            jsonTextWriter.WriteStringValue(this.RntbdConnectionConfig.ToString());
            jsonTextWriter.WriteFieldName("other");
            jsonTextWriter.WriteStringValue(this.OtherConnectionConfig.ToString());

            jsonTextWriter.WriteObjectEnd();

            jsonTextWriter.WriteFieldName("ConsistencyConfig");
            jsonTextWriter.WriteStringValue(this.ConsistencyConfig.ToString());
            jsonTextWriter.WriteObjectEnd();

            return jsonTextWriter.GetResult();
        }
    }

    internal class GatewayConnectionConfig
    {
        public GatewayConnectionConfig(
            int maxConnectionLimit,
            TimeSpan requestTimeout,
            IWebProxy webProxy,
            Func<HttpClient> httpClientFactory)
        {
            this.MaxConnectionLimit = maxConnectionLimit;
            this.UserRequestTimeout = (int)requestTimeout.TotalSeconds;
            this.IsWebProxyConfigured = webProxy != null;
            this.IsHttpClientFactoryConfigured = httpClientFactory != null;
            this.lazyString = new Lazy<string>(() => string.Format(CultureInfo.InvariantCulture,
                                "(cps:{0}, urto:{1}, p:{2}, httpf: {3})",
                                maxConnectionLimit,
                                (int)requestTimeout.TotalSeconds,
                                webProxy != null,
                                httpClientFactory != null));
            this.lazyJsonString = new Lazy<string>(() => Newtonsoft.Json.JsonConvert.SerializeObject(this));
        }

        public int MaxConnectionLimit { get; }
        public int UserRequestTimeout { get; }
        public bool IsWebProxyConfigured { get; }
        public bool IsHttpClientFactoryConfigured { get; }

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
            bool tcpEndpointRediscovery,
            PortReuseMode portReuseMode)
        {
            this.ConnectionTimeout = connectionTimeout;
            this.IdleConnectionTimeout = idleConnectionTimeout;
            this.MaxRequestsPerChannel = maxRequestsPerChannel;
            this.MaxRequestsPerEndpoint = maxRequestsPerEndpoint;
            this.TcpEndpointRediscovery = tcpEndpointRediscovery;
            this.PortReuseMode = portReuseMode;
            this.lazyString = new Lazy<string>(() => string.Format(CultureInfo.InvariantCulture,
                                "(cto: {0}, icto: {1}, mrpc: {2}, mcpe: {3}, erd: {4}, pr: {5})",
                                connectionTimeout,
                                idleConnectionTimeout,
                                maxRequestsPerChannel,
                                maxRequestsPerEndpoint,
                                tcpEndpointRediscovery,
                                portReuseMode.ToString()));
            this.lazyJsonString = new Lazy<string>(() => Newtonsoft.Json.JsonConvert.SerializeObject(this));
        }

        public int ConnectionTimeout { get; }
        public int IdleConnectionTimeout { get; }
        public int MaxRequestsPerChannel { get; }
        public int MaxRequestsPerEndpoint { get; }
        public bool TcpEndpointRediscovery { get; }
        public PortReuseMode PortReuseMode { get; }

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
            bool limitToEndpoint,
            bool allowBulkExecution)
        {
            this.LimitToEndpoint = limitToEndpoint;
            this.AllowBulkExecution = allowBulkExecution;
            this.lazyString = new Lazy<string>(() => string.Format(CultureInfo.InvariantCulture,
                                 "(ed:{0}, be:{1})",
                                 limitToEndpoint,
                                 allowBulkExecution));
            this.lazyJsonString = new Lazy<string>(() => Newtonsoft.Json.JsonConvert.SerializeObject(this));
        }

        public bool LimitToEndpoint { get; }
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
            IReadOnlyList<string> preferredRegions)
        {
            this.ConsistencyLevel = consistencyLevel.GetValueOrDefault();
            this.PreferredRegions = preferredRegions;
            this.lazyString = new Lazy<string>(() => string.Format(CultureInfo.InvariantCulture,
                                "(consistency: {0}, prgns:[{1}])",
                                consistencyLevel.GetValueOrDefault(),
                                ConsistencyConfig.PreferredRegionsInternal(preferredRegions)));
            this.lazyJsonString = new Lazy<string>(() => Newtonsoft.Json.JsonConvert.SerializeObject(this));
        }

        public ConsistencyLevel ConsistencyLevel { get; }
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
