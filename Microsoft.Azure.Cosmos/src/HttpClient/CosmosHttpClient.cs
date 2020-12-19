//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal abstract class CosmosHttpClient : IDisposable
    {
        public static readonly TimeSpan GatewayRequestTimeout = TimeSpan.FromSeconds(65);

        public abstract HttpMessageHandler HttpMessageHandler { get; }

        public abstract Task<HttpResponseMessage> GetAsync(
            Uri uri,
            INameValueCollection additionalHeaders,
            ResourceType resourceType,
            HttpTimeoutPolicy timeoutPolicy,
            ITrace trace,
            CancellationToken cancellationToken);

        public abstract Task<HttpResponseMessage> SendHttpAsync(
            Func<ValueTask<HttpRequestMessage>> createRequestMessageAsync,
            ResourceType resourceType,
            HttpTimeoutPolicy timeoutPolicy,
            ITrace trace,
            CancellationToken cancellationToken);

        protected abstract void Dispose(bool disposing);

        public abstract void Dispose();
    }
}
