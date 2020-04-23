//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Cosmos;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosContainerTests
    {
        private CosmosClient cosmosClient = null;
        private CosmosDatabase cosmosDatabase = null;
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
            CosmosContainerProperties cp = new CosmosContainerProperties()
            {
                Id = "ReIndexContainer",
                PartitionKeyPath = "/pk",
                IndexingPolicy = new IndexingPolicy()
                {
                    Automatic = false,
                }
            };

            ContainerResponse response = await this.cosmosDatabase.CreateContainerAsync(cp);
            CosmosContainer container = response;
            CosmosContainerProperties existingCosmosContainerProperties = response.Value;

            // Turn on indexing
            existingCosmosContainerProperties.IndexingPolicy.Automatic = true;
            existingCosmosContainerProperties.IndexingPolicy.IndexingMode = IndexingMode.Consistent;

            await container.ReplaceContainerAsync(existingCosmosContainerProperties);

            // Check progress
            ContainerRequestOptions requestOptions = new ContainerRequestOptions();
            requestOptions.PopulateQuotaInfo = true;

            while(true)
            {
                ContainerResponse readResponse = await container.ReadContainerAsync(requestOptions);
                Assert.IsTrue(readResponse.GetRawResponse().Headers.TryGetValue("x-ms-documentdb-collection-index-transformation-progress", out string indexTransformationStatus));
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

        //[TestMethod]
        //public async Task ContainerBuilderContractTest()
        //{
        //    ContainerResponse response = await this.cosmosDatabase.DefineContainer(new Guid().ToString(), "/id").CreateAsync();
        //    this.ValidateCreateContainerResponseContract(response);

        //    response = await this.cosmosDatabase.DefineContainer(new Guid().ToString(), "/id").CreateIfNotExistsAsync();
        //    this.ValidateCreateContainerResponseContract(response);

        //    response = await this.cosmosDatabase.DefineContainer(response.Container.Id, "/id").CreateIfNotExistsAsync();
        //    this.ValidateCreateContainerResponseContract(response);
        //}

        [TestMethod]
        public async Task PartitionedCRUDTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerName, partitionKeyPath);

            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());
            //Assert.IsNotNull(containerResponse.Diagnostics);
            //string diagnostics = containerResponse.Diagnostics.ToString();
            //Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            //Assert.IsTrue(diagnostics.Contains("StatusCode"));

            CosmosContainerProperties settings = new CosmosContainerProperties(containerName, partitionKeyPath)
            {
                IndexingPolicy = new IndexingPolicy()
                {
                    IndexingMode = IndexingMode.None,
                    Automatic = false
                }
            };

            CosmosContainer container = containerResponse;
            containerResponse = await container.ReplaceContainerAsync(settings);
            Assert.AreEqual((int)HttpStatusCode.OK, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());
            Assert.AreEqual(IndexingMode.None, containerResponse.Value.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Value.IndexingPolicy.Automatic);
            //Assert.IsNotNull(containerResponse.Diagnostics);
            //diagnostics = containerResponse.Diagnostics.ToString();
            //Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            //Assert.IsTrue(diagnostics.Contains("StatusCode"));

            containerResponse = await container.ReadContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.OK, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            //Assert.AreEqual(Cosmos.PartitionKeyDefinitionVersion.V2, containerResponse.Resource.PartitionKeyDefinitionVersion);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());
            Assert.AreEqual(IndexingMode.None, containerResponse.Value.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Value.IndexingPolicy.Automatic);
            //Assert.IsNotNull(containerResponse.Diagnostics);
            //diagnostics = containerResponse.Diagnostics.ToString();
            //Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            //Assert.IsTrue(diagnostics.Contains("StatusCode"));

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task CreateHashV1Container()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerProperties settings = new CosmosContainerProperties(containerName, partitionKeyPath);
            settings.PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V1;

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(settings);

            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);

            Assert.AreEqual(PartitionKeyDefinitionVersion.V1, containerResponse.Value.PartitionKeyDefinitionVersion);
        }

        [TestMethod]
        public async Task PartitionedCreateWithPathDelete()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            Microsoft.Azure.Documents.PartitionKeyDefinition partitionKeyDefinition = new Microsoft.Azure.Documents.PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add(partitionKeyPath);

            CosmosContainerProperties settings = new CosmosContainerProperties(containerName, partitionKeyDefinition);
            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(settings);

            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task CreateContainerIfNotExistsAsyncTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath1 = "/users";

            CosmosContainerProperties settings = new CosmosContainerProperties(containerName, partitionKeyPath1);
            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(settings);

            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath1, containerResponse.Value.PartitionKey.Paths.First());

            //Creating container with same partition key path
            containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(settings);

            Assert.AreEqual((int)HttpStatusCode.OK, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath1, containerResponse.Value.PartitionKey.Paths.First());

            //Creating container with different partition key path
            string partitionKeyPath2 = "/users2";
            try
            {
                settings = new CosmosContainerProperties(containerName, partitionKeyPath2);
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
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);


            //Creating existing container with partition key having value for SystemKey
            //https://github.com/Azure/azure-cosmos-dotnet-v3/issues/623
            string v2ContainerName = "V2Container";
            Microsoft.Azure.Documents.PartitionKeyDefinition partitionKeyDefinition = new Microsoft.Azure.Documents.PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("/test");
            partitionKeyDefinition.IsSystemKey = false;
            CosmosContainerProperties containerPropertiesWithSystemKey = new CosmosContainerProperties()
            {
                Id = v2ContainerName,
                PartitionKey = partitionKeyDefinition,
            };
            await this.cosmosDatabase.CreateContainerAsync(containerPropertiesWithSystemKey);

            CosmosContainerProperties containerProperties = new CosmosContainerProperties(v2ContainerName, "/test");
            containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerProperties);
            Assert.AreEqual((int)HttpStatusCode.OK, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(v2ContainerName, containerResponse.Value.Id);
            Assert.AreEqual("/test", containerResponse.Value.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);

            containerPropertiesWithSystemKey.PartitionKey.IsSystemKey = true;
            await this.cosmosDatabase.CreateContainerAsync(containerPropertiesWithSystemKey);

            containerProperties = new CosmosContainerProperties(v2ContainerName, "/test");
            containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerProperties);
            Assert.AreEqual((int)HttpStatusCode.OK, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(v2ContainerName, containerResponse.Value.Id);
            Assert.AreEqual("/test", containerResponse.Value.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task StreamPartitionedCreateWithPathDelete()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            Microsoft.Azure.Documents.PartitionKeyDefinition partitionKeyDefinition = new Microsoft.Azure.Documents.PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add(partitionKeyPath);

            CosmosContainerProperties settings = new CosmosContainerProperties(containerName, partitionKeyDefinition);
            using (Response containerResponse = await this.cosmosDatabase.CreateContainerStreamAsync(settings))
            {
                Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.Status);
            }

            using (Response containerResponse = await this.cosmosDatabase.GetContainer(containerName).DeleteContainerStreamAsync())
            {
                Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.Status);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(CosmosException))]
        public async Task NegativePartitionedCreateDelete()
        {
            string containerName = Guid.NewGuid().ToString();

            Microsoft.Azure.Documents.PartitionKeyDefinition partitionKeyDefinition = new Microsoft.Azure.Documents.PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("/users");
            partitionKeyDefinition.Paths.Add("/test");

            CosmosContainerProperties settings = new CosmosContainerProperties(containerName, partitionKeyDefinition);
            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(settings);

            Assert.Fail("Multiple partition keys should have caused an exception.");
        }

        [TestMethod]
        public async Task NoPartitionedCreateFail()
        {
            string containerName = Guid.NewGuid().ToString();
            try
            {
                new CosmosContainerProperties(id: containerName, partitionKeyPath: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                new CosmosContainerProperties(id: containerName, partitionKeyDefinition: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            CosmosContainerProperties settings = new CosmosContainerProperties() { Id = containerName };
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
            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());

            containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual((int)HttpStatusCode.OK, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task IteratorTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerName, partitionKeyPath);
            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());

            HashSet<string> containerIds = new HashSet<string>();
            await foreach (CosmosContainerProperties setting in this.cosmosDatabase.GetContainerQueryIterator<CosmosContainerProperties>())
            {
                if (!containerIds.Contains(setting.Id))
                {
                    containerIds.Add(setting.Id);
                }
            }

            Assert.IsTrue(containerIds.Count > 0, "The iterator did not find any containers.");
            Assert.IsTrue(containerIds.Contains(containerName), "The iterator did not find the created container");

            await foreach (Page<CosmosContainerProperties> page in this.cosmosDatabase.GetContainerQueryIterator<CosmosContainerProperties>($"select * from c where c.id = \"{containerName}\"").AsPages())
            {
                Assert.AreEqual(1, page.Values.Count);
                Assert.AreEqual(containerName, page.Values.First().Id);
            }

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task StreamIteratorTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerName, partitionKeyPath);
            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());

            containerName = Guid.NewGuid().ToString();
            containerResponse = await this.cosmosDatabase.CreateContainerAsync(containerName, partitionKeyPath);
            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            Assert.AreEqual(containerName, containerResponse.Value.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Value.PartitionKey.Paths.First());

            HashSet<string> containerIds = new HashSet<string>();
            await foreach(Response message in this.cosmosDatabase.GetContainerQueryStreamIterator(
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 1 }))
             {
                Assert.AreEqual((int)HttpStatusCode.OK, message.Status);
                CosmosTextJsonSerializer defaultJsonSerializer = CosmosTextJsonSerializer.CreateUserDefaultSerializer();
                Dictionary<string, JsonElement> containers = defaultJsonSerializer.FromStream<Dictionary<string, JsonElement>>(message.ContentStream);
                foreach (JsonElement container in containers["DocumentCollections"].EnumerateArray())
                {
                    string id = container.GetProperty("id").GetString();
                    containerIds.Add(id);
                }
            }

            Assert.IsTrue(containerIds.Count > 0, "The iterator did not find any containers.");
            Assert.IsTrue(containerIds.Contains(containerName), "The iterator did not find the created container");

            containerResponse = await containerResponse.Container.DeleteContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task DeleteNonExistingContainer()
        {
            string containerName = Guid.NewGuid().ToString();
            CosmosContainer container = this.cosmosDatabase.GetContainer(containerName);

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
            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            CosmosContainer container = this.cosmosDatabase.GetContainer(containerName);

            int? readThroughput = await container.ReadThroughputAsync();
            Assert.IsNotNull(readThroughput);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task ReplaceThroughputTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);
            CosmosContainer container = this.cosmosDatabase.GetContainer(containerName);

            int? readThroughput = await container.ReadThroughputAsync();
            Assert.IsNotNull(readThroughput);

            await container.ReplaceThroughputAsync(readThroughput.Value + 1000);
            int? replaceThroughput = await ((ContainerCore)container).ReadThroughputAsync();
            Assert.IsNotNull(replaceThroughput);
            Assert.AreEqual(readThroughput.Value + 1000, replaceThroughput);

            containerResponse = await container.DeleteContainerAsync();
            Assert.AreEqual((int)HttpStatusCode.NoContent, containerResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task ReadReplaceThroughputResponseTests()
        {
            int toStreamCount = 0;
            int fromStreamCount = 0;

            CosmosSerializerHelper mockJsonSerializer = new CosmosSerializerHelper(
                null,
                (x) => fromStreamCount++,
                (x) => toStreamCount++);

            //Create a new cosmos client with the mocked cosmos json serializer
            CosmosClient client = TestCommon.CreateCosmosClient(new CosmosClientOptions() { Serializer = mockJsonSerializer });

            int databaseThroughput = 10000;
            CosmosDatabase databaseNoThroughput = await client.CreateDatabaseAsync(Guid.NewGuid().ToString(), null);
            CosmosDatabase databaseWithThroughput = await client.CreateDatabaseAsync(Guid.NewGuid().ToString(), databaseThroughput, null);


            string containerId = Guid.NewGuid().ToString();
            string partitionPath = "/users";
            CosmosContainer containerNoThroughput = await databaseWithThroughput.CreateContainerAsync(containerId, partitionPath, throughput: null);
            try
            {
                await containerNoThroughput.ReadThroughputAsync(new RequestOptions());
                Assert.Fail("Should through not found exception as throughput is not configured");
            }
            catch (CosmosException exception)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
            }

            try
            {
                await containerNoThroughput.ReplaceThroughputAsync(2000, new RequestOptions());
                Assert.Fail("Should through not found exception as throughput is not configured");
            }
            catch (CosmosException exception)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
            }

            int containerThroughput = 1000;
            CosmosContainer container = await databaseNoThroughput.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", throughput: containerThroughput);

            int? containerResponseThroughput = await container.ReadThroughputAsync();
            Assert.AreEqual(containerThroughput, containerResponseThroughput);

            ThroughputResponse containerThroughputResponse = await container.ReadThroughputAsync(new RequestOptions());
            Assert.IsNotNull(containerThroughputResponse);
            Assert.IsNotNull(containerThroughputResponse.Value);
            Assert.IsNotNull(containerThroughputResponse.MinThroughput);
            Assert.IsNotNull(containerThroughputResponse.Value.Throughput);
            Assert.AreEqual(containerThroughput, containerThroughputResponse.Value.Throughput.Value);

            containerThroughput += 500;
            containerThroughputResponse = await container.ReplaceThroughputAsync(containerThroughput, new RequestOptions());
            Assert.IsNotNull(containerThroughputResponse);
            Assert.IsNotNull(containerThroughputResponse.Value);
            Assert.IsNotNull(containerThroughputResponse.Value.Throughput);
            Assert.AreEqual(containerThroughput, containerThroughputResponse.Value.Throughput.Value);

            Assert.AreEqual(0, toStreamCount, "Custom serializer to stream should not be used for offer operations");
            Assert.AreEqual(0, fromStreamCount, "Custom serializer from stream should not be used for offer operations");
            await databaseNoThroughput.DeleteAsync();
        }

        [TestMethod]
        public async Task ThroughputNonExistingResourceTest()
        {
            string containerName = Guid.NewGuid().ToString();
            CosmosContainer container = this.cosmosDatabase.GetContainer(containerName);

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
            CosmosContainer container = containerResponse;
            CosmosContainerProperties containerSettings = containerResponse;
            Assert.IsNotNull(container);
            Assert.IsNotNull(containerSettings);

            containerResponse = await container.DeleteContainerAsync();
            container = containerResponse;
            containerSettings = containerResponse;
            Assert.IsNotNull(container);
            Assert.IsNull(containerSettings);
        }

        private void ValidateCreateContainerResponseContract(ContainerResponse containerResponse)
        {
            Assert.IsNotNull(containerResponse);
            ResponseHeaders headers = containerResponse.GetRawResponse().Headers;
            Assert.IsNotNull(headers);
            Assert.IsTrue(headers.TryGetValue(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.RequestCharge, out string requestCharge));
            Assert.IsNotNull(requestCharge);
            Assert.IsTrue(double.Parse(requestCharge, CultureInfo.InvariantCulture) > 0);
            Assert.IsTrue(headers.TryGetValue(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.ActivityId, out string activityId));
            Assert.IsNotNull(activityId);

            CosmosContainerProperties containerSettings = containerResponse.Value;
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
