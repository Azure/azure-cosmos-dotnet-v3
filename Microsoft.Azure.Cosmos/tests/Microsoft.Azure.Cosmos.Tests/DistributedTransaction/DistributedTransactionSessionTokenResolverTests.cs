// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.DistributedTransaction
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

    /// <summary>
    /// Unit tests for <see cref="DistributedTransactionSessionTokenResolver"/> — the distributed-transaction
    /// auto per-partition session-token resolution (TryCreateAsync factory, ApplyTokensAsync resolution,
    /// single-master write-gate parity, and the K2 fail-instead-of-drop policy on metadata failure).
    /// </summary>
    [TestClass]
    public class DistributedTransactionSessionTokenResolverTests
    {
        private const string DatabaseName = "testdb";
        private const string ContainerName = "testcontainer";

        private static readonly string CollectionResourceId =
            ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();

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
        [Description("Parity with the point-op capture path: an operation carrying an explicit user token still gets its partition range resolved and recorded (ResolvedPartitionKeyRangeId), so the post-commit capture pass can detect a split/partition move for user-token ops too — the token itself is still not overridden.")]
        public async Task ApplyTokens_ExplicitUserToken_StillRecordsResolvedRangeForSplitDetection()
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
                "The explicit user-supplied token must still not be overridden.");
            Assert.AreEqual("0", op.ResolvedPartitionKeyRangeId,
                "The resolved range must be recorded even for a user-token op, so the capture pass can detect a partition move for it (parity with the point-op path).");
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

        [TestMethod]
        [Description("Shared-core delegation: ResolvePartitionLocalToken delegates the component-count guard, effective-key computation and range lookup to AddressResolver.TryResolvePartitionKeyToRange. A Resolved key applies the partition-local token AND records ResolvedPartitionKeyRangeId (for split detection); an unroutable key (KeyMismatch) applies no token AND records no range id — neither path throws.")]
        public async Task ApplyTokens_DelegatesToSharedCore_ResolvedRecordsRangeId_MismatchRecordsNothing()
        {
            const string rangeToken = "0:1#100#4=90#5=2";
            SessionContainer sessionContainer = SeedSessionContainer(rangeToken);
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap = BuildCompleteRoutingMap(("0", string.Empty, "FF", null));
            (DistributedTransactionSessionTokenResolver resolver, _, string collectionPath) =
                await this.CreateResolverAsync(sessionContainer, routingMap);

            // Two-path hierarchical (sub-partitioned) definition so a partial prefix key hits the shared
            // core's KeyMismatch branch, while a full key hits Resolved.
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId(CollectionResourceId);
            containerProperties.Id = "TestContainerId";
            containerProperties.PartitionKey = new PartitionKeyDefinition
            {
                Kind = PartitionKind.MultiHash,
                Paths = new System.Collections.ObjectModel.Collection<string> { "/tenant", "/user" },
                Version = Microsoft.Azure.Documents.PartitionKeyDefinitionVersion.V2
            };

            // Resolved: a full two-component key maps to the single range "0".
            DistributedTransactionOperation resolvedOp = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 0, DatabaseName, ContainerName,
                new PartitionKeyBuilder().Add("tenant1").Add("user1").Build(), id: "resolved");

            // KeyMismatch: a one-component prefix spans multiple ranges → unroutable to a single range.
            DistributedTransactionOperation mismatchOp = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 1, DatabaseName, ContainerName,
                new PartitionKeyBuilder().Add("tenant1").Build(), id: "mismatch");

            await resolver.ApplyTokensAsync(new[] { resolvedOp, mismatchOp }, collectionPath, containerProperties);

            Assert.AreEqual(rangeToken, resolvedOp.SessionToken,
                "A Resolved key must receive its partition-local token through the shared-core delegation.");
            Assert.AreEqual("0", resolvedOp.ResolvedPartitionKeyRangeId,
                "A Resolved key must record its range id so the capture pass can detect a split/move.");

            Assert.IsNull(mismatchOp.SessionToken,
                "A KeyMismatch key must receive no token (degrade-to-eventual), never a wrong-partition token.");
            Assert.IsNull(mismatchOp.ResolvedPartitionKeyRangeId,
                "A KeyMismatch key resolves no range, so no range id is recorded; the delegation must not throw.");
        }

        // ─── K2: fail-instead-of-drop on metadata failure with cached progress ───

        [TestMethod]
        [Description("Reviewer feedback K2: when the routing-map lookup FAILS but the SessionContainer already holds causal progress for the collection, the resolver must FAIL (surface the error) rather than silently degrade the operation to a tokenless Session request.")]
        public async Task ApplyTokens_RoutingLookupThrows_WithCachedProgress_Throws()
        {
            SessionContainer sessionContainer = SeedSessionContainer("0:1#100#4=90#5=2");
            InvalidOperationException lookupFailure = new InvalidOperationException("transient metadata failure");
            DistributedTransactionSessionTokenResolver resolver =
                CreateResolverWithThrowingLookup(sessionContainer, lookupFailure);
            (ContainerProperties containerProperties, string collectionPath) = BuildResolverContainerContext();

            DistributedTransactionOperation op = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");

            InvalidOperationException thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => resolver.ApplyTokensAsync(new[] { op }, collectionPath, containerProperties),
                "A routing-map lookup failure with cached progress must fail rather than drop the token.");
            Assert.AreSame(lookupFailure, thrown, "The original lookup failure must be surfaced, not swallowed.");
            Assert.IsNull(op.SessionToken, "No tokenless Session request should have been prepared for the operation.");
        }

        [TestMethod]
        [Description("Reviewer feedback K2: when the routing-map lookup fails and there is NO cached progress to lose, the resolver degrades to best-effort no-token (does not throw) — parity with the original best-effort behavior when nothing is at stake.")]
        public async Task ApplyTokens_RoutingLookupThrows_NoCachedProgress_DegradesToNoToken()
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");
            DistributedTransactionSessionTokenResolver resolver =
                CreateResolverWithThrowingLookup(sessionContainer, new InvalidOperationException("transient metadata failure"));
            (ContainerProperties containerProperties, string collectionPath) = BuildResolverContainerContext();

            DistributedTransactionOperation op = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");

            await resolver.ApplyTokensAsync(new[] { op }, collectionPath, containerProperties);

            Assert.IsNull(op.SessionToken, "With no cached progress, the operation degrades to no token without throwing.");
        }

        [TestMethod]
        [Description("Reviewer feedback K2: when the routing-map lookup returns null (cache miss) but the SessionContainer already holds causal progress, the resolver must FAIL rather than silently drop the token.")]
        public async Task ApplyTokens_RoutingMapNull_WithCachedProgress_Throws()
        {
            SessionContainer sessionContainer = SeedSessionContainer("0:1#100#4=90#5=2");
            DistributedTransactionSessionTokenResolver resolver =
                CreateResolverWithNullLookup(sessionContainer);
            (ContainerProperties containerProperties, string collectionPath) = BuildResolverContainerContext();

            DistributedTransactionOperation op = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => resolver.ApplyTokensAsync(new[] { op }, collectionPath, containerProperties),
                "A null routing map with cached progress must fail rather than drop the token.");
            Assert.IsNull(op.SessionToken, "No tokenless Session request should have been prepared for the operation.");
        }

        [TestMethod]
        [Description("Reviewer feedback K2: when the routing-map lookup returns null and there is NO cached progress, the resolver degrades to best-effort no-token (does not throw).")]
        public async Task ApplyTokens_RoutingMapNull_NoCachedProgress_DegradesToNoToken()
        {
            SessionContainer sessionContainer = new SessionContainer("testhost");
            DistributedTransactionSessionTokenResolver resolver =
                CreateResolverWithNullLookup(sessionContainer);
            (ContainerProperties containerProperties, string collectionPath) = BuildResolverContainerContext();

            DistributedTransactionOperation op = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "doc1");

            await resolver.ApplyTokensAsync(new[] { op }, collectionPath, containerProperties);

            Assert.IsNull(op.SessionToken, "With no cached progress, the operation degrades to no token without throwing.");
        }

        // ─── TryCreateAsync factory tests ───────────────────────────────────────

        [TestMethod]
        [Description("TryCreateAsync returns null under non-Session consistency — auto token resolution only applies to Session.")]
        public async Task TryCreateAsync_NonSessionConsistency_ReturnsNull()
        {
            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                new SessionContainer("testhost"), responseContent: null, statusCode: HttpStatusCode.OK,
                routingMap: BuildCompleteRoutingMap(("0", string.Empty, "FF", null)));

            DistributedTransactionSessionTokenResolver resolver =
                await DistributedTransactionSessionTokenResolver.TryCreateAsync(mockContext.Object, isSessionConsistency: false);

            Assert.IsNull(resolver, "Non-Session consistency must disable auto session-token resolution.");
        }

        [TestMethod]
        [Description("TryCreateAsync returns null when the client uses a custom ISessionContainer — auto resolution needs the built-in SessionContainer.")]
        public async Task TryCreateAsync_CustomSessionContainer_ReturnsNull()
        {
            Mock<ISessionContainer> customContainer = new Mock<ISessionContainer>();
            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                customContainer.Object, responseContent: null, statusCode: HttpStatusCode.OK,
                routingMap: BuildCompleteRoutingMap(("0", string.Empty, "FF", null)));

            DistributedTransactionSessionTokenResolver resolver =
                await DistributedTransactionSessionTokenResolver.TryCreateAsync(mockContext.Object, isSessionConsistency: true);

            Assert.IsNull(resolver, "A custom ISessionContainer (not the built-in SessionContainer) must disable auto resolution.");
        }

        [TestMethod]
        [Description("TryCreateAsync returns null when the PartitionKeyRangeCache is unavailable — the commit then applies no auto-resolved token.")]
        public async Task TryCreateAsync_NullPartitionKeyRangeCache_ReturnsNull()
        {
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(c => c.DocumentClient).Returns(new NullCacheMockDocumentClient
            {
                sessionContainer = new SessionContainer("testhost")
            });

            DistributedTransactionSessionTokenResolver resolver =
                await DistributedTransactionSessionTokenResolver.TryCreateAsync(mockContext.Object, isSessionConsistency: true);

            Assert.IsNull(resolver, "A null PartitionKeyRangeCache must disable auto resolution for the commit.");
        }

        [TestMethod]
        [Description("TryCreateAsync returns a resolver under Session consistency with the built-in SessionContainer and an available routing cache.")]
        public async Task TryCreateAsync_SessionWithBuiltInContainer_ReturnsResolver()
        {
            Mock<CosmosClientContext> mockContext = this.CreateMockContext(
                new SessionContainer("testhost"), responseContent: null, statusCode: HttpStatusCode.OK,
                routingMap: BuildCompleteRoutingMap(("0", string.Empty, "FF", null)));

            DistributedTransactionSessionTokenResolver resolver =
                await DistributedTransactionSessionTokenResolver.TryCreateAsync(mockContext.Object, isSessionConsistency: true);

            Assert.IsNotNull(resolver, "Session consistency with the built-in SessionContainer and an available cache must yield a resolver.");
        }

        // ─── Single-master write-gate ───────────────────────────────────────────

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

        [TestMethod]
        [Description("Write-gate derivation via TryCreateAsync: building the resolver through the real factory (not the internal constructor) exercises the throwaway Document/Create probe and GlobalEndpointManager.CanUseMultipleWriteLocations(request) gate. On a single-master account the derivation must gate a write sub-op (no token) while still tokening a read sub-op — proving the probe path actually runs and gates, which the constructor-injection write-gate tests bypass.")]
        public async Task WriteGate_DerivedFromTryCreate_SingleMaster_GatesWriteButNotRead()
        {
            const string token = "0:1#100#4=90#5=2";
            SessionContainer sessionContainer = SeedSessionContainer(token);
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap = BuildCompleteRoutingMap(("0", string.Empty, "FF", null));

            // CreateResolverAsync builds the resolver through TryCreateAsync, which derives multi-master
            // capability from the mock's real GlobalEndpointManager (default single-master ConnectionPolicy) —
            // the exact probe the internal-constructor write-gate tests inject past.
            (DistributedTransactionSessionTokenResolver resolver, ContainerProperties containerProperties, string collectionPath) =
                await this.CreateResolverAsync(sessionContainer, routingMap);

            DistributedTransactionOperation writeOp = new DistributedTransactionOperation(
                OperationType.Create, operationIndex: 0, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "write");
            DistributedTransactionOperation readOp = new DistributedTransactionOperation(
                OperationType.Read, operationIndex: 1, DatabaseName, ContainerName, new PartitionKey("pk1"), id: "read");

            await resolver.ApplyTokensAsync(new[] { writeOp, readOp }, collectionPath, containerProperties);

            Assert.IsTrue(string.IsNullOrEmpty(writeOp.SessionToken),
                "The real TryCreateAsync derivation must gate a single-master write sub-op (no auto-resolved token).");
            Assert.AreEqual("0", writeOp.ResolvedPartitionKeyRangeId,
                "The resolved range must still be recorded for a gated write so split detection covers it.");
            Assert.AreEqual(token, readOp.SessionToken,
                "A read sub-op is never gated and must receive the resolved token under the derived single-master gate.");
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

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

            (ContainerProperties containerProperties, string collectionPath) = BuildResolverContainerContext();
            return (resolver, containerProperties, collectionPath);
        }

        private Mock<CosmosClientContext> CreateMockContext(
            ISessionContainer sessionContainer,
            string responseContent,
            HttpStatusCode statusCode,
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap = null)
        {
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

        private static (ContainerProperties containerProperties, string collectionPath) BuildResolverContainerContext()
        {
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId(CollectionResourceId);
            containerProperties.Id = "TestContainerId";
            containerProperties.PartitionKeyPath = "/pk";
            string collectionPath = DistributedTransactionConstants.GetCollectionFullName(DatabaseName, ContainerName);
            return (containerProperties, collectionPath);
        }

        private static DistributedTransactionSessionTokenResolver CreateResolverWithMultiMaster(
            SessionContainer sessionContainer,
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap,
            bool canUseMultipleWriteLocations)
        {
            Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> cache =
                new Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache>(null, null, null, null, false, false, null);
            cache
                .Setup(m => m.TryLookupAsync(
                    It.IsAny<string>(),
                    It.IsAny<Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap>(),
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<ITrace>()))
                .Returns(Task.FromResult(routingMap));
            return new DistributedTransactionSessionTokenResolver(sessionContainer, cache.Object, canUseMultipleWriteLocations);
        }

        private static DistributedTransactionSessionTokenResolver CreateResolverWithThrowingLookup(
            SessionContainer sessionContainer,
            Exception lookupFailure)
        {
            Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> cache =
                new Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache>(null, null, null, null, false, false, null);
            cache
                .Setup(m => m.TryLookupAsync(
                    It.IsAny<string>(),
                    It.IsAny<Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap>(),
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<ITrace>()))
                .ThrowsAsync(lookupFailure);
            return new DistributedTransactionSessionTokenResolver(sessionContainer, cache.Object, canUseMultipleWriteLocations: false);
        }

        private static DistributedTransactionSessionTokenResolver CreateResolverWithNullLookup(
            SessionContainer sessionContainer)
        {
            Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> cache =
                new Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache>(null, null, null, null, false, false, null);
            cache
                .Setup(m => m.TryLookupAsync(
                    It.IsAny<string>(),
                    It.IsAny<Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap>(),
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<ITrace>()))
                .Returns(Task.FromResult<Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap>(null));
            return new DistributedTransactionSessionTokenResolver(sessionContainer, cache.Object, canUseMultipleWriteLocations: false);
        }

        private sealed class NullCacheMockDocumentClient : MockDocumentClient
        {
            internal override Task<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync(ITrace trace)
            {
                return Task.FromResult<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache>(null);
            }
        }

        private sealed class RoutingMapMockDocumentClient : MockDocumentClient
        {
            private readonly Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache routingCache;

            public RoutingMapMockDocumentClient(Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap routingMap)
            {
                Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache> cache =
                    new Mock<Microsoft.Azure.Cosmos.Routing.PartitionKeyRangeCache>(null, null, null, null, false, false, null);
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
    }
}
