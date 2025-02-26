//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

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
            public override void Write(string message)
            {
                this.Callback(message);
            }

            public override void WriteLine(string message)
            {
                this.Callback(message);
            }
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

            const string failedToResolveMessage = "GlobalEndpointManager: Fail to reach gateway endpoint https://veryrandomurl123456789.documents.azure.com/, ";
            string didNotRetryMessage = null;

            void TraceHandler(string message)
            {
                if (message.Contains(failedToResolveMessage))
                {
                    Assert.IsFalse(failedToResolve, "Failure to resolve should happen only once.");
                    failedToResolve = true;
                    didNotRetryMessage = message[failedToResolveMessage.Length..].Split('\n')[0];
                }

                if (failedToResolve && message.Contains("NOT be retried") && message.Contains(didNotRetryMessage))
                {
                    didNotRetry = true;
                }

                Console.WriteLine(message);
            }

            TestTraceListener testTraceListener = new TestTraceListener { Callback = TraceHandler };
            DefaultTrace.TraceSource.Listeners.Add(testTraceListener);
            DefaultTrace.InitEventListener();

            try
            {
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

                DefaultTrace.TraceSource.Flush();

                // it should fail fast and not into the retry logic.
                Assert.IsTrue(failedToResolve, "OpenAsync did not fail to resolve. No matching trace was received.");
                Assert.IsTrue(didNotRetry, "OpenAsync did not fail without retrying. No matching trace was received.");
            }
            finally
            {

                DefaultTrace.TraceSource.Listeners.Remove(testTraceListener);
            }
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

            using GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            using GatewayStoreModel storeModel = new GatewayStoreModel(
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

        /// <summary>
        /// Verifies that if the DCE has Properties set, the HttpRequestMessage has them too. Used on ThinClient.
        /// </summary>
        [TestMethod]
        public async Task PassesPropertiesFromDocumentServiceRequest()
        {
            IDictionary<string, object> properties = new Dictionary<string, object>()
            {
                {"property1", Guid.NewGuid() },
                {"property2", Guid.NewGuid().ToString() }
            };

            Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc = request =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                Assert.AreEqual(properties.Count, request.Properties.Count);
                foreach (KeyValuePair<string, object> item in properties)
                {
                    Assert.AreEqual(item.Value, request.Properties[item.Key]);
                }
#pragma warning restore CS0618 // Type or member is obsolete

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            };

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            using GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            using GatewayStoreModel storeModel = new GatewayStoreModel(
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
                    // Add properties to the DCE
                    request.Properties = new Dictionary<string, object>();
                    foreach (KeyValuePair<string, object> property in properties)
                    {
                        request.Properties.Add(property.Key, property.Value);
                    }

                    await storeModel.ProcessMessageAsync(request);
                }
            }
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

                    await this.GetGatewayStoreModelForConsistencyTest(async (gatewayStoreModel) =>
                    {
                        await GatewayStoreModel.ApplySessionTokenAsync(
                           dsr,
                           ConsistencyLevel.Session,
                           new Mock<ISessionContainer>().Object,
                           partitionKeyRangeCache: new Mock<PartitionKeyRangeCache>(null, null, null, null).Object,
                           clientCollectionCache: new Mock<ClientCollectionCache>(new SessionContainer("testhost"), gatewayStoreModel, null, null, null).Object,
                           globalEndpointManager: Mock.Of<IGlobalEndpointManager>());

                        Assert.IsNull(dsr.Headers[HttpConstants.HttpHeaders.SessionToken]);
                    });
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

            await this.GetGatewayStoreModelForConsistencyTest(async (gatewayStoreModel) =>
            {
                await GatewayStoreModel.ApplySessionTokenAsync(
                    dsrQueryPlan,
                    ConsistencyLevel.Session,
                    new Mock<ISessionContainer>().Object,
                    partitionKeyRangeCache: new Mock<PartitionKeyRangeCache>(null, null, null, null).Object,
                    clientCollectionCache: new Mock<ClientCollectionCache>(new SessionContainer("testhost"), gatewayStoreModel, null, null, null).Object,
                    globalEndpointManager: Mock.Of<IGlobalEndpointManager>());

                Assert.IsNull(dsrQueryPlan.Headers[HttpConstants.HttpHeaders.SessionToken]);
            });
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task TestApplySessionForDataOperation(bool multiMaster)
        {
            List<ResourceType> resourceTypes = new List<ResourceType>()
            {
                ResourceType.Document,
                ResourceType.Conflict
            };

            List<OperationType> operationTypes = new List<OperationType>()
            {
                OperationType.Create,
                OperationType.Delete,
                OperationType.Read,
                OperationType.Upsert,
                OperationType.Replace,
                OperationType.Batch
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

                        await this.GetGatewayStoreModelForConsistencyTest(async (gatewayStoreModel) =>
                        {
                            await GatewayStoreModel.ApplySessionTokenAsync(
                                dsr,
                                ConsistencyLevel.Session,
                                new Mock<ISessionContainer>().Object,
                                partitionKeyRangeCache: new Mock<PartitionKeyRangeCache>(null, null, null, null).Object,
                                clientCollectionCache: new Mock<ClientCollectionCache>(new SessionContainer("testhost"), gatewayStoreModel, null, null, null).Object,
                                globalEndpointManager: Mock.Of<IGlobalEndpointManager>());

                            Assert.AreEqual(dsrSessionToken, dsr.Headers[HttpConstants.HttpHeaders.SessionToken]);
                        });
                    }

                    {
                        // Verify when user does not set session token
                        DocumentServiceRequest dsrNoSessionToken = DocumentServiceRequest.Create(operationType,
                                                        resourceType,
                                                        new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                                                        AuthorizationTokenType.PrimaryMasterKey);

                        SessionContainer sessionContainer = new SessionContainer(string.Empty);
                        sessionContainer.SetSessionToken(
                                ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString(),
                                "dbs/db1/colls/coll1",
                                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#9#4=8#5=7" } });

                        Mock<IGlobalEndpointManager> globalEndpointManager = new Mock<IGlobalEndpointManager>();
                        globalEndpointManager.Setup(gem => gem.CanUseMultipleWriteLocations(It.Is<DocumentServiceRequest>(drs => drs == dsrNoSessionToken))).Returns(multiMaster);
                        await this.GetGatewayStoreModelForConsistencyTest(async (gatewayStoreModel) =>
                        {
                            dsrNoSessionToken.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange { Id = "range_1" };
                            await GatewayStoreModel.ApplySessionTokenAsync(
                                dsrNoSessionToken,
                                ConsistencyLevel.Session,
                                sessionContainer,
                                partitionKeyRangeCache: new Mock<PartitionKeyRangeCache>(null, null, null, null).Object,
                                clientCollectionCache: new Mock<ClientCollectionCache>(new SessionContainer("testhost"), gatewayStoreModel, null, null, null).Object,
                                globalEndpointManager: globalEndpointManager.Object);

                            if (dsrNoSessionToken.IsReadOnlyRequest || dsrNoSessionToken.OperationType == OperationType.Batch || multiMaster)
                            {
                                Assert.AreEqual("range_1:1#9#4=8#5=7", dsrNoSessionToken.Headers[HttpConstants.HttpHeaders.SessionToken]);
                            }
                            else
                            {
                                Assert.IsNull(dsrNoSessionToken.Headers[HttpConstants.HttpHeaders.SessionToken]);
                            }
                        });
                    }

                    {
                        // Verify when partition key range is configured
                        string partitionKeyRangeId = "range_1";
                        DocumentServiceRequest dsr = DocumentServiceRequest.Create(operationType,
                                                        resourceType,
                                                        new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                                                        AuthorizationTokenType.PrimaryMasterKey,
                                                        new RequestNameValueCollection() { { WFConstants.BackendHeaders.PartitionKeyRangeId, new PartitionKeyRangeIdentity(partitionKeyRangeId).ToHeader() } });


                        SessionContainer sessionContainer = new SessionContainer(string.Empty);
                        sessionContainer.SetSessionToken(
                                ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString(),
                                "dbs/db1/colls/coll1",
                                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#9#4=8#5=7" } });

                        ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("ccZ1ANCszwk=");
                        containerProperties.Id = "TestId";
                        containerProperties.PartitionKeyPath = "/pk";

                        Mock<CollectionCache> mockCollectionCahce = new Mock<CollectionCache>(MockBehavior.Strict);
                        mockCollectionCahce.Setup(x => x.ResolveCollectionAsync(
                            dsr,
                            It.IsAny<CancellationToken>(),
                            NoOpTrace.Singleton)).Returns(Task.FromResult(containerProperties));

                        Mock<PartitionKeyRangeCache> mockPartitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(MockBehavior.Strict, null, null, null, null);
                        mockPartitionKeyRangeCache.Setup(x => x.TryGetPartitionKeyRangeByIdAsync(
                            containerProperties.ResourceId,
                            partitionKeyRangeId,
                            It.IsAny<ITrace>(),
                            false)).Returns(Task.FromResult(new PartitionKeyRange { Id = "range_1" }));

                        await GatewayStoreModel.ApplySessionTokenAsync(
                            dsr,
                            ConsistencyLevel.Session,
                            sessionContainer,
                            partitionKeyRangeCache: mockPartitionKeyRangeCache.Object,
                            clientCollectionCache: mockCollectionCahce.Object,
                            globalEndpointManager: Mock.Of<IGlobalEndpointManager>());

                        if (dsr.IsReadOnlyRequest || dsr.OperationType == OperationType.Batch)
                        {
                            Assert.AreEqual("range_1:1#9#4=8#5=7", dsr.Headers[HttpConstants.HttpHeaders.SessionToken]);
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

            await this.GetGatewayStoreModelForConsistencyTest(async (gatewayStoreModel) =>
            {
                await GatewayStoreModel.ApplySessionTokenAsync(
                    dsrSprocExecute,
                    ConsistencyLevel.Session,
                    new Mock<ISessionContainer>().Object,
                    partitionKeyRangeCache: new Mock<PartitionKeyRangeCache>(null, null, null, null).Object,
                    clientCollectionCache: new Mock<ClientCollectionCache>(new SessionContainer("testhost"), gatewayStoreModel, null, null, null).Object,
                    globalEndpointManager: Mock.Of<IGlobalEndpointManager>());

                Assert.AreEqual(sessionToken, dsrSprocExecute.Headers[HttpConstants.HttpHeaders.SessionToken]);
            });
        }

        [DataTestMethod]
        [DataRow(false, false, DisplayName = "Single master - Read")]
        [DataRow(true, false, DisplayName = "Multi master - Read")]
        [DataRow(false, true, DisplayName = "Single master - Write")]
        [DataRow(true, true, DisplayName = "Multi master - Write")]
        public async Task TestRequestOverloadRemovesSessionToken(bool multiMaster, bool isWriteRequest)
        {
            INameValueCollection headers = new RequestNameValueCollection();
            headers.Set(HttpConstants.HttpHeaders.ConsistencyLevel, ConsistencyLevel.Eventual.ToString());

            DocumentServiceRequest dsrNoSessionToken = DocumentServiceRequest.Create(isWriteRequest ? OperationType.Create : OperationType.Read,
                                                                    ResourceType.Document,
                                                                    new Uri("https://foo.com/dbs/db1/colls/coll1/docs/doc1", UriKind.Absolute),
                                                                    AuthorizationTokenType.PrimaryMasterKey,
                                                                    headers);

            SessionContainer sessionContainer = new SessionContainer(string.Empty);
            sessionContainer.SetSessionToken(
                    ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString(),
                    "dbs/db1/colls/coll1",
                    new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#9#4=8#5=7" } });

            Mock<IGlobalEndpointManager> globalEndpointManager = new Mock<IGlobalEndpointManager>();
            globalEndpointManager.Setup(gem => gem.CanUseMultipleWriteLocations(It.Is<DocumentServiceRequest>(drs => drs == dsrNoSessionToken))).Returns(multiMaster);
            await this.GetGatewayStoreModelForConsistencyTest(async (gatewayStoreModel) =>
            {
                dsrNoSessionToken.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange { Id = "range_1" };
                await GatewayStoreModel.ApplySessionTokenAsync(
                    dsrNoSessionToken,
                    ConsistencyLevel.Session,
                    sessionContainer,
                    partitionKeyRangeCache: new Mock<PartitionKeyRangeCache>(null, null, null, null).Object,
                    clientCollectionCache: new Mock<ClientCollectionCache>(new SessionContainer("testhost"), gatewayStoreModel, null, null, null).Object,
                    globalEndpointManager: globalEndpointManager.Object);

                if (isWriteRequest && multiMaster)
                {
                    // Multi master write requests should not lower the consistency and remove the session token
                    Assert.AreEqual("range_1:1#9#4=8#5=7", dsrNoSessionToken.Headers[HttpConstants.HttpHeaders.SessionToken]);
                }
                else
                {
                    Assert.IsNull(dsrNoSessionToken.Headers[HttpConstants.HttpHeaders.SessionToken]);
                }
            });
        }

        [TestMethod]
        public async Task TestErrorResponsesProvideBody()
        {
            string testContent = "Content";
            Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc = request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent(testContent) });

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            using GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            using GatewayStoreModel storeModel = new GatewayStoreModel(
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
            INameValueCollection headers = new RequestNameValueCollection();
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

            Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc = request => throw ex;

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            using GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            SessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            using GatewayStoreModel storeModel = new GatewayStoreModel(
                endpointManager,
                sessionContainer,
                ConsistencyLevel.Eventual,
                eventSource,
                null,
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            INameValueCollection headers = new RequestNameValueCollection();
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
            INameValueCollection headers = new RequestNameValueCollection();
            headers.Set(HttpConstants.HttpHeaders.SessionToken, "0:1#100#1=20#2=5#3=30");
            headers.Set(WFConstants.BackendHeaders.LocalLSN, "10");
            await this.GatewayStoreModel_Exception_NotUpdateSessionTokenOnKnownException(new RequestRateTooLargeException("429", headers, new Uri("http://one.com")));
        }

        private async Task GatewayStoreModel_Exception_NotUpdateSessionTokenOnKnownException(Exception ex)
        {
            const string originalSessionToken = "0:1#100#1=20#2=5#3=30";

            Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc = request => throw ex;

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            using GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            SessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            using GatewayStoreModel storeModel = new GatewayStoreModel(
                endpointManager,
                sessionContainer,
                ConsistencyLevel.Eventual,
                eventSource,
                null,
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            INameValueCollection headers = new RequestNameValueCollection();
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
            await this.GetGatewayStoreModelForConsistencyTest(async (gatewayStoreModel) =>
            {
                using (DocumentServiceRequest request =
                DocumentServiceRequest.Create(
                    Documents.OperationType.Read,
                    Documents.ResourceType.Collection,
                    new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                    new MemoryStream(Encoding.UTF8.GetBytes("collection")),
                    AuthorizationTokenType.PrimaryMasterKey,
                    null))
                {
                    await this.TestGatewayStoreModelProcessMessageAsync(gatewayStoreModel, request);
                }
            });
        }

        /// <summary>
        /// Tests that non-empty session token is sent for operations on Session inconsistent resources like
        /// Documents, Sprocs, UDFs, Triggers
        /// </summary>
        [TestMethod]
        public async Task TestSessionTokenForSessionInconsistentResourceType()
        {
            await this.GetGatewayStoreModelForConsistencyTest(async (gatewayStoreModel) =>
            {
                using (DocumentServiceRequest request =
                DocumentServiceRequest.Create(
                    Documents.OperationType.Query,
                    Documents.ResourceType.Document,
                    new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                    new MemoryStream(Encoding.UTF8.GetBytes("document")),
                    AuthorizationTokenType.PrimaryMasterKey,
                    null))
                {
                    await this.TestGatewayStoreModelProcessMessageAsync(gatewayStoreModel, request);
                }
            });
        }

        /// <summary>
        /// Tests that session token is available for document operation after it is stripped out of header
        /// for collection operaion
        /// </summary>
        [TestMethod]
        public async Task TestSessionTokenAvailability()
        {
            await this.GetGatewayStoreModelForConsistencyTest(async (gatewayStoreModel) =>
            {
                using (DocumentServiceRequest request =
                DocumentServiceRequest.Create(
                    Documents.OperationType.Read,
                    Documents.ResourceType.Collection,
                    new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                    new MemoryStream(Encoding.UTF8.GetBytes("collection")),
                    AuthorizationTokenType.PrimaryMasterKey,
                    null))
                {
                    await this.TestGatewayStoreModelProcessMessageAsync(gatewayStoreModel, request);
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
                    await this.TestGatewayStoreModelProcessMessageAsync(gatewayStoreModel, request);
                }
            });
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

            using GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            SessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            using GatewayStoreModel storeModel = new GatewayStoreModel(
                endpointManager,
                sessionContainer,
                ConsistencyLevel.Eventual,
                eventSource,
                null,
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            INameValueCollection headers = new RequestNameValueCollection();
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
        public async Task GatewayStatsDurationTest()
        {
            bool failedOnce = false;
            Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc = async request =>
            {
                await Task.Delay(1000);
                if (!failedOnce)
                {
                    failedOnce = true;
                    throw new OperationCanceledException();
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Response") };
            };

            HttpMessageHandler mockMessageHandler = new MockMessageHandler(sendFunc);
            CosmosHttpClient cosmosHttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(mockMessageHandler),
                                                                                    DocumentClientEventSource.Instance);

            using (ITrace trace = Tracing.Trace.GetRootTrace(nameof(GatewayStatsDurationTest)))
            {

                Tracing.TraceData.ClientSideRequestStatisticsTraceDatum clientSideRequestStatistics = new Tracing.TraceData.ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace);

                await cosmosHttpClient.SendHttpAsync(() => new ValueTask<HttpRequestMessage>(new HttpRequestMessage(HttpMethod.Get, "http://someuri.com")),
                                                      ResourceType.Document,
                                                      HttpTimeoutPolicyDefault.InstanceShouldThrow503OnTimeout,
                                                      clientSideRequestStatistics,
                                                      CancellationToken.None);

                Assert.AreEqual(clientSideRequestStatistics.HttpResponseStatisticsList.Count, 2);
                // The duration is calculated using date times which can cause the duration to be slightly off. This allows for up to 15 Ms of variance.
                // https://stackoverflow.com/questions/2143140/c-sharp-datetime-now-precision#:~:text=The%20precision%20is%20related%20to,35%2D40%20ms%20accuracy
                Assert.IsTrue(clientSideRequestStatistics.HttpResponseStatisticsList[0].Duration.TotalMilliseconds >= 985, $"First request did was not delayed by at least 1 second. {JsonConvert.SerializeObject(clientSideRequestStatistics.HttpResponseStatisticsList[0])}");
                Assert.IsTrue(clientSideRequestStatistics.HttpResponseStatisticsList[1].Duration.TotalMilliseconds >= 985, $"Second request did was not delayed by at least 1 second. {JsonConvert.SerializeObject(clientSideRequestStatistics.HttpResponseStatisticsList[1])}");
                Assert.IsTrue(clientSideRequestStatistics.HttpResponseStatisticsList[0].RequestStartTime <
                              clientSideRequestStatistics.HttpResponseStatisticsList[1].RequestStartTime);
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

            using GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            SessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            using GatewayStoreModel storeModel = new GatewayStoreModel(
                endpointManager,
                sessionContainer,
                ConsistencyLevel.Eventual,
                eventSource,
                null,
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            INameValueCollection headers = new RequestNameValueCollection();
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

        [TestMethod]
        [Owner("askagarw")]
        public async Task GatewayStoreModel_AvoidGlobalSessionToken()
        {
            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));
            using GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            SessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            using GatewayStoreModel storeModel = new GatewayStoreModel(
                endpointManager,
                sessionContainer,
                ConsistencyLevel.Session,
                eventSource,
                null,
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient()));
            Mock<ClientCollectionCache> clientCollectionCache = new Mock<ClientCollectionCache>(new SessionContainer("testhost"), storeModel, null, null, null);
            Mock<PartitionKeyRangeCache> partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(null, storeModel, clientCollectionCache.Object, endpointManager);

            sessionContainer.SetSessionToken(
                    ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString(),
                    "dbs/db1/colls/coll1",
                    new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_1:1#9#4=8#5=7" } });

            using (DocumentServiceRequest dsr = DocumentServiceRequest.Create(OperationType.Query,
                                                    ResourceType.Document,
                                                    new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                                                    AuthorizationTokenType.PrimaryMasterKey))
            {
                // pkrange 1 : which has session token
                dsr.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange { Id = "range_1" };
                await GatewayStoreModel.ApplySessionTokenAsync(dsr,
                                                ConsistencyLevel.Session,
                                                sessionContainer,
                                                partitionKeyRangeCache.Object,
                                                clientCollectionCache.Object,
                                                endpointManager);
                Assert.AreEqual(dsr.Headers[HttpConstants.HttpHeaders.SessionToken], "range_1:1#9#4=8#5=7");
            }

            using (DocumentServiceRequest dsr = DocumentServiceRequest.Create(OperationType.Query,
                                                    ResourceType.Document,
                                                    new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                                                    AuthorizationTokenType.PrimaryMasterKey))
            {
                // pkrange 2 : which has no session token
                dsr.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange { Id = "range_2" };
                await GatewayStoreModel.ApplySessionTokenAsync(dsr,
                                                ConsistencyLevel.Session,
                                                sessionContainer,
                                                partitionKeyRangeCache.Object,
                                                clientCollectionCache.Object,
                                                endpointManager);
                Assert.AreEqual(dsr.Headers[HttpConstants.HttpHeaders.SessionToken], null);

                // There exists global session token, but we do not use it
                Assert.AreEqual(sessionContainer.ResolveGlobalSessionToken(dsr), "range_1:1#9#4=8#5=7");
            }

            using (DocumentServiceRequest dsr = DocumentServiceRequest.Create(OperationType.Query,
                                                    ResourceType.Document,
                                                    new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                                                    AuthorizationTokenType.PrimaryMasterKey))
            {
                // pkrange 3 : Split scenario where session token exists for parent of pk range
                Collection<string> parents = new Collection<string> { "range_1" };
                dsr.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange { Id = "range_3", Parents = parents };
                await GatewayStoreModel.ApplySessionTokenAsync(dsr,
                                                ConsistencyLevel.Session,
                                                sessionContainer,
                                                partitionKeyRangeCache.Object,
                                                clientCollectionCache.Object,
                                                endpointManager);
                Assert.AreEqual(dsr.Headers[HttpConstants.HttpHeaders.SessionToken], "range_3:1#9#4=8#5=7");
            }
        }

        /// <summary>
        /// When the response contains a PKRangeId header different than the one targeted with the session token, trigger a refresh of the PKRange cache
        /// </summary>
        [DataTestMethod]
        [DataRow("0", "0", false)]
        [DataRow("0", "1", true)]
        public async Task GatewayStoreModel_OnSplitRefreshesPKRanges(string originalPKRangeId, string splitPKRangeId, bool shouldCallRefresh)
        {
            string originalSessionToken = originalPKRangeId + ":1#100#1=20#2=5#3=30";
            string updatedSessionToken = splitPKRangeId + ":1#100#1=20#2=5#3=31";

            Task<HttpResponseMessage> sendFunc(HttpRequestMessage request)
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.Add(HttpConstants.HttpHeaders.SessionToken, updatedSessionToken);
                response.Headers.Add(WFConstants.BackendHeaders.PartitionKeyRangeId, splitPKRangeId);
                return Task.FromResult(response);
            }

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            using GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            SessionContainer sessionContainer = new SessionContainer(string.Empty);
            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            using GatewayStoreModel storeModel = new GatewayStoreModel(
                endpointManager,
                sessionContainer,
                ConsistencyLevel.Session,
                eventSource,
                null,
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            Mock<ClientCollectionCache> clientCollectionCache = new Mock<ClientCollectionCache>(new SessionContainer("testhost"), storeModel, null, null, null);

            Mock<PartitionKeyRangeCache> partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(null, storeModel, clientCollectionCache.Object, endpointManager);
            storeModel.SetCaches(partitionKeyRangeCache.Object, clientCollectionCache.Object);

            INameValueCollection headers = new RequestNameValueCollection();
            headers.Set(HttpConstants.HttpHeaders.SessionToken, originalSessionToken);

            using (new ActivityScope(Guid.NewGuid()))
            {
                using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                "dbs/OVJwAA==/colls/OVJwAOcMtA0=/docs/OVJwAOcMtA0BAAAAAAAAAA==/",
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
                {
                    request.RequestContext.ResolvedCollectionRid = "dbs/OVJwAA==/colls/OVJwAOcMtA0=";
                    request.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange() { Id = originalPKRangeId };
                    await storeModel.ProcessMessageAsync(request);
                    Assert.AreEqual(updatedSessionToken, sessionContainer.GetSessionToken("dbs/OVJwAA==/colls/OVJwAOcMtA0="));

                    partitionKeyRangeCache.Verify(pkRangeCache => pkRangeCache.TryGetPartitionKeyRangeByIdAsync(
                         It.Is<string>(str => str == "dbs/OVJwAA==/colls/OVJwAOcMtA0="),
                         It.Is<string>(str => str == splitPKRangeId),
                         It.IsAny<ITrace>(),
                         It.Is<bool>(b => b == true)), shouldCallRefresh ? Times.Once : Times.Never);
                }
            }
        }

        /// <summary>
        /// Simulating partition split and cache having only the parent token
        /// </summary>
        [TestMethod]
        public async Task GatewayStoreModel_ObtainsSessionFromParent_AfterSplit()
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            // Set token for the parent
            string parentPKRangeId = "0";
            string parentSession = "1#100#4=90#5=1";
            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, $"{parentPKRangeId}:{parentSession}" } }
            );

            // Create the request for the child
            string childPKRangeId = "1";
            DocumentServiceRequest documentServiceRequestToChild = DocumentServiceRequest.CreateFromName(OperationType.Read, "dbs/db1/colls/collName/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null);

            documentServiceRequestToChild.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange()
            {
                Id = childPKRangeId,
                MinInclusive = "",
                MaxExclusive = "AA",
                Parents = new Collection<string>() { parentPKRangeId } // PartitionKeyRange says who is the parent
            };

            Mock<IGlobalEndpointManager> globalEndpointManager = new Mock<IGlobalEndpointManager>();
            await this.GetGatewayStoreModelForConsistencyTest(async (gatewayStoreModel) =>
            {
                await GatewayStoreModel.ApplySessionTokenAsync(
                    documentServiceRequestToChild,
                    ConsistencyLevel.Session,
                    sessionContainer,
                    partitionKeyRangeCache: new Mock<PartitionKeyRangeCache>(null, null, null, null).Object,
                    clientCollectionCache: new Mock<ClientCollectionCache>(sessionContainer, gatewayStoreModel, null, null, null).Object,
                    globalEndpointManager: globalEndpointManager.Object);

                Assert.AreEqual($"{childPKRangeId}:{parentSession}", documentServiceRequestToChild.Headers[HttpConstants.HttpHeaders.SessionToken]);
            });
        }

        /// <summary>
        /// Simulating partition merge and cache having only the parents tokens
        /// </summary>
        [TestMethod]
        public async Task GatewayStoreModel_ObtainsSessionFromParents_AfterMerge()
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            string collectionResourceId = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionFullname = "dbs/db1/colls/collName";

            // Set tokens for the parents
            string parentPKRangeId = "0";
            int maxGlobalLsn = 100;
            int maxLsnRegion1 = 200;
            int maxLsnRegion2 = 300;
            int maxLsnRegion3 = 400;

            // Generate 2 tokens, one has max global but lower regional, the other lower global but higher regional
            // Expect the merge to contain all the maxes
            string parentSession = $"1#{maxGlobalLsn}#1={maxLsnRegion1 - 1}#2={maxLsnRegion2}#3={maxLsnRegion3 - 1}";
            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, $"{parentPKRangeId}:{parentSession}" } }
            );

            string parent2PKRangeId = "1";
            string parent2Session = $"1#{maxGlobalLsn - 1}#1={maxLsnRegion1}#2={maxLsnRegion2 - 1}#3={maxLsnRegion3}";
            sessionContainer.SetSessionToken(
                collectionResourceId,
                collectionFullname,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, $"{parent2PKRangeId}:{parent2Session}" } }
            );

            string tokenWithAllMax = $"1#{maxGlobalLsn}#1={maxLsnRegion1}#2={maxLsnRegion2}#3={maxLsnRegion3}";

            // Create the request for the child
            // Request for a child from both parents
            string childPKRangeId = "2";

            DocumentServiceRequest documentServiceRequestToChild = DocumentServiceRequest.CreateFromName(OperationType.Read, "dbs/db1/colls/collName/docs/42", ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, null);

            documentServiceRequestToChild.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange()
            {
                Id = childPKRangeId,
                MinInclusive = "",
                MaxExclusive = "FF",
                Parents = new Collection<string>() { parentPKRangeId, parent2PKRangeId } // PartitionKeyRange says who are the parents
            };

            Mock<IGlobalEndpointManager> globalEndpointManager = new Mock<IGlobalEndpointManager>();
            await this.GetGatewayStoreModelForConsistencyTest(async (gatewayStoreModel) =>
            {
                await GatewayStoreModel.ApplySessionTokenAsync(
                    documentServiceRequestToChild,
                    ConsistencyLevel.Session,
                    sessionContainer,
                    partitionKeyRangeCache: new Mock<PartitionKeyRangeCache>(null, null, null, null).Object,
                    clientCollectionCache: new Mock<ClientCollectionCache>(sessionContainer, gatewayStoreModel, null, null, null).Object,
                    globalEndpointManager: globalEndpointManager.Object);

                Assert.AreEqual($"{childPKRangeId}:{tokenWithAllMax}", documentServiceRequestToChild.Headers[HttpConstants.HttpHeaders.SessionToken]);
            });
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

        private async Task GetGatewayStoreModelForConsistencyTest(
            Func<GatewayStoreModel, Task> executeWithGatewayStoreModel)
        {
            static async Task<HttpResponseMessage> messageHandler(HttpRequestMessage request)
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
                    Assert.IsFalse(request.Headers.TryGetValues("x-ms-session-token", out IEnumerable<string> enumerable));
                }
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Response") };
            }

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));
            mockDocumentClient.Setup(client => client.ConsistencyLevel).Returns(Documents.ConsistencyLevel.Session);

            using GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());

            SessionContainer sessionContainer = new SessionContainer(string.Empty);
            sessionContainer.SetSessionToken(
                    ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString(),
                    "dbs/db1/colls/coll1",
                    new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, "range_0:1#9#4=8#5=7" } });

            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler httpMessageHandler = new MockMessageHandler(messageHandler);

            using GatewayStoreModel storeModel = new GatewayStoreModel(
                endpointManager,
                sessionContainer,
                ConsistencyLevel.Eventual,
                eventSource,
                null,
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(httpMessageHandler)));

            ClientCollectionCache clientCollectionCache = new Mock<ClientCollectionCache>(new SessionContainer("testhost"), storeModel, null, null, null).Object;
            PartitionKeyRangeCache partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(null, storeModel, clientCollectionCache, endpointManager).Object;
            storeModel.SetCaches(partitionKeyRangeCache, clientCollectionCache);

            await executeWithGatewayStoreModel(storeModel);
        }

        private async Task TestGatewayStoreModelProcessMessageAsync(GatewayStoreModel storeModel, DocumentServiceRequest request)
        {
            using (new ActivityScope(Guid.NewGuid()))
            {
                request.Headers["x-ms-session-token"] = "range_0:1#9#4=8#5=7";
                await storeModel.ProcessMessageAsync(request);
                request.Headers.Remove("x-ms-session-token");
                request.Headers["x-ms-consistency-level"] = "Session";
                request.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange { Id = "range_0" };
                await storeModel.ProcessMessageAsync(request);
            }
        }
    }
}