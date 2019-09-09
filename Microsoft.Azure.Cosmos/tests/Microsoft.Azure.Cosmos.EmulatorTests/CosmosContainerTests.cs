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
        private Cosmos.Database cosmosDatabase = null;
        private static long ToEpoch(DateTime dateTime) => (long)(dateTime - new DateTime(1970, 1, 1)).TotalSeconds;

        [TestInitialize]
        public async Task TestInit()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();

            string databaseName = Guid.NewGuid().ToString();
            DatabaseResponse cosmosDatabaseResponse = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
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
                await this.cosmosDatabase.DeleteStreamAsync();
            }
            this.cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task ReIndexingTest()
        {
            ContainerProperties cp = new ContainerProperties()
            {
                Id = "ReIndexContainer",
                PartitionKeyPath = "/pk",
                IndexingPolicy = new Cosmos.IndexingPolicy()
                {
                    Automatic = false,
                }
            };

            ContainerResponse response = await this.cosmosDatabase.CreateContainerAsync(cp);
            Container container = response;
            ContainerProperties existingContainerProperties = response.Resource;

            // Turn on indexing
            existingContainerProperties.IndexingPolicy.Automatic = true;
            existingContainerProperties.IndexingPolicy.IndexingMode = Cosmos.IndexingMode.Consistent;

            await container.ReplaceContainerAsync(existingContainerProperties);

            // Check progress
            ContainerRequestOptions requestOptions = new ContainerRequestOptions();
            requestOptions.PopulateQuotaInfo = true;

            while(true)
            {
                ContainerResponse readResponse = await container.ReadContainerAsync(requestOptions);
                string indexTransformationStatus = readResponse.Headers["x-ms-documentdb-collection-index-transformation-progress"];
                Assert.IsNotNull(indexTransformationStatus);

                if (int.Parse(indexTransformationStatus) == 100)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(20));
            }
        }

        [TestMethod]
        public async Task ContainerContractTest()
        {
            ContainerResponse response = await this.cosmosDatabase.CreateContainerAsync(new Guid().ToString(), "/id");
            this.ValidateCreateContainerResponseContract(response);
        }

        [TestMethod]
        public async Task ContainerBuilderContractTest()
        {
            ContainerResponse response = await this.cosmosDatabase.DefineContainer(new Guid().ToString(), "/id").CreateAsync();
            this.ValidateCreateContainerResponseContract(response);

            response = await this.cosmosDatabase.DefineContainer(new Guid().ToString(), "/id").CreateIfNotExistsAsync();
            this.ValidateCreateContainerResponseContract(response);

            response = await this.cosmosDatabase.DefineContainer(response.Container.Id, "/id").CreateIfNotExistsAsync();
            this.ValidateCreateContainerResponseContract(response);
        }

        [Ignore]
        [TestMethod]
        public async Task PartitionedCRUDTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerName, partitionKeyPath);

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            ContainerProperties settings = new ContainerProperties(containerName, partitionKeyPath)
            {
                IndexingPolicy = new Cosmos.IndexingPolicy()
                {
                    IndexingMode = Cosmos.IndexingMode.None,
                    Automatic = false
                }
            };

            Container container = containerResponse;
            containerResponse = await container.ReplaceContainerAsync(settings);
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Assert.AreEqual(Cosmos.IndexingMode.None, containerResponse.Resource.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Resource.IndexingPolicy.Automatic);

            containerResponse = await container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(Cosmos.PartitionKeyDefinitionVersion.V2, containerResponse.Resource.PartitionKeyDefinitionVersion);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Assert.AreEqual(Cosmos.IndexingMode.None, containerResponse.Resource.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Resource.IndexingPolicy.Automatic);

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task CreateHashV1Container()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerProperties settings = new ContainerProperties(containerName, partitionKeyPath);
            settings.PartitionKeyDefinitionVersion = Cosmos.PartitionKeyDefinitionVersion.V1;

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(settings);

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);

            Assert.AreEqual(Cosmos.PartitionKeyDefinitionVersion.V1, containerResponse.Resource.PartitionKeyDefinitionVersion);
        }

        [TestMethod]
        public async Task PartitionedCreateWithPathDelete()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add(partitionKeyPath);

            ContainerProperties settings = new ContainerProperties(containerName, partitionKeyDefinition);
            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(settings);

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task CreateContainerIfNotExistsAsyncTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath1 = "/users";

            ContainerProperties settings = new ContainerProperties(containerName, partitionKeyPath1);
            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(settings);

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath1, containerResponse.Resource.PartitionKey.Paths.First());

            //Creating container with same partition key path
            containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(settings);

            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath1, containerResponse.Resource.PartitionKey.Paths.First());

            //Creating container with different partition key path
            string partitionKeyPath2 = "/users2";
            try
            {
                settings = new ContainerProperties(containerName, partitionKeyPath2);
                containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(settings);
                Assert.Fail("Should through ArgumentException on partition key path");
            }
            catch(ArgumentException ex)
            {
                Assert.AreEqual(nameof(settings.PartitionKey), ex.ParamName);
                Assert.IsTrue(ex.Message.Contains(string.Format(
                    ClientResources.PartitionKeyPathConflict,
                    partitionKeyPath2,
                    containerName,
                    partitionKeyPath1)));
            }

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);


            //Creating existing container with partition key having value for SystemKey
            //https://github.com/Azure/azure-cosmos-dotnet-v3/issues/623
            string v2ContainerName = "V2Container";
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("/test");
            partitionKeyDefinition.IsSystemKey = false;
            ContainerProperties containerPropertiesWithSystemKey = new ContainerProperties()
            {
                Id = v2ContainerName,
                PartitionKey = partitionKeyDefinition,
            };
            await this.cosmosDatabase.CreateContainerAsync(containerPropertiesWithSystemKey);

            ContainerProperties containerProperties = new ContainerProperties(v2ContainerName, "/test");
            containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerProperties);
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(v2ContainerName, containerResponse.Resource.Id);
            Assert.AreEqual("/test", containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);

            containerPropertiesWithSystemKey.PartitionKey.IsSystemKey = true;
            await this.cosmosDatabase.CreateContainerAsync(containerPropertiesWithSystemKey);

            containerProperties = new ContainerProperties(v2ContainerName, "/test");
            containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerProperties);
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(v2ContainerName, containerResponse.Resource.Id);
            Assert.AreEqual("/test", containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task StreamPartitionedCreateWithPathDelete()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add(partitionKeyPath);

            ContainerProperties settings = new ContainerProperties(containerName, partitionKeyDefinition);
            using (ResponseMessage containerResponse = await this.cosmosDatabase.CreateContainerStreamAsync(settings))
            {
                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            }

            using (ResponseMessage containerResponse = await this.cosmosDatabase.GetContainer(containerName).DeleteContainerStreamAsync())
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

            ContainerProperties settings = new ContainerProperties(containerName, partitionKeyDefinition);
            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(settings);

            Assert.Fail("Multiple partition keys should have caused an exception.");
        }

        [TestMethod]
        public async Task NoPartitionedCreateFail()
        {
            string containerName = Guid.NewGuid().ToString();
            try
            {
                new ContainerProperties(id: containerName, partitionKeyPath: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                new ContainerProperties(id: containerName, partitionKeyDefinition: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            ContainerProperties settings = new ContainerProperties() { Id = containerName };
            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(settings);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(settings);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(id: containerName, partitionKeyPath: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(id: containerName, partitionKeyPath: null);
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

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task IteratorTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            HashSet<string> containerIds = new HashSet<string>();
            FeedIterator<ContainerProperties> resultSet = this.cosmosDatabase.GetContainerQueryIterator<ContainerProperties>();
            while (resultSet.HasMoreResults)
            {
                foreach (ContainerProperties setting in await resultSet.ReadNextAsync())
                {
                    if (!containerIds.Contains(setting.Id))
                    {
                        containerIds.Add(setting.Id);
                    }
                }
            }

            Assert.IsTrue(containerIds.Count > 0, "The iterator did not find any containers.");
            Assert.IsTrue(containerIds.Contains(containerName), "The iterator did not find the created container");

            resultSet = this.cosmosDatabase.GetContainerQueryIterator<ContainerProperties>($"select * from c where c.id = \"{containerName}\"");
            FeedResponse<ContainerProperties> queryProperties = await resultSet.ReadNextAsync();

            Assert.AreEqual(1, queryProperties.Resource.Count());
            Assert.AreEqual(containerName, queryProperties.First().Id);

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task StreamIteratorTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerName = Guid.NewGuid().ToString();
            containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            HashSet<string> containerIds = new HashSet<string>();
            FeedIterator resultSet = this.cosmosDatabase.GetContainerQueryStreamIterator(
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 1 });

            while (resultSet.HasMoreResults)
            {
                using (ResponseMessage message = await resultSet.ReadNextAsync())
                {
                    Assert.AreEqual(HttpStatusCode.OK, message.StatusCode);
                    CosmosJsonDotNetSerializer defaultJsonSerializer = new CosmosJsonDotNetSerializer();
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

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task DeleteNonExistingContainer()
        {
            string containerName = Guid.NewGuid().ToString();
            Container container = this.cosmosDatabase.GetContainer(containerName);

            try
            {
                ContainerResponse containerResponse = await container.DeleteContainerAsync();
                Assert.Fail();
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }            
        }

        [TestMethod]
        public async Task DefaultThroughputTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = this.cosmosDatabase.GetContainer(containerName);

            int? readThroughput = await container.ReadThroughputAsync();
            Assert.IsNotNull(readThroughput);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task TimeToLiveTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";
            int timeToLiveInSeconds = 10;
            ContainerProperties setting = new ContainerProperties()
            {
                Id = containerName,
                PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = PartitionKind.Hash },
                DefaultTimeToLive = timeToLiveInSeconds,
            };

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(setting);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = containerResponse;
            ContainerProperties responseSettings = containerResponse;

            Assert.AreEqual(timeToLiveInSeconds, responseSettings.DefaultTimeToLive);

            ContainerResponse readResponse = await container.ReadContainerAsync();
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(timeToLiveInSeconds, readResponse.Resource.DefaultTimeToLive);

            JObject itemTest = JObject.FromObject(new { id = Guid.NewGuid().ToString(), users = "testUser42" });
            ItemResponse<JObject> createResponse = await container.CreateItemAsync<JObject>(item: itemTest);
            JObject responseItem = createResponse;
            Assert.IsNull(responseItem["ttl"]);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task ReplaceThroughputTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = this.cosmosDatabase.GetContainer(containerName);

            int? readThroughput = await container.ReadThroughputAsync();
            Assert.IsNotNull(readThroughput);

            await container.ReplaceThroughputAsync(readThroughput.Value + 1000);
            int? replaceThroughput = await ((ContainerCore)container).ReadThroughputAsync();
            Assert.IsNotNull(replaceThroughput);
            Assert.AreEqual(readThroughput.Value + 1000, replaceThroughput);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task ReadReplaceThroughputReourceTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Container container = this.cosmosDatabase.GetContainer(containerName);

            ThroughputResponse readThroughputResponse = await container.ReadThroughputAsync(new RequestOptions());
            Assert.IsNotNull(readThroughputResponse);
            Assert.IsNotNull(readThroughputResponse.Resource);
            Assert.IsNotNull(readThroughputResponse.MinThroughput);
            Assert.IsNotNull(readThroughputResponse.Resource.Throughput);

            ThroughputResponse replaceThroughputResponse = await container.ReplaceThroughputAsync(readThroughputResponse.Resource.Throughput.Value + 1000);
            Assert.IsNotNull(replaceThroughputResponse);
            Assert.IsNotNull(replaceThroughputResponse.Resource);
            Assert.AreEqual(readThroughputResponse.Resource.Throughput.Value + 1000, replaceThroughputResponse.Resource.Throughput.Value);
            try
            {
                ThroughputResponse nonExistingContainerThroughput = await this.cosmosDatabase
                        .GetContainer("nonExistingContainer")
                        .ReadThroughputAsync(new RequestOptions());
                Assert.Fail("It should throw Resource Not Found exception");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task ThroughputNonExistingTest()
        {
            string containerName = Guid.NewGuid().ToString();
            Container container = this.cosmosDatabase.GetContainer(containerName);

            try
            {
                await container.ReadThroughputAsync();
                Assert.Fail("It should throw Resource Not Found exception");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }
        }

        [TestMethod]
        public async Task ImplicitConversion()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Container container = containerResponse;
            ContainerProperties containerSettings = containerResponse;
            Assert.IsNotNull(container);
            Assert.IsNotNull(containerSettings);

            containerResponse = await container.DeleteContainerAsync();
            container = containerResponse;
            containerSettings = containerResponse;
            Assert.IsNotNull(container);
            Assert.IsNull(containerSettings);
        }

        /// <summary>
        /// This test verifies that we are able to set the ttl property path correctly using SDK.
        /// Also this test will successfully read active item based on its TimeToLivePropertyPath value.
        /// </summary>
        [Obsolete]
        [TestMethod]
        public async Task TimeToLivePropertyPath()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/user";
            int timeToLivetimeToLiveInSeconds = 10;
            ContainerProperties setting = new ContainerProperties()
            {
                Id = containerName,
                PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = PartitionKind.Hash },
                TimeToLivePropertyPath = "/creationDate",
            };

            ContainerResponse containerResponse = null;
            try
            {
                containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(setting);
                Assert.Fail("CreateColleciton with TtlPropertyPath and with no DefaultTimeToLive should have failed.");
            }
            catch (CosmosException exeption)
            {
                // expected because DefaultTimeToLive was not specified
                Assert.AreEqual(HttpStatusCode.BadRequest, exeption.StatusCode);
            }

            // Verify the container content.
            setting.DefaultTimeToLive = timeToLivetimeToLiveInSeconds;
            containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(setting);
            Container container = containerResponse;
            Assert.AreEqual(timeToLivetimeToLiveInSeconds, containerResponse.Resource.DefaultTimeToLive);
            Assert.AreEqual("/creationDate", containerResponse.Resource.TimeToLivePropertyPath);

            //verify removing the ttl property path
            setting.TimeToLivePropertyPath = null;
            containerResponse = await container.ReplaceContainerAsync(setting);
            container = containerResponse;
            Assert.AreEqual(timeToLivetimeToLiveInSeconds, containerResponse.Resource.DefaultTimeToLive);
            Assert.IsNull(containerResponse.Resource.TimeToLivePropertyPath);

            //adding back the ttl property path
            setting.TimeToLivePropertyPath = "/creationDate";
            containerResponse = await container.ReplaceContainerAsync(setting);
            container = containerResponse;
            Assert.AreEqual(containerResponse.Resource.TimeToLivePropertyPath, "/creationDate");

            //Creating an item and reading before expiration
            var payload = new { id = "testId", user = "testUser", creationDate = ToEpoch(DateTime.UtcNow) };
            ItemResponse<dynamic> createItemResponse = await container.CreateItemAsync<dynamic>(payload);
            Assert.IsNotNull(createItemResponse.Resource);
            Assert.AreEqual(createItemResponse.StatusCode, HttpStatusCode.Created);
            ItemResponse<dynamic> readItemResponse = await container.ReadItemAsync<dynamic>(payload.id, new Cosmos.PartitionKey(payload.user));
            Assert.IsNotNull(readItemResponse.Resource);
            Assert.AreEqual(readItemResponse.StatusCode, HttpStatusCode.OK);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        private void ValidateCreateContainerResponseContract(ContainerResponse containerResponse)
        {
            Assert.IsNotNull(containerResponse);
            Assert.IsTrue(containerResponse.RequestCharge > 0);
            Assert.IsNotNull(containerResponse.Headers);
            Assert.IsNotNull(containerResponse.Headers.ActivityId);

            ContainerProperties containerSettings = containerResponse.Resource;
            Assert.IsNotNull(containerSettings.Id);
            Assert.IsNotNull(containerSettings.ResourceId);
            Assert.IsNotNull(containerSettings.ETag);
            Assert.IsTrue(containerSettings.LastModified.HasValue);

            Assert.IsNotNull(containerSettings.PartitionKeyPath);
            Assert.IsNotNull(containerSettings.PartitionKeyPathTokens);
            Assert.AreEqual(1, containerSettings.PartitionKeyPathTokens.Length);
            Assert.AreEqual("id", containerSettings.PartitionKeyPathTokens[0]);

            ContainerCore containerCore = containerResponse.Container as ContainerCore;
            Assert.IsNotNull(containerCore);
            Assert.IsNotNull(containerCore.LinkUri);
            Assert.IsFalse(containerCore.LinkUri.ToString().StartsWith("/"));

            Assert.IsTrue(containerSettings.LastModified.Value > new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), containerSettings.LastModified.Value.ToString());
        }
    }
}
