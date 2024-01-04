//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Reflection.Metadata;
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
        /// Testing CreateDocumentClientExceptionAsync when media type is not application/json. Not meant to be an exhaustive test for all
        /// legitimate content media types.
        /// </summary>
        [TestMethod]
        [DataRow("text/html", "<!DOCTYPE html><html><body></body></html>")]
        [DataRow("text/plain", "This is a test error message.")]
        public async Task TestCreateDocumentClientExceptionWhenMediaTypeIsNotApplicationJsonAsync(
            string mediaType,
            string contentMessage)
        {
            HttpResponseMessage responseMessage = new(statusCode: System.Net.HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: @"https://pt_ac_test_uri.com/"),
                Content = new StringContent(
                    mediaType: mediaType,
                    encoding: Encoding.UTF8,
                    content: JsonConvert.SerializeObject(
                        value: new Error() { Code = HttpStatusCode.NotFound.ToString(), Message = contentMessage })),
            };

            DocumentClientException documentClientException = await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                responseMessage: responseMessage,
                requestStatistics: GatewayStoreClientTests.CreateClientSideRequestStatistics());

            Assert.IsNotNull(value: documentClientException);
            Assert.AreEqual(expected: HttpStatusCode.NotFound, actual: documentClientException.StatusCode);
            Assert.IsTrue(condition: documentClientException.Message.Contains(contentMessage));

            Assert.IsNotNull(value: documentClientException.Error);
            Assert.AreEqual(expected: HttpStatusCode.NotFound.ToString(), actual: documentClientException.Error.Code);
            Assert.IsTrue(documentClientException.Error.Message.Contains(contentMessage));
        }

        /// <summary>
        /// Testing CreateDocumentClientExceptionAsync when media type is not application/json. Not meant to be an exhaustive test for all
        /// legitimate content media types.
        /// </summary>
        [TestMethod]
        [DataRow("text/html", "")]
        [DataRow("text/html", "     ")]
        [DataRow("text/plain", "")]
        [DataRow("text/plain", "     ")]
        public async Task TestCreateDocumentClientExceptionWhenMediaTypeIsNotApplicationJsonAndHeaderContentLengthIsZeroAsync(
            string mediaType,
            string contentMessage)
        {
            HttpResponseMessage responseMessage = new(statusCode: System.Net.HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: @"https://pt_ac_test_uri.com/"),
                Content = new StringContent(
                    mediaType: mediaType,
                    encoding: Encoding.UTF8,
                    content: JsonConvert.SerializeObject(
                        value: new Error() { Code = HttpStatusCode.NotFound.ToString(), Message = contentMessage })),
            };

            DocumentClientException documentClientException = await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                responseMessage: responseMessage,
                requestStatistics: GatewayStoreClientTests.CreateClientSideRequestStatistics());

            Assert.IsNotNull(value: documentClientException);
            Assert.AreEqual(expected: HttpStatusCode.NotFound, actual: documentClientException.StatusCode);
            Assert.IsTrue(condition: documentClientException.Message.Contains("No response content from gateway."));

            Assert.IsNotNull(value: documentClientException.Error);
            Assert.AreEqual(expected: HttpStatusCode.NotFound.ToString(), actual: documentClientException.Error.Code);
            Assert.IsTrue(documentClientException.Error.Message.Contains("No response content from gateway."));
        }

        /// <summary>
        /// Testing CreateDocumentClientExceptionAsync when media type is application/json and the error message length is zero.
        /// </summary>
        [TestMethod]
        public async Task TestCreateDocumentClientExceptionWhenMediaTypeIsApplicationJsonAndErrorMessageLengthIsZeroAsync()
        {
            HttpResponseMessage responseMessage = new(statusCode: System.Net.HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: @"https://pt_ac_test_uri.com/"),
                Content = new StringContent(
                    mediaType: "application/json",
                    encoding: Encoding.UTF8,
                    content: JsonConvert.SerializeObject(
                        value: new Error() { Code = HttpStatusCode.NotFound.ToString(), Message = "" })),
            };

            IClientSideRequestStatistics requestStatistics = new ClientSideRequestStatisticsTraceDatum(
                startTime: DateTime.UtcNow,
                trace: NoOpTrace.Singleton);

            DocumentClientException documentClientException = await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                responseMessage: responseMessage,
                requestStatistics: requestStatistics);

            Assert.IsNotNull(value: documentClientException);
            Assert.AreEqual(expected: HttpStatusCode.NotFound, actual: documentClientException.StatusCode);
            Assert.IsTrue(condition: documentClientException.Message.Contains("No response content from gateway."));

            Assert.IsNotNull(value: documentClientException.Error);
            Assert.AreEqual(expected: HttpStatusCode.NotFound.ToString(), actual: documentClientException.Error.Code);
            Assert.AreEqual(expected: "No response content from gateway.", actual: documentClientException.Error.Message);
        }

        /// <summary>
        /// Testing CreateDocumentClientExceptionAsync when media type is application/json and the content is not valid json
        /// and has a content length that is not zero after trim.
        /// </summary>
        [TestMethod]
        [DataRow(@"<!DOCTYPE html><html><body></body></html>")]
        [DataRow(@"   <!DOCTYPE html><html><body></body></html>")]
        [DataRow(@"<!DOCTYPE html><html><body></body></html>   ")]
        [DataRow(@"   <!DOCTYPE html><html><body></body></html>   ")]
        [DataRow(@"ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890")]
        [DataRow(@"   ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890")]
        [DataRow(@"ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890   ")]
        [DataRow(@"   ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890   ")]
        public async Task TestCreateDocumentClientExceptionWhenMediaTypeIsApplicationJsonAndContentIsNotValidJsonAndContentLengthIsNotZeroAsync(string contentMessage)
        {
            HttpResponseMessage responseMessage = new(statusCode: System.Net.HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: @"https://pt_ac_test_uri.com/"),
                Content = new StringContent(
                    mediaType: "application/json",
                    encoding: Encoding.UTF8,
                    content: JsonConvert.SerializeObject(
                        value: new Error() { Code = HttpStatusCode.NotFound.ToString(), Message = contentMessage })),
            };

            IClientSideRequestStatistics requestStatistics = new ClientSideRequestStatisticsTraceDatum(
                startTime: DateTime.UtcNow,
                trace: NoOpTrace.Singleton);

            DocumentClientException documentClientException = await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                    responseMessage: responseMessage,
                    requestStatistics: requestStatistics);

            Assert.IsNotNull(value: documentClientException);
            Assert.AreEqual(expected: HttpStatusCode.NotFound, actual: documentClientException.StatusCode);
            Assert.IsTrue(condition: documentClientException.Message.Contains(contentMessage));

            Assert.IsNotNull(value: documentClientException.Error);
            Assert.AreEqual(expected: HttpStatusCode.NotFound.ToString(), actual: documentClientException.Error.Code);
            Assert.AreEqual(expected: contentMessage, actual: documentClientException.Error.Message);
        }

        /// <summary>
        /// Testing CreateDocumentClientExceptionAsync when media type is application/json and the content is not valid json
        /// and has a content length that is zero after trim.
        /// </summary>
        [TestMethod]
        [DataRow(@"")]
        [DataRow(@"    ")]
        public async Task TestCreateDocumentClientExceptionWhenMediaTypeIsApplicationJsonAndContentIsNotValidJsonAndHeaderContentLengthIsZeroAsync(string contentMessage)
        {
            HttpResponseMessage responseMessage = new(statusCode: System.Net.HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: @"https://pt_ac_test_uri.com/"),
                Content = new StringContent(
                    mediaType: "application/json",
                    encoding: Encoding.UTF8, 
                    content: contentMessage),
            };

            IClientSideRequestStatistics requestStatistics = new ClientSideRequestStatisticsTraceDatum(
                startTime: DateTime.UtcNow,
                trace: NoOpTrace.Singleton);

            DocumentClientException documentClientException = await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                    responseMessage: responseMessage,
                    requestStatistics: requestStatistics);

            Assert.IsNotNull(value: documentClientException);
            Assert.AreEqual(expected: HttpStatusCode.NotFound, actual: documentClientException.StatusCode);
            Assert.IsTrue(condition: documentClientException.Message.Contains("No response content from gateway."));

            Assert.IsNotNull(value: documentClientException.Error);
            Assert.AreEqual(expected: HttpStatusCode.NotFound.ToString(), actual: documentClientException.Error.Code);
            Assert.IsTrue(documentClientException.Error.Message.Contains("No response content from gateway."));
        }

        /// <summary>
        /// Testing CreateDocumentClientExceptionAsync when response message argument is null, then expects an argumentNullException.
        /// </summary>
        [TestMethod]
        public async Task TestCreateDocumentClientExceptionWhenResponseMessageIsNullExpectsArgumentNullException()
        {
            IClientSideRequestStatistics requestStatistics = new ClientSideRequestStatisticsTraceDatum(
                startTime: DateTime.UtcNow,
                trace: NoOpTrace.Singleton);

            ArgumentNullException argumentNullException = await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                    responseMessage: default,
                    requestStatistics: requestStatistics)
            );

            Assert.IsNotNull(argumentNullException);
            Assert.AreEqual(expected: "Value cannot be null. (Parameter 'responseMessage')", actual: argumentNullException.Message);
        }

        /// <summary>
        /// Testing CreateDocumentClientExceptionAsync when request statistics argument is null, then expects an argumentNullException.
        /// </summary>
        [TestMethod]
        public async Task TestCreateDocumentClientExceptionWhenRequestStatisticsIsNullExpectsArgumentNullException()
        {
            HttpResponseMessage responseMessage = new(statusCode: System.Net.HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: @"https://pt_ac_test_uri.com/"),
                Content = new StringContent(
                    content: JsonConvert.SerializeObject(
                        value: new Error() { Code = HttpStatusCode.NotFound.ToString(), Message = "" })),
            };

            ArgumentNullException argumentNullException = await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                    responseMessage: responseMessage,
                    requestStatistics: default)
            );

            Assert.IsNotNull(argumentNullException);
            Assert.AreEqual(expected: "Value cannot be null. (Parameter 'requestStatistics')", actual: argumentNullException.Message);
        }

        private static IClientSideRequestStatistics CreateClientSideRequestStatistics()
        {
            return new ClientSideRequestStatisticsTraceDatum(
                startTime: DateTime.UtcNow,
                trace: NoOpTrace.Singleton);
        }
    }
}
