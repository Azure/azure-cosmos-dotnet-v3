// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.DistributedTransaction
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Moq.Protected;
    using Newtonsoft.Json.Linq;
    using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

    [TestClass]
    public class DistributedTransactionCommitterTests
    {
        private const string DatabaseName = "testdb";
        private const string ContainerName = "testcontainer";

        private static readonly string CollectionResourceId =
            ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();

        // Known-valid collection resource ID that passes ResourceId.Parse.
        private const string TestCollectionResourceId = "ccZ1ANCszwk=";

        [DataTestMethod]
        [DataRow(false, ConnectionMode.Gateway)]
        [DataRow(true, ConnectionMode.Gateway)]
        [DataRow(false, ConnectionMode.Direct)]
        [DataRow(true, ConnectionMode.Direct)]
        public async Task ExecuteTransactionAsync_RecreatedContainer_RealPipelineRefreshesMetadataAndSessions(
            bool readOnly, ConnectionMode connectionMode)
        {
            const string unchangedContainerName = "unchanged";
            const string sessionToken = "0:1#9#4=8#5=7";
            string currentRid = CollectionResourceId;
            string replacementRid = ResourceId.NewDocumentCollectionId(42, 130).DocumentCollectionId.ToString();
            string unchangedRid = ResourceId.NewDocumentCollectionId(42, 131).DocumentCollectionId.ToString();
            Mock<IHttpHandler> http = new Mock<IHttpHandler>(MockBehavior.Strict);
            Uri endpoint = MockSetupsHelper.SetupSingleRegionAccount(
                "dtx-recreation", Cosmos.ConsistencyLevel.Session, http, out string regionEndpoint);
            MockSetupsHelper.SetupContainerProperties(http, regionEndpoint, DatabaseName, ContainerName, currentRid);
            MockSetupsHelper.SetupContainerProperties(http, regionEndpoint, DatabaseName, unchangedContainerName, unchangedRid);
            List<JObject> payloads = new List<JObject>();
            List<Guid> tokens = new List<Guid>();
            http.Setup(h => h.SendAsync(
                    It.Is<HttpRequestMessage>(request => request.RequestUri.AbsolutePath == "/operations/dtc"),
                    It.IsAny<CancellationToken>()))
                .Returns<HttpRequestMessage, CancellationToken>(async (request, _) =>
                {
                    JObject payload = JObject.Parse(await request.Content.ReadAsStringAsync());
                    payloads.Add(payload);
                    tokens.Add(Guid.Parse(request.Headers.GetValues(HttpConstants.HttpHeaders.IdempotencyToken).Single()));
                    if ((string)payload[DistributedTransactionSerializer.Operations][0]
                        [DistributedTransactionSerializer.CollectionResourceId] != currentRid)
                    {
                        HttpResponseMessage stale = new HttpResponseMessage(HttpStatusCode.Gone)
                        {
                            Content = new StringContent(
                                "{\"isRetriable\":true,\"diagnosticString\":\"original stale participant\"}",
                                Encoding.UTF8, "application/json")
                        };
                        stale.Headers.Add(WFConstants.BackendHeaders.SubStatus, "1000");
                        return stale;
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(BuildDtcResponseJson(Enumerable.Repeat(
                            (statusCode: readOnly ? 200 : 201, subStatusCode: (int?)null,
                            sessionToken: "1#9#4=8#5=7", partitionKeyRangeId: "0"), 2).ToArray()),
                            Encoding.UTF8, "application/json")
                    };
                });
            using HttpClient transport = new HttpClient(new HttpHandlerHelper(http.Object));
            using CosmosClient client = new CosmosClient(endpoint.ToString(), MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey,
                new CosmosClientOptions
                {
                    ConnectionMode = connectionMode,
                    ConsistencyLevel = Cosmos.ConsistencyLevel.Session,
                    HttpClientFactory = () => transport,
                    MaxRetryAttemptsOnAbortedTransactions = 0
                });
            Container container = client.GetContainer(DatabaseName, ContainerName);
            Container unchanged = client.GetContainer(DatabaseName, unchangedContainerName);
            string containerPath = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);
            string unchangedPath = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, unchangedContainerName);
            int execution = 0;
            Task<DistributedTransactionResponse> ExecuteAsync()
            {
                string id = "item-" + execution++;
                return readOnly
                    ? client.CreateDistributedReadTransaction()
                        .ReadItem(container, new PartitionKey("pk"), id)
                        .ReadItem(unchanged, new PartitionKey("pk"), id)
                        .ExecuteTransactionAsync(CancellationToken.None)
                    : client.CreateDistributedWriteTransaction()
                        .CreateItem(container, new PartitionKey("pk"), id, new { id, pk = "pk" })
                        .CreateItem(unchanged, new PartitionKey("pk"), id, new { id, pk = "pk" })
                        .ExecuteTransactionAsync(CancellationToken.None);
            }

            using (DistributedTransactionResponse warm = await ExecuteAsync())
            {
                Assert.IsTrue(warm.IsSuccessStatusCode);
            }

            Assert.IsInstanceOfType(client.DocumentClient.sessionContainer, typeof(SessionContainer));
            SessionContainer sessions = (SessionContainer)client.DocumentClient.sessionContainer;
            Assert.AreEqual(sessionToken, sessions.GetSessionToken(containerPath));
            Assert.AreEqual(sessionToken, sessions.GetSessionToken(unchangedPath));
            currentRid = replacementRid;
            MockSetupsHelper.SetupContainerProperties(http, regionEndpoint, DatabaseName, ContainerName, replacementRid);
            using (DistributedTransactionResponse stale = await ExecuteAsync())
            {
                Assert.AreEqual(HttpStatusCode.Gone, stale.StatusCode);
                Assert.AreEqual(SubStatusCodes.NameCacheIsStale, stale.SubStatusCode);
                Assert.AreEqual("original stale participant", stale.DiagnosticString);
                Assert.IsTrue(stale.IsRetriable);
                Assert.AreEqual(2, payloads.Count, "A changed-RID transaction must not be resubmitted.");
                Assert.AreEqual(tokens[1], stale.IdempotencyToken);
                Assert.IsNotNull(stale.Diagnostics);
            }

            Assert.AreEqual(string.Empty, sessions.GetSessionToken(containerPath));
            Assert.AreEqual(sessionToken, sessions.GetSessionToken(unchangedPath));
            using (DistributedTransactionResponse subsequent = await ExecuteAsync())
            {
                Assert.IsTrue(subsequent.IsSuccessStatusCode);
            }

            Assert.AreEqual(3, payloads.Count);
            Assert.AreEqual(CollectionResourceId, (string)payloads[1][DistributedTransactionSerializer.Operations][0]
                [DistributedTransactionSerializer.CollectionResourceId]);
            Assert.AreEqual(replacementRid, (string)payloads[2][DistributedTransactionSerializer.Operations][0]
                [DistributedTransactionSerializer.CollectionResourceId]);
            Assert.AreEqual(unchangedRid, (string)payloads[2][DistributedTransactionSerializer.Operations][1]
                [DistributedTransactionSerializer.CollectionResourceId]);
            Assert.AreNotEqual(tokens[1], tokens[2]);
            Assert.AreEqual(sessionToken, sessions.GetSessionToken(containerPath));
            foreach (string name in new[] { ContainerName, unchangedContainerName })
            {
                Uri metadataUri = new Uri($"{regionEndpoint}/dbs/{DatabaseName}/colls/{name}");
                http.Verify(h => h.SendAsync(It.Is<HttpRequestMessage>(request => request.RequestUri == metadataUri),
                    It.IsAny<CancellationToken>()), Times.Exactly(2));
            }
        }

        [DataTestMethod]
        [DataRow(false, false, false, false, false, false)]
        [DataRow(true, false, false, false, false, false)]
        [DataRow(false, false, false, true, false, false)]
        [DataRow(true, false, false, true, false, false)]
        [DataRow(false, true, false, true, false, false)]
        [DataRow(true, true, false, true, false, false)]
        [DataRow(false, true, false, true, false, true)]
        [DataRow(true, true, false, true, false, true)]
        [DataRow(false, false, false, false, true, false)]
        [DataRow(true, false, false, false, true, false)]
        [DataRow(false, false, false, true, true, false)]
        [DataRow(true, false, false, true, true, false)]
        [DataRow(false, true, false, true, true, false)]
        [DataRow(true, true, false, true, true, false)]
        [DataRow(false, true, false, true, true, true)]
        [DataRow(true, true, false, true, true, true)]
        [DataRow(false, false, true, true, true, false)]
        [DataRow(true, false, true, true, true, false)]
        [DataRow(false, true, true, true, true, false)]
        [DataRow(true, true, true, true, true, false)]
        [DataRow(false, true, true, true, true, true)]
        [DataRow(true, true, true, true, true, true)]
        public async Task ExecuteTransactionAsync_NameCacheIsStale_RetriesOnlyWithUnchangedRids(
            bool readOnly,
            bool perOperation,
            bool remainsStale,
            bool hasBody,
            bool unchangedRids,
            bool durablyAborted)
        {
            const string sessionToken = "0:1#9#4=8#5=7";
            Mock<CosmosClientContext> context = this.CreateMockClientContext();
            SessionContainer sessions = new SessionContainer("testhost");
            Mock<ISessionContainer> sessionContainer = new Mock<ISessionContainer>();
            sessionContainer.Setup(s => s.ClearTokenByResourceId(It.IsAny<string>()))
                .Callback<string>(sessions.ClearTokenByResourceId);
            using MockDocumentClient documentClient = CreateCollectionCacheDocumentClient(sessionContainer.Object);
            context.Setup(c => c.DocumentClient).Returns(documentClient);
            Mock<ClientCollectionCache> cache = Mock.Get(await documentClient.GetCollectionCacheAsync(NoOpTrace.Singleton));
            string refreshedRid = unchangedRids ? TestCollectionResourceId
                : ResourceId.NewDocumentCollectionId(43, 130).DocumentCollectionId.ToString();
            string secondRid = ResourceId.NewDocumentCollectionId(44, 131).DocumentCollectionId.ToString();
            context.Setup(c => c.GetCachedContainerPropertiesAsync(
                    DistributedTransactionConstants.GetCollectionFullName("testDb", "secondContainer"),
                    It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ContainerProperties.CreateWithResourceId(secondRid));
            SetupCollectionRefresh(cache, (path, _) => Task.FromResult(ContainerProperties.CreateWithResourceId(
                path.EndsWith("/secondContainer", StringComparison.Ordinal) ? secondRid : refreshedRid)));

            List<DistributedTransactionOperation> operations = CreateTestOperations(3).ToList();
            DistributedTransactionRequestOptions options = new DistributedTransactionRequestOptions { IfMatchEtag = "original" };
            operations[0] = new DistributedTransactionOperation<object>(
                OperationType.Create, 0, "testDb", "testContainer", PartitionKey.Null,
                "doc1", new { id = "doc1", value = "original" }, options);
            operations[2].Container = "secondContainer";
            if (readOnly)
            {
                operations.ForEach(op => op.OperationType = OperationType.Read);
            }

            foreach (string container in new[] { "testContainer", "secondContainer" })
            {
                sessions.SetSessionToken(
                    container == "secondContainer" ? secondRid : TestCollectionResourceId,
                    DistributedTransactionConstants.GetCollectionFullName("testDb", container),
                    new RequestNameValueCollection { [HttpConstants.HttpHeaders.SessionToken] = sessionToken });
            }

            List<JObject> payloads = new List<JObject>();
            List<Guid> tokens = new List<Guid>();
            List<(bool IsRetry, bool IsCrossRegionRedirect)> dispatchSignals = new List<(bool, bool)>();
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                context,
                (stream, enrich) =>
                {
                    using StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                    payloads.Add(JObject.Parse(reader.ReadToEnd()));
                    using RequestMessage request = new RequestMessage(HttpMethod.Post, new Uri("https://localhost/operations/dtc"));
                    enrich(request);
                    tokens.Add(Guid.Parse(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]));
                    if (request.DistributedTransactionDispatchTracker != null)
                    {
                        dispatchSignals.Add(request.DistributedTransactionDispatchTracker.RecordDispatch("East US"));
                    }
                    options.IfMatchEtag = "changed-after-dispatch";
                },
                () =>
                {
                    if (payloads.Count > 1 && !remainsStale)
                    {
                        return Task.FromResult(CreateSuccessResponseMessage(3));
                    }

                    ResponseMessage response = new ResponseMessage(durablyAborted
                        ? (HttpStatusCode)StatusCodes.TransactionAborted
                        : perOperation ? (HttpStatusCode)207 : HttpStatusCode.Gone);
                    if (perOperation)
                    {
                        response.Content = new MemoryStream(Encoding.UTF8.GetBytes(
                            "{\"isRetriable\":true,\"diagnosticString\":\"original failure\",\"operationResponses\":[" +
                            "{\"index\":0,\"statusCode\":400}," +
                            "{\"index\":1,\"statusCode\":410,\"subStatusCode\":1000}," +
                            "{\"index\":2,\"statusCode\":424}]}"));
                    }
                    else
                    {
                        response.Headers.SubStatusCode = SubStatusCodes.NameCacheIsStale;
                        if (hasBody)
                        {
                            response.Content = new MemoryStream(Encoding.UTF8.GetBytes(
                                "{\"isRetriable\":true,\"diagnosticString\":\"original failure\"}"));
                        }
                    }

                    return Task.FromResult(response);
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations,
                context.Object,
                readOnly ? OperationType.Read : OperationType.CommitDistributedTransaction,
                TimeSpan.Zero,
                maxIsRetriableRetryCount: 2);
            using DistributedTransactionResponse result = await committer.ExecuteTransactionAsync(
                NoOpTrace.Singleton, CancellationToken.None);

            bool canRetry = unchangedRids && hasBody;
            int expectedRequests = canRetry ? (remainsStale ? 3 : 2) : 1;
            bool failed = !canRetry || remainsStale;
            HttpStatusCode failureStatus = durablyAborted ? (HttpStatusCode)StatusCodes.TransactionAborted
                : perOperation ? HttpStatusCode.BadRequest : HttpStatusCode.Gone;
            Assert.AreEqual(failed ? failureStatus : HttpStatusCode.OK, result.StatusCode);
            Assert.AreEqual(expectedRequests, payloads.Count);
            Assert.IsNotNull(result.Diagnostics);
            Assert.AreEqual(tokens.Last(), result.IdempotencyToken);
            if (failed)
            {
                Assert.AreEqual(hasBody ? "original failure" : null, result.DiagnosticString);
                Assert.AreEqual(perOperation ? SubStatusCodes.Unknown : SubStatusCodes.NameCacheIsStale,
                    result.SubStatusCode);
                if (perOperation)
                {
                    Assert.AreEqual(HttpStatusCode.Gone, result[1].StatusCode);
                    Assert.AreEqual(SubStatusCodes.NameCacheIsStale, result[1].SubStatusCode);
                }
            }

            for (int i = 1; i < tokens.Count; i++)
            {
                Assert.AreEqual(!durablyAborted, tokens[i - 1] == tokens[i]);
            }

            if (!readOnly)
            {
                Assert.AreEqual((false, false), dispatchSignals[0]);
                for (int i = 1; i < dispatchSignals.Count; i++)
                {
                    Assert.AreEqual((!durablyAborted, false), dispatchSignals[i]);
                }
            }

            for (int i = 0; i < operations.Count; i++)
            {
                string expectedRid = i == 2 ? secondRid : TestCollectionResourceId;
                Assert.AreEqual(expectedRid, operations[i].CollectionResourceId);
                Assert.AreEqual(ResourceId.Parse(expectedRid).DatabaseId.ToString(),
                    operations[i].DatabaseResourceId);
            }

            Assert.IsTrue(payloads.All(payload => JToken.DeepEquals(payloads[0], payload)),
                "Refreshing metadata must not change the submitted body.");
            sessionContainer.Verify(s => s.ClearTokenByResourceId(TestCollectionResourceId),
                unchangedRids ? Times.Never() : Times.Once());
            sessionContainer.Verify(s => s.ClearTokenByResourceId(It.Is<string>(rid => rid != TestCollectionResourceId)),
                Times.Never());
            sessionContainer.Verify(s => s.ClearTokenByCollectionFullname(It.IsAny<string>()), Times.Never());
            Assert.AreEqual(unchangedRids ? sessionToken : string.Empty,
                sessions.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName("testDb", "testContainer")));
            Assert.AreEqual(sessionToken,
                sessions.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName("testDb", "secondContainer")));
            foreach (string container in new[] { "testContainer", "secondContainer" })
            {
                string path = DistributedTransactionConstants.GetCollectionFullName("testDb", container);
                cache.Verify(c => c.RefreshCollectionAsync(
                    HttpConstants.Versions.CurrentVersion, path, It.IsAny<string>(), It.IsAny<ITrace>(),
                    It.IsAny<IClientSideRequestStatistics>(), It.IsAny<CancellationToken>()),
                    Times.Once());
            }
        }

        [DataTestMethod]
        [DataRow(410, 0)]
        [DataRow(410, 1002)]
        [DataRow(404, 1003)]
        [DataRow(400, 1000)]
        public async Task ExecuteTransactionAsync_OtherRoutingErrors_DoNotRefresh(int statusCode, int subStatusCode)
        {
            Mock<CosmosClientContext> context = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(context, () =>
            {
                ResponseMessage response = new ResponseMessage((HttpStatusCode)statusCode);
                response.Headers.SubStatusCode = (SubStatusCodes)subStatusCode;
                return Task.FromResult(response);
            });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(), context.Object, OperationType.CommitDistributedTransaction);
            using DistributedTransactionResponse result = await committer.ExecuteTransactionAsync(
                NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual((HttpStatusCode)statusCode, result.StatusCode);
            Assert.AreEqual((SubStatusCodes)subStatusCode, result.SubStatusCode);
            this.VerifyProcessResourceOperationCallCount(context, Times.Once());
            context.Verify(c => c.DocumentClient, Times.Once(), "Only session-token bookkeeping accesses the client.");
        }

        [DataTestMethod]
        [DataRow(false, 0, true, false)]
        [DataRow(true, 0, true, false)]
        [DataRow(false, 1, true, false)]
        [DataRow(true, 1, true, false)]
        [DataRow(false, 2, true, false)]
        [DataRow(true, 2, true, false)]
        [DataRow(false, 0, false, false)]
        [DataRow(true, 0, false, false)]
        [DataRow(false, 0, true, true)]
        [DataRow(true, 0, true, true)]
        [DataRow(false, 0, false, true)]
        [DataRow(true, 0, false, true)]
        [DataRow(false, 1, true, true)]
        [DataRow(true, 1, true, true)]
        [DataRow(false, 2, true, true)]
        [DataRow(true, 2, true, true)]
        public async Task ExecuteTransactionAsync_EarlierRetries_ChangedRidsAlwaysStop(
            bool readOnly, int failureKind, bool changedRid, bool durablyAborted)
        {
            Mock<CosmosClientContext> context = this.CreateMockClientContext();
            using MockDocumentClient documentClient = CreateCollectionCacheDocumentClient();
            using CosmosClient client = new CosmosClient(
                "https://localhost", MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey,
                new CosmosClientOptions(), documentClient);
            context.Setup(c => c.DocumentClient).Returns(documentClient);
            string refreshedRid = changedRid
                ? ResourceId.NewDocumentCollectionId(43, 130).DocumentCollectionId.ToString()
                : TestCollectionResourceId;
            Mock<ClientCollectionCache> cache = Mock.Get(await documentClient.GetCollectionCacheAsync(NoOpTrace.Singleton));
            SetupCollectionRefresh(cache, (path, _) =>
            {
                ContainerProperties properties = ContainerProperties.CreateWithResourceId(
                    path.EndsWith("/unchangedContainer", StringComparison.Ordinal)
                        ? TestCollectionResourceId : refreshedRid);
                context.Setup(c => c.GetCachedContainerPropertiesAsync(
                        path, It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(properties);
                return Task.FromResult(properties);
            });

            List<Guid> tokens = new List<Guid>();
            RetryHandler handler = new RetryHandler(client)
            {
                InnerHandler = new TestHandler((request, _) =>
                {
                    request.ToDocumentServiceRequest();
                    tokens.Add(Guid.Parse(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]));
                    Assert.AreEqual(readOnly, request.DistributedTransactionDispatchTracker == null);
                    int dispatch = tokens.Count;
                    if (dispatch == 1)
                    {
                        if (failureKind == 2)
                        {
                            throw new HttpRequestException("Response lost after dispatch.");
                        }

                        return Task.FromResult(failureKind == 1
                            ? new ResponseMessage(HttpStatusCode.RequestTimeout)
                            : CreateRetriableNonAbortedResponseMessage());
                    }

                    if (dispatch == 2)
                    {
                        ResponseMessage stale = new ResponseMessage(durablyAborted
                            ? (HttpStatusCode)StatusCodes.TransactionAborted : HttpStatusCode.Gone);
                        stale.Headers.SubStatusCode = SubStatusCodes.NameCacheIsStale;
                        if (durablyAborted)
                        {
                            stale.Content = new MemoryStream(Encoding.UTF8.GetBytes(
                                "{\"isRetriable\":true,\"operationResponses\":[{\"index\":0,\"statusCode\":410,\"subStatusCode\":1000}," +
                                "{\"index\":1,\"statusCode\":424}]}"));
                        }
                        else
                        {
                            stale.Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"isRetriable\":true}"));
                        }

                        return Task.FromResult(stale);
                    }

                    return Task.FromResult(CreateSuccessResponseMessage(2));
                })
            };
            RequestMessage pending = null;
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                context,
                (stream, enrich) =>
                {
                    pending = new RequestMessage(HttpMethod.Post, new Uri("https://localhost/operations/dtc"))
                    {
                        Content = stream,
                        ResourceType = ResourceType.DistributedTransactionBatch,
                        OperationType = readOnly ? OperationType.Read : OperationType.CommitDistributedTransaction,
                        Trace = NoOpTrace.Singleton
                    };
                    enrich(pending);
                },
                async () =>
                {
                    using (pending)
                    {
                        return await handler.SendAsync(pending, CancellationToken.None);
                    }
                });
            List<DistributedTransactionOperation> operations = CreateTestOperations(2).ToList();
            operations[1].Container = "unchangedContainer";
            if (readOnly)
            {
                operations.ForEach(op => op.OperationType = OperationType.Read);
            }

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, context.Object,
                readOnly ? OperationType.Read : OperationType.CommitDistributedTransaction, TimeSpan.Zero);
            using DistributedTransactionResponse result = await committer.ExecuteTransactionAsync(
                NoOpTrace.Singleton, CancellationToken.None);
            bool shouldStop = changedRid;
            Assert.AreEqual(shouldStop
                ? (durablyAborted ? (HttpStatusCode)StatusCodes.TransactionAborted : HttpStatusCode.Gone)
                : HttpStatusCode.OK, result.StatusCode);
            Assert.AreEqual(shouldStop ? 2 : 3, tokens.Count);
            Assert.AreEqual(tokens[0], tokens[1]);
            Assert.AreEqual(TestCollectionResourceId, operations[0].CollectionResourceId);
            if (shouldStop)
            {
                Assert.AreEqual(tokens[0], result.IdempotencyToken);
                Assert.IsNotNull(result.Diagnostics);
                DistributedTransactionCommitter subsequent = new DistributedTransactionCommitter(
                    operations, context.Object,
                    readOnly ? OperationType.Read : OperationType.CommitDistributedTransaction);
                using DistributedTransactionResponse recovered = await subsequent.ExecuteTransactionAsync(
                    NoOpTrace.Singleton, CancellationToken.None);
                Assert.AreEqual(HttpStatusCode.OK, recovered.StatusCode);
                Assert.AreEqual(refreshedRid, operations[0].CollectionResourceId);
                Assert.AreNotEqual(tokens[0], tokens[2]);
            }
            else
            {
                Assert.AreEqual(!durablyAborted, tokens[0] == tokens[2]);
            }
        }

        [DataTestMethod]
        [DataRow(true, 408, 0, true)]
        [DataRow(true, 408, 1, true)]
        [DataRow(true, 408, 2, true)]
        [DataRow(true, 408, 0, false)]
        [DataRow(false, 408, 0, true)]
        [DataRow(false, 408, 1, true)]
        [DataRow(false, 408, 2, true)]
        [DataRow(true, 503, 0, true)]
        [DataRow(true, 503, 1, true)]
        [DataRow(true, 503, 2, true)]
        public async Task ExecuteTransactionAsync_TerminalFailure_DoesNotRefreshMetadata(
            bool readOnly, int statusCode, int stopKind, bool hasBody)
        {
            Mock<CosmosClientContext> context = this.CreateMockClientContext();
            Mock<ISessionContainer> sessions = new Mock<ISessionContainer>();
            using MockDocumentClient documentClient = CreateCollectionCacheDocumentClient(sessions.Object);
            context.Setup(c => c.DocumentClient).Returns(documentClient);
            Mock<ClientCollectionCache> cache = Mock.Get(await documentClient.GetCollectionCacheAsync(NoOpTrace.Singleton));
            SetupCollectionRefresh(cache, (_, _) => Task.FromResult(ContainerProperties.CreateWithResourceId(
                ResourceId.NewDocumentCollectionId(43, 130).DocumentCollectionId.ToString())));

            List<DistributedTransactionOperation> operations = CreateTestOperations(3).ToList();
            operations[2].Container = "secondContainer";
            if (readOnly)
            {
                operations.ForEach(operation => operation.OperationType = OperationType.Read);
            }

            List<Guid> tokens = new List<Guid>();
            List<JObject> payloads = new List<JObject>();
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                context,
                (stream, enrich) =>
                {
                    using StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                    payloads.Add(JObject.Parse(reader.ReadToEnd()));
                    using RequestMessage request = new RequestMessage(HttpMethod.Post, new Uri("https://localhost/operations/dtc"));
                    enrich(request);
                    tokens.Add(Guid.Parse(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]));
                },
                () =>
                {
                    ResponseMessage response = new ResponseMessage((HttpStatusCode)statusCode)
                    {
                        Content = hasBody ? new MemoryStream(Encoding.UTF8.GetBytes(
                        "{\"isRetriable\":" + (stopKind != 0 ? "true" : "false") +
                        ",\"diagnosticString\":\"original failure\",\"operationResponses\":[" +
                        "{\"index\":0,\"statusCode\":" + statusCode + "}," +
                        "{\"index\":1,\"statusCode\":" + statusCode + "}," +
                        "{\"index\":2,\"statusCode\":" + statusCode + "}]}")) : null
                    };
                    return Task.FromResult(response);
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, context.Object,
                readOnly ? OperationType.Read : OperationType.CommitDistributedTransaction,
                retryBaseDelay: stopKind == 2 ? TimeSpan.FromSeconds(1) : TimeSpan.Zero,
                maxCumulativeRetryDelay: stopKind == 2 ? TimeSpan.Zero : TimeSpan.FromMinutes(1),
                maxIsRetriableRetryCount: 1);
            using DistributedTransactionResponse result = await committer.ExecuteTransactionAsync(
                NoOpTrace.Singleton, CancellationToken.None);

            int expectedRequests = stopKind == 1 ? 2 : 1;
            Assert.AreEqual(expectedRequests, payloads.Count);
            Assert.AreEqual((HttpStatusCode)statusCode, result.StatusCode);
            if (hasBody)
            {
                Assert.AreEqual("original failure", result.DiagnosticString);
            }

            Assert.AreEqual(3, result.Count);
            Assert.IsTrue(result.All(operation => operation.StatusCode == (HttpStatusCode)statusCode));
            Assert.IsNotNull(result.Diagnostics);
            Assert.IsTrue(tokens.All(token => token == result.IdempotencyToken));
            Assert.IsTrue(payloads.All(payload => JToken.DeepEquals(payloads[0], payload)));
            Assert.IsTrue(operations.All(operation => operation.CollectionResourceId == TestCollectionResourceId));
            Assert.IsTrue(operations.All(operation =>
                operation.DatabaseResourceId == ResourceId.Parse(TestCollectionResourceId).DatabaseId.ToString()));

            foreach (string container in new[] { "testContainer", "secondContainer" })
            {
                string path = DistributedTransactionConstants.GetCollectionFullName("testDb", container);
                context.Verify(c => c.GetCachedContainerPropertiesAsync(
                    path, It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Once());
                cache.Verify(c => c.RefreshCollectionAsync(
                    HttpConstants.Versions.CurrentVersion, path, It.IsAny<string>(), It.IsAny<ITrace>(),
                    It.IsAny<IClientSideRequestStatistics>(), It.IsAny<CancellationToken>()), Times.Never());
            }

            sessions.Verify(s => s.ClearTokenByResourceId(It.IsAny<string>()), Times.Never());
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ExecuteTransactionAsync_TerminalReadTimeout_DoesNotRepeatAnEarlierMetadataRefresh(bool changedRids)
        {
            Mock<CosmosClientContext> context = this.CreateMockClientContext();
            using MockDocumentClient documentClient = CreateCollectionCacheDocumentClient();
            context.Setup(c => c.DocumentClient).Returns(documentClient);
            Mock<ClientCollectionCache> cache = Mock.Get(await documentClient.GetCollectionCacheAsync(NoOpTrace.Singleton));
            string refreshedRid = changedRids
                ? ResourceId.NewDocumentCollectionId(43, 130).DocumentCollectionId.ToString()
                : TestCollectionResourceId;
            SetupCollectionRefresh(cache, (_, _) => Task.FromResult(ContainerProperties.CreateWithResourceId(refreshedRid)));

            int requests = 0;
            this.SetupProcessResourceOperation(context, () =>
            {
                if (++requests == 1)
                {
                    ResponseMessage stale = new ResponseMessage(HttpStatusCode.Gone);
                    stale.Headers.SubStatusCode = SubStatusCodes.NameCacheIsStale;
                    stale.Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"isRetriable\":true}"));
                    return Task.FromResult(stale);
                }

                return Task.FromResult(CreateResponseMessageWithDiagnosticString(
                    HttpStatusCode.RequestTimeout, 1, "original timeout"));
            });
            List<DistributedTransactionOperation> operations = CreateTestOperations().ToList();
            operations[0].OperationType = OperationType.Read;
            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, context.Object, OperationType.Read, TimeSpan.Zero);
            using DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(
                NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(changedRids ? HttpStatusCode.Gone : HttpStatusCode.RequestTimeout, response.StatusCode);
            Assert.AreEqual(changedRids ? 1 : 2, requests);
            Assert.AreEqual(TestCollectionResourceId, operations[0].CollectionResourceId);
            cache.Verify(c => c.RefreshCollectionAsync(
                HttpConstants.Versions.CurrentVersion, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(),
                It.IsAny<IClientSideRequestStatistics>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [DataTestMethod]
        [DataRow(false, 0)]
        [DataRow(true, 0)]
        [DataRow(false, 1)]
        [DataRow(true, 1)]
        [DataRow(false, 2)]
        [DataRow(true, 2)]
        [DataRow(false, 3)]
        [DataRow(true, 3)]
        public async Task ExecuteTransactionAsync_MetadataRefreshFailure_DoesNotResubmit(bool readOnly, int failureKind)
        {
            Mock<CosmosClientContext> context = this.CreateMockClientContext();
            using MockDocumentClient documentClient = CreateCollectionCacheDocumentClient();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            context.Setup(c => c.DocumentClient).Returns(documentClient);
            Mock<ClientCollectionCache> cache = Mock.Get(await documentClient.GetCollectionCacheAsync(NoOpTrace.Singleton));
            InvalidOperationException unexpected = new InvalidOperationException("Unexpected refresh failure.");
            SetupCollectionRefresh(cache, (_, token) =>
            {
                Assert.AreEqual(cancellation.Token, token);
                if (failureKind == 2)
                {
                    cancellation.Cancel();
                    return Task.FromCanceled<ContainerProperties>(cancellation.Token);
                }

                Exception failure = failureKind switch
                {
                    0 => new CosmosException("Container deleted", HttpStatusCode.NotFound, 1003, null, 0),
                    1 => new DocumentClientException(
                        "Container deleted", innerException: null,
                        statusCode: HttpStatusCode.NotFound, substatusCode: SubStatusCodes.OwnerResourceNotFound,
                        requestUri: null, responseHeaders: new DictionaryNameValueCollection()),
                    _ => unexpected
                };
                return Task.FromException<ContainerProperties>(failure);
            });
            this.SetupProcessResourceOperation(context, () =>
            {
                ResponseMessage response = new ResponseMessage(HttpStatusCode.Gone);
                response.Headers.SubStatusCode = SubStatusCodes.NameCacheIsStale;
                response.Content = new MemoryStream(Encoding.UTF8.GetBytes(
                    "{\"isRetriable\":true,\"diagnosticString\":\"original transaction failure\"}"));
                return Task.FromResult(response);
            });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(), context.Object,
                readOnly ? OperationType.Read : OperationType.CommitDistributedTransaction);
            List<string> warnings = new List<string>();
            using System.Diagnostics.TraceListener listener = new DelegatingTraceListener(
                (eventType, message) =>
                {
                    if (eventType == System.Diagnostics.TraceEventType.Warning)
                    {
                        warnings.Add(message);
                    }
                });
            System.Diagnostics.SourceLevels previousLevel = DefaultTrace.TraceSource.Switch.Level;
            DefaultTrace.TraceSource.Switch.Level = System.Diagnostics.SourceLevels.All;
            DefaultTrace.TraceSource.Listeners.Add(listener);
            try
            {
                if (failureKind == 2)
                {
                    await Assert.ThrowsExceptionAsync<TaskCanceledException>(() =>
                        committer.ExecuteTransactionAsync(NoOpTrace.Singleton, cancellation.Token));
                }
                else if (failureKind == 3)
                {
                    InvalidOperationException thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                        committer.ExecuteTransactionAsync(NoOpTrace.Singleton, cancellation.Token));
                    Assert.AreSame(unexpected, thrown);
                }
                else
                {
                    using DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(
                        NoOpTrace.Singleton, cancellation.Token);
                    Assert.AreEqual(HttpStatusCode.Gone, response.StatusCode);
                    Assert.AreEqual(SubStatusCodes.NameCacheIsStale, response.SubStatusCode);
                    Assert.AreEqual("original transaction failure", response.DiagnosticString);
                    Assert.IsTrue(response.IsRetriable);
                    Assert.IsNotNull(response.Diagnostics);
                    Assert.IsTrue(warnings.Any(message =>
                        message.Contains("metadata refresh failed") &&
                        message.Contains("SubStatusCode=1003") &&
                        message.Contains("Returning the original transaction failure")));
                }
            }
            finally
            {
                DefaultTrace.TraceSource.Listeners.Remove(listener);
                DefaultTrace.TraceSource.Switch.Level = previousLevel;
            }

            this.VerifyProcessResourceOperationCallCount(context, Times.Once());
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public async Task ExecuteTransactionAsync_RealCacheRefresh_PreservesSessionRecordedBeforeCleanup(
            bool readOnly, bool alreadyRefreshed)
        {
            const string token = "0:1#9#4=8#5=7";
            string path = DistributedTransactionConstants.GetCollectionFullName("testDb", "testContainer");
            string unrelatedPath = DistributedTransactionConstants.GetCollectionFullName("testDb", "unrelatedContainer");
            string replacementRid = ResourceId.NewDocumentCollectionId(43, 130).DocumentCollectionId.ToString();
            SessionContainer sessions = new SessionContainer("testhost");
            RequestNameValueCollection headers = new RequestNameValueCollection
            {
                [HttpConstants.HttpHeaders.SessionToken] = token
            };
            sessions.SetSessionToken(TestCollectionResourceId, path, headers);
            sessions.SetSessionToken(CollectionResourceId, unrelatedPath, headers);
            bool recordedReplacementSession = false;
            void RecordReplacementSession()
            {
                if (recordedReplacementSession)
                {
                    return;
                }

                // Both production refresh paths publish replacement metadata before invoking cleanup.
                sessions.SetSessionToken(replacementRid, path, headers);
                recordedReplacementSession = true;
            }

            Mock<ISessionContainer> sessionContainer = new Mock<ISessionContainer>();
            sessionContainer.Setup(s => s.ClearTokenByResourceId(It.IsAny<string>()))
                .Callback<string>(rid =>
                {
                    RecordReplacementSession();
                    sessions.ClearTokenByResourceId(rid);
                });
            sessionContainer.Setup(s => s.ClearTokenByCollectionFullname(It.IsAny<string>()))
                .Callback<string>(name =>
                {
                    RecordReplacementSession();
                    sessions.ClearTokenByCollectionFullname(name);
                });
            Mock<ClientCollectionCache> cache = new Mock<ClientCollectionCache>(
                sessionContainer.Object, new ServerStoreModel(null), null, null, null, true, null)
            {
                CallBase = true
            };
            cache.Setup(c => c.RefreshCollectionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(),
                    It.IsAny<IClientSideRequestStatistics>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, ITrace, IClientSideRequestStatistics, CancellationToken>(
                    (apiVersion, resourceAddress, submittedRid, _, _, cancellationToken) =>
                    {
                        Assert.AreEqual(HttpConstants.Versions.CurrentVersion, apiVersion);
                        Assert.AreEqual(TestCollectionResourceId, submittedRid);
                        Assert.AreEqual(path, resourceAddress);
                        Assert.AreEqual(CancellationToken.None, cancellationToken);
                    })
                .CallBase();
            string currentRid = TestCollectionResourceId;
            int metadataReads = 0;
            cache.Protected().Setup<Task<ContainerProperties>>(
                "GetByNameAsync", ItExpr.IsAny<string>(), ItExpr.IsAny<string>(),
                ItExpr.IsAny<ITrace>(), ItExpr.IsAny<IClientSideRequestStatistics>(),
                ItExpr.IsAny<CancellationToken>())
                .Returns(() =>
                {
                    metadataReads++;
                    return Task.FromResult(ContainerProperties.CreateWithResourceId(currentRid));
                });
            await cache.Object.ResolveByNameAsync(
                HttpConstants.Versions.CurrentVersion, path, false, NoOpTrace.Singleton, null, CancellationToken.None);
            currentRid = replacementRid;
            if (alreadyRefreshed)
            {
                cache.Object.Refresh(path, HttpConstants.Versions.CurrentVersion);
                await cache.Object.ResolveByNameAsync(
                    HttpConstants.Versions.CurrentVersion, path, false, NoOpTrace.Singleton, null, CancellationToken.None);
            }

            using MockDocumentClient client = new CollectionCacheDocumentClient(cache.Object);
            client.sessionContainer = sessionContainer.Object;
            Mock<CosmosClientContext> context = this.CreateMockClientContext();
            context.Setup(c => c.DocumentClient).Returns(client);
            this.SetupProcessResourceOperation(context, () =>
            {
                ResponseMessage stale = new ResponseMessage(HttpStatusCode.Gone);
                stale.Headers.SubStatusCode = SubStatusCodes.NameCacheIsStale;
                stale.Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"isRetriable\":true}"));
                return Task.FromResult(stale);
            });
            List<DistributedTransactionOperation> operations = CreateTestOperations(2).ToList();
            if (readOnly)
            {
                operations.ForEach(operation => operation.OperationType = OperationType.Read);
            }

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, context.Object,
                readOnly ? OperationType.Read : OperationType.CommitDistributedTransaction, TimeSpan.Zero);
            using DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(
                NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Gone, response.StatusCode);
            Assert.IsTrue(recordedReplacementSession);
            Assert.AreEqual(token, sessions.GetSessionToken(path), "Cleanup must preserve the replacement container's session.");
            Assert.AreEqual(token, sessions.GetSessionToken(unrelatedPath));
            Assert.AreEqual(2, metadataReads, "An already refreshed cache should not fetch metadata again.");
            Assert.IsTrue(operations.All(operation => operation.CollectionResourceId == TestCollectionResourceId));
            this.VerifyProcessResourceOperationCallCount(context, Times.Once());
            sessionContainer.Verify(s => s.ClearTokenByResourceId(TestCollectionResourceId), Times.Once());
            sessionContainer.Verify(s => s.ClearTokenByResourceId(It.Is<string>(rid => rid != TestCollectionResourceId)),
                Times.Never());
            sessionContainer.Verify(s => s.ClearTokenByCollectionFullname(It.IsAny<string>()), Times.Never());
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public async Task ExecuteTransactionAsync_SharedCacheAlreadyRefreshed_ClearsOnlyOldSubmittedSession(
            bool readOnly, bool replacementSessionExists)
        {
            const string token = "0:1#9#4=8#5=7";
            string refreshedRid = ResourceId.NewDocumentCollectionId(43, 130).DocumentCollectionId.ToString();
            string path = DistributedTransactionConstants.GetCollectionFullName("testDb", "testContainer");
            string unrelatedPath = DistributedTransactionConstants.GetCollectionFullName("testDb", "unrelatedContainer");
            SessionContainer sessions = new SessionContainer("testhost");
            RequestNameValueCollection headers = new RequestNameValueCollection
            {
                [HttpConstants.HttpHeaders.SessionToken] = token
            };
            sessions.SetSessionToken(TestCollectionResourceId, path, headers);
            sessions.SetSessionToken(CollectionResourceId, unrelatedPath, headers);
            Mock<ISessionContainer> sessionContainer = new Mock<ISessionContainer>();
            sessionContainer.Setup(s => s.ClearTokenByResourceId(It.IsAny<string>()))
                .Callback<string>(sessions.ClearTokenByResourceId);
            using MockDocumentClient documentClient = CreateCollectionCacheDocumentClient(sessionContainer.Object);
            Mock<CosmosClientContext> context = this.CreateMockClientContext();
            context.Setup(c => c.DocumentClient).Returns(documentClient);
            Mock<ClientCollectionCache> cache = Mock.Get(await documentClient.GetCollectionCacheAsync(NoOpTrace.Singleton));
            ContainerProperties replacement = ContainerProperties.CreateWithResourceId(refreshedRid);
            SetupCollectionRefresh(cache, (_, _) => Task.FromResult(replacement));
            await cache.Object.ResolveByNameAsync(
                HttpConstants.Versions.CurrentVersion, path, false, NoOpTrace.Singleton, null, CancellationToken.None);
            this.SetupProcessResourceOperation(context, () =>
            {
                context.Setup(c => c.GetCachedContainerPropertiesAsync(
                        path, It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(replacement);
                if (replacementSessionExists)
                {
                    sessions.SetSessionToken(refreshedRid, path, headers);
                }

                ResponseMessage stale = new ResponseMessage(HttpStatusCode.Gone);
                stale.Headers.SubStatusCode = SubStatusCodes.NameCacheIsStale;
                stale.Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"isRetriable\":true}"));
                return Task.FromResult(stale);
            });
            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(2), context.Object,
                readOnly ? OperationType.Read : OperationType.CommitDistributedTransaction, TimeSpan.Zero);
            using DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(
                NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Gone, response.StatusCode);
            this.VerifyProcessResourceOperationCallCount(context, Times.Once());
            sessionContainer.Verify(s => s.ClearTokenByResourceId(TestCollectionResourceId), Times.Once());
            sessionContainer.Verify(s => s.ClearTokenByResourceId(It.Is<string>(rid => rid != TestCollectionResourceId)),
                Times.Never());
            sessionContainer.Verify(s => s.ClearTokenByCollectionFullname(It.IsAny<string>()), Times.Never());
            Assert.AreEqual(replacementSessionExists ? token : string.Empty, sessions.GetSessionToken(path));
            Assert.AreEqual(token, sessions.GetSessionToken(unrelatedPath));
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public async Task ExecuteTransactionAsync_RepeatedStaleResponses_RefreshOnlyOncePerCommit(
            bool readOnly, bool delayBudgetExhausted)
        {
            Mock<CosmosClientContext> context = this.CreateMockClientContext();
            Mock<ISessionContainer> sessions = new Mock<ISessionContainer>();
            using MockDocumentClient documentClient = CreateCollectionCacheDocumentClient(sessions.Object);
            context.Setup(c => c.DocumentClient).Returns(documentClient);
            Mock<ClientCollectionCache> cache = Mock.Get(await documentClient.GetCollectionCacheAsync(NoOpTrace.Singleton));
            SetupCollectionRefresh(cache, (_, _) => Task.FromResult(ContainerProperties.CreateWithResourceId(TestCollectionResourceId)));
            List<Guid> tokens = new List<Guid>();
            List<string> payloads = new List<string>();
            List<TimeSpan> delays = new List<TimeSpan>();
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(context, (stream, enrich) =>
            {
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                payloads.Add(reader.ReadToEnd());
                using RequestMessage request = new RequestMessage(HttpMethod.Post, new Uri("https://localhost/operations/dtc"));
                enrich(request);
                tokens.Add(Guid.Parse(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]));
            }, () =>
            {
                ResponseMessage stale = new ResponseMessage(HttpStatusCode.Gone);
                stale.Headers.SubStatusCode = SubStatusCodes.NameCacheIsStale;
                stale.Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"isRetriable\":true}"));
                return Task.FromResult(stale);
            });
            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(), context.Object,
                readOnly ? OperationType.Read : OperationType.CommitDistributedTransaction,
                TimeSpan.FromSeconds(1),
                delayProvider: (delay, _) =>
                {
                    delays.Add(delay);
                    return Task.CompletedTask;
                },
                maxCumulativeRetryDelay: delayBudgetExhausted ? TimeSpan.Zero : TimeSpan.FromMinutes(1),
                maxIsRetriableRetryCount: 3);
            using DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(
                NoOpTrace.Singleton, CancellationToken.None);

            int expectedRequests = delayBudgetExhausted ? 1 : 4;
            Assert.AreEqual(HttpStatusCode.Gone, response.StatusCode);
            Assert.AreEqual(expectedRequests, tokens.Count);
            Assert.IsTrue(tokens.All(token => token == response.IdempotencyToken));
            Assert.IsTrue(payloads.All(payload => payload == payloads[0]));
            Assert.AreEqual(expectedRequests - 1, delays.Count);
            Assert.IsTrue(delays.All(delay => delay > TimeSpan.Zero));
            cache.Verify(c => c.RefreshCollectionAsync(
                HttpConstants.Versions.CurrentVersion, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(),
                It.IsAny<IClientSideRequestStatistics>(), It.IsAny<CancellationToken>()), Times.Once());
            sessions.Verify(s => s.ClearTokenByResourceId(It.IsAny<string>()), Times.Never());
        }

        [TestMethod]
        [Description("Verifies that when the DTC response carries a session token, the token is merged into the SessionContainer")]
        public async Task ExecuteTransactionAsync_MergesSessionTokensIntoSessionContainer()
        {
            const string lsnOnly = "1#9#4=8#5=7";
            const string pkRangeId = "0";
            const string expectedToken = "0:1#9#4=8#5=7";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            string responseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: pkRangeId) });

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: responseJson,
                statusCode: HttpStatusCode.OK);

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction);

            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.AreEqual(expectedToken, storedToken,
                "Session token should be assembled as {pkRangeId}:{lsn} and merged into SessionContainer after a successful DTC commit.");
        }

        [TestMethod]
        [Description("When a per-operation session token is absent, SetSessionToken is NOT called for that operation and the SessionContainer is not updated")]
        public async Task ExecuteTransactionAsync_SkipsMerge_WhenSessionTokenIsNull()
        {
            // sessionToken: null omits the field from the JSON body entirely
            string responseJson = BuildDtcResponseJson(new[] { (statusCode: 201, sessionToken: (string)null) });

            SessionContainer sessionContainer = new SessionContainer("testhost");
            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: responseJson,
                statusCode: HttpStatusCode.OK);

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction);

            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.IsTrue(string.IsNullOrEmpty(storedToken),
                "SessionContainer should not be updated when the operation result has no session token.");
        }

        [TestMethod]
        [Description("Verifies that the correct collectionRid and collectionFullname are passed to SetSessionToken for each operation")]
        public async Task ExecuteTransactionAsync_PassesCorrectCollectionToSetSessionToken()
        {
            const string lsnOnly = "1#5#4=3";
            const string pkRangeId = "0";
            const string assembledToken = "0:1#5#4=3";
            const string container2 = "testcontainer2";

            string collectionRid1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionRid2 = ResourceId.NewDocumentCollectionId(42, 200).DocumentCollectionId.ToString();

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();

            MockDocumentClient documentClient = new MockDocumentClient
            {
                sessionContainer = mockSessionContainer.Object
            };

            ContainerProperties containerProperties1 = ContainerProperties.CreateWithResourceId(collectionRid1);
            containerProperties1.PartitionKeyPath = "/pk";
            ContainerProperties containerProperties2 = ContainerProperties.CreateWithResourceId(collectionRid2);
            containerProperties2.PartitionKeyPath = "/pk";

            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(c => c.DocumentClient).Returns(documentClient);
            mockContext.Setup(c => c.SerializerCore).Returns(MockCosmosUtil.Serializer);
            mockContext
                .Setup(c => c.GetCachedContainerPropertiesAsync(
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName),
                    It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProperties1);
            mockContext
                .Setup(c => c.GetCachedContainerPropertiesAsync(
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, container2),
                    It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProperties2);

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new MemoryStream(
                    Encoding.UTF8.GetBytes(BuildDtcResponseJson(
                        new[]
                        {
                            (statusCode: 200, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: pkRangeId),
                            (statusCode: 200, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: pkRangeId),
                        })))
            };
            mockContext.Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    ResourceType.DistributedTransactionBatch,
                    OperationType.CommitDistributedTransaction,
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMessage);

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1"),
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 1,
                    DatabaseName,
                    container2,
                    new PartitionKey("pk2"),
                    id: "doc2"),
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction);

            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            // Verify SetSessionToken was called once per operation with the correct collection identity.
            mockSessionContainer.Verify(
                s => s.SetSessionToken(
                    collectionRid1,
                   DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName),
                    It.Is<INameValueCollection>(h => h[HttpConstants.HttpHeaders.SessionToken] == assembledToken)),
                Times.Once,
                "SetSessionToken should be called for the first operation with its collection RID and fullname.");

            mockSessionContainer.Verify(
                s => s.SetSessionToken(
                    collectionRid2,
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, container2),
                    It.Is<INameValueCollection>(h => h[HttpConstants.HttpHeaders.SessionToken] == assembledToken)),
                Times.Once,
                "SetSessionToken should be called for the second operation with its collection RID and fullname.");
        }

        [TestMethod]
        [Description("Verifies that session tokens are still merged into the SessionContainer even when the DTC response indicates a failure")]
        public async Task ExecuteTransactionAsync_MergesSessionTokens_OnFailureResponse()
        {
            // Deliberately distinct from the success-path token so a copy-paste regression would be caught.
            const string lsnOnly = "1#3#4=2#5=1";
            const string pkRangeId = "0";
            const string expectedToken = "0:1#3#4=2#5=1";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: BuildDtcResponseJson(new[] { (statusCode: 409, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: pkRangeId) }),
                statusCode: HttpStatusCode.Conflict);

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction);

            DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.AreEqual(expectedToken, storedToken,
                "Session token should still be merged even when the DTC response indicates a failure.");
        }

        [TestMethod]
        [Description("When session token is LSN-only and partitionKeyRangeId is present, the token is assembled as {pkRangeId}:{lsn}")]
        public async Task ExecuteTransactionAsync_AssemblesSessionToken_WhenPartitionKeyRangeIdIsPresent()
        {
            const string lsnOnly = "1#9#4=8#5=7";
            const string pkRangeId = "0";
            const string expectedToken = "0:1#9#4=8#5=7";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            string responseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: pkRangeId) });

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: responseJson,
                statusCode: HttpStatusCode.OK);

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction);

            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.AreEqual(expectedToken, storedToken,
                "Session token should be assembled as {pkRangeId}:{lsn} when partitionKeyRangeId is present.");
        }

        [TestMethod]
        [Description("When partitionKeyRangeId is absent, merge is silently skipped")]
        public async Task ExecuteTransactionAsync_SkipsMerge_WhenLsnOnlyAndPartitionKeyRangeIdIsAbsent()
        {
            const string lsnOnly = "1#9#4=8#5=7";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            // No partitionKeyRangeId; session token is LSN-only (as always returned by the endpoint)
            string responseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: (string)null) });

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: responseJson,
                statusCode: HttpStatusCode.OK);

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction);

            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.IsTrue(string.IsNullOrEmpty(storedToken),
                "SessionContainer should not be updated when partitionKeyRangeId is absent.");
        }


        [DataTestMethod]
        [DataRow("", DisplayName = "Empty string partitionKeyRangeId")]
        [DataRow(" ", DisplayName = "Whitespace-only partitionKeyRangeId")]
        [DataRow("   ", DisplayName = "Multiple whitespace partitionKeyRangeId")]
        [Description("When partitionKeyRangeId is present but empty or whitespace, merge is silently skipped. " +
                     "The server has no validation on this field; throwing would risk failing a committed transaction.")]
        public async Task ExecuteTransactionAsync_SkipsMerge_WhenPartitionKeyRangeIdIsEmptyOrWhitespace(string pkRangeId)
        {
            const string lsnOnly = "1#9#4=8#5=7";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            string responseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: pkRangeId) });

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: responseJson,
                statusCode: HttpStatusCode.OK);

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction);

            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.IsTrue(string.IsNullOrEmpty(storedToken),
                $"SessionContainer should not be updated when partitionKeyRangeId is '{pkRangeId}' (empty/whitespace).");
        }

        // ─── Retry / Spec-Compliance Tests ─────────────────────────────────────

        [TestMethod]
        [Description("m8: In a multi-operation response, an op whose pkRangeId is absent is skipped while " +
                     "subsequent ops with pkRangeId still have their session tokens merged correctly.")]
        public async Task ExecuteTransactionAsync_MultiOp_SkipsOpWithMissingPkRangeId_MergesRemainingOps()
        {
            const string lsnOnly = "1#9#4=8#5=7";
            const string pkRangeId = "0";
            const string assembledToken = "0:1#9#4=8#5=7";
            const string container2 = "testcontainer2";

            string collectionRid1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionRid2 = ResourceId.NewDocumentCollectionId(42, 200).DocumentCollectionId.ToString();

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();

            MockDocumentClient documentClient = new MockDocumentClient
            {
                sessionContainer = mockSessionContainer.Object
            };

            ContainerProperties containerProperties1 = ContainerProperties.CreateWithResourceId(collectionRid1);
            containerProperties1.PartitionKeyPath = "/pk";
            ContainerProperties containerProperties2 = ContainerProperties.CreateWithResourceId(collectionRid2);
            containerProperties2.PartitionKeyPath = "/pk";

            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(c => c.DocumentClient).Returns(documentClient);
            mockContext.Setup(c => c.SerializerCore).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(c => c.GetCachedContainerPropertiesAsync(
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName),
                    It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProperties1);
            mockContext.Setup(c => c.GetCachedContainerPropertiesAsync(
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, container2),
                    It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProperties2);

            // op 0: missing pkRangeId — should be skipped (SessionToken nulled in FromJson)
            // op 1: has pkRangeId — should be merged
            string responseJson = BuildDtcResponseJson(new[]
            {
                (statusCode: 201, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: (string)null),
                (statusCode: 201, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: pkRangeId),
            });

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(responseJson))
            };

            mockContext.Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    ResourceType.DistributedTransactionBatch,
                    OperationType.CommitDistributedTransaction,
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMessage);

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create, operationIndex: 0,
                    DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1"),
                new DistributedTransactionOperation(
                    OperationType.Create, operationIndex: 1,
                    DatabaseName, container2, new PartitionKey("pk2"), id: "doc2"),
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction);

            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            // op 0 (missing pkRangeId) must NOT have been merged.
            mockSessionContainer.Verify(
                s => s.SetSessionToken(
                    collectionRid1,
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName),
                    It.IsAny<INameValueCollection>()),
                Times.Never,
                "SetSessionToken must not be called for an operation whose pkRangeId is absent.");

            // op 1 (has pkRangeId) must have been merged with the assembled token.
            mockSessionContainer.Verify(
                s => s.SetSessionToken(
                    collectionRid2,
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, container2),
                    It.Is<INameValueCollection>(h => h[HttpConstants.HttpHeaders.SessionToken] == assembledToken)),
                Times.Once,
                "SetSessionToken must be called for the operation that has pkRangeId, with the assembled token.");
        }

        [TestMethod]
        [Description("m9: When an operation result has no partitionKeyRangeId, FromJson emits a TraceWarning " +
                     "so the skip is observable in diagnostic traces.")]
        public async Task ExecuteTransactionAsync_EmitsTraceWarning_WhenPartitionKeyRangeIdIsAbsent()
        {
            const string lsnOnly = "1#9#4=8#5=7";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            string responseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: (string)null) });

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: responseJson,
                statusCode: HttpStatusCode.OK);

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create, operationIndex: 0,
                    DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1")
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction);

            List<string> capturedWarnings = new List<string>();
            System.Diagnostics.TraceListener listener = new DelegatingTraceListener(
                (eventType, message) =>
                {
                    if (eventType == System.Diagnostics.TraceEventType.Warning)
                    {
                        capturedWarnings.Add(message);
                    }
                });

            System.Diagnostics.SourceLevels previousLevel = DefaultTrace.TraceSource.Switch.Level;
            DefaultTrace.TraceSource.Switch.Level = System.Diagnostics.SourceLevels.All;
            DefaultTrace.TraceSource.Listeners.Add(listener);
            try
            {
                await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);
            }
            finally
            {
                DefaultTrace.TraceSource.Listeners.Remove(listener);
                DefaultTrace.TraceSource.Switch.Level = previousLevel;
            }

            Assert.IsTrue(
                capturedWarnings.Any(m => m.Contains("partitionKeyRangeId")),
                "A TraceWarning mentioning 'partitionKeyRangeId' should be emitted when pkRangeId is absent.");
        }


        [TestMethod]
        [Description("When SetSessionToken throws, the exception is swallowed and ExecuteTransactionAsync still returns the response rather than rethrowing")]
        public async Task ExecuteTransactionAsync_SwallowsSetSessionTokenException()
        {
            const string lsnOnly = "1#9#4=8#5=7";
            const string pkRangeId = "0";

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();
            mockSessionContainer
                .Setup(s => s.SetSessionToken(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<INameValueCollection>()))
                .Throws(new InvalidOperationException("simulated SetSessionToken failure"));

            MockDocumentClient documentClient = new MockDocumentClient
            {
                sessionContainer = mockSessionContainer.Object
            };

            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId(CollectionResourceId);
            containerProperties.Id = "TestContainerId";
            containerProperties.PartitionKeyPath = "/pk";

            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(c => c.DocumentClient).Returns(documentClient);
            mockContext.Setup(c => c.SerializerCore).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(c => c.GetCachedContainerPropertiesAsync(
                    It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProperties);

            string responseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: pkRangeId) });

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(responseJson))
            };

            mockContext.Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    ResourceType.DistributedTransactionBatch,
                    OperationType.CommitDistributedTransaction,
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMessage);

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction);

            // Must not throw even though SetSessionToken throws internally.
            DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);
            Assert.IsNotNull(response, "ExecuteTransactionAsync should return a response even when SetSessionToken throws.");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        [Description("When SetSessionToken throws OperationCanceledException, the exception must propagate — it must not be swallowed by the MergeSessionTokens catch block.")]
        public async Task ExecuteTransactionAsync_PropagatesOperationCanceledException_FromSetSessionToken()
        {
            const string lsnOnly = "1#9#4=8#5=7";
            const string pkRangeId = "0";

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();
            mockSessionContainer
                .Setup(s => s.SetSessionToken(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<INameValueCollection>()))
                .Throws(new OperationCanceledException("simulated cancellation in SetSessionToken"));

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                mockSessionContainer.Object,
                responseContent: BuildDtcResponseJson(
                    new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: pkRangeId) }),
                statusCode: HttpStatusCode.OK);

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction);

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None),
                "OperationCanceledException from SetSessionToken must propagate, not be swallowed.");
        }


        [TestMethod]
        [Description("Verifies that a commit succeeds without retrying when the server returns a success response on the first attempt.")]
        public async Task CommitTransaction_SucceedsOnFirstAttempt()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.IsFalse(response.IsRetriable);
                Assert.AreEqual(1, callCount);
            }
        }

        [TestMethod]
        [Description("Verifies that when the server responds with isRetriable:true, the committer retries and eventually succeeds.")]
        public async Task CommitTransaction_RetriesOnRetriableResponse_ThenSucceeds()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return Task.FromResult(CreateRetriableErrorResponseMessage());
                    }

                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(2, callCount);
            }
        }

        [TestMethod]
        [Description("Verifies that the committer retries on isRetriable:true responses until the cancellation token is cancelled (before the retry budget is exhausted).")]
        public async Task CommitTransaction_RetriableResponse_RetriesUntilCancelledBeforeBudgetExhausted()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                int callCount = 0;
                Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
                this.SetupProcessResourceOperation(
                    mockContext,
                    () =>
                    {
                        callCount++;
                        if (callCount == 3)
                        {
                            cts.Cancel();
                        }

                        return Task.FromResult(CreateRetriableErrorResponseMessage());
                    });

                // Non-zero delay so Task.Delay honours the already-cancelled token.
                DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                    CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.FromMilliseconds(1));

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, cts.Token));

                // Retries continue until the cancellation token fires (before exhausting the budget).
                Assert.AreEqual(3, callCount);
            }
        }

        [TestMethod]
        [Description("Verifies that the outer isRetriable retry loop returns the last response after exhausting the retry budget (MaxIsRetriableRetryCount).")]
        public async Task CommitTransaction_ExhaustsIsRetriableRetryBudget_ReturnsLastResponse()
        {
            int callCount = 0;
            List<TimeSpan> capturedDelays = new List<TimeSpan>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(CreateRetriableErrorResponseMessage());
                });

            Func<TimeSpan, CancellationToken, Task> captureDelay = (delay, _) =>
            {
                capturedDelays.Add(delay);
                return Task.CompletedTask;
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                retryBaseDelay: TimeSpan.Zero,
                delayProvider: captureDelay);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                // MaxIsRetriableRetryCount (10) retries + 1 final call that hits the budget check = 11 total calls.
                Assert.AreEqual(DistributedTransactionCommitter.MaxIsRetriableRetryCount + 1, callCount,
                    "Expected exactly MaxIsRetriableRetryCount retries plus one final call that triggers budget exhaustion.");
                Assert.AreEqual(DistributedTransactionCommitter.MaxIsRetriableRetryCount, capturedDelays.Count,
                    "Delay provider must be called once per retry attempt.");
                Assert.IsFalse(response.IsSuccessStatusCode,
                    "The returned response must be the last non-success response.");
                Assert.IsTrue(response.IsRetriable,
                    "The returned response must still have IsRetriable=true (budget exhausted, not a new response).");
                Assert.IsNotNull(response.Diagnostics,
                    "Diagnostics must not be null when the retry budget is exhausted — this is the most important failure path to have diagnostics on.");
            }
        }

        [TestMethod]
        [Description("Verifies that the attempt-count cap is read from CosmosClientOptions.MaxRetryAttemptsOnAbortedTransactions.")]
        public async Task CommitTransaction_AttemptCapFromClientOptions_IsHonored()
        {
            const int configuredCap = 3;
            int callCount = 0;
            List<TimeSpan> capturedDelays = new List<TimeSpan>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext(
                new CosmosClientOptions { MaxRetryAttemptsOnAbortedTransactions = configuredCap });
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(CreateRetriableErrorResponseMessage());
                });

            Func<TimeSpan, CancellationToken, Task> captureDelay = (delay, _) =>
            {
                capturedDelays.Add(delay);
                return Task.CompletedTask;
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                retryBaseDelay: TimeSpan.Zero,
                delayProvider: captureDelay);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(configuredCap + 1, callCount,
                    "Expected exactly configuredCap retries plus one final call that triggers budget exhaustion.");
                Assert.AreEqual(configuredCap, capturedDelays.Count,
                    "Delay provider must be called once per retry attempt.");
                Assert.IsTrue(response.IsRetriable);
            }
        }

        [TestMethod]
        [Description("Verifies that the cumulative wait cap is read from CosmosClientOptions.MaxRetryWaitTimeOnAbortedTransactions.")]
        public async Task CommitTransaction_CumulativeWaitCapFromClientOptions_IsHonored()
        {
            int callCount = 0;
            List<TimeSpan> capturedDelays = new List<TimeSpan>();
            // Small cumulative budget (10s) with a 15s base delay: the first planned delay already exceeds it,
            // so exactly one call happens and no delay is slept.
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext(
                new CosmosClientOptions { MaxRetryWaitTimeOnAbortedTransactions = TimeSpan.FromSeconds(10) });
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(CreateRetriableErrorResponseMessage());
                });

            Func<TimeSpan, CancellationToken, Task> captureDelay = (delay, _) =>
            {
                capturedDelays.Add(delay);
                return Task.CompletedTask;
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                retryBaseDelay: TimeSpan.FromSeconds(15),
                delayProvider: captureDelay);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(1, callCount,
                    "Expected exactly 1 call: the first planned delay (~15s) exceeds the 10s cumulative budget.");
                Assert.AreEqual(0, capturedDelays.Count,
                    "No delay should be slept once the first planned delay exceeds the cumulative budget.");
                Assert.IsTrue(response.IsRetriable);
            }
        }

        [TestMethod]
        [Description("Verifies that setting CosmosClientOptions.MaxRetryAttemptsOnAbortedTransactions to 0 disables automatic abort retries.")]
        public async Task CommitTransaction_ZeroAttemptCapFromClientOptions_DisablesRetries()
        {
            int callCount = 0;
            List<TimeSpan> capturedDelays = new List<TimeSpan>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext(
                new CosmosClientOptions { MaxRetryAttemptsOnAbortedTransactions = 0 });
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(CreateRetriableErrorResponseMessage());
                });

            Func<TimeSpan, CancellationToken, Task> captureDelay = (delay, _) =>
            {
                capturedDelays.Add(delay);
                return Task.CompletedTask;
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                retryBaseDelay: TimeSpan.Zero,
                delayProvider: captureDelay);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(1, callCount,
                    "With the attempt cap at 0, the first retriable response must be returned without retrying.");
                Assert.AreEqual(0, capturedDelays.Count,
                    "No delay should be slept when abort retries are disabled.");
                Assert.IsTrue(response.IsRetriable);
            }
        }

        [TestMethod]
        [Description("Verifies that when CosmosClientOptions leaves the abort-retry bounds unset, the committer falls back to the SDK defaults.")]
        public async Task CommitTransaction_UnsetClientOptions_FallsBackToDefaults()
        {
            int callCount = 0;
            List<TimeSpan> capturedDelays = new List<TimeSpan>();
            // Client options present but bounds unset -> defaults apply (10 attempts).
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext(new CosmosClientOptions());
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(CreateRetriableErrorResponseMessage());
                });

            Func<TimeSpan, CancellationToken, Task> captureDelay = (delay, _) =>
            {
                capturedDelays.Add(delay);
                return Task.CompletedTask;
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                retryBaseDelay: TimeSpan.Zero,
                delayProvider: captureDelay);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(DistributedTransactionCommitter.MaxIsRetriableRetryCount + 1, callCount,
                    "Unset options must fall back to the default attempt cap.");
                Assert.AreEqual(DistributedTransactionCommitter.MaxIsRetriableRetryCount, capturedDelays.Count,
                    "Delay provider must be called once per retry attempt under the default cap.");
            }
        }

        [TestMethod]
        [Description("Verifies that an explicit test override for maxIsRetriableRetryCount takes precedence over CosmosClientOptions.")]
        public async Task CommitTransaction_ExplicitAttemptCapOverridesClientOptions()
        {
            const int optionsCap = 8;
            const int explicitCap = 2;
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext(
                new CosmosClientOptions { MaxRetryAttemptsOnAbortedTransactions = optionsCap });
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(CreateRetriableErrorResponseMessage());
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                retryBaseDelay: TimeSpan.Zero,
                delayProvider: (delay, _) => Task.CompletedTask,
                maxIsRetriableRetryCount: explicitCap);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(explicitCap + 1, callCount,
                    "The explicit constructor override must take precedence over the client options value.");
            }
        }

        [TestMethod]
        [Description("Verifies that the outer retry loop stops when the cumulative delay budget (MaxCumulativeRetryDelay) is exceeded, even if attempt count has not been reached.")]
        public async Task CommitTransaction_ExhaustsCumulativeDelayBudget_ReturnsLastResponse()
        {
            int callCount = 0;
            List<TimeSpan> capturedDelays = new List<TimeSpan>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(CreateRetriableErrorResponseMessage());
                });

            Func<TimeSpan, CancellationToken, Task> captureDelay = (delay, _) =>
            {
                capturedDelays.Add(delay);
                return Task.CompletedTask;
            };

            // Use a large base delay (15s) so the cumulative budget (30s) is exceeded after 2-3 retries,
            // well before the attempt count cap (10).
            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                retryBaseDelay: TimeSpan.FromSeconds(15),
                delayProvider: captureDelay);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                // With 15s base delay and exponential backoff (±25% jitter):
                //   attempt 0 delay = 15s * 2^0 * jitter ≈ 11.25–18.75s (cumulative ≈ 11.25–18.75s, under 30s budget)
                //   attempt 1 delay = 15s * 2^1 * jitter ≈ 22.5–37.5s  (cumulative ≈ 33.75–56.25s, exceeds 30s budget)
                // So the loop should make exactly 2 calls and sleep exactly once before the budget stops it.
                Assert.AreEqual(2, callCount,
                    $"Expected exactly 2 calls (initial + 1 retry) before cumulative delay budget is exceeded. Got {callCount}.");
                Assert.AreEqual(1, capturedDelays.Count,
                    $"Expected exactly 1 delay to be slept before the budget-exceeding second delay triggers early exit. Got {capturedDelays.Count}.");

                // The single slept delay must be under the budget (it passed the check).
                Assert.IsTrue(capturedDelays[0] <= DistributedTransactionCommitter.MaxCumulativeRetryDelay,
                    $"The slept delay ({capturedDelays[0].TotalMilliseconds}ms) must be within budget since it passed the check.");
                // The slept delay must be substantial (15s base * 0.75 jitter minimum = 11.25s).
                Assert.IsTrue(capturedDelays[0] >= TimeSpan.FromSeconds(11),
                    $"Delay should reflect 15s base with jitter, but was only {capturedDelays[0].TotalMilliseconds}ms.");

                Assert.IsFalse(response.IsSuccessStatusCode,
                    "The returned response must be the last non-success response.");
                Assert.IsNotNull(response.Diagnostics,
                    "Diagnostics must not be null when the cumulative delay budget is exhausted.");
            }
        }

        [TestMethod]
        [Description("Verifies that large server RetryAfter headers exhaust the cumulative delay budget after only a few attempts, " +
                     "even though the attempt count cap (10) is far from reached.")]
        public async Task CommitTransaction_ServerRetryAfterDominates_ExhaustsCumulativeDelayBudgetEarly()
        {
            int callCount = 0;
            List<TimeSpan> capturedDelays = new List<TimeSpan>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    // Server returns RetryAfter=25s on every retriable response
                    ResponseMessage msg = CreateRetriableErrorResponseMessage();
                    msg.Headers.RetryAfter = TimeSpan.FromSeconds(25);
                    return Task.FromResult(msg);
                });

            Func<TimeSpan, CancellationToken, Task> captureDelay = (delay, _) =>
            {
                capturedDelays.Add(delay);
                return Task.CompletedTask;
            };

            // Use small base delay so server RetryAfter dominates the delay selection.
            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                retryBaseDelay: TimeSpan.FromMilliseconds(100),
                delayProvider: captureDelay);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                // With 25s server hint per attempt: attempt 0 delay=25s (cumulative=25s OK), attempt 1 delay=25s (cumulative=50s > 30s budget).
                // So only 1 retry should succeed before budget exhaustion stops the loop.
                Assert.AreEqual(1, capturedDelays.Count,
                    $"Expected exactly 1 retry before the cumulative budget (30s) is exceeded by the second 25s RetryAfter. Got {capturedDelays.Count}.");
                Assert.AreEqual(2, callCount,
                    "Expected 2 total calls: initial attempt + 1 retry before budget exhaustion on the second delay computation.");
                Assert.IsFalse(response.IsSuccessStatusCode);
                Assert.IsTrue(response.IsRetriable);
                Assert.IsNotNull(response.Diagnostics);

                // Verify the captured delay used the server hint (25s) not the computed backoff
                Assert.IsTrue(capturedDelays[0] >= TimeSpan.FromSeconds(24),
                    $"Delay should reflect server RetryAfter (~25s), but was {capturedDelays[0].TotalMilliseconds}ms.");
            }
        }

        [DataTestMethod]
        [Description("Verifies that a CosmosException thrown from the pipeline propagates immediately without triggering the outer retry loop, regardless of status code. Status-code-based retries (e.g. 408, 449/5352) are handled by ClientRetryPolicy inside the pipeline; the outer loop only handles the isRetriable JSON body flag.")]
        [DataRow((int)HttpStatusCode.RequestTimeout, DisplayName = "408 RequestTimeout — propagates")]
        [DataRow((int)HttpStatusCode.NotFound, DisplayName = "404 NotFound — propagates")]
        public async Task CommitTransaction_CosmosException_PropagatesImmediately(int statusCode)
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    CosmosException ex = new CosmosException(
                        "test exception",
                        (HttpStatusCode)statusCode,
                        subStatusCode: 0,
                        activityId: null,
                        requestCharge: 0);
                    return Task.FromException<ResponseMessage>(ex);
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            CosmosException thrown = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

            Assert.AreEqual((HttpStatusCode)statusCode, thrown.StatusCode);
            Assert.AreEqual(1, callCount);
        }

        [DataTestMethod]
        [Description("Verifies that a response without isRetriable:true (BadRequest body, or generic 500 body) is returned immediately without any retry attempt.")]
        [DataRow((int)HttpStatusCode.BadRequest, DisplayName = "400 BadRequest with empty body — no retry")]
        [DataRow((int)HttpStatusCode.InternalServerError, DisplayName = "500 InternalServerError with empty body — no retry")]
        public async Task CommitTransaction_DoesNotRetryOnNonRetriableBody(int statusCode)
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(
                        new ResponseMessage((HttpStatusCode)statusCode)
                        {
                            Content = new MemoryStream(Encoding.UTF8.GetBytes("{}"))
                        });
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual((HttpStatusCode)statusCode, response.StatusCode);
                Assert.IsFalse(response.IsSuccessStatusCode);
                Assert.IsFalse(response.IsRetriable);
                Assert.AreEqual(1, callCount);
            }
        }

        [TestMethod]
        [Description("FastResponse retry matrix (isRetriable=true, not durably Aborted): the commit retries the identical operations but replays the SAME idempotency token, because the prior attempt was not terminally consumed. Any non-452 retriable status is treated as 'not aborted'.")]
        public async Task CommitTransaction_RetriesWithSameTokenWhenRetriableButNotAborted()
        {
            int callCount = 0;
            List<string> capturedTokens = new List<string>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                mockContext,
                (stream, enricher) =>
                {
                    RequestMessage request = new RequestMessage
                    {
                        ResourceType = ResourceType.DistributedTransactionBatch,
                        OperationType = OperationType.CommitDistributedTransaction,
                    };
                    enricher(request);
                    capturedTokens.Add(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]);
                },
                () =>
                {
                    callCount++;
                    return callCount == 1
                        ? Task.FromResult(CreateRetriableNonAbortedResponseMessage())
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(2, callCount, "A retriable non-aborted outcome must be retried.");
            }

            Assert.AreEqual(2, capturedTokens.Count, "Both the initial attempt and the retry must stamp a token.");
            Assert.AreEqual(capturedTokens[0], capturedTokens[1],
                "A retriable non-aborted retry must replay the SAME idempotency token, not a rotated one.");
            Assert.IsFalse(capturedTokens.Contains(Guid.Empty.ToString()),
                "Every attempt must carry a real (non-empty) idempotency token.");
        }

        // ─── Dispatch signals ──────────────────────────────────────────────────
        // The committer only attaches a tracker whose lifetime matches the idempotency token;
        // RetryHandler injects it into ClientRetryPolicy, which stamps the headers per dispatch.

        [TestMethod]
        [Description("A write transaction carries the dispatch tracker on the request, which is how ClientRetryPolicy stamps the signals on a dispatch it re-routes to another write region without returning to the committer.")]
        public async Task ExecuteTransactionAsync_WriteTransaction_CarriesDispatchTracker()
        {
            List<DistributedTransactionDispatchTracker> capturedTrackers = new List<DistributedTransactionDispatchTracker>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                mockContext,
                (stream, enricher) => capturedTrackers.Add(
                    CaptureDispatchTracker(enricher, OperationType.CommitDistributedTransaction)),
                () => Task.FromResult(CreateSuccessResponseMessage(operationCount: 1)));

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            Assert.AreEqual(1, capturedTrackers.Count);
            Assert.IsNotNull(capturedTrackers[0],
                "Without a tracker on the request the coordinator can never be told that a dispatch is a retry or crossed write regions.");
        }

        [TestMethod]
        [Description("A read transaction holds no commit state, so replaying it cannot execute a write twice; both signals are omitted entirely rather than sent as False.")]
        public async Task ExecuteTransactionAsync_ReadTransaction_OmitsDispatchTracker()
        {
            List<DistributedTransactionDispatchTracker> capturedTrackers = new List<DistributedTransactionDispatchTracker>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                mockContext,
                (stream, enricher) => capturedTrackers.Add(
                    CaptureDispatchTracker(enricher, OperationType.Read)),
                () => Task.FromResult(CreateSuccessResponseMessage(operationCount: 1)));

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(), mockContext.Object, OperationType.Read, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            Assert.AreEqual(1, capturedTrackers.Count);
            Assert.IsNull(capturedTrackers[0],
                "A read transaction must not carry the signals at all.");
        }

        [TestMethod]
        [Description("A durably aborted (452) retry resubmits under a new idempotency token, which has no dispatch history, so the next attempt starts from False on both signals.")]
        public async Task ExecuteTransactionAsync_RetryAfterAbort_ReplacesDispatchTracker()
        {
            int callCount = 0;
            List<DistributedTransactionDispatchTracker> capturedTrackers = new List<DistributedTransactionDispatchTracker>();
            List<(bool IsRetry, bool IsCrossRegionRedirect)> signalsAtAttemptStart = new List<(bool, bool)>();

            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                mockContext,
                (stream, enricher) => DriveCrossRegionCrossingForAttempt(
                    enricher, capturedTrackers, signalsAtAttemptStart),
                () =>
                {
                    callCount++;
                    return callCount == 1
                        ? Task.FromResult(new ResponseMessage((HttpStatusCode)StatusCodes.TransactionAborted)
                        {
                            Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"isRetriable\":true}"))
                        })
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(2, callCount, "A durably aborted retriable outcome must be retried.");
            }

            Assert.AreNotSame(capturedTrackers[0], capturedTrackers[1],
                "Rotation starts the new token on its own tracker, so the attempts must not share one.");
            CollectionAssert.AreEqual(
                new[] { (IsRetry: false, IsCrossRegionRedirect: false), (IsRetry: false, IsCrossRegionRedirect: false) },
                signalsAtAttemptStart,
                "The rotated token has no dispatch history, so its first dispatch must report neither signal even though its predecessor crossed a boundary.");
        }

        [TestMethod]
        [Description("A retriable non-aborted retry replays the SAME idempotency token, so both signals already earned under that token must still be reported on every later dispatch.")]
        public async Task ExecuteTransactionAsync_RetryWithoutAbort_KeepsDispatchTracker()
        {
            int callCount = 0;
            List<DistributedTransactionDispatchTracker> capturedTrackers = new List<DistributedTransactionDispatchTracker>();
            List<(bool IsRetry, bool IsCrossRegionRedirect)> signalsAtAttemptStart = new List<(bool, bool)>();

            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                mockContext,
                (stream, enricher) => DriveCrossRegionCrossingForAttempt(
                    enricher, capturedTrackers, signalsAtAttemptStart),
                () =>
                {
                    callCount++;
                    return callCount == 1
                        ? Task.FromResult(CreateRetriableNonAbortedResponseMessage())
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(2, callCount, "A retriable non-aborted outcome must be retried.");
            }

            Assert.AreSame(capturedTrackers[0], capturedTrackers[1],
                "The token is replayed rather than rotated, so both attempts must share the tracker that describes it.");
            CollectionAssert.AreEqual(
                new[] { (IsRetry: false, IsCrossRegionRedirect: false), (IsRetry: true, IsCrossRegionRedirect: true) },
                signalsAtAttemptStart,
                "The same token is replayed through a fresh retry policy, so both signals must survive that policy's reset.");
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ExecuteTransactionAsync_PendingAttempt_PreservesTokenTrackerOwnership(bool abortFirstAttempt)
        {
            TaskCompletionSource<bool> firstAttemptStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<ResponseMessage> firstAttemptResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
            List<(Guid Token, DistributedTransactionDispatchTracker Tracker)> attempts = new();
            List<(bool IsRetry, bool IsCrossRegionRedirect)> startingSignals = new();
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                mockContext,
                (stream, enricher) => CaptureDispatchTracker(
                    enricher,
                    OperationType.CommitDistributedTransaction,
                    (token, tracker) =>
                    {
                        attempts.Add((token, tracker));
                        startingSignals.Add(tracker.RecordDispatch("East US"));
                        tracker.RecordDispatch("West US");
                    }),
                () =>
                {
                    if (Interlocked.Increment(ref callCount) == 1)
                    {
                        firstAttemptStarted.TrySetResult(true);
                        return firstAttemptResponse.Task;
                    }

                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(10));
            using ResponseMessage retryResponse = abortFirstAttempt
                ? new ResponseMessage((HttpStatusCode)StatusCodes.TransactionAborted)
                {
                    Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"isRetriable\":true}"))
                }
                : CreateRetriableNonAbortedResponseMessage();
            DistributedTransactionCommitter committer = new(
                CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);
            Task<DistributedTransactionResponse> execution = committer.ExecuteTransactionAsync(
                NoOpTrace.Singleton, cancellation.Token);
            try
            {
                await firstAttemptStarted.Task.WaitAsync(cancellation.Token);
                Assert.AreEqual(1, Volatile.Read(ref callCount));
                Assert.AreEqual(1, attempts.Count);
                Assert.AreNotEqual(Guid.Empty, attempts[0].Token);
                Assert.IsFalse(execution.IsCompleted);
            }
            finally
            {
                firstAttemptResponse.TrySetResult(retryResponse);
                using DistributedTransactionResponse response = await execution.WaitAsync(cancellation.Token);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            Assert.AreEqual(2, attempts.Count);
            Assert.AreEqual((false, false), startingSignals[0]);
            if (abortFirstAttempt)
            {
                Assert.AreNotEqual(attempts[0].Token, attempts[1].Token);
                Assert.AreNotSame(attempts[0].Tracker, attempts[1].Tracker);
                Assert.AreEqual((false, false), startingSignals[1]);
            }
            else
            {
                Assert.AreEqual(attempts[0].Token, attempts[1].Token);
                Assert.AreSame(attempts[0].Tracker, attempts[1].Tracker);
                Assert.AreEqual((true, true), startingSignals[1]);
            }

            Assert.AreEqual((true, true), attempts[0].Tracker.RecordDispatch("East US"));
        }

        [TestMethod]
        [Description("Exercises the production retry-policy wrapper and gateway HTTP transport, recording URI, token, DTX headers, 449 suppression, and payload across inner failover, same-token outer replay, and post-abort rotation.")]
        public async Task ExecuteTransactionAsync_ThroughProductionGatewayPipeline_RecordsCompleteDispatchSequence()
        {
            Uri accountEndpoint = new Uri("https://account.documents.azure.com/");
            Uri eastEndpoint = new Uri("https://account-eastus.documents.azure.com/");
            Uri westEndpoint = new Uri("https://account-westus.documents.azure.com/");
            AccountProperties accountProperties = new AccountProperties
            {
                EnableMultipleWriteLocations = true,
                Consistency = new AccountConsistency
                {
                    DefaultConsistencyLevel = Cosmos.ConsistencyLevel.Session,
                },
                ReadLocationsInternal = new System.Collections.ObjectModel.Collection<AccountRegion>
                {
                    new AccountRegion { Name = "East US", Endpoint = eastEndpoint.ToString() },
                    new AccountRegion { Name = "West US", Endpoint = westEndpoint.ToString() },
                },
                WriteLocationsInternal = new System.Collections.ObjectModel.Collection<AccountRegion>
                {
                    new AccountRegion { Name = "East US", Endpoint = eastEndpoint.ToString() },
                    new AccountRegion { Name = "West US", Endpoint = westEndpoint.ToString() },
                },
            };
            List<(Uri Uri, Guid Token, string IsRetry, string IsCrossRegionRedirect, string NoRetryOn449, byte[] Payload)> dispatches = new();
            RecordingHttpMessageHandler recordingHandler = new RecordingHttpMessageHandler(async request =>
            {
                if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath == "/")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            Newtonsoft.Json.JsonConvert.SerializeObject(accountProperties),
                            Encoding.UTF8,
                            "application/json"),
                    };
                }

                int sendIndex = dispatches.Count;
                dispatches.Add((
                    request.RequestUri,
                    Guid.Parse(GetRequiredHeader(request, HttpConstants.HttpHeaders.IdempotencyToken)),
                    GetRequiredHeader(request, DistributedTransactionConstants.IsDtxRetry),
                    GetRequiredHeader(request, DistributedTransactionConstants.IsDtxCrossRegionRedirect),
                    GetRequiredHeader(request, HttpConstants.HttpHeaders.NoRetryOn449StatusCode),
                    await request.Content.ReadAsByteArrayAsync()));

                switch (sendIndex)
                {
                    case 0:
                        return CreateHttpResponse(
                            HttpStatusCode.Forbidden,
                            SubStatusCodes.WriteForbidden);
                    case 1:
                        return CreateHttpResponse(
                            (HttpStatusCode)StatusCodes.RetryWith,
                            SubStatusCodes.DtcCoordinatorRaceConflict,
                            "{\"isRetriable\":true}");
                    case 2:
                        return CreateHttpResponse(
                            (HttpStatusCode)StatusCodes.TransactionAborted,
                            SubStatusCodes.Unknown,
                            "{\"isRetriable\":true}");
                    default:
                        return CreateHttpResponse(
                            HttpStatusCode.OK,
                            SubStatusCodes.Unknown,
                            BuildDtcResponseJson(new[] { (200, (string)null) }));
                }
            });

            ConnectionPolicy connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Gateway,
            };
            connectionPolicy.PreferredLocations.Add("East US");

            using DocumentClient documentClient = new DocumentClient(
                accountEndpoint,
                MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey,
                recordingHandler,
                connectionPolicy);
            await documentClient.EnsureValidClientAsync(NoOpTrace.Singleton);

            using CosmosClient client = new CosmosClient(
                accountEndpoint.ToString(),
                MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ApplicationPreferredRegions = new List<string> { "East US" },
                },
                documentClient);

            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            mockContext
                .Setup(context => context.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, Cosmos.PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (resourceUri, resourceType, operationType, requestOptions, container, partitionKey, itemId, stream, enricher, trace, cancellationToken) =>
                        client.ClientContext.ProcessResourceOperationStreamAsync(
                            resourceUri,
                            resourceType,
                            operationType,
                            requestOptions,
                            container,
                            partitionKey,
                            itemId,
                            stream,
                            enricher,
                            trace,
                            cancellationToken));

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(
                NoOpTrace.Singleton,
                CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            Assert.AreEqual(4, dispatches.Count);
            CollectionAssert.AreEqual(
                new[]
                {
                    new Uri(eastEndpoint, "operations/dtc"),
                    new Uri(westEndpoint, "operations/dtc"),
                    new Uri(eastEndpoint, "operations/dtc"),
                    new Uri(eastEndpoint, "operations/dtc"),
                },
                dispatches.Select(dispatch => dispatch.Uri).ToArray());

            Assert.AreEqual(dispatches[0].Token, dispatches[1].Token, "Inner failover must retain the token.");
            Assert.AreEqual(dispatches[1].Token, dispatches[2].Token, "A non-aborted outer retry must replay the token.");
            Assert.AreNotEqual(dispatches[2].Token, dispatches[3].Token, "An aborted attempt must rotate the token.");

            CollectionAssert.AreEqual(
                new[]
                {
                    (bool.FalseString, bool.FalseString),
                    (bool.TrueString, bool.TrueString),
                    (bool.TrueString, bool.TrueString),
                    (bool.FalseString, bool.FalseString),
                },
                dispatches.Select(dispatch => (dispatch.IsRetry, dispatch.IsCrossRegionRedirect)).ToArray());

            Assert.IsTrue(dispatches.All(dispatch => dispatch.NoRetryOn449 == bool.TrueString));
            Assert.IsTrue(dispatches[0].Payload.Length > 0);
            foreach ((Uri Uri, Guid Token, string IsRetry, string IsCrossRegionRedirect, string NoRetryOn449, byte[] Payload) dispatch in dispatches.Skip(1))
            {
                CollectionAssert.AreEqual(dispatches[0].Payload, dispatch.Payload);
            }
        }

        /// <summary>
        /// Stands in for <see cref="ClientRetryPolicy"/> driving one attempt across a write-region
        /// boundary, recording the signals the attempt started with.
        /// </summary>
        /// <remarks>
        /// The crossing is simulated because the committer's contract is only that one tracker spans the
        /// attempts of a single token and is replaced when the token rotates; deriving the signals from
        /// pinned endpoints is <see cref="ClientRetryPolicyTests"/>' concern.
        /// </remarks>
        private static void DriveCrossRegionCrossingForAttempt(
            Action<RequestMessage> enricher,
            List<DistributedTransactionDispatchTracker> capturedTrackers,
            List<(bool IsRetry, bool IsCrossRegionRedirect)> signalsAtAttemptStart)
        {
            DistributedTransactionDispatchTracker tracker = CaptureDispatchTracker(
                enricher, OperationType.CommitDistributedTransaction);

            capturedTrackers.Add(tracker);

            signalsAtAttemptStart.Add(tracker.RecordDispatch("East US"));
            tracker.RecordDispatch("West US");
        }

        private static DistributedTransactionDispatchTracker CaptureDispatchTracker(
            Action<RequestMessage> enricher,
            OperationType operationType,
            Action<Guid, DistributedTransactionDispatchTracker> capture = null)
        {
            using (RequestMessage request = new RequestMessage
            {
                ResourceType = ResourceType.DistributedTransactionBatch,
                OperationType = operationType,
            })
            {
                enricher(request);

                Assert.IsFalse(
                    request.IsPropertiesInitialized,
                    "Publishing the DTX tracker must not initialize or mutate the public Properties dictionary.");

                Guid idempotencyToken = Guid.Parse(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]);
                if (request.DistributedTransactionDispatchTracker != null)
                {
                    Assert.AreEqual(
                        idempotencyToken,
                        request.DistributedTransactionDispatchTracker.IdempotencyToken,
                        "The idempotency header and dispatch tracker must describe the same logical attempt.");
                }

                capture?.Invoke(
                    idempotencyToken,
                    request.DistributedTransactionDispatchTracker);
                return request.DistributedTransactionDispatchTracker;
            }
        }

        [TestMethod]
        [Description("FastResponse retry model: a durably Aborted (HTTP 452) response marked isRetriable:true is retried until success.")]
        public async Task CommitTransaction_RetriesWhenRetriableAndAborted_ThenSucceeds()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return Task.FromResult(
                            new ResponseMessage((HttpStatusCode)StatusCodes.TransactionAborted)
                            {
                                Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"isRetriable\":true}"))
                            });
                    }

                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(2, callCount);
            }
        }
        [DataTestMethod]
        [Description("FastResponse retry matrix (isRetriable=false): a non-retriable outcome is never retried, regardless of the transaction status — including a durably Aborted (HTTP 452) transaction. The response is returned after a single call.")]
        [DataRow((int)StatusCodes.TransactionAborted, DisplayName = "isRetriable:false + Aborted (452) — no retry")]
        [DataRow((int)HttpStatusCode.ServiceUnavailable, DisplayName = "isRetriable:false + non-aborted (503) — no retry")]
        public async Task CommitTransaction_DoesNotRetryWhenNotRetriable(int statusCode)
        {
            int callCount = 0;
            string json = "{\"isRetriable\":false}";
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(
                        new ResponseMessage((HttpStatusCode)statusCode)
                        {
                            Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
                        });
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual((HttpStatusCode)statusCode, response.StatusCode);
                Assert.IsFalse(response.IsRetriable);
                Assert.AreEqual(1, callCount, "A non-retriable outcome must never be retried, even when durably Aborted.");
            }
        }

        [TestMethod]
        [Description("FastResponse retry matrix (isRetriable=true, not durably Aborted): N consecutive non-aborted retriable outcomes produce N+1 wire attempts that ALL replay the same idempotency token — the token is only rotated once an attempt is terminally Aborted (spec §4.2).")]
        public async Task CommitTransaction_NRetriableNonAbortedReplaysSameToken()
        {
            const int retriableNonAbortedCount = 4;
            int callCount = 0;
            List<string> capturedTokens = new List<string>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                mockContext,
                (stream, enricher) =>
                {
                    RequestMessage request = new RequestMessage
                    {
                        ResourceType = ResourceType.DistributedTransactionBatch,
                        OperationType = OperationType.CommitDistributedTransaction,
                    };
                    enricher(request);
                    capturedTokens.Add(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]);
                },
                () =>
                {
                    callCount++;
                    return callCount <= retriableNonAbortedCount
                        ? Task.FromResult(CreateRetriableNonAbortedResponseMessage())
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 2));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(count: 2),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.IsTrue(response.IsSuccessStatusCode);
            }

            Assert.AreEqual(retriableNonAbortedCount + 1, callCount, "Expected N non-aborted retriable outcomes followed by one success.");
            Assert.AreEqual(retriableNonAbortedCount + 1, capturedTokens.Count,
                "Each wire attempt — including the first — must stamp an idempotency token.");
            Assert.AreEqual(1, new HashSet<string>(capturedTokens).Count,
                "All attempts must replay the SAME idempotency token; a non-aborted retriable outcome never rotates the token.");
            Assert.IsFalse(capturedTokens.Contains(Guid.Empty.ToString()),
                "Every attempt must carry a real (non-empty) idempotency token.");
        }

        [TestMethod]
        [Description("FastResponse retry matrix, mixed transitions: a durably Aborted retriable outcome rotates to a NEW token, while a subsequent non-aborted retriable outcome replays the SAME token. Verifies the token strategy is decided per-response from its durable status (spec §4.2).")]
        public async Task CommitTransaction_AbortThenNonAbort_RotatesThenReplaysToken()
        {
            int callCount = 0;
            List<string> capturedTokens = new List<string>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                mockContext,
                (stream, enricher) =>
                {
                    RequestMessage request = new RequestMessage
                    {
                        ResourceType = ResourceType.DistributedTransactionBatch,
                        OperationType = OperationType.CommitDistributedTransaction,
                    };
                    enricher(request);
                    capturedTokens.Add(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]);
                },
                () =>
                {
                    callCount++;
                    switch (callCount)
                    {
                        // Attempt 1: durably Aborted → the next attempt must rotate to a NEW token.
                        case 1:
                            return Task.FromResult(CreateRetriableErrorResponseMessage());
                        // Attempt 2 (new token): not aborted → the next attempt must REPLAY the same token.
                        case 2:
                            return Task.FromResult(CreateRetriableNonAbortedResponseMessage());
                        // Attempt 3 (same token as attempt 2) succeeds.
                        default:
                            return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                    }
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(3, callCount);
            }

            Assert.AreEqual(3, capturedTokens.Count, "Three wire attempts expected.");
            Assert.AreNotEqual(capturedTokens[0], capturedTokens[1],
                "After a durable Abort, the retry must rotate to a NEW idempotency token.");
            Assert.AreEqual(capturedTokens[1], capturedTokens[2],
                "After a non-aborted retriable outcome, the retry must replay the SAME idempotency token.");
        }

        [TestMethod]
        [Description("Verifies that a pre-cancelled CancellationToken causes ExecuteTransactionAsync to throw immediately without issuing any network request.")]
        public async Task CommitTransaction_RespectsCancellationToken_PreCancelled()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                cts.Cancel();

                Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
                this.SetupProcessResourceOperation(
                    mockContext,
                    () => throw new InvalidOperationException("Should not be called on a pre-cancelled token."));

                DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, cts.Token));

                this.VerifyProcessResourceOperationCallCount(mockContext, Times.Never());
            }
        }

        [DataTestMethod]
        [DataRow(false, DisplayName = "Cancellation before a same-token retry")]
        [DataRow(true, DisplayName = "Cancellation before a new-token retry after abort")]
        [Description("Verifies that cancelling the token during the retry delay causes OperationCanceledException to propagate rather than proceeding with the next attempt.")]
        public async Task CommitTransaction_CancelledDuringRetryDelay_ThrowsOperationCanceledException(bool isAborted)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                int callCount = 0;
                int delayCount = 0;
                List<Guid> publishedTokens = new List<Guid>();
                List<string> capturedRequestTokens = new List<string>();
                Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
                this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                    mockContext,
                    (stream, enricher) =>
                    {
                        using RequestMessage request = new RequestMessage
                        {
                            ResourceType = ResourceType.DistributedTransactionBatch,
                            OperationType = OperationType.CommitDistributedTransaction,
                        };
                        enricher(request);
                        capturedRequestTokens.Add(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]);
                    },
                    () =>
                    {
                        callCount++;
                        return Task.FromResult(isAborted
                            ? CreateRetriableErrorResponseMessage()
                            : CreateRetriableNonAbortedResponseMessage());
                    });

                DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                    CreateTestOperations(),
                    mockContext.Object,
                    OperationType.CommitDistributedTransaction,
                    retryBaseDelay: TimeSpan.Zero,
                    delayProvider: (delay, token) =>
                    {
                        delayCount++;
                        Assert.AreEqual(cts.Token, token);
                        Assert.IsFalse(token.IsCancellationRequested,
                            "The response must be parsed before cancellation is triggered at the retry delay.");
                        cts.Cancel();
                        return Task.FromCanceled(token);
                    },
                    onDispatch: token => publishedTokens.Add(token));

                TaskCanceledException cancellation = await Assert.ThrowsExceptionAsync<TaskCanceledException>(
                    () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, cts.Token));

                Assert.AreEqual(cts.Token, cancellation.CancellationToken);
                Assert.AreEqual(1, delayCount, "Cancellation must occur at the retry delay, not during response parsing.");
                Assert.AreEqual(1, callCount);
                Assert.AreEqual(1, capturedRequestTokens.Count);
                Assert.AreEqual(1, publishedTokens.Count, "No token may be published for an unsent next attempt.");
                Assert.AreNotEqual(Guid.Empty, publishedTokens[0]);
                Assert.AreEqual(capturedRequestTokens[0], publishedTokens[0].ToString());
                this.VerifyProcessResourceOperationCallCount(mockContext, Times.Once());
            }
        }

        [TestMethod]
        [Description("Verifies that the committer retries on multiple consecutive isRetriable responses and eventually returns the success response.")]
        public async Task CommitTransaction_MultipleRetriesThenSuccessOnLastAttempt()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    if (callCount <= 3)
                    {
                        return Task.FromResult(CreateRetriableErrorResponseMessage());
                    }

                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                // 3 retriable failures + 1 success = 4 total calls.
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(4, callCount);
            }
        }

        [TestMethod]
        [Description("Verifies that a non-CosmosException thrown from the pipeline propagates immediately without retrying.")]
        public async Task CommitTransaction_NonCosmosException_PropagatesImmediately()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromException<ResponseMessage>(new IOException("Network error"));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            IOException ex = await Assert.ThrowsExceptionAsync<IOException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

            Assert.AreEqual("Network error", ex.Message);
            Assert.AreEqual(1, callCount);
        }

        [TestMethod]
        [Description("Verifies that the SDK sends byte-for-byte identical request bodies but a NEW, distinct idempotency token on every outer-loop attempt. Per DistributedTransactionFastResponseMode.md §4.2, each retriable-abort retry is a new logical attempt that MUST use a fresh idempotency token; the prior token remains terminally Aborted and must never be replayed. The serialized body is reused unchanged so the coordinator re-prepares from the identical payload under the new token.")]
        public async Task CommitTransaction_SendsNewIdempotencyTokenOnEachRetry()
        {
            int callCount = 0;
            List<string> capturedTokens = new List<string>();
            List<byte[]> capturedBodies = new List<byte[]>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                mockContext,
                (stream, enricher) =>
                {
                    using (MemoryStream copy = new MemoryStream())
                    {
                        long originalPosition = stream.CanSeek ? stream.Position : 0;
                        if (stream.CanSeek)
                        {
                            stream.Position = 0;
                        }

                        stream.CopyTo(copy);
                        capturedBodies.Add(copy.ToArray());

                        if (stream.CanSeek)
                        {
                            stream.Position = originalPosition;
                        }
                    }

                    RequestMessage request = new RequestMessage
                    {
                        ResourceType = ResourceType.DistributedTransactionBatch,
                        OperationType = OperationType.CommitDistributedTransaction,
                    };
                    enricher(request);
                    capturedTokens.Add(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]);
                },
                () =>
                {
                    callCount++;
                    return callCount < 3
                        ? Task.FromResult(CreateRetriableErrorResponseMessage())
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 2));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(count: 2),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(3, callCount);
            }

            Assert.AreEqual(3, capturedTokens.Count, "Three attempts expected: two retriable failures plus one success.");
            Assert.AreEqual(3, new HashSet<string>(capturedTokens).Count,
                "Each attempt must use a NEW, distinct idempotency token; the prior aborted token must never be replayed.");
            Assert.IsFalse(capturedTokens.Contains(Guid.Empty.ToString()),
                "Every attempt must carry a real (non-empty) idempotency token.");

            Assert.AreEqual(3, capturedBodies.Count);
            Assert.IsTrue(capturedBodies[0].Length > 0, "Captured body must be non-empty.");
            CollectionAssert.AreEqual(capturedBodies[0], capturedBodies[1],
                "Retry attempt #2 must send a byte-for-byte identical request body.");
            CollectionAssert.AreEqual(capturedBodies[0], capturedBodies[2],
                "Retry attempt #3 must send a byte-for-byte identical request body.");
        }

        [TestMethod]
        [Description("Verifies that the first attempt gets a freshly rotated idempotency token and that N retriable-abort responses produce N+1 distinct tokens across attempts (spec §4.2).")]
        public async Task CommitTransaction_NRetriableAbortsProduceNPlusOneDistinctTokens()
        {
            const int retriableAbortCount = 4;
            int callCount = 0;
            List<string> capturedTokens = new List<string>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                mockContext,
                (stream, enricher) =>
                {
                    RequestMessage request = new RequestMessage
                    {
                        ResourceType = ResourceType.DistributedTransactionBatch,
                        OperationType = OperationType.CommitDistributedTransaction,
                    };
                    enricher(request);
                    capturedTokens.Add(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]);
                },
                () =>
                {
                    callCount++;
                    return callCount <= retriableAbortCount
                        ? Task.FromResult(CreateRetriableErrorResponseMessage())
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 2));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(count: 2),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.IsTrue(response.IsSuccessStatusCode);
            }

            Assert.AreEqual(retriableAbortCount + 1, callCount, "Expected N retriable aborts followed by one success.");
            Assert.AreEqual(retriableAbortCount + 1, capturedTokens.Count,
                "Each wire attempt — including the first — must stamp an idempotency token.");
            Assert.AreEqual(retriableAbortCount + 1, new HashSet<string>(capturedTokens).Count,
                "N retriable aborts must produce N+1 distinct idempotency tokens (a fresh token per attempt, including the first).");
        }

        [TestMethod]
        [Description("Verifies the onDispatch callback fires once per wire attempt (including the first) with the freshly rotated idempotency token, so the published token always matches the token stamped on that attempt's request (spec §4.4).")]
        public async Task CommitTransaction_OnDispatchCallback_PublishesRotatedTokenPerAttempt()
        {
            const int retriableAbortCount = 3;
            int callCount = 0;
            List<string> capturedRequestTokens = new List<string>();
            List<Guid> publishedTokens = new List<Guid>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                mockContext,
                (stream, enricher) =>
                {
                    RequestMessage request = new RequestMessage
                    {
                        ResourceType = ResourceType.DistributedTransactionBatch,
                        OperationType = OperationType.CommitDistributedTransaction,
                    };
                    enricher(request);
                    capturedRequestTokens.Add(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]);
                },
                () =>
                {
                    callCount++;
                    return callCount <= retriableAbortCount
                        ? Task.FromResult(CreateRetriableErrorResponseMessage())
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                retryBaseDelay: TimeSpan.Zero,
                onDispatch: token => publishedTokens.Add(token));

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.IsTrue(response.IsSuccessStatusCode);
            }

            Assert.AreEqual(retriableAbortCount + 1, publishedTokens.Count,
                "onDispatch must fire exactly once per wire attempt (including the first).");
            CollectionAssert.AreEqual(
                capturedRequestTokens,
                publishedTokens.Select(t => t.ToString()).ToList(),
                "Each published token must equal the idempotency token stamped on that attempt's request.");
            Assert.IsFalse(publishedTokens.Contains(Guid.Empty),
                "No published token may be Guid.Empty — every dispatch carries a real token.");
        }

        [TestMethod]
        [Description("When cancellation fires at the retry boundary (via the injected delay provider) after an attempt has dispatched, the commit throws OperationCanceledException but the last token published to onDispatch equals the last dispatched request token — never Guid.Empty. Spec §4.4: cancellation preserves the latest token that reached dispatch.")]
        public async Task CommitTransaction_CancelledAtRetryBoundary_RetainsLastDispatchedToken()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                int callCount = 0;
                List<string> capturedRequestTokens = new List<string>();
                List<Guid> publishedTokens = new List<Guid>();
                Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
                this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                    mockContext,
                    (stream, enricher) =>
                    {
                        RequestMessage request = new RequestMessage
                        {
                            ResourceType = ResourceType.DistributedTransactionBatch,
                            OperationType = OperationType.CommitDistributedTransaction,
                        };
                        enricher(request);
                        capturedRequestTokens.Add(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]);
                    },
                    () =>
                    {
                        callCount++;
                        return Task.FromResult(CreateRetriableErrorResponseMessage());
                    });

                // Injected delay provider cancels at the first retry boundary, then honours the cancelled token —
                // deterministically triggering cancellation between attempts, never mid-dispatch.
                Func<TimeSpan, CancellationToken, Task> cancelAtBoundary = (delay, token) =>
                {
                    cts.Cancel();
                    token.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                };

                DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                    CreateTestOperations(),
                    mockContext.Object,
                    OperationType.CommitDistributedTransaction,
                    retryBaseDelay: TimeSpan.Zero,
                    delayProvider: cancelAtBoundary,
                    onDispatch: token => publishedTokens.Add(token));

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, cts.Token));

                Assert.AreEqual(1, callCount, "Exactly one attempt should dispatch before cancellation at the retry boundary.");
                Assert.AreEqual(1, publishedTokens.Count, "onDispatch must have published the token of the dispatched attempt.");
                Assert.AreNotEqual(Guid.Empty, publishedTokens[publishedTokens.Count - 1],
                    "The published token must not be Guid.Empty after cancellation.");
                Assert.AreEqual(capturedRequestTokens[capturedRequestTokens.Count - 1], publishedTokens[publishedTokens.Count - 1].ToString(),
                    "The latest published token must equal the last dispatched request token, so the attempt remains identifiable after cancellation.");
            }
        }

        [DataTestMethod]
        [Description("Verifies that envelope responses without a DTX sub-status code (449 without 5352, 500 without 5411-5413) are not retried by the outer loop.")]
        [DataRow((int)StatusCodes.RetryWith, DisplayName = "449 without 5352 — no retry")]
        [DataRow((int)HttpStatusCode.InternalServerError, DisplayName = "500 without DTC sub-status — no retry")]
        public async Task CommitTransaction_DoesNotRetryOnUnrecognizedSubStatus(int statusCode)
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(CreateEmptyResponseMessage((HttpStatusCode)statusCode, subStatusCode: 0));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual((HttpStatusCode)statusCode, response.StatusCode);
                Assert.AreEqual(1, callCount, "Envelope response without a DTX sub-status code must not be retried.");
            }
        }

        [DataTestMethod]
        [Description("Verifies that DTC validation failure responses (400 with DTC-specific sub-status codes) are never retried by the outer loop.")]
        [DataRow(5405, DisplayName = "400/5405 ParseFailure")]
        [DataRow(5406, DisplayName = "400/5406 FeatureDisabled")]
        [DataRow(5407, DisplayName = "400/5407 MaxOpsExceeded")]
        [DataRow(5408, DisplayName = "400/5408 MissingIdempotencyToken")]
        [DataRow(5409, DisplayName = "400/5409 InvalidAccountName")]
        [DataRow(5410, DisplayName = "400/5410 InvalidOperation")]
        public async Task CommitTransaction_DoesNotRetryOnValidationFailure400(int subStatusCode)
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(CreateEmptyResponseMessage(HttpStatusCode.BadRequest, subStatusCode));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.AreEqual(1, callCount, $"Validation failure 400/{subStatusCode} must not be retried.");
            }
        }

        [TestMethod]
        [Description("Verifies that GetRetryDelay produces exponentially growing delays with a cap at maxExponent=5, and that each delay falls within the expected jitter range [0.5*base*2^n, 1.5*base*2^n].")]
        public async Task GetRetryDelay_ExponentialBackoff_DelaysGrowAndCapCorrectly()
        {
            const int retryCount = 7;
            TimeSpan baseDelay = TimeSpan.FromSeconds(1);
            List<TimeSpan> capturedDelays = new List<TimeSpan>();

            // Set up: retryCount retriable responses so we capture retryCount delay values.
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return callCount <= retryCount
                        ? Task.FromResult(CreateRetriableErrorResponseMessage())
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            Func<TimeSpan, CancellationToken, Task> captureDelay = (delay, _) =>
            {
                capturedDelays.Add(delay);
                return Task.CompletedTask;
            };

            // Override the cumulative delay budget so the 7-retry exponential backoff (worst case
            // cumulative ~95s with 1s base) can complete and we exercise the full backoff curve
            // including delays beyond the maxExponent cap.
            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                retryBaseDelay: baseDelay,
                delayProvider: captureDelay,
                maxCumulativeRetryDelay: TimeSpan.FromMinutes(5));

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            Assert.AreEqual(retryCount, capturedDelays.Count, "One delay per retry attempt.");

            const int maxExponent = 5;
            for (int i = 0; i < capturedDelays.Count; i++)
            {
                int exponent = Math.Min(i, maxExponent);
                double baseMs = baseDelay.TotalMilliseconds * Math.Pow(2, exponent);
                double minMs = baseMs * 0.5;
                double maxMs = baseMs * 1.5;

                Assert.IsTrue(
                    capturedDelays[i].TotalMilliseconds >= minMs && capturedDelays[i].TotalMilliseconds <= maxMs,
                    $"Attempt {i}: delay {capturedDelays[i].TotalMilliseconds:F0}ms must be in [{minMs:F0}, {maxMs:F0}]ms.");
            }

            // Delays at attempt >= maxExponent should be at the same magnitude (capped exponent).
            double cappedBase = baseDelay.TotalMilliseconds * Math.Pow(2, maxExponent);
            Assert.IsTrue(
                capturedDelays[maxExponent].TotalMilliseconds >= cappedBase * 0.5
                && capturedDelays[maxExponent].TotalMilliseconds <= cappedBase * 1.5,
                "Delay at maxExponent must be capped.");
            Assert.IsTrue(
                capturedDelays[maxExponent + 1].TotalMilliseconds >= cappedBase * 0.5
                && capturedDelays[maxExponent + 1].TotalMilliseconds <= cappedBase * 1.5,
                "Delay beyond maxExponent must still use the capped exponent, producing a similar magnitude.");
        }

        // ─── Per-operation session token tests ────────────────────────────────

        [TestMethod]
        [Description("A SessionToken set on DistributedTransactionRequestOptions is propagated to the operation's SessionToken field and serialized in the request body JSON.")]
        public async Task ExecuteTransactionAsync_PerOperationSessionToken_IsSerializedInRequestBody()
        {
            const string expectedToken = "0:1#9#4=8#5=7";
            byte[] capturedBody = null;

            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            mockContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, ResourceType, OperationType, RequestOptions, ContainerInternal, Cosmos.PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (_, _, _, _, _, _, _, stream, _, _, _) =>
                    {
                        using MemoryStream copy = new MemoryStream();
                        stream.CopyTo(copy);
                        capturedBody = copy.ToArray();
                    })
                .ReturnsAsync(CreateSuccessResponseMessage(operationCount: 1));

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = expectedToken })
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(operations, mockContext.Object, OperationType.CommitDistributedTransaction);
            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(capturedBody, "Request body must have been captured.");
            string bodyJson = Encoding.UTF8.GetString(capturedBody);
            Assert.IsTrue(bodyJson.Contains($"\"sessionToken\":\"{expectedToken}\""),
                $"Per-operation session token '{expectedToken}' must appear in the serialized request body. Body was: {bodyJson}");
        }

        [TestMethod]
        [Description("When no SessionToken is set on the per-operation options, no sessionToken field appears in the serialized request body.")]
        public async Task ExecuteTransactionAsync_NoPerOperationSessionToken_OmitsFieldFromRequestBody()
        {
            byte[] capturedBody = null;

            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            mockContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, ResourceType, OperationType, RequestOptions, ContainerInternal, Cosmos.PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (_, _, _, _, _, _, _, stream, _, _, _) =>
                    {
                        using MemoryStream copy = new MemoryStream();
                        stream.CopyTo(copy);
                        capturedBody = copy.ToArray();
                    })
                .ReturnsAsync(CreateSuccessResponseMessage(operationCount: 1));

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction);
            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(capturedBody);
            string bodyJson = Encoding.UTF8.GetString(capturedBody);
            Assert.IsFalse(bodyJson.Contains("\"sessionToken\""),
                $"sessionToken field must be absent when no per-operation session token is set. Body was: {bodyJson}");
        }

        [DataTestMethod]
        [DataRow("", DisplayName = "Empty string session token")]
        [DataRow(" ", DisplayName = "Single space session token")]
        [DataRow("   ", DisplayName = "Multi-space session token")]
        [Description("A whitespace-only or empty SessionToken on DistributedTransactionRequestOptions must be treated as absent and must not appear in the serialized request body.")]
        public async Task ExecuteTransactionAsync_WhitespaceOrEmptyPerOperationSessionToken_OmitsFieldFromRequestBody(string sessionToken)
        {
            byte[] capturedBody = null;

            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            mockContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, ResourceType, OperationType, RequestOptions, ContainerInternal, Cosmos.PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (_, _, _, _, _, _, _, stream, _, _, _) =>
                    {
                        using MemoryStream copy = new MemoryStream();
                        stream.CopyTo(copy);
                        capturedBody = copy.ToArray();
                    })
                .ReturnsAsync(CreateSuccessResponseMessage(operationCount: 1));

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = sessionToken })
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(operations, mockContext.Object, OperationType.CommitDistributedTransaction);
            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(capturedBody);
            string bodyJson = Encoding.UTF8.GetString(capturedBody);
            Assert.IsFalse(bodyJson.Contains("\"sessionToken\""),
                $"sessionToken field must be absent for whitespace/empty token '{sessionToken}'. Body was: {bodyJson}");
        }

        [TestMethod]
        [Description("Each operation independently carries its own session token in the request body.")]
        public async Task ExecuteTransactionAsync_MultipleOperations_EachCarriesOwnSessionToken()
        {
            const string token1 = "0:1#5";
            const string token2 = "1:2#8";

            byte[] capturedBody = null;

            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            mockContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, ResourceType, OperationType, RequestOptions, ContainerInternal, Cosmos.PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (_, _, _, _, _, _, _, stream, _, _, _) =>
                    {
                        using MemoryStream copy = new MemoryStream();
                        stream.CopyTo(copy);
                        capturedBody = copy.ToArray();
                    })
                .ReturnsAsync(CreateSuccessResponseMessage(operationCount: 2));

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create, 0, DatabaseName, ContainerName,
                    new PartitionKey("pk1"), id: "doc1",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = token1 }),
                new DistributedTransactionOperation(
                    OperationType.Create, 1, DatabaseName, "container2",
                    new PartitionKey("pk2"), id: "doc2",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = token2 }),
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(operations, mockContext.Object, OperationType.CommitDistributedTransaction);
            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(capturedBody);
            string bodyJson = Encoding.UTF8.GetString(capturedBody);
            Assert.IsTrue(bodyJson.Contains($"\"sessionToken\":\"{token1}\""),
                $"token1 must appear in request body. Body: {bodyJson}");
            Assert.IsTrue(bodyJson.Contains($"\"sessionToken\":\"{token2}\""),
                $"token2 must appear in request body. Body: {bodyJson}");
        }

        // ─── Diagnostics ──────────────────────────────────────────────────────────

        [TestMethod]
        [Description("Verifies that the response Diagnostics is non-null and covers the caller's trace span on a successful single-attempt commit.")]
        public async Task ExecuteTransactionAsync_Diagnostics_IsNonNullOnSuccess()
        {
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () => Task.FromResult(CreateSuccessResponseMessage(operationCount: 1)));

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (ITrace trace = Trace.GetRootTrace("CommitDistributedTransaction", TraceComponent.Batch, TraceLevel.Info))
            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(trace, CancellationToken.None))
            {
                Assert.IsNotNull(response.Diagnostics, "Diagnostics must not be null.");
                string diagnosticText = response.Diagnostics.ToString();
                Assert.IsFalse(string.IsNullOrEmpty(diagnosticText), "Diagnostics.ToString() must not be empty.");
                Assert.IsTrue(diagnosticText.Contains("CommitDistributedTransaction"),
                    "Diagnostics must be rooted at the caller-supplied parent trace, not a sibling root allocated by the committer.");
                Assert.IsTrue(diagnosticText.Contains("Execute Distributed Transaction Commit"),
                    "Diagnostics must contain the per-attempt span.");
            }
        }

        [TestMethod]
        [Description("Verifies that Diagnostics spans all retry attempts — the caller's trace is attached to the final returned response even after multiple isRetriable retries.")]
        public async Task ExecuteTransactionAsync_Diagnostics_CoversRetryAttempts()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return callCount < 3
                        ? Task.FromResult(CreateRetriableErrorResponseMessage())
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (ITrace trace = Trace.GetRootTrace("CommitDistributedTransaction", TraceComponent.Batch, TraceLevel.Info))
            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(trace, CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsNotNull(response.Diagnostics, "Diagnostics must not be null after retries.");
                string diagnosticText = response.Diagnostics.ToString();
                Assert.IsTrue(diagnosticText.Contains("CommitDistributedTransaction"),
                    "Diagnostics must be rooted at the caller-supplied parent trace across all retry attempts.");
                Assert.IsTrue(diagnosticText.Contains("Execute Distributed Transaction Commit"),
                    "Diagnostics must contain per-attempt spans covering the full commit flow.");
            }
        }

        [TestMethod]
        [Description("Verifies that DiagnosticString parsed from the wire response body is correctly propagated through ExecuteTransactionAsync — protects against accidental omission of the property assignment in the object initializer.")]
        public async Task ExecuteTransactionAsync_DiagnosticString_PropagatedFromWireResponse()
        {
            const string expectedDiagnosticString = "TransactionAbortedByCoordinator";
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () => Task.FromResult(CreateResponseMessageWithDiagnosticString(
                    HttpStatusCode.Conflict,
                    operationCount: 1,
                    diagnosticString: expectedDiagnosticString)));

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(expectedDiagnosticString, response.DiagnosticString,
                    "DiagnosticString must be propagated from the wire response body through ExecuteTransactionAsync.");
            }
        }

        [TestMethod]
        [Description("Verifies that DiagnosticString from a successful commit is propagated through ExecuteTransactionAsync and does NOT leak into ErrorMessage (which must remain null on success).")]
        public async Task ExecuteTransactionAsync_DiagnosticString_PropagatedOnSuccess_DoesNotPolluteErrorMessage()
        {
            const string expectedDiagnosticString = "TransactionCommitted";
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () => Task.FromResult(CreateResponseMessageWithDiagnosticString(
                    HttpStatusCode.OK,
                    operationCount: 1,
                    diagnosticString: expectedDiagnosticString)));

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(expectedDiagnosticString, response.DiagnosticString,
                    "DiagnosticString must be propagated even on a successful commit.");
                Assert.IsNull(response.ErrorMessage,
                    "ErrorMessage must remain null on success — the diagnostic string must NOT be merged into ErrorMessage on 2xx responses.");
            }
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private static string GetRequiredHeader(HttpRequestMessage request, string headerName)
        {
            Assert.IsTrue(
                request.Headers.TryGetValues(headerName, out IEnumerable<string> values),
                $"Expected HTTP header '{headerName}'.");
            return values.Single();
        }

        private static HttpResponseMessage CreateHttpResponse(
            HttpStatusCode statusCode,
            SubStatusCodes subStatusCode,
            string content = null)
        {
            HttpResponseMessage response = new HttpResponseMessage(statusCode);
            response.Headers.Add(
                WFConstants.BackendHeaders.SubStatus,
                ((int)subStatusCode).ToString(CultureInfo.InvariantCulture));
            if (content != null)
            {
                response.Content = new StringContent(content, Encoding.UTF8, "application/json");
            }

            return response;
        }

        private static CollectionCacheDocumentClient CreateCollectionCacheDocumentClient(ISessionContainer sessions = null)
        {
            Mock<ClientCollectionCache> cache = new Mock<ClientCollectionCache>(
                sessions, new ServerStoreModel(null), null, null, null, true, null)
            {
                CallBase = true
            };
            cache.Setup(c => c.RefreshCollectionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(),
                    It.IsAny<IClientSideRequestStatistics>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, ITrace, IClientSideRequestStatistics, CancellationToken>(
                    (apiVersion, resourceAddress, submittedRid, _, _, _) =>
                    {
                        Assert.IsTrue(PathsHelper.IsNameBased(resourceAddress));
                        Assert.IsNotNull(submittedRid);
                        Assert.AreEqual(HttpConstants.Versions.CurrentVersion, apiVersion);
                    })
                .CallBase();
            cache.Setup(c => c.ResolveByNameAsync(
                    It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<ITrace>(),
                    It.IsAny<IClientSideRequestStatistics>(), It.IsAny<CancellationToken>()))
                .Throws(new AssertFailedException("Transaction refresh must not use name-scoped session cleanup."));
            return new CollectionCacheDocumentClient(cache.Object) { sessionContainer = sessions };
        }

        private static void SetupCollectionRefresh(
            Mock<ClientCollectionCache> cache,
            Func<string, CancellationToken, Task<ContainerProperties>> resolve)
        {
            cache.Protected().Setup<Task<ContainerProperties>>(
                "GetByNameAsync", ItExpr.IsAny<string>(), ItExpr.IsAny<string>(),
                ItExpr.IsAny<ITrace>(), ItExpr.IsAny<IClientSideRequestStatistics>(),
                ItExpr.IsAny<CancellationToken>())
                .Returns<string, string, ITrace, IClientSideRequestStatistics, CancellationToken>(
                    (_, path, _, _, cancellationToken) => resolve(path, cancellationToken));
        }

        private sealed class CollectionCacheDocumentClient : MockDocumentClient
        {
            private readonly ClientCollectionCache collectionCache;

            public CollectionCacheDocumentClient(ClientCollectionCache collectionCache)
            {
                this.collectionCache = collectionCache;
            }

            internal override Task<ClientCollectionCache> GetCollectionCacheAsync(ITrace trace)
            {
                return Task.FromResult(this.collectionCache);
            }
        }

        private sealed class RecordingHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync;

            internal RecordingHttpMessageHandler(
                Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync)
            {
                this.sendAsync = sendAsync;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return this.sendAsync(request);
            }
        }

        private static string BuildDtcResponseJson(
            (int statusCode, string sessionToken)[] operations)
        {
            return BuildDtcResponseJson(
                operations.Select(o => (o.statusCode, subStatusCode: (int?)null, o.sessionToken, partitionKeyRangeId: (string)null)).ToArray());
        }

        private static string BuildDtcResponseJson(
            (int statusCode, int? subStatusCode, string sessionToken)[] operations)
        {
            return BuildDtcResponseJson(
                operations.Select(o => (o.statusCode, o.subStatusCode, o.sessionToken, partitionKeyRangeId: (string)null)).ToArray());
        }

        private static string BuildDtcResponseJson(
            (int statusCode, int? subStatusCode, string sessionToken, string partitionKeyRangeId)[] operations)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(@"{""operationResponses"":[");
            for (int i = 0; i < operations.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append($@"{{""index"":{i},""statuscode"":{operations[i].statusCode}");
                if (operations[i].subStatusCode.HasValue)
                {
                    sb.Append($@",""substatuscode"":{operations[i].subStatusCode.Value}");
                }

                if (operations[i].sessionToken != null)
                {
                    sb.Append($@",""{DistributedTransactionSerializer.SessionToken}"":""{operations[i].sessionToken}""");
                }

                if (operations[i].partitionKeyRangeId != null)
                {
                    sb.Append($@",""{DistributedTransactionSerializer.PartitionKeyRangeId}"":""{operations[i].partitionKeyRangeId}""");
                }

                sb.Append('}');
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private Mock<CosmosClientContext> CreateMockContext(
            ISessionContainer sessionContainer,
            string responseContent,
            HttpStatusCode statusCode)
        {
            MockDocumentClient documentClient = new MockDocumentClient
            {
                sessionContainer = sessionContainer
            };

            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId(CollectionResourceId);
            containerProperties.Id = "TestContainerId";
            containerProperties.PartitionKeyPath = "/pk";

            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(c => c.DocumentClient).Returns(documentClient);
            mockContext.Setup(c => c.SerializerCore).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(c => c.GetCachedContainerPropertiesAsync(
                    It.IsAny<string>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProperties);

            ResponseMessage responseMessage = new ResponseMessage(statusCode);
            if (responseContent != null)
            {
                responseMessage.Content = new MemoryStream(Encoding.UTF8.GetBytes(responseContent));
            }

            mockContext.Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    ResourceType.DistributedTransactionBatch,
                    OperationType.CommitDistributedTransaction,
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMessage);

            return mockContext;
        }

        // ─── Retry test helpers ────────────────────────────────────────────────

        private Mock<CosmosClientContext> CreateMockClientContext()
        {
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();

            mockContext.Setup(x => x.SerializerCore).Returns(MockCosmosUtil.Serializer);

            mockContext.Setup(x => x.GetCachedContainerPropertiesAsync(
                It.IsAny<string>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(ContainerProperties.CreateWithResourceId(TestCollectionResourceId));

            return mockContext;
        }

        private Mock<CosmosClientContext> CreateMockClientContext(CosmosClientOptions clientOptions)
        {
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            mockContext.Setup(x => x.ClientOptions).Returns(clientOptions);
            return mockContext;
        }

        private void SetupProcessResourceOperation(
            Mock<CosmosClientContext> mockContext,
            Func<Task<ResponseMessage>> responseFactory)
        {
            mockContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns(responseFactory);
        }

        private void SetupProcessResourceOperationWithStreamAndEnricherCapture(
            Mock<CosmosClientContext> mockContext,
            Action<Stream, Action<RequestMessage>> capture,
            Func<Task<ResponseMessage>> responseFactory)
        {
            mockContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, ResourceType, OperationType, RequestOptions, ContainerInternal, Cosmos.PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (_, _, _, _, _, _, _, stream, enricher, _, _) => capture(stream, enricher))
                .Returns(responseFactory);
        }

        private void VerifyProcessResourceOperationCallCount(
            Mock<CosmosClientContext> mockContext,
            Times times)
        {
            mockContext.Verify(c => c.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(),
                It.IsAny<ResourceType>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerInternal>(),
                It.IsAny<Cosmos.PartitionKey?>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()), times);
        }

        private static IReadOnlyList<DistributedTransactionOperation> CreateTestOperations(int count = 1)
        {
            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>(count);
            for (int i = 0; i < count; i++)
            {
                operations.Add(new DistributedTransactionOperation(
                    OperationType.Create,
                    i,
                    "testDb",
                    "testContainer",
                    Cosmos.PartitionKey.Null));
            }

            return operations;
        }

        private static ResponseMessage CreateSuccessResponseMessage(int operationCount)
        {
            StringBuilder json = new StringBuilder();
            json.Append("{\"operationResponses\":[");
            for (int i = 0; i < operationCount; i++)
            {
                if (i > 0)
                {
                    json.Append(",");
                }

                json.Append($"{{\"index\":{i},\"statuscode\":200,\"substatuscode\":0}}");
            }

            json.Append("]}");

            return new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json.ToString()))
            };
        }

        private static ResponseMessage CreateRetriableErrorResponseMessage()
        {
            // FastResponse retry model: durably Aborted (HTTP 452) AND retriable — the retry uses a NEW
            // token because the prior token is terminally consumed on the coordinator.
            string json = "{\"isRetriable\":true}";
            return new ResponseMessage((HttpStatusCode)StatusCodes.TransactionAborted)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
            };
        }

        private static ResponseMessage CreateRetriableNonAbortedResponseMessage()
        {
            // FastResponse retry model: retriable but NOT durably Aborted (any non-452 status) — the retry
            // replays the SAME token so the coordinator's duplicate detection keeps the resubmission idempotent.
            string json = "{\"isRetriable\":true}";
            return new ResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
            };
        }

        private static ResponseMessage CreateResponseMessageWithDiagnosticString(
            HttpStatusCode statusCode,
            int operationCount,
            string diagnosticString)
        {
            StringBuilder json = new StringBuilder();
            json.Append($"{{\"diagnosticString\":\"{diagnosticString}\",\"operationResponses\":[");
            for (int i = 0; i < operationCount; i++)
            {
                if (i > 0)
                {
                    json.Append(",");
                }

                json.Append($"{{\"index\":{i},\"statusCode\":{(int)statusCode},\"subStatusCode\":0}}");
            }

            json.Append("]}");

            return new ResponseMessage(statusCode)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json.ToString()))
            };
        }

        /// <summary>Creates an empty-body response with the given status and sub-status codes.</summary>
        private static ResponseMessage CreateEmptyResponseMessage(HttpStatusCode statusCode, int subStatusCode)
        {
            ResponseMessage message = new ResponseMessage(statusCode);
            message.Headers.SubStatusCodeLiteral = subStatusCode.ToString();
            return message;
        }

        /// <summary>
        /// A <see cref="System.Diagnostics.TraceListener"/> that forwards each event to a delegate,
        /// used in tests to assert that specific trace messages are emitted.
        /// </summary>
        private sealed class DelegatingTraceListener : System.Diagnostics.TraceListener
        {
            private readonly Action<System.Diagnostics.TraceEventType, string> onEvent;

            public DelegatingTraceListener(Action<System.Diagnostics.TraceEventType, string> onEvent)
                => this.onEvent = onEvent;

            public override void Write(string message) { }

            public override void WriteLine(string message) { }

            public override void TraceEvent(
                System.Diagnostics.TraceEventCache eventCache,
                string source,
                System.Diagnostics.TraceEventType eventType,
                int id,
                string format,
                params object[] args)
            {
                string message = args != null && args.Length > 0
                    ? string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args)
                    : format;
                this.onEvent(eventType, message);
            }
        }
    }
}
