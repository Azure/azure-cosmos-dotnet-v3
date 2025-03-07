namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public class DistributedQueryClientTests : QueryTestsBase
    {
        private const int DocumentCount = 420;

        private static readonly int[] PageSizes = new[] { 1, 10, 100, DocumentCount };

        [TestMethod]
        public async Task SanityTestsAsync()
        {
            static Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> _)
            {
                TestCase[] testCases = new[]
                {
                    MakeTest(
                        "SELECT VALUE c.x FROM c",
                        PageSizes,
                        Expectations.AllDocumentsArePresent),
                    MakeTest(
                        "SELECT VALUE c.x FROM c WHERE c.x < 200",
                        PageSizes,
                        Expectations.AllDocumentsLessThan200ArePresent),
                    MakeTest(
                        "SELECT VALUE c.x FROM c WHERE c.x > 200",
                        PageSizes,
                        Expectations.AllDocumentsGreaterThan200ArePresent),
                };

                return RunTestsAsync(container, testCases);
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                CreateDocuments(DocumentCount),
                ImplementationAsync);
        }

        [TestMethod]
        public async Task ContinuationTestsAsync()
        {
            static Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> _)
            {
                TestCase[] testCases = new[]
                {
                    MakeTest(
                        "SELECT VALUE c.x FROM c",
                        PageSizes,
                        Expectations.AllDocumentsArePresent),
                    MakeTest(
                        "SELECT VALUE c.x FROM c WHERE c.x < 200",
                        PageSizes,
                        Expectations.AllDocumentsLessThan200ArePresent),
                    MakeTest(
                        "SELECT VALUE c.x FROM c WHERE c.x > 200",
                        PageSizes,
                        Expectations.AllDocumentsGreaterThan200ArePresent),
                };

                return ContinuationTestsAsync(container, testCases);
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                CreateDocuments(DocumentCount),
                ImplementationAsync);
        }

        [TestMethod]
        public async Task PartitionedParityTestsAsync()
        {
            static Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> _)
            {
                string[] testCases = new[]
                {
                    "SELECT VALUE COUNT(1) FROM c",
                    "SELECT VALUE COUNT(1) FROM c WHERE c.x <= 400",
                    "SELECT c.x % 3 AS mod3 , COUNT(1) FROM c GROUP BY c.x % 3",
                    "SELECT c.x % 10 AS mod10 , MAX(c.x) FROM c GROUP BY c.x % 10",
                    "SELECT c.id, c.x FROM c ORDER BY c.x",
                    "SELECT c.id, c.x FROM c ORDER BY c.x DESC",
                    "SELECT c.id, c.x FROM c ORDER BY c.x OFFSET 2 LIMIT 2",
                    "SELECT c.id, c.x FROM c ORDER BY c.x DESC OFFSET 2 LIMIT 2",
                    "SELECT TOP 20 c.id, c.x FROM c WHERE c.x <= 200 ORDER BY c.x DESC",
                };

                return RunPartitionedParityTestsAsync(container, testCases);
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                CreateDocuments(DocumentCount),
                ImplementationAsync);
        }

        [TestMethod]
        public void TestDistributedQueryGatewayModeOverride()
        {
            {
                using EnvironmentVariableOverride enableDistributedQueryOverride = new EnvironmentVariableOverride(
                    ConfigurationManager.DistributedQueryGatewayModeEnabled,
                    bool.TrueString);

                QueryRequestOptions options = new QueryRequestOptions();
                Assert.IsTrue(options.EnableDistributedQueryGatewayMode);

                options = new QueryRequestOptions()
                {
                    EnableDistributedQueryGatewayMode = false,
                };
                Assert.IsFalse(options.EnableDistributedQueryGatewayMode);

                options = new QueryRequestOptions()
                {
                    EnableDistributedQueryGatewayMode = true,
                };
                Assert.IsTrue(options.EnableDistributedQueryGatewayMode);

                options = new QueryRequestOptions();
                Assert.IsTrue(options.EnableDistributedQueryGatewayMode);
            }

            Assert.IsNull(Environment.GetEnvironmentVariable(ConfigurationManager.DistributedQueryGatewayModeEnabled));
        }

        [TestMethod]
        public async Task StreamIteratorTestsAsync()
        {
            static Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> _)
            {
                int[] pageSizes = new[] { DocumentCount };

                TestCase[] testCases = new[]
                {
                    MakeTest(
                        "SELECT VALUE c.x FROM c WHERE c.x < 200",
                        pageSizes,
                        Expectations.AllDocumentsLessThan200ArePresent),
                };

                return RunStreamIteratorTestsAsync(container, testCases);
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                CreateDocuments(DocumentCount),
                ImplementationAsync);
        }

        private static async Task RunPartitionedParityTestsAsync(Container container, IEnumerable<string> testCases)
        {
            IReadOnlyList<FeedRange> feedRanges = await container.GetFeedRangesAsync();

            foreach (string query in testCases)
            {
                int index = 0;
                foreach (FeedRange _ in feedRanges)
                {
                    FeedRangePartitionKeyRange feedRangePartitionKeyRange = new FeedRangePartitionKeyRange(index.ToString());

                    List<CosmosElement> expected = await RunQueryAsync(
                        container,
                        feedRangePartitionKeyRange,
                        query,
                        new QueryRequestOptions());

                    QueryRequestOptions options = new QueryRequestOptions()
                    {
                        EnableDistributedQueryGatewayMode = true,
                    };

                    List<CosmosElement> actual = await RunQueryAsync(
                        container,
                        feedRangePartitionKeyRange,
                        query,
                        options);

                    if (!expected.SequenceEqual(actual))
                    {
                        System.Diagnostics.Trace.TraceError($"Expected: {string.Join(",", expected)}");
                        System.Diagnostics.Trace.TraceError($"Actual: {string.Join(",", actual)}");
                    }
                }
            }
        }

        private static async Task<List<CosmosElement>> RunQueryAsync(
            Container container,
            FeedRange feedRange,
            string query,
            QueryRequestOptions options)
        {
            FeedIterator<CosmosElement> feedIterator = container.GetItemQueryIterator<CosmosElement>(
                queryDefinition: new QueryDefinition(query),
                feedRange: feedRange,
                requestOptions: options);

            List<CosmosElement> results = new List<CosmosElement>();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<CosmosElement> feedResponse = await feedIterator.ReadNextAsync();
                Assert.IsTrue(feedResponse.StatusCode.IsSuccess());
                results.AddRange(feedResponse);
            }

            return results;
        }

        private static async Task ContinuationTestsAsync(Container container, IEnumerable<TestCase> testCases)
        {
            foreach (TestCase testCase in testCases)
            {
                foreach (int pageSize in testCase.PageSizes)
                {
                    List<int> results = await RunContinuationBasedQueryTestAsync(container, testCase.Query, pageSize);
                    Assert.IsTrue(testCase.ValidateResult(results));
                }
            }
        }

        private static async Task<List<int>> RunContinuationBasedQueryTestAsync(
            Container container,
            string query,
            int pageSize)
        {
            QueryRequestOptions options = new QueryRequestOptions()
            {
                MaxItemCount = pageSize,
                EnableDistributedQueryGatewayMode = false,
            };
            FeedIterator<int> feedIterator = container.GetItemQueryIterator<int>(query, requestOptions: options);

            List<int> results = new List<int>();
            Assert.IsTrue(feedIterator.HasMoreResults);
            FeedResponse<int> response = await feedIterator.ReadNextAsync();
            results.AddRange(response);

            options = new QueryRequestOptions()
            {
                MaxItemCount = pageSize,
                EnableDistributedQueryGatewayMode = true,
            };
            feedIterator = container.GetItemQueryIterator<int>(
                    query,
                    continuationToken: response.ContinuationToken,
                    requestOptions: options);

            while (feedIterator.HasMoreResults)
            {
                response = await feedIterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }

        private static async Task RunTestsAsync(Container container, IEnumerable<TestCase> testCases)
        {
            foreach (TestCase testCase in testCases)
            {
                foreach (int pageSize in testCase.PageSizes)
                {
                    QueryRequestOptions options = new QueryRequestOptions()
                    {
                        MaxItemCount = pageSize,
                        EnableDistributedQueryGatewayMode = true,
                    };

                    List<int> results = await RunQueryCombinationsAsync<int>(
                        container,
                        testCase.Query,
                        options,
                        QueryDrainingMode.HoldState | QueryDrainingMode.ContinuationToken);

                    Assert.IsTrue(testCase.ValidateResult(results));
                }
            }
        }

        private static async Task RunStreamIteratorTestsAsync(Container container, IEnumerable<TestCase> testCases)
        {
            foreach (TestCase testCase in testCases)
            {
                foreach (int pageSize in testCase.PageSizes)
                {
                    QueryRequestOptions options = new QueryRequestOptions()
                    {
                        MaxItemCount = pageSize,
                        EnableDistributedQueryGatewayMode = true,
                    };

                    List<int> extractedResults = new List<int>();
                    await foreach (ResponseMessage response in RunSimpleQueryAsync(
                        container,
                        testCase.Query,
                        options))
                    {
                        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);

                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            response.Content.CopyTo(memoryStream);
                            byte[] content = memoryStream.ToArray();

                            IJsonNavigator navigator = JsonNavigator.Create(content);
                            IJsonNavigatorNode rootNode = navigator.GetRootNode();

                            Assert.IsTrue(navigator.TryGetObjectProperty(rootNode, "_rid", out ObjectProperty ridProperty));
                            string rid = navigator.GetStringValue(ridProperty.ValueNode);
                            Assert.IsTrue(rid.Length > 0);

                            Assert.IsTrue(navigator.TryGetObjectProperty(rootNode, "Documents", out ObjectProperty documentsProperty));
                            IEnumerable<IJsonNavigatorNode> arrayItems = navigator.GetArrayItems(documentsProperty.ValueNode);
                            foreach (IJsonNavigatorNode node in arrayItems)
                            {
                                Assert.AreEqual(JsonNodeType.Number, navigator.GetNodeType(node));

                                extractedResults.Add((int)Number64.ToLong(navigator.GetNumberValue(node)));
                            }
                        }
                    }

                    Assert.IsTrue(testCase.ValidateResult(extractedResults));
                }
            }
        }

        private static IEnumerable<string> CreateDocuments(int count)
        {
            return Enumerable
                .Range(0, count)
                .Select(x => $"{{\"id\": \"{x}\", \"x\": {x}}}");
        }

        private static TestCase MakeTest(string query, int[] pageSizes, Func<List<int>, bool> validateResult)
        {
            return new TestCase(query, pageSizes, validateResult);
        }

        private sealed class TestCase
        {
            public string Query { get; }

            public int[] PageSizes { get; }

            public Func<List<int>, bool> ValidateResult { get; }

            public TestCase(string query, int[] pageSizes, Func<List<int>, bool> validateResult)
            {
                this.Query = query ?? throw new ArgumentNullException(nameof(query));
                this.PageSizes = pageSizes ?? throw new ArgumentNullException(nameof(pageSizes));
                this.ValidateResult = validateResult ?? throw new ArgumentNullException(nameof(validateResult));
            }
        }

        private static class Expectations
        {
            private static readonly IEnumerable<int> DocumentIndices = Enumerable
                .Range(0, DocumentCount);

            public static bool AllDocumentsArePresent(List<int> actual)
            {
                return DocumentIndices.ToHashSet().SetEquals(actual);
            }

            public static bool AllDocumentsLessThan200ArePresent(List<int> actual)
            {
                return DocumentIndices
                    .Where(x => x < 200)
                    .ToHashSet()
                    .SetEquals(actual);
            }

            public static bool AllDocumentsGreaterThan200ArePresent(List<int> actual)
            {
                return DocumentIndices
                    .Where(x => x > 200)
                    .ToHashSet()
                    .SetEquals(actual);
            }
        }

        private sealed class EnvironmentVariableOverride : IDisposable
        {
            private readonly string originalValue;
            private readonly string variableName;

            public EnvironmentVariableOverride(string variableName, string value)
            {
                this.variableName = variableName;
                this.originalValue = Environment.GetEnvironmentVariable(variableName);
                Environment.SetEnvironmentVariable(variableName, value);
            }

            public void Dispose()
            {
                Environment.SetEnvironmentVariable(this.variableName, this.originalValue);
            }
        }
    }
}