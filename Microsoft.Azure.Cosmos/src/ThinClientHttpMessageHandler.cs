//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

/*namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;

    /// <summary>
    /// blabla
    /// </summary>
    internal class ThinClientHttpMessageHandler : DelegatingHandler
    {
        private readonly string accountName;
        private readonly Uri gatewayEndpoint;
        private readonly Uri thinClientEndpoint;
        private readonly BufferProviderWrapper bufferProvider;
        private readonly string customUserAgent;

        public ThinClientHttpMessageHandler(Uri gatewayEndpoint, Uri thinClientEndpoint, string accountName)
            : this(gatewayEndpoint, thinClientEndpoint, accountName, serverValidator: null)
        {
        }

        public ThinClientHttpMessageHandler(Uri gatewayEndpoint, Uri thinClientEndpoint, string accountName, RemoteCertificateValidationCallback serverValidator, string customUserAgent = "")
        {
            this.gatewayEndpoint = gatewayEndpoint;
            this.thinClientEndpoint = thinClientEndpoint;
            this.accountName = accountName ?? throw new ArgumentNullException(nameof(accountName));
            this.customUserAgent = customUserAgent;
            this.bufferProvider = new();
            SocketsHttpHandler clientHandler = new SocketsHttpHandler();
            clientHandler.EnableMultipleHttp2Connections = true;
            if (serverValidator != null)
            {
                clientHandler.SslOptions.RemoteCertificateValidationCallback = serverValidator;
            }

            this.InnerHandler = clientHandler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Contains(ProxyExtensions.RoutedViaProxy))
            {
                return this.SendViaProxyAsync(request, cancellationToken);
            }
            else
            {
                request.RequestUri = new Uri(this.gatewayEndpoint, request.RequestUri.PathAndQuery);
                return base.SendAsync(request, cancellationToken);
            }
        }

        private async Task<HttpResponseMessage> SendViaProxyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Stream contentStream = await ThinClientTransportSerializer.SerializeProxyRequestAsync(this.bufferProvider, this.accountName, request);

            // force Http2, post and route to the thin client endpoint.
            request.Content = new StreamContent(contentStream);
            request.Content.Headers.ContentLength = contentStream.Length;
            request.Headers.Clear();

            request.Version = new Version("2.0");
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            request.RequestUri = this.thinClientEndpoint;
            request.Method = HttpMethod.Post;

            if (!string.IsNullOrEmpty(this.customUserAgent))
            {
                request.Headers.UserAgent.ParseAdd(this.customUserAgent);
            }

            using HttpResponseMessage responseMessage = await base.SendAsync(request, cancellationToken);
            return await ProxyExtensions.ConvertProxyResponseAsync(responseMessage);
        }
    }
}*/
