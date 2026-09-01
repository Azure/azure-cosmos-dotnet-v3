//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CollectionMetadataSecondaryIndexMetadataProviderTests
    {
        private static readonly string SourceRid = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
        private static readonly string GsiARid = ResourceId.NewDocumentCollectionId(42, 130).DocumentCollectionId.ToString();
        private static readonly string GsiBRid = ResourceId.NewDocumentCollectionId(42, 131).DocumentCollectionId.ToString();
        private static readonly string OtherRid = ResourceId.NewDocumentCollectionId(42, 132).DocumentCollectionId.ToString();
        private static readonly string FilteredRid = ResourceId.NewDocumentCollectionId(42, 133).DocumentCollectionId.ToString();
        private static readonly string OtherTypeRid = ResourceId.NewDocumentCollectionId(42, 134).DocumentCollectionId.ToString();
        private static readonly string EligibleRid = ResourceId.NewDocumentCollectionId(42, 135).DocumentCollectionId.ToString();

        [TestMethod]
        public async Task ProviderNormalizesOrdersAndDeduplicatesMetadata()
        {
            ContainerProperties source = CreateSource();
            source.MaterializedViews = new List<MaterializedViewProperties>
            {
                new MaterializedViewProperties
                {
                    ResourceId = GsiBRid,
                    ContainerType = CollectionMetadataSecondaryIndexMetadataProvider.GlobalSecondaryIndexContainerType,
                },
                new MaterializedViewProperties
                {
                    ResourceId = GsiARid,
                    ContainerType = CollectionMetadataSecondaryIndexMetadataProvider.GlobalSecondaryIndexContainerType,
                },
                new MaterializedViewProperties
                {
                    ResourceId = GsiARid,
                    ContainerType = CollectionMetadataSecondaryIndexMetadataProvider.GlobalSecondaryIndexContainerType,
                },
            };

            Dictionary<string, ContainerProperties> collections = new Dictionary<string, ContainerProperties>
                {
                    [SourceRid] = source,
                    [GsiARid] = CreateMaterializedView(
                        GsiARid,
                        "SELECT c.id AS _id, c.region AS region FROM c",
                        "/region"),
                    [GsiBRid] = CreateMaterializedView(
                        GsiBRid,
                        "SELECT c['id'] AS _id, c['category'] AS category FROM c",
                        "/category"),
                };

            using ProviderTestContext context = new ProviderTestContext(collections);

            IReadOnlyList<ISecondaryIndexMetadata> metadata = await context.Provider.GetSecondaryIndexMetadataAsync(SourceRid, NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(3, context.ResolveCount);
            Assert.AreEqual(2, metadata.Count);
            CollectionAssert.AreEqual(
                new[] { GsiARid, GsiBRid }.OrderBy(rid => rid, StringComparer.Ordinal).ToArray(),
                metadata.Select(candidate => candidate.Rid).ToArray());
            ISecondaryIndexMetadata gsiA = metadata.Single(candidate => candidate.Rid == GsiARid);
            ISecondaryIndexMetadata gsiB = metadata.Single(candidate => candidate.Rid == GsiBRid);
            Assert.AreEqual(SourceRid, gsiA.SourceCollectionRid);
            Assert.AreEqual("/_id", gsiA.IncludedProperties["/id"]);
            Assert.AreEqual("/region", gsiA.IncludedProperties["/region"]);
            Assert.AreEqual(Microsoft.Azure.Cosmos.ConsistencyLevel.Eventual, gsiA.Consistency);
            Assert.AreEqual("/category", gsiB.IncludedProperties["/category"]);

            collections[GsiARid].PartitionKey.Paths[0] = "/changed";
            Assert.AreEqual(
                "/region",
                gsiA.PartitionKey.Paths[0].ToString(),
                "Normalized metadata must not retain mutable collection metadata references.");
        }

        [TestMethod]
        public async Task ProviderReturnsEmptyForSourceWithoutReferences()
        {
            Dictionary<string, ContainerProperties> collections = new Dictionary<string, ContainerProperties>
                {
                    [SourceRid] = CreateSource(),
                };
            using ProviderTestContext context = new ProviderTestContext(collections);

            IReadOnlyList<ISecondaryIndexMetadata> metadata = await context.Provider.GetSecondaryIndexMetadataAsync(SourceRid, NoOpTrace.Singleton);

            Assert.AreEqual(0, metadata.Count);
        }

        [TestMethod]
        public async Task ProviderSkipsCandidatesThatDoNotReferenceSource()
        {
            ContainerProperties source = CreateSource();
            source.MaterializedViews = new List<MaterializedViewProperties>
            {
                new MaterializedViewProperties { ResourceId = OtherRid },
            };
            ContainerProperties unrelated = CreateMaterializedView(
                OtherRid,
                "SELECT c.id, c.region FROM c",
                "/region");
            unrelated.MaterializedViewDefinition.SourceContainerResourceId = ResourceId.NewDocumentCollectionId(42, 200).DocumentCollectionId.ToString();
            unrelated.MaterializedViewDefinition.SourceContainerId = "differentSource";

            Dictionary<string, ContainerProperties> collections = new Dictionary<string, ContainerProperties>
                {
                    [SourceRid] = source,
                    [OtherRid] = unrelated,
                };
            using ProviderTestContext context = new ProviderTestContext(collections);

            IReadOnlyList<ISecondaryIndexMetadata> metadata = await context.Provider.GetSecondaryIndexMetadataAsync(SourceRid, NoOpTrace.Singleton);

            Assert.AreEqual(0, metadata.Count);
        }

        [TestMethod]
        public async Task ProviderRejectsFilteredAndNonGsiViews()
        {
            ContainerProperties source = CreateSource();
            source.MaterializedViews = new List<MaterializedViewProperties>
            {
                new MaterializedViewProperties
                {
                    ResourceId = FilteredRid,
                    ContainerType = CollectionMetadataSecondaryIndexMetadataProvider.GlobalSecondaryIndexContainerType,
                },
                new MaterializedViewProperties
                {
                    ResourceId = OtherTypeRid,
                    ContainerType = "MaterializedView",
                },
                new MaterializedViewProperties
                {
                    ResourceId = EligibleRid,
                    ContainerType = CollectionMetadataSecondaryIndexMetadataProvider.GlobalSecondaryIndexContainerType,
                },
            };

            ContainerProperties filtered = CreateMaterializedView(
                FilteredRid,
                "SELECT c.id, c.region FROM c WHERE c.enabled = true",
                "/region");
            ContainerProperties eligible = CreateMaterializedView(
                EligibleRid,
                "SELECT c.id, c.region FROM c",
                "/region");

            Dictionary<string, ContainerProperties> collections = new Dictionary<string, ContainerProperties>
                {
                    [SourceRid] = source,
                    [FilteredRid] = filtered,
                    [EligibleRid] = eligible,
                };
            using ProviderTestContext context = new ProviderTestContext(collections);

            IReadOnlyList<ISecondaryIndexMetadata> metadata = await context.Provider.GetSecondaryIndexMetadataAsync(SourceRid, NoOpTrace.Singleton);

            Assert.AreEqual(1, metadata.Count);
            Assert.AreEqual(EligibleRid, metadata[0].Rid);
        }

        private static ContainerProperties CreateSource()
        {
            ContainerProperties source = ContainerProperties.CreateWithResourceId(SourceRid);
            source.Id = "source";
            source.PartitionKey = new ContainerProperties("source", "/tenantId").PartitionKey;
            return source;
        }

        private static ContainerProperties CreateMaterializedView(
            string rid,
            string query,
            string partitionKeyPath)
        {
            ContainerProperties candidate = ContainerProperties.CreateWithResourceId(rid);
            candidate.Id = rid;
            candidate.PartitionKey = new ContainerProperties(rid, partitionKeyPath).PartitionKey;
            candidate.MaterializedViewDefinition = new Microsoft.Azure.Cosmos.MaterializedViewDefinition
                {
                    SourceContainerId = "source",
                    SourceContainerResourceId = SourceRid,
                    Definition = query,
                    ContainerType = CollectionMetadataSecondaryIndexMetadataProvider.GlobalSecondaryIndexContainerType,
                };

            candidate.IndexingPolicy.IncludedPaths.Add(new Microsoft.Azure.Cosmos.IncludedPath { Path = "/*" });
            return candidate;
        }

        private sealed class ProviderTestContext : IDisposable
        {
            private readonly TestClientCollectionCache collectionCache;
            private readonly TestDocumentClient documentClient;

            public ProviderTestContext(IReadOnlyDictionary<string, ContainerProperties> collections)
            {
                this.collectionCache = new TestClientCollectionCache(collections);
                this.documentClient = new TestDocumentClient(this.collectionCache);
                this.Provider = new CollectionMetadataSecondaryIndexMetadataProvider(this.documentClient);
            }

            public CollectionMetadataSecondaryIndexMetadataProvider Provider { get; }

            public int ResolveCount => this.collectionCache.ResolveCount;

            public void Dispose()
            {
                this.documentClient.Dispose();
            }
        }

        private sealed class TestClientCollectionCache : ClientCollectionCache
        {
            private readonly IReadOnlyDictionary<string, ContainerProperties> collections;

            public TestClientCollectionCache(IReadOnlyDictionary<string, ContainerProperties> collections)
                : base(new SessionContainer("testhost"), new ServerStoreModel(null), null, null, null, true, null)
            {
                this.collections = collections;
            }

            public int ResolveCount { get; private set; }

            protected override Task<ContainerProperties> GetByRidAsync(
                string apiVersion,
                string collectionRid,
                ITrace trace,
                IClientSideRequestStatistics clientSideRequestStatistics,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.ResolveCount++;
                return Task.FromResult(this.collections[collectionRid]);
            }
        }

        private sealed class TestDocumentClient : MockDocumentClient
        {
            private readonly ClientCollectionCache collectionCache;

            public TestDocumentClient(ClientCollectionCache collectionCache)
            {
                this.collectionCache = collectionCache;
            }

            internal override Task<ClientCollectionCache> GetCollectionCacheAsync(ITrace trace)
            {
                return Task.FromResult(this.collectionCache);
            }
        }
    }
}
