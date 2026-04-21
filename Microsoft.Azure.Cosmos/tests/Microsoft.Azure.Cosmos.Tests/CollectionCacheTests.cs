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
        [TestMethod]
        public async Task ResolveCollectionAsync_WithDatabaseRidInResolvedCollectionRid_FallsBackToNameResolution()
        {
            const string databaseRid = "jy2ekg==";
            const string containerRid = "jy2eklxnboe=";

            TestCollectionCache cache = new TestCollectionCache(containerRid);
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

            Assert.AreEqual(containerRid, resolved.ResourceId);
            Assert.AreEqual(1, cache.NameLookupCount);
            Assert.AreEqual(0, cache.RidLookupCount);
        }

        [TestMethod]
        public async Task ResolveCollectionAsync_WithCollectionRidInResolvedCollectionRid_UsesRidResolution()
        {
            const string containerRid = "jy2eklxnboe=";

            TestCollectionCache cache = new TestCollectionCache(containerRid);
            using DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                OperationType.Read,
                "dbs/db1/colls/c1/docs/d1",
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey,
                null);
            request.RequestContext.ResolvedCollectionRid = containerRid;

            ContainerProperties resolved = await cache.ResolveCollectionAsync(
                request,
                CancellationToken.None,
                NoOpTrace.Singleton);

            Assert.AreEqual(containerRid, resolved.ResourceId);
            Assert.AreEqual(0, cache.NameLookupCount);
            Assert.AreEqual(1, cache.RidLookupCount);
        }

        private sealed class TestCollectionCache : CollectionCache
        {
            private readonly string containerRid;

            public TestCollectionCache(string containerRid)
                : base(enableAsyncCacheExceptionNoSharing: false)
            {
                this.containerRid = containerRid;
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
                if (StringComparer.Ordinal.Equals(collectionRid, this.containerRid))
                {
                    return Task.FromResult(ContainerProperties.CreateWithResourceId(this.containerRid));
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
                return Task.FromResult(ContainerProperties.CreateWithResourceId(this.containerRid));
            }
        }
    }
}
