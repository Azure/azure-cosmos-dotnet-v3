// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Globalization;

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
}