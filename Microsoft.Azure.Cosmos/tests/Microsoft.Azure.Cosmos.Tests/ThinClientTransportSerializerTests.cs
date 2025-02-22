//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            message.Headers.Add("x-ms-thinclient-proxy-operation-type", OperationType.Read.ToString());
            message.Headers.Add("x-ms-thinclient-proxy-resource-type", ResourceType.Document.ToString());
            message.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString()); // required

            ThinClientTransportSerializer.BufferProviderWrapper bufferProvider = new();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InternalServerErrorException>(() =>
                ThinClientTransportSerializer.SerializeProxyRequestAsync(
                    bufferProvider,
                    "MockAccount",
                    message));
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
            message.Headers.Add("x-ms-thinclient-proxy-operation-type", OperationType.Read.ToString());
            message.Headers.Add("x-ms-thinclient-proxy-resource-type", ResourceType.Document.ToString());
            message.Headers.TryAddWithoutValidation(HttpConstants.HttpHeaders.PartitionKey, "[\"SamplePk\"]");
            message.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());

            ThinClientTransportSerializer.BufferProviderWrapper bufferProvider = new();

            // Act
            Stream resultStream = await ThinClientTransportSerializer.SerializeProxyRequestAsync(
                bufferProvider,
                "MockAccount",
                message);

            // Assert
            Assert.IsNotNull(resultStream, "Expected a valid stream result.");
            Assert.IsTrue(resultStream.Length > 0, "Stream should contain RNTBD-serialized bytes.");
        }

        [TestMethod]
        public void GetEffectivePartitionKeyHash_ShouldReturnHash()
        {
            // Arrange
            string pkJson = "[\"TestValue\"]"; // e.g. an array with a single PK value

            // Act
            string epkHash = ThinClientTransportSerializer.GetEffectivePartitionKeyHash(pkJson);

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

            string bodyString = await converted.Content.ReadAsStringAsync();
            Assert.IsTrue(
                converted.Headers.Any(h => h.Key == ThinClientTransportSerializer.RoutedViaProxy),
                "Expected 'x-ms-thinclient-route-via-proxy' header to be set in the converted response.");
        }
    }
}
