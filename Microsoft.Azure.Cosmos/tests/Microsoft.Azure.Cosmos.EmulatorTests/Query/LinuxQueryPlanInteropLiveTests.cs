//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using LegacyQueryTests = Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryTests;
    using TestCommon = Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestCommon;

    [TestClass]
    [TestCategory("Query")]
    [DoNotParallelize]
    public sealed class LinuxQueryPlanInteropLiveTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert.Inconclusive("Requires Linux.");
            }

            string runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
            if (runtimeIdentifier.StartsWith("linux-musl-", StringComparison.Ordinal)
                || runtimeIdentifier.StartsWith("alpine", StringComparison.Ordinal))
            {
                Assert.Inconclusive("Requires glibc Linux. Alpine/musl is not supported by QueryPlanInterop.");
            }
        }

        [TestMethod]
        [TestCategory("LinuxQueryPlanInteropLive")]
        public async Task TestQueryUnicodeDocumentHttpsGateway()
        {
            // Reuse the assertions without QueryTests' account-wide setup/cleanup.
            await LegacyQueryTests.TestQueryUnicodeDocument(useGateway: true, protocol: Protocol.Https);
        }

        [TestMethod]
        [TestCategory("LinuxQueryPlanInteropLive")]
        public async Task CityCaseFoldingMatchesGatewayAsync()
        {
            if (!ConfigurationManager.IsQueryInteropEnabled())
            {
                Assert.Inconclusive("Requires Linux with AZURE_COSMOS_USE_QUERY_PLAN_INTEROP_LINUX=true.");
            }

            Assert.IsFalse(QueryPlanRetriever.BypassQueryParsing(), "Native planning must be available and enabled.");
            string originalBypass = Environment.GetEnvironmentVariable(ConfigurationManager.BypassQueryParsing);
            using CosmosClient admin = TestCommon.CreateCosmosClient(true);
            Database database = null;
            try
            {
                database = await admin.CreateDatabaseAsync("qpi-locale-" + Guid.NewGuid().ToString("N"));
                Container container = await database.CreateContainerAsync("cities", "/city", throughput: 400);
                string[] cities = { "\u0130stanbul", "istanbul", "\u0131stanbul", "Istanbul", "i\u0307stanbul" };
                string sessionToken = null;
                for (int i = 0; i < cities.Length; i++)
                {
                    ItemResponse<object> response = await container.CreateItemAsync<object>(
                        new { id = i.ToString(CultureInfo.InvariantCulture), city = cities[i] },
                        new PartitionKey(cities[i]));
                    sessionToken = response.Headers.Session;
                }

                (string Expression, string Expected)[] scenarios =
                {
                    ("LOWER('\u0130stanbul')", "\u0130stanbul"),
                    ("LOWER('Istanbul')", "istanbul"),
                    ("LOWER('\u0131stanbul')", "\u0131stanbul"),
                    ("'\u0130stanbul'", "\u0130stanbul"),
                    ("'istanbul'", "istanbul"),
                    ("'\u0131stanbul'", "\u0131stanbul"),
                };
                foreach (bool gateway in new[] { true, false })
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BypassQueryParsing, gateway ? "true" : "false");
                    Assert.AreEqual(gateway, QueryPlanRetriever.BypassQueryParsing());
                    using CosmosClient client = TestCommon.CreateCosmosClient(true);
                    Container queryContainer = client.GetContainer(database.Id, "cities");
                    foreach ((string expression, string expected) in scenarios)
                    {
                        List<string> actual = new List<string>();
                        StringBuilder diagnostics = new StringBuilder();
                        using FeedIterator<JObject> iterator = queryContainer.GetItemQueryIterator<JObject>(
                            $"SELECT * FROM c WHERE c.city = {expression}",
                            requestOptions: new QueryRequestOptions
                            {
                                EnableOptimisticDirectExecution = false,
                                MaxItemCount = 1,
                                SessionToken = sessionToken,
                            });
                        while (iterator.HasMoreResults)
                        {
                            FeedResponse<JObject> page = await iterator.ReadNextAsync();
                            actual.AddRange(page.Select(document => (string)document["city"]));
                            diagnostics.Append(page.Diagnostics);
                        }

                        CollectionAssert.AreEqual(new[] { expected }, actual, $"Gateway={gateway}, expression={expression}");
                        StringAssert.Contains(diagnostics.ToString(), gateway ? "Gateway QueryPlan" : "Service Interop Query Plan");
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BypassQueryParsing, originalBypass);
                if (database != null)
                {
                    await database.DeleteAsync();
                }
            }
        }
    }
}
