//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class MaterializedViewTests
    {
        private CosmosClient cosmosClient;
        private Database database;

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();
            await Util.DeleteAllDatabasesAsync(this.cosmosClient);
            this.database = await this.cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.database != null)
            {
                await this.database.DeleteStreamAsync();
            }

            this.cosmosClient?.Dispose();
        }

        [TestMethod]
        public async Task ReadContainerPropertiesIncludesMaterializedViewMetadata()
        {
            string sourceContainerId = $"source-{Guid.NewGuid()}";
            string materializedViewContainerId = $"view-{Guid.NewGuid()}";

            ContainerProperties sourceContainerProperties = new ContainerProperties(sourceContainerId, "/pk");
            // The emulator preview contract requires the legacy source opt-in field.
            JsonConvert.PopulateObject(
                @"{""allowMaterializedViews"":true}",
                sourceContainerProperties);

            ContainerResponse sourceCreateResponse = await this.database.CreateContainerAsync(
                sourceContainerProperties);
            Assert.AreEqual(HttpStatusCode.Created, sourceCreateResponse.StatusCode);

            ContainerProperties materializedViewProperties = new ContainerProperties(
                materializedViewContainerId,
                "/pk")
            {
                MaterializedViewDefinition = new MaterializedViewDefinition
                {
                    SourceContainerResourceId = sourceCreateResponse.Resource.ResourceId,
                    SourceContainerId = sourceContainerId,
                    Definition = "SELECT * FROM c",
                },
            };

            ContainerResponse materializedViewCreateResponse = await this.database.CreateContainerAsync(
                materializedViewProperties,
                ThroughputProperties.CreateAutoscaleThroughput(5000));
            Assert.AreEqual(HttpStatusCode.Created, materializedViewCreateResponse.StatusCode);

            ContainerResponse sourceReadResponse = await sourceCreateResponse.Container.ReadContainerAsync();
            Assert.IsNotNull(sourceReadResponse.Resource.MaterializedViews);
            Assert.AreEqual(1, sourceReadResponse.Resource.MaterializedViews.Count);
            MaterializedViewProperties sourceMetadata = sourceReadResponse.Resource.MaterializedViews[0];
            Assert.AreEqual(materializedViewContainerId, sourceMetadata.Id);
            Assert.AreEqual(materializedViewCreateResponse.Resource.ResourceId, sourceMetadata.ResourceId);

            ContainerResponse materializedViewReadResponse =
                await materializedViewCreateResponse.Container.ReadContainerAsync();
            Assert.IsNotNull(materializedViewReadResponse.Resource.MaterializedViewDefinition);
            MaterializedViewDefinition definition =
                materializedViewReadResponse.Resource.MaterializedViewDefinition;
            Assert.AreEqual(sourceCreateResponse.Resource.ResourceId, definition.SourceContainerResourceId);
            Assert.AreEqual(sourceContainerId, definition.SourceContainerId);
            Assert.AreEqual("SELECT * FROM c", definition.Definition);
        }
    }
}
