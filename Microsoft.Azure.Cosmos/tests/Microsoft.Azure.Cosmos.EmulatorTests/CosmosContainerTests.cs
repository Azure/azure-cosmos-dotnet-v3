//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosContainerTests
    {
        private CosmosClient cosmosClient = null;
        private CosmosDatabase cosmosDatabase = null;

        [TestInitialize]
        public async Task TestInit()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();

            string databaseName = Guid.NewGuid().ToString();
            CosmosDatabaseResponse cosmosDatabaseResponse = await this.cosmosClient.Databases.CreateDatabaseIfNotExistsAsync(databaseName);
            this.cosmosDatabase = cosmosDatabaseResponse;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            if (this.cosmosDatabase != null)
            {
                await this.cosmosDatabase.DeleteAsync();
            }
            this.cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task PartitionedCRUDTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(containerName, partitionKeyPath);

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            CosmosContainerSettings settings = new CosmosContainerSettings(containerName, partitionKeyPath)
            {
                IndexingPolicy = new Cosmos.IndexingPolicy()
                {
                    IndexingMode = Cosmos.IndexingMode.None,
                    Automatic = false
                }
            };

            CosmosContainer cosmosContainer = containerResponse;
            containerResponse = await cosmosContainer.ReplaceAsync(settings);
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Assert.AreEqual(Cosmos.IndexingMode.None, containerResponse.Resource.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Resource.IndexingPolicy.Automatic);

            containerResponse = await cosmosContainer.ReadAsync();
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Assert.AreEqual(Cosmos.IndexingMode.None, containerResponse.Resource.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Resource.IndexingPolicy.Automatic);

            containerResponse = await containerResponse.Container.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task PartitionedCreateWithPathDelete()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add(partitionKeyPath);

            CosmosContainerSettings settings = new CosmosContainerSettings(containerName, partitionKeyDefinition);
            CosmosContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(settings);

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task StreamPartitionedCreateWithPathDelete()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add(partitionKeyPath);

            CosmosContainerSettings settings = new CosmosContainerSettings(containerName, partitionKeyDefinition);
            using (CosmosResponseMessage containerResponse = await this.cosmosDatabase.Containers.CreateContainerStreamAsync(CosmosResource.ToStream(settings)))
            {
                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            }

            using (CosmosResponseMessage containerResponse = await this.cosmosDatabase.Containers[containerName].DeleteStreamAsync())
            {
                Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(CosmosException))]
        public async Task NegativePartitionedCreateDelete()
        {
            string containerName = Guid.NewGuid().ToString();

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("/users");
            partitionKeyDefinition.Paths.Add("/test");

            CosmosContainerSettings settings = new CosmosContainerSettings(containerName, partitionKeyDefinition);
            CosmosContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(settings);

            Assert.Fail("Multiple partition keys should have caused an exception.");
        }

        [TestMethod]
        public async Task NoPartitionedCreateFail()
        {
            string containerName = Guid.NewGuid().ToString();
            try
            {
                new CosmosContainerSettings(id: containerName, partitionKeyPath: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                new CosmosContainerSettings(id: containerName, partitionKeyDefinition: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            CosmosContainerSettings settings = new CosmosContainerSettings() { Id = containerName };
            try
            {
                CosmosContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(settings);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                CosmosContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(settings);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                CosmosContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(id: containerName, partitionKeyPath: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                CosmosContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(id: containerName, partitionKeyPath: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }
        }

        [TestMethod]
        public async Task PartitionedCreateDeleteIfNotExists()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task IteratorTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            HashSet<string> containerIds = new HashSet<string>();
            CosmosResultSetIterator<CosmosContainerSettings> resultSet = this.cosmosDatabase.Containers.GetContainerIterator();
            while (resultSet.HasMoreResults)
            {
                foreach (CosmosContainerSettings setting in await resultSet.FetchNextSetAsync())
                {
                    if (!containerIds.Contains(setting.Id))
                    {
                        containerIds.Add(setting.Id);
                    }
                }
            }

            Assert.IsTrue(containerIds.Count > 0, "The iterator did not find any containers.");
            Assert.IsTrue(containerIds.Contains(containerName), "The iterator did not find the created container");

            containerResponse = await containerResponse.Container.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task StreamIteratorTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            HashSet<string> containerIds = new HashSet<string>();
            CosmosFeedResultSetIterator resultSet = this.cosmosDatabase.Containers.GetContainerStreamIterator();
            while (resultSet.HasMoreResults)
            {
                using (CosmosResponseMessage message = await resultSet.FetchNextSetAsync())
                {
                    Assert.AreEqual(HttpStatusCode.OK, message.StatusCode);
                    CosmosDefaultJsonSerializer defaultJsonSerializer = new CosmosDefaultJsonSerializer();
                    dynamic containers = defaultJsonSerializer.FromStream<dynamic>(message.Content).DocumentCollections;
                    foreach (dynamic container in containers)
                    {
                        string id = container.id.ToString();
                        containerIds.Add(id);
                    }
                }
            }

            Assert.IsTrue(containerIds.Count > 0, "The iterator did not find any containers.");
            Assert.IsTrue(containerIds.Contains(containerName), "The iterator did not find the created container");

            containerResponse = await containerResponse.Container.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task DeleteNonExistingContainer()
        {
            string containerName = Guid.NewGuid().ToString();
            CosmosContainer cosmosContainer = this.cosmosDatabase.Containers[containerName];

            CosmosContainerResponse containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NotFound, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task DefaultThroughputTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            CosmosContainer cosmosContainer = this.cosmosDatabase.Containers[containerName];

            int? readThroughput = await cosmosContainer.ReadProvisionedThroughputAsync();
            Assert.IsNotNull(readThroughput);

            containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task TimeToLiveTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";
            TimeSpan timeToLive = TimeSpan.FromSeconds(1);
            CosmosContainerSettings setting = new CosmosContainerSettings()
            {
                Id = containerName,
                PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = PartitionKind.Hash },
                DefaultTimeToLive = timeToLive
            };

            CosmosContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(setting);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            CosmosContainer cosmosContainer = containerResponse;
            CosmosContainerSettings responseSettings = containerResponse;

            Assert.AreEqual(timeToLive.TotalSeconds, responseSettings.DefaultTimeToLive.Value.TotalSeconds);

            CosmosContainerResponse readResponse = await cosmosContainer.ReadAsync();
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(timeToLive.TotalSeconds, readResponse.Resource.DefaultTimeToLive.Value.TotalSeconds);

            JObject itemTest = JObject.FromObject(new { id = Guid.NewGuid().ToString(), users = "testUser42" });
            CosmosItemResponse<JObject> createResponse = await cosmosContainer.Items.CreateItemAsync<JObject>(partitionKey: itemTest["users"].ToString(), item: itemTest);
            JObject responseItem = createResponse;
            Assert.IsNull(responseItem["ttl"]);

            containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task ReplaceThroughputTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            CosmosContainer cosmosContainer = this.cosmosDatabase.Containers[containerName];

            int? readThroughput = await cosmosContainer.ReadProvisionedThroughputAsync();
            Assert.IsNotNull(readThroughput);

            await cosmosContainer.ReplaceProvisionedThroughputAsync(readThroughput.Value + 1000);
            int? replaceThroughput = await cosmosContainer.ReadProvisionedThroughputAsync();
            Assert.IsNotNull(replaceThroughput);
            Assert.AreEqual(readThroughput.Value + 1000, replaceThroughput);

            containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public async Task ThroughputNonExistingTest()
        {
            string containerName = Guid.NewGuid().ToString();
            CosmosContainer cosmosContainer = this.cosmosDatabase.Containers[containerName];

            await cosmosContainer.ReadProvisionedThroughputAsync();

            CosmosContainerResponse containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NotFound, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task ImplicitConversion()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerResponse containerResponse = await this.cosmosDatabase.Containers[containerName].ReadAsync();
            CosmosContainer cosmosContainer = containerResponse;
            CosmosContainerSettings cosmosContainerSettings = containerResponse;

            Assert.AreEqual(HttpStatusCode.NotFound, containerResponse.StatusCode);
            Assert.IsNotNull(cosmosContainer);
            Assert.IsNull(cosmosContainerSettings);

            containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            cosmosContainer = containerResponse;
            cosmosContainerSettings = containerResponse;
            Assert.IsNotNull(cosmosContainer);
            Assert.IsNotNull(cosmosContainerSettings);

            containerResponse = await cosmosContainer.DeleteAsync();
            cosmosContainer = containerResponse;
            cosmosContainerSettings = containerResponse;
            Assert.IsNotNull(cosmosContainer);
            Assert.IsNull(cosmosContainerSettings);
        }

        /// <summary>
        /// This test only verifies that we are able to set the ttl property path correctly using SDK.
        /// For ttl custom property path case, we don't do logical document filtering in backend and since expired
        /// resource purger is not yet enabled in any fabric emulator; not verifying whether documents are getting ttl'ed
        /// correctly or not. There are native tests to verify this functionality.
        /// </summary>
        [TestMethod]
        public async Task TimeToLivePropertyPath()
        {

            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";
            TimeSpan timeToLive = TimeSpan.FromSeconds(1);
            CosmosContainerSettings setting = new CosmosContainerSettings()
            {
                Id = containerName,
                PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = PartitionKind.Hash },
                TimeToLivePropertyPath = "/creationDate"
            };
            CosmosContainerResponse containerResponse = null;
            try
            {
                containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(setting);
                Assert.Fail("CreateColleciton with TtlPropertyPath and with no DefaultTimeToLive should have failed.");
            }
            catch (CosmosException exeption)
            {
                // expected because DefaultTimeToLive was not specified
                Assert.AreEqual(HttpStatusCode.BadRequest, exeption.StatusCode);
            }

            // Verify the collection content.
            setting.DefaultTimeToLive = TimeSpan.FromSeconds(-1);
            containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(setting);
            CosmosContainer cosmosContainer = containerResponse;
            Assert.AreEqual(TimeSpan.FromSeconds(-1), containerResponse.Resource.DefaultTimeToLive);
            Assert.AreEqual("/creationDate", containerResponse.Resource.TimeToLivePropertyPath);

            // verify removing the ttl property path
            setting.TimeToLivePropertyPath = null;
            containerResponse = await cosmosContainer.ReplaceAsync(setting);
            cosmosContainer = containerResponse;
            Assert.AreEqual(TimeSpan.FromSeconds(-1), containerResponse.Resource.DefaultTimeToLive);
            Assert.IsNull(containerResponse.Resource.TimeToLivePropertyPath);

            containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }
    }
}
