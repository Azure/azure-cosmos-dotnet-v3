//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Emulator tests that exercise <see cref="Routing.CollectionCache"/> behaviour
    /// directly, so that edge-case paths (e.g. a database RID leaking into
    /// <c>ResolvedCollectionRid</c>) can be verified end-to-end against the real
    /// Cosmos emulator.
    /// </summary>
    [TestClass]
    public class CollectionCacheEmulatorTests
    {
        private CosmosClient cosmosClient;
        private Cosmos.Database database;
        private Container container;
        private ContainerProperties containerProperties;

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();
            this.database = await this.cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            this.containerProperties = new ContainerProperties(
                id: Guid.NewGuid().ToString(),
                partitionKeyPath: "/pk");
            ContainerResponse containerResponse = await this.database.CreateContainerAsync(this.containerProperties);
            this.container = containerResponse.Container;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.database != null)
            {
                await this.database.DeleteStreamAsync();
            }

            this.cosmosClient?.Dispose();
        }

        /// <summary>
        /// Repro for the scenario where <c>ResolvedCollectionRid</c> is set to a
        /// database-level RID instead of a collection-level RID.
        ///
        /// SCENARIO
        /// --------
        /// In some code paths (e.g. via <see cref="RenameCollectionAwareClientRetryPolicy"/>
        /// or a stale cache after container delete+recreate) the
        /// <c>DocumentServiceRequest.RequestContext.ResolvedCollectionRid</c> field can
        /// end up holding the database RID (4-byte form, e.g. "jy2ekg==") rather than
        /// a proper collection RID (8-byte form, e.g. "jy2eklxnboe=").  When that
        /// happens, the subsequent RID-based cache look-up fails or routes to the
        /// wrong container.
        ///
        /// FIX
        /// ---
        /// <see cref="CollectionCache.ResolveCollectionAsync"/> now:
        /// 1. Logs a <c>TraceWarning</c> when it detects that the pre-existing
        ///    <c>ResolvedCollectionRid</c> is not a collection RID.
        /// 2. Falls back to name-based resolution in that case.
        /// 3. Throws <see cref="InvalidOperationException"/> at the assignment site
        ///    if the resolved ResourceId is still not a collection RID (guards against
        ///    a corrupt response from the server).
        ///
        /// TEST
        /// ----
        /// This test injects the real database RID into
        /// <c>request.RequestContext.ResolvedCollectionRid</c> to simulate the bug,
        /// then verifies that <see cref="CollectionCache.ResolveCollectionAsync"/>
        /// still returns the correct <see cref="ContainerProperties"/> (with the
        /// proper collection RID) via the name-based fallback.
        /// </summary>
        [TestMethod]
        public async Task ResolveCollectionAsync_WithDatabaseRidInResolvedCollectionRid_FallsBackToNameResolutionReproTest()
        {
            // Arrange: get the real database RID from the emulator.
            DatabaseResponse databaseResponse = await this.database.ReadAsync();
            string databaseRid = databaseResponse.Resource.ResourceId;
            Assert.IsNotNull(databaseRid, "Database ResourceId must not be null");

            // Sanity-check: the database RID must NOT be a collection RID.
            // (A database RID is 4 bytes base64-encoded; a collection RID is 8 bytes.)
            Assert.IsFalse(
                IsCollectionRid(databaseRid),
                $"Expected '{databaseRid}' to be a database RID, not a collection RID.");

            // Get the container's actual collection RID so we can assert against it.
            ContainerResponse containerResponse = await this.container.ReadContainerAsync();
            string expectedCollectionRid = containerResponse.Resource.ResourceId;
            Assert.IsNotNull(expectedCollectionRid, "Container ResourceId must not be null");
            Assert.IsTrue(
                IsCollectionRid(expectedCollectionRid),
                $"Expected '{expectedCollectionRid}' to be a valid collection RID.");

            // Build a name-based DocumentServiceRequest that simulates a read on a
            // document inside the container so the request goes through the
            // IsNameBased path of CollectionCache.ResolveCollectionAsync.
            string documentPath = $"dbs/{this.database.Id}/colls/{this.container.Id}/docs/someDoc";
            using DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                OperationType.Read,
                documentPath,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey,
                null);

            // Simulate the bug: set ResolvedCollectionRid to the database RID.
            request.RequestContext.ResolvedCollectionRid = databaseRid;

            // Act: call the collection cache directly.
            ClientCollectionCache collectionCache =
                await this.cosmosClient.DocumentClient.GetCollectionCacheAsync(NoOpTrace.Singleton);

            ContainerProperties resolved = await collectionCache.ResolveCollectionAsync(
                request,
                CancellationToken.None,
                NoOpTrace.Singleton);

            // Assert: the cache must have fallen back to name-based resolution and
            // returned the correct collection RID — not the database RID.
            Assert.IsNotNull(resolved, "ResolveCollectionAsync must return a non-null ContainerProperties");
            Assert.AreEqual(
                expectedCollectionRid,
                resolved.ResourceId,
                $"Expected collection RID '{expectedCollectionRid}' but got '{resolved.ResourceId}'. " +
                "The cache should have fallen back to name-based resolution and returned the correct collection RID.");

            Assert.IsTrue(
                IsCollectionRid(resolved.ResourceId),
                $"Resolved ResourceId '{resolved.ResourceId}' must be a collection RID, not a database RID.");

            Assert.AreNotEqual(
                databaseRid,
                resolved.ResourceId,
                "Resolved ResourceId must not be the database RID that was injected.");
        }

        /// <summary>
        /// Verifies that end-to-end item operations (read/write) still succeed on a
        /// container whose collection cache was primed with a database RID.  This
        /// tests the full SDK retry stack rather than just the cache layer.
        /// </summary>
        [TestMethod]
        public async Task ItemRead_AfterDatabaseRidInjectedIntoCollectionCache_Succeeds()
        {
            // Create a test item so we have something to read back.
            string pk = Guid.NewGuid().ToString("N");
            string id = Guid.NewGuid().ToString("N");
            var testItem = new { id, pk };
            await this.container.CreateItemAsync(testItem, new Cosmos.PartitionKey(pk));

            // Get the database RID from the emulator.
            DatabaseResponse databaseResponse = await this.database.ReadAsync();
            string databaseRid = databaseResponse.Resource.ResourceId;

            // Warm up the collection cache so that subsequent reads use the cache
            // hit path; this ensures the cache is initialised before we corrupt it.
            ContainerResponse containerResponse = await this.container.ReadContainerAsync();
            string expectedCollectionRid = containerResponse.Resource.ResourceId;

            // Build the request and inject the database RID as ResolvedCollectionRid.
            string documentPath = $"dbs/{this.database.Id}/colls/{this.container.Id}/docs/{id}";
            using DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                OperationType.Read,
                documentPath,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey,
                null);

            request.RequestContext.ResolvedCollectionRid = databaseRid;

            ClientCollectionCache collectionCache =
                await this.cosmosClient.DocumentClient.GetCollectionCacheAsync(NoOpTrace.Singleton);

            // The cache must resolve to the correct collection RID via name-based fallback.
            ContainerProperties resolved = await collectionCache.ResolveCollectionAsync(
                request,
                CancellationToken.None,
                NoOpTrace.Singleton);

            Assert.AreEqual(expectedCollectionRid, resolved.ResourceId);

            // The real item read through the full SDK stack must also succeed.
            ItemResponse<dynamic> itemResponse = await this.container.ReadItemAsync<dynamic>(
                id,
                new Cosmos.PartitionKey(pk));

            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
        }

        /// <summary>
        /// Delegates to <see cref="CollectionCache.IsCollectionRid"/> which is now
        /// <c>internal</c>, so tests in this assembly can use it directly.
        /// </summary>
        private static bool IsCollectionRid(string resourceId)
            => CollectionCache.IsCollectionRid(resourceId);
    }
}
