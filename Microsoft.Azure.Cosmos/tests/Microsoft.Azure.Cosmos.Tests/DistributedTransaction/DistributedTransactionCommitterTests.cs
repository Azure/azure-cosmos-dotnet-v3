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
            const string canonicalToken = "0:1#9#4=8#5=7";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            string responseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: canonicalToken, partitionKeyRangeId: (string)null) });

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
            Assert.AreEqual(canonicalToken, storedToken,
                "Session token in canonical format should be merged into SessionContainer after a successful DTC commit.");
        }

        [TestMethod]
        [Description("When a per-operation session token is absent, MergeSessionTokens silently skips (mirrors GatewayStoreModel behavior)")]
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

            // Should NOT throw — silently skips merge like GatewayStoreModel does
            DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        [Description("Verifies that the correct collectionRid and collectionFullname are passed to SetSessionToken for each operation")]
        public async Task CommitTransactionAsync_PassesCorrectCollectionToSetSessionToken()
        {
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
                            (statusCode: 200, subStatusCode: (int?)null, sessionToken: assembledToken, partitionKeyRangeId: pkRangeId),
                            (statusCode: 200, subStatusCode: (int?)null, sessionToken: assembledToken, partitionKeyRangeId: pkRangeId),
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
        [Description("When DTC returns a failure response, per-op session tokens are empty (DTC contract). " +
                     "The SDK silently skips merge — no throw, no session container update.")]
        public async Task CommitTransactionAsync_SkipsMerge_OnFailureResponse_EmptyTokens()
        {
            // DTC contract (verified via E2E): failed transactions return empty session tokens.
            // The SDK's IsNullOrEmpty check silently skips them — no merge, no throw.
            SessionContainer sessionContainer = new SessionContainer("testhost");

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: BuildDtcResponseJson(new[] { (statusCode: 409, subStatusCode: (int?)null, sessionToken: string.Empty, partitionKeyRangeId: (string)null) }),
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

            // No token should be stored — DTC returns empty tokens on failure
            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.IsTrue(string.IsNullOrEmpty(storedToken),
                "DTC does not emit session tokens on failed transactions; SessionContainer must remain empty.");
        }

        [DataTestMethod]
        [DataRow("0", DisplayName = "LSN-only token with pkRangeId present — SDK no longer assembles tokens")]
        [DataRow(null, DisplayName = "LSN-only token with pkRangeId absent")]
        [Description("When the per-op session token is LSN-only (not canonical), FromJson throws " +
                     "CosmosException with a descriptive message — regardless of whether " +
                     "partitionKeyRangeId is supplied alongside it.")]
        public async Task CommitTransactionAsync_Throws_WhenSessionTokenIsNotCanonical(string pkRangeId)
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

            CosmosException ex = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None),
                $"A non-canonical session token (pkRangeId='{pkRangeId ?? "<null>"}') must throw CosmosException.");

            Assert.IsTrue(
                ex.Message.Contains("malformed session token") || ex.Message.Contains("canonical"),
                $"Exception message should mention token format requirement. Actual: {ex.Message}");
        }


        [DataTestMethod]
        [DataRow("", DisplayName = "Empty string partitionKeyRangeId")]
        [DataRow(" ", DisplayName = "Whitespace-only partitionKeyRangeId")]
        [DataRow("   ", DisplayName = "Multiple whitespace partitionKeyRangeId")]
        [Description("When sessionToken is LSN-only and partitionKeyRangeId is empty/whitespace, " +
                     "FromJson throws CosmosException because the token is not canonical.")]
        public async Task CommitTransactionAsync_Throws_WhenPartitionKeyRangeIdIsEmptyOrWhitespace(string pkRangeId)
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

            await Assert.ThrowsExceptionAsync<CosmosException>(
                () => committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None),
                $"An LSN-only session token with pkRangeId '{pkRangeId}' must throw CosmosException.");
        }

        // ─── Retry / Spec-Compliance Tests ─────────────────────────────────────

        [TestMethod]
        [Description("In a multi-op response, if op 0 has a malformed token and op 1 has a valid one, " +
                     "MergeSessionTokens throws on the FIRST malformed token; op 1's later token is NOT merged.")]
        public async Task CommitTransactionAsync_MultiOp_ThrowsOnFirstMalformed_WithoutMergingLaterOps()
        {
            const string lsnOnly = "1#9#4=8#5=7";
            const string canonicalToken = "0:1#9#4=8#5=7";
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

            // op 0: malformed (LSN-only, not canonical) — throws on the first malformed token
            // op 1: valid canonical token — NOT merged because op 0 throws first
            string responseJson = BuildDtcResponseJson(new[]
            {
                (statusCode: 201, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: (string)null),
                (statusCode: 201, subStatusCode: (int?)null, sessionToken: canonicalToken, partitionKeyRangeId: (string)null),
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

            await Assert.ThrowsExceptionAsync<CosmosException>(
                () => committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None),
                "A malformed session token must throw CosmosException on the first malformed op.");

            // The key assertion: op 1's later token was NOT merged — op 0 threw first
            mockSessionContainer.Verify(
                s => s.SetSessionToken(
                    collectionRid2,
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, container2),
                    It.Is<INameValueCollection>(h => h[HttpConstants.HttpHeaders.SessionToken] == canonicalToken)),
                Times.Never,
                "Op 1's later token must NOT be merged because op 0 throws on the first malformed token.");

            // Op 0's malformed token must NOT have been merged
            mockSessionContainer.Verify(
                s => s.SetSessionToken(
                    collectionRid1,
                    It.IsAny<string>(),
                    It.IsAny<INameValueCollection>()),
                Times.Never,
                "Op 0's malformed token must not be merged.");
        }

        [TestMethod]
        // When SetSessionToken throws on a content-invalid token (SessionTokenHelper.Parse throws
        // BadRequestException after the shape check), the malformed token is surfaced as a
        // CosmosException under Session consistency on a committed response.
        public async Task CommitTransactionAsync_AggregatesSetSessionTokenException()
        {
            const string canonicalToken = "0:1#9#4=8#5=7";

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();
            mockSessionContainer
                .Setup(s => s.SetSessionToken(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<INameValueCollection>()))
                .Throws(new BadRequestException("simulated SetSessionToken failure"));

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
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: canonicalToken, partitionKeyRangeId: (string)null) });

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

            await Assert.ThrowsExceptionAsync<CosmosException>(
                () => committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None),
                "SetSessionToken exception must be aggregated and surfaced as CosmosException.");
        }

        [TestMethod]
        [Description("When SetSessionToken throws OperationCanceledException, the exception propagates.")]
        public async Task CommitTransactionAsync_PropagatesOperationCanceledException_FromSetSessionToken()
        {
            const string canonicalToken = "0:1#9#4=8#5=7";

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
                    new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: canonicalToken, partitionKeyRangeId: (string)null) }),
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
        // When CollectionResourceId is null (e.g., resolve failed or serverless/system resources),
        // MergeSessionTokens skips the op instead of crashing with a NullReferenceException
        // from SessionContainer.SetSessionToken → ResourceId.Parse(null).
        public async Task MergeSessionTokens_SkipsMerge_WhenCollectionResourceIdIsNull()
        {
            const string canonicalToken = "0:1#9#4=8#5=7";

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();

            // Build a mock response with a valid session token
            DistributedTransactionOperationResult result = new DistributedTransactionOperationResult();
            result.Index = 0;
            result.StatusCode = HttpStatusCode.Created;
            result.SessionToken = canonicalToken;

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
            mockResponse.Setup(r => r.Count).Returns(1);
            mockResponse.Setup(r => r[0]).Returns(result);

            // Operation has NULL CollectionResourceId (never resolved)
            DistributedTransactionOperation operation = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0,
                DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            // CollectionResourceId defaults to null — do not set it

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { operation },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            // Should not throw — skips the merge gracefully
            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                mockResponse.Object, serverRequest, mockSessionContainer.Object, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton);

            // SetSessionToken must NOT have been called (skipped due to null CollectionResourceId)
            mockSessionContainer.Verify(
                s => s.SetSessionToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<INameValueCollection>()),
                Times.Never,
                "SetSessionToken must not be called when CollectionResourceId is null.");
        }

        [TestMethod]
        // A non-success sub-op carrying a non-empty malformed token must be skipped: neither merged
        // nor allowed to trigger the commit-wide throw, even on an overall-success response.
        public async Task MergeSessionTokens_SkipsFailedOp_WithNonEmptyMalformedToken_DoesNotThrow()
        {
            const string malformedToken = "no-colon-lsn-only";

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();

            // op 0: non-success (404/1002) result carrying a malformed token — must be skipped.
            DistributedTransactionOperationResult failedResult = new DistributedTransactionOperationResult();
            failedResult.Index = 0;
            failedResult.StatusCode = HttpStatusCode.NotFound;
            failedResult.SubStatusCode = SubStatusCodes.ReadSessionNotAvailable;
            failedResult.SessionToken = malformedToken;

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
            mockResponse.Setup(r => r.Count).Returns(1);
            mockResponse.Setup(r => r[0]).Returns(failedResult);
            mockResponse.Setup(r => r.IsSuccessStatusCode).Returns(true);
            mockResponse.Setup(r => r.StatusCode).Returns(HttpStatusCode.OK);

            DistributedTransactionOperation operation = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0,
                DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            operation.CollectionResourceId = CollectionResourceId;

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { operation },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            // Must NOT throw despite the malformed token, because the carrying op did not succeed.
            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                mockResponse.Object, serverRequest, mockSessionContainer.Object, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton);

            mockSessionContainer.Verify(
                s => s.SetSessionToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<INameValueCollection>()),
                Times.Never,
                "A failed sub-op's token must not be merged.");
        }

        [TestMethod]
        // When SetSessionToken throws on op 0 (e.g., SessionTokenHelper.Parse rejects a content-invalid
        // token that passed the colon-shape check), MergeSessionTokens throws on that FIRST malformed
        // op; op 1's later token is NOT merged. Cancellation still propagates as-is.
        public async Task MergeSessionTokens_SetSessionTokenThrows_ThrowsOnFirstWithoutMergingLaterOps()
        {
            const string token0 = "0:bad-content";
            const string token1 = "1:1#9#4=8#5=7";
            const string container2 = "testcontainer2";
            string collectionRid1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionRid2 = ResourceId.NewDocumentCollectionId(42, 200).DocumentCollectionId.ToString();

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();
            // Only op 0's call throws (content-invalid token → BadRequestException, what
            // SessionTokenHelper.Parse actually throws); op 1's call would succeed.
            mockSessionContainer
                .Setup(s => s.SetSessionToken(collectionRid1, It.IsAny<string>(), It.IsAny<INameValueCollection>()))
                .Throws(new BadRequestException("simulated parse failure"));

            DistributedTransactionOperationResult result0 = new DistributedTransactionOperationResult();
            result0.Index = 0;
            result0.StatusCode = HttpStatusCode.Created;
            result0.SessionToken = token0;

            DistributedTransactionOperationResult result1 = new DistributedTransactionOperationResult();
            result1.Index = 1;
            result1.StatusCode = HttpStatusCode.Created;
            result1.SessionToken = token1;

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
            mockResponse.Setup(r => r.IsSuccessStatusCode).Returns(true);
            mockResponse.Setup(r => r.Count).Returns(2);
            mockResponse.Setup(r => r[0]).Returns(result0);
            mockResponse.Setup(r => r[1]).Returns(result1);

            DistributedTransactionOperation op0 = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0,
                DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            op0.CollectionResourceId = collectionRid1;

            DistributedTransactionOperation op1 = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 1,
                DatabaseName, container2, new PartitionKey("pk2"), id: "doc2");
            op1.CollectionResourceId = collectionRid2;

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { op0, op1 },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            // Aggregated exception should surface; op 1's later token must NOT be merged (op 0 threw first)
            await Assert.ThrowsExceptionAsync<CosmosException>(
                () => DistributedTransactionCommitter.MergeSessionTokensAsync(
                    mockResponse.Object, serverRequest, mockSessionContainer.Object, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton),
                "CosmosException must surface on the first malformed op.");

            mockSessionContainer.Verify(
                s => s.SetSessionToken(
                    collectionRid2,
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, container2),
                    It.Is<INameValueCollection>(h => h[HttpConstants.HttpHeaders.SessionToken] == token1)),
                Times.Never,
                "Op 1's later token must NOT be merged because op 0 throws on the first malformed token.");
        }

        [TestMethod]
        // SessionContainer.SetSessionToken throws InternalServerErrorException on a content-invalid token
        // (SessionTokenHelper.Parse with the version-aware overload) and also on a benign concurrent-add
        // race or a VectorSessionToken region-mismatch merge. The DTX classifier cannot distinguish these
        // (identical type), so it treats them uniformly: under Session consistency on a committed response
        // it surfaces a clean, non-retriable CosmosException instead of leaking a raw Direct
        // InternalServerErrorException out of a committed transaction; under non-Session consistency it
        // traces-and-skips without throwing.
        public async Task MergeSessionTokens_InternalServerErrorFromSetSessionToken_SurfacedAsCosmosException()
        {
            const string canonicalToken = "0:1#9#4=8#5=7";
            string collectionRid = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();
            mockSessionContainer
                .Setup(s => s.SetSessionToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<INameValueCollection>()))
                .Throws(new InternalServerErrorException("AddSessionToken failed to get or add the session token dictionary."));

            DistributedTransactionOperationResult result = new DistributedTransactionOperationResult
            { Index = 0, StatusCode = HttpStatusCode.Created, SessionToken = canonicalToken };

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
            mockResponse.Setup(r => r.IsSuccessStatusCode).Returns(true);
            mockResponse.Setup(r => r.Count).Returns(1);
            mockResponse.Setup(r => r[0]).Returns(result);

            DistributedTransactionOperation operation = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0,
                DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            operation.CollectionResourceId = collectionRid;

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { operation },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            // Session consistency: the InternalServerErrorException is surfaced as a non-retriable
            // CosmosException, NOT leaked as the raw Direct exception.
            CosmosException ex = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => DistributedTransactionCommitter.MergeSessionTokensAsync(
                    mockResponse.Object, serverRequest, mockSessionContainer.Object, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton),
                "InternalServerErrorException from SetSessionToken must be surfaced as a CosmosException under Session consistency.");
            Assert.AreEqual(HttpStatusCode.InternalServerError, ex.StatusCode);

            // Non-Session consistency: traced-and-skipped, no exception escapes the merge.
            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                mockResponse.Object, serverRequest, mockSessionContainer.Object, isSessionConsistency: false, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton);

            // Strengthen: SetSessionToken must have been ATTEMPTED on both calls (the InternalServerErrorException
            // was genuinely reached and swallowed on the non-Session half — not short-circuited before the write path).
            mockSessionContainer.Verify(
                s => s.SetSessionToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<INameValueCollection>()),
                Times.Exactly(2),
                "SetSessionToken must be attempted once per MergeSessionTokens call (Session throw-half + non-Session skip-half).");
        }

        [TestMethod]
        // OperationCanceledException from SetSessionToken must NOT be caught/aggregated — it propagates
        // immediately to honor the cancellation token.
        public async Task MergeSessionTokens_OperationCanceledException_Propagates()
        {
            const string canonicalToken = "0:1#9#4=8#5=7";
            string collectionRid = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();
            mockSessionContainer
                .Setup(s => s.SetSessionToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<INameValueCollection>()))
                .Throws(new OperationCanceledException("simulated cancellation"));

            DistributedTransactionOperationResult result = new DistributedTransactionOperationResult();
            result.Index = 0;
            result.StatusCode = HttpStatusCode.Created;
            result.SessionToken = canonicalToken;

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
            mockResponse.Setup(r => r.Count).Returns(1);
            mockResponse.Setup(r => r[0]).Returns(result);

            DistributedTransactionOperation operation = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0,
                DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            operation.CollectionResourceId = collectionRid;

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { operation },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => DistributedTransactionCommitter.MergeSessionTokensAsync(
                    mockResponse.Object, serverRequest, mockSessionContainer.Object, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton),
                "OperationCanceledException must propagate, not be caught/aggregated.");
        }

        [TestMethod]
        // Defensive: a malformed server response with an out-of-range op Index must not crash
        // the merge loop or skip remaining ops. The bad index is traced and skipped, and
        // subsequent valid ops still get merged (no exception thrown).
        public async Task MergeSessionTokens_OutOfRangeIndex_SkippedAndTraced()
        {
            const string canonicalToken = "0:1#9#4=8#5=7";
            string collectionRid = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();

            // Result 0: out-of-range Index (99 when request only has 1 op)
            DistributedTransactionOperationResult badResult = new DistributedTransactionOperationResult();
            badResult.Index = 99;
            badResult.StatusCode = HttpStatusCode.Created;
            badResult.SessionToken = canonicalToken;

            // Result 1: valid in-range Index
            DistributedTransactionOperationResult validResult = new DistributedTransactionOperationResult();
            validResult.Index = 0;
            validResult.StatusCode = HttpStatusCode.Created;
            validResult.SessionToken = canonicalToken;

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
            mockResponse.Setup(r => r.Count).Returns(2);
            mockResponse.Setup(r => r[0]).Returns(badResult);
            mockResponse.Setup(r => r[1]).Returns(validResult);

            DistributedTransactionOperation operation = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0,
                DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            operation.CollectionResourceId = collectionRid;

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { operation },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            // Out-of-range Index is skipped (traced), not treated as a malformed-token throw; it must
            // not raise ArgumentOutOfRangeException and must not abort the loop.
            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                mockResponse.Object, serverRequest, mockSessionContainer.Object, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton);

            // The valid in-range op was still merged after the bad-index result was skipped
            mockSessionContainer.Verify(
                s => s.SetSessionToken(collectionRid, It.IsAny<string>(), It.IsAny<INameValueCollection>()),
                Times.Once,
                "Valid in-range op must still be merged after a bad-index op is skipped.");
        }

        // ─── v2 edge case coverage ───────────────────────────────────────────────

        [TestMethod]
        // v2 edge case #1: a token like "0:-1" that passes the colon-shape check but causes
        // SessionTokenHelper.Parse to throw internally must be classified as malformed (caught
        // by the per-op try/catch). Under Session consistency on a committed response the merge
        // throws on this FIRST malformed op; op 1's later token is NOT merged.
        public async Task MergeSessionTokens_ShapeValidButContentInvalid_ClassifiedAsMalformed()
        {
            // op 0: structurally valid (passes colon check) but content is invalid for the parser
            const string shapeValidContentInvalid = "0:not-a-valid-lsn";
            // op 1: fully canonical — must still be merged
            const string canonicalToken = "1:1#9#4=8#5=7";
            const string container2 = "testcontainer2";
            string collectionRid1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionRid2 = ResourceId.NewDocumentCollectionId(42, 200).DocumentCollectionId.ToString();

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();
            // op 0's content is rejected by the classifier. This mock throws BadRequestException — one of
            // the two malformed types IsMalformedSessionTokenException catches. The real version-aware
            // SessionTokenHelper.Parse used by SetSessionToken actually throws InternalServerErrorException;
            // that end-to-end path is covered by MergeSessionTokens_RealContainer_ContentInvalidToken_ThrowsCosmosException.
            mockSessionContainer
                .Setup(s => s.SetSessionToken(
                    collectionRid1,
                    It.IsAny<string>(),
                    It.Is<INameValueCollection>(h => h[HttpConstants.HttpHeaders.SessionToken] == shapeValidContentInvalid)))
                .Throws(new BadRequestException("Invalid session token content"));

            DistributedTransactionOperationResult result0 = new DistributedTransactionOperationResult
            { Index = 0, StatusCode = HttpStatusCode.Created, SessionToken = shapeValidContentInvalid };
            DistributedTransactionOperationResult result1 = new DistributedTransactionOperationResult
            { Index = 1, StatusCode = HttpStatusCode.Created, SessionToken = canonicalToken };

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
            mockResponse.Setup(r => r.IsSuccessStatusCode).Returns(true);
            mockResponse.Setup(r => r.Count).Returns(2);
            mockResponse.Setup(r => r[0]).Returns(result0);
            mockResponse.Setup(r => r[1]).Returns(result1);

            DistributedTransactionOperation op0 = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0,
                DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            op0.CollectionResourceId = collectionRid1;

            DistributedTransactionOperation op1 = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 1,
                DatabaseName, container2, new PartitionKey("pk2"), id: "doc2");
            op1.CollectionResourceId = collectionRid2;

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { op0, op1 },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            CosmosException ex = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => DistributedTransactionCommitter.MergeSessionTokensAsync(
                    mockResponse.Object, serverRequest, mockSessionContainer.Object, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton));

            Assert.IsTrue(ex.Message.Contains(shapeValidContentInvalid) || ex.Message.Contains("Op 0"),
                $"Aggregated message should identify the bad op. Actual: {ex.Message}");

            // Op 1's later token must NOT have been merged — op 0 throws on the first malformed token
            mockSessionContainer.Verify(
                s => s.SetSessionToken(
                    collectionRid2,
                    It.IsAny<string>(),
                    It.Is<INameValueCollection>(h => h[HttpConstants.HttpHeaders.SessionToken] == canonicalToken)),
                Times.Never,
                "Op 1's later token must NOT be merged because op 0 throws on the first malformed token.");
        }

        [TestMethod]
        // End-to-end with a REAL SessionContainer: a shape-valid but content-invalid token
        // ("0:not-a-valid-lsn") passes the colon-shape pre-check and reaches SessionContainer.SetSessionToken,
        // whose version-aware SessionTokenHelper.Parse throws InternalServerErrorException (NOT
        // BadRequestException — confirmed by the two Parse overloads). The DTX classifier must catch it and
        // surface a non-retriable CosmosException under Session consistency instead of letting the raw Direct
        // exception escape a committed transaction. This exercises the real path the BadRequestException mock
        // in MergeSessionTokens_ShapeValidButContentInvalid_ClassifiedAsMalformed cannot reach.
        public async Task MergeSessionTokens_RealContainer_ContentInvalidToken_ThrowsCosmosException()
        {
            const string shapeValidContentInvalid = "0:not-a-valid-lsn";
            string collectionRid = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();

            SessionContainer sessionContainer = new SessionContainer("testhost");

            DistributedTransactionOperationResult result = new DistributedTransactionOperationResult
            { Index = 0, StatusCode = HttpStatusCode.Created, SessionToken = shapeValidContentInvalid };

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
            mockResponse.Setup(r => r.IsSuccessStatusCode).Returns(true);
            mockResponse.Setup(r => r.Count).Returns(1);
            mockResponse.Setup(r => r[0]).Returns(result);

            DistributedTransactionOperation operation = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0,
                DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            operation.CollectionResourceId = collectionRid;

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { operation },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            CosmosException ex = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => DistributedTransactionCommitter.MergeSessionTokensAsync(
                    mockResponse.Object, serverRequest, sessionContainer, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton),
                "A content-invalid token reaching the real SetSessionToken throws InternalServerErrorException, " +
                "which must be surfaced as a non-retriable CosmosException (not leaked as the raw Direct exception).");
            Assert.AreEqual(HttpStatusCode.InternalServerError, ex.StatusCode);
        }

        [TestMethod]
        [Description("Session tokens must be in canonical '{pkRangeId}:{lsn}' format. " +
            "An LSN-only token without a colon separator is classified as malformed.")]
        public async Task MergeSessionTokens_LegacyTwoFieldFormat_TreatedAsMalformed()
        {
            // LSN-only token without colon separator is malformed.
            const string lsnOnly = "1#9#4=8#5=7";
            string collectionRid = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();

            DistributedTransactionOperationResult result0 = new DistributedTransactionOperationResult
            { Index = 0, StatusCode = HttpStatusCode.Created, SessionToken = lsnOnly };

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
            mockResponse.Setup(r => r.Count).Returns(1);
            mockResponse.Setup(r => r[0]).Returns(result0);
            mockResponse.Setup(r => r.IsSuccessStatusCode).Returns(true);
            mockResponse.Setup(r => r.ActivityId).Returns("test-activity-id");
            mockResponse.Setup(r => r.RequestCharge).Returns(3.14);

            DistributedTransactionOperation op0 = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0,
                DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            op0.CollectionResourceId = collectionRid;

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { op0 },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            CosmosException ex = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => DistributedTransactionCommitter.MergeSessionTokensAsync(
                    mockResponse.Object, serverRequest, mockSessionContainer.Object, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton));

            Assert.AreEqual(HttpStatusCode.InternalServerError, ex.StatusCode);
            Assert.IsTrue(ex.Message.Contains("committed"),
                $"Exception must indicate the transaction committed. Actual: {ex.Message}");
            Assert.IsTrue(ex.Message.Contains(lsnOnly) || ex.Message.Contains("Op 0"),
                $"Exception must identify the malformed token. Actual: {ex.Message}");
            Assert.AreEqual("test-activity-id", ex.ActivityId,
                "ActivityId from the response must be propagated to the exception for support tickets.");
            Assert.AreEqual(3.14, ex.RequestCharge,
                "RequestCharge from the response must be propagated to the exception.");

            // SetSessionToken must NOT have been called — no token was canonical
            mockSessionContainer.Verify(
                s => s.SetSessionToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<INameValueCollection>()),
                Times.Never,
                "An LSN-only token without colon separator must not be merged.");
        }

        [TestMethod]
        // Gating: under non-Session consistency (isSessionConsistency == false) a malformed token must
        // NOT throw — it is traced best-effort and the loop continues, so a subsequent valid op's token
        // is still merged. This locks in the throwOnMalformed = IsSuccessStatusCode && isSessionConsistency
        // gate (mirrors GatewayStoreModel: session-token bookkeeping is only enforced under Session).
        public async Task MergeSessionTokens_NonSessionConsistency_DoesNotThrow_AndMergesValidOps()
        {
            const string malformedToken = "1#9#4=8#5=7"; // LSN-only, no colon → malformed
            const string canonicalToken = "1:1#9#4=8#5=7";
            const string container2 = "testcontainer2";
            string collectionRid1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionRid2 = ResourceId.NewDocumentCollectionId(42, 200).DocumentCollectionId.ToString();

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();

            DistributedTransactionOperationResult result0 = new DistributedTransactionOperationResult
            { Index = 0, StatusCode = HttpStatusCode.Created, SessionToken = malformedToken };
            DistributedTransactionOperationResult result1 = new DistributedTransactionOperationResult
            { Index = 1, StatusCode = HttpStatusCode.Created, SessionToken = canonicalToken };

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
            mockResponse.Setup(r => r.IsSuccessStatusCode).Returns(true);
            mockResponse.Setup(r => r.Count).Returns(2);
            mockResponse.Setup(r => r[0]).Returns(result0);
            mockResponse.Setup(r => r[1]).Returns(result1);

            DistributedTransactionOperation op0 = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0,
                DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            op0.CollectionResourceId = collectionRid1;

            DistributedTransactionOperation op1 = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 1,
                DatabaseName, container2, new PartitionKey("pk2"), id: "doc2");
            op1.CollectionResourceId = collectionRid2;

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { op0, op1 },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            // No throw despite op 0's malformed token — isSessionConsistency is false.
            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                mockResponse.Object, serverRequest, mockSessionContainer.Object, isSessionConsistency: false, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton);

            // op 0's malformed token was traced and skipped (not merged).
            mockSessionContainer.Verify(
                s => s.SetSessionToken(
                    collectionRid1, It.IsAny<string>(), It.IsAny<INameValueCollection>()),
                Times.Never,
                "Op 0's malformed token must not be merged.");

            // op 1's valid token was still merged — the loop did not abort.
            mockSessionContainer.Verify(
                s => s.SetSessionToken(
                    collectionRid2,
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, container2),
                    It.Is<INameValueCollection>(h => h[HttpConstants.HttpHeaders.SessionToken] == canonicalToken)),
                Times.Once,
                "Op 1's valid token must still be merged when a malformed token is suppressed (no throw).");
        }

        [TestMethod]
        // Auto-resolution: under Session consistency, PrepareOperationsAsync stamps the collection
        // resource id and resolves a per-partition session token for ops without an explicit token. When
        // the routing map is unavailable (MockDocumentClient returns a null CollectionRoutingMap) the
        // operation's partition can't be resolved, so NO token is applied — the compound collection-wide
        // token is never substituted (mirrors GatewayStoreModel.TryResolveSessionTokenAsync returning no
        // token). Stamping the compound token would attach other partitions' progress to this single op.
        public async Task PrepareOperationsAsync_StampsRidButAppliesNoToken_WhenRoutingMapUnavailable()
        {
            const string compoundToken = "0:1#9#4=8#5=7";
            string collectionPath = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);

            SessionContainer sessionContainer = new SessionContainer("testhost");
            INameValueCollection seedHeaders = new RequestNameValueCollection();
            seedHeaders[HttpConstants.HttpHeaders.SessionToken] = compoundToken;
            sessionContainer.SetSessionToken(CollectionResourceId, collectionPath, seedHeaders);

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer, responseContent: null, statusCode: HttpStatusCode.OK);

            DistributedTransactionOperation operation = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0,
                DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");

            await DistributedTransactionCommitterUtils.PrepareOperationsAsync(
                new List<DistributedTransactionOperation> { operation },
                mockContext.Object,
                isSessionConsistency: true,
                CancellationToken.None);

            Assert.AreEqual(CollectionResourceId, operation.CollectionResourceId,
                "PrepareOperationsAsync must stamp the resolved collection resource id on the operation.");
            Assert.IsTrue(string.IsNullOrEmpty(operation.SessionToken),
                "When the routing map is unavailable the partition can't be resolved, so no token must be applied; " +
                "the compound collection-wide token must NOT be substituted onto a single operation.");
        }

        [TestMethod]
        // Auto-resolution must NOT override a user-provided session token: if the operation already
        // carries a token, PrepareOperationsAsync leaves it untouched (it still stamps the rid).
        public async Task PrepareOperationsAsync_PreservesUserProvidedToken()
        {
            const string userToken = "0:1#42";
            string collectionPath = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);

            SessionContainer sessionContainer = new SessionContainer("testhost");
            // Seed a DIFFERENT token; it must be ignored because the op already carries one.
            INameValueCollection seedHeaders = new RequestNameValueCollection();
            seedHeaders[HttpConstants.HttpHeaders.SessionToken] = "0:1#9#4=8#5=7";
            sessionContainer.SetSessionToken(CollectionResourceId, collectionPath, seedHeaders);

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer, responseContent: null, statusCode: HttpStatusCode.OK);

            DistributedTransactionOperation operation = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0,
                DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            operation.SessionToken = userToken;

            await DistributedTransactionCommitterUtils.PrepareOperationsAsync(
                new List<DistributedTransactionOperation> { operation },
                mockContext.Object,
                isSessionConsistency: true,
                CancellationToken.None);

            Assert.AreEqual(userToken, operation.SessionToken,
                "A user-provided session token must not be overridden by auto-resolution.");
        }

        [TestMethod]
        // Deserialization correctness: if the coordinator ever includes a session token alongside
        // a failed op (the current contract returns empty, but the field is still parsed),
        // the SDK preserves it on the result object without crashing or discarding.
        public async Task OperationResult_SessionTokenAccessible_AfterFailedOpStatus()
        {
            const string canonicalToken = "0:1#9#4=8#5=7";

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                CreateTestOperations(),
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            // Simulate a failure status (412) with a valid session token in the response body
            string responseJson = BuildDtcResponseJson(new[]
            {
                (statusCode: 412, subStatusCode: (int?)null, sessionToken: canonicalToken, partitionKeyRangeId: (string)null),
            });

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(responseJson))
            };

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.PreconditionFailed, response[0].StatusCode,
                "Status code should reflect the op-level failure.");
            Assert.AreEqual(canonicalToken, response[0].SessionToken,
                "SessionToken must be accessible on failed op results so callers can pass it forward " +
                "for read-your-writes consistency on subsequent ops.");
        }

        [TestMethod]
        // v2 edge case #3 documentation: SessionContainer.SetSessionToken is internally thread-safe
        // (rwlock-protected). Concurrent MergeSessionTokens calls produce a consistent final state
        // — this matches GatewayStoreModel's behavior and is acceptable eventually-consistent semantics.
        public async Task MergeSessionTokens_ConcurrentInvocations_DoNotCorruptSessionContainer()
        {
            const string token0 = "0:1#5";
            const string token1 = "0:1#10"; // higher LSN — should win
            string collectionRid = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionPath = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);

            SessionContainer sessionContainer = new SessionContainer("testhost");
            // Pre-register the collection so SetSessionToken is known
            INameValueCollection seedHeaders = new RequestNameValueCollection();
            seedHeaders[HttpConstants.HttpHeaders.SessionToken] = "0:1#1";
            sessionContainer.SetSessionToken(collectionRid, collectionPath, seedHeaders);

            DistributedTransactionOperation MakeOp(int idx)
            {
                DistributedTransactionOperation op = new DistributedTransactionOperation(
                    OperationType.Create, operationIndex: idx,
                    DatabaseName, ContainerName, new PartitionKey($"pk{idx}"), id: $"doc{idx}");
                op.CollectionResourceId = collectionRid;
                return op;
            }

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { MakeOp(0), MakeOp(1) },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            Mock<DistributedTransactionResponse> BuildResp(string tA, string tB)
            {
                DistributedTransactionOperationResult r0 = new DistributedTransactionOperationResult
                { Index = 0, StatusCode = HttpStatusCode.Created, SessionToken = tA };
                DistributedTransactionOperationResult r1 = new DistributedTransactionOperationResult
                { Index = 1, StatusCode = HttpStatusCode.Created, SessionToken = tB };
                Mock<DistributedTransactionResponse> m = new Mock<DistributedTransactionResponse>();
                m.Setup(r => r.Count).Returns(2);
                m.Setup(r => r[0]).Returns(r0);
                m.Setup(r => r[1]).Returns(r1);
                return m;
            }

            // Fire many concurrent merges with interleaved tokens
            const int parallelism = 50;
            Task[] tasks = new Task[parallelism];
            for (int i = 0; i < parallelism; i++)
            {
                bool useHigh = (i % 2 == 0);
                Mock<DistributedTransactionResponse> resp = useHigh
                    ? BuildResp(token0, token1)
                    : BuildResp(token1, token0);
                tasks[i] = Task.Run(() => DistributedTransactionCommitter.MergeSessionTokensAsync(
                    resp.Object, serverRequest, sessionContainer, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton));
            }

            await Task.WhenAll(tasks);

            // SessionContainer survived concurrent updates without throwing or corrupting
            string finalToken = sessionContainer.GetSessionToken(collectionPath);
            Assert.IsFalse(string.IsNullOrEmpty(finalToken),
                "SessionContainer should hold a valid token after concurrent merges.");
            Assert.IsTrue(finalToken.StartsWith("0:"),
                $"Final token should still be canonical. Actual: {finalToken}");
        }

        [TestMethod]
        // CosmosException from MergeSessionTokens (malformed token) does NOT
        // trigger the DTX retry loop — it propagates immediately. The transaction is already
        // committed at this point; retrying would duplicate it.
        public async Task CommitTransactionAsync_MalformedToken_DoesNotTriggerRetry()
        {
            const string lsnOnly = "1#9#4=8#5=7"; // malformed — no colon separator
            int callCount = 0;

            SessionContainer sessionContainer = new SessionContainer("testhost");

            string responseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: lsnOnly, partitionKeyRangeId: (string)null) });

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: responseJson,
                statusCode: HttpStatusCode.OK);

            // Track how many times ProcessResourceOperationStreamAsync is called
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
                .Returns(() =>
                {
                    callCount++;
                    return Task.FromResult(new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new MemoryStream(Encoding.UTF8.GetBytes(responseJson))
                    });
                });

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create, operationIndex: 0,
                    DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1")
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction);

            await Assert.ThrowsExceptionAsync<CosmosException>(
                () => committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None));

            Assert.AreEqual(1, callCount,
                "The HTTP call must happen exactly once — CosmosException from token validation " +
                "must NOT trigger the retry loop (the transaction is already committed).");
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

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
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

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None))
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

        [TestMethod]
        [Description("Session tokens from DTX read response are merged into SessionContainer")]
        public async Task CommitTransactionAsync_MergesSessionTokens_ForReadOperations()
        {
            const string canonicalToken = "0:1#9#4=8#5=7";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            string responseJson = BuildDtcResponseJson(
                new[] { (statusCode: 200, subStatusCode: (int?)null, sessionToken: canonicalToken, partitionKeyRangeId: (string)null) });

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: responseJson,
                statusCode: HttpStatusCode.OK);

            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Read,
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
            Assert.AreEqual(canonicalToken, storedToken,
                "Session token from a DTX read response must be merged into SessionContainer.");
        }

        [TestMethod]
        [Description("DTX write followed by DTX read: the write response token is merged into the SessionContainer, but when the routing map is unavailable the subsequent read carries NO auto-resolved token — the compound collection-wide token is never substituted.")]
        public async Task CommitTransactionAsync_WriteTokensMerged_NoTokenSubstitutedOnRead_WhenRoutingUnavailable()
        {
            const string writeToken = "0:1#9#4=8#5=7";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            // First: DTX write commits and merges its session token
            string writeResponseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: writeToken, partitionKeyRangeId: (string)null) });

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: writeResponseJson,
                statusCode: HttpStatusCode.OK);

            List<DistributedTransactionOperation> writeOps = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter writeCommitter = new DistributedTransactionCommitter(
                writeOps, mockContext.Object, OperationType.CommitDistributedTransaction);

            await writeCommitter.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            // Verify write token was merged
            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.AreEqual(writeToken, storedToken, "Write token must be merged into SessionContainer.");

            // Second: DTX read — auto-resolution should pick up the merged write token
            byte[] capturedReadBody = null;
            string readResponseJson = BuildDtcResponseJson(
                new[] { (statusCode: 200, subStatusCode: (int?)null, sessionToken: "0:1#9#4=8#5=7", partitionKeyRangeId: (string)null) });

            Mock<CosmosClientContext> readMockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: readResponseJson,
                statusCode: HttpStatusCode.OK);

            readMockContext
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
                        capturedReadBody = copy.ToArray();
                    })
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new MemoryStream(Encoding.UTF8.GetBytes(readResponseJson))
                });

            List<DistributedTransactionOperation> readOps = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Read,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter readCommitter = new DistributedTransactionCommitter(
                readOps, readMockContext.Object, OperationType.CommitDistributedTransaction);

            await readCommitter.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(capturedReadBody);
            string bodyJson = Encoding.UTF8.GetString(capturedReadBody);
            // M4: the write token is merged into the SessionContainer (verified above), but the routing map is
            // unavailable in this mock, so the operation's partition can't be resolved. Auto-resolution must
            // therefore apply NO token — it must NOT substitute the compound collection-wide token onto the read.
            Assert.IsFalse(bodyJson.Contains("\"sessionToken\":"),
                $"When the routing map is unavailable, auto-resolution must not substitute the compound token onto the " +
                $"read; the request body must carry no session token. Body: {bodyJson}");
        }

        [TestMethod]
        [Description("DTX read auto-resolution happy path: when the routing map resolves the operation's partition to a range and the SessionContainer holds that range's token, exactly that per-partition token is stamped onto the read request body (never the compound collection-wide token).")]
        public async Task CommitTransactionAsync_ReadStampsPerPartitionToken_WhenRoutingMapResolvesRange()
        {
            const string rangeId = "0";
            // The value seeded into the SessionContainer (range-prefixed) and the value expected on the wire are
            // identical: resolution round-trips "{rangeId}:{lsn}" for a range that has its own token.
            const string partitionLocalToken = "0:1#100#4=90#5=2";

            string collectionFullName = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);

            // Seed the SessionContainer with a per-partition token for range "0" under this collection's rid.
            SessionContainer sessionContainer = new SessionContainer("testhost");
            sessionContainer.SetSessionToken(
                CollectionResourceId,
                collectionFullName,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, partitionLocalToken } });

            // A complete single-range routing map ("" .. "FF") so the operation's effective partition key resolves
            // to range "0". IsCompleteSetOfRanges requires exactly these min/max sentinels; every valid effective
            // partition key is < "FF", so any partition key resolves into this single range.
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap =
                Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap.TryCreateCompleteRoutingMap(
                    new[]
                    {
                        Tuple.Create(
                            new PartitionKeyRange { Id = rangeId, MinInclusive = "", MaxExclusive = "FF" },
                            (ServiceIdentity)null)
                    },
                    string.Empty,
                    false);
            Assert.IsNotNull(routingMap, "Test setup: complete routing map must be constructible.");

            byte[] capturedBody = null;
            string responseJson = BuildDtcResponseJson(
                new[] { (statusCode: 200, subStatusCode: (int?)null, sessionToken: (string)null, partitionKeyRangeId: (string)null) });

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: responseJson,
                statusCode: HttpStatusCode.OK,
                routingMap: routingMap);

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
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new MemoryStream(Encoding.UTF8.GetBytes(responseJson))
                });

            List<DistributedTransactionOperation> readOps = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Read,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                readOps, mockContext.Object, OperationType.CommitDistributedTransaction);

            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(capturedBody);
            string bodyJson = Encoding.UTF8.GetString(capturedBody);
            Assert.IsTrue(
                bodyJson.Contains($"\"sessionToken\":\"{partitionLocalToken}\""),
                $"The resolved per-partition token must be stamped onto the read request body. Body: {bodyJson}");
        }

        [TestMethod]
        [Description("DTX write followed by DTX read: explicit requestOptions.SessionToken on read takes precedence over auto-resolved token from SessionContainer")]
        public async Task CommitTransactionAsync_ExplicitSessionTokenOnRead_TakesPrecedenceOverAutoResolved()
        {
            const string writeToken = "0:1#9#4=8#5=7";
            const string explicitReadToken = "0:2#10#4=9#5=8";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            // First: DTX write commits and merges its session token
            string writeResponseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: writeToken, partitionKeyRangeId: (string)null) });

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: writeResponseJson,
                statusCode: HttpStatusCode.OK);

            List<DistributedTransactionOperation> writeOps = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter writeCommitter = new DistributedTransactionCommitter(
                writeOps, mockContext.Object, OperationType.CommitDistributedTransaction);

            await writeCommitter.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            // Verify write token was merged
            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.AreEqual(writeToken, storedToken);

            // Second: DTX read with EXPLICIT session token (should override auto-resolved)
            byte[] capturedReadBody = null;
            string readResponseJson = BuildDtcResponseJson(
                new[] { (statusCode: 200, subStatusCode: (int?)null, sessionToken: "0:2#10#4=9#5=8", partitionKeyRangeId: (string)null) });

            Mock<CosmosClientContext> readMockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: readResponseJson,
                statusCode: HttpStatusCode.OK);

            readMockContext
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
                        capturedReadBody = copy.ToArray();
                    })
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new MemoryStream(Encoding.UTF8.GetBytes(readResponseJson))
                });

            List<DistributedTransactionOperation> readOps = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Read,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = explicitReadToken })
            };

            DistributedTransactionCommitter readCommitter = new DistributedTransactionCommitter(
                readOps, readMockContext.Object, OperationType.CommitDistributedTransaction);

            await readCommitter.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(capturedReadBody);
            string bodyJson = Encoding.UTF8.GetString(capturedReadBody);
            Assert.IsTrue(bodyJson.Contains($"\"sessionToken\":\"{explicitReadToken}\""),
                $"Explicit session token must appear in request body (not the auto-resolved one). Body: {bodyJson}");
            Assert.IsFalse(bodyJson.Contains($"\"sessionToken\":\"{writeToken}\""),
                $"Auto-resolved write token must NOT appear when explicit token is set. Body: {bodyJson}");
        }

        [TestMethod]
        [Description("Session tokens in both DTX write and read responses are validated for canonical format during deserialization")]
        public async Task CommitTransactionAsync_ValidatesSessionToken_InBothWriteAndReadResponses()
        {
            const string malformedToken = "1#9#4=8#5=7"; // LSN-only, missing pkRangeId prefix

            // Test write response with malformed token
            SessionContainer sessionContainer = new SessionContainer("testhost");
            string writeResponseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: malformedToken, partitionKeyRangeId: (string)null) });

            Mock<CosmosClientContext> writeMockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: writeResponseJson,
                statusCode: HttpStatusCode.OK);

            List<DistributedTransactionOperation> writeOps = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter writeCommitter = new DistributedTransactionCommitter(
                writeOps, writeMockContext.Object, OperationType.CommitDistributedTransaction);

            await Assert.ThrowsExceptionAsync<CosmosException>(
                () => writeCommitter.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None),
                "Malformed session token in write response must throw CosmosException.");

            // Test read response with malformed token
            string readResponseJson = BuildDtcResponseJson(
                new[] { (statusCode: 200, subStatusCode: (int?)null, sessionToken: malformedToken, partitionKeyRangeId: (string)null) });

            Mock<CosmosClientContext> readMockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: readResponseJson,
                statusCode: HttpStatusCode.OK);

            List<DistributedTransactionOperation> readOps = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Read,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter readCommitter = new DistributedTransactionCommitter(
                readOps, readMockContext.Object, OperationType.CommitDistributedTransaction);

            await Assert.ThrowsExceptionAsync<CosmosException>(
                () => readCommitter.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None),
                "Malformed session token in read response must throw CosmosException.");
        }

        [TestMethod]
        [Description("Session tokens merged from DTX write are usable in subsequent point reads via SessionContainer")]
        public async Task CommitTransactionAsync_MergedTokens_UsableInSubsequentPointReads()
        {
            const string canonicalToken = "0:1#9#4=8#5=7";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            string responseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: canonicalToken, partitionKeyRangeId: (string)null) });

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

            // Verify the token is retrievable from SessionContainer (same call path that
            // GatewayStoreModel.ApplySessionTokenAsync uses for point-reads)
            string collectionFullName = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);
            string resolvedToken = sessionContainer.GetSessionToken(collectionFullName);
            Assert.AreEqual(canonicalToken, resolvedToken,
                "Merged DTX session token must be retrievable from SessionContainer for subsequent point-read resolution.");
        }

        [TestMethod]
        [Description("Session tokens are only merged on success attempts; retriable failure responses have empty tokens (DTC contract)")]
        public async Task CommitTransactionAsync_MergesSessionTokens_OnlyOnSuccessAttempt()
        {
            const string successToken = "0:1#9#4=8#5=7";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            // First attempt: retriable failure with empty session token (DTC contract)
            string retriableResponseJson = $"{{\"isRetriable\":true,\"operationResponses\":[{{\"index\":0,\"statuscode\":503,\"sessionToken\":\"\"}}]}}";
            // Second attempt: success with valid token
            string successResponseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: successToken, partitionKeyRangeId: (string)null) });

            int callCount = 0;

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
                    It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProperties);

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
                .Returns(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return Task.FromResult(new ResponseMessage(HttpStatusCode.ServiceUnavailable)
                        {
                            Content = new MemoryStream(Encoding.UTF8.GetBytes(retriableResponseJson))
                        });
                    }

                    return Task.FromResult(new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new MemoryStream(Encoding.UTF8.GetBytes(successResponseJson))
                    });
                });

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
                operations, mockContext.Object, OperationType.CommitDistributedTransaction, retryBaseDelay: TimeSpan.Zero);

            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            // Only the success attempt's token should be in SessionContainer
            string collectionFullName = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);
            string resolvedToken = sessionContainer.GetSessionToken(collectionFullName);
            Assert.AreEqual(successToken, resolvedToken,
                "Only the success attempt's session token should be merged (DTC returns empty tokens on failures).");
            Assert.AreEqual(2, callCount, "Should have made exactly 2 attempts (1 retry + 1 success).");
        }

        [TestMethod]
        [Description("DTX write with multiple ops on the same partition — SessionContainer merges to the latest (highest LSN) token")]
        public async Task CommitTransactionAsync_SamePartition_MergesLatestToken()
        {
            // Two ops on same collection, same pkRangeId (0) but different LSNs
            const string lowerToken = "0:1#5#4=3#5=2";
            const string higherToken = "0:1#9#4=8#5=7";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            string responseJson = BuildDtcResponseJson(
                new[]
                {
                    (statusCode: 201, subStatusCode: (int?)null, sessionToken: lowerToken, partitionKeyRangeId: (string)null),
                    (statusCode: 201, subStatusCode: (int?)null, sessionToken: higherToken, partitionKeyRangeId: (string)null),
                });

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
                    id: "doc1"),
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 1,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc2"),
            };

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                operations, mockContext.Object, OperationType.CommitDistributedTransaction);

            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            string collectionFullName = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);
            string resolvedToken = sessionContainer.GetSessionToken(collectionFullName);
            Assert.AreEqual(higherToken, resolvedToken,
                "When multiple ops target the same partition, SessionContainer must merge to the highest LSN token.");
        }

        [TestMethod]
        [Description("End user can extract SessionToken from DistributedTransactionOperationResult and pass it back via DistributedTransactionRequestOptions.SessionToken")]
        public async Task CommitTransactionAsync_UserCanExtractAndPassBackSessionToken()
        {
            const string responseToken = "0:1#9#4=8#5=7";

            SessionContainer sessionContainer = new SessionContainer("testhost");

            // First DTX write returns a session token
            string writeResponseJson = BuildDtcResponseJson(
                new[] { (statusCode: 201, subStatusCode: (int?)null, sessionToken: responseToken, partitionKeyRangeId: (string)null) });

            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: writeResponseJson,
                statusCode: HttpStatusCode.OK);

            List<DistributedTransactionOperation> writeOps = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1")
            };

            DistributedTransactionCommitter writeCommitter = new DistributedTransactionCommitter(
                writeOps, mockContext.Object, OperationType.CommitDistributedTransaction);

            DistributedTransactionResponse writeResponse = await writeCommitter.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            // User extracts the session token from the response
            string extractedToken = writeResponse[0].SessionToken;
            Assert.AreEqual(responseToken, extractedToken,
                "User must be able to extract SessionToken from DistributedTransactionOperationResult.");

            // User passes extracted token back on a subsequent DTX operation via requestOptions
            byte[] capturedBody = null;
            string readResponseJson = BuildDtcResponseJson(
                new[] { (statusCode: 200, subStatusCode: (int?)null, sessionToken: responseToken, partitionKeyRangeId: (string)null) });

            Mock<CosmosClientContext> readMockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: readResponseJson,
                statusCode: HttpStatusCode.OK);

            readMockContext
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
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new MemoryStream(Encoding.UTF8.GetBytes(readResponseJson))
                });

            List<DistributedTransactionOperation> readOps = new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Read,
                    operationIndex: 0,
                    DatabaseName,
                    ContainerName,
                    new PartitionKey("pk1"),
                    id: "doc1",
                    requestOptions: new DistributedTransactionRequestOptions { SessionToken = extractedToken })
            };

            DistributedTransactionCommitter readCommitter = new DistributedTransactionCommitter(
                readOps, readMockContext.Object, OperationType.CommitDistributedTransaction);

            await readCommitter.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(capturedBody);
            string bodyJson = Encoding.UTF8.GetString(capturedBody);
            Assert.IsTrue(bodyJson.Contains($"\"sessionToken\":\"{extractedToken}\""),
                $"User-provided session token (extracted from prior response) must appear in request body. Body: {bodyJson}");
        }

        // ─── Per-partition resolver tests (ApplyTokensAsync) ─────────────────────

        [TestMethod]
        [Description("Auto-resolution: when the operation's partition resolves to a range that has no token in the SessionContainer, no token is applied (the compound collection-wide token is never substituted).")]
        public async Task ApplyTokens_ResolvedRangeHasNoToken_AppliesNoToken()
        {
            // Seed a token for range "5" only; the single-range map resolves every key to range "0",
            // which has no token — so the operation must be sent with no token (never a compound token).
            SessionContainer sessionContainer = SeedSessionContainer("5:1#100#4=90#5=2");
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap = BuildCompleteRoutingMap(("0", string.Empty, "FF", null));
            (DistributedTransactionSessionTokenResolver resolver, ContainerProperties containerProperties, string collectionPath) =
                await this.CreateResolverAsync(sessionContainer, routingMap);

            DistributedTransactionOperation op = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");

            await resolver.ApplyTokensAsync(new[] { op }, collectionPath, containerProperties);

            Assert.IsNull(op.SessionToken,
                "A partition that resolves to a tokenless range must receive no session token (never the compound token).");
        }

        [TestMethod]
        [Description("Auto-resolution with a multi-range routing map selects exactly the resolved range's per-partition token — not another range's token and never a compound (comma-joined) token.")]
        public async Task ApplyTokens_MultiRange_SelectsResolvedRangeToken()
        {
            const string token0 = "0:1#100#4=90#5=2";
            const string token1 = "1:1#200#4=91#5=3";
            SessionContainer sessionContainer = SeedSessionContainer(token0, token1);
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap = BuildCompleteRoutingMap(
                ("0", string.Empty, "3F", null),
                ("1", "3F", "FF", null));
            (DistributedTransactionSessionTokenResolver resolver, ContainerProperties containerProperties, string collectionPath) =
                await this.CreateResolverAsync(sessionContainer, routingMap);

            PartitionKey partitionKey = new PartitionKey("pk1");

            // Oracle: resolve the range the SAME way the resolver does, so the assertion is deterministic
            // regardless of how "pk1" hashes into the two ranges (no brittle hash-value assumptions).
            string effectiveKey = partitionKey.InternalKey.GetEffectivePartitionKeyString(containerProperties.PartitionKey);
            PartitionKeyRange expectedRange = routingMap.GetRangeByEffectivePartitionKey(effectiveKey);
            Assert.IsNotNull(expectedRange, "Test setup: the partition key must resolve to one of the two ranges.");
            string expectedToken = expectedRange.Id == "0" ? token0 : token1;
            string otherToken = expectedRange.Id == "0" ? token1 : token0;

            DistributedTransactionOperation op = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 0, DatabaseName, ContainerName, partitionKey, id: "doc1");

            await resolver.ApplyTokensAsync(new[] { op }, collectionPath, containerProperties);

            Assert.AreEqual(expectedToken, op.SessionToken, "Exactly the resolved range's token must be applied.");
            Assert.AreNotEqual(otherToken, op.SessionToken, "A different range's token must not be applied.");
            Assert.IsFalse(op.SessionToken != null && op.SessionToken.Contains(","),
                "A compound (comma-joined) collection-wide token must never be applied.");
        }

        [TestMethod]
        [Description("Auto-resolution guards unroutable partition keys: PartitionKey.None and default(PartitionKey) receive no token (never a wrong-partition token), while a normal key on the same resolvable path does get its range token.")]
        public async Task ApplyTokens_NoneOrDefaultPartitionKey_AppliesNoToken()
        {
            const string rangeToken = "0:1#100#4=90#5=2";
            SessionContainer sessionContainer = SeedSessionContainer(rangeToken);
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap = BuildCompleteRoutingMap(("0", string.Empty, "FF", null));
            (DistributedTransactionSessionTokenResolver resolver, ContainerProperties containerProperties, string collectionPath) =
                await this.CreateResolverAsync(sessionContainer, routingMap);

            DistributedTransactionOperation normalOp = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "normal");
            DistributedTransactionOperation noneOp = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 1, DatabaseName, ContainerName, PartitionKey.None, id: "none");
            DistributedTransactionOperation defaultOp = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 2, DatabaseName, ContainerName, default(PartitionKey), id: "default");

            await resolver.ApplyTokensAsync(new[] { normalOp, noneOp, defaultOp }, collectionPath, containerProperties);

            Assert.AreEqual(rangeToken, normalOp.SessionToken,
                "Positive control: a routable key must receive its range's token, proving the setup would otherwise apply a token.");
            Assert.IsNull(noneOp.SessionToken, "PartitionKey.None is unroutable and must receive no token.");
            Assert.IsNull(defaultOp.SessionToken,
                "default(PartitionKey) has a null InternalKey and must receive no token (no wrong-partition token, no NullReferenceException).");
        }

        [TestMethod]
        [Description("An operation that already carries an explicit session token keeps it: auto-resolution must not override it even when the routing map + SessionContainer would resolve a (different) per-partition token.")]
        public async Task ApplyTokens_ExplicitUserToken_NotOverriddenByResolvedToken()
        {
            const string seededRangeToken = "0:1#100#4=90#5=2";
            const string explicitUserToken = "0:9#999#4=99#5=9";
            SessionContainer sessionContainer = SeedSessionContainer(seededRangeToken);
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap = BuildCompleteRoutingMap(("0", string.Empty, "FF", null));
            (DistributedTransactionSessionTokenResolver resolver, ContainerProperties containerProperties, string collectionPath) =
                await this.CreateResolverAsync(sessionContainer, routingMap);

            DistributedTransactionOperation op = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1",
                requestOptions: new DistributedTransactionRequestOptions { SessionToken = explicitUserToken });

            await resolver.ApplyTokensAsync(new[] { op }, collectionPath, containerProperties);

            Assert.AreEqual(explicitUserToken, op.SessionToken,
                "The explicit user-supplied token must win; the guard must prevent the resolved range token from overriding it.");
        }

        [TestMethod]
        [Description("A freshly-split child range (range.Parents populated) with no token of its own inherits the parent's per-partition token through the resolver + routing map path (range.Parents is forwarded to GetSessionTokenForPartitionKeyRange).")]
        public async Task ApplyTokens_SplitChildRange_InheritsParentToken()
        {
            const string parentToken = "0:1#100#4=90#5=2";
            SessionContainer sessionContainer = SeedSessionContainer(parentToken);
            // The routing map exposes a single child range "1" whose parent is "0"; range "1" has no token
            // of its own, so resolution must walk to parent "0" via the forwarded range.Parents.
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap = BuildCompleteRoutingMap(("1", string.Empty, "FF", new[] { "0" }));
            (DistributedTransactionSessionTokenResolver resolver, ContainerProperties containerProperties, string collectionPath) =
                await this.CreateResolverAsync(sessionContainer, routingMap);

            DistributedTransactionOperation op = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");

            await resolver.ApplyTokensAsync(new[] { op }, collectionPath, containerProperties);

            Assert.IsNotNull(op.SessionToken,
                "A split child with a known parent must inherit the parent's token via range.Parents forwarding.");
            string expectedInherited = "1:" + parentToken.Substring("0:".Length);
            Assert.AreEqual(expectedInherited, op.SessionToken,
                "The inherited token must equal the parent's token re-tagged with the child range id.");
        }

        [TestMethod]
        [Description("Parity with AddressResolver.TryResolveServerPartitionByPartitionKey: a PARTIAL hierarchical partition key (fewer components than the definition's paths) spans multiple ranges and is unroutable to one range, so it receives no token — while a FULL key on the same definition resolves its range token (positive control).")]
        public async Task ApplyTokens_PartialHierarchicalPartitionKey_AppliesNoToken()
        {
            const string rangeToken = "0:1#100#4=90#5=2";
            SessionContainer sessionContainer = SeedSessionContainer(rangeToken);
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap = BuildCompleteRoutingMap(("0", string.Empty, "FF", null));
            (DistributedTransactionSessionTokenResolver resolver, _, string collectionPath) =
                await this.CreateResolverAsync(sessionContainer, routingMap);

            // Two-path hierarchical (sub-partitioned) definition, overriding the helper's single-path "/pk".
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId(CollectionResourceId);
            containerProperties.Id = "TestContainerId";
            containerProperties.PartitionKey = new PartitionKeyDefinition
            {
                Kind = PartitionKind.MultiHash,
                Paths = new System.Collections.ObjectModel.Collection<string> { "/tenant", "/user" },
                Version = Microsoft.Azure.Documents.PartitionKeyDefinitionVersion.V2
            };

            // Positive control: a FULL two-component key resolves into the single range "0" and gets its token.
            DistributedTransactionOperation fullOp = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 0, DatabaseName, ContainerName,
                new PartitionKeyBuilder().Add("tenant1").Add("user1").Build(), id: "full");

            // A PARTIAL one-component prefix key spans multiple ranges → no token (never a wrong-partition token).
            DistributedTransactionOperation partialOp = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 1, DatabaseName, ContainerName,
                new PartitionKeyBuilder().Add("tenant1").Build(), id: "partial");

            await resolver.ApplyTokensAsync(new[] { fullOp, partialOp }, collectionPath, containerProperties);

            Assert.AreEqual(rangeToken, fullOp.SessionToken,
                "Positive control: a full hierarchical key must receive its range's token, proving the setup would otherwise apply a token.");
            Assert.IsNull(partialOp.SessionToken,
                "A partial hierarchical prefix key spans multiple ranges and must receive no session token (parity with AddressResolver's component-count guard).");
        }

        // ─── Post-commit merge tests (MergeSessionTokens) ────────────────────────

        [TestMethod]
        // HIGHEST-VALUE cross-SDK gap (peer Java RegionScopedSessionContainerConcurrencyTest): a real region-set
        // MISMATCH during the post-commit merge. Seed range "0" with a two-region token, then merge a one-region
        // token for the same range: VectorSessionToken.Merge throws InternalServerErrorException (region-count
        // mismatch) inside the real SetSessionToken — the AddSessionToken merge path (not a mock, not the
        // concurrent-add race). Under Session consistency on a committed response the DTX classifier surfaces it
        // as a non-retriable CosmosException; under non-Session it traces-and-skips. LOCKS IN current behavior.
        public async Task MergeSessionTokens_RealContainer_RegionSetMismatchMerge_ThrowsUnderSession()
        {
            const string seededTwoRegionToken = "0:1#100#4=90#5=2"; // regions {4,5}
            const string mergeOneRegionToken = "0:1#101#4=91";      // region {4} only -> merge mismatch
            string collectionFullName = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);

            SessionContainer sessionContainer = new SessionContainer("testhost");
            sessionContainer.SetSessionToken(
                CollectionResourceId,
                collectionFullName,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, seededTwoRegionToken } });

            DistributedTransactionOperationResult result = new DistributedTransactionOperationResult
            { Index = 0, StatusCode = HttpStatusCode.Created, SessionToken = mergeOneRegionToken };

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
            mockResponse.Setup(r => r.IsSuccessStatusCode).Returns(true);
            mockResponse.Setup(r => r.Count).Returns(1);
            mockResponse.Setup(r => r[0]).Returns(result);

            DistributedTransactionOperation operation = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            operation.CollectionResourceId = CollectionResourceId;

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { operation },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            // Session consistency on a committed response: surfaced as a non-retriable CosmosException.
            CosmosException ex = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => DistributedTransactionCommitter.MergeSessionTokensAsync(
                    mockResponse.Object, serverRequest, sessionContainer, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton),
                "A region-set-mismatch merge on a committed transaction under Session consistency must surface as a non-retriable CosmosException.");
            Assert.AreEqual(HttpStatusCode.InternalServerError, ex.StatusCode);

            // The failed merge must not corrupt the previously stored progress.
            Assert.AreEqual(seededTwoRegionToken, sessionContainer.GetSessionToken(collectionFullName),
                "A failed region-mismatch merge must leave the previously stored token intact.");

            // Non-Session consistency: traced-and-skipped, no exception escapes and the stored token is unchanged.
            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                mockResponse.Object, serverRequest, sessionContainer, isSessionConsistency: false, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton);
            Assert.AreEqual(seededTwoRegionToken, sessionContainer.GetSessionToken(collectionFullName),
                "Under non-Session consistency the mismatch is skipped and the stored token remains intact.");
        }

        [TestMethod]
        // Locks the reachable malformed-token exception surface. The SDK always parses stored tokens with the fixed
        // modern CurrentVersion, so SessionTokenHelper.Parse only ever yields VectorSessionToken and REJECTS a legacy
        // non-Vector-format lsn (e.g. "100") as invalid BEFORE any merge — surfacing a classified
        // InternalServerErrorException. This means a Vector-vs-Simple type-mismatch merge is unreachable via
        // SetSessionToken; the DTX classifier (BadRequestException || InternalServerErrorException) already covers
        // every exception the merge path can actually throw. Under Session consistency on a committed response the
        // rejection surfaces as a non-retriable CosmosException; under non-Session it traces-and-skips.
        public async Task MergeSessionTokens_RealContainer_LegacyFormatToken_RejectedByParseUnderSession()
        {
            const string seededVectorToken = "0:1#100#4=90#5=2"; // valid Vector token already stored
            const string mergeLegacyToken = "0:100";             // legacy non-Vector lsn -> rejected by Parse
            string collectionFullName = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);

            SessionContainer sessionContainer = new SessionContainer("testhost");
            sessionContainer.SetSessionToken(
                CollectionResourceId,
                collectionFullName,
                new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, seededVectorToken } });

            DistributedTransactionOperationResult result = new DistributedTransactionOperationResult
            { Index = 0, StatusCode = HttpStatusCode.Created, SessionToken = mergeLegacyToken };

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
            mockResponse.Setup(r => r.IsSuccessStatusCode).Returns(true);
            mockResponse.Setup(r => r.Count).Returns(1);
            mockResponse.Setup(r => r[0]).Returns(result);

            DistributedTransactionOperation operation = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            operation.CollectionResourceId = CollectionResourceId;

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { operation },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            // Session consistency on a committed response: surfaced as a non-retriable CosmosException.
            CosmosException ex = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => DistributedTransactionCommitter.MergeSessionTokensAsync(
                    mockResponse.Object, serverRequest, sessionContainer, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton),
                "A legacy-format token rejected by Parse on a committed transaction under Session consistency must surface as a non-retriable CosmosException.");
            Assert.AreEqual(HttpStatusCode.InternalServerError, ex.StatusCode);

            // The rejected token must not corrupt the previously stored progress.
            Assert.AreEqual(seededVectorToken, sessionContainer.GetSessionToken(collectionFullName),
                "A rejected legacy-format token must leave the previously stored token intact.");

            // Non-Session consistency: traced-and-skipped, no exception escapes and the stored token is unchanged.
            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                mockResponse.Object, serverRequest, sessionContainer, isSessionConsistency: false, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton);
            Assert.AreEqual(seededVectorToken, sessionContainer.GetSessionToken(collectionFullName),
                "Under non-Session consistency the rejected token is skipped and the stored token remains intact.");
        }

        [TestMethod]
        // A successful sub-op whose response carries NO session token must be skipped entirely — SetSessionToken
        // is never called (the absent-token guard short-circuits before the write path).
        public async Task MergeSessionTokens_SuccessOpWithNoToken_SkipsSetSessionToken()
        {
            string collectionRid = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();

            DistributedTransactionOperationResult result = new DistributedTransactionOperationResult
            { Index = 0, StatusCode = HttpStatusCode.Created, SessionToken = null };

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
            mockResponse.Setup(r => r.IsSuccessStatusCode).Returns(true);
            mockResponse.Setup(r => r.Count).Returns(1);
            mockResponse.Setup(r => r[0]).Returns(result);

            DistributedTransactionOperation operation = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            operation.CollectionResourceId = collectionRid;

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { operation },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            // Must not throw and must not touch the SessionContainer.
            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                mockResponse.Object, serverRequest, mockSessionContainer.Object, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton);

            mockSessionContainer.Verify(
                s => s.SetSessionToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<INameValueCollection>()),
                Times.Never,
                "A successful op with no session token must not invoke SetSessionToken.");
        }

        [TestMethod]
        // Multi-collection DTX: each operation's token is merged into ITS OWN collection (keyed by collection
        // resource id), so a subsequent Session read of either collection resolves the right token.
        public async Task MergeSessionTokens_MultipleCollections_EachTokenStoredUnderItsCollection()
        {
            const string container1 = "container1";
            const string container2 = "container2";
            const string token1 = "0:1#100#4=90#5=2";
            const string token2 = "0:1#200#4=91#5=3";
            string collectionRid1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionRid2 = ResourceId.NewDocumentCollectionId(42, 130).DocumentCollectionId.ToString();

            SessionContainer sessionContainer = new SessionContainer("testhost");

            DistributedTransactionOperationResult r0 = new DistributedTransactionOperationResult
            { Index = 0, StatusCode = HttpStatusCode.Created, SessionToken = token1 };
            DistributedTransactionOperationResult r1 = new DistributedTransactionOperationResult
            { Index = 1, StatusCode = HttpStatusCode.Created, SessionToken = token2 };

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
            mockResponse.Setup(r => r.IsSuccessStatusCode).Returns(true);
            mockResponse.Setup(r => r.Count).Returns(2);
            mockResponse.Setup(r => r[0]).Returns(r0);
            mockResponse.Setup(r => r[1]).Returns(r1);

            DistributedTransactionOperation op0 = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0, DatabaseName, container1, new PartitionKey("pk1"), id: "doc1");
            op0.CollectionResourceId = collectionRid1;
            DistributedTransactionOperation op1 = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 1, DatabaseName, container2, new PartitionKey("pk2"), id: "doc2");
            op1.CollectionResourceId = collectionRid2;

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { op0, op1 },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                mockResponse.Object, serverRequest, sessionContainer, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton);

            Assert.AreEqual(token1,
                sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, container1)),
                "Collection 1 must store its own token.");
            Assert.AreEqual(token2,
                sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, container2)),
                "Collection 2 must store its own token.");
        }

        [TestMethod]
        // Cross-SDK gap (peer concurrency suites): concurrent post-commit merges on the SAME collection/range
        // must be thread-safe and must not corrupt the stored token. Many tasks merge distinct, mergeable
        // (same-region-set) tokens for range "0"; the final stored token must remain a single valid canonical
        // token (no compound, no torn write), with no exception escaping.
        public async Task MergeSessionTokens_ConcurrentMergesSameRange_NoCorruption()
        {
            const int writers = 32;
            string collectionFullName = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);
            SessionContainer sessionContainer = new SessionContainer("testhost");

            DistributedTransactionOperation operation = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            operation.CollectionResourceId = CollectionResourceId;
            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { operation },
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            IEnumerable<Task> merges = Enumerable.Range(1, writers).Select(i => Task.Run(async () =>
            {
                // Same region set {4,5}, varying global + regional LSNs so every pair is mergeable.
                string token = $"0:1#{100 + i}#4={90 + i}#5={2 + i}";
                DistributedTransactionOperationResult result = new DistributedTransactionOperationResult
                { Index = 0, StatusCode = HttpStatusCode.Created, SessionToken = token };
                Mock<DistributedTransactionResponse> response = new Mock<DistributedTransactionResponse>();
                response.Setup(r => r.IsSuccessStatusCode).Returns(true);
                response.Setup(r => r.Count).Returns(1);
                response.Setup(r => r[0]).Returns(result);

                await DistributedTransactionCommitter.MergeSessionTokensAsync(
                    response.Object, serverRequest, sessionContainer, isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton);
            }));

            await Task.WhenAll(merges);

            string finalToken = sessionContainer.GetSessionToken(collectionFullName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(finalToken), "A token must be stored after concurrent merges.");
            Assert.IsTrue(SessionContainer.IsCanonicalSessionTokenShape(finalToken),
                $"The stored token must remain a single canonical '{{pkRangeId}}:{{lsn}}' token after concurrent merges. Actual: {finalToken}");
            Assert.IsFalse(finalToken.Contains(","),
                $"Concurrent merges on one range must not produce a compound multi-range token. Actual: {finalToken}");
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
            HttpStatusCode statusCode,
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap = null)
        {
            // When a routing map is supplied, use a document client whose PartitionKeyRangeCache resolves it so
            // the per-partition token happy path (GetRangeByEffectivePartitionKey → GetSessionTokenForPartitionKeyRange)
            // can be exercised. Otherwise use the shared MockDocumentClient, whose cache returns a null routing map
            // and therefore exercises the "partition unresolvable → no token applied" branch.
            MockDocumentClient documentClient = routingMap == null
                ? new MockDocumentClient
                {
                    sessionContainer = sessionContainer
                }
                : new RoutingMapMockDocumentClient(routingMap)
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

        private static SessionContainer SeedSessionContainer(params string[] tokens)
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");
            string collectionFullName = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);
            foreach (string token in tokens)
            {
                sessionContainer.SetSessionToken(
                    CollectionResourceId,
                    collectionFullName,
                    new RequestNameValueCollection() { { HttpConstants.HttpHeaders.SessionToken, token } });
            }

            return sessionContainer;
        }

        private static Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap BuildCompleteRoutingMap(
            params (string id, string min, string max, string[] parents)[] ranges)
        {
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap =
                Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap.TryCreateCompleteRoutingMap(
                    ranges.Select(r => Tuple.Create(
                        new PartitionKeyRange
                        {
                            Id = r.id,
                            MinInclusive = r.min,
                            MaxExclusive = r.max,
                            Parents = r.parents == null ? null : new System.Collections.ObjectModel.Collection<string>(r.parents)
                        },
                        (ServiceIdentity)null)).ToArray(),
                    string.Empty,
                    false);
            Assert.IsNotNull(routingMap, "Test setup: complete routing map must be constructible.");
            return routingMap;
        }

        private async Task<(DistributedTransactionSessionTokenResolver resolver, ContainerProperties containerProperties, string collectionPath)> CreateResolverAsync(
            SessionContainer sessionContainer,
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap)
        {
            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                sessionContainer,
                responseContent: null,
                statusCode: HttpStatusCode.OK,
                routingMap: routingMap);

            DistributedTransactionSessionTokenResolver resolver =
                await DistributedTransactionSessionTokenResolver.TryCreateAsync(mockContext.Object, isSessionConsistency: true);
            Assert.IsNotNull(resolver,
                "Test setup: TryCreateAsync must return a resolver under Session consistency with the built-in SessionContainer.");

            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId(CollectionResourceId);
            containerProperties.Id = "TestContainerId";
            containerProperties.PartitionKeyPath = "/pk";

            string collectionPath = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);
            return (resolver, containerProperties, collectionPath);
        }

        /// <summary>
        /// A <see cref="MockDocumentClient"/> whose <see cref="PartitionKeyRangeCache"/> returns a real, complete
        /// routing map so the DTX per-partition token happy path can be exercised. The shared MockDocumentClient
        /// returns a null routing map, which only covers the "routing unavailable" branch.
        /// </summary>
        private sealed class RoutingMapMockDocumentClient : MockDocumentClient
        {
            private readonly Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache routingCache;

            public RoutingMapMockDocumentClient(Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap)
            {
                Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> cache =
                    new Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache>(null, null, null, null, false, false);
                cache
                    .Setup(m => m.TryLookupAsync(
                        It.IsAny<string>(),
                        It.IsAny<Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap>(),
                        It.IsAny<DocumentServiceRequest>(),
                        It.IsAny<ITrace>()))
                    .Returns(Task.FromResult(routingMap));
                this.routingCache = cache.Object;
            }

            internal override Task<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync(ITrace trace)
            {
                return Task.FromResult(this.routingCache);
            }
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

                json.Append($"{{\"index\":{i},\"statuscode\":200,\"substatuscode\":0,\"sessionToken\":\"{i}:1#100#1=50\"}}");
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

        // ─── Capture-side split / partition-move refresh (Phase 2, §7.2) ─────────

        private static Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> BuildRefreshTrackingCache()
        {
            Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> cache =
                new Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache>(null, null, null, null, false, false);
            cache
                .Setup(c => c.TryGetPartitionKeyRangeByIdAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<bool>()))
                .ReturnsAsync((PartitionKeyRange)null);
            return cache;
        }

        // Builds a committed (207-equivalent) response whose per-op served range and success can be scripted,
        // with each operation pre-stamped with its client-resolved range so the capture pass can compare them.
        private static async Task<(Mock<DistributedTransactionResponse> response, DistributedTransactionServerRequest serverRequest)> BuildRefreshScenarioAsync(
            params (string resolvedRange, string servedRange, HttpStatusCode status)[] ops)
        {
            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>();
            for (int i = 0; i < ops.Length; i++)
            {
                DistributedTransactionOperation op = new DistributedTransactionOperation(
                    OperationType.Create, operationIndex: i, DatabaseName, ContainerName, new PartitionKey("pk" + i), id: "doc" + i);
                op.CollectionResourceId = CollectionResourceId;
                op.ResolvedPartitionKeyRangeId = ops[i].resolvedRange;
                operations.Add(op);
            }

            DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                operations, MockCosmosUtil.Serializer, CancellationToken.None);

            Mock<DistributedTransactionResponse> response = new Mock<DistributedTransactionResponse>();
            response.Setup(r => r.Count).Returns(ops.Length);
            response.Setup(r => r.IsSuccessStatusCode).Returns(true);
            response.Setup(r => r.StatusCode).Returns(HttpStatusCode.OK);
            for (int i = 0; i < ops.Length; i++)
            {
                DistributedTransactionOperationResult result = new DistributedTransactionOperationResult
                {
                    Index = i,
                    StatusCode = ops[i].status,
                    PartitionKeyRangeId = ops[i].servedRange,
                    SessionToken = null,
                };
                int captured = i;
                response.Setup(r => r[captured]).Returns(result);
            }

            return (response, serverRequest);
        }

        [TestMethod]
        [Description("Capture-side split detection: when the server serves an op from a different range than the client resolved, the routing cache is force-refreshed for the served range (point-op parity).")]
        public async Task MergeSessionTokens_ForcesRefresh_WhenServerRangeDiffers()
        {
            Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> cache = BuildRefreshTrackingCache();
            (Mock<DistributedTransactionResponse> response, DistributedTransactionServerRequest serverRequest) =
                await BuildRefreshScenarioAsync(("0", "1", HttpStatusCode.Created));

            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                response.Object, serverRequest, new Mock<ISessionContainer>().Object,
                isSessionConsistency: true, partitionKeyRangeCache: cache.Object, trace: NoOpTrace.Singleton);

            cache.Verify(
                c => c.TryGetPartitionKeyRangeByIdAsync(CollectionResourceId, "1", It.IsAny<ITrace>(), true),
                Times.Once,
                "A moved partition (served range != resolved range) must force-refresh the routing cache once.");
        }

        [TestMethod]
        [Description("No refresh when the server served exactly the range the client resolved.")]
        public async Task MergeSessionTokens_NoRefresh_WhenRangesEqual()
        {
            Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> cache = BuildRefreshTrackingCache();
            (Mock<DistributedTransactionResponse> response, DistributedTransactionServerRequest serverRequest) =
                await BuildRefreshScenarioAsync(("0", "0", HttpStatusCode.Created));

            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                response.Object, serverRequest, new Mock<ISessionContainer>().Object,
                isSessionConsistency: true, partitionKeyRangeCache: cache.Object, trace: NoOpTrace.Singleton);

            cache.Verify(
                c => c.TryGetPartitionKeyRangeByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<bool>()),
                Times.Never,
                "Equal ranges must not trigger a refresh.");
        }

        [TestMethod]
        [Description("No refresh when the server did not report a served partition key range id.")]
        public async Task MergeSessionTokens_NoRefresh_WhenServerRangeMissing()
        {
            Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> cache = BuildRefreshTrackingCache();
            (Mock<DistributedTransactionResponse> response, DistributedTransactionServerRequest serverRequest) =
                await BuildRefreshScenarioAsync(("0", null, HttpStatusCode.Created));

            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                response.Object, serverRequest, new Mock<ISessionContainer>().Object,
                isSessionConsistency: true, partitionKeyRangeCache: cache.Object, trace: NoOpTrace.Singleton);

            cache.Verify(
                c => c.TryGetPartitionKeyRangeByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<bool>()),
                Times.Never,
                "A missing served range id must not trigger a refresh.");
        }

        [TestMethod]
        [Description("No refresh when the client never recorded a resolved range (e.g. user-token / unresolved op).")]
        public async Task MergeSessionTokens_NoRefresh_WhenResolvedRangeMissing()
        {
            Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> cache = BuildRefreshTrackingCache();
            (Mock<DistributedTransactionResponse> response, DistributedTransactionServerRequest serverRequest) =
                await BuildRefreshScenarioAsync((null, "1", HttpStatusCode.Created));

            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                response.Object, serverRequest, new Mock<ISessionContainer>().Object,
                isSessionConsistency: true, partitionKeyRangeCache: cache.Object, trace: NoOpTrace.Singleton);

            cache.Verify(
                c => c.TryGetPartitionKeyRangeByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<bool>()),
                Times.Never,
                "No client-resolved range means there is nothing to compare against; no refresh.");
        }

        [TestMethod]
        [Description("A null routing cache (auto-resolution disabled) must not throw and simply skips split detection.")]
        public async Task MergeSessionTokens_NoRefresh_WhenCacheNull()
        {
            (Mock<DistributedTransactionResponse> response, DistributedTransactionServerRequest serverRequest) =
                await BuildRefreshScenarioAsync(("0", "1", HttpStatusCode.Created));

            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                response.Object, serverRequest, new Mock<ISessionContainer>().Object,
                isSessionConsistency: true, partitionKeyRangeCache: null, trace: NoOpTrace.Singleton);
        }

        [TestMethod]
        [Description("A failed sub-op is skipped entirely, so its differing served range does not trigger a refresh.")]
        public async Task MergeSessionTokens_NoRefresh_ForFailedSubOp()
        {
            Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> cache = BuildRefreshTrackingCache();
            (Mock<DistributedTransactionResponse> response, DistributedTransactionServerRequest serverRequest) =
                await BuildRefreshScenarioAsync(("0", "1", HttpStatusCode.NotFound));

            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                response.Object, serverRequest, new Mock<ISessionContainer>().Object,
                isSessionConsistency: true, partitionKeyRangeCache: cache.Object, trace: NoOpTrace.Singleton);

            cache.Verify(
                c => c.TryGetPartitionKeyRangeByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<bool>()),
                Times.Never,
                "A failed sub-op must be skipped before split detection.");
        }

        [TestMethod]
        [Description("Refreshes are deduped per distinct moved range: two ops moving to the same range refresh once; two ops moving to two ranges refresh twice.")]
        public async Task MergeSessionTokens_RefreshesOncePerDistinctRange()
        {
            Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> sameRangeCache = BuildRefreshTrackingCache();
            (Mock<DistributedTransactionResponse> sameResponse, DistributedTransactionServerRequest sameRequest) =
                await BuildRefreshScenarioAsync(
                    ("0", "9", HttpStatusCode.Created),
                    ("0", "9", HttpStatusCode.Created));

            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                sameResponse.Object, sameRequest, new Mock<ISessionContainer>().Object,
                isSessionConsistency: true, partitionKeyRangeCache: sameRangeCache.Object, trace: NoOpTrace.Singleton);

            sameRangeCache.Verify(
                c => c.TryGetPartitionKeyRangeByIdAsync(CollectionResourceId, "9", It.IsAny<ITrace>(), true),
                Times.Once,
                "Two ops moving to the same range must force-refresh that range exactly once.");

            Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> twoRangeCache = BuildRefreshTrackingCache();
            (Mock<DistributedTransactionResponse> twoResponse, DistributedTransactionServerRequest twoRequest) =
                await BuildRefreshScenarioAsync(
                    ("0", "8", HttpStatusCode.Created),
                    ("0", "9", HttpStatusCode.Created));

            await DistributedTransactionCommitter.MergeSessionTokensAsync(
                twoResponse.Object, twoRequest, new Mock<ISessionContainer>().Object,
                isSessionConsistency: true, partitionKeyRangeCache: twoRangeCache.Object, trace: NoOpTrace.Singleton);

            twoRangeCache.Verify(
                c => c.TryGetPartitionKeyRangeByIdAsync(CollectionResourceId, "8", It.IsAny<ITrace>(), true),
                Times.Once);
            twoRangeCache.Verify(
                c => c.TryGetPartitionKeyRangeByIdAsync(CollectionResourceId, "9", It.IsAny<ITrace>(), true),
                Times.Once);
        }

        [TestMethod]
        [Description("A force-refresh failure is NOT swallowed: it propagates to the caller, matching point operations (which call the same force-refresh as a bare await).")]
        public async Task MergeSessionTokens_RefreshExceptionPropagates()
        {
            Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> cache =
                new Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache>(null, null, null, null, false, false);
            cache
                .Setup(c => c.TryGetPartitionKeyRangeByIdAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(), true))
                .ThrowsAsync(new InvalidOperationException("routing refresh failed"));

            (Mock<DistributedTransactionResponse> response, DistributedTransactionServerRequest serverRequest) =
                await BuildRefreshScenarioAsync(("0", "1", HttpStatusCode.Created));

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => DistributedTransactionCommitter.MergeSessionTokensAsync(
                    response.Object, serverRequest, new Mock<ISessionContainer>().Object,
                    isSessionConsistency: true, partitionKeyRangeCache: cache.Object, trace: NoOpTrace.Singleton),
                "A capture-side refresh failure must propagate, not be swallowed.");
        }

        // ─── Single-master write-gate (Phase 4, §7.1) ───────────────────────────

        private static DistributedTransactionSessionTokenResolver CreateResolverWithMultiMaster(
            SessionContainer sessionContainer,
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap,
            bool canUseMultipleWriteLocations)
        {
            Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> cache =
                new Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache>(null, null, null, null, false, false);
            cache
                .Setup(m => m.TryLookupAsync(
                    It.IsAny<string>(),
                    It.IsAny<Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap>(),
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<ITrace>()))
                .Returns(Task.FromResult(routingMap));
            return new DistributedTransactionSessionTokenResolver(sessionContainer, cache.Object, canUseMultipleWriteLocations);
        }

        private static (ContainerProperties containerProperties, string collectionPath) BuildResolverContainerContext()
        {
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId(CollectionResourceId);
            containerProperties.Id = "TestContainerId";
            containerProperties.PartitionKeyPath = "/pk";
            string collectionPath = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);
            return (containerProperties, collectionPath);
        }

        [TestMethod]
        [Description("Write-gate parity: a write sub-op on a single-master account is NOT assigned an auto-resolved session token (matching the point-op gateway gate), though the resolved range is still recorded for split detection.")]
        public async Task WriteGate_SingleMaster_WriteSubOp_NoToken()
        {
            SessionContainer sessionContainer = SeedSessionContainer("0:1#100#4=90#5=2");
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap = BuildCompleteRoutingMap(("0", string.Empty, "FF", null));
            DistributedTransactionSessionTokenResolver resolver =
                CreateResolverWithMultiMaster(sessionContainer, routingMap, canUseMultipleWriteLocations: false);
            (ContainerProperties containerProperties, string collectionPath) = BuildResolverContainerContext();

            DistributedTransactionOperation op = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");

            await resolver.ApplyTokensAsync(new[] { op }, collectionPath, containerProperties);

            Assert.IsTrue(string.IsNullOrEmpty(op.SessionToken),
                "A single-master write sub-op must not receive an auto-resolved session token.");
            Assert.AreEqual("0", op.ResolvedPartitionKeyRangeId,
                "The resolved range must still be recorded so split detection covers gated writes.");
        }

        [TestMethod]
        [Description("Write-gate parity: a read sub-op on a single-master account still receives the resolved token.")]
        public async Task WriteGate_SingleMaster_ReadSubOp_AppliesToken()
        {
            const string token = "0:1#100#4=90#5=2";
            SessionContainer sessionContainer = SeedSessionContainer(token);
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap = BuildCompleteRoutingMap(("0", string.Empty, "FF", null));
            DistributedTransactionSessionTokenResolver resolver =
                CreateResolverWithMultiMaster(sessionContainer, routingMap, canUseMultipleWriteLocations: false);
            (ContainerProperties containerProperties, string collectionPath) = BuildResolverContainerContext();

            DistributedTransactionOperation op = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");

            await resolver.ApplyTokensAsync(new[] { op }, collectionPath, containerProperties);

            Assert.AreEqual(token, op.SessionToken,
                "A read sub-op is never gated and must receive the resolved token even on a single-master account.");
        }

        [TestMethod]
        [Description("Write-gate parity: a write sub-op on a multi-master account still receives the resolved token.")]
        public async Task WriteGate_MultiMaster_WriteSubOp_AppliesToken()
        {
            const string token = "0:1#100#4=90#5=2";
            SessionContainer sessionContainer = SeedSessionContainer(token);
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap = BuildCompleteRoutingMap(("0", string.Empty, "FF", null));
            DistributedTransactionSessionTokenResolver resolver =
                CreateResolverWithMultiMaster(sessionContainer, routingMap, canUseMultipleWriteLocations: true);
            (ContainerProperties containerProperties, string collectionPath) = BuildResolverContainerContext();

            DistributedTransactionOperation op = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");

            await resolver.ApplyTokensAsync(new[] { op }, collectionPath, containerProperties);

            Assert.AreEqual(token, op.SessionToken,
                "A multi-master write sub-op must receive the resolved token (parity with the point-op gate).");
        }

        [TestMethod]
        [Description("Write-gate never clears a caller-supplied token: a user token on a single-master write is preserved.")]
        public async Task WriteGate_UserSuppliedToken_AlwaysPreserved()
        {
            const string userToken = "3:1#7";
            SessionContainer sessionContainer = SeedSessionContainer("0:1#100#4=90#5=2");
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap = BuildCompleteRoutingMap(("0", string.Empty, "FF", null));
            DistributedTransactionSessionTokenResolver resolver =
                CreateResolverWithMultiMaster(sessionContainer, routingMap, canUseMultipleWriteLocations: false);
            (ContainerProperties containerProperties, string collectionPath) = BuildResolverContainerContext();

            DistributedTransactionOperation op = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");
            op.SessionToken = userToken;

            await resolver.ApplyTokensAsync(new[] { op }, collectionPath, containerProperties);

            Assert.AreEqual(userToken, op.SessionToken,
                "A caller-supplied session token must always be honored, regardless of the write-gate.");
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
