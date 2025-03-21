//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ThinClientTransportSerializerTests
    {
        // A single base64-encoded RNTBD response representing an HTTP 201 (Created) in RNTBD format.
        private const string base64MockResponse =
            "9AEAAMkAAAAIvhHfD23jSaynaR+gyTZ3AAAAAQIAByFUaHUsIDEzIEZlYiAyMDI1IDE0OjI1OjI4LjAyNCBHTVQEAAgmACIwMDAwYWQzZS0wMDAwLTAyMDAtMDAwMC02N2FlNjRjMDAwMDAiDgAIVABkb2N1bWVudFNpemU9NTEyMDA7ZG9jdW1lbnRzU2l6ZT01MjQyODgwMDtkb2N1bWVudHNDb3VudD0tMTtjb2xsZWN0aW9uU2l6ZT01MjQyODgwMDsPAAhBAGRvY3VtZW50U2l6ZT0wO2RvY3VtZW50c1NpemU9MTtkb2N1bWVudHNDb3VudD04O2NvbGxlY3Rpb25TaXplPTM7EAAHBDEuMTkTAAUKAAAAAAAAABUADgzDMAzDMBxAFwAIOgBkYnMvdGhpbi1jbGllbnQtdGVzdC1kYi9jb2xscy90aGluLWNsaWVudC10ZXN0LWNvbnRhaW5lci0xGAAIDABOSDF1QUo2QU5tMD0aAAUJAAAAAAAAAB4AAgMAAAAfAAIEAAAAIQAIAQAwJgACAQAAACkABQkAAAAAAAAAMAACAAAAADUAAgEAAAA6AAUKAAAAAAAAADsABQkAAAAAAAAAPgAIBQAtMSMxMFEADkjhehSuRxBAYwAIAQAweAAF//////////89AQAAeyJpZCI6IjNiMTFiNDM2LTViMTUtNGQwZS1iZWYwLWY1MzVmNjA0MTQxYyIsInBrIjoicGsiLCJuYW1lIjoiODM2MzI0NTA2IiwiZW1haWwiOiJhYmNAZGVmLmNvbSIsImJvZHkiOiJibGFibGEiLCJfcmlkIjoiTkgxdUFKNkFObTBKQUFBQUFBQUFBQT09IiwiX3NlbGYiOiJkYnMvTkgxdUFBPT0vY29sbHMvTkgxdUFKNkFObTA9L2RvY3MvTkgxdUFKNkFObTBKQUFBQUFBQUFBQT09LyIsIl9ldGFnIjoiXCIwMDAwYWQzZS0wMDAwLTAyMDAtMDAwMC02N2FlNjRjMDAwMDBcIiIsIl9hdHRhY2htZW50cyI6ImF0dGFjaG1lbnRzLyIsIl90cyI6MTczOTQ4MjMwNH0=";

        [TestMethod]
        public async Task SerializeProxyRequestAsync_ShouldThrowIfNoPartitionKeyInPointOperation()
        {
            // Arrange
            HttpRequestMessage message = new HttpRequestMessage
            {
                RequestUri = new Uri("https://localhost/dbs/TestDb/colls/TestColl/docs/TestDoc")
            };
            message.Headers.Add(ThinClientConstants.ProxyOperationType, OperationType.Read.ToString());
            message.Headers.Add(ThinClientConstants.ProxyResourceType, ResourceType.Document.ToString());
            message.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());

            ThinClientTransportSerializer.BufferProviderWrapper bufferProvider = new();

            Mock<ISessionContainer> sessionContainerMock = new Mock<ISessionContainer>();
            Mock<IStoreModel> storeModelMock = new Mock<IStoreModel>();
            Mock<ICosmosAuthorizationTokenProvider> tokenProviderMock = new Mock<ICosmosAuthorizationTokenProvider>();
            Mock<IRetryPolicyFactory> retryPolicyFactoryMock = new Mock<IRetryPolicyFactory>();
            TelemetryToServiceHelper telemetry = null; // or mock if needed

            Mock<ClientCollectionCache> clientCollectionCacheMock = new Mock<ClientCollectionCache>(
                sessionContainerMock.Object,
                storeModelMock.Object,
                tokenProviderMock.Object,
                retryPolicyFactoryMock.Object,
                telemetry,
                true)
            {
                CallBase = true
            };

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InternalServerErrorException>(
                () => ThinClientTransportSerializer.SerializeProxyRequestAsync(
                    bufferProvider,
                    "MockAccount",
                    clientCollectionCacheMock.Object,
                    message),
                "Expected an InternalServerErrorException for missing PartitionKey in point operation");
        }

        [TestMethod]
        public async Task SerializeProxyRequestAsync_ShouldNotThrowIfPartitionKeyProvided()
        {
            // Arrange
            HttpRequestMessage message = new HttpRequestMessage
            {
                Content = new StringContent("Test Body"),
                RequestUri = new Uri("https://localhost/dbs/TestDb/colls/TestColl/docs/TestDoc")
            };
            message.Headers.Add(ThinClientConstants.ProxyOperationType, OperationType.Read.ToString());
            message.Headers.Add(ThinClientConstants.ProxyResourceType, ResourceType.Document.ToString());
            message.Headers.TryAddWithoutValidation(HttpConstants.HttpHeaders.PartitionKey, "[\"SamplePk\"]");
            message.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());

            ThinClientTransportSerializer.BufferProviderWrapper bufferProvider = new();

            Mock<ISessionContainer> sessionContainerMock = new Mock<ISessionContainer>();
            Mock<IStoreModel> storeModelMock = new Mock<IStoreModel>();
            Mock<ICosmosAuthorizationTokenProvider> tokenProviderMock = new Mock<ICosmosAuthorizationTokenProvider>();
            Mock<IRetryPolicyFactory> retryPolicyFactoryMock = new Mock<IRetryPolicyFactory>();
            TelemetryToServiceHelper telemetry = null;

            Mock<ClientCollectionCache> clientCollectionCacheMock = new Mock<ClientCollectionCache>(
                sessionContainerMock.Object,
                storeModelMock.Object,
                tokenProviderMock.Object,
                retryPolicyFactoryMock.Object,
                telemetry,
                true)
            {
                CallBase = true
            };

            clientCollectionCacheMock
                .Setup(c => c.ResolveCollectionAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<ITrace>()))
                .ReturnsAsync(this.GetMockContainerProperties());

            // Act
            Stream resultStream = await ThinClientTransportSerializer.SerializeProxyRequestAsync(
                bufferProvider,
                "MockAccount",
                clientCollectionCacheMock.Object,
                message);

            // Assert
            Assert.IsNotNull(resultStream, "Expected a valid stream result.");
            Assert.IsTrue(resultStream.Length > 0, "Stream should contain RNTBD-serialized bytes.");
        }

        [TestMethod]
        public void GetEffectivePartitionKeyHash_ShouldReturnHash()
        {
            // Arrange
            string pkJson = "[\"TestValue\"]";

            // Act
            string epkHash = ThinClientTransportSerializer.GetEffectivePartitionKeyHash(
                pkJson,
                this.GetMockContainerProperties().PartitionKey);

            // Assert
            Assert.IsNotNull(epkHash, "EPK hash should not be null for a valid JSON partition key.");
            Assert.IsTrue(epkHash.Length > 0, "EPK hash should have a non-empty value.");
        }

        [TestMethod]
        public async Task ConvertProxyResponseAsync_ShouldThrowIfStatusMismatch()
        {
            // Arrange
            HttpResponseMessage httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(base64MockResponse))
            };

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InternalServerErrorException>(() =>
                ThinClientTransportSerializer.ConvertProxyResponseAsync(httpResponse));
        }

        [TestMethod]
        public async Task ConvertProxyResponseAsync_ShouldReturnHttpResponse_WhenValid()
        {
            // Arrange
            HttpResponseMessage httpResponse = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(base64MockResponse))
            };

            // Act
            HttpResponseMessage converted = await ThinClientTransportSerializer.ConvertProxyResponseAsync(httpResponse);

            // Assert
            Assert.AreEqual(HttpStatusCode.Created, converted.StatusCode, "Expected matched 201 status code.");
            Assert.IsNotNull(converted.Content, "Converted response should have content.");

            Assert.IsTrue(
                converted.Headers.Any(h => h.Key == ThinClientConstants.RoutedViaProxy),
                "Expected 'x-ms-thinclient-route-via-proxy' header to be set in the converted response.");
        }

        private ContainerProperties GetMockContainerProperties()
        {
            return new ContainerProperties
            {
                PartitionKey = new PartitionKeyDefinition
                {
                    Paths = new Collection<string> { "/pk" }
                }
            };
        }
    }
}
