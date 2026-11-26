//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using CosmosDocuments = Microsoft.Azure.Documents;
    using TestCommon = Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestCommon;

    [TestClass]
    [TestCategory("Query")]
    [TestCategory("WindowsArm64QueryPlanInteropLive")]
    [DoNotParallelize]
    public sealed class WindowsArm64QueryPlanInteropLiveTests
    {
        // Uneven partition populations and an outlier exercise weighted AVG merging.
        private const int DocumentCount = 131;
        private static readonly TestDocument[] Documents = Enumerable.Range(0, DocumentCount)
            .Select(index => new TestDocument
            {
                Id = "doc-" + ((index * 37) % DocumentCount).ToString("D3", CultureInfo.InvariantCulture),
                Tenant = "tenant-" + (index % 32).ToString("D2", CultureInfo.InvariantCulture),
                Category = "category-" + (index % 7).ToString(CultureInfo.InvariantCulture),
                Score = index,
                Amount = index == DocumentCount - 1 ? 1000 : index - 64,
            }).ToArray();

        private static CosmosClient admin;
        private static Database database;
        private static Container container;
        private static string sessionToken;

        [ClassInitialize]
        public static async Task InitializeAsync(TestContext testContext)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                || RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
            {
                Assert.Inconclusive("Requires native Windows ARM64.");
            }

            Assert.IsTrue(ConfigurationManager.IsWindowsArm64QueryInteropEnabled(), "Windows ARM64 opt-in must be enabled.");
            Assert.AreSame(WindowsArm64QueryPlanInterop.Instance, QueryPlanInterop.Instance);
            Assert.IsFalse(QueryPlanRetriever.BypassQueryParsing(), "Native planning must be available and enabled.");

            admin = TestCommon.CreateCosmosClient(true);
            database = admin.GetDatabase("qpi-arm64-live-" + Guid.NewGuid().ToString("N"));
            bool initialized = false;
            try
            {
                await admin.CreateDatabaseAsync(database.Id);
                ContainerProperties properties = new ContainerProperties("queries", new List<string> { "/tenant", "/id" });
                // Exceed one physical partition's 10,000 RU/s capacity, sharing this temporary container across all four tests.
                container = await database.CreateContainerAsync(properties, throughput: 11000);
                IReadOnlyList<FeedRange> ranges = await container.GetFeedRangesAsync();
                Assert.IsTrue(ranges.Count > 1, "The fixture must have multiple physical partitions.");

                for (int start = 0; start < Documents.Length; start += 8)
                {
                    await Task.WhenAll(Documents.Skip(start).Take(8).Select(document =>
                        container.CreateItemAsync(document, new PartitionKeyBuilder().Add(document.Tenant).Add(document.Id).Build())));
                }

                // New clients for each planning mode must observe every partition's seeded writes.
                using (CosmosDocuments.DocumentServiceRequest request = CosmosDocuments.DocumentServiceRequest.Create(
                    CosmosDocuments.OperationType.Query,
                    CosmosDocuments.ResourceType.Document,
                    $"dbs/{database.Id}/colls/{container.Id}/docs",
                    CosmosDocuments.AuthorizationTokenType.PrimaryMasterKey,
                    null))
                {
                    sessionToken = admin.DocumentClient.sessionContainer.ResolveGlobalSessionToken(request);
                }

                Assert.IsFalse(string.IsNullOrEmpty(sessionToken), "Seed writes must produce a session token.");
                int populatedRanges = 0;
                int total = 0;
                foreach (FeedRange range in ranges)
                {
                    int count = 0;
                    using FeedIterator<int> iterator = container.GetItemQueryIterator<int>(
                        feedRange: range,
                        queryDefinition: new QueryDefinition("SELECT VALUE COUNT(1) FROM c"),
                        requestOptions: new QueryRequestOptions
                        {
                            EnableOptimisticDirectExecution = false,
                            SessionToken = sessionToken,
                        });
                    while (iterator.HasMoreResults)
                    {
                        count += (await iterator.ReadNextAsync()).Sum();
                    }

                    populatedRanges += count > 0 ? 1 : 0;
                    total += count;
                }

                Assert.AreEqual(Documents.Length, total, "Every seeded document must be visible across the feed ranges.");
                Assert.IsTrue(populatedRanges > 1, "Data must populate multiple physical partitions, not just multiple logical keys.");
                testContext.WriteLine($"Seeded {total} documents across {populatedRanges} populated physical partitions.");
                initialized = true;
            }
            finally
            {
                if (!initialized)
                {
                    await CleanupAsync();
                }
            }
        }

        [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
        public static async Task CleanupAsync()
        {
            try
            {
                if (database != null)
                {
                    using ResponseMessage response = await database.DeleteStreamAsync();
                    Console.WriteLine($"Owned database {database.Id} cleanup: {response.StatusCode}.");
                    if (response.StatusCode != HttpStatusCode.NotFound)
                    {
                        response.EnsureSuccessStatusCode();
                    }
                }
            }
            finally
            {
                admin?.Dispose();
                admin = null;
                database = null;
                container = null;
            }
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task CrossPartitionOrderByTopAndContinuationAsync()
        {
            await RunInBothModesAsync(async (queryContainer, gateway) =>
            {
                List<string> ordered = await ReadQueryAsync<string>(
                    queryContainer,
                    new QueryDefinition("SELECT VALUE c.id FROM c ORDER BY c.score"),
                    gateway,
                    recreateIterator: true);
                CollectionAssert.AreEqual(Documents.OrderBy(document => document.Score).Select(document => document.Id).ToArray(), ordered);

                List<string> top = await ReadQueryAsync<string>(
                    queryContainer,
                    new QueryDefinition("SELECT TOP @take VALUE c.id FROM c ORDER BY c.score DESC").WithParameter("@take", 17),
                    gateway);
                CollectionAssert.AreEqual(Documents.OrderByDescending(document => document.Score).Take(17).Select(document => document.Id).ToArray(), top);
            });
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task CrossPartitionAggregatesAndGroupByAsync()
        {
            await RunInBothModesAsync(async (queryContainer, gateway) =>
            {
                List<AggregateResult> aggregates = await ReadQueryAsync<AggregateResult>(
                    queryContainer,
                    new QueryDefinition("SELECT COUNT(1) AS count, SUM(c.amount) AS sum, AVG(c.amount) AS average FROM c"),
                    gateway);
                Assert.AreEqual(1, aggregates.Count);
                Assert.AreEqual(Documents.Length, aggregates[0].Count);
                Assert.AreEqual(Documents.Sum(document => document.Amount), aggregates[0].Sum, 1e-9);
                Assert.AreEqual(Documents.Average(document => document.Amount), aggregates[0].Average, 1e-9);

                List<AggregateResult> groups = await ReadQueryAsync<AggregateResult>(
                    queryContainer,
                    new QueryDefinition("SELECT c.category, COUNT(1) AS count, SUM(c.amount) AS sum FROM c GROUP BY c.category"),
                    gateway);
                IGrouping<string, TestDocument>[] expected = Documents.GroupBy(document => document.Category).OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
                AggregateResult[] actual = groups.OrderBy(group => group.Category, StringComparer.Ordinal).ToArray();
                Assert.AreEqual(expected.Length, actual.Length);
                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.AreEqual(expected[i].Key, actual[i].Category);
                    Assert.AreEqual(expected[i].Count(), actual[i].Count);
                    Assert.AreEqual(expected[i].Sum(document => document.Amount), actual[i].Sum, 1e-9);
                }
            });
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task HierarchicalPrefixAndParameterizedQueriesAsync()
        {
            await RunInBothModesAsync(async (queryContainer, gateway) =>
            {
                const string tenant = "tenant-05";
                List<string> prefix = await ReadQueryAsync<string>(
                    queryContainer,
                    new QueryDefinition("SELECT VALUE c.id FROM c WHERE c.tenant = @tenant AND c.score >= @minimum ORDER BY c.score")
                        .WithParameter("@tenant", tenant).WithParameter("@minimum", 37),
                    gateway,
                    partitionKey: new PartitionKeyBuilder().Add(tenant).Build());
                CollectionAssert.AreEqual(
                    Documents.Where(document => document.Tenant == tenant && document.Score >= 37).OrderBy(document => document.Score).Select(document => document.Id).ToArray(),
                    prefix);

                List<string> parameterized = await ReadQueryAsync<string>(
                    queryContainer,
                    new QueryDefinition("SELECT VALUE c.id FROM c WHERE c.score >= @minimum AND c.score < @maximum AND c.category = @category ORDER BY c.score")
                        .WithParameter("@minimum", 11).WithParameter("@maximum", 113).WithParameter("@category", "category-3"),
                    gateway);
                CollectionAssert.AreEqual(
                    Documents.Where(document => document.Score >= 11 && document.Score < 113 && document.Category == "category-3")
                        .OrderBy(document => document.Score).Select(document => document.Id).ToArray(),
                    parameterized);
            });
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task CrossPartitionDistinctAndOffsetLimitAsync()
        {
            await RunInBothModesAsync(async (queryContainer, gateway) =>
            {
                List<string> distinct = await ReadQueryAsync<string>(
                    queryContainer,
                    new QueryDefinition("SELECT DISTINCT VALUE c.category FROM c ORDER BY c.category"),
                    gateway);
                CollectionAssert.AreEqual(
                    Documents.Select(document => document.Category).Distinct().OrderBy(category => category, StringComparer.Ordinal).ToArray(),
                    distinct);

                List<string> offset = await ReadQueryAsync<string>(
                    queryContainer,
                    new QueryDefinition("SELECT VALUE c.id FROM c ORDER BY c.score OFFSET @offset LIMIT @limit")
                        .WithParameter("@offset", 13).WithParameter("@limit", 17),
                    gateway);
                CollectionAssert.AreEqual(
                    Documents.OrderBy(document => document.Score).Skip(13).Take(17).Select(document => document.Id).ToArray(),
                    offset);
            });
        }

        private static async Task RunInBothModesAsync(Func<Container, bool, Task> test)
        {
            string originalBypass = Environment.GetEnvironmentVariable(ConfigurationManager.BypassQueryParsing);
            try
            {
                foreach (bool gateway in new[] { true, false })
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BypassQueryParsing, gateway ? "true" : "false");
                    Assert.AreEqual(gateway, QueryPlanRetriever.BypassQueryParsing());
                    using CosmosClient client = TestCommon.CreateCosmosClient(true);
                    await test(client.GetContainer(database.Id, container.Id), gateway);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BypassQueryParsing, originalBypass);
            }
        }

        private static async Task<List<T>> ReadQueryAsync<T>(
            Container queryContainer,
            QueryDefinition query,
            bool gateway,
            PartitionKey? partitionKey = null,
            bool recreateIterator = false)
        {
            QueryRequestOptions options = new QueryRequestOptions
            {
                EnableOptimisticDirectExecution = false,
                MaxItemCount = 5,
                MaxConcurrency = 1,
                SessionToken = sessionToken,
                PartitionKey = partitionKey,
            };
            List<T> actual = new List<T>();
            StringBuilder diagnostics = new StringBuilder();
            string continuation = null;
            int requests = 0;
            int resumes = 0;
            do
            {
                using FeedIterator<T> iterator = queryContainer.GetItemQueryIterator<T>(query, continuation, options);
                while (iterator.HasMoreResults)
                {
                    FeedResponse<T> page = await iterator.ReadNextAsync();
                    actual.AddRange(page);
                    diagnostics.Append(page.Diagnostics);
                    Assert.IsTrue(++requests <= 256, "The bounded fixture should not require more than 256 pages.");
                    if (recreateIterator)
                    {
                        continuation = page.ContinuationToken;
                        resumes += continuation != null ? 1 : 0;
                        break;
                    }
                }
            }
            while (recreateIterator && continuation != null);

            if (recreateIterator)
            {
                Assert.IsTrue(resumes > 0, "This query must exercise continuation-token resume with a new iterator.");
            }

            string trace = diagnostics.ToString();
            StringAssert.Contains(trace, gateway ? "Gateway QueryPlan" : "Service Interop Query Plan");
            Assert.IsFalse(trace.Contains(gateway ? "Service Interop Query Plan" : "Gateway QueryPlan"), $"Unexpected planning path for Gateway={gateway}.");
            return actual;
        }

        private sealed class TestDocument
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("tenant")]
            public string Tenant { get; set; }

            [JsonProperty("category")]
            public string Category { get; set; }

            [JsonProperty("score")]
            public int Score { get; set; }

            [JsonProperty("amount")]
            public int Amount { get; set; }
        }

        private sealed class AggregateResult
        {
            [JsonProperty("category")]
            public string Category { get; set; }

            [JsonProperty("count")]
            public int Count { get; set; }

            [JsonProperty("sum")]
            public double Sum { get; set; }

            [JsonProperty("average")]
            public double Average { get; set; }
        }
    }
}
