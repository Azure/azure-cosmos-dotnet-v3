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
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using IClientSideRequestStatistics = Microsoft.Azure.Documents.IClientSideRequestStatistics;
    using ResourceId = Microsoft.Azure.Documents.ResourceId;
    using ServerStoreModel = Microsoft.Azure.Documents.ServerStoreModel;

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
            Assert.AreEqual(GsiARid, gsiA.Id);
            Assert.AreEqual(SourceRid, gsiA.SourceCollectionRid);
            Assert.AreEqual("/_id", gsiA.IncludedProperties["/id"]);
            Assert.AreEqual("/region", gsiA.IncludedProperties["/region"]);
            Assert.AreEqual(ConsistencyLevel.Eventual, gsiA.Consistency);
            Assert.AreEqual("/category", gsiB.IncludedProperties["/category"]);

            collections[GsiARid].PartitionKey.Paths[0] = "/changed";
            Assert.AreEqual(
                "/region",
                gsiA.PartitionKey.Paths[0].ToString(),
                "Normalized metadata must not retain mutable collection metadata references.");
            collections[GsiARid].IndexingPolicy.IncludedPaths[0].Path = "/changed";
            Assert.AreEqual(
                "/*",
                gsiA.IndexingPolicy.IncludedPaths[0].Path,
                "Normalized metadata must not retain mutable indexing policy references.");
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

        #region TryGetIncludedProperties Tests

        [DataTestMethod]
        [DataRow("SELECT c.id AS _id FROM c", "/id", "/_id")]
        [DataRow("SELECT c['category'] FROM c", "/category", "/category")]
        [DataRow("SELECT c.address.zip AS postalCode FROM c", "/address/zip", "/postalCode")]
        [DataRow("SELECT c['address']['zip'] AS postalCode FROM c", "/address/zip", "/postalCode")]
        [DataRow("SELECT c.address['zip'] FROM c", "/address/zip", "/zip")]
        [DataRow("SELECT item.id FROM ROOT item", "/id", "/id")]
        [DataRow("SELECT item.id FROM c AS item", "/id", "/id")]
        public void TryGetIncludedPropertiesMapsPropertyPaths(
            string query,
            string sourcePath,
            string projectedPath)
        {
            bool succeeded = CollectionMetadataSecondaryIndexMetadataProvider.TryGetIncludedProperties(
                CreateMaterializedViewDefinition(query),
                CreateSource(),
                out IReadOnlyDictionary<string, string> includedProperties);

            Assert.IsTrue(succeeded);
            Assert.AreEqual(1, includedProperties.Count);
            Assert.AreEqual(projectedPath, includedProperties[sourcePath]);
        }

        [TestMethod]
        public void TryGetIncludedPropertiesMapsMultiplePropertyPaths()
        {
            bool succeeded = CollectionMetadataSecondaryIndexMetadataProvider.TryGetIncludedProperties(
                CreateMaterializedViewDefinition(
                    "SELECT c.id AS _id, c.region, c.address.zip AS postalCode FROM c"),
                CreateSource(),
                out IReadOnlyDictionary<string, string> includedProperties);

            Assert.IsTrue(succeeded);
            Assert.AreEqual(3, includedProperties.Count);
            Assert.AreEqual("/_id", includedProperties["/id"]);
            Assert.AreEqual("/region", includedProperties["/region"]);
            Assert.AreEqual("/postalCode", includedProperties["/address/zip"]);
        }

        [TestMethod]
        public void TryGetIncludedPropertiesMapsSpecialCharacterProperties()
        {
            bool succeeded = CollectionMetadataSecondaryIndexMetadataProvider.TryGetIncludedProperties(
                CreateMaterializedViewDefinition(
                    "SELECT c[\"a/b\"], c[\"a~1b\"] FROM c"),
                CreateSource(),
                out IReadOnlyDictionary<string, string> includedProperties);

            Assert.IsTrue(succeeded);
            Assert.AreEqual(2, includedProperties.Count);
            Assert.AreEqual("/\"a/b\"", includedProperties["/\"a/b\""]);
            Assert.AreEqual("/\"a~1b\"", includedProperties["/\"a~1b\""]);
        }

        [TestMethod]
        public void TryGetIncludedPropertiesDistinguishesPropertyFromNestedPath()
        {
            bool succeeded = CollectionMetadataSecondaryIndexMetadataProvider.TryGetIncludedProperties(
                CreateMaterializedViewDefinition(
                    "SELECT c[\"a/b\"], c.a.b FROM c"),
                CreateSource(),
                out IReadOnlyDictionary<string, string> includedProperties);

            Assert.IsTrue(succeeded);
            Assert.AreEqual(2, includedProperties.Count);
            Assert.AreEqual("/\"a/b\"", includedProperties["/\"a/b\""]);
            Assert.AreEqual("/b", includedProperties["/a/b"]);
        }

        [TestMethod]
        public void TryGetIncludedPropertiesMapsWildcardAndPartitionKey()
        {
            bool succeeded = CollectionMetadataSecondaryIndexMetadataProvider.TryGetIncludedProperties(
                CreateMaterializedViewDefinition("SELECT * FROM c"),
                CreateSource(),
                out IReadOnlyDictionary<string, string> includedProperties);

            Assert.IsTrue(succeeded);
            Assert.AreEqual("/*", includedProperties["/*"]);
            Assert.AreEqual("/tenantId", includedProperties["/tenantId"]);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("not a query")]
        [DataRow("SELECT VALUE c.id FROM c")]
        [DataRow("SELECT c FROM c")]
        [DataRow("SELECT c.value + 1 AS value FROM c")]
        [DataRow("SELECT UPPER(c.name) AS name FROM c")]
        [DataRow("SELECT udf.normalize(c.name) AS name FROM c")]
        [DataRow("SELECT COUNT(1) AS count FROM c")]
        [DataRow("SELECT c.id, UPPER(c.name) AS name FROM c")]
        [DataRow("SELECT c.id FROM c JOIN child IN c.children")]
        [DataRow("SELECT child.id FROM c JOIN child IN c.children")]
        [DataRow("SELECT item.id FROM item IN c.items")]
        [DataRow("SELECT item.id FROM (SELECT * FROM c) item")]
        [DataRow("SELECT other.id FROM c")]
        public void TryGetIncludedPropertiesRejectsUnsupportedDefinitions(string query)
        {
            bool succeeded = CollectionMetadataSecondaryIndexMetadataProvider.TryGetIncludedProperties(
                CreateMaterializedViewDefinition(query),
                CreateSource(),
                out IReadOnlyDictionary<string, string> includedProperties);

            Assert.IsFalse(succeeded);
            Assert.IsNull(includedProperties);
        }

        [TestMethod]
        public void TryGetIncludedPropertiesRejectsMissingInputs()
        {
            Assert.IsFalse(CollectionMetadataSecondaryIndexMetadataProvider.TryGetIncludedProperties(
                definition: null,
                CreateSource(),
                out IReadOnlyDictionary<string, string> missingDefinitionProperties));
            Assert.IsNull(missingDefinitionProperties);

            Assert.IsFalse(CollectionMetadataSecondaryIndexMetadataProvider.TryGetIncludedProperties(
                CreateMaterializedViewDefinition("SELECT * FROM c"),
                source: null,
                out IReadOnlyDictionary<string, string> missingSourceProperties));
            Assert.IsNull(missingSourceProperties);
        }

        #endregion TryGetIncludedProperties Tests

        [DataTestMethod]
        [DataRow("SELECT * FROM c", false)]
        [DataRow("SELECT * FROM c WHERE c.enabled = true", true)]
        [DataRow(null, false)]
        [DataRow("not a query", false)]
        public void IsFilteredMaterializedViewIdentifiesWhereClause(string query, bool expected)
        {
            Assert.AreEqual(
                expected,
                CollectionMetadataSecondaryIndexMetadataProvider.IsFilteredMaterializedView(
                    CreateMaterializedViewDefinition(query)));
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
            candidate.MaterializedViewDefinition = new MaterializedViewDefinition
                {
                    SourceContainerId = "source",
                    SourceContainerResourceId = SourceRid,
                    Definition = query,
                    ContainerType = CollectionMetadataSecondaryIndexMetadataProvider.GlobalSecondaryIndexContainerType,
                };

            candidate.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });
            return candidate;
        }

        private static MaterializedViewDefinition CreateMaterializedViewDefinition(string query)
        {
            return new MaterializedViewDefinition
            {
                Definition = query,
            };
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
