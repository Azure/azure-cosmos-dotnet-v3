//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Net.Http;
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
        /// Testing the exception behavior when a response from the Gateway has no response (deserializable Error object) based on the content length.
        /// For more information, see <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4162"/>.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [DataRow(@"")]
        [DataRow(@"    ")]
        public async Task CreateDocumentClientExceptionInvalidJsonResponseFromGatewayTestAsync(string content)
        {
            HttpResponseMessage responseMessage = new(statusCode: System.Net.HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: @"https://pt_ac_test_uri.com/"),
                Content = new StringContent(
                    content: content),
            };

            responseMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

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
        /// Testing the exception behavior when a response from the Gateway has a response (deserializable Error object) based the content length.
        /// For more information, see <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4162"/>.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [DataRow(@"This is the content of a test error message.")]
        public async Task CreateDocumentClientExceptionValidJsonResponseFromGatewayTestAsync(string content)
        {
            HttpResponseMessage responseMessage = new(statusCode: System.Net.HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: @"https://pt_ac_test_uri.com/"),
                Content = new StringContent(
                    content: JsonConvert.SerializeObject(
                        value: new Error() { Code = HttpStatusCode.NotFound.ToString(), Message = content })),
            };

            responseMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            IClientSideRequestStatistics requestStatistics = new ClientSideRequestStatisticsTraceDatum(
                startTime: DateTime.UtcNow,
                trace: NoOpTrace.Singleton);

            DocumentClientException documentClientException = await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                responseMessage: responseMessage,
                requestStatistics: requestStatistics);

            Assert.IsNotNull(value: documentClientException);
            Assert.AreEqual(expected: HttpStatusCode.NotFound, actual: documentClientException.StatusCode);
            Assert.IsTrue(condition: documentClientException.Message.Contains(content));

            Assert.IsNotNull(value: documentClientException.Error);
            Assert.AreEqual(expected: HttpStatusCode.NotFound.ToString(), actual: documentClientException.Error.Code);
            Assert.AreEqual(expected: content, actual: documentClientException.Error.Message);
        }
    }
}
