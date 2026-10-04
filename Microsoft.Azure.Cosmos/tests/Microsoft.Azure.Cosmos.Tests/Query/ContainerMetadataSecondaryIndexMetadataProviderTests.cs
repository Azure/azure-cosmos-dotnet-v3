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
    public class ContainerMetadataSecondaryIndexMetadataProviderTests
    {
        private static readonly string SourceRid = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
        private static readonly string GsiARid = ResourceId.NewDocumentCollectionId(42, 130).DocumentCollectionId.ToString();
        private static readonly string GsiBRid = ResourceId.NewDocumentCollectionId(42, 131).DocumentCollectionId.ToString();
        private static readonly string OtherRid = ResourceId.NewDocumentCollectionId(42, 132).DocumentCollectionId.ToString();
        private static readonly string FilteredRid = ResourceId.NewDocumentCollectionId(42, 133).DocumentCollectionId.ToString();
        private static readonly string OtherTypeRid = ResourceId.NewDocumentCollectionId(42, 134).DocumentCollectionId.ToString();
        private static readonly string EligibleRid = ResourceId.NewDocumentCollectionId(42, 135).DocumentCollectionId.ToString();

        [TestMethod]
        public async Task ProviderPreservesReferenceOrderAndDuplicates()
        {
            ContainerProperties source = CreateSource();
            source.MaterializedViews = new List<MaterializedViewProperties>
            {
                new MaterializedViewProperties
                {
                    ResourceId = GsiBRid,
                    ContainerType = ContainerMetadataSecondaryIndexMetadataProvider.GlobalSecondaryIndexContainerType,
                },
                new MaterializedViewProperties
                {
                    ResourceId = GsiARid,
                    ContainerType = ContainerMetadataSecondaryIndexMetadataProvider.GlobalSecondaryIndexContainerType,
                },
                new MaterializedViewProperties
                {
                    ResourceId = GsiARid,
                    ContainerType = ContainerMetadataSecondaryIndexMetadataProvider.GlobalSecondaryIndexContainerType,
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

            IEnumerable<ISecondaryIndexMetadata> metadata = await context.Provider.GetSecondaryIndexMetadataAsync(SourceRid, NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(3, context.ResolveCount);
            Assert.AreEqual(3, metadata.Count());
            CollectionAssert.AreEqual(
                new[] { GsiBRid, GsiARid, GsiARid },
                metadata.Select(candidate => candidate.Rid).ToArray());
            ISecondaryIndexMetadata gsiA = metadata.ElementAt(1);
            ISecondaryIndexMetadata gsiB = metadata.Single(candidate => candidate.Rid == GsiBRid);
            Assert.AreEqual(GsiARid, gsiA.Id);
            Assert.AreEqual(SourceRid, gsiA.SourceCollectionRid);
            Assert.AreEqual(new PropertyPath(new[] { "_id" }), gsiA.IncludedProperties[new PropertyPath(new[] { "id" })]);
            Assert.AreEqual(new PropertyPath(new[] { "region" }), gsiA.IncludedProperties[new PropertyPath(new[] { "region" })]);
            Assert.AreEqual(ConsistencyLevel.Eventual, gsiA.ConsistencyLevel);
            Assert.AreEqual(new PropertyPath(new[] { "category" }), gsiB.IncludedProperties[new PropertyPath(new[] { "category" })]);

            foreach (ISecondaryIndexMetadata candidate in metadata)
            {
                Assert.AreSame(collections[candidate.Rid].PartitionKey, candidate.PartitionKey);
                Assert.AreSame(collections[candidate.Rid].IndexingPolicy, candidate.IndexingPolicy);
            }
        }

        [TestMethod]
        public async Task ProviderReturnsEmptyForSourceWithoutReferences()
        {
            Dictionary<string, ContainerProperties> collections = new Dictionary<string, ContainerProperties>
                {
                    [SourceRid] = CreateSource(),
                };
            using ProviderTestContext context = new ProviderTestContext(collections);

            IEnumerable<ISecondaryIndexMetadata> metadata = await context.Provider.GetSecondaryIndexMetadataAsync(SourceRid, NoOpTrace.Singleton);

            Assert.AreEqual(0, metadata.Count());
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

            IEnumerable<ISecondaryIndexMetadata> metadata = await context.Provider.GetSecondaryIndexMetadataAsync(SourceRid, NoOpTrace.Singleton);

            Assert.AreEqual(0, metadata.Count());
        }

        [TestMethod]
        public async Task ProviderSkipsFilteredViewsWithoutFilteringContainerType()
        {
            ContainerProperties source = CreateSource();
            source.MaterializedViews = new List<MaterializedViewProperties>
            {
                new MaterializedViewProperties
                {
                    ResourceId = FilteredRid,
                    ContainerType = ContainerMetadataSecondaryIndexMetadataProvider.GlobalSecondaryIndexContainerType,
                },
                new MaterializedViewProperties
                {
                    ResourceId = OtherTypeRid,
                    ContainerType = "MaterializedView",
                },
                new MaterializedViewProperties
                {
                    ResourceId = EligibleRid,
                    ContainerType = ContainerMetadataSecondaryIndexMetadataProvider.GlobalSecondaryIndexContainerType,
                },
            };

            ContainerProperties filtered = CreateMaterializedView(
                FilteredRid,
                "SELECT c.id, c.region FROM c WHERE c.enabled = true",
                "/region");
            ContainerProperties otherType = CreateMaterializedView(
                OtherTypeRid,
                "SELECT c.id, c.region FROM c",
                "/region");
            otherType.MaterializedViewDefinition.ContainerType = "MaterializedView";
            ContainerProperties eligible = CreateMaterializedView(
                EligibleRid,
                "SELECT c.id, c.region FROM c",
                "/region");

            Dictionary<string, ContainerProperties> collections = new Dictionary<string, ContainerProperties>
                {
                    [SourceRid] = source,
                    [FilteredRid] = filtered,
                    [OtherTypeRid] = otherType,
                    [EligibleRid] = eligible,
                };
            using ProviderTestContext context = new ProviderTestContext(collections);

            IEnumerable<ISecondaryIndexMetadata> metadata = await context.Provider.GetSecondaryIndexMetadataAsync(SourceRid, NoOpTrace.Singleton);

            Assert.AreEqual(4, context.ResolveCount);
            Assert.AreEqual(2, metadata.Count());
            CollectionAssert.AreEqual(
                new[] { OtherTypeRid, EligibleRid },
                metadata.Select(candidate => candidate.Rid).ToArray());
        }

        #region TryGetIncludedProperties Tests

        [DataTestMethod]
        [DataRow("SELECT c.id AS _id FROM c", new[] { "id" }, "_id")]
        [DataRow("SELECT c['category'] FROM c", new[] { "category" }, "category")]
        [DataRow("SELECT c.address.zip AS postalCode FROM c", new[] { "address", "zip" }, "postalCode")]
        [DataRow("SELECT c['address']['zip'] AS postalCode FROM c", new[] { "address", "zip" }, "postalCode")]
        [DataRow("SELECT c.address['zip'] FROM c", new[] { "address", "zip" }, "zip")]
        [DataRow("SELECT item.id FROM ROOT item", new[] { "id" }, "id")]
        [DataRow("SELECT item.id FROM c AS item", new[] { "id" }, "id")]
        [DataRow("SELECT c[''] AS empty FROM c", new[] { "" }, "empty")]
        [DataRow("SELECT c[''].zip FROM c", new[] { "", "zip" }, "zip")]
        [DataRow("SELECT c['a.b'] FROM c", new[] { "a.b" }, "a.b")]
        [DataRow("SELECT c['a b'] FROM c", new[] { "a b" }, "a b")]
        [DataRow("SELECT c['a\"b'] FROM c", new[] { "a\"b" }, "a\"b")]
        public async Task TryGetIncludedPropertiesMapsPropertyPaths(
            string query,
            string[] sourceSegments,
            string projectedProperty)
        {
            ISecondaryIndexMetadata metadata = (await GetMetadataAsync(query)).Single();

            Assert.AreEqual(1, metadata.IncludedProperties.Count);
            Assert.AreEqual(
                new PropertyPath(new[] { projectedProperty }),
                metadata.IncludedProperties[new PropertyPath(sourceSegments)]);
        }

        [TestMethod]
        public async Task TryGetIncludedPropertiesMapsMultiplePropertyPaths()
        {
            ISecondaryIndexMetadata metadata = (await GetMetadataAsync(
                "SELECT c.id AS _id, c.region, c.address.zip AS postalCode FROM c")).Single();

            Assert.AreEqual(3, metadata.IncludedProperties.Count);
            Assert.AreEqual(new PropertyPath(new[] { "_id" }), metadata.IncludedProperties[new PropertyPath(new[] { "id" })]);
            Assert.AreEqual(new PropertyPath(new[] { "region" }), metadata.IncludedProperties[new PropertyPath(new[] { "region" })]);
            Assert.AreEqual(new PropertyPath(new[] { "postalCode" }), metadata.IncludedProperties[new PropertyPath(new[] { "address", "zip" })]);
        }

        [TestMethod]
        public async Task TryGetIncludedPropertiesUnifiesDotAndBracketPaths()
        {
            ISecondaryIndexMetadata metadata = (await GetMetadataAsync(
                "SELECT c.address.zip, c['address']['zip'] FROM c")).Single();

            Assert.AreEqual(1, metadata.IncludedProperties.Count);
            Assert.AreEqual(new PropertyPath(new[] { "zip" }), metadata.IncludedProperties[new PropertyPath(new[] { "address", "zip" })]);
        }

        [TestMethod]
        public async Task TryGetIncludedPropertiesMapsSpecialCharacterProperties()
        {
            ISecondaryIndexMetadata metadata = (await GetMetadataAsync(
                "SELECT c[\"a/b\"], c[\"a~1b\"] FROM c")).Single();

            Assert.AreEqual(2, metadata.IncludedProperties.Count);
            Assert.AreEqual(new PropertyPath(new[] { "a/b" }), metadata.IncludedProperties[new PropertyPath(new[] { "a/b" })]);
            Assert.AreEqual(new PropertyPath(new[] { "a~1b" }), metadata.IncludedProperties[new PropertyPath(new[] { "a~1b" })]);
        }

        [TestMethod]
        public async Task TryGetIncludedPropertiesDistinguishesPropertyFromNestedPath()
        {
            ISecondaryIndexMetadata metadata = (await GetMetadataAsync(
                "SELECT c[\"a/b\"], c.a.b FROM c")).Single();

            Assert.AreEqual(2, metadata.IncludedProperties.Count);
            Assert.AreEqual(new PropertyPath(new[] { "a/b" }), metadata.IncludedProperties[new PropertyPath(new[] { "a/b" })]);
            Assert.AreEqual(new PropertyPath(new[] { "b" }), metadata.IncludedProperties[new PropertyPath(new[] { "a", "b" })]);
        }

        [TestMethod]
        public async Task TryGetIncludedPropertiesDistinguishesLiteralStarFromWildcard()
        {
            ISecondaryIndexMetadata metadata = (await GetMetadataAsync("SELECT c['*'] FROM c")).Single();
            PropertyPath literalStar = new PropertyPath(new[] { "*" });

            Assert.AreEqual(1, metadata.IncludedProperties.Count);
            Assert.AreEqual(literalStar, metadata.IncludedProperties[literalStar]);
            Assert.IsFalse(metadata.IncludedProperties.ContainsKey(PropertyPath.Wildcard));
        }

        [DataTestMethod]
        [DataRow("/tenantId", new[] { "tenantId" })]
        [DataRow("/address/zip", new[] { "address", "zip" })]
        [DataRow("/\"a/b\"", new[] { "a/b" })]
        [DataRow("/\"\"", new[] { "" })]
        [DataRow("/*", new[] { "*" })]
        public async Task TryGetIncludedPropertiesMapsWildcardAndPartitionKey(string partitionKeyPath, string[] partitionKeySegments)
        {
            ISecondaryIndexMetadata metadata = (await GetMetadataAsync("SELECT * FROM c", partitionKeyPath)).Single();
            PropertyPath expectedPartitionKeyPath = new PropertyPath(partitionKeySegments);

            Assert.AreEqual(2, metadata.IncludedProperties.Count);
            Assert.AreEqual(PropertyPath.Wildcard, metadata.IncludedProperties[PropertyPath.Wildcard]);
            Assert.AreEqual(expectedPartitionKeyPath, metadata.IncludedProperties[expectedPartitionKeyPath]);
        }

        [TestMethod]
        public async Task TryGetIncludedPropertiesThrowsForNullDefinition()
        {
            ArgumentNullException exception = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => GetMetadataAsync(null));

            Assert.AreEqual("definition", exception.ParamName);
        }

        [DataTestMethod]
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
        public async Task TryGetIncludedPropertiesRejectsUnsupportedDefinitions(string query)
        {
            Assert.AreEqual(0, (await GetMetadataAsync(query)).Count());
        }

        #endregion TryGetIncludedProperties Tests

        [DataTestMethod]
        [DataRow("SELECT * FROM c", false)]
        [DataRow("SELECT * FROM c WHERE c.enabled = true", true)]
        public async Task IsFilteredMaterializedViewIdentifiesWhereClause(string query, bool expected)
        {
            Assert.AreEqual(expected ? 0 : 1, (await GetMetadataAsync(query)).Count());
        }

        private static async Task<IEnumerable<ISecondaryIndexMetadata>> GetMetadataAsync(string query, string partitionKeyPath = "/tenantId")
        {
            ContainerProperties source = CreateSource(partitionKeyPath);
            source.MaterializedViews = new List<MaterializedViewProperties>
            {
                new MaterializedViewProperties
                {
                    ResourceId = EligibleRid,
                    ContainerType = ContainerMetadataSecondaryIndexMetadataProvider.GlobalSecondaryIndexContainerType,
                },
            };

            Dictionary<string, ContainerProperties> collections = new Dictionary<string, ContainerProperties>
            {
                [SourceRid] = source,
                [EligibleRid] = CreateMaterializedView(EligibleRid, query, partitionKeyPath),
            };

            using ProviderTestContext context = new ProviderTestContext(collections);
            return await context.Provider.GetSecondaryIndexMetadataAsync(SourceRid, NoOpTrace.Singleton);
        }

        private static ContainerProperties CreateSource(string partitionKeyPath = "/tenantId")
        {
            ContainerProperties source = ContainerProperties.CreateWithResourceId(SourceRid);
            source.Id = "source";
            source.PartitionKey = new ContainerProperties("source", partitionKeyPath).PartitionKey;
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
                    ContainerType = ContainerMetadataSecondaryIndexMetadataProvider.GlobalSecondaryIndexContainerType,
                };

            candidate.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });
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
                this.Provider = new ContainerMetadataSecondaryIndexMetadataProvider(this.documentClient);
            }

            public ContainerMetadataSecondaryIndexMetadataProvider Provider { get; }

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
