//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal abstract class CosmosHttpClient : IDisposable
    {
        public abstract HttpMessageHandler HttpMessageHandler { get; }

        public abstract Task<HttpResponseMessage> GetAsync(
            Uri uri,
            INameValueCollection additionalHeaders,
            ResourceType resourceType,
            CancellationToken cancellationToken);

        public abstract Task<HttpResponseMessage> SendHttpAsync(
            Func<ValueTask<HttpRequestMessage>> createRequestMessage,
            ResourceType resourceType,
            CancellationToken cancellationToken);

        protected abstract void Dispose(bool disposing);

        public abstract void Dispose();
    }
}
