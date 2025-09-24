namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class BypassQueryParsingTests : QueryTestsBase
    {
        private const int DocumentCount = 400;

        private static readonly Documents.PartitionKeyDefinition HierarchicalPartitionKeyDefinition = new Documents.PartitionKeyDefinition
        {
            Paths = new Collection<string> { "/nullField", "/numberField" },
            Kind = Documents.PartitionKind.MultiHash,
            Version = Documents.PartitionKeyDefinitionVersion.V2
        };

        private static readonly Documents.PartitionKeyDefinition SimplePartitionKeyDefinition = new Documents.PartitionKeyDefinition
        {
            Paths = new Collection<string> { "/UndefinedField" },
            Kind = Documents.PartitionKind.Hash
        };

        [TestMethod]
        [TestCategory("Query")]
        public async Task TestBypassQueryParsing()
        {
            IReadOnlyList<QueryTestCase> testCases = new List<QueryTestCase>
            {
                new(
                    PartitionKey.None,
                    "SELECT VALUE r.numberField FROM r",
                    Enumerable.Range(0, DocumentCount).Select(i => i.ToString()).ToList(),
                    TestInjections.PipelineType.Passthrough),
                new(
                    PartitionKey.None,
                    @"SELECT VALUE { """" : r.numberField } FROM r",
                    Enumerable.Range(0, DocumentCount).Select(i => String.Format("{{\"\":{0}}}", i)).ToList(),
                    TestInjections.PipelineType.Passthrough),
            };

            await this.ValidateQueryBypass(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.NonPartitioned | CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                SimplePartitionKeyDefinition,
                testCases);
        }

        [TestMethod]
        [TestCategory("Query")]
        public async Task TestBypassQueryParsingWithHPK()
        {
            PartitionKey partialPartitionKey = new PartitionKeyBuilder().AddNullValue().Build();

            IReadOnlyList<QueryTestCase> testCases = new List<QueryTestCase>
            {
                new(
                    partialPartitionKey,
                    "SELECT VALUE r.numberField FROM r",
                    Enumerable.Range(0, DocumentCount).Select(i => i.ToString()).ToList(),
                    TestInjections.PipelineType.Passthrough), // Passthrough because it is a client streaming query
                new(
                    partialPartitionKey,
                    $"SELECT TOP {DocumentCount} VALUE r.numberField FROM r",
                    Enumerable.Range(0, DocumentCount).Select(i => i.ToString()).ToList(),
                    TestInjections.PipelineType.Specialized),
                new(
                    partialPartitionKey,
                    @"SELECT VALUE { """" : r.numberField } FROM r",
                    Enumerable.Range(0, DocumentCount).Select(i => String.Format("{{\"\":{0}}}", i)).ToList(),
                    TestInjections.PipelineType.Passthrough), // Passthrough because it is a client streaming query
                new(
                    partialPartitionKey,
                    "SELECT VALUE r.numberField FROM r ORDER BY r.numberField",
                    Enumerable.Range(0, DocumentCount).Select(i => i.ToString()).ToList(),
                    TestInjections.PipelineType.Specialized),
            };

            await this.ValidateQueryBypass(
                ConnectionModes.Gateway,
                CollectionTypes.MultiPartition,
                HierarchicalPartitionKeyDefinition,
                testCases);
        }

        private Task ValidateQueryBypass(ConnectionModes connectionModes, CollectionTypes collectionTypes, Documents.PartitionKeyDefinition partitionKeyDefinition, IReadOnlyList<QueryTestCase> testCases)
        {
            IReadOnlyList<string> documents = CreateDocuments(DocumentCount);

            return this.CreateIngestQueryDeleteAsync(
                connectionModes,
                collectionTypes,
                documents,
                query: (container, _) => RunTestsAsync(container, testCases),
                partitionKeyDefinition);
        }

        private static async Task RunTestsAsync(Container container, IReadOnlyList<QueryTestCase> testCases)
        {
            foreach (QueryTestCase testCase in testCases)
            {
                QueryRequestOptions feedOptions = new QueryRequestOptions
                {
                    PartitionKey = testCase.PartitionKey,
                    TestSettings = new TestInjections(simulate429s: false, simulateEmptyPages: false, responseStats: new())
                };

                ContainerInternal containerCore = container as ContainerInlineCore;

                MockCosmosQueryClient cosmosQueryClientCore = new MockCosmosQueryClient(
                    containerCore.ClientContext,
                    containerCore,
                    forceQueryPlanGatewayElseServiceInterop: true);

                ContainerInternal containerWithBypassParsing = new ContainerInlineCore(
                    containerCore.ClientContext,
                    (DatabaseCore)containerCore.Database,
                    containerCore.Id,
                    cosmosQueryClientCore);

                List<CosmosElement> items = await RunQueryAsync(containerWithBypassParsing, testCase.Query, feedOptions);
                string[] actualOutput = items.Select(x => x.ToString()).ToArray();

                if(!testCase.ExpectedOutput.SequenceEqual(actualOutput))
                {
                    System.Diagnostics.Trace.WriteLine($"Expected: [{string.Join(", ", testCase.ExpectedOutput)}]");
                    System.Diagnostics.Trace.WriteLine($"Actual:   [{string.Join(", ", actualOutput)}]");

                    Assert.Fail("Query results do not match expected results.");
                }

                Assert.AreEqual(testCase.ExpectedPipelineType, feedOptions.TestSettings.Stats.PipelineType);
            }
        }

        private static IReadOnlyList<string> CreateDocuments(int documentCount)
        {
            List<string> documents = new List<string>(documentCount);
            for (int i = 0; i < documentCount; ++i)
            {
                string document = $@"{{ ""numberField"": {i}, ""nullField"": null }}";
                documents.Add(document);
            }

            return documents;
        }

        private sealed class QueryTestCase
        {
            public PartitionKey PartitionKey { get; }

            public string Query { get; }

            public IReadOnlyList<string> ExpectedOutput { get; }

            public TestInjections.PipelineType ExpectedPipelineType { get; }

            public QueryTestCase(PartitionKey partitionKey, string query, IReadOnlyList<string> expectedOutput, TestInjections.PipelineType expectedPipelineType)
            {
                this.PartitionKey = partitionKey;
                this.Query = query ?? throw new ArgumentNullException(nameof(query));
                this.ExpectedOutput = expectedOutput ?? throw new ArgumentNullException(nameof(expectedOutput));
                this.ExpectedPipelineType = expectedPipelineType;
            }
        }
    }
}