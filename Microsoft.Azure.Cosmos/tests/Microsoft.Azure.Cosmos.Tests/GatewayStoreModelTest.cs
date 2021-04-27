//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Tests for <see cref="GatewayStoreModel"/>.
    /// </summary>
    [TestClass]
    public class GatewayStoreModelTest
    {
        private class TestTraceListener : TraceListener
        {
            public Action<string> Callback { get; set; }
            public override bool IsThreadSafe => true;
            public override void Write(string message) => this.Callback(message);
            public override void WriteLine(string message) => this.Callback(message);

        }

        /// <summary>
        /// Tests to make sure OpenAsync should fail fast with bad url.
        /// </summary>
        [TestMethod]
        [Owner("kraman")]
        public async Task TestOpenAsyncFailFast()
        {
            const string accountEndpoint = "https://veryrandomurl123456789.documents.azure.com:443/";

            bool failedToResolve = false;
            bool didNotRetry = false;

            const string failedToResolveMessage = "Fail to reach gateway endpoint https://veryrandomurl123456789.documents.azure.com/, ";
            string didNotRetryMessage = null;

            void TraceHandler(string message)
            {
                if (message.Contains(failedToResolveMessage))
                {
                    Assert.IsFalse(failedToResolve, "Failure to resolve should happen only once.");
                    failedToResolve = true;
                    didNotRetryMessage = message.Substring(failedToResolveMessage.Length).Split('\n')[0];
                }

                if (failedToResolve && message.Contains("NOT be retried") && message.Contains(didNotRetryMessage))
                {
                    didNotRetry = true;
                }

                Console.WriteLine(message);
            }

            DefaultTrace.TraceSource.Listeners.Add(new TestTraceListener { Callback = TraceHandler });

            try
            {
                DocumentClient myclient = new DocumentClient(new Uri(accountEndpoint), "base64encodedurl",
                    new ConnectionPolicy
                    {
                    });

                await myclient.OpenAsync();
            }
            catch
            {
            }

            // it should fail fast and not into the retry logic.
            Assert.IsTrue(failedToResolve, "OpenAsync did not fail to resolve. No matching trace was received.");
            Assert.IsTrue(didNotRetry, "OpenAsync did not fail without retrying. No matching trace was received.");
        }

        /// <summary>
        /// Tests that after web exception we retry and request's content is preserved.
        /// </summary>
        [TestMethod]
        public async Task TestRetries()
        {
            int run = 0;
            Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc = async request =>
            {
                string content = await request.Content.ReadAsStringAsync();
                Assert.AreEqual("content1", content);

                if (run == 0)
                {
                    run++;
                    throw new WebException("", WebExceptionStatus.ConnectFailure);
                }
                else
                {
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Response") };
                }
            };

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            GatewayStoreModel storeModel = new GatewayStoreModel(
                endpointManager,
                sessionContainer,
                ConsistencyLevel.Eventual,
                eventSource,
                null,
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            using (new ActivityScope(Guid.NewGuid()))
            {
                using (DocumentServiceRequest request =
                DocumentServiceRequest.Create(
                    Documents.OperationType.Query,
                    Documents.ResourceType.Document,
                    new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                    new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                    AuthorizationTokenType.PrimaryMasterKey,
                    null))
                {
                    await storeModel.ProcessMessageAsync(request);
                }
            }

            Assert.IsTrue(run > 0);

        }

        [TestMethod]
        public async Task TestApplySessionForMasterOperation()
        {
            List<ResourceType> resourceTypes = new List<ResourceType>()
            {
                ResourceType.Database,
                ResourceType.Collection,
                ResourceType.User,
                ResourceType.Permission,
                ResourceType.StoredProcedure,
                ResourceType.Trigger,
                ResourceType.UserDefinedFunction,
                ResourceType.Offer,
                ResourceType.DatabaseAccount,
                ResourceType.PartitionKeyRange,
                ResourceType.UserDefinedType,
            };

            List<OperationType> operationTypes = new List<OperationType>()
            {
                OperationType.Create,
                OperationType.Delete,
                OperationType.Read,
                OperationType.Upsert,
                OperationType.Replace
            };

            foreach (ResourceType resourceType in resourceTypes)
            {
                foreach (OperationType operationType in operationTypes)
                {
                    Assert.IsTrue(GatewayStoreModel.IsMasterOperation(
                        resourceType,
                        operationType),
                        $"{resourceType}, {operationType}");

                    DocumentServiceRequest dsr = DocumentServiceRequest.CreateFromName(
                        operationType,
                        "Test",
                        resourceType,
                        AuthorizationTokenType.PrimaryMasterKey);

                    dsr.Headers.Add(HttpConstants.HttpHeaders.SessionToken, Guid.NewGuid().ToString());

                    await GatewayStoreModel.ApplySessionTokenAsync(
                        dsr,
                        ConsistencyLevel.Session,
                        new Mock<ISessionContainer>().Object,
                        partitionKeyRangeCache: new Mock<PartitionKeyRangeCache>(null, null, null).Object,
                        clientCollectionCache: new Mock<ClientCollectionCache>(new SessionContainer("testhost"), this.GetGatewayStoreModelForConsistencyTest(), null, null).Object);

                    Assert.IsNull(dsr.Headers[HttpConstants.HttpHeaders.SessionToken]);
                }
            }

            Assert.IsTrue(GatewayStoreModel.IsMasterOperation(
                    ResourceType.Document,
                    OperationType.QueryPlan));

            DocumentServiceRequest dsrQueryPlan = DocumentServiceRequest.CreateFromName(
                OperationType.QueryPlan,
                "Test",
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);

            dsrQueryPlan.Headers.Add(HttpConstants.HttpHeaders.SessionToken, Guid.NewGuid().ToString());

            await GatewayStoreModel.ApplySessionTokenAsync(
                dsrQueryPlan,
                ConsistencyLevel.Session,
                new Mock<ISessionContainer>().Object,
                partitionKeyRangeCache: new Mock<PartitionKeyRangeCache>(null, null, null).Object,
                clientCollectionCache: new Mock<ClientCollectionCache>(new SessionContainer("testhost"), this.GetGatewayStoreModelForConsistencyTest(), null, null).Object);


            Assert.IsNull(dsrQueryPlan.Headers[HttpConstants.HttpHeaders.SessionToken]);
        }

        [TestMethod]
        public async Task TestApplySessionForDataOperation()
        {
            List<ResourceType> resourceTypes = new List<ResourceType>()
            {
                ResourceType.Document,
                ResourceType.Conflict,
                ResourceType.Batch
            };

            List<OperationType> operationTypes = new List<OperationType>()
            {
                OperationType.Create,
                OperationType.Delete,
                OperationType.Read,
                OperationType.Upsert,
                OperationType.Replace
            };

            foreach (ResourceType resourceType in resourceTypes)
            {
                foreach (OperationType operationType in operationTypes)
                {
                    Assert.IsFalse(GatewayStoreModel.IsMasterOperation(
                        resourceType,
                        operationType),
                        $"{resourceType}, {operationType}");
                    {
                        // Verify when user does set session token
                        DocumentServiceRequest dsr = DocumentServiceRequest.CreateFromName(
                            operationType,
                            "Test",
                            resourceType,
                            AuthorizationTokenType.PrimaryMasterKey);

                        string dsrSessionToken = Guid.NewGuid().ToString();
                        dsr.Headers.Add(HttpConstants.HttpHeaders.SessionToken, dsrSessionToken);

                        await GatewayStoreModel.ApplySessionTokenAsync(
                            dsr,
                            ConsistencyLevel.Session,
                            new Mock<ISessionContainer>().Object,
                            partitionKeyRangeCache: new Mock<PartitionKeyRangeCache>(null, null, null).Object,
                            clientCollectionCache: new Mock<ClientCollectionCache>(new SessionContainer("testhost"), this.GetGatewayStoreModelForConsistencyTest(), null, null).Object);


                        Assert.AreEqual(dsrSessionToken, dsr.Headers[HttpConstants.HttpHeaders.SessionToken]);
                    }

                    {
                        // Verify when user does not set session token
                        DocumentServiceRequest dsrNoSessionToken = DocumentServiceRequest.CreateFromName(
                            operationType,
                            "Test",
                            resourceType,
                            AuthorizationTokenType.PrimaryMasterKey);

                        string dsrSessionToken = Guid.NewGuid().ToString();
                        Mock<ISessionContainer> sMock = new Mock<ISessionContainer>();
                        sMock.Setup(x => x.ResolveGlobalSessionToken(dsrNoSessionToken)).Returns(dsrSessionToken);

                        await GatewayStoreModel.ApplySessionTokenAsync(
                            dsrNoSessionToken,
                            ConsistencyLevel.Session,
                            sMock.Object,
                            partitionKeyRangeCache: new Mock<PartitionKeyRangeCache>(null, null, null).Object,
                            clientCollectionCache: new Mock<ClientCollectionCache>(new SessionContainer("testhost"), this.GetGatewayStoreModelForConsistencyTest(), null, null).Object);


                        if (dsrNoSessionToken.IsReadOnlyRequest || dsrNoSessionToken.OperationType == OperationType.Batch)
                        {
                            Assert.AreEqual(dsrSessionToken, dsrNoSessionToken.Headers[HttpConstants.HttpHeaders.SessionToken]);
                        }
                        else
                        {
                            Assert.IsNull(dsrNoSessionToken.Headers[HttpConstants.HttpHeaders.SessionToken]);
                        }
                    }

                    {
                        // Verify when partition key range is configured
                        DocumentServiceRequest dsr = DocumentServiceRequest.CreateFromName(
                            operationType,
                            "Test",
                            resourceType,
                            AuthorizationTokenType.PrimaryMasterKey);

                        string partitionKeyRangeId = "1";
                        dsr.Headers[WFConstants.BackendHeaders.PartitionKeyRangeId] = new PartitionKeyRangeIdentity(partitionKeyRangeId).ToHeader();

                        string dsrSessionToken = Guid.NewGuid().ToString();
                        Mock<ISessionContainer> sMock = new Mock<ISessionContainer>();
                        sMock.Setup(x => x.ResolveGlobalSessionToken(dsr)).Returns(dsrSessionToken);

                        ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("ccZ1ANCszwk=");
                        containerProperties.Id = "TestId";
                        containerProperties.PartitionKeyPath = "/pk";

                        Mock<CollectionCache> mockCollectionCahce = new Mock<CollectionCache>(MockBehavior.Strict);
                        mockCollectionCahce.Setup(x => x.ResolveCollectionAsync(
                            dsr,
                            It.IsAny<CancellationToken>(),
                            NoOpTrace.Singleton)).Returns(Task.FromResult(containerProperties));

                        Mock<PartitionKeyRangeCache> mockPartitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(MockBehavior.Strict, null, null, null);
                        mockPartitionKeyRangeCache.Setup(x => x.TryGetPartitionKeyRangeByIdAsync(
                            containerProperties.ResourceId,
                            partitionKeyRangeId,
                            It.IsAny<ITrace>(),
                            false)).Returns(Task.FromResult(new PartitionKeyRange()));

                        await GatewayStoreModel.ApplySessionTokenAsync(
                            dsr,
                            ConsistencyLevel.Session,
                            sMock.Object,
                            partitionKeyRangeCache: mockPartitionKeyRangeCache.Object,
                            clientCollectionCache: mockCollectionCahce.Object);

                        if (dsr.IsReadOnlyRequest || dsr.OperationType == OperationType.Batch)
                        {
                            Assert.AreEqual(dsrSessionToken, dsr.Headers[HttpConstants.HttpHeaders.SessionToken]);
                        }
                        else
                        {
                            Assert.IsNull(dsr.Headers[HttpConstants.HttpHeaders.SessionToken]);
                        }
                    }
                }
            }

            // Verify stored procedure execute
            Assert.IsFalse(GatewayStoreModel.IsMasterOperation(
                ResourceType.StoredProcedure,
                OperationType.ExecuteJavaScript));

            DocumentServiceRequest dsrSprocExecute = DocumentServiceRequest.CreateFromName(
                OperationType.ExecuteJavaScript,
                "Test",
                ResourceType.StoredProcedure,
                AuthorizationTokenType.PrimaryMasterKey);

            string sessionToken = Guid.NewGuid().ToString();
            dsrSprocExecute.Headers.Add(HttpConstants.HttpHeaders.SessionToken, sessionToken);

            await GatewayStoreModel.ApplySessionTokenAsync(
                dsrSprocExecute,
                ConsistencyLevel.Session,
                new Mock<ISessionContainer>().Object,
                partitionKeyRangeCache: new Mock<PartitionKeyRangeCache>(null, null, null).Object,
                clientCollectionCache: new Mock<ClientCollectionCache>(new SessionContainer("testhost"), this.GetGatewayStoreModelForConsistencyTest(), null, null).Object);


            Assert.AreEqual(sessionToken, dsrSprocExecute.Headers[HttpConstants.HttpHeaders.SessionToken]);
        }

        [TestMethod]
        public async Task TestErrorResponsesProvideBody()
        {
            string testContent = "Content";
            Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc = request =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent(testContent) });
            };

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            GatewayStoreModel storeModel = new GatewayStoreModel(
                endpointManager,
                sessionContainer,
                ConsistencyLevel.Eventual,
                eventSource,
                null,
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            using (new ActivityScope(Guid.NewGuid()))
            {
                using (DocumentServiceRequest request =
                DocumentServiceRequest.Create(
                    Documents.OperationType.Query,
                    Documents.ResourceType.Document,
                    new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                    new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                    AuthorizationTokenType.PrimaryMasterKey,
                    null))
                {
                    request.UseStatusCodeForFailures = true;
                    request.UseStatusCodeFor429 = true;

                    DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);
                    Assert.IsNotNull(response.ResponseBody);
                    using (StreamReader reader = new StreamReader(response.ResponseBody))
                    {
                        Assert.AreEqual(testContent, await reader.ReadToEndAsync());
                    }
                }
            }

        }

        [TestMethod]
        // Verify that for known exceptions, session token is updated
        public async Task GatewayStoreModel_Exception_UpdateSessionTokenOnKnownException()
        {
            INameValueCollection headers = new StoreRequestNameValueCollection();
            headers.Set(HttpConstants.HttpHeaders.SessionToken, "0:1#100#1=20#2=5#3=31");
            headers.Set(WFConstants.BackendHeaders.LocalLSN, "10");
            await this.GatewayStoreModel_Exception_UpdateSessionTokenOnKnownException(new ConflictException("test", headers, new Uri("http://one.com")));
            await this.GatewayStoreModel_Exception_UpdateSessionTokenOnKnownException(new NotFoundException("test", headers, new Uri("http://one.com")));
            await this.GatewayStoreModel_Exception_UpdateSessionTokenOnKnownException(new PreconditionFailedException("test", headers, new Uri("http://one.com")));
        }

        private async Task GatewayStoreModel_Exception_UpdateSessionTokenOnKnownException(Exception ex)
        {
            const string originalSessionToken = "0:1#100#1=20#2=5#3=30";
            const string updatedSessionToken = "0:1#100#1=20#2=5#3=31";

            Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc = request =>
            {
                throw ex;
            };

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            SessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            GatewayStoreModel storeModel = new GatewayStoreModel(
                endpointManager,
                sessionContainer,
                ConsistencyLevel.Eventual,
                eventSource,
                null,
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            INameValueCollection headers = new StoreRequestNameValueCollection();
            headers.Set(HttpConstants.HttpHeaders.ConsistencyLevel, ConsistencyLevel.Session.ToString());
            headers.Set(HttpConstants.HttpHeaders.SessionToken, originalSessionToken);
            headers.Set(WFConstants.BackendHeaders.PartitionKeyRangeId, "0");

            using (new ActivityScope(Guid.NewGuid()))
            {
                using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                "dbs/OVJwAA==/colls/OVJwAOcMtA0=/docs/OVJwAOcMtA0BAAAAAAAAAA==/",
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
                {
                    request.UseStatusCodeFor429 = true;
                    request.UseStatusCodeForFailures = true;
                    try
                    {
                        DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);
                        Assert.Fail("Should had thrown exception");
                    }
                    catch (Exception)
                    {
                        // Expecting exception
                    }
                    Assert.AreEqual(updatedSessionToken, sessionContainer.GetSessionToken("dbs/OVJwAA==/colls/OVJwAOcMtA0="));
                }
            }
        }

        [TestMethod]
        // Verify that for 429 exceptions, session token is not updated
        public async Task GatewayStoreModel_Exception_NotUpdateSessionTokenOnKnownExceptions()
        {
            INameValueCollection headers = new StoreRequestNameValueCollection();
            headers.Set(HttpConstants.HttpHeaders.SessionToken, "0:1#100#1=20#2=5#3=30");
            headers.Set(WFConstants.BackendHeaders.LocalLSN, "10");
            await this.GatewayStoreModel_Exception_NotUpdateSessionTokenOnKnownException(new RequestRateTooLargeException("429", headers, new Uri("http://one.com")));
        }

        private async Task GatewayStoreModel_Exception_NotUpdateSessionTokenOnKnownException(Exception ex)
        {
            const string originalSessionToken = "0:1#100#1=20#2=5#3=30";

            Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc = request =>
            {
                throw ex;
            };

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            SessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            GatewayStoreModel storeModel = new GatewayStoreModel(
                endpointManager,
                sessionContainer,
                ConsistencyLevel.Eventual,
                eventSource,
                null,
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            INameValueCollection headers = new StoreRequestNameValueCollection();
            headers.Set(HttpConstants.HttpHeaders.ConsistencyLevel, ConsistencyLevel.Session.ToString());
            headers.Set(HttpConstants.HttpHeaders.SessionToken, originalSessionToken);
            headers.Set(WFConstants.BackendHeaders.PartitionKeyRangeId, "0");

            using (new ActivityScope(Guid.NewGuid()))
            {
                using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                "dbs/OVJwAA==/colls/OVJwAOcMtA0=/docs/OVJwAOcMtA0BAAAAAAAAAA==/",
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
                {
                    request.UseStatusCodeFor429 = true;
                    request.UseStatusCodeForFailures = true;
                    try
                    {
                        DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);
                        Assert.Fail("Should had thrown exception");
                    }
                    catch (Exception)
                    {
                        // Expecting exception
                    }
                    Assert.AreEqual(string.Empty, sessionContainer.GetSessionToken("dbs/OVJwAA==/colls/OVJwAOcMtA0="));
                }
            }
        }

        /// <summary>
        /// Tests that empty session token is sent for operations on Session Consistent resources like
        /// Databases, Collections, Users, Permissions, PartitionKeyRanges, DatabaseAccounts and Offers
        /// </summary>
        [TestMethod]
        public async Task TestSessionTokenForSessionConsistentResourceType()
        {
            GatewayStoreModel storeModel = GetGatewayStoreModelForConsistencyTest();

            using (DocumentServiceRequest request =
                DocumentServiceRequest.Create(
                    Documents.OperationType.Read,
                    Documents.ResourceType.Collection,
                    new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                    new MemoryStream(Encoding.UTF8.GetBytes("collection")),
                    AuthorizationTokenType.PrimaryMasterKey,
                    null))
            {
                await TestGatewayStoreModelProcessMessageAsync(storeModel, request);
            }
        }

        /// <summary>
        /// Tests that non-empty session token is sent for operations on Session inconsistent resources like
        /// Documents, Sprocs, UDFs, Triggers
        /// </summary>
        [TestMethod]
        public async Task TestSessionTokenForSessionInconsistentResourceType()
        {
            GatewayStoreModel storeModel = GetGatewayStoreModelForConsistencyTest();

            using (DocumentServiceRequest request =
                DocumentServiceRequest.Create(
                    Documents.OperationType.Query,
                    Documents.ResourceType.Document,
                    new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                    new MemoryStream(Encoding.UTF8.GetBytes("document")),
                    AuthorizationTokenType.PrimaryMasterKey,
                    null))
            {
                await TestGatewayStoreModelProcessMessageAsync(storeModel, request);
            }
        }

        /// <summary>
        /// Tests that session token is available for document operation after it is stripped out of header
        /// for collection operaion
        /// </summary>
        [TestMethod]
        public async Task TestSessionTokenAvailability()
        {
            GatewayStoreModel storeModel = GetGatewayStoreModelForConsistencyTest();

            using (DocumentServiceRequest request =
                DocumentServiceRequest.Create(
                    Documents.OperationType.Read,
                    Documents.ResourceType.Collection,
                    new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                    new MemoryStream(Encoding.UTF8.GetBytes("collection")),
                    AuthorizationTokenType.PrimaryMasterKey,
                    null))
            {
                await TestGatewayStoreModelProcessMessageAsync(storeModel, request);
            }

            using (DocumentServiceRequest request =
                DocumentServiceRequest.Create(
                    Documents.OperationType.Query,
                    Documents.ResourceType.Document,
                    new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                    new MemoryStream(Encoding.UTF8.GetBytes("document")),
                    AuthorizationTokenType.PrimaryMasterKey,
                    null))
            {
                await TestGatewayStoreModelProcessMessageAsync(storeModel, request);
            }

        }

        [TestMethod]
        // When exceptionless is turned on Session Token should only be updated on known failures
        public async Task GatewayStoreModel_Exceptionless_UpdateSessionTokenOnKnownFailedStoreResponses()
        {
            await this.GatewayStoreModel_Exceptionless_UpdateSessionTokenOnKnownResponses(HttpStatusCode.Conflict);
            await this.GatewayStoreModel_Exceptionless_UpdateSessionTokenOnKnownResponses(HttpStatusCode.NotFound, SubStatusCodes.OwnerResourceNotFound);
            await this.GatewayStoreModel_Exceptionless_UpdateSessionTokenOnKnownResponses(HttpStatusCode.PreconditionFailed);
        }

        private async Task GatewayStoreModel_Exceptionless_UpdateSessionTokenOnKnownResponses(HttpStatusCode httpStatusCode, SubStatusCodes subStatusCode = SubStatusCodes.Unknown)
        {
            const string originalSessionToken = "0:1#100#1=20#2=5#3=30";
            const string updatedSessionToken = "0:1#100#1=20#2=5#3=31";

            Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc = request =>
            {
                HttpResponseMessage response = new HttpResponseMessage(httpStatusCode);
                response.Headers.Add(HttpConstants.HttpHeaders.SessionToken, updatedSessionToken);
                response.Headers.Add(WFConstants.BackendHeaders.SubStatus, subStatusCode.ToString());
                return Task.FromResult(response);
            };

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            SessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            GatewayStoreModel storeModel = new GatewayStoreModel(
                endpointManager,
                sessionContainer,
                ConsistencyLevel.Eventual,
                eventSource,
                null,
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            INameValueCollection headers = new StoreRequestNameValueCollection();
            headers.Set(HttpConstants.HttpHeaders.ConsistencyLevel, ConsistencyLevel.Session.ToString());
            headers.Set(HttpConstants.HttpHeaders.SessionToken, originalSessionToken);
            headers.Set(WFConstants.BackendHeaders.PartitionKeyRangeId, "0");

            using (new ActivityScope(Guid.NewGuid()))
            {
                using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                "dbs/OVJwAA==/colls/OVJwAOcMtA0=/docs/OVJwAOcMtA0BAAAAAAAAAA==/",
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
                {
                    request.UseStatusCodeFor429 = true;
                    request.UseStatusCodeForFailures = true;
                    DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);
                    Assert.AreEqual(updatedSessionToken, sessionContainer.GetSessionToken("dbs/OVJwAA==/colls/OVJwAOcMtA0="));
                }
            }
        }

        [TestMethod]
        [Owner("maquaran")]
        // Validates that if its a master resource, we don't update the Session Token, even though the status code would be one of the included ones
        public async Task GatewayStoreModel_Exceptionless_NotUpdateSessionTokenOnKnownFailedMasterResource()
        {
            await this.GatewayStoreModel_Exceptionless_NotUpdateSessionTokenOnKnownResponses(ResourceType.Collection, HttpStatusCode.Conflict);
        }

        [TestMethod]
        [Owner("maquaran")]
        // When exceptionless is turned on Session Token should only be updated on known failures
        public async Task GatewayStoreModel_Exceptionless_NotUpdateSessionTokenOnKnownFailedStoreResponses()
        {
            await this.GatewayStoreModel_Exceptionless_NotUpdateSessionTokenOnKnownResponses(ResourceType.Document, (HttpStatusCode)429);
        }

        private async Task GatewayStoreModel_Exceptionless_NotUpdateSessionTokenOnKnownResponses(ResourceType resourceType, HttpStatusCode httpStatusCode, SubStatusCodes subStatusCode = SubStatusCodes.Unknown)
        {
            const string originalSessionToken = "0:1#100#1=20#2=5#3=30";
            const string updatedSessionToken = "0:1#100#1=20#2=5#3=31";

            Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc = request =>
            {
                HttpResponseMessage response = new HttpResponseMessage(httpStatusCode);
                response.Headers.Add(HttpConstants.HttpHeaders.SessionToken, updatedSessionToken);
                response.Headers.Add(WFConstants.BackendHeaders.SubStatus, subStatusCode.ToString());
                return Task.FromResult(response);
            };

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            SessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            GatewayStoreModel storeModel = new GatewayStoreModel(
                endpointManager,
                sessionContainer,
                ConsistencyLevel.Eventual,
                eventSource,
                null,
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            INameValueCollection headers = new StoreRequestNameValueCollection();
            headers.Set(HttpConstants.HttpHeaders.ConsistencyLevel, ConsistencyLevel.Session.ToString());
            headers.Set(HttpConstants.HttpHeaders.SessionToken, originalSessionToken);
            headers.Set(WFConstants.BackendHeaders.PartitionKeyRangeId, "0");

            using (new ActivityScope(Guid.NewGuid()))
            {
                using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                resourceType,
                "dbs/OVJwAA==/colls/OVJwAOcMtA0=/docs/OVJwAOcMtA0BAAAAAAAAAA==/",
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
                {
                    request.UseStatusCodeFor429 = true;
                    request.UseStatusCodeForFailures = true;
                    DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);
                    Assert.AreEqual(string.Empty, sessionContainer.GetSessionToken("dbs/OVJwAA==/colls/OVJwAOcMtA0="));
                }
            }
        }

        private class MockMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc;

            public MockMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> func)
            {
                this.sendFunc = func;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return await this.sendFunc(request);
            }
        }

        private GatewayStoreModel GetGatewayStoreModelForConsistencyTest()
        {
            Func<HttpRequestMessage, Task<HttpResponseMessage>> messageHandler = async request =>
            {
                String content = await request.Content.ReadAsStringAsync();
                if (content.Equals("document"))
                {
                    IEnumerable<string> sessionTokens = request.Headers.GetValues("x-ms-session-token");
                    string sessionToken = "";
                    foreach (string singleToken in sessionTokens)
                    {
                        sessionToken = singleToken;
                        break;
                    }
                    Assert.AreEqual(sessionToken, "range_0:1#9#4=8#5=7");
                }
                else
                {
                    IEnumerable<string> enumerable;
                    Assert.IsFalse(request.Headers.TryGetValues("x-ms-session-token", out enumerable));
                }
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Response") };
            };

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));
            mockDocumentClient.Setup(client => client.ConsistencyLevel).Returns(Documents.ConsistencyLevel.Session);

            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());

            SessionContainer sessionContainer = new SessionContainer(string.Empty);
            sessionContainer.SetSessionToken(
                    ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString(),
                    "dbs/db1/colls/coll1",
                    new StoreRequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#9#4=8#5=7" } });

            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler httpMessageHandler = new MockMessageHandler(messageHandler);

            GatewayStoreModel storeModel = new GatewayStoreModel(
               endpointManager,
                sessionContainer,
                ConsistencyLevel.Eventual,
                eventSource,
                null,
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(httpMessageHandler)));

            ClientCollectionCache clientCollectionCache = new Mock<ClientCollectionCache>(new SessionContainer("testhost"), storeModel, null, null).Object;
            PartitionKeyRangeCache partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(null, storeModel, clientCollectionCache).Object;
            storeModel.SetCaches(partitionKeyRangeCache, clientCollectionCache);

            return storeModel;
        }

        private async Task TestGatewayStoreModelProcessMessageAsync(GatewayStoreModel storeModel, DocumentServiceRequest request)
        {
            using (new ActivityScope(Guid.NewGuid()))
            {
                request.Headers["x-ms-session-token"] = "range_0:1#9#4=8#5=7";
                await storeModel.ProcessMessageAsync(request);
                request.Headers.Remove("x-ms-session-token");
                request.Headers["x-ms-consistency-level"] = "Session";
                await storeModel.ProcessMessageAsync(request);
            }
        }
    }
}
