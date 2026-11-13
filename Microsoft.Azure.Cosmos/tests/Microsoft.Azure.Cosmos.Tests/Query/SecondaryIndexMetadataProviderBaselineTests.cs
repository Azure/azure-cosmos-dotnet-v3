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
    using System.Xml;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Formatting = Newtonsoft.Json.Formatting;
    using IClientSideRequestStatistics = Microsoft.Azure.Documents.IClientSideRequestStatistics;
    using ResourceId = Microsoft.Azure.Documents.ResourceId;
    using ServerStoreModel = Microsoft.Azure.Documents.ServerStoreModel;

    [TestClass]
    public class SecondaryIndexMetadataProviderBaselineTests
        : BaselineTests<SecondaryIndexMetadataProviderBaselineTests.Input, SecondaryIndexMetadataProviderBaselineTests.Output>
    {
        private const string GlobalSecondaryIndexContainerType = "GlobalSecondaryIndex";

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
                    ContainerType = GlobalSecondaryIndexContainerType,
                },
                new MaterializedViewProperties
                {
                    ResourceId = GsiARid,
                    ContainerType = GlobalSecondaryIndexContainerType,
                },
                new MaterializedViewProperties
                {
                    ResourceId = GsiARid,
                    ContainerType = GlobalSecondaryIndexContainerType,
                },
            };

            Dictionary<string, ContainerProperties> collections = new Dictionary<string, ContainerProperties>
                {
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

            IEnumerable<ISecondaryIndexMetadata> metadata = await context.Provider.GetSecondaryIndexMetadataAsync(source, NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(2, context.ResolveCount);
            Assert.AreEqual(3, metadata.Count());
            CollectionAssert.AreEqual(
                new[] { GsiBRid, GsiARid, GsiARid },
                metadata.Select(candidate => candidate.Rid).ToArray());
            ISecondaryIndexMetadata gsiA = metadata.ElementAt(1);
            ISecondaryIndexMetadata gsiB = metadata.Single(candidate => candidate.Rid == GsiBRid);
            Assert.AreEqual(GsiARid, gsiA.Id);
            Assert.AreEqual(SourceRid, gsiA.SourceCollectionRid);
            Assert.AreEqual(new PropertyPath(new[] { "id" }), gsiA.IncludedProperties["_id"]);
            Assert.AreEqual(new PropertyPath(new[] { "region" }), gsiA.IncludedProperties["region"]);
            Assert.AreEqual(ConsistencyLevel.Eventual, gsiA.ConsistencyLevel);
            Assert.AreEqual(new PropertyPath(new[] { "category" }), gsiB.IncludedProperties["category"]);

            foreach (ISecondaryIndexMetadata candidate in metadata)
            {
                Assert.AreSame(collections[candidate.Rid].PartitionKey, candidate.PartitionKey);
                Assert.AreSame(collections[candidate.Rid].IndexingPolicy, candidate.IndexingPolicy);
            }
        }

        [TestMethod]
        public async Task ProviderReturnsEmptyForSourceWithoutReferences()
        {
            ContainerProperties source = CreateSource();
            Dictionary<string, ContainerProperties> collections = new Dictionary<string, ContainerProperties>();
            using ProviderTestContext context = new ProviderTestContext(collections);

            IEnumerable<ISecondaryIndexMetadata> metadata = await context.Provider.GetSecondaryIndexMetadataAsync(source, NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(0, metadata.Count());
            Assert.AreEqual(0, context.ResolveCount);
        }

        [TestMethod]
        public async Task ProviderRejectsNullSource()
        {
            using ProviderTestContext context = new ProviderTestContext(new Dictionary<string, ContainerProperties>());

            ArgumentNullException exception = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                context.Provider.GetSecondaryIndexMetadataAsync(null, NoOpTrace.Singleton, CancellationToken.None));

            Assert.AreEqual("source", exception.ParamName);
            Assert.AreEqual(0, context.ResolveCount);
        }

        [TestMethod]
        public async Task ProviderRejectsSourceWithoutResourceId()
        {
            using ProviderTestContext context = new ProviderTestContext(new Dictionary<string, ContainerProperties>());

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                context.Provider.GetSecondaryIndexMetadataAsync(new ContainerProperties("source", "/pk"), NoOpTrace.Singleton, CancellationToken.None));

            Assert.AreEqual("source", exception.ParamName);
            Assert.AreEqual(0, context.ResolveCount);
        }

        [TestMethod]
        public async Task ProviderHonorsCancellationForSourceWithoutReferences()
        {
            using ProviderTestContext context = new ProviderTestContext(new Dictionary<string, ContainerProperties>());
            using CancellationTokenSource cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
                context.Provider.GetSecondaryIndexMetadataAsync(CreateSource(), NoOpTrace.Singleton, cancellationSource.Token));

            Assert.AreEqual(0, context.ResolveCount);
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
                    [OtherRid] = unrelated,
                };
            using ProviderTestContext context = new ProviderTestContext(collections);

            IEnumerable<ISecondaryIndexMetadata> metadata = await context.Provider.GetSecondaryIndexMetadataAsync(source, NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(0, metadata.Count());
            Assert.AreEqual(1, context.ResolveCount);
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
                    ContainerType = GlobalSecondaryIndexContainerType,
                },
                new MaterializedViewProperties
                {
                    ResourceId = OtherTypeRid,
                    ContainerType = "MaterializedView",
                },
                new MaterializedViewProperties
                {
                    ResourceId = EligibleRid,
                    ContainerType = GlobalSecondaryIndexContainerType,
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
                    [FilteredRid] = filtered,
                    [OtherTypeRid] = otherType,
                    [EligibleRid] = eligible,
                };
            using ProviderTestContext context = new ProviderTestContext(collections);

            IEnumerable<ISecondaryIndexMetadata> metadata = await context.Provider.GetSecondaryIndexMetadataAsync(source, NoOpTrace.Singleton, CancellationToken.None);

            Assert.AreEqual(3, context.ResolveCount);
            Assert.AreEqual(2, metadata.Count());
            CollectionAssert.AreEqual(
                new[] { OtherTypeRid, EligibleRid },
                metadata.Select(candidate => candidate.Rid).ToArray());
        }

        [TestMethod]
        public void PropertyProjections()
        {
            this.ExecuteTestSuite(new[]
            {
                new Input("Aliased property", "SELECT c.id AS _id FROM c"),
                new Input("Bracket property", "SELECT c['category'] FROM c"),
                new Input("Aliased nested dot path", "SELECT c.address.zip AS postalCode FROM c"),
                new Input("Aliased nested bracket path", "SELECT c['address']['zip'] AS postalCode FROM c"),
                new Input("Mixed dot and bracket path", "SELECT c.address['zip'] FROM c"),
                new Input("Explicit ROOT alias", "SELECT item.id FROM ROOT item"),
                new Input("Explicit collection alias", "SELECT item.id FROM c AS item"),
                new Input("Empty property name", "SELECT c[''] AS empty FROM c"),
                new Input("Empty segment in nested path", "SELECT c[''].zip FROM c"),
                new Input("Literal dot in property name", "SELECT c['a.b'] FROM c"),
                new Input("Literal space in property name", "SELECT c['a b'] FROM c"),
                new Input("Literal quote in property name", "SELECT c['a\"b'] FROM c"),
                new Input("Multiple property paths", "SELECT c.id AS _id, c.region, c.address.zip AS postalCode FROM c"),
                new Input("Equivalent dot and bracket paths", "SELECT c.address.zip, c['address']['zip'] FROM c"),
                new Input("Same source path with different aliases", "SELECT c.address.zip AS postalCode, c.address.zip AS ZIP FROM c"),
                new Input("Case-sensitive projected names", "SELECT c.id, c.otherId AS ID FROM c"),
                new Input("Literal slash and tilde in property names", "SELECT c[\"a/b\"], c[\"a~1b\"] FROM c"),
                new Input("Literal slash versus nested path", "SELECT c[\"a/b\"], c.a.b FROM c"),
                new Input("Literal star in property name", "SELECT c['*'] FROM c"),
                new Input("Unfiltered materialized view", "SELECT c.id FROM c"),
            });
        }

        [TestMethod]
        public void RejectedDefinitions()
        {
            this.ExecuteTestSuite(new[]
            {
                new Input("Empty query", ""),
                new Input("Invalid query", "not a query"),
                new Input("SELECT VALUE", "SELECT VALUE c.id FROM c"),
                new Input("Root object projection", "SELECT c FROM c"),
                new Input("Arithmetic expression", "SELECT c.value + 1 AS value FROM c"),
                new Input("System function", "SELECT UPPER(c.name) AS name FROM c"),
                new Input("User-defined function", "SELECT udf.normalize(c.name) AS name FROM c"),
                new Input("Aggregate", "SELECT COUNT(1) AS count FROM c"),
                new Input("Mixed direct and computed projections", "SELECT c.id, UPPER(c.name) AS name FROM c"),
                new Input("Root property with JOIN", "SELECT c.id FROM c JOIN child IN c.children"),
                new Input("Joined alias projection", "SELECT child.id FROM c JOIN child IN c.children"),
                new Input("Array iterator", "SELECT item.id FROM item IN c.items"),
                new Input("Subquery collection", "SELECT item.id FROM (SELECT * FROM c) item"),
                new Input("Unrelated root identifier", "SELECT other.id FROM c"),
                new Input("Conflicting leaf names", "SELECT c.billing.zip, c.shipping.zip FROM c"),
                new Input("Conflicting leaf names in reverse order", "SELECT c.shipping.zip, c.billing.zip FROM c"),
                new Input("Conflicting alias", "SELECT c.id, c.name AS id FROM c"),
                new Input("Conflicting alias in reverse order", "SELECT c.name AS id, c.id FROM c"),

                // Wildcard projections are unsupported regardless of alias or partition-key shape.
                new Input("Wildcard with ROOT alias", "SELECT * FROM ROOT item"),
                new Input("Wildcard with collection alias", "SELECT * FROM c AS item"),
                new Input("Simple partition key", "SELECT * FROM c", "/tenantId"),
                new Input("Nested partition key", "SELECT * FROM c", "/address/zip"),
                new Input("Literal slash in partition key", "SELECT * FROM c", "/\"a/b\""),
                new Input("Empty partition key segment", "SELECT * FROM c", "/\"\""),
                new Input("Literal star partition key", "SELECT * FROM c", "/*"),

                // Filtered materialized views are not eligible for routing.
                new Input("Filtered materialized view", "SELECT c.id FROM c WHERE c.enabled = true"),
            });
        }

        [TestMethod]
        public async Task TryGetIncludedPropertiesThrowsForNullDefinition()
        {
            ArgumentNullException exception = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => GetMetadataAsync(null));

            Assert.AreEqual("definition", exception.ParamName);
        }

        public override Output ExecuteTest(Input input)
        {
            return new Output(GetMetadataAsync(input.Query, input.PartitionKeyPath).GetAwaiter().GetResult());
        }

        private static async Task<IEnumerable<ISecondaryIndexMetadata>> GetMetadataAsync(string query, string partitionKeyPath = "/tenantId")
        {
            ContainerProperties source = CreateSource(partitionKeyPath);
            source.MaterializedViews = new List<MaterializedViewProperties>
            {
                new MaterializedViewProperties
                {
                    ResourceId = EligibleRid,
                    ContainerType = GlobalSecondaryIndexContainerType,
                },
            };

            Dictionary<string, ContainerProperties> collections = new Dictionary<string, ContainerProperties>
            {
                [EligibleRid] = CreateMaterializedView(EligibleRid, query, partitionKeyPath),
            };

            using ProviderTestContext context = new ProviderTestContext(collections);
            return await context.Provider.GetSecondaryIndexMetadataAsync(source, NoOpTrace.Singleton, CancellationToken.None);
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
                    ContainerType = GlobalSecondaryIndexContainerType,
                };

            candidate.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });
            return candidate;
        }

        public sealed class Input : BaselineTestInput
        {
            public Input(string description, string query, string partitionKeyPath = "/tenantId")
                : base(description)
            {
                this.Query = query;
                this.PartitionKeyPath = partitionKeyPath;
            }

            public string Query { get; }

            public string PartitionKeyPath { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteElementString(nameof(this.Description), this.Description);
                xmlWriter.WriteElementString(nameof(this.Query), this.Query);
                xmlWriter.WriteElementString(nameof(this.PartitionKeyPath), this.PartitionKeyPath);
            }
        }

        public sealed class Output : BaselineTestOutput
        {
            private readonly IEnumerable<ISecondaryIndexMetadata> metadata;

            internal Output(IEnumerable<ISecondaryIndexMetadata> metadata)
            {
                this.metadata = metadata;
            }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteStartElement("Candidates");
                foreach (ISecondaryIndexMetadata candidate in this.metadata)
                {
                    Dictionary<string, IEnumerable<string>> projections = candidate.IncludedProperties
                        .OrderBy(projection => projection.Key, StringComparer.Ordinal)
                        .ToDictionary(projection => projection.Key, projection => projection.Value.Segments, StringComparer.Ordinal);
                    string serializedProjections = JsonConvert.SerializeObject(projections, Formatting.None, new JsonSerializerSettings
                    {
                        StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                    });

                    // Baseline comparison ignores whitespace, so preserve literal spaces as JSON escapes.
                    serializedProjections = serializedProjections.Replace(" ", "\\u0020");
                    xmlWriter.WriteStartElement("Candidate");
                    xmlWriter.WriteElementString("IncludedProperties", serializedProjections);
                    xmlWriter.WriteEndElement();
                }

                xmlWriter.WriteEndElement();
            }
        }

        private sealed class ProviderTestContext : IDisposable
        {
            private readonly TestClientCollectionCache collectionCache;
            private readonly TestDocumentClient documentClient;

            public ProviderTestContext(IReadOnlyDictionary<string, ContainerProperties> collections)
            {
                this.collectionCache = new TestClientCollectionCache(collections);
                this.documentClient = new TestDocumentClient(this.collectionCache);
                this.Provider = new SecondaryIndexMetadataProvider(this.documentClient);
            }

            public SecondaryIndexMetadataProvider Provider { get; }

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
                : base(new SessionContainer("testhost"), new ServerStoreModel(null), null, null, true, null)
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
