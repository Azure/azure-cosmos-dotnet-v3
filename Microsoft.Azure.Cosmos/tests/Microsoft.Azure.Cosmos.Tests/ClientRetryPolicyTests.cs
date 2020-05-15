//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="ClientRetryPolicy"/>
    /// </summary>

    [TestClass]
    public class ClientRetryPolicyTests
    {
        [TestMethod]
        public async Task ClientRetryPolicy_RetryOnSessionNotAvailableUpToMax_Exception()
        {
            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));
            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            ClientRetryPolicy clientRetryPolicy = new ClientRetryPolicy(endpointManager, true, new RetryOptions());
            DocumentClientException documentClientException = new DocumentClientException("test", HttpStatusCode.NotFound, SubStatusCodes.ReadSessionNotAvailable);
            INameValueCollection headers = new DictionaryNameValueCollection();
            DocumentServiceRequest documentServiceRequest = new DocumentServiceRequest(
                OperationType.Create,
                ResourceType.Document,
                "dbs/db/colls/coll1/docs/doc1",
                null,
                AuthorizationTokenType.PrimaryMasterKey,
                headers);
            for (int i = 0; i < 5; i++)
            {
                ShouldRetryResult result = await clientRetryPolicy.ShouldRetryAsync(documentClientException, default(CancellationToken));
                Assert.IsTrue(result.ShouldRetry);
            }

            ShouldRetryResult resultAfter = await clientRetryPolicy.ShouldRetryAsync(documentClientException, default(CancellationToken));
            Assert.IsFalse(resultAfter.ShouldRetry);
        }

        [TestMethod]
        public async Task ClientRetryPolicy_RetryOnSessionNotAvailableUpToMax_StatusCode()
        {
            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));
            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            ClientRetryPolicy clientRetryPolicy = new ClientRetryPolicy(endpointManager, true, new RetryOptions());
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.NotFound);
            responseMessage.Headers.SubStatusCode = SubStatusCodes.ReadSessionNotAvailable;
            INameValueCollection headers = new DictionaryNameValueCollection();
            DocumentServiceRequest documentServiceRequest = new DocumentServiceRequest(
                OperationType.Create,
                ResourceType.Document,
                "dbs/db/colls/coll1/docs/doc1",
                null,
                AuthorizationTokenType.PrimaryMasterKey,
                headers);
            for (int i = 0; i < 5; i++)
            {
                ShouldRetryResult result = await clientRetryPolicy.ShouldRetryAsync(responseMessage, default(CancellationToken));
                Assert.IsTrue(result.ShouldRetry);
            }

            ShouldRetryResult resultAfter = await clientRetryPolicy.ShouldRetryAsync(responseMessage, default(CancellationToken));
            Assert.IsFalse(resultAfter.ShouldRetry);
        }
    }
}
