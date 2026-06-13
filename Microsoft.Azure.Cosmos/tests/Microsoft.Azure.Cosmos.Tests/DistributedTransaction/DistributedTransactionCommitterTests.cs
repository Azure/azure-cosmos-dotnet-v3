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
                operations, mockContext.Object);

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
                operations, mockContext.Object);

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
                operations, mockContext.Object);

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
                operations, mockContext.Object);

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
                operations, mockContext.Object);

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
                operations, mockContext.Object);

            await Assert.ThrowsExceptionAsync<CosmosException>(
                () => committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None),
                $"An LSN-only session token with pkRangeId '{pkRangeId}' must throw CosmosException.");
        }

        // ─── Retry / Spec-Compliance Tests ─────────────────────────────────────

        [TestMethod]
        [Description("In a multi-op response, if op 0 has a malformed token and op 1 has a valid one, " +
                     "MergeSessionTokens merges the valid token FIRST, then throws CosmosException.")]
        public async Task CommitTransactionAsync_MultiOp_MergesValidTokens_BeforeThrowingOnMalformed()
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

            // op 0: malformed (LSN-only, not canonical) — will be skipped during merge
            // op 1: valid canonical token — will be merged before the throw
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
                operations, mockContext.Object);

            await Assert.ThrowsExceptionAsync<CosmosException>(
                () => committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None),
                "A malformed session token must still throw CosmosException after merging valid ops.");

            // The key assertion: op 1's valid token WAS merged before the throw
            mockSessionContainer.Verify(
                s => s.SetSessionToken(
                    collectionRid2,
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, container2),
                    It.Is<INameValueCollection>(h => h[HttpConstants.HttpHeaders.SessionToken] == canonicalToken)),
                Times.Once,
                "Op 1's valid token must be merged into the session container even when op 0 is malformed.");

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
        // When SetSessionToken throws on one op (e.g., SessionTokenHelper.Parse fails on content-invalid
        // token that passed the shape check), the exception is caught and aggregated. Other ops' valid
        // tokens are still merged. The aggregated error is surfaced as CosmosException.
        public async Task CommitTransactionAsync_AggregatesSetSessionTokenException()
        {
            const string canonicalToken = "0:1#9#4=8#5=7";

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
                operations, mockContext.Object);

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
                operations, mockContext.Object);

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
            DistributedTransactionCommitter.MergeSessionTokens(
                mockResponse.Object, serverRequest, mockSessionContainer.Object);

            // SetSessionToken must NOT have been called (skipped due to null CollectionResourceId)
            mockSessionContainer.Verify(
                s => s.SetSessionToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<INameValueCollection>()),
                Times.Never,
                "SetSessionToken must not be called when CollectionResourceId is null.");
        }

        [TestMethod]
        // When SetSessionToken throws on op 0 (e.g., SessionTokenHelper.Parse rejects a content-invalid
        // token that passed the colon-shape check), op 1's valid token must still be merged before the
        // aggregated CosmosException surfaces. Cancellation must still propagate as-is.
        public async Task MergeSessionTokens_SetSessionTokenThrows_StillMergesValidOps()
        {
            const string token0 = "0:bad-content";
            const string token1 = "1:1#9#4=8#5=7";
            const string container2 = "testcontainer2";
            string collectionRid1 = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
            string collectionRid2 = ResourceId.NewDocumentCollectionId(42, 200).DocumentCollectionId.ToString();

            Mock<ISessionContainer> mockSessionContainer = new Mock<ISessionContainer>();
            // Only op 0's call throws; op 1's call succeeds
            mockSessionContainer
                .Setup(s => s.SetSessionToken(collectionRid1, It.IsAny<string>(), It.IsAny<INameValueCollection>()))
                .Throws(new InvalidOperationException("simulated parse failure"));

            DistributedTransactionOperationResult result0 = new DistributedTransactionOperationResult();
            result0.Index = 0;
            result0.StatusCode = HttpStatusCode.Created;
            result0.SessionToken = token0;

            DistributedTransactionOperationResult result1 = new DistributedTransactionOperationResult();
            result1.Index = 1;
            result1.StatusCode = HttpStatusCode.Created;
            result1.SessionToken = token1;

            Mock<DistributedTransactionResponse> mockResponse = new Mock<DistributedTransactionResponse>();
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

            // Aggregated exception should surface; op 1's valid token should already be merged
            Assert.ThrowsException<CosmosException>(
                () => DistributedTransactionCommitter.MergeSessionTokens(
                    mockResponse.Object, serverRequest, mockSessionContainer.Object),
                "Aggregated CosmosException must surface after the loop completes.");

            mockSessionContainer.Verify(
                s => s.SetSessionToken(
                    collectionRid2,
                    DistributedTransactionConstants.GetCollectionFullName(DatabaseName, container2),
                    It.Is<INameValueCollection>(h => h[HttpConstants.HttpHeaders.SessionToken] == token1)),
                Times.Once,
                "Op 1's valid token must be merged even when op 0's SetSessionToken threw.");
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

            Assert.ThrowsException<OperationCanceledException>(
                () => DistributedTransactionCommitter.MergeSessionTokens(
                    mockResponse.Object, serverRequest, mockSessionContainer.Object),
                "OperationCanceledException must propagate, not be caught/aggregated.");
        }

        [TestMethod]
        // Defensive: a malformed server response with an out-of-range op Index must not crash
        // the merge loop or skip remaining ops. The bad index is recorded as malformed and
        // subsequent valid ops still get merged.
        public async Task MergeSessionTokens_OutOfRangeIndex_RecordedAsMalformed()
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

            CosmosException ex = Assert.ThrowsException<CosmosException>(
                () => DistributedTransactionCommitter.MergeSessionTokens(
                    mockResponse.Object, serverRequest, mockSessionContainer.Object),
                "Out-of-range Index must be reported, not throw raw ArgumentOutOfRangeException.");

            Assert.IsTrue(ex.Message.Contains("out-of-range"),
                $"Aggregated message should mention out-of-range index. Actual: {ex.Message}");

            // The valid in-range op was still merged
            mockSessionContainer.Verify(
                s => s.SetSessionToken(collectionRid, It.IsAny<string>(), It.IsAny<INameValueCollection>()),
                Times.Once,
                "Valid in-range op must still be merged after a bad-index op is skipped.");
        }

        // ─── v2 edge case coverage ───────────────────────────────────────────────

        [TestMethod]
        // v2 edge case #1: a token like "0:-1" that passes the colon-shape check but causes
        // SessionTokenHelper.Parse to throw internally must be classified as malformed (caught
        // by the per-op try/catch). Demonstrates that the merge-all-first invariant still holds
        // — other ops' tokens are merged before the aggregated exception surfaces.
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
            // Simulate SessionTokenHelper.Parse rejecting op 0's content (BadRequestException is what
            // the real parser would throw on a non-numeric LSN)
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

            CosmosException ex = Assert.ThrowsException<CosmosException>(
                () => DistributedTransactionCommitter.MergeSessionTokens(
                    mockResponse.Object, serverRequest, mockSessionContainer.Object));

            Assert.IsTrue(ex.Message.Contains(shapeValidContentInvalid) || ex.Message.Contains("Op 0"),
                $"Aggregated message should identify the bad op. Actual: {ex.Message}");

            // Critical: op 1's valid token must STILL have been merged
            mockSessionContainer.Verify(
                s => s.SetSessionToken(
                    collectionRid2,
                    It.IsAny<string>(),
                    It.Is<INameValueCollection>(h => h[HttpConstants.HttpHeaders.SessionToken] == canonicalToken)),
                Times.Once,
                "Op 1's valid token must be merged even when op 0 fails SessionTokenHelper.Parse.");
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

            CosmosException ex = Assert.ThrowsException<CosmosException>(
                () => DistributedTransactionCommitter.MergeSessionTokens(
                    mockResponse.Object, serverRequest, mockSessionContainer.Object));

            Assert.AreEqual(HttpStatusCode.InternalServerError, ex.StatusCode);
            Assert.IsTrue(ex.Message.Contains("committed successfully"),
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
                tasks[i] = Task.Run(() => DistributedTransactionCommitter.MergeSessionTokens(
                    resp.Object, serverRequest, sessionContainer));
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
                operations, mockContext.Object);

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

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

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

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

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
                    CreateTestOperations(),
                    mockContext.Object,
                    TimeSpan.FromMilliseconds(1));

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

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

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

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

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

                DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

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
                    CreateTestOperations(),
                    mockContext.Object,
                    TimeSpan.FromMilliseconds(500));

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

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

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

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

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

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

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

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

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

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(operations, mockContext.Object);
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

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object);
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

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(operations, mockContext.Object);
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

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(operations, mockContext.Object);
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
                CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

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
                CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

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
                CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

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
                CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

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
                operations, mockContext.Object);

            await committer.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            string storedToken = sessionContainer.GetSessionToken(DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName));
            Assert.AreEqual(canonicalToken, storedToken,
                "Session token from a DTX read response must be merged into SessionContainer.");
        }

        [TestMethod]
        [Description("DTX write followed by DTX read: write response session tokens are merged and auto-resolved into subsequent read request")]
        public async Task CommitTransactionAsync_WriteTokensMerged_ThenAutoResolvedIntoSubsequentRead()
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
                writeOps, mockContext.Object);

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
                readOps, readMockContext.Object);

            await readCommitter.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(capturedReadBody);
            string bodyJson = Encoding.UTF8.GetString(capturedReadBody);
            Assert.IsTrue(bodyJson.Contains($"\"sessionToken\":\"{writeToken}\""),
                $"Auto-resolved session token from SessionContainer must appear in read request body. Body: {bodyJson}");
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
                writeOps, mockContext.Object);

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
                readOps, readMockContext.Object);

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
                writeOps, writeMockContext.Object);

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
                readOps, readMockContext.Object);

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
                operations, mockContext.Object);

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
                operations, mockContext.Object, retryBaseDelay: TimeSpan.Zero);

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
                operations, mockContext.Object);

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
                writeOps, mockContext.Object);

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
                readOps, readMockContext.Object);

            await readCommitter.CommitTransactionAsync(NoOpTrace.Singleton, CancellationToken.None);

            Assert.IsNotNull(capturedBody);
            string bodyJson = Encoding.UTF8.GetString(capturedBody);
            Assert.IsTrue(bodyJson.Contains($"\"sessionToken\":\"{extractedToken}\""),
                $"User-provided session token (extracted from prior response) must appear in request body. Body: {bodyJson}");
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
