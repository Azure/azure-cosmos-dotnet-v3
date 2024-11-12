namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Helper to allow Mock a HttpMessageHandler
    /// </summary>
    public class HttpHandlerHelper : HttpMessageHandler
    {
        public IHttpHandler MockHttpHandler { get; }
        public HttpHandlerHelper(IHttpHandler mockHttpHandler)
        {
            this.MockHttpHandler = mockHttpHandler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return this.MockHttpHandler.SendAsync(request, cancellationToken);
        }
    }
}