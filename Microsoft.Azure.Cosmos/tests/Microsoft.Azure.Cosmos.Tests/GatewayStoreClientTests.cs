//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
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

        private static IClientSideRequestStatistics CreateClientSideRequestStatistics()
        {
            return new ClientSideRequestStatisticsTraceDatum(
                startTime: DateTime.UtcNow,
                trace: NoOpTrace.Singleton);
        }
    }
}