//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ThinClientStoreClientTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            System.Diagnostics.Trace.CorrelationManager.ActivityId = Guid.NewGuid();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            System.Diagnostics.Trace.CorrelationManager.ActivityId = Guid.Empty;
        }

        [TestMethod]
        public async Task InvokeAsync_Json404_ShouldThrowDocumentClientException()
        {
            // Arrange
            HttpResponseMessage notFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"message\":\"Sample 404 JSON error.\"}", Encoding.UTF8, "application/json")
            };

            CosmosHttpClient cosmosHttpClient = MockCosmosUtil.CreateMockCosmosHttpClientFromFunc(
                _ => Task.FromResult(notFoundResponse));

            ThinClientStoreClient thinClientStoreClient = new ThinClientStoreClient(
                httpClient: cosmosHttpClient,
                eventSource: null,
                serializerSettings: null);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: OperationType.Read,
                resourceType: ResourceType.Document,
                resourceId: "docId",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            request.Headers[HttpConstants.HttpHeaders.PartitionKey] = "[\"myPartitionKey\"]";

            // Act + Assert => Should throw DocumentClientException
            await Assert.ThrowsExceptionAsync<DocumentClientException>(async () =>
                await thinClientStoreClient.InvokeAsync(
                    request,
                    ResourceType.Document,
                    new Uri("https://mock.cosmos.com/dbs/mockdb/colls/mockcoll/docs/mockdoc"),
                    default));
        }

        [TestMethod]
        public async Task InvokeAsync_Json500_ShouldThrowDocumentClientException()
        {
            // Arrange
            HttpResponseMessage serverErrorResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"message\":\"Sample 500 JSON error.\"}", Encoding.UTF8, "application/json")
            };

            CosmosHttpClient cosmosHttpClient = MockCosmosUtil.CreateMockCosmosHttpClientFromFunc(
                _ => Task.FromResult(serverErrorResponse));

            ThinClientStoreClient thinClientStoreClient = new ThinClientStoreClient(
                httpClient: cosmosHttpClient,
                eventSource: null,
                serializerSettings: null);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: OperationType.Read,
                resourceType: ResourceType.Document,
                resourceId: "docId",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            // Act + Assert => Should throw DocumentClientException

            request.Headers[HttpConstants.HttpHeaders.PartitionKey] = "[\"myPartitionKey\"]";

            await Assert.ThrowsExceptionAsync<DocumentClientException>(async () =>
                await thinClientStoreClient.InvokeAsync(
                    request,
                    ResourceType.Document,
                    new Uri("https://mock.cosmos.com/dbs/mockdb/colls/mockcoll/docs/mockdoc"),
                    default));
        }

        [TestMethod]
        public async Task InvokeAsync_403HtmlError_ShouldThrowDocumentClientException()
        {
            // Arrange
            HttpResponseMessage forbiddenHtml = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("<html><body>403 Forbidden.</body></html>", Encoding.UTF8, "text/html")
            };

            CosmosHttpClient cosmosHttpClient = MockCosmosUtil.CreateMockCosmosHttpClientFromFunc(
                _ => Task.FromResult(forbiddenHtml));

            ThinClientStoreClient thinClientStoreClient = new ThinClientStoreClient(
                httpClient: cosmosHttpClient,
                eventSource: null,
                serializerSettings: null);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: OperationType.Read,
                resourceType: ResourceType.Document,
                resourceId: "docId",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            // Add partition key
            request.Headers[HttpConstants.HttpHeaders.PartitionKey] = "[\"myPartitionKey\"]";

            // Act + Assert => Should throw DocumentClientException
            await Assert.ThrowsExceptionAsync<DocumentClientException>(async () =>
                await thinClientStoreClient.InvokeAsync(
                    request,
                    ResourceType.Document,
                    new Uri("https://mock.cosmos.com/dbs/mockdb/colls/mockcoll/docs/mockdoc"),
                    default));
        }

        [TestMethod]
        public async Task InvokeAsync_Rntbd200_ShouldReturnDocumentServiceResponse()
        {
            // A single base64-encoded RNTBD response representing an HTTP 201 (Created) in RNTBD format.
            string base64RntbdSuccess = "9AEAAMkAAAAIvhHfD23jSaynaR+gyTZ3AAAAAQIAByFUaHUsIDEzIEZlYiAyMDI1IDE0OjI1OjI4LjAyNCBHTVQEAAgmACIwMDAwYWQzZS0wMDAwLTAyMDAtMDAwMC02N2FlNjRjMDAwMDAiDgAIVABkb2N1bWVudFNpemU9NTEyMDA7ZG9jdW1lbnRzU2l6ZT01MjQyODgwMDtkb2N1bWVudHNDb3VudD0tMTtjb2xsZWN0aW9uU2l6ZT01MjQyODgwMDsPAAhBAGRvY3VtZW50U2l6ZT0wO2RvY3VtZW50c1NpemU9MTtkb2N1bWVudHNDb3VudD04O2NvbGxlY3Rpb25TaXplPTM7EAAHBDEuMTkTAAUKAAAAAAAAABUADgzDMAzDMBxAFwAIOgBkYnMvdGhpbi1jbGllbnQtdGVzdC1kYi9jb2xscy90aGluLWNsaWVudC10ZXN0LWNvbnRhaW5lci0xGAAIDABOSDF1QUo2QU5tMD0aAAUJAAAAAAAAAB4AAgMAAAAfAAIEAAAAIQAIAQAwJgACAQAAACkABQkAAAAAAAAAMAACAAAAADUAAgEAAAA6AAUKAAAAAAAAADsABQkAAAAAAAAAPgAIBQAtMSMxMFEADkjhehSuRxBAYwAIAQAweAAF//////////89AQAAeyJpZCI6IjNiMTFiNDM2LTViMTUtNGQwZS1iZWYwLWY1MzVmNjA0MTQxYyIsInBrIjoicGsiLCJuYW1lIjoiODM2MzI0NTA2IiwiZW1haWwiOiJhYmNAZGVmLmNvbSIsImJvZHkiOiJibGFibGEiLCJfcmlkIjoiTkgxdUFKNkFObTBKQUFBQUFBQUFBQT09IiwiX3NlbGYiOiJkYnMvTkgxdUFBPT0vY29sbHMvTkgxdUFKNkFObTA9L2RvY3MvTkgxdUFKNkFObTBKQUFBQUFBQUFBQT09LyIsIl9ldGFnIjoiXCIwMDAwYWQzZS0wMDAwLTAyMDAtMDAwMC02N2FlNjRjMDAwMDBcIiIsIl9hdHRhY2htZW50cyI6ImF0dGFjaG1lbnRzLyIsIl90cyI6MTczOTQ4MjMwNH0=";

            byte[] rntbdBytes = Convert.FromBase64String(base64RntbdSuccess);
            HttpResponseMessage successResponse = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new ByteArrayContent(rntbdBytes)
            };

            CosmosHttpClient cosmosHttpClient = MockCosmosUtil.CreateMockCosmosHttpClientFromFunc(
                _ => Task.FromResult(successResponse));

            ThinClientStoreClient thinClientStoreClient = new ThinClientStoreClient(
                httpClient: cosmosHttpClient,
                eventSource: null,
                serializerSettings: null);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: OperationType.Read,
                resourceType: ResourceType.Document,
                resourceId: "docId",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            // Add partition key
            request.Headers[HttpConstants.HttpHeaders.PartitionKey] = "[\"myPartitionKey\"]";

            // Act
            DocumentServiceResponse dsr = await thinClientStoreClient.InvokeAsync(
                request,
                ResourceType.Document,
                new Uri("https://mock.cosmos.com/dbs/mockdb/colls/mockcoll/docs/mockdoc"),
                default);

            // Assert
            Assert.IsNotNull(dsr);
            Assert.AreEqual(HttpStatusCode.Created, dsr.StatusCode);

            using StreamReader sr = new StreamReader(dsr.ResponseBody);
            string responseBody = sr.ReadToEnd();
        }
    }
}
