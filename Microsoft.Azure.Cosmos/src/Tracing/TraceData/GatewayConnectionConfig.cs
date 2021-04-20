// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;

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
}
