//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
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

            clientCollectionCacheMock
              .Setup(c => c.ResolveCollectionAsync(
                  It.IsAny<DocumentServiceRequest>(),
                  It.IsAny<CancellationToken>(),
                  It.IsAny<ITrace>()))
              .ReturnsAsync(this.GetMockContainerProperties());

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
            ContainerProperties containerProperties = new ContainerProperties
            {
                PartitionKey = new PartitionKeyDefinition
                {
                    Paths = new Collection<string> { "/pk" }
                }
            };

            typeof(ContainerProperties)
                .GetProperty("ResourceId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(containerProperties, "-Jlvm9pqHGk=");

            return containerProperties;
        }

        /// <summary>
        /// Verifies that when the <c>x-ms-cosmos-read-consistency-strategy</c> header is set on the
        /// outbound <see cref="HttpRequestMessage"/>, the RNTBD payload built by
        /// <see cref="ThinClientTransportSerializer.SerializeProxyRequestAsync"/> contains the
        /// corresponding RNTBD token. Token layout for byte-typed RNTBD identifiers is
        /// <c>[identifier (uint16 LE)][type (1 byte)][value (1 byte)]</c>. ReadConsistencyStrategy
        /// has identifier <c>254</c> (<c>0x00FE</c>) and type <c>Byte</c> (<c>0x00</c>).
        /// </summary>
        [TestMethod]
        [DataRow("Eventual", (byte)1, DisplayName = "Eventual -> 1")]
        [DataRow("Session", (byte)2, DisplayName = "Session -> 2")]
        [DataRow("LatestCommitted", (byte)3, DisplayName = "LatestCommitted -> 3")]
        [DataRow("GlobalStrong", (byte)4, DisplayName = "GlobalStrong -> 4")]
        public async Task SerializeProxyRequestAsync_ShouldEncodeReadConsistencyStrategy(string strategyName, byte expectedRntbdValue)
        {
            byte[] payload = await this.SerializeWithExtraHeadersAsync(
                new Dictionary<string, string>
                {
                    { HttpConstants.HttpHeaders.ReadConsistencyStrategy, strategyName }
                });

            byte[] expectedToken = { 0xFE, 0x00, 0x00, expectedRntbdValue };

            Assert.IsTrue(
                ContainsSequence(payload, expectedToken),
                $"Expected to find ReadConsistencyStrategy RNTBD token bytes [FE 00 00 {expectedRntbdValue:X2}] for strategy '{strategyName}' in the serialized proxy request payload.");
        }

        private async Task<byte[]> SerializeWithExtraHeadersAsync(IDictionary<string, string> extraHeaders)
        {
            HttpRequestMessage message = new HttpRequestMessage
            {
                Content = new StringContent("{\"id\":\"item\"}"),
                RequestUri = new Uri("https://localhost/dbs/TestDb/colls/TestColl/docs/TestDoc")
            };
            message.Headers.Add(ThinClientConstants.ProxyOperationType, OperationType.Read.ToString());
            message.Headers.Add(ThinClientConstants.ProxyResourceType, ResourceType.Document.ToString());
            message.Headers.TryAddWithoutValidation(HttpConstants.HttpHeaders.PartitionKey, "[\"SamplePk\"]");
            message.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());

            foreach (KeyValuePair<string, string> header in extraHeaders)
            {
                message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            Mock<ClientCollectionCache> clientCollectionCacheMock = this.CreateClientCollectionCacheMock();

            ThinClientTransportSerializer.BufferProviderWrapper bufferProvider = new();

            using Stream resultStream = await ThinClientTransportSerializer.SerializeProxyRequestAsync(
                bufferProvider,
                "MockAccount",
                clientCollectionCacheMock.Object,
                message);

            Assert.IsTrue(resultStream.CanSeek, "Expected a seekable stream.");
            resultStream.Position = 0;

            using MemoryStream copy = new MemoryStream();
            await resultStream.CopyToAsync(copy);
            return copy.ToArray();
        }

        private Mock<ClientCollectionCache> CreateClientCollectionCacheMock()
        {
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

            return clientCollectionCacheMock;
        }

        private static bool ContainsSequence(byte[] haystack, byte[] needle)
        {
            if (haystack == null || needle == null || needle.Length == 0 || haystack.Length < needle.Length)
            {
                return false;
            }

            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
