//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

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
            Assert.IsTrue(response.RequestCharge > 0);
            Assert.IsNotNull(response.Headers);
            Assert.IsNotNull(response.Headers.ActivityId);

            DatabaseProperties databaseSettings = response.Resource;
            Assert.IsNotNull(databaseSettings.Id);
            Assert.IsNotNull(databaseSettings.ResourceId);
            Assert.IsNotNull(databaseSettings.ETag);
            Assert.IsTrue(databaseSettings.LastModified.HasValue);
            Assert.IsTrue(databaseSettings.LastModified.Value > new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), databaseSettings.LastModified.Value.ToString());

            DatabaseInternal databaseCore = response.Database as DatabaseInlineCore;
            Assert.IsNotNull(databaseCore);
            Assert.IsNotNull(databaseCore.LinkUri);
            Assert.IsFalse(databaseCore.LinkUri.ToString().StartsWith("/"));

            response = await response.Database.DeleteAsync(cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        [TestMethod]
        public async Task CreateDropDatabase()
        {
            DatabaseResponse response = await this.CreateDatabaseHelper();
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.IsNotNull(response.Diagnostics);
            string diagnostics = response.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
            Assert.IsTrue(response.Database is DatabaseInlineCore);

            response = await response.Database.DeleteAsync(cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
            Assert.IsNotNull(response.Diagnostics);
            diagnostics = response.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
            Assert.IsTrue(response.Database is DatabaseInlineCore);
        }

        [TestMethod]
        public async Task StreamCrudTestAsync()
        {
            Cosmos.Database database = await this.CreateDatabaseStreamHelper();

            using (ResponseMessage response = await database.ReadStreamAsync())
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsNotNull(response.Headers);
                Assert.IsTrue(response.Headers.RequestCharge > 0);
            }

            using (ResponseMessage response = await database.DeleteStreamAsync())
            {
                Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
                Assert.IsNotNull(response.Headers);
                Assert.IsTrue(response.Headers.RequestCharge > 0);
            }
        }

        [TestMethod]
        public async Task StreamCreateConflictTestAsync()
        {
            DatabaseProperties databaseSettings = new DatabaseProperties()
            {
                Id = Guid.NewGuid().ToString()
            };

            using (ResponseMessage response = await this.cosmosClient.CreateDatabaseStreamAsync(databaseSettings))
            {
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                Assert.IsNotNull(response.Headers);
                Assert.IsTrue(response.Headers.RequestCharge > 0);
            }

            // Stream operations do not throw exceptions.
            using (ResponseMessage response = await this.cosmosClient.CreateDatabaseStreamAsync(databaseSettings))
            {
                Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
                Assert.IsNotNull(response.Headers);
                Assert.IsTrue(response.Headers.RequestCharge > 0);
            }

            using (ResponseMessage response = await this.cosmosClient.GetDatabase(databaseSettings.Id).DeleteStreamAsync())
            {
                Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
                Assert.IsNotNull(response.Headers);
                Assert.IsTrue(response.Headers.RequestCharge > 0);
            }
        }

        [TestMethod]
        public async Task CreateConflict()
        {
            DatabaseResponse response = await this.CreateDatabaseHelper();
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            try
            {
                response = await this.CreateDatabaseHelper(response.Resource.Id);
                Assert.Fail($"Unexpected success status code {response.StatusCode}");
            }
            catch (CosmosException hre)
            {
                DefaultTrace.TraceInformation(hre.ToString());
            }

            response = await response.Database.DeleteAsync(cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        [TestMethod]
        public async Task ImplicitConversion()
        {
            DatabaseResponse cosmosDatabaseResponse = await this.CreateDatabaseHelper();
            Cosmos.Database cosmosDatabase = cosmosDatabaseResponse;
            DatabaseProperties cosmosDatabaseSettings = cosmosDatabaseResponse;
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
            Assert.AreEqual(createResponse.Resource.Id, readResponse.Resource.Id);
            Assert.AreNotEqual(createResponse.ActivityId, readResponse.ActivityId);
            Assert.IsNotNull(createResponse.Resource.SelfLink);
            Assert.IsNotNull(readResponse.Resource.SelfLink);
            Assert.AreEqual(createResponse.Resource.SelfLink, readResponse.Resource.SelfLink);
            SelflinkValidator.ValidateDbSelfLink(readResponse.Resource.SelfLink);

            this.ValidateHeaders(readResponse);
            await createResponse.Database.DeleteAsync(cancellationToken: this.cancellationToken);
        }

        [TestMethod]
        public async Task CreateIfNotExists()
        {
            RequestChargeHandlerHelper requestChargeHandler = new RequestChargeHandlerHelper();
            RequestHandlerHelper requestHandlerHelper = new RequestHandlerHelper();

            CosmosClient client = TestCommon.CreateCosmosClient(x => x.AddCustomHandlers(requestChargeHandler, requestHandlerHelper));

            // Create a new database
            requestChargeHandler.TotalRequestCharges = 0;
            DatabaseResponse createResponse = await client.CreateDatabaseIfNotExistsAsync(Guid.NewGuid().ToString());
            Assert.AreEqual(requestChargeHandler.TotalRequestCharges, createResponse.RequestCharge);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            requestChargeHandler.TotalRequestCharges = 0;
            DatabaseResponse createExistingResponse = await client.CreateDatabaseIfNotExistsAsync(createResponse.Resource.Id);
            Assert.AreEqual(HttpStatusCode.OK, createExistingResponse.StatusCode);
            Assert.AreEqual(requestChargeHandler.TotalRequestCharges, createExistingResponse.RequestCharge);
            Assert.IsNotNull(createExistingResponse.Diagnostics);
            string diagnostics = createExistingResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("CreateDatabaseIfNotExistsAsync"));

            bool conflictReturned = false;
            requestHandlerHelper.CallBackOnResponse = (request, response) =>
            {
                if(request.OperationType == Documents.OperationType.Create &&
                    request.ResourceType == Documents.ResourceType.Database)
                {
                    conflictReturned = true;
                    // Simulate a race condition which results in a 409
                    return CosmosExceptionFactory.Create(
                        statusCode: HttpStatusCode.Conflict,
                        message: "Fake 409 conflict",
                        stackTrace: string.Empty,
                        headers: response.Headers,
                        error: default,
                        innerException: default,
                        trace: request.Trace).ToCosmosResponseMessage(request);
                }

                return response;
            };

            requestChargeHandler.TotalRequestCharges = 0;
            DatabaseResponse createWithConflictResponse = await client.CreateDatabaseIfNotExistsAsync(Guid.NewGuid().ToString());
            Assert.AreEqual(requestChargeHandler.TotalRequestCharges, createWithConflictResponse.RequestCharge);
            Assert.AreEqual(HttpStatusCode.OK, createWithConflictResponse.StatusCode);
            Assert.IsTrue(conflictReturned);

            await createResponse.Database.DeleteAsync();
            await createWithConflictResponse.Database.DeleteAsync();
        }

        [TestMethod]
        public async Task NoThroughputTests()
        {
            string databaseId = Guid.NewGuid().ToString();
            DatabaseResponse createResponse = await this.CreateDatabaseHelper(databaseId, databaseExists: false);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            Cosmos.Database cosmosDatabase = createResponse;
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
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            Cosmos.Database cosmosDatabase = createResponse;
            ThroughputResponse readThroughputResponse = await cosmosDatabase.ReadThroughputAsync(new RequestOptions());
            Assert.IsNotNull(readThroughputResponse);
            Assert.IsNotNull(readThroughputResponse.Resource);
            Assert.IsNotNull(readThroughputResponse.MinThroughput);
            Assert.IsNotNull(readThroughputResponse.Resource.Throughput);
            Assert.AreEqual(throughput, readThroughputResponse.Resource.Throughput.Value);

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
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);

            ThroughputResponse replaceThroughputResponse = await cosmosDatabase.ReplaceThroughputAsync(readThroughputResponse.Resource.Throughput.Value + 1000);
            Assert.IsNotNull(replaceThroughputResponse);
            Assert.IsNotNull(replaceThroughputResponse.Resource);
            Assert.AreEqual(readThroughputResponse.Resource.Throughput.Value + 1000, replaceThroughputResponse.Resource.Throughput.Value);

            await cosmosDatabase.DeleteAsync();
            Database databaseNoThroughput = await client.CreateDatabaseAsync(Guid.NewGuid().ToString(), throughput: null);
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
            List<Cosmos.Database> deleteList = new List<Cosmos.Database>();
            HashSet<string> databaseIds = new HashSet<string>();
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    DatabaseResponse createResponse = await this.CreateDatabaseHelper();
                    deleteList.Add(createResponse.Database);
                    databaseIds.Add(createResponse.Resource.Id);
                }

                FeedIterator<DatabaseProperties> feedIterator = this.cosmosClient.GetDatabaseQueryIterator<DatabaseProperties>(
                    queryDefinition: null,
                    continuationToken: null,
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 2 });

                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<DatabaseProperties> iterator =
                        await feedIterator.ReadNextAsync(this.cancellationToken);
                    foreach (DatabaseProperties databaseSettings in iterator)
                    {
                        if (databaseIds.Contains(databaseSettings.Id))
                        {
                            databaseIds.Remove(databaseSettings.Id);
                        }
                    }
                }
            }
            finally
            {
                foreach (Cosmos.Database database in deleteList)
                {
                    await database.DeleteAsync(cancellationToken: this.cancellationToken);
                }
            }

            Assert.AreEqual(0, databaseIds.Count);
        }

        [TestMethod]
        public async Task EncryptionCreateReplaceCek()
        {
            DatabaseResponse response = await this.CreateDatabaseHelper();

            string cekId = "anotherCek";
            ClientEncryptionKeyProperties cekProperties = await CosmosDatabaseTests.CreateCekAsync((DatabaseInlineCore)response.Database, cekId);

            Assert.IsNotNull(cekProperties);
            Assert.IsNotNull(cekProperties.CreatedTime);
            Assert.IsNotNull(cekProperties.LastModified);
            Assert.IsNotNull(cekProperties.SelfLink);
            Assert.IsNotNull(cekProperties.ResourceId);

            Assert.AreEqual(
                new EncryptionKeyWrapMetadata("custom", "metadataName", "metadataValue"),
                cekProperties.EncryptionKeyWrapMetadata);

            // Use a different client instance to avoid (unintentional) cache impact
            ClientEncryptionKeyProperties readProperties = await ((DatabaseInlineCore)this.cosmosClient.GetDatabase(response.Database.Id)).GetClientEncryptionKey(cekId).ReadAsync();
            Assert.AreEqual(cekProperties, readProperties);

            // Replace
            cekProperties = await CosmosDatabaseTests.ReplaceCekAsync((DatabaseInlineCore)response.Database, cekId);

            Assert.IsNotNull(cekProperties);
            Assert.IsNotNull(cekProperties.CreatedTime);
            Assert.IsNotNull(cekProperties.LastModified);
            Assert.IsNotNull(cekProperties.SelfLink);
            Assert.IsNotNull(cekProperties.ResourceId);

            Assert.AreEqual(
                new EncryptionKeyWrapMetadata("custom", "metadataName", "updatedMetadataValue"),
                cekProperties.EncryptionKeyWrapMetadata);

            // Use a different client instance to avoid (unintentional) cache impact
            readProperties =
                await ((DatabaseCore)(DatabaseInlineCore)this.cosmosClient.GetDatabase(response.Database.Id)).GetClientEncryptionKey(cekId).ReadAsync();
            Assert.AreEqual(cekProperties, readProperties);

            response = await response.Database.DeleteAsync(cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        private static async Task<ClientEncryptionKeyProperties> CreateCekAsync(DatabaseInlineCore databaseCore, string cekId)
        {
            byte[] rawCek = new byte[32];
            // Generate random bytes cryptographically.
            using (RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider())
            {
                rngCsp.GetBytes(rawCek);
            }

            ClientEncryptionKeyProperties cekProperties = new ClientEncryptionKeyProperties(cekId, "AEAD_AES_256_CBC_HMAC_SHA256", rawCek, new EncryptionKeyWrapMetadata("custom", "metadataName", "metadataValue"));

            ClientEncryptionKeyResponse cekResponse = await databaseCore.CreateClientEncryptionKeyAsync(cekProperties);

            Assert.AreEqual(HttpStatusCode.Created, cekResponse.StatusCode);
            Assert.IsTrue(cekResponse.RequestCharge > 0);
            Assert.IsNotNull(cekResponse.ETag);

            ClientEncryptionKeyProperties retrievedCekProperties = cekResponse.Resource;
            Assert.IsTrue(rawCek.SequenceEqual(retrievedCekProperties.WrappedDataEncryptionKey));
            EqualityComparer<EncryptionKeyWrapMetadata>.Default.Equals(cekResponse.Resource.EncryptionKeyWrapMetadata, retrievedCekProperties.EncryptionKeyWrapMetadata);
            Assert.AreEqual(cekResponse.ETag, retrievedCekProperties.ETag);
            Assert.AreEqual(cekId, retrievedCekProperties.Id);
            Assert.AreEqual("AEAD_AES_256_CBC_HMAC_SHA256", retrievedCekProperties.EncryptionAlgorithm);
            return retrievedCekProperties;
        }

        private static async Task<ClientEncryptionKeyProperties> ReplaceCekAsync(DatabaseCore databaseCore, string cekId)
        {
            ClientEncryptionKeyCore cek = (ClientEncryptionKeyInlineCore)databaseCore.GetClientEncryptionKey(cekId);

            byte[] rawCek = new byte[32];
            // Generate random bytes cryptographically.
            using (RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider())
            {
                rngCsp.GetBytes(rawCek);
            }

            ClientEncryptionKeyProperties cekProperties = new ClientEncryptionKeyProperties(cekId, "AEAD_AES_256_CBC_HMAC_SHA256", rawCek, new EncryptionKeyWrapMetadata("custom", "metadataName", "updatedMetadataValue"));

            ClientEncryptionKeyResponse cekResponse = await cek.ReplaceAsync(cekProperties);
            Assert.AreEqual(HttpStatusCode.OK, cekResponse.StatusCode);
            Assert.IsTrue(cekResponse.RequestCharge > 0);
            Assert.IsNotNull(cekResponse.ETag);

            ClientEncryptionKeyProperties retrievedCekProperties = cekResponse.Resource;
            Assert.IsTrue(rawCek.SequenceEqual(retrievedCekProperties.WrappedDataEncryptionKey));
            Assert.AreEqual(cekResponse.ETag, retrievedCekProperties.ETag);
            Assert.AreEqual(cekId, retrievedCekProperties.Id);
            Assert.AreEqual(cekProperties.EncryptionAlgorithm, retrievedCekProperties.EncryptionAlgorithm);
            return retrievedCekProperties;
        }

        [TestMethod]
        public async Task VerifyCekFeedIterator()
        {
            DatabaseResponse response = await this.CreateDatabaseHelper();

            DatabaseInlineCore databaseCore = (DatabaseInlineCore)response.Database;

            string cekId = "Cek1";

            byte[] rawCek1 = new byte[32];
            // Generate random bytes cryptographically.
            using (RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider())
            {
                rngCsp.GetBytes(rawCek1);
            }

            ClientEncryptionKeyProperties cekProperties = new ClientEncryptionKeyProperties(cekId, "AEAD_AES_256_CBC_HMAC_SHA256", rawCek1, new EncryptionKeyWrapMetadata("custom", "metadataName", "metadataValue"));

            ClientEncryptionKeyResponse cekResponse = await databaseCore.CreateClientEncryptionKeyAsync(cekProperties);

            Assert.AreEqual(HttpStatusCode.Created, cekResponse.StatusCode);

            cekId = "Cek2";

            byte[] rawCek2 = new byte[32];
            // Generate random bytes cryptographically.
            using (RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider())
            {
                rngCsp.GetBytes(rawCek2);
            }

            cekProperties = new ClientEncryptionKeyProperties(cekId, "AEAD_AES_256_CBC_HMAC_SHA256", rawCek2, new EncryptionKeyWrapMetadata("custom", "metadataName", "metadataValue"));

            cekResponse = await databaseCore.CreateClientEncryptionKeyAsync(cekProperties);

            Assert.AreEqual(HttpStatusCode.Created, cekResponse.StatusCode);

            FeedIterator<ClientEncryptionKeyProperties> feedIteratorcep = databaseCore.GetClientEncryptionKeyQueryIterator(null);
            Assert.IsTrue(feedIteratorcep.HasMoreResults);

            FeedResponse<ClientEncryptionKeyProperties> feedResponse = null;

            while (feedIteratorcep.HasMoreResults)
            {
                feedResponse = await feedIteratorcep.ReadNextAsync();
            }

            Assert.AreEqual(2, feedResponse.Count);
            List<string> readDekIds = new List<string>();
            List<string> expectedDekIds = new List<string> { "Cek1" , "Cek2" };

            foreach (ClientEncryptionKeyProperties clientEncryptionKeyProperties in feedResponse.Resource)
            {
                readDekIds.Add(clientEncryptionKeyProperties.Id);
                Assert.AreEqual("AEAD_AES_256_CBC_HMAC_SHA256", clientEncryptionKeyProperties.EncryptionAlgorithm);
                Assert.AreEqual(cekProperties.EncryptionKeyWrapMetadata.Type, clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Type);
                Assert.AreEqual(cekProperties.EncryptionKeyWrapMetadata.Name, clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Name);
                Assert.AreEqual(cekProperties.EncryptionKeyWrapMetadata.Value, clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Value);
            }

            Assert.IsTrue(expectedDekIds.ToHashSet().SetEquals(readDekIds));
            
            response = await response.Database.DeleteAsync(cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);

        }

        [TestMethod]
        public async Task DatabaseQueryIterator()
        {
            List<Cosmos.Database> deleteList = new List<Cosmos.Database>();
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

                using (FeedIterator<DatabaseProperties> feedIterator =
                   this.cosmosClient.GetDatabaseQueryIterator<DatabaseProperties>(
                       new QueryDefinition("select c.id From c where c.id = @id ")
                       .WithParameter("@id", createResponse.Database.Id),
                       requestOptions: new QueryRequestOptions() { MaxItemCount = 1 }))
                {
                    FeedResponse<DatabaseProperties> iterator = await feedIterator.ReadNextAsync(this.cancellationToken);
                    Assert.AreEqual(1, iterator.Resource.Count());
                    Assert.AreEqual(firstDb, iterator.First().Id);

                    Assert.IsFalse(feedIterator.HasMoreResults);
                }

                using (FeedIterator feedIterator =
                    this.cosmosClient.GetDatabaseQueryStreamIterator(
                        "select value c.id From c "))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        using (ResponseMessage response = await feedIterator.ReadNextAsync(this.cancellationToken))
                        {
                            response.EnsureSuccessStatusCode();
                            using(StreamReader streamReader = new StreamReader(response.Content))
                            using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
                            {
                                // Output will be:
                                // { "_rid":"","Databases":["Zoo","Abcdefg","Bcdefgh"],"_count":3}
                                JObject jObject = await JObject.LoadAsync(jsonTextReader);
                                Assert.IsNotNull(jObject["_rid"].ToString());
                                Assert.IsTrue(jObject["Databases"].ToObject<JArray>().Count > 0);
                                Assert.IsTrue(jObject["_count"].ToObject<int>() > 0);
                            }
                        }
                    }
                }

                List<string> ids = new List<string>();
                using (FeedIterator<string> feedIterator =
                    this.cosmosClient.GetDatabaseQueryIterator<string>(
                        "select value c.id From c "))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<string> iterator = await feedIterator.ReadNextAsync(this.cancellationToken);
                        ids.AddRange(iterator);
                    }
                }
                
                Assert.IsTrue(ids.Count >= 2);
            }
            finally
            {
                foreach (Cosmos.Database database in deleteList)
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
            Assert.IsNotNull(response.Resource);
            Assert.AreEqual(databaseId, response.Resource.Id);
            Assert.AreEqual(databaseId, response.Database.Id);
            this.ValidateHeaders(response);

            Assert.IsTrue(response.StatusCode == HttpStatusCode.OK || (response.StatusCode == HttpStatusCode.Created && !databaseExists));

            return response;
        }

        private async Task<Cosmos.Database> CreateDatabaseStreamHelper(
            string databaseId = null,
            int? throughput = null,
            bool databaseExists = false)
        {
            if (string.IsNullOrEmpty(databaseId))
            {
                databaseId = Guid.NewGuid().ToString();
            }

            DatabaseProperties databaseSettings = new DatabaseProperties() { Id = databaseId };
            ResponseMessage response = await this.cosmosClient.CreateDatabaseStreamAsync(
                databaseSettings,
                throughput: 400);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Headers.RequestCharge);
            Assert.IsNotNull(response.Headers.ActivityId);

            Assert.IsTrue(response.StatusCode == HttpStatusCode.OK || (response.StatusCode == HttpStatusCode.Created && !databaseExists));

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
