namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public class CosmosMultiRegionDiagnosticsTests
    {
        CosmosClient client;
        Database database;
        Container container;

        string dbName;
        string containerName;

        [TestInitialize]
        public async Task TestInitialize()
        {
            string connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_MULTI_REGION", null);
            this.client = new CosmosClient(connectionString);

            this.dbName = Guid.NewGuid().ToString();
            this.database = await this.client.CreateDatabaseIfNotExistsAsync(this.dbName);

            this.containerName = Guid.NewGuid().ToString();
            this.container = await this.database.CreateContainerIfNotExistsAsync(this.containerName, "/pk");

            await this.container.CreateItemAsync(new ToDoActivity() { id = "1", pk = "1" });
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.database != null)
            {
                await this.database.DeleteAsync();
            }

            this.client.Dispose();
        }


        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task ExlcudeRegionDiagnosticsTest()
        {
            ItemResponse<ToDoActivity> itemResponse = await this.container.ReadItemAsync<ToDoActivity>(
                "1", new Cosmos.PartitionKey("1"),
                new ItemRequestOptions()
                {
                    ExcludeRegions = new List<string>() { "North Central US", "East US" }
                });

            List<string> excludeRegionsList;
            CosmosTraceDiagnostics traceDiagnostic = itemResponse.Diagnostics as CosmosTraceDiagnostics;
            traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionObject);
            excludeRegionsList = excludeRegionObject as List<string>;
            Assert.IsTrue(excludeRegionsList.Contains("North Central US"));
            Assert.IsTrue(excludeRegionsList.Contains("East US"));
        }
    }
}
