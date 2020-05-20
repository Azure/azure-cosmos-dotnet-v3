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
    using Microsoft.Azure.Cosmos.Routing;
    using System.Threading.Tasks;
    using System.Threading;
    using System.Net;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Tests for <see cref="GatewayAddressCache"/>.
    /// </summary>
    [TestClass]
    public class GatewayAddressCacheTests
    {
        [TestMethod]
        public void GatewayAddressCache_HttpClientFactory()
        {
            HttpClient staticHttpClient = new HttpClient();

            Mock<Func<HttpClient>> mockFactory = new Mock<Func<HttpClient>>();
            mockFactory.Setup(f => f()).Returns(staticHttpClient);

            GatewayAddressCache addressCache = new GatewayAddressCache(
                new Uri("https://localhost"),
                Documents.Client.Protocol.Https,
                Mock.Of<IAuthorizationTokenProvider>(),
                new Documents.UserAgentContainer(),
                Mock.Of<IServiceConfigurationReader>(),
                TimeSpan.FromSeconds(60),
                mockFactory.Object);

            Mock.Get(mockFactory.Object)
                .Verify(f => f(), Times.Once);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GatewayAddressCache_HttpClientFactory_IfNull()
        {
            HttpClient staticHttpClient = null;

            Mock<Func<HttpClient>> mockFactory = new Mock<Func<HttpClient>>();
            mockFactory.Setup(f => f()).Returns(staticHttpClient);

            GatewayAddressCache addressCache = new GatewayAddressCache(
                new Uri("https://localhost"),
                Documents.Client.Protocol.Https,
                Mock.Of<IAuthorizationTokenProvider>(),
                new Documents.UserAgentContainer(),
                Mock.Of<IServiceConfigurationReader>(),
                TimeSpan.FromSeconds(60),
                mockFactory.Object);
        }

        [TestMethod]
        public void GatewayAddressCache_MessageHandler()
        {
            HttpMessageHandler messageHandler = new CustomMessageHandler();
            HttpClient staticHttpClient = new HttpClient(messageHandler);

            Mock<Func<HttpClient>> mockFactory = new Mock<Func<HttpClient>>();
            mockFactory.Setup(f => f()).Returns(staticHttpClient);

            GatewayAddressCache addressCache = new GatewayAddressCache(
                new Uri("https://localhost"),
                Documents.Client.Protocol.Https,
                Mock.Of<IAuthorizationTokenProvider>(),
                new Documents.UserAgentContainer(),
                Mock.Of<IServiceConfigurationReader>(),
                TimeSpan.FromSeconds(60),
                mockFactory.Object);

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
