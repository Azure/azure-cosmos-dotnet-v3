namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
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
                        Expectations.AllDocumentsLessThan200ArePresent)
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

        private static async Task ContinuationTestsAsync(Container container, IEnumerable<TestCase> testCases)
        {
            foreach (TestCase testCase in testCases)
            {
                foreach (int pageSize in testCase.PageSizes)
                {
                    List<int> results = await RunContinuationBasedQueryTestAsync(container, testCase.Query, pageSize);
                    testCase.ValidateResult(results);
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

                    testCase.ValidateResult(results);
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