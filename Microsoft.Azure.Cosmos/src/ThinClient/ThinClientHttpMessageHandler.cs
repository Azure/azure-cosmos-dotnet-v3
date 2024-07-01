//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ThinClient
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core;
    using static Microsoft.Azure.Cosmos.ThinClient.ProxyExtensions;

    /// <summary>
    /// HttpMessageHandler that redirects requests to the Proxy if so requested over the http headers on request.
    /// Must be on the latest handlers in the request processing pipe.
    /// </summary>
    /// <remarks>
    /// Make sure to put after the mTLS handler if design changes to support cross node communication over TLS.
    /// </remarks>
    public sealed class ThinClientHttpMessageHandler : DelegatingHandler
    {
        /// <summary>
        /// TODO wololo
        /// </summary>
        public const string AccountName = "x-ms-thinclient-account-name";

        private readonly Uri proxyEndpoint;

        private readonly Action<HttpRequestMessage> forceHttp20Action;

        /// <summary>
        /// TODO wololo
        /// </summary>
        /// <param name="proxyEndpoint"></param>
        /// <param name="innerHandler"></param>
        /// <param name="forceHttp20Action"></param>
        public ThinClientHttpMessageHandler(Uri proxyEndpoint, HttpMessageHandler innerHandler, Action<HttpRequestMessage> forceHttp20Action)
        {
            this.proxyEndpoint = proxyEndpoint;
            this.forceHttp20Action = forceHttp20Action;
            this.InnerHandler = innerHandler ?? new HttpClientHandler();
        }

        /// <summary>
        /// TODO wololo
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>TODO wololo2</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Contains(ProxyExtensions.RoutedViaProxy))
            {
                return this.SendViaProxyAsync(request, cancellationToken);
            }
            else
            {
                return base.SendAsync(request, cancellationToken);
            }
        }

        private async Task<HttpResponseMessage> SendViaProxyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Contract.Requires(request.Headers.TryGetValues(AccountName, out IEnumerable<string> accountName));
            BufferProviderWrapper bufferProviderWrapper = new ();
            Stream contentStream = await ProxyExtensions.SerializeProxyRequestAsync(bufferProviderWrapper, accountName.First(), request);

            // force Http2, post and route to the thin client endpoint.
            request.Content = new StreamContent(contentStream);
            request.Content.Headers.ContentLength = contentStream.Length;
            request.Headers.Clear();

            // Force Http 2.0 on the request
            this.forceHttp20Action.Invoke(request);

            request.RequestUri = this.proxyEndpoint;
            request.Method = HttpMethod.Post;

            using HttpResponseMessage responseMessage = await base.SendAsync(request, cancellationToken);

            return await ProxyExtensions.ConvertProxyResponseAsync(responseMessage);
        }
    }
}
