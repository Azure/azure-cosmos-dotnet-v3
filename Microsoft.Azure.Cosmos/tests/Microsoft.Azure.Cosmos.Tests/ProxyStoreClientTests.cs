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
    public class ProxyStoreClientTests
    {
        [TestMethod]
        public async Task InvokeAsync_Json404_ShouldThrowDocumentClientException()
        {
            // Arrange
            HttpResponseMessage notFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"message\":\"Sample 404 JSON error.\"}", Encoding.UTF8, "application/json")
            };

            CosmosHttpClient mockHttpClient = new MockCosmosHttpClient(notFoundResponse, notFoundResponse);
            ProxyStoreClient proxyClient = new ProxyStoreClient(
                httpClient: mockHttpClient,
                eventSource: null,
                proxyEndpoint: new Uri("https://mock.proxy.com"),
                globalDatabaseAccountName: "MockAccount",
                serializerSettings: null);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: OperationType.Read,
                resourceType: ResourceType.Document,
                resourceId: "docId",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            // Act + Assert => Should throw DocumentClientException
            await Assert.ThrowsExceptionAsync<DocumentClientException>(async () =>
                await proxyClient.InvokeAsync(
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

            CosmosHttpClient mockHttpClient = new MockCosmosHttpClient(serverErrorResponse, serverErrorResponse);
            ProxyStoreClient proxyClient = new ProxyStoreClient(
                httpClient: mockHttpClient,
                eventSource: null,
                proxyEndpoint: new Uri("https://mock.proxy.com"),
                globalDatabaseAccountName: "MockAccount",
                serializerSettings: null);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: OperationType.Read,
                resourceType: ResourceType.Document,
                resourceId: "docId",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            // Act + Assert => Should throw DocumentClientException
            await Assert.ThrowsExceptionAsync<DocumentClientException>(async () =>
                await proxyClient.InvokeAsync(
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

            CosmosHttpClient mockHttpClient = new MockCosmosHttpClient(forbiddenHtml, forbiddenHtml);
            ProxyStoreClient proxyClient = new ProxyStoreClient(
                httpClient: mockHttpClient,
                eventSource: null,
                proxyEndpoint: new Uri("https://mock.proxy.com"),
                globalDatabaseAccountName: "MockAccount",
                serializerSettings: null);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: OperationType.Read,
                resourceType: ResourceType.Document,
                resourceId: "docId",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            // Act + Assert => Should throw DocumentClientException
            await Assert.ThrowsExceptionAsync<DocumentClientException>(async () =>
                await proxyClient.InvokeAsync(
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

            // Build a 200 HttpResponseMessage with the binary content
            HttpResponseMessage successResponse = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new ByteArrayContent(rntbdBytes)
            };

            CosmosHttpClient mockHttpClient = new MockCosmosHttpClient(successResponse, successResponse);
            ProxyStoreClient proxyClient = new ProxyStoreClient(
                httpClient: mockHttpClient,
                eventSource: null,
                proxyEndpoint: new Uri("https://mock.proxy.com"),
                globalDatabaseAccountName: "MockAccount",
                serializerSettings: null);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: OperationType.Read,
                resourceType: ResourceType.Document,
                resourceId: "docId",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            // Act
            DocumentServiceResponse dsr = await proxyClient.InvokeAsync(
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

    internal class MockCosmosHttpClient : CosmosHttpClient
    {
        private readonly HttpResponseMessage getAsyncResponse;
        private readonly HttpResponseMessage sendHttpAsyncResponse;
        private bool disposed;

        public override HttpMessageHandler HttpMessageHandler { get; } = new HttpClientHandler();

        public MockCosmosHttpClient(HttpResponseMessage getAsyncResponse, HttpResponseMessage sendHttpAsyncResponse)
        {
            this.getAsyncResponse = getAsyncResponse;
            this.sendHttpAsyncResponse = sendHttpAsyncResponse;
        }

        public override Task<HttpResponseMessage> GetAsync(
            Uri uri,
            INameValueCollection additionalHeaders,
            ResourceType resourceType,
            HttpTimeoutPolicy timeoutPolicy,
            IClientSideRequestStatistics clientSideRequestStatistics,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(this.getAsyncResponse);
        }

        public override Task<HttpResponseMessage> SendHttpAsync(
            Func<ValueTask<HttpRequestMessage>> createRequestMessageAsync,
            ResourceType resourceType,
            HttpTimeoutPolicy timeoutPolicy,
            IClientSideRequestStatistics clientSideRequestStatistics,
            CancellationToken cancellationToken,
            DocumentServiceRequest documentServiceRequest = null)
        {
            return Task.FromResult(this.sendHttpAsyncResponse);
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.getAsyncResponse?.Dispose();
                    this.sendHttpAsyncResponse?.Dispose();
                }
                this.disposed = true;
            }
        }

        public override void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
