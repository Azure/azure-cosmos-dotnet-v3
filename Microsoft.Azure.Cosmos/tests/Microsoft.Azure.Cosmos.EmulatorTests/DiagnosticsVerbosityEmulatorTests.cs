//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class DiagnosticsVerbosityEmulatorTests
    {
        private Container Container = null;
        private Cosmos.Database Database = null;
        private CosmosClient Client = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                DiagnosticsVerbosity = DiagnosticsVerbosity.Summary,
                MaxDiagnosticsSummarySizeBytes = 8192,
                ConsistencyLevel = Cosmos.ConsistencyLevel.Session
            };
            this.Client = TestCommon.CreateCosmosClient(clientOptions);
            this.Database = (await this.Client.CreateDatabaseAsync(Guid.NewGuid().ToString())).Database;
            this.Container = (await this.Database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk")).Container;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await this.Database.DeleteAsync();
            this.Client.Dispose();
        }

        [TestMethod]
        public async Task CreateItem_SummaryMode_ProducesValidJson()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync(
                testItem, new Cosmos.PartitionKey(testItem.pk));

            string summary = response.Diagnostics.ToString(DiagnosticsVerbosity.Summary);
            Assert.IsNotNull(summary);

            JObject parsed = JObject.Parse(summary);
            JObject summaryObj = (JObject)parsed["Summary"];
            Assert.IsNotNull(summaryObj, "Summary object should exist");
            Assert.AreEqual("Summary", summaryObj["DiagnosticsVerbosity"].ToString());
            Assert.IsTrue(summaryObj["TotalRequestCount"].Value<int>() >= 1);
            Assert.IsNotNull(summaryObj["RegionsSummary"]);
        }

        [TestMethod]
        public async Task ReadItem_SummaryMode_ContainsRegionInfo()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            await this.Container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));

            ItemResponse<ToDoActivity> response = await this.Container.ReadItemAsync<ToDoActivity>(
                testItem.id, new Cosmos.PartitionKey(testItem.pk));

            string summary = response.Diagnostics.ToString(DiagnosticsVerbosity.Summary);
            JObject parsed = JObject.Parse(summary);
            JArray regions = (JArray)parsed["Summary"]["RegionsSummary"];

            Assert.IsTrue(regions.Count >= 1, "Should have at least one region");
            JObject firstRegion = (JObject)regions[0];
            Assert.IsNotNull(firstRegion["Region"]);
            Assert.IsNotNull(firstRegion["First"]);
            Assert.IsTrue(firstRegion["RequestCount"].Value<int>() >= 1);
        }

        [TestMethod]
        public async Task SummaryMode_SmallerThanDetailed()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync(
                testItem, new Cosmos.PartitionKey(testItem.pk));

            string detailed = response.Diagnostics.ToString();
            string summary = response.Diagnostics.ToString(DiagnosticsVerbosity.Summary);

            int detailedBytes = Encoding.UTF8.GetByteCount(detailed);
            int summaryBytes = Encoding.UTF8.GetByteCount(summary);

            Assert.IsTrue(summaryBytes <= detailedBytes,
                $"Summary ({summaryBytes} bytes) should be <= Detailed ({detailedBytes} bytes)");
        }

        [TestMethod]
        public async Task ParameterlessToString_UnchangedBySummaryOption()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync(
                testItem, new Cosmos.PartitionKey(testItem.pk));

            string parameterless = response.Diagnostics.ToString();
            string explicitDetailed = response.Diagnostics.ToString(DiagnosticsVerbosity.Detailed);

            JObject parsedDefault = JObject.Parse(parameterless);
            JObject parsedExplicit = JObject.Parse(explicitDetailed);

            // Both should have the trace name (detailed format), not a Summary wrapper
            Assert.IsNotNull(parsedDefault["name"], "Parameterless ToString should have trace name");
            Assert.AreEqual(parsedDefault["name"].ToString(), parsedExplicit["name"].ToString());
        }

        [TestMethod]
        public async Task SummaryCaching_ReturnsSameInstance()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync(
                testItem, new Cosmos.PartitionKey(testItem.pk));

            string summary1 = response.Diagnostics.ToString(DiagnosticsVerbosity.Summary);
            string summary2 = response.Diagnostics.ToString(DiagnosticsVerbosity.Summary);

            Assert.AreSame(summary1, summary2, "Summary should be cached via Lazy<string>");
        }

        [TestMethod]
        public async Task Query_SummaryMode_ProducesValidJson()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            await this.Container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));

            FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
                $"select * from c where c.id = '{testItem.id}'");

            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> feedResponse = await feedIterator.ReadNextAsync();
                string summary = feedResponse.Diagnostics.ToString(DiagnosticsVerbosity.Summary);
                Assert.IsNotNull(summary);

                JObject parsed = JObject.Parse(summary);
                Assert.IsNotNull(parsed["Summary"]);
                Assert.AreEqual("Summary", parsed["Summary"]["DiagnosticsVerbosity"].ToString());
            }
        }

        [TestMethod]
        public async Task ReplaceItem_SummaryMode_ProducesValidJson()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            await this.Container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));

            testItem.cost = 9999;
            ItemResponse<ToDoActivity> response = await this.Container.ReplaceItemAsync(
                testItem, testItem.id, new Cosmos.PartitionKey(testItem.pk));

            string summary = response.Diagnostics.ToString(DiagnosticsVerbosity.Summary);
            JObject parsed = JObject.Parse(summary);
            Assert.IsNotNull(parsed["Summary"]);
            Assert.IsTrue(parsed["Summary"]["TotalRequestCount"].Value<int>() >= 1);
        }

        [TestMethod]
        public async Task DeleteItem_SummaryMode_ProducesValidJson()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            await this.Container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));

            ItemResponse<ToDoActivity> response = await this.Container.DeleteItemAsync<ToDoActivity>(
                testItem.id, new Cosmos.PartitionKey(testItem.pk));

            string summary = response.Diagnostics.ToString(DiagnosticsVerbosity.Summary);
            JObject parsed = JObject.Parse(summary);
            Assert.IsNotNull(parsed["Summary"]);
            Assert.AreEqual("Summary", parsed["Summary"]["DiagnosticsVerbosity"].ToString());
        }
    }
}
