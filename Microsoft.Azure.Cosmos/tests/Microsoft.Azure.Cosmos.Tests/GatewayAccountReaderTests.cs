//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net.Http;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Microsoft.Azure.Documents;
    using System.Threading.Tasks;
    using System.Threading;
    using System.Net;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Tests for <see cref="GatewayAccountReader"/>.
    /// </summary>
    [TestClass]
    public class GatewayAccountReaderTests
    {
        [TestMethod]
        public void GatewayAccountReader_HttpClientFactory()
        {
            HttpClient staticHttpClient = new HttpClient();

            Mock<Func<HttpClient>> mockFactory = new Mock<Func<HttpClient>>();
            mockFactory.Setup(f => f()).Returns(staticHttpClient);

            GatewayAccountReader accountReader = new GatewayAccountReader(
                new Uri("https://localhost"),
                Mock.Of<IComputeHash>(),
                false,
                null,
                new ConnectionPolicy(),
                ApiType.None,
                mockFactory.Object);

            Mock.Get(mockFactory.Object)
                .Verify(f => f(), Times.Once);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GatewayAccountReader_HttpClientFactory_IfNull()
        {
            HttpClient staticHttpClient = null;

            Mock<Func<HttpClient>> mockFactory = new Mock<Func<HttpClient>>();
            mockFactory.Setup(f => f()).Returns(staticHttpClient);

            GatewayAccountReader accountReader = new GatewayAccountReader(
                new Uri("https://localhost"),
                Mock.Of<IComputeHash>(),
                false,
                null,
                new ConnectionPolicy(),
                ApiType.None,
                mockFactory.Object);
        }

        [TestMethod]
        public async Task GatewayAccountReader_MessageHandler()
        {
            HttpMessageHandler messageHandler = new CustomMessageHandler();
            HttpClient staticHttpClient = new HttpClient(messageHandler);

            Mock<Func<HttpClient>> mockFactory = new Mock<Func<HttpClient>>();
            mockFactory.Setup(f => f()).Returns(staticHttpClient);

            GatewayAccountReader accountReader = new GatewayAccountReader(
                new Uri("https://localhost"),
                Mock.Of<IComputeHash>(),
                false,
                null,
                new ConnectionPolicy(),
                ApiType.None,
                mockFactory.Object);

            DocumentClientException exception = await Assert.ThrowsExceptionAsync<DocumentClientException>(() => accountReader.InitializeReaderAsync());
            Assert.AreEqual(HttpStatusCode.Conflict, exception.StatusCode);

            Mock.Get(mockFactory.Object)
                .Verify(f => f(), Times.Once);
        }

        [TestMethod]
        public async Task DocumentClient_GetHttpClientFactory_WithHandler()
        {
            HttpMessageHandler messageHandler = new CustomMessageHandler();
            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            Func<HttpClient> factory = DocumentClient.GetHttpClientFactory(connectionPolicy, messageHandler);
            HttpClient httpClient = factory();
            Assert.IsNotNull(httpClient);
            HttpResponseMessage response = await httpClient.GetAsync("https://localhost");
            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        }

        [TestMethod]
        public void DocumentClient_GetHttpClientFactory_WithFactory()
        {
            HttpClient staticHttpClient = new HttpClient();

            Mock<Func<HttpClient>> mockFactory = new Mock<Func<HttpClient>>();
            mockFactory.Setup(f => f()).Returns(staticHttpClient);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                HttpClientFactory = mockFactory.Object
            };

            Func<HttpClient> factory = DocumentClient.GetHttpClientFactory(connectionPolicy, messageHandler: null);
            HttpClient httpClient = factory();
            Assert.IsNotNull(httpClient);

            Mock.Get(mockFactory.Object)
                .Verify(f => f(), Times.Once);
        }

        public class CustomMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict) { RequestMessage = request, Content = new StringContent("Notfound") });
            }
        }
    }
}
