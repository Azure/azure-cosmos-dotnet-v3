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
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="ClientExtensions"/>.
    /// </summary>
    [TestClass]
    public class ClientExtensionsUnitTests
    {
        [TestMethod]
        public async Task TestBadRequestParseHandling()
        {
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://localhost:8081");
            HttpResponseMessage message = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                ReasonPhrase = "Invalid Parameter",
                RequestMessage = httpRequestMessage
            };
            message.Headers.Add("x-ms-activity-id", Guid.NewGuid().ToString());
            message.Content = new StringContent("<html>Your response text</html>", encoding: Encoding.Default, mediaType: "text/html");

            try
            {
                DocumentServiceResponse dsr = await ClientExtensions.ParseResponseAsync(message);
            }
            catch (DocumentClientException dce)
            {
                Assert.IsNotNull(dce.Message);
                Assert.AreEqual(HttpStatusCode.BadRequest, dce.StatusCode);
                Assert.AreEqual("Invalid Parameter", dce.StatusDescription);
            }
        }

        [TestMethod]
        public async Task TestJsonParseHandling()
        {
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://localhost:8081");
            HttpResponseMessage message = new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                ReasonPhrase = "Id already exists",
                RequestMessage = httpRequestMessage
            };
            message.Headers.Add("x-ms-activity-id", Guid.NewGuid().ToString());
            message.Content = new StringContent(
                @"{ ""id"": ""test"", ""message"": ""Conflict"", ""Code"": ""409""}",
                encoding: Encoding.Default,
                mediaType: "application/json");

            try
            {
                DocumentServiceResponse dsr = await ClientExtensions.ParseResponseAsync(message);
            }
            catch (DocumentClientException dce)
            {
                Assert.IsNotNull(dce.Message);
                Assert.AreEqual(HttpStatusCode.Conflict, dce.StatusCode);
                Assert.AreEqual("Id already exists", dce.StatusDescription);
            }
        }
    }
}