//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CollectionCacheTests
    {
        // A real collection RID from the existing test suite (same value used in FeedRangeTests
        // and CosmosBadReplicaTests).  ResourceId.Parse accepts this value, so it exercises the
        // actual binary round-trip through the Direct-package parser.
        private const string CollectionRid = "ccZ1ANCszwk=";

        [TestMethod]
        public async Task ResolveCollectionAsync_WithDatabaseRidInResolvedCollectionRid_FallsBackToNameResolution()
        {
            // Derive a real database RID from the known collection RID so that
            // ResourceId.TryParse recognises it as a valid (but database-level) RID.
            string databaseRid = ResourceId.Parse(CollectionRid).DatabaseId.ToString();

            TestCollectionCache cache = new TestCollectionCache(CollectionRid);
            using DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                OperationType.Read,
                "dbs/db1/colls/c1/docs/d1",
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey,
                null);
            request.RequestContext.ResolvedCollectionRid = databaseRid;

            ContainerProperties resolved = await cache.ResolveCollectionAsync(
                request,
                CancellationToken.None,
                NoOpTrace.Singleton);

            Assert.AreEqual(CollectionRid, resolved.ResourceId);
            Assert.AreEqual(1, cache.NameLookupCount);
            Assert.AreEqual(0, cache.RidLookupCount);
        }

        [TestMethod]
        public async Task ResolveCollectionAsync_WithCollectionRidInResolvedCollectionRid_UsesRidResolution()
        {
            TestCollectionCache cache = new TestCollectionCache(CollectionRid);
            using DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                OperationType.Read,
                "dbs/db1/colls/c1/docs/d1",
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey,
                null);
            request.RequestContext.ResolvedCollectionRid = CollectionRid;

            ContainerProperties resolved = await cache.ResolveCollectionAsync(
                request,
                CancellationToken.None,
                NoOpTrace.Singleton);

            Assert.AreEqual(CollectionRid, resolved.ResourceId);
            Assert.AreEqual(0, cache.NameLookupCount);
            Assert.AreEqual(1, cache.RidLookupCount);
        }

        /// <summary>
        /// Assignment guard: when the name-based lookup returns a container whose ResourceId is
        /// a database-level RID (i.e. the server sent a corrupt response), CollectionCache must
        /// throw <see cref="InvalidOperationException"/> before persisting the bad value onto
        /// <c>request.RequestContext.ResolvedCollectionRid</c>.
        ///
        /// On the <c>msdata/direct</c> branch an additional first line of defence exists:
        /// the <see cref="DocumentServiceRequestContext.ResolvedCollectionRid"/> setter itself
        /// emits a <c>TraceWarning</c> (with the call stack) whenever a non-collection RID is
        /// assigned, so the exact call site can be identified in production logs even before the
        /// error surfaces in <see cref="CollectionCache"/>.
        /// </summary>
        [TestMethod]
        public async Task ResolveCollectionAsync_WhenNameResolutionReturnsDatabaseRid_ThrowsInvalidOperation()
        {
            // Use a real database RID derived from a known collection RID so that
            // ResourceId.TryParse recognises it as a database-level RID.
            string databaseRid = ResourceId.Parse(CollectionRid).DatabaseId.ToString();

            // The mock returns a container whose ResourceId is a database RID — simulating
            // a corrupt server response or a stale in-process cache entry.
            TestCollectionCache cache = new TestCollectionCache(returnRid: databaseRid);
            using DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                OperationType.Read,
                "dbs/db1/colls/c1/docs/d1",
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey,
                null);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => cache.ResolveCollectionAsync(
                    request,
                    CancellationToken.None,
                    NoOpTrace.Singleton));
        }

        private sealed class TestCollectionCache : CollectionCache
        {
            private readonly string returnRid;

            public TestCollectionCache(string returnRid)
                : base(enableAsyncCacheExceptionNoSharing: false)
            {
                this.returnRid = returnRid;
            }

            public int NameLookupCount { get; private set; }

            public int RidLookupCount { get; private set; }

            protected override Task<ContainerProperties> GetByRidAsync(
                string apiVersion,
                string collectionRid,
                ITrace trace,
                IClientSideRequestStatistics clientSideRequestStatistics,
                CancellationToken cancellationToken)
            {
                this.RidLookupCount++;
                if (StringComparer.Ordinal.Equals(collectionRid, this.returnRid))
                {
                    return Task.FromResult(ContainerProperties.CreateWithResourceId(this.returnRid));
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected collection rid: {collectionRid}");
                }
            }

            protected override Task<ContainerProperties> GetByNameAsync(
                string apiVersion,
                string resourceAddress,
                ITrace trace,
                IClientSideRequestStatistics clientSideRequestStatistics,
                CancellationToken cancellationToken)
            {
                this.NameLookupCount++;
                return Task.FromResult(ContainerProperties.CreateWithResourceId(this.returnRid));
            }
        }
    }
}
