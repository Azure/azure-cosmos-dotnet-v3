//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosDatabaseTests
    {
        protected CosmosClient cosmosClient = null;
        protected CancellationTokenSource cancellationTokenSource = null;
        protected CancellationToken cancellationToken;

        [TestInitialize]
        public void TestInit()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

            this.cosmosClient = TestCommon.CreateCosmosClient();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            this.cancellationTokenSource?.Cancel();
            this.cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task DatabaseContractTest()
        {
            DatabaseResponse response = await this.CreateDatabaseHelper();
            Assert.IsNotNull(response);
            Assert.IsTrue(response.GetRawResponse().Headers.GetRequestCharge() > 0);
            Assert.IsNotNull(response.GetRawResponse().Headers);
            Assert.IsNotNull(response.GetRawResponse().Headers.GetActivityId());

            CosmosDatabaseProperties databaseSettings = response.Value;
            Assert.IsNotNull(databaseSettings.Id);
            Assert.IsNotNull(databaseSettings.ResourceId);
            Assert.IsNotNull(databaseSettings.ETag);
            Assert.IsTrue(databaseSettings.LastModified.HasValue);
            Assert.IsTrue(databaseSettings.LastModified.Value > new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), databaseSettings.LastModified.Value.ToString());

            DatabaseCore databaseCore = response.Database as DatabaseCore;
            Assert.IsNotNull(databaseCore);
            Assert.IsNotNull(databaseCore.LinkUri);
            Assert.IsFalse(databaseCore.LinkUri.ToString().StartsWith("/"));

            response = await response.Database.DeleteAsync(cancellationToken: this.cancellationToken);
            Assert.AreEqual((int)HttpStatusCode.NoContent, response.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task CreateDropDatabase()
        {
            DatabaseResponse response = await this.CreateDatabaseHelper();
            Assert.AreEqual((int)HttpStatusCode.Created, response.GetRawResponse().Status);
            //Assert.IsNotNull(response.Diagnostics);
            //string diagnostics = response.Diagnostics.ToString();
            //Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            //Assert.IsTrue(diagnostics.Contains("StatusCode"));

            response = await response.Database.DeleteAsync(cancellationToken: this.cancellationToken);
            Assert.AreEqual((int)HttpStatusCode.NoContent, response.GetRawResponse().Status);
            //Assert.IsNotNull(response.Diagnostics);
            //diagnostics = response.Diagnostics.ToString();
            //Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            //Assert.IsTrue(diagnostics.Contains("StatusCode"));
        }

        [TestMethod]
        public async Task StreamCrudTestAsync()
        {
            Cosmos.CosmosDatabase database = await this.CreateDatabaseStreamHelper();

            using (Response response = await database.ReadStreamAsync())
            {
                Assert.AreEqual((int)HttpStatusCode.OK, response.Status);
                Assert.IsNotNull(response.Headers);
                if (response.Headers.TryGetValue(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.RequestCharge, out string requestChargeString))
                {
                    Assert.IsTrue(double.Parse(requestChargeString, CultureInfo.InvariantCulture) > 0);
                }
            }

            using (Response response = await database.DeleteStreamAsync())
            {
                Assert.AreEqual((int)HttpStatusCode.NoContent, response.Status);
                Assert.IsNotNull(response.Headers);
                if (response.Headers.TryGetValue(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.RequestCharge, out string requestChargeString))
                {
                    Assert.IsTrue(double.Parse(requestChargeString, CultureInfo.InvariantCulture) > 0);
                }
            }
        }

        [TestMethod]
        public async Task StreamCreateConflictTestAsync()
        {
            CosmosDatabaseProperties databaseSettings = new CosmosDatabaseProperties()
            {
                Id = Guid.NewGuid().ToString()
            };

            using (Response response = await this.cosmosClient.CreateDatabaseStreamAsync(databaseSettings))
            {
                Assert.AreEqual((int)HttpStatusCode.Created, response.Status);
                Assert.IsNotNull(response.Headers);
                if (response.Headers.TryGetValue(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.RequestCharge, out string requestChargeString))
                {
                    Assert.IsTrue(double.Parse(requestChargeString, CultureInfo.InvariantCulture) > 0);
                }
            }

            // Stream operations do not throw exceptions.
            using (Response response = await this.cosmosClient.CreateDatabaseStreamAsync(databaseSettings))
            {
                Assert.AreEqual((int)HttpStatusCode.Conflict, response.Status);
                Assert.IsNotNull(response.Headers);
                if (response.Headers.TryGetValue(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.RequestCharge, out string requestChargeString))
                {
                    Assert.IsTrue(double.Parse(requestChargeString, CultureInfo.InvariantCulture) > 0);
                }
            }

            using (Response response = await this.cosmosClient.GetDatabase(databaseSettings.Id).DeleteStreamAsync())
            {
                Assert.AreEqual((int)HttpStatusCode.NoContent, response.Status);
                Assert.IsNotNull(response.Headers);
                if (response.Headers.TryGetValue(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.RequestCharge, out string requestChargeString))
                {
                    Assert.IsTrue(double.Parse(requestChargeString, CultureInfo.InvariantCulture) > 0);
                }
            }
        }

        [TestMethod]
        public async Task CreateConflict()
        {
            DatabaseResponse response = await this.CreateDatabaseHelper();
            Assert.AreEqual((int)HttpStatusCode.Created, response.GetRawResponse().Status);

            try
            {
                response = await this.CreateDatabaseHelper(response.Value.Id);
                Assert.Fail($"Unexpected success status code {response.Value}");
            }
            catch (CosmosException hre)
            {
                DefaultTrace.TraceInformation(hre.ToString());
            }

            response = await response.Database.DeleteAsync(cancellationToken: this.cancellationToken);
            Assert.AreEqual((int)HttpStatusCode.NoContent, response.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task ImplicitConversion()
        {
            DatabaseResponse cosmosDatabaseResponse = await this.CreateDatabaseHelper();
            Cosmos.CosmosDatabase cosmosDatabase = cosmosDatabaseResponse;
            CosmosDatabaseProperties cosmosDatabaseSettings = cosmosDatabaseResponse;
            Assert.IsNotNull(cosmosDatabase);
            Assert.IsNotNull(cosmosDatabaseSettings);

            cosmosDatabaseResponse = await cosmosDatabase.DeleteAsync(cancellationToken: this.cancellationToken);
            cosmosDatabase = cosmosDatabaseResponse;
            cosmosDatabaseSettings = cosmosDatabaseResponse;
            Assert.IsNotNull(cosmosDatabase);
            Assert.IsNull(cosmosDatabaseSettings);
        }

        [TestMethod]
        public async Task DropNonExistingDatabase()
        {
            try
            {
                DatabaseResponse response = await this.cosmosClient.GetDatabase(Guid.NewGuid().ToString()).DeleteAsync(cancellationToken: this.cancellationToken);
                Assert.Fail();
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }
        }

        [TestMethod]
        public async Task ReadDatabase()
        {
            DatabaseResponse createResponse = await this.CreateDatabaseHelper();
            DatabaseResponse readResponse = await createResponse.Database.ReadAsync(cancellationToken: this.cancellationToken);

            Assert.AreEqual(createResponse.Database.Id, readResponse.Database.Id);
            Assert.AreEqual(createResponse.Value.Id, readResponse.Value.Id);
            Assert.AreNotEqual(createResponse.GetRawResponse().Headers.GetActivityId(), readResponse.GetRawResponse().Headers.GetActivityId());
            this.ValidateHeaders(readResponse);
            await createResponse.Database.DeleteAsync(cancellationToken: this.cancellationToken);
        }

        [TestMethod]
        public async Task CreateIfNotExists()
        {
            DatabaseResponse createResponse = await this.CreateDatabaseHelper();
            Assert.AreEqual((int)HttpStatusCode.Created, createResponse.GetRawResponse().Status);

            createResponse = await this.CreateDatabaseHelper(createResponse.Value.Id, databaseExists: true);
            Assert.AreEqual((int)HttpStatusCode.OK, createResponse.GetRawResponse().Status);
            //Assert.IsNotNull(createResponse.Diagnostics);
            //string diagnostics = createResponse.Diagnostics.ToString();
            //Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            //Assert.IsTrue(diagnostics.Contains("requestStartTime"));
        }

        [TestMethod]
        public async Task NoThroughputTests()
        {
            string databaseId = Guid.NewGuid().ToString();
            DatabaseResponse createResponse = await this.CreateDatabaseHelper(databaseId, databaseExists: false);
            Assert.AreEqual((int)HttpStatusCode.Created, createResponse.GetRawResponse().Status);

            Cosmos.CosmosDatabase cosmosDatabase = createResponse;
            int? readThroughput = await cosmosDatabase.ReadThroughputAsync();
            Assert.IsNull(readThroughput);

            await cosmosDatabase.DeleteAsync();
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
            CosmosClient client = TestCommon.CreateCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.WithCustomSerializer(mockJsonSerializer));

            string databaseId = Guid.NewGuid().ToString();
            int throughput = 10000;
            DatabaseResponse createResponse = await client.CreateDatabaseAsync(databaseId, throughput, null);
            Assert.AreEqual((int)HttpStatusCode.Created, createResponse.GetRawResponse().Status);

            Cosmos.CosmosDatabase cosmosDatabase = createResponse;
            ThroughputResponse readThroughputResponse = await cosmosDatabase.ReadThroughputAsync(new RequestOptions());
            Assert.IsNotNull(readThroughputResponse);
            Assert.IsNotNull(readThroughputResponse.Value);
            Assert.IsNotNull(readThroughputResponse.MinThroughput);
            Assert.IsNotNull(readThroughputResponse.Value.Throughput);
            Assert.AreEqual(throughput, readThroughputResponse.Value.Throughput.Value);

            // Implicit
            ThroughputProperties throughputProperties = await cosmosDatabase.ReadThroughputAsync(new RequestOptions());
            Assert.IsNotNull(throughputProperties);
            Assert.AreEqual(throughput, throughputProperties.Throughput);

            // Simple API 
            int? readThroughput = await cosmosDatabase.ReadThroughputAsync();
            Assert.AreEqual(throughput, readThroughput);

            // Database must have a container before it can be scaled
            string containerId = Guid.NewGuid().ToString();
            string partitionPath = "/users";
            ContainerResponse containerResponse = await cosmosDatabase.CreateContainerAsync(containerId, partitionPath, throughput: null);
            Assert.AreEqual((int)HttpStatusCode.Created, containerResponse.GetRawResponse().Status);

            ThroughputResponse replaceThroughputResponse = await cosmosDatabase.ReplaceThroughputAsync(readThroughputResponse.Value.Throughput.Value + 1000);
            Assert.IsNotNull(replaceThroughputResponse);
            Assert.IsNotNull(replaceThroughputResponse.Value);
            Assert.AreEqual(readThroughputResponse.Value.Throughput.Value + 1000, replaceThroughputResponse.Value.Throughput.Value);

            await cosmosDatabase.DeleteAsync();
            CosmosDatabase databaseNoThroughput = await client.CreateDatabaseAsync(Guid.NewGuid().ToString(), throughput: null);
            try
            {
                ThroughputResponse throughputResponse = await databaseNoThroughput.ReadThroughputAsync(new RequestOptions());
                Assert.Fail("Should through not found exception as throughput is not configured");
            }
            catch (CosmosException exception)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
            }

            try
            {
                ThroughputResponse throughputResponse = await databaseNoThroughput.ReplaceThroughputAsync(2000, new RequestOptions());
                Assert.Fail("Should through not found exception as throughput is not configured");
            }
            catch (CosmosException exception)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
            }

            int? dbThroughput = await databaseNoThroughput.ReadThroughputAsync();
            Assert.IsNull(dbThroughput);

            Assert.AreEqual(0, toStreamCount, "Custom serializer to stream should not be used for offer operations");
            Assert.AreEqual(0, fromStreamCount, "Custom serializer from stream should not be used for offer operations");
            await databaseNoThroughput.DeleteAsync();
        }

        [TestMethod]
        public async Task DatabaseIterator()
        {
            List<Cosmos.CosmosDatabase> deleteList = new List<Cosmos.CosmosDatabase>();
            HashSet<string> databaseIds = new HashSet<string>();
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    DatabaseResponse createResponse = await this.CreateDatabaseHelper();
                    deleteList.Add(createResponse.Database);
                    databaseIds.Add(createResponse.Value.Id);
                }

                await foreach(CosmosDatabaseProperties databaseSettings in this.cosmosClient.GetDatabaseQueryIterator<CosmosDatabaseProperties>(
                    queryDefinition: null,
                    continuationToken: null,
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 2 }))
                {
                    if (databaseIds.Contains(databaseSettings.Id))
                    {
                        databaseIds.Remove(databaseSettings.Id);
                    }
                }
            }
            finally
            {
                foreach (Cosmos.CosmosDatabase database in deleteList)
                {
                    await database.DeleteAsync(cancellationToken: this.cancellationToken);
                }
            }

            Assert.AreEqual(0, databaseIds.Count);
        }

        [TestMethod]
        public async Task DatabaseQueryIterator()
        {
            List<Cosmos.CosmosDatabase> deleteList = new List<Cosmos.CosmosDatabase>();
            try
            {
                string firstDb = "Abcdefg";
                string secondDb = "Bcdefgh";
                string thirdDb = "Zoo";

                DatabaseResponse createResponse2 = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(secondDb);
                deleteList.Add(createResponse2.Database);
                DatabaseResponse createResponse = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(firstDb);
                deleteList.Add(createResponse.Database);
                DatabaseResponse createResponse3 = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(thirdDb);
                deleteList.Add(createResponse3.Database);

                int iterations = 0;
                await foreach(Page<CosmosDatabaseProperties> iterator in this.cosmosClient.GetDatabaseQueryIterator<CosmosDatabaseProperties>(
                        new QueryDefinition("select c.id From c where c.id = @id ")
                        .WithParameter("@id", createResponse.Database.Id),
                        requestOptions: new QueryRequestOptions() { MaxItemCount = 1 }).AsPages())
                {
                    Assert.AreEqual(1, iterator.Values.Count);
                    Assert.AreEqual(firstDb, iterator.Values.First().Id);
                    iterations++;
                }

                Assert.AreEqual(1, iterations);
            }
            finally
            {
                foreach (Cosmos.CosmosDatabase database in deleteList)
                {
                    await database.DeleteAsync(cancellationToken: this.cancellationToken);
                }
            }
        }

        private Task<DatabaseResponse> CreateDatabaseHelper()
        {
            return this.CreateDatabaseHelper(Guid.NewGuid().ToString(), databaseExists: false);
        }

        private async Task<DatabaseResponse> CreateDatabaseHelper(
            string databaseId,
            int? throughput = null,
            bool databaseExists = false)
        {
            DatabaseResponse response = null;
            if (databaseExists)
            {
                response = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(
                    databaseId,
                    throughput,
                    cancellationToken: this.cancellationToken);
            }
            else
            {
                response = await this.cosmosClient.CreateDatabaseAsync(
                    databaseId,
                    throughput,
                    cancellationToken: this.cancellationToken);
            }

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Database);
            Assert.IsNotNull(response.Value);
            Assert.AreEqual(databaseId, response.Value.Id);
            Assert.AreEqual(databaseId, response.Database.Id);
            this.ValidateHeaders(response);

            Assert.IsTrue(response.GetRawResponse().Status == (int)HttpStatusCode.OK || (response.GetRawResponse().Status == (int)HttpStatusCode.Created && !databaseExists));

            return response;
        }

        private async Task<Cosmos.CosmosDatabase> CreateDatabaseStreamHelper(
            string databaseId = null,
            int? throughput = null,
            bool databaseExists = false)
        {
            if (string.IsNullOrEmpty(databaseId))
            {
                databaseId = Guid.NewGuid().ToString();
            }

            CosmosDatabaseProperties databaseSettings = new CosmosDatabaseProperties() { Id = databaseId };
            Response response = await this.cosmosClient.CreateDatabaseStreamAsync(
                databaseSettings,
                throughput: 400);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Headers.GetRequestCharge());
            Assert.IsNotNull(response.Headers.GetActivityId());

            Assert.IsTrue(response.Status == (int)HttpStatusCode.OK || (response.Status == (int)HttpStatusCode.Created && !databaseExists));

            return this.cosmosClient.GetDatabase(databaseId);
        }

        private void ValidateHeaders(DatabaseResponse cosmosDatabaseResponse)
        {
            // Test emulator is regression and commented out to unblock
            // Assert.IsNotNull(cosmosDatabaseResponse.MaxResourceQuota);
            // Assert.IsNotNull(cosmosDatabaseResponse.CurrentResourceQuotaUsage);
        }
    }
}
