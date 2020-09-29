//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;
    using System;
    using System.Net.Http;
    using System.Net;
    using System.Threading;

    [TestClass]
    public class CosmosHttpClientCoreTests
    {
        [TestMethod]
        public async Task ResponseMessageHasRequestMessageAsync()
        {
            // We don't set the RequestMessage property on purpose on the Failed response
            // This will make it go through GatewayStoreClient.CreateDocumentClientExceptionAsync
            Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc = request =>
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("test")
                };
                return Task.FromResult(response);
            };

            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            CosmosHttpClient cosmoshttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri("http://localhost"));

            HttpResponseMessage responseMessage = await cosmoshttpClient.SendHttpAsync(() => new ValueTask<HttpRequestMessage>(httpRequestMessage), ResourceType.Collection, null, default);

            Assert.AreEqual(httpRequestMessage, responseMessage.RequestMessage);
        }

        private class MockMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc;
            public MockMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> func)
            {
                this.sendFunc = func;
            }
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return await this.sendFunc(request);
            }
        }
    }
}
