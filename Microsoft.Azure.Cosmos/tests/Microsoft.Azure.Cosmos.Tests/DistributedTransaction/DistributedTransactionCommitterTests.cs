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
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
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

        [TestMethod]
        [Description("Verifies that when the DTC response carries a session token, the token is merged into the SessionContainer")]
        public async Task CommitTransactionAsync_MergesSessionTokensIntoSessionContainer()
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

            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.AreEqual(expectedToken, storedToken,
                "Session token should be assembled as {pkRangeId}:{lsn} and merged into SessionContainer after a successful DTC commit.");
        }

        [TestMethod]
        [Description("When a per-operation session token is absent, SetSessionToken is NOT called for that operation and the SessionContainer is not updated")]
        public async Task CommitTransactionAsync_SkipsMerge_WhenSessionTokenIsNull()
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

            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.IsTrue(string.IsNullOrEmpty(storedToken),
                "SessionContainer should not be updated when the operation result has no session token.");
        }

        [TestMethod]
        [Description("Verifies that the correct collectionRid and collectionFullname are passed to SetSessionToken for each operation")]
        public async Task CommitTransactionAsync_PassesCorrectCollectionToSetSessionToken()
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

            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

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
        public async Task CommitTransactionAsync_MergesSessionTokens_OnFailureResponse()
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

            DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.AreEqual(expectedToken, storedToken,
                "Session token should still be merged even when the DTC response indicates a failure.");
        }

        [TestMethod]
        [Description("When session token is LSN-only and partitionKeyRangeId is present, the token is assembled as {pkRangeId}:{lsn}")]
        public async Task CommitTransactionAsync_AssemblesSessionToken_WhenPartitionKeyRangeIdIsPresent()
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

            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.AreEqual(expectedToken, storedToken,
                "Session token should be assembled as {pkRangeId}:{lsn} when partitionKeyRangeId is present.");
        }

        [TestMethod]
        [Description("When partitionKeyRangeId is absent, merge is silently skipped")]
        public async Task CommitTransactionAsync_SkipsMerge_WhenLsnOnlyAndPartitionKeyRangeIdIsAbsent()
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

            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

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
        public async Task CommitTransactionAsync_SkipsMerge_WhenPartitionKeyRangeIdIsEmptyOrWhitespace(string pkRangeId)
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

            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.IsTrue(string.IsNullOrEmpty(storedToken),
                $"SessionContainer should not be updated when partitionKeyRangeId is '{pkRangeId}' (empty/whitespace).");
        }

        // ─── Retry / Spec-Compliance Tests ─────────────────────────────────────

        [TestMethod]
        [Description("m8: In a multi-operation response, an op whose pkRangeId is absent is skipped while " +
                     "subsequent ops with pkRangeId still have their session tokens merged correctly.")]
        public async Task CommitTransactionAsync_MultiOp_SkipsOpWithMissingPkRangeId_MergesRemainingOps()
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

            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

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
        public async Task CommitTransactionAsync_EmitsTraceWarning_WhenPartitionKeyRangeIdIsAbsent()
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
                await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);
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
        [Description("When SetSessionToken throws, the exception is swallowed and CommitTransactionAsync still returns the response rather than rethrowing")]
        public async Task CommitTransactionAsync_SwallowsSetSessionTokenException()
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
            DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);
            Assert.IsNotNull(response, "CommitTransactionAsync should return a response even when SetSessionToken throws.");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        [Description("When SetSessionToken throws OperationCanceledException, the exception must propagate — it must not be swallowed by the MergeSessionTokens catch block.")]
        public async Task CommitTransactionAsync_PropagatesOperationCanceledException_FromSetSessionToken()
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
                () => committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None),
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

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
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

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
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
                    () => committer.CommitTransactionAsync(NoOpTrace.Singleton, cts.Token));

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

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
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
                () => committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

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

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual((HttpStatusCode)statusCode, response.StatusCode);
                Assert.IsFalse(response.IsSuccessStatusCode);
                Assert.IsFalse(response.IsRetriable);
                Assert.AreEqual(1, callCount);
            }
        }

        [TestMethod]
        [Description("Verifies that a pre-cancelled CancellationToken causes CommitTransactionAsync to throw immediately without issuing any network request.")]
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
                    () => committer.CommitTransactionAsync(NoOpTrace.Singleton, cts.Token));

                this.VerifyProcessResourceOperationCallCount(mockContext, Times.Never());
            }
        }

        [TestMethod]
        [Description("Verifies that cancelling the token during the retry delay causes OperationCanceledException to propagate rather than proceeding with the next attempt.")]
        public async Task CommitTransaction_CancelledDuringRetryDelay_ThrowsOperationCanceledException()
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
                        cts.Cancel(); // Cancel after the first call so the retry delay throws.
                        return Task.FromResult(CreateRetriableErrorResponseMessage());
                    });

                // Non-zero delay so the retry path enters Task.Delay
                // the token is already cancelled synchronously in the callback, so it throws immediately.
                DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                    CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.FromMilliseconds(500));

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    () => committer.CommitTransactionAsync(NoOpTrace.Singleton, cts.Token));

                Assert.AreEqual(1, callCount);
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

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
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
                () => committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

            Assert.AreEqual("Network error", ex.Message);
            Assert.AreEqual(1, callCount);
        }

        [TestMethod]
        [Description("Verifies that the SDK sends byte-for-byte identical request bodies AND the same idempotency token on every outer-loop retry. Required so the coordinator can recognise replays via the idempotency token and safely re-prepare from the same payload (per dtx-sdk-response-status-codes.md, Part C §9: 'Aborted (SDK retry): Resets record to Preparing with new transaction ID, same idempotency token').")]
        public async Task CommitTransaction_SameBodyAndTokenSentOnEveryRetryAttempt()
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

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(3, callCount);
            }

            Assert.AreEqual(3, capturedTokens.Count, "Three attempts expected: two retriable failures plus one success.");
            Assert.AreEqual(1, new HashSet<string>(capturedTokens).Count,
                "The same idempotency token must be used on every retry attempt.");

            Assert.AreEqual(3, capturedBodies.Count);
            Assert.IsTrue(capturedBodies[0].Length > 0, "Captured body must be non-empty.");
            CollectionAssert.AreEqual(capturedBodies[0], capturedBodies[1],
                "Retry attempt #2 must send a byte-for-byte identical request body.");
            CollectionAssert.AreEqual(capturedBodies[0], capturedBodies[2],
                "Retry attempt #3 must send a byte-for-byte identical request body.");
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

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
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

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
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

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                OperationType.CommitDistributedTransaction,
                retryBaseDelay: baseDelay,
                delayProvider: captureDelay);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
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
        public async Task CommitTransactionAsync_PerOperationSessionToken_IsSerializedInRequestBody()
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
            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(capturedBody, "Request body must have been captured.");
            string bodyJson = Encoding.UTF8.GetString(capturedBody);
            Assert.IsTrue(bodyJson.Contains($"\"sessionToken\":\"{expectedToken}\""),
                $"Per-operation session token '{expectedToken}' must appear in the serialized request body. Body was: {bodyJson}");
        }

        [TestMethod]
        [Description("When no SessionToken is set on the per-operation options, no sessionToken field appears in the serialized request body.")]
        public async Task CommitTransactionAsync_NoPerOperationSessionToken_OmitsFieldFromRequestBody()
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
            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

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
        public async Task CommitTransactionAsync_WhitespaceOrEmptyPerOperationSessionToken_OmitsFieldFromRequestBody(string sessionToken)
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
            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(capturedBody);
            string bodyJson = Encoding.UTF8.GetString(capturedBody);
            Assert.IsFalse(bodyJson.Contains("\"sessionToken\""),
                $"sessionToken field must be absent for whitespace/empty token '{sessionToken}'. Body was: {bodyJson}");
        }

        [TestMethod]
        [Description("Each operation independently carries its own session token in the request body.")]
        public async Task CommitTransactionAsync_MultipleOperations_EachCarriesOwnSessionToken()
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
            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

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
        public async Task CommitTransactionAsync_Diagnostics_IsNonNullOnSuccess()
        {
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () => Task.FromResult(CreateSuccessResponseMessage(operationCount: 1)));

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using (ITrace trace = Trace.GetRootTrace("CommitDistributedTransaction", TraceComponent.Batch, TraceLevel.Info))
            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(trace, CancellationToken.None))
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
        public async Task CommitTransactionAsync_Diagnostics_CoversRetryAttempts()
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
            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(trace, CancellationToken.None))
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
        [Description("Verifies that DiagnosticString parsed from the wire response body is correctly propagated through CommitTransactionAsync — protects against accidental omission of the property assignment in the object initializer.")]
        public async Task CommitTransactionAsync_DiagnosticString_PropagatedFromWireResponse()
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

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(expectedDiagnosticString, response.DiagnosticString,
                    "DiagnosticString must be propagated from the wire response body through CommitTransactionAsync.");
            }
        }

        [TestMethod]
        [Description("Verifies that DiagnosticString from a successful commit is propagated through CommitTransactionAsync and does NOT leak into ErrorMessage (which must remain null on success).")]
        public async Task CommitTransactionAsync_DiagnosticString_PropagatedOnSuccess_DoesNotPolluteErrorMessage()
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

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(expectedDiagnosticString, response.DiagnosticString,
                    "DiagnosticString must be propagated even on a successful commit.");
                Assert.IsNull(response.ErrorMessage,
                    "ErrorMessage must remain null on success — the diagnostic string must NOT be merged into ErrorMessage on 2xx responses.");
            }
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

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
