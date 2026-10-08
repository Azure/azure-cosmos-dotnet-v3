//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;
    using System.Net.Security;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosClientResourceUnitTests
    {
        [TestMethod]
        public void ValidateUriGenerationForResources()
        {
            string databaseId = "db1234";
            string crId = "cr42";

            CosmosClientContext context = this.CreateMockClientContext();
            DatabaseInternal db = new DatabaseInlineCore(context, databaseId);
            Assert.AreEqual(db.LinkUri, "dbs/" + databaseId);

            ContainerInternal container = new ContainerInlineCore(context, db, crId);
            Assert.AreEqual(container.LinkUri, "dbs/" + databaseId + "/colls/" + crId);
        }

        [TestMethod]
        public void ValidateItemRequestOptions()
        {
            ItemRequestOptions options = new ItemRequestOptions
            {
                PreTriggers = new List<string>()
                {
                    "preTrigger"
                },

                PostTriggers = new List<string>()
                {
                    "postTrigger"
                }
            };

            RequestMessage httpRequest = new RequestMessage(
                HttpMethod.Post,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative));

            options.PopulateRequestOptions(httpRequest);
            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PreTriggerInclude, out _));
            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PostTriggerInclude, out _));
        }

        [TestMethod]
        public void ValidateItemRequestOptionsMultipleTriggers()
        {
            ItemRequestOptions options = new ItemRequestOptions
            {
                PreTriggers = new List<string>()
                {
                    "preTrigger",
                    "preTrigger2",
                    "preTrigger3",
                    "preTrigger4"
                },

                PostTriggers = new List<string>()
                {
                    "postTrigger",
                    "postTrigger2",
                    "postTrigger3",
                    "postTrigger4",
                    "postTrigger5"
                }
            };

            RequestMessage httpRequest = new RequestMessage(
                HttpMethod.Post,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative));

            options.PopulateRequestOptions(httpRequest);
            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PreTriggerInclude, out _));
            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PostTriggerInclude, out _));
        }

        [TestMethod]
        public void ValidateSetItemRequestOptions()
        {
            ItemRequestOptions options = new ItemRequestOptions
            {
                PreTriggers = new List<string>() { "preTrigger" },
                PostTriggers = new List<string>() { "postTrigger" }
            };

            RequestMessage httpRequest = new RequestMessage(
                HttpMethod.Post,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative));

            options.PopulateRequestOptions(httpRequest);
            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PreTriggerInclude, out _));
            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PostTriggerInclude, out _));
        }

        [TestMethod]
        public void InitializeBatchExecutorForContainer_Null_WhenAllowBulk_False()
        {
            string databaseId = "db1234";
            string crId = "cr42";

            CosmosClientContext context = this.CreateMockClientContext();
            DatabaseInternal db = new DatabaseInlineCore(context, databaseId);
            ContainerInternal container = new ContainerInlineCore(context, db, crId);
            Assert.IsNull(container.BatchExecutor);
        }

        [TestMethod]
        public void InitializeBatchExecutorForContainer_NotNull_WhenAllowBulk_True()
        {
            string databaseId = "db1234";
            string crId = "cr42";

            CosmosClientContext context = this.CreateMockClientContext(allowBulkExecution: true);

            DatabaseInternal db = new DatabaseInlineCore(context, databaseId);
            ContainerInternal container = new ContainerInlineCore(context, db, crId);
            Assert.IsNotNull(container.BatchExecutor);
        }

        [TestMethod]
        public void WithServerCertificateAddedClientOptions_CreateContext_RemoteCertificateCallbackReturnsTrue()
        {
            //Arrange
            X509Certificate2 x509Certificate2 = new CertificateRequest("cn=www.test", ECDsa.Create(), HashAlgorithmName.SHA256).CreateSelfSigned(DateTime.Now, DateTime.Now.AddYears(1));
            X509Chain x509Chain = new X509Chain();
            SslPolicyErrors sslPolicyErrors = new SslPolicyErrors();

            string authKeyValue = "MockAuthKey";
            Mock<AuthorizationTokenProvider> mockAuth = new Mock<AuthorizationTokenProvider>(MockBehavior.Strict);
            mockAuth.Setup(x => x.Dispose());
            mockAuth.Setup(x => x.AddAuthorizationHeaderAsync(
                It.IsAny<Documents.Collections.INameValueCollection>(),
                It.IsAny<Uri>(),
                It.IsAny<string>(),
                It.IsAny<Documents.AuthorizationTokenType>()))
                .Callback<Documents.Collections.INameValueCollection, Uri, string, Documents.AuthorizationTokenType>(
                (headers, uri, verb, tokenType) => headers.Add(Documents.HttpConstants.HttpHeaders.Authorization, authKeyValue))
                .Returns(() => new ValueTask());

            CosmosClient client = new CosmosClient(
                "https://localhost:8081",
                authorizationTokenProvider: mockAuth.Object,
                new CosmosClientOptions()
                {
                    ServerCertificateCustomValidationCallback = (X509Certificate2 cerf, X509Chain chain, SslPolicyErrors error) => true
                });

            //Act
            CosmosClientContext context = ClientContextCore.Create(
                    client,
                    client.ClientOptions);

            //Assert
            Assert.IsTrue(context.DocumentClient.remoteCertificateValidationCallback(new object(), x509Certificate2, x509Chain, sslPolicyErrors));

        }

        private CosmosClientContext CreateMockClientContext(bool allowBulkExecution = false)
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));

            return ClientContextCore.Create(
                mockClient.Object,
                new MockDocumentClient(),
                new CosmosClientOptions() { AllowBulkExecution = allowBulkExecution });
        }
    }
}