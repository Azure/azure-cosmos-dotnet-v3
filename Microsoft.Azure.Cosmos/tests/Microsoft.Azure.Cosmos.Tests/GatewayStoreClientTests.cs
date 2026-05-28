//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// Tests for <see cref="GatewayStoreClient"/>.
    /// </summary>
    [TestClass]
    public class GatewayStoreClientTests
    {
        /// <summary>
        /// Testing CreateDocumentClientExceptionAsync when media type is NOT application/json and the error message has a length that is not zero.
        /// This is not meant to be an exhaustive test for all legitimate content media types.
        /// <see cref="GatewayStoreClient.CreateDocumentClientExceptionAsync(HttpResponseMessage, IClientSideRequestStatistics)"/>
        /// </summary>
        [TestMethod]
        [DataRow("text/html", "<!DOCTYPE html><html><body></body></html>")]
        [DataRow("text/plain", "This is a test error message.")]
        [Owner("philipthomas-MSFT")]
        public async Task TestCreateDocumentClientExceptionWhenMediaTypeIsNotApplicationJsonAndErrorMessageLengthIsNotZeroAsync(
            string mediaType,
            string errorMessage)
        {
            HttpResponseMessage responseMessage = new(statusCode: HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: @"https://pt_ac_test_uri.com/"),
                Content = new StringContent(
                    mediaType: mediaType,
                    encoding: Encoding.UTF8,
                    content: JsonConvert.SerializeObject(
                        value: new Error() { Code = HttpStatusCode.NotFound.ToString(), Message = errorMessage })),
            };

            DocumentClientException documentClientException = await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                responseMessage: responseMessage,
                requestStatistics: GatewayStoreClientTests.CreateClientSideRequestStatistics());

            Assert.IsNotNull(value: documentClientException);
            Assert.AreEqual(expected: HttpStatusCode.NotFound, actual: documentClientException.StatusCode);
            Assert.IsTrue(condition: documentClientException.Message.Contains(errorMessage));

            Assert.IsNotNull(value: documentClientException.Error);
            Assert.AreEqual(expected: HttpStatusCode.NotFound.ToString(), actual: documentClientException.Error.Code);
            Assert.IsTrue(documentClientException.Error.Message.Contains(errorMessage));
        }

        /// <summary>
        /// Testing CreateDocumentClientExceptionAsync when media type is NOT application/json and the error message has a length that is zero.
        /// This is not meant to be an exhaustive test for all legitimate content media types.
        /// <see cref="GatewayStoreClient.CreateDocumentClientExceptionAsync(HttpResponseMessage, IClientSideRequestStatistics)"/>
        /// </summary>
        [TestMethod]
        [DataRow("text/html", "")]
        [DataRow("text/html", "     ")]
        [DataRow("text/plain", "")]
        [DataRow("text/plain", "     ")]
        [Owner("philipthomas-MSFT")]
        public async Task TestCreateDocumentClientExceptionWhenMediaTypeIsNotApplicationJsonAndErrorMessageLengthIsZeroAsync(
            string mediaType,
            string errorMessage)
        {
            HttpResponseMessage responseMessage = new(statusCode: HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: @"https://pt_ac_test_uri.com/"),
                Content = new StringContent(
                    mediaType: mediaType,
                    encoding: Encoding.UTF8,
                    content: JsonConvert.SerializeObject(
                        value: new Error() { Code = HttpStatusCode.NotFound.ToString(), Message = errorMessage })),
            };

            DocumentClientException documentClientException = await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                responseMessage: responseMessage,
                requestStatistics: GatewayStoreClientTests.CreateClientSideRequestStatistics());

            Assert.IsNotNull(value: documentClientException);
            Assert.AreEqual(expected: HttpStatusCode.NotFound, actual: documentClientException.StatusCode);
            Assert.IsNotNull(value: documentClientException.Message);

            Assert.IsNotNull(value: documentClientException.Error);
            Assert.AreEqual(expected: HttpStatusCode.NotFound.ToString(), actual: documentClientException.Error.Code);
            Assert.IsNotNull(value: documentClientException.Error.Message);
        }

        /// <summary>
        /// Testing CreateDocumentClientExceptionAsync when media type is NOT application/json and the header content length is zero.
        /// This is not meant to be an exhaustive test for all legitimate content media types.
        /// <see cref="GatewayStoreClient.CreateDocumentClientExceptionAsync(HttpResponseMessage, IClientSideRequestStatistics)"/>
        /// </summary>
        [TestMethod]
        [DataRow("text/plain", @"")]
        [DataRow("text/plain", @"    ")]
        [Owner("philipthomas-MSFT")]
        public async Task TestCreateDocumentClientExceptionWhenMediaTypeIsNotApplicationJsonAndHeaderContentLengthIsZeroAsync(
            string mediaType,
            string contentMessage)
        {
            HttpResponseMessage responseMessage = new(statusCode: HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: @"https://pt_ac_test_uri.com/"),
                Content = new StringContent(
                    mediaType: mediaType,
                    encoding: Encoding.UTF8,
                    content: contentMessage),
            };

            DocumentClientException documentClientException = await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                responseMessage: responseMessage,
                requestStatistics: GatewayStoreClientTests.CreateClientSideRequestStatistics());

            Assert.IsNotNull(value: documentClientException);
            Assert.AreEqual(expected: HttpStatusCode.NotFound, actual: documentClientException.StatusCode);
            Assert.IsNotNull(value: documentClientException.Message);

            Assert.IsNotNull(value: documentClientException.Error);
            Assert.AreEqual(expected: HttpStatusCode.NotFound.ToString(), actual: documentClientException.Error.Code);
            Assert.IsNotNull(value: documentClientException.Error.Message);
        }

        /// <summary>
        /// Testing CreateDocumentClientExceptionAsync when media type is application/json and the error message length is zero.
        /// <see cref="GatewayStoreClient.CreateDocumentClientExceptionAsync(HttpResponseMessage, IClientSideRequestStatistics)"/>
        /// </summary>
        [TestMethod]
        [DataRow("application/json", "")]
        [DataRow("application/json", "     ")]
        [Owner("philipthomas-MSFT")]
        public async Task TestCreateDocumentClientExceptionWhenMediaTypeIsApplicationJsonAndErrorMessageLengthIsZeroAsync(
            string mediaType,
            string errorMessage)
        {
            HttpResponseMessage responseMessage = new(statusCode: HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: @"https://pt_ac_test_uri.com/"),
                Content = new StringContent(
                    mediaType: mediaType,
                    encoding: Encoding.UTF8,
                    content: JsonConvert.SerializeObject(
                        value: new Error() { Code = HttpStatusCode.NotFound.ToString(), Message = errorMessage })),
            };

            DocumentClientException documentClientException = await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                responseMessage: responseMessage,
                requestStatistics: GatewayStoreClientTests.CreateClientSideRequestStatistics());

            Assert.IsNotNull(value: documentClientException);
            Assert.AreEqual(expected: HttpStatusCode.NotFound, actual: documentClientException.StatusCode);
            Assert.IsNotNull(value: documentClientException.Message);

            Assert.IsNotNull(value: documentClientException.Error);
            Assert.AreEqual(expected: HttpStatusCode.NotFound.ToString(), actual: documentClientException.Error.Code);
            Assert.IsNotNull(value: documentClientException.Error.Message);
        }

        /// <summary>
        /// Testing CreateDocumentClientExceptionAsync when media type is application/json and the content message is not valid json.
        /// and has a content length that is not zero after trim.
        /// <see cref="GatewayStoreClient.CreateDocumentClientExceptionAsync(HttpResponseMessage, IClientSideRequestStatistics)"/>
        /// </summary>
        [TestMethod]
        [DataRow("application/json", @"<!DOCTYPE html><html><body></body></html>")]
        [DataRow("application/json", @"   <!DOCTYPE html><html><body></body></html>")]
        [DataRow("application/json", @"<!DOCTYPE html><html><body></body></html>   ")]
        [DataRow("application/json", @"   <!DOCTYPE html><html><body></body></html>   ")]
        [DataRow("application/json", @"ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890")]
        [DataRow("application/json", @"   ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890")]
        [DataRow("application/json", @"ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890   ")]
        [DataRow("application/json", @"   ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890   ")]
        [Owner("philipthomas-MSFT")]
        public async Task TestCreateDocumentClientExceptionWhenMediaTypeIsApplicationJsonAndContentMessageIsNotValidJsonAsync(
            string mediaType,
            string contentMessage)
        {
            HttpResponseMessage responseMessage = new(statusCode: HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: @"https://pt_ac_test_uri.com/"),
                Content = new StringContent(
                    mediaType: mediaType,
                    encoding: Encoding.UTF8,
                    content: contentMessage),
            };

            DocumentClientException documentClientException = await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                responseMessage: responseMessage,
                requestStatistics: GatewayStoreClientTests.CreateClientSideRequestStatistics());

            Assert.IsNotNull(value: documentClientException);
            Assert.AreEqual(expected: HttpStatusCode.NotFound, actual: documentClientException.StatusCode);
            Assert.IsTrue(condition: documentClientException.Message.Contains(contentMessage));
        }

        /// <summary>
        /// Testing CreateDocumentClientExceptionAsync when media type is application/json and the header content length is zero.
        /// </summary>
        [TestMethod]
        [DataRow("application/json", @"")]
        [DataRow("application/json", @"    ")]
        [Owner("philipthomas-MSFT")]
        public async Task TestCreateDocumentClientExceptionWhenMediaTypeIsApplicationJsonAndHeaderContentLengthIsZeroAsync(
            string mediaType,
            string contentMessage)
        {
            HttpResponseMessage responseMessage = new(statusCode: HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: @"https://pt_ac_test_uri.com/"),
                Content = new StringContent(
                    mediaType: mediaType,
                    encoding: Encoding.UTF8,
                    content: contentMessage),
            };

            DocumentClientException documentClientException = await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                responseMessage: responseMessage,
                requestStatistics: GatewayStoreClientTests.CreateClientSideRequestStatistics());

            Assert.IsNotNull(value: documentClientException);
            Assert.AreEqual(expected: HttpStatusCode.NotFound, actual: documentClientException.StatusCode);
            Assert.IsNotNull(value: documentClientException.Message);

            Assert.IsNotNull(value: documentClientException.Error);
            Assert.AreEqual(expected: HttpStatusCode.NotFound.ToString(), actual: documentClientException.Error.Code);
            Assert.IsNotNull(value: documentClientException.Error.Message);
        }

        /// <summary>
        /// Test to verify the fix for the stream consumption issue when JSON deserialization fails.
        /// This reproduces the scenario where a 403 response has application/json content type 
        /// but invalid JSON content, which would previously cause "stream already consumed" exception.
        /// Fixes issue #5243.
        /// </summary>
        [TestMethod]
        [Owner("copilot")]
        public async Task TestStreamConsumptionBugFixWhenJsonDeserializationFails()
        {
            // Create invalid JSON content that will fail deserialization but has application/json content type
            string invalidJson = "{ \"error\": invalid json content that will fail parsing }";
            
            HttpResponseMessage responseMessage = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://test.com/dbs/db1/colls/coll1/docs/doc1"),
                Content = new StringContent(invalidJson, Encoding.UTF8, "application/json")
            };

            IClientSideRequestStatistics requestStatistics = GatewayStoreClientTests.CreateClientSideRequestStatistics();

            // This should NOT throw an InvalidOperationException about stream being consumed
            DocumentClientException exception = await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                responseMessage: responseMessage,
                requestStatistics: requestStatistics);

            // Verify the exception was created successfully with fallback logic
            Assert.IsNotNull(exception);
            Assert.AreEqual(HttpStatusCode.Forbidden, exception.StatusCode);
            Assert.IsTrue(exception.Message.Contains(invalidJson), "Exception message should contain the original invalid JSON content");
        }

        /// <summary>
        /// Verifies that ParseResponseAsync preserves the response body for DTX requests
        /// returning error status codes (e.g., 452), instead of throwing DocumentClientException
        /// which would discard the per-operation results JSON.
        /// </summary>
        [TestMethod]
        public async Task TestParseResponseAsync_DtxRequest_PreservesBodyOn452()
        {
            string dtxResponseBody = "{\"operationResponses\":[{\"statusCode\":412,\"subStatusCode\":0}],\"isRetriable\":false}";

            HttpResponseMessage httpResponse = new HttpResponseMessage((HttpStatusCode)452)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://test.documents.azure.com/operations/dtc"),
                Content = new StringContent(dtxResponseBody, Encoding.UTF8, "application/json")
            };

            using DocumentServiceRequest dsRequest = DocumentServiceRequest.Create(
                OperationType.CommitDistributedTransaction,
                ResourceType.DistributedTransactionBatch,
                "operations/dtc",
                new System.IO.MemoryStream(Encoding.UTF8.GetBytes("{}")),
                AuthorizationTokenType.PrimaryMasterKey,
                null);

            DocumentServiceResponse response = await GatewayStoreClient.ParseResponseAsync(httpResponse, request: dsRequest);

            Assert.IsNotNull(response);
            Assert.AreEqual((HttpStatusCode)452, response.StatusCode);
            Assert.IsNotNull(response.ResponseBody, "Response body must be preserved for DTX per-operation parsing.");

            using System.IO.StreamReader reader = new System.IO.StreamReader(response.ResponseBody);
            string body = await reader.ReadToEndAsync();
            Assert.IsTrue(body.Contains("412"), "Per-operation status code should be present in the preserved body.");
        }

        /// <summary>
        /// Verifies that ParseResponseAsync preserves the body for DTX requests returning
        /// other error codes (e.g., 409 Conflict) that are not in the standard exceptionless set.
        /// </summary>
        [TestMethod]
        [DataRow(409)]
        [DataRow(500)]
        [DataRow(449)]
        public async Task TestParseResponseAsync_DtxRequest_PreservesBodyOnVariousErrorCodes(int statusCode)
        {
            string dtxResponseBody = "{\"operationResponses\":[{\"statusCode\":412,\"subStatusCode\":0}],\"isRetriable\":true}";

            HttpResponseMessage httpResponse = new HttpResponseMessage((HttpStatusCode)statusCode)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://test.documents.azure.com/operations/dtc"),
                Content = new StringContent(dtxResponseBody, Encoding.UTF8, "application/json")
            };

            using DocumentServiceRequest dsRequest = DocumentServiceRequest.Create(
                OperationType.CommitDistributedTransaction,
                ResourceType.DistributedTransactionBatch,
                "operations/dtc",
                new System.IO.MemoryStream(Encoding.UTF8.GetBytes("{}")),
                AuthorizationTokenType.PrimaryMasterKey,
                null);

            DocumentServiceResponse response = await GatewayStoreClient.ParseResponseAsync(httpResponse, request: dsRequest);

            Assert.IsNotNull(response);
            Assert.AreEqual((HttpStatusCode)statusCode, response.StatusCode);
            Assert.IsNotNull(response.ResponseBody, $"Response body must be preserved for DTX requests returning {statusCode}.");
        }

        /// <summary>
        /// Verifies that ParseResponseAsync falls back to throwing DocumentClientException
        /// for DTX requests when there is no content body (preserving detailed error context).
        /// </summary>
        [TestMethod]
        public async Task TestParseResponseAsync_DtxRequest_NullContentThrowsForErrorContext()
        {
            HttpResponseMessage httpResponse = new HttpResponseMessage((HttpStatusCode)452)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://test.documents.azure.com/operations/dtc"),
                Content = null
            };

            using DocumentServiceRequest dsRequest = DocumentServiceRequest.Create(
                OperationType.CommitDistributedTransaction,
                ResourceType.DistributedTransactionBatch,
                "operations/dtc",
                new System.IO.MemoryStream(Encoding.UTF8.GetBytes("{}")),
                AuthorizationTokenType.PrimaryMasterKey,
                null);

            await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => GatewayStoreClient.ParseResponseAsync(httpResponse, request: dsRequest));
        }

        /// <summary>
        /// Verifies that Extensions.ToCosmosResponseMessage preserves the response body
        /// for DTX error responses, so DistributedTransactionResponse can parse per-op results,
        /// while also populating CosmosException for consumers that check it.
        /// </summary>
        [TestMethod]
        public void TestToCosmosResponseMessage_DtxResponse_PreservesBodyOnError()
        {
            string dtxResponseBody = "{\"operationResponses\":[{\"statusCode\":412}]}";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(dtxResponseBody);
            System.IO.MemoryStream bodyStream = new System.IO.MemoryStream(bodyBytes);

            DocumentServiceResponse dsResponse = new DocumentServiceResponse(
                body: bodyStream,
                headers: new Documents.Collections.RequestNameValueCollection(),
                statusCode: (HttpStatusCode)452);

            RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Post,
                new Uri("operations/dtc", UriKind.Relative))
            {
                ResourceType = ResourceType.DistributedTransactionBatch,
                OperationType = OperationType.CommitDistributedTransaction
            };

            ResponseMessage responseMessage = dsResponse.ToCosmosResponseMessage(requestMessage, requestChargeTracker: null);

            Assert.IsNotNull(responseMessage);
            Assert.AreEqual((HttpStatusCode)452, responseMessage.StatusCode);
            Assert.IsNotNull(responseMessage.Content, "DTX response body must be preserved through ToCosmosResponseMessage.");
            Assert.IsNotNull(responseMessage.CosmosException, "CosmosException should be populated for error context.");
            Assert.AreEqual((HttpStatusCode)452, responseMessage.CosmosException.StatusCode);

            responseMessage.Content.Position = 0;
            using System.IO.StreamReader reader = new System.IO.StreamReader(responseMessage.Content);
            string body = reader.ReadToEnd();
            Assert.IsTrue(body.Contains("412"), "Per-operation status code should be present in the preserved body.");
        }

        /// <summary>
        /// Verifies that Extensions.ToCosmosResponseMessage still creates CosmosException
        /// (without body) for non-DTX error responses — the existing behavior is unchanged.
        /// </summary>
        [TestMethod]
        public void TestToCosmosResponseMessage_NonDtxResponse_DoesNotPreserveBodyOnError()
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes("{\"error\":\"test\"}");
            System.IO.MemoryStream bodyStream = new System.IO.MemoryStream(bodyBytes);

            DocumentServiceResponse dsResponse = new DocumentServiceResponse(
                body: bodyStream,
                headers: new Documents.Collections.RequestNameValueCollection(),
                statusCode: HttpStatusCode.InternalServerError);

            RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Post,
                new Uri("dbs/db1/colls/coll1/docs", UriKind.Relative))
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Create
            };

            ResponseMessage responseMessage = dsResponse.ToCosmosResponseMessage(requestMessage, requestChargeTracker: null);

            Assert.IsNotNull(responseMessage);
            Assert.AreEqual(HttpStatusCode.InternalServerError, responseMessage.StatusCode);
            // Non-DTX error responses go through CosmosException path — Content is not the original body.
            Assert.IsNull(responseMessage.Content, "Non-DTX error responses should not preserve raw body (goes through CosmosException path).");
        }

        /// <summary>
        /// Verifies that ParseResponseAsync still throws DocumentClientException for non-DTX
        /// requests that return error status codes not covered by exceptionless retry flags.
        /// </summary>
        [TestMethod]
        public async Task TestParseResponseAsync_NonDtxRequest_StillThrowsOnError()
        {
            HttpResponseMessage httpResponse = new HttpResponseMessage((HttpStatusCode)452)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://test.documents.azure.com/dbs/db1/colls/coll1/docs"),
                Content = new StringContent("{\"error\":\"test\"}", Encoding.UTF8, "application/json")
            };

            using DocumentServiceRequest dsRequest = DocumentServiceRequest.Create(
                OperationType.Create,
                ResourceType.Document,
                new Uri("https://test.documents.azure.com/dbs/db1/colls/coll1/docs", UriKind.Absolute),
                new System.IO.MemoryStream(Encoding.UTF8.GetBytes("{}")),
                AuthorizationTokenType.PrimaryMasterKey,
                null);

            await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => GatewayStoreClient.ParseResponseAsync(httpResponse, request: dsRequest));
        }

        private static IClientSideRequestStatistics CreateClientSideRequestStatistics()
        {
            return new ClientSideRequestStatisticsTraceDatum(
                startTime: DateTime.UtcNow,
                trace: NoOpTrace.Singleton);
        }
    }
}