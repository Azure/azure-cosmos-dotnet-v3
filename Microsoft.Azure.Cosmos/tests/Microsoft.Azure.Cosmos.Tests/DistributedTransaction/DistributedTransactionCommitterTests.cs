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

        // DataRow sentinel: MSTest cannot express an absent int? cleanly, and -1 is not a real substatus.
        private const int SubStatusCodeAbsent = -1;

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
                "Session token should be recorded in the SessionContainer after a successful DTC commit.");
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
        [Description("Verifies that session tokens are still merged into the SessionContainer on a 409, a failure status point operations also capture on")]
        public async Task ExecuteTransactionAsync_MergesSessionTokens_OnCapturableFailureStatus()
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
                "Session token should still be merged on a 409, which point operations also capture on.");
        }

        [DataTestMethod]
        [Description("Session token capture is gated on exactly the statuses point operations capture on: anything below 400, plus 409, 412, and 404 with a substatus other than ReadSessionNotAvailable")]
        [DataRow(200, SubStatusCodeAbsent, true, DisplayName = "200 OK is captured")]
        [DataRow(201, SubStatusCodeAbsent, true, DisplayName = "201 Created is captured")]
        [DataRow(304, SubStatusCodeAbsent, true, DisplayName = "304 NotModified is captured")]
        [DataRow(0, SubStatusCodeAbsent, true, DisplayName = "A zero status, which is also what an absent statuscode yields, is captured")]
        [DataRow(412, SubStatusCodeAbsent, true, DisplayName = "412 PreconditionFailed is captured")]
        [DataRow(409, SubStatusCodeAbsent, true, DisplayName = "409 Conflict is captured")]
        [DataRow(404, 0, true, DisplayName = "404 NotFound with an unrelated substatus is captured")]
        [DataRow(404, 1002, false, DisplayName = "404 ReadSessionNotAvailable is skipped")]
        [DataRow(424, SubStatusCodeAbsent, false, DisplayName = "424 FailedDependency is skipped")]
        [DataRow(429, 3200, false, DisplayName = "429 TooManyRequests is skipped")]
        [DataRow(410, SubStatusCodeAbsent, false, DisplayName = "410 Gone is skipped")]
        [DataRow(503, SubStatusCodeAbsent, false, DisplayName = "503 ServiceUnavailable is skipped")]
        [DataRow(408, SubStatusCodeAbsent, false, DisplayName = "408 RequestTimeout is skipped")]
        [DataRow(500, SubStatusCodeAbsent, false, DisplayName = "500 InternalServerError is skipped")]
        public async Task ExecuteTransactionAsync_CapturesSessionToken_OnPointOperationStatusesOnly(
            int operationStatusCode,
            int operationSubStatusCode,
            bool expectCaptured)
        {
            const string lsnOnly = "1#11#4=9";
            const string pkRangeId = "0";
            const string expectedToken = "0:1#11#4=9";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            string responseJson = BuildDtcResponseJson(
                new[]
                {
                    (statusCode: operationStatusCode,
                     subStatusCode: operationSubStatusCode == SubStatusCodeAbsent ? (int?)null : operationSubStatusCode,
                     sessionToken: lsnOnly,
                     partitionKeyRangeId: pkRangeId),
                });

            // MultiStatus keeps the envelope uniform across rows; per-operation statuses are what the
            // capture gate reads, so the envelope must not be what decides the outcome.
            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: responseJson,
                statusCode: (HttpStatusCode)StatusCodes.MultiStatus);

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(1), mockContext.Object, OperationType.CommitDistributedTransaction);

            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            string storedToken = sessionContainer.GetSessionToken(
                DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));

            Assert.AreEqual(
                expectCaptured ? expectedToken : string.Empty,
                storedToken,
                $"Status {operationStatusCode}/{operationSubStatusCode} should {(expectCaptured ? "be" : "not be")} captured.");
        }

        [TestMethod]
        [Description("A MultiStatus response mixing captured and skipped per-operation statuses captures only the qualifying operations")]
        public async Task ExecuteTransactionAsync_CapturesPerOperation_WhenMultiStatusMixesStatuses()
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            // Operations 0 and 1 share partition 0 so the recorded LSN proves the skip rather than just
            // the absence of a range: capturing the 503 would advance partition 0 from 11 to 99.
            string responseJson = BuildDtcResponseJson(
                new[]
                {
                    (statusCode: 200, subStatusCode: (int?)null, sessionToken: "1#11#4=9", partitionKeyRangeId: "0"),
                    (statusCode: 503, subStatusCode: (int?)null, sessionToken: "1#99#4=9", partitionKeyRangeId: "0"),
                    (statusCode: 409, subStatusCode: (int?)null, sessionToken: "1#12#4=9", partitionKeyRangeId: "1"),
                    (statusCode: 424, subStatusCode: (int?)null, sessionToken: "1#13#4=9", partitionKeyRangeId: "2"),
                });

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: responseJson,
                statusCode: (HttpStatusCode)StatusCodes.MultiStatus);

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(4), mockContext.Object, OperationType.CommitDistributedTransaction);

            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Dictionary<string, string> tokensByRange = ParseSessionTokensByRange(
                sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName)));

            CollectionAssert.AreEquivalent(
                new[] { "0", "1" },
                tokensByRange.Keys.ToArray(),
                "Only the operations whose status permits capture should reach the session container.");

            Assert.AreEqual(
                "1#11#4=9",
                tokensByRange["0"],
                "Partition 0 must keep the 200's LSN: the skipped 503 on the same partition must not advance it.");

            Assert.AreEqual(
                "1#12#4=9",
                tokensByRange["1"],
                "Partition 1 must hold the 409's LSN.");
        }

        [TestMethod]
        [Description("A 207 pairing a 200 with a 304, the shape a conditional read transaction returns, captures both tokens")]
        public async Task ExecuteTransactionAsync_CapturesBothTokens_WhenMultiStatusPairsOkWithNotModified()
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            string responseJson = BuildDtcResponseJson(
                new[]
                {
                    (statusCode: 200, subStatusCode: (int?)null, sessionToken: "1#11#4=9", partitionKeyRangeId: "0"),
                    (statusCode: 304, subStatusCode: (int?)null, sessionToken: "1#12#4=9", partitionKeyRangeId: "1"),
                });

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: responseJson,
                statusCode: (HttpStatusCode)StatusCodes.MultiStatus);

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(2), mockContext.Object, OperationType.CommitDistributedTransaction);

            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Dictionary<string, string> tokensByRange = ParseSessionTokensByRange(
                sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName)));

            CollectionAssert.AreEquivalent(
                new[] { "0", "1" },
                tokensByRange.Keys.ToArray(),
                "A 304 read observed real replica progress, so its token must be captured just as the gateway captures it.");
        }

        [TestMethod]
        [Description("An unresolvable account consistency level still merges tokens and does not surface token failures")]
        public async Task ExecuteTransactionAsync_MergesSessionTokens_WhenAccountConsistencyIsUnresolvable()
        {
            const string expectedToken = "0:1#14#4=9";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                new UnresolvableConsistencyDocumentClient { sessionContainer = sessionContainer },
                responseContent: BuildDtcResponseJson(
                    new[] { (statusCode: 200, subStatusCode: (int?)null, sessionToken: "1#14#4=9", partitionKeyRangeId: "0") }),
                statusCode: HttpStatusCode.OK);

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(1), mockContext.Object, OperationType.CommitDistributedTransaction);

            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(
                expectedToken,
                sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName)),
                "A transient account read failure must not degrade a session client to eventual consistency.");
        }

        [TestMethod]
        [Description("An unresolvable account consistency level traces a malformed token rather than throwing")]
        public async Task ExecuteTransactionAsync_DoesNotThrowOnMalformedToken_WhenAccountConsistencyIsUnresolvable()
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                new UnresolvableConsistencyDocumentClient { sessionContainer = sessionContainer },
                responseContent: BuildDtcResponseJson(
                    new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: "1#9#4=8#5=7", partitionKeyRangeId: (string)null) },
                    prefixRangeLessTokens: false),
                statusCode: HttpStatusCode.OK);

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(1), mockContext.Object, OperationType.CommitDistributedTransaction);

            DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(
                response,
                "Token failures are surfaced only when the account is known to be session consistent, so an unknown level must trace instead of throw.");

            Assert.IsTrue(
                string.IsNullOrEmpty(sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName))),
                "A malformed token must never be recorded, regardless of the resolved consistency level.");
        }

        [TestMethod]
        [Description("Capture is not gated on consistency: tokens are merged on a non-session account, matching point-operation behavior")]
        public async Task ExecuteTransactionAsync_MergesSessionTokens_OnNonSessionAccount()
        {
            const string lsnOnly = "1#14#4=9";
            const string expectedToken = "0:1#14#4=9";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: BuildDtcResponseJson(
                    new[] { (statusCode: 200, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: "0") }),
                statusCode: HttpStatusCode.OK,
                accountConsistencyLevel: Cosmos.ConsistencyLevel.Eventual);

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(1), mockContext.Object, OperationType.CommitDistributedTransaction);

            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(
                expectedToken,
                sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName)),
                "Point operations capture regardless of consistency level, so the DTx path must too.");
        }

        [TestMethod]
        [Description("A transport-level failure whose results are padded placeholders merges nothing and throws nothing")]
        public async Task ExecuteTransactionAsync_MergesNothing_WhenResultsArePaddedPlaceholders()
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: null,
                statusCode: HttpStatusCode.ServiceUnavailable);

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(2), mockContext.Object, OperationType.CommitDistributedTransaction);

            DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(response, "A transport failure must still produce a response rather than throwing.");
            Assert.AreEqual(
                string.Empty,
                sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName)),
                "Padded placeholder results carry no session token, so nothing should be captured.");
        }

        [TestMethod]
        [Description("When session token is LSN-only, the malformed token is surfaced even when partitionKeyRangeId is present")]
        public async Task ExecuteTransactionAsync_ThrowsOnLsnOnlySessionToken_WhenPartitionKeyRangeIdIsPresent()
        {
            const string lsnOnly = "1#9#4=8#5=7";
            const string pkRangeId = "0";
            SessionContainer sessionContainer = new SessionContainer("testhost");

            string responseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: pkRangeId) },
                prefixRangeLessTokens: false);

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: responseJson,
                statusCode: HttpStatusCode.OK,
                accountConsistencyLevel: Cosmos.ConsistencyLevel.Session);

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

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

            StringAssert.Contains(exception.Message, lsnOnly);
            Assert.IsTrue(string.IsNullOrEmpty(sessionContainer.GetSessionToken(
                DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName))));
        }

        [TestMethod]
        [Description("When partitionKeyRangeId is absent, merge is silently skipped")]
        public async Task ExecuteTransactionAsync_SkipsMerge_WhenLsnOnlyAndPartitionKeyRangeIdIsAbsent()
        {
            const string lsnOnly = "1#9#4=8#5=7";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            // No partitionKeyRangeId, so the token stays LSN-only: the coordinator prefixes the range id
            // onto each per-operation token and falls back to the raw value only when it cannot.
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
        [Description("m9: When an operation result carries a session token with no partitionKeyRangeId, the capture path " +
                     "emits a TraceWarning under non-Session consistency so the skip is observable in diagnostic traces.")]
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
        [Description("A malformed token on a retriable response must not surface: the retry loop has not yet decided " +
                     "the outcome, so throwing would pre-empt a retry that could still succeed.")]
        public async Task ExecuteTransactionAsync_DoesNotThrowOnMalformedToken_WhileTheResponseIsStillRetriable()
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: null,
                statusCode: HttpStatusCode.OK,
                accountConsistencyLevel: Cosmos.ConsistencyLevel.Session);

            int attempts = 0;
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    attempts++;

                    // The retriable attempt carries a malformed token; the terminal attempt is clean.
                    if (attempts == 1)
                    {
                        string retriableJson = @"{""isRetriable"":true,""operationResponses"":[{""index"":0,""statusCode"":200,""sessionToken"":""malformed""}]}";
                        return Task.FromResult(new ResponseMessage((HttpStatusCode)StatusCodes.TransactionAborted)
                        {
                            Content = new MemoryStream(Encoding.UTF8.GetBytes(retriableJson))
                        });
                    }

                    string successJson = BuildDtcResponseJson(
                        new[] { (statusCode: 200, subStatusCode: (int?)null, sessionToken: "1#3#4=2", partitionKeyRangeId: "0") });

                    return Task.FromResult(new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new MemoryStream(Encoding.UTF8.GetBytes(successJson))
                    });
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(1), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(2, attempts, "The retriable attempt must be retried rather than aborted by a token failure.");
            Assert.IsTrue(response.IsSuccessStatusCode, "The retry succeeded, so the caller sees the successful response.");
        }

        [TestMethod]
        [Description("A success envelope that also reports isRetriable is never retried, so it is the outcome the " +
                     "caller receives and a malformed token on it must surface rather than be traced as unsettled.")]
        public async Task ExecuteTransactionAsync_ThrowsOnMalformedToken_WhenSuccessEnvelopeAlsoReportsRetriable()
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: null,
                statusCode: HttpStatusCode.OK,
                accountConsistencyLevel: Cosmos.ConsistencyLevel.Session);

            int attempts = 0;
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    attempts++;

                    string json = @"{""isRetriable"":true,""operationResponses"":[{""index"":0,""statusCode"":200,""sessionToken"":""malformed""}]}";
                    return Task.FromResult(new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
                    });
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(1), mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None),
                "A success status ends the retry loop regardless of isRetriable, so the token failure is settled.");

            Assert.AreEqual(1, attempts, "A success envelope is terminal, so it must not be retried.");
            StringAssert.Contains(exception.Message, "malformed",
                "The message must include the offending value so it can be diagnosed.");

            Assert.IsTrue(
                string.IsNullOrEmpty(sessionContainer.GetSessionToken(
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName))),
                "A malformed token must never reach the session container.");
        }

        [TestMethod]
        [Description("A malformed token on a NotModified operation surfaces. NotModified is captured on the point " +
                     "operation path, so dropping it here would be the same silent degradation on a read transaction.")]
        public async Task ExecuteTransactionAsync_ThrowsOnMalformedToken_WhenOperationIsNotModified()
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: BuildDtcResponseJson(
                    new[] { (statusCode: 304, subStatusCode: (int?)null, sessionToken: "malformed", partitionKeyRangeId: (string)null) },
                    prefixRangeLessTokens: false),
                statusCode: HttpStatusCode.OK,
                accountConsistencyLevel: Cosmos.ConsistencyLevel.Session);

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(1), mockContext.Object, OperationType.CommitDistributedTransaction);

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None),
                "A NotModified operation still observed replica progress, so its token must not be dropped silently.");

            StringAssert.Contains(exception.Message, "malformed",
                "The message must include the offending value so it can be diagnosed.");
        }

        [TestMethod]
        [Description("A read transaction never commits, so a malformed token on a read must not tell the caller the " +
                     "transaction was committed.")]
        public async Task ExecuteTransactionAsync_ReportsReadOutcome_WhenMalformedTokenSurfacesOnARead()
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: null,
                statusCode: HttpStatusCode.OK,
                accountConsistencyLevel: Cosmos.ConsistencyLevel.Session);

            this.SetupProcessResourceOperation(
                mockContext,
                () => Task.FromResult(new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new MemoryStream(Encoding.UTF8.GetBytes(BuildDtcResponseJson(
                        new[] { (statusCode: 200, subStatusCode: (int?)null, sessionToken: "malformed", partitionKeyRangeId: (string)null) },
                        prefixRangeLessTokens: false)))
                }));

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(1), mockContext.Object, OperationType.Read);

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

            StringAssert.Contains(exception.Message, "read transaction completed successfully",
                "A read transaction must be described as completed, not committed.");
            Assert.IsFalse(exception.Message.Contains("was committed"),
                "A read transaction never commits, so the commit wording must not appear.");
        }

        [DataTestMethod]
        [Description("A malformed session token on a committed operation under Session consistency surfaces to the caller " +
                     "instead of silently degrading the collection to eventual consistency.")]
        [DataRow("1#9#4=8#5=7", null, "1#9#4=8#5=7", "missing the partitionKeyRangeId prefix", DisplayName = "bare LSN with no partitionKeyRangeId to prefix it")]
        [DataRow("5", null, "5", "missing the partitionKeyRangeId prefix", DisplayName = "bare number with no partitionKeyRangeId to prefix it")]
        [DataRow("garbage", "0", "garbage", "could not be parsed", DisplayName = "unparsable token alongside a valid partitionKeyRangeId")]
        [DataRow("0:garbage", null, "0:garbage", "could not be parsed", DisplayName = "valid partitionKeyRangeId with an unparsable LSN segment")]
        [DataRow("0:1#5,1:1#7", null, "0:1#5,1:1#7", "could not be parsed", DisplayName = "compound multi-partition token in a partition-local slot")]
        public async Task ExecuteTransactionAsync_ThrowsOnMalformedToken_WhenCommittedUnderSessionConsistency(
            string sessionToken,
            string partitionKeyRangeId,
            string expectedTokenInMessage,
            string expectedReason)
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: BuildDtcResponseJson(
                    new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken, partitionKeyRangeId) },
                    prefixRangeLessTokens: false),
                statusCode: HttpStatusCode.OK,
                accountConsistencyLevel: Cosmos.ConsistencyLevel.Session);

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(1), mockContext.Object, OperationType.CommitDistributedTransaction);

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None),
                "A malformed token on a committed operation under Session consistency must surface.");

            StringAssert.Contains(exception.Message, "index 0",
                "The message must identify which operation carried the malformed token.");
            StringAssert.Contains(exception.Message, expectedTokenInMessage,
                "The message must include the offending value so it can be diagnosed.");
            StringAssert.Contains(exception.Message, expectedReason,
                "The message must state why the token could not be recorded, not a fixed reason.");
            StringAssert.Contains(exception.Message, DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName),
                "The message must name the collection whose progress was lost.");
            StringAssert.Contains(exception.Message, "should not be retried",
                "The message must state the transaction already committed so callers do not double-apply it.");

            Assert.IsTrue(
                string.IsNullOrEmpty(sessionContainer.GetSessionToken(
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName))),
                "A malformed token must never reach the session container.");
        }

        [TestMethod]
        [Description("When several operations carry malformed tokens, the failure surfaces on the first one and names that index.")]
        public async Task ExecuteTransactionAsync_ThrowsOnFirstMalformedToken_WhenSeveralAreMalformed()
        {
            const string container0 = "Container0";
            const string container1 = "Container1";
            const string container2 = "Container2";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: BuildDtcResponseJson(new[]
                {
                    (statusCode: 200, subStatusCode: (int?)null, sessionToken: "1#3#4=2", partitionKeyRangeId: "0"),
                    (statusCode: 200, subStatusCode: (int?)null, sessionToken: "malformedfirst", partitionKeyRangeId: (string)null),
                    (statusCode: 200, subStatusCode: (int?)null, sessionToken: "malformedsecond", partitionKeyRangeId: (string)null)
                }),
                statusCode: HttpStatusCode.OK,
                accountConsistencyLevel: Cosmos.ConsistencyLevel.Session);

            // Distinct collections so that the token recorded before the failure and the tokens skipped
            // after it occupy separate session container slots.
            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create, operationIndex: 0,
                    DatabaseName, container0, new PartitionKey("pk0"), id: "doc0"),
                new DistributedTransactionOperation(
                    OperationType.Create, operationIndex: 1,
                    DatabaseName, container1, new PartitionKey("pk1"), id: "doc1"),
                new DistributedTransactionOperation(
                    OperationType.Create, operationIndex: 2,
                    DatabaseName, container2, new PartitionKey("pk2"), id: "doc2"),
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction);

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

            StringAssert.Contains(exception.Message, "index 1",
                "The failure must name the first malformed operation.");
            StringAssert.Contains(exception.Message, "malformedfirst");
            Assert.IsFalse(exception.Message.Contains("malformedsecond"),
                "Validation must stop at the first malformed token rather than accumulating every failure.");

            Assert.AreEqual(
                "0:1#3#4=2",
                sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, container0)),
                "Operations validated before the failure keep the progress they already recorded.");

            Assert.IsTrue(
                string.IsNullOrEmpty(sessionContainer.GetSessionToken(
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, container1))),
                "The collection carrying the first malformed token must not be recorded.");

            Assert.IsTrue(
                string.IsNullOrEmpty(sessionContainer.GetSessionToken(
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, container2))),
                "Validation stops at the first failure, so collections after it are never reached.");
        }

        [DataTestMethod]
        [Description("Under a non-Session effective consistency a malformed token carries no guarantee the caller relies on, " +
                     "so it is traced and skipped while its siblings still merge.")]
        [DataRow(Cosmos.ConsistencyLevel.Strong, DisplayName = "Strong")]
        [DataRow(Cosmos.ConsistencyLevel.BoundedStaleness, DisplayName = "BoundedStaleness")]
        [DataRow(Cosmos.ConsistencyLevel.ConsistentPrefix, DisplayName = "ConsistentPrefix")]
        [DataRow(Cosmos.ConsistencyLevel.Eventual, DisplayName = "Eventual")]
        public async Task ExecuteTransactionAsync_SkipsMalformedToken_UnderNonSessionConsistency(
            Cosmos.ConsistencyLevel accountConsistencyLevel)
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: BuildDtcResponseJson(new[]
                {
                    (statusCode: 200, subStatusCode: (int?)null, sessionToken: "malformed", partitionKeyRangeId: (string)null),
                    (statusCode: 200, subStatusCode: (int?)null, sessionToken: "1#3#4=2", partitionKeyRangeId: "1")
                }),
                statusCode: HttpStatusCode.OK,
                accountConsistencyLevel: accountConsistencyLevel);

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(2), mockContext.Object, OperationType.CommitDistributedTransaction);

            DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(
                "1:1#3#4=2",
                sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName)),
                "A malformed token must not prevent its siblings from recording their progress.");
        }

        [DataTestMethod]
        [Description("A malformed token on an operation the server already failed must never replace the server's error: " +
                     "the caller still receives the status they need to act on.")]
        [DataRow(409, HttpStatusCode.Conflict, DisplayName = "409 Conflict")]
        [DataRow(412, HttpStatusCode.PreconditionFailed, DisplayName = "412 PreconditionFailed")]
        public async Task ExecuteTransactionAsync_SkipsMalformedToken_WhenOperationFailedUnderSessionConsistency(
            int operationStatusCode,
            HttpStatusCode envelopeStatusCode)
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: BuildDtcResponseJson(
                    new[] { (operationStatusCode, subStatusCode: (int?)null, sessionToken: "malformed", partitionKeyRangeId: (string)null) }),
                statusCode: envelopeStatusCode,
                accountConsistencyLevel: Cosmos.ConsistencyLevel.Session);

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(1), mockContext.Object, OperationType.CommitDistributedTransaction);

            DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(envelopeStatusCode, response.StatusCode,
                "The server's error must reach the caller rather than being masked by a token-validation failure.");
        }

        [DataTestMethod]
        [Description("Effective consistency is the client override when one is set, and the account default otherwise: " +
                     "the same precedence the point-operation path applies.")]
        [DataRow(Cosmos.ConsistencyLevel.Eventual, Cosmos.ConsistencyLevel.Session, true,
            DisplayName = "client override to Session on an Eventual account surfaces the failure")]
        [DataRow(Cosmos.ConsistencyLevel.Session, Cosmos.ConsistencyLevel.Eventual, false,
            DisplayName = "client override to Eventual on a Session account suppresses the failure")]
        public async Task ExecuteTransactionAsync_UsesClientConsistencyOverride_ToGradeMalformedTokens(
            Cosmos.ConsistencyLevel accountConsistencyLevel,
            Cosmos.ConsistencyLevel clientConsistencyOverride,
            bool expectThrow)
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: BuildDtcResponseJson(
                    new[] { (statusCode: 200, subStatusCode: (int?)null, sessionToken: "malformed", partitionKeyRangeId: (string)null) }),
                statusCode: HttpStatusCode.OK,
                accountConsistencyLevel: accountConsistencyLevel);

            mockContext
                .Setup(c => c.ClientOptions)
                .Returns(new CosmosClientOptions { ConsistencyLevel = clientConsistencyOverride });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(1), mockContext.Object, OperationType.CommitDistributedTransaction);

            if (expectThrow)
            {
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None),
                    "The client override, not the account default, decides whether the failure surfaces.");
            }
            else
            {
                DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [TestMethod]
        [Description("A SetSessionToken failure and a malformed token cost the caller the same guarantee, so they are graded " +
                     "by one policy: both surface on a committed operation under Session consistency.")]
        public async Task ExecuteTransactionAsync_SurfacesSetSessionTokenException_WhenCommittedUnderSessionConsistency()
        {
            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();
            mockSessionContainer
                .Setup(s => s.SetSessionToken(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<INameValueCollection>()))
                .Throws(new InvalidOperationException("simulated SetSessionToken failure"));

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                mockSessionContainer.Object,
                responseContent: BuildDtcResponseJson(
                    new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: "1#9#4=8#5=7", partitionKeyRangeId: "0") }),
                statusCode: HttpStatusCode.OK,
                accountConsistencyLevel: Cosmos.ConsistencyLevel.Session);

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(1), mockContext.Object, OperationType.CommitDistributedTransaction);

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

            StringAssert.Contains(exception.Message, "index 0");
            Assert.IsNotNull(exception.InnerException,
                "The originating failure must be preserved so the cause is diagnosable.");
            StringAssert.Contains(exception.InnerException.Message, "simulated SetSessionToken failure");
        }

        [TestMethod]
        [Description("When SetSessionToken throws under a non-Session effective consistency, the exception is swallowed and ExecuteTransactionAsync still returns the response rather than rethrowing")]
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
                    () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, cts.Token));

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

        [TestMethod]
        [Description("Verifies that capture stops at the first malformed token: tokens recorded before it survive, and later operations' valid tokens are deliberately not recorded. The session guarantee is already broken at that point, so a partially advanced token would read no more correctly than the abandoned one.")]
        public async Task ExecuteTransactionAsync_ThrowsAtFirstMalformedToken_AndDoesNotRecordLaterTokens()
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: BuildDtcResponseJson(
                    new[]
                    {
                        (statusCode: 201, sessionToken: "0:1#5"),
                        (statusCode: 201, sessionToken: "not-a-token"),
                        (statusCode: 201, sessionToken: "2:1#9"),
                    }),
                statusCode: HttpStatusCode.OK,
                accountConsistencyLevel: Cosmos.ConsistencyLevel.Session);

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(3), mockContext.Object, OperationType.CommitDistributedTransaction);

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

            StringAssert.Contains(exception.Message, "index 1",
                "The first malformed token must be the one reported.");

            string recorded = sessionContainer.GetSessionToken(
                DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));

            StringAssert.Contains(recorded, "0:1#5",
                "A token already recorded before the failure must not be rolled back.");
            Assert.IsFalse(recorded.Contains("2:1#9"),
                "Capture must stop at the first malformed token; a later operation's token is deliberately abandoned.");
        }

        [TestMethod]
        [Description("Verifies that a malformed token is only traced when another operation failed. A failure anywhere " +
                     "rolls the transaction back, so no operation left a durable write to read back, and throwing would " +
                     "replace the server's own actionable error with a bookkeeping exception.")]
        public async Task ExecuteTransactionAsync_TracesMalformedToken_WhenAnotherOperationFailed()
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: BuildDtcResponseJson(
                    new[]
                    {
                        // Conflict rolls the transaction back, so the sibling's write is not durable.
                        (statusCode: 409, sessionToken: "bad-on-conflict"),
                        (statusCode: 201, sessionToken: "bad-on-success"),
                    }),
                statusCode: HttpStatusCode.OK,
                accountConsistencyLevel: Cosmos.ConsistencyLevel.Session);

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                this.CreateOperations(2), mockContext.Object, OperationType.CommitDistributedTransaction);

            DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response[0].StatusCode,
                "The server's own error must reach the caller rather than being masked by a token failure.");

            Assert.IsTrue(
                string.IsNullOrEmpty(sessionContainer.GetSessionToken(
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName))),
                "A malformed token must never reach the session container.");
        }

        // ─── Request-side session token validation ────────────────────────────────

        [TestMethod]
        [Description("Verifies that a caller-supplied session token the SDK cannot interpret fails the transaction before collection metadata is read or anything is sent.")]
        [DataRow("garbage", DisplayName = "no separator, unparseable")]
        [DataRow("5", DisplayName = "bare LSN, no partitionKeyRangeId")]
        [DataRow("1#5#4=3", DisplayName = "bare vector, no partitionKeyRangeId")]
        [DataRow(":1#5", DisplayName = "empty partitionKeyRangeId")]
        [DataRow("0:", DisplayName = "empty token")]
        [DataRow("0:garbage", DisplayName = "valid prefix, unparseable token")]
        [DataRow("0:1#5 ", DisplayName = "trailing space, lands in lsn")]
        [DataRow("0:1#5 ,1:2#8", DisplayName = "space before separator, lands in lsn")]
        public async Task ExecuteTransactionAsync_ThrowsOnMalformedUserSuppliedSessionToken(string malformedToken)
        {
            int dispatchCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    dispatchCount++;
                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create, 0, DatabaseName, ContainerName,
                    new PartitionKey("pk1"), id: "doc1",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = malformedToken }),
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

            Assert.IsTrue(exception.Message.Contains(malformedToken),
                $"Message must quote the offending token. Message: {exception.Message}");
            Assert.AreEqual(0, dispatchCount,
                "A malformed caller-supplied token must fail pre-flight, before the transaction is dispatched.");
        }

        [TestMethod]
        [Description("Session-consistent read transactions validate caller-supplied tokens before dispatch.")]
        public async Task ExecuteTransactionAsync_ThrowsOnMalformedUserSuppliedSessionToken_ForSessionRead()
        {
            int dispatchCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext(
                new CosmosClientOptions { ConsistencyLevel = Cosmos.ConsistencyLevel.Session });
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    dispatchCount++;
                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Read, 0, DatabaseName, ContainerName,
                    new PartitionKey("pk1"), id: "doc1",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = "0:not-a-session-token" }),
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.Read, TimeSpan.Zero);

            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

            Assert.AreEqual(0, dispatchCount,
                "A malformed Session-consistent read token must fail before dispatch.");
        }

        [DataTestMethod]
        [Description("Non-Session read transactions forward caller-supplied tokens without client-side validation, matching the Gateway point-read hop.")]
        [DataRow(Cosmos.ConsistencyLevel.Eventual)]
        [DataRow(Cosmos.ConsistencyLevel.ConsistentPrefix)]
        [DataRow(Cosmos.ConsistencyLevel.BoundedStaleness)]
        [DataRow(Cosmos.ConsistencyLevel.Strong)]
        public async Task ExecuteTransactionAsync_ForwardsMalformedUserSuppliedSessionToken_ForNonSessionRead(
            Cosmos.ConsistencyLevel consistencyLevel)
        {
            await this.AssertMalformedReadTokenIsForwardedAsync(
                new CosmosClientOptions { ConsistencyLevel = consistencyLevel });
        }

        [TestMethod]
        [Description("A read transaction forwards caller-supplied tokens when effective consistency cannot be resolved.")]
        public async Task ExecuteTransactionAsync_ForwardsMalformedUserSuppliedSessionToken_WhenReadConsistencyIsUnresolved()
        {
            await this.AssertMalformedReadTokenIsForwardedAsync(clientOptions: null);
        }

        [TestMethod]
        [Description("Verifies that a malformed caller token is bounded and has line breaks escaped before it reaches the exception text that the committer logs.")]
        public async Task ExecuteTransactionAsync_BoundsMalformedUserSuppliedSessionTokenInException()
        {
            string malformedToken = "0:garbage\r\n" + new string('x', 300);
            int dispatchCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    dispatchCount++;
                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create, 0, DatabaseName, ContainerName,
                    new PartitionKey("pk1"), id: "doc1",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = malformedToken }),
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

            StringAssert.Contains(exception.Message, @"0:garbage\r\n");
            StringAssert.Contains(exception.Message, "...[truncated]");
            Assert.IsFalse(exception.Message.Contains("0:garbage\r\n"),
                "Caller-controlled line breaks must not be emitted verbatim in the exception text that is logged.");
            Assert.IsFalse(exception.Message.Contains(malformedToken),
                "The full caller-controlled token must not be emitted in the exception text that is logged.");
            Assert.AreEqual(0, dispatchCount,
                "A malformed caller-supplied token must fail pre-flight, before the transaction is dispatched.");
        }

        [TestMethod]
        [Description("Verifies that validation reports the first malformed token in operation order, so the caller is pointed at one deterministic operation.")]
        public async Task ExecuteTransactionAsync_ThrowsOnFirstMalformedUserSuppliedSessionToken()
        {
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () => Task.FromResult(CreateSuccessResponseMessage(operationCount: 3)));

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create, 0, DatabaseName, ContainerName,
                    new PartitionKey("pk0"), id: "doc0",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = "0:1#5" }),
                new DistributedTransactionOperation(
                    OperationType.Create, 1, DatabaseName, ContainerName,
                    new PartitionKey("pk1"), id: "doc1",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = "first-bad" }),
                new DistributedTransactionOperation(
                    OperationType.Create, 2, DatabaseName, ContainerName,
                    new PartitionKey("pk2"), id: "doc2",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = "second-bad" }),
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

            Assert.IsTrue(exception.Message.Contains("index 1"),
                $"Message must identify the first malformed operation. Message: {exception.Message}");
            Assert.IsFalse(exception.Message.Contains("second-bad"),
                $"Message must not report a later malformed token. Message: {exception.Message}");
        }

        [TestMethod]
        [Description("Verifies that a compound collection-level session token is accepted and forwarded verbatim. This is compatibility tolerance matching ItemRequestOptions.SessionToken, not a supported round-trip: the coordinator returns a single '<partitionKeyRangeId>:<token>' pair per operation, so no response hands the caller a compound token.")]
        public async Task ExecuteTransactionAsync_AcceptsCompoundUserSuppliedSessionToken()
        {
            const string compoundToken = "0:1#5,1:2#8";
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
                    OperationType.Create, 0, DatabaseName, ContainerName,
                    new PartitionKey("pk1"), id: "doc1",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = compoundToken }),
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(capturedBody);
            string bodyJson = Encoding.UTF8.GetString(capturedBody);
            Assert.IsTrue(bodyJson.Contains($"\"sessionToken\":\"{compoundToken}\""),
                $"The compound token must be forwarded verbatim. Body: {bodyJson}");
        }

        [TestMethod]
        [Description("Verifies that every segment of a compound session token is validated, not just the first; a trailing malformed segment would otherwise reach the coordinator undetected.")]
        public async Task ExecuteTransactionAsync_ThrowsOnMalformedSegmentOfCompoundUserSuppliedSessionToken()
        {
            int dispatchCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    dispatchCount++;
                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create, 0, DatabaseName, ContainerName,
                    new PartitionKey("pk1"), id: "doc1",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = "0:1#5,1:garbage" }),
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

            Assert.IsTrue(exception.Message.Contains("1:garbage"),
                $"Message must quote the offending segment. Message: {exception.Message}");
            Assert.AreEqual(0, dispatchCount, "Nothing may be dispatched when a token segment is malformed.");
        }

        [TestMethod]
        [Description("Verifies that operations without a caller-supplied session token are unaffected by request-side validation.")]
        public async Task ExecuteTransactionAsync_DoesNotValidateAbsentUserSuppliedSessionToken()
        {
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () => Task.FromResult(CreateSuccessResponseMessage(operationCount: 2)));

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create, 0, DatabaseName, ContainerName, new PartitionKey("pk0"), id: "doc0"),
                new DistributedTransactionOperation(
                    OperationType.Create, 1, DatabaseName, ContainerName,
                    new PartitionKey("pk1"), id: "doc1",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = "   " }),
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            using DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        [Description("Verifies that surrounding whitespace and empty segments are accepted, matching what SessionTokenHelper tolerates on the point-operation path. Rejecting them here would fail transactions for input that succeeds today on every other operation type.")]
        [DataRow("0:1#5, 1:2#8", DisplayName = "space after separator, lands in range id")]
        [DataRow(" 0:1#5", DisplayName = "leading space, lands in range id")]
        [DataRow("0:1#5,", DisplayName = "trailing separator, empty segment")]
        [DataRow("0:1#5,,1:2#8", DisplayName = "repeated separator, empty segment")]
        public async Task ExecuteTransactionAsync_AcceptsSessionTokenShapesThePointOperationPathTolerates(string tolerantToken)
        {
            int dispatchCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    dispatchCount++;
                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create, 0, DatabaseName, ContainerName,
                    new PartitionKey("pk1"), id: "doc1",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = tolerantToken }),
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction, TimeSpan.Zero);

            await committer.ExecuteTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(1, dispatchCount,
                $"Validation must not be stricter than the point-operation path, which accepts '{tolerantToken}'.");
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

        // The server returns tokens in '<partitionKeyRangeId>:<token>' form. Test cases pass the range id
        // separately for readability, so it is prefixed here unless a case is exercising a malformed token.
        private static string BuildDtcResponseJson(
            (int statusCode, int? subStatusCode, string sessionToken, string partitionKeyRangeId)[] operations,
            bool prefixRangeLessTokens = true)
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

                string sessionToken = operations[i].sessionToken;
                if (prefixRangeLessTokens
                    && !string.IsNullOrEmpty(sessionToken)
                    && !sessionToken.Contains(":")
                    && !string.IsNullOrWhiteSpace(operations[i].partitionKeyRangeId)
                    && SessionTokenHelper.TryParse(sessionToken, out string _, out ISessionToken _))
                {
                    sessionToken = operations[i].partitionKeyRangeId + ":" + sessionToken;
                }

                if (sessionToken != null)
                {
                    sb.Append($@",""{DistributedTransactionSerializer.SessionToken}"":""{sessionToken}""");
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
            return this.CreateMockContext(sessionContainer, responseContent, statusCode, accountConsistencyLevel: null);
        }

        private Mock<CosmosClientContext> CreateMockContext(
            ISessionContainer sessionContainer,
            string responseContent,
            HttpStatusCode statusCode,
            Cosmos.ConsistencyLevel? accountConsistencyLevel)
        {
            MockDocumentClient documentClient = accountConsistencyLevel.HasValue
                ? new MockDocumentClient(accountConsistencyLevel.Value) { sessionContainer = sessionContainer }
                : new MockDocumentClient { sessionContainer = sessionContainer };

            return this.CreateMockContext(documentClient, responseContent, statusCode);
        }

        private Mock<CosmosClientContext> CreateMockContext(
            MockDocumentClient documentClient,
            string responseContent,
            HttpStatusCode statusCode)
        {
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

        /// <summary>
        /// Builds <paramref name="count"/> create operations against the single collection the
        /// <c>CreateMockContext</c> helpers stub, each on its own partition key.
        /// </summary>
        private List<DistributedTransactionOperation> CreateOperations(int count)
        {
            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>(count);
            for (int i = 0; i < count; i++)
            {
                operations.Add(new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: i,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey($"pk{i}"),
                    id: $"doc{i}"));
            }

            return operations;
        }

        /// <summary>
        /// Extracts the partition key range ids from a compound collection session token
        /// (<c>"0:1#5,1:1#7"</c>), identifying which operations reached the session container.
        /// </summary>
        private static Dictionary<string, string> ParseSessionTokensByRange(string compoundSessionToken)
        {
            Dictionary<string, string> tokensByRange = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(compoundSessionToken))
            {
                return tokensByRange;
            }

            foreach (string segment in compoundSessionToken.Split(','))
            {
                int separatorIndex = segment.IndexOf(':');
                if (separatorIndex > 0)
                {
                    tokensByRange[segment.Substring(0, separatorIndex)] = segment.Substring(separatorIndex + 1);
                }
            }

            return tokensByRange;
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

        private async Task AssertMalformedReadTokenIsForwardedAsync(CosmosClientOptions clientOptions)
        {
            const string malformedToken = "0:not-a-session-token";
            int dispatchCount = 0;
            byte[] capturedBody = null;
            Mock<CosmosClientContext> mockContext = clientOptions == null
                ? this.CreateMockClientContext()
                : this.CreateMockClientContext(clientOptions);
            this.SetupProcessResourceOperationWithStreamAndEnricherCapture(
                mockContext,
                (stream, _) =>
                {
                    dispatchCount++;
                    using MemoryStream copy = new MemoryStream();
                    stream.CopyTo(copy);
                    capturedBody = copy.ToArray();
                },
                () => Task.FromResult(CreateSuccessResponseMessage(operationCount: 1)));

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Read, 0, DatabaseName, ContainerName,
                    new PartitionKey("pk1"), id: "doc1",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = malformedToken }),
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.Read, TimeSpan.Zero);

            using DistributedTransactionResponse response = await committer.ExecuteTransactionAsync(
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, dispatchCount);
            Assert.IsNotNull(capturedBody);
            StringAssert.Contains(
                Encoding.UTF8.GetString(capturedBody),
                $"\"sessionToken\":\"{malformedToken}\"");
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
        /// A <see cref="MockDocumentClient"/> whose account consistency level cannot be resolved,
        /// standing in for a transient failure of the gateway account read.
        /// </summary>
        private sealed class UnresolvableConsistencyDocumentClient : MockDocumentClient
        {
            internal override Task<Cosmos.ConsistencyLevel> GetDefaultConsistencyLevelAsync()
            {
                throw new InvalidOperationException("Simulated account read failure.");
            }
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
