//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class UniqueIndexTests
    {
        private CosmosClient client;  // This is only used for housekeeping this.database.
        private Cosmos.Database database;
        private readonly string defaultPartitionKeyDefinition = "/pk";

        [TestInitialize]
        public async Task TestInitializeAsync()
        {
            this.client = TestCommon.CreateCosmosClient(true);
            this.database = await this.client.CreateDatabaseAsync(Guid.NewGuid().ToString());
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            if (this.database != null)
            {
                using (await this.database.DeleteStreamAsync()) { }
            }
        }

        [TestMethod]
        public async Task InsertWithUniqueIndex()
        {
            ContainerProperties collectionSpec = new ContainerProperties
            {
                Id = "InsertWithUniqueIndexConstraint_" + Guid.NewGuid(),
                PartitionKeyPath = defaultPartitionKeyDefinition,
                UniqueKeyPolicy = new UniqueKeyPolicy
                {
                    UniqueKeys = new Collection<UniqueKey> {
                        new UniqueKey {
                            Paths = new Collection<string> { "/name", "/address" }
                        }
                    }
                },
                IndexingPolicy = new IndexingPolicy
                {
                    IndexingMode = IndexingMode.Consistent,
                    IncludedPaths = new Collection<IncludedPath> {
                        new IncludedPath { Path = "/name/?", Indexes = new Collection<Index> { new HashIndex(DataType.String, 7) } },
                        new IncludedPath { Path = "/address/?", Indexes = new Collection<Index> { new HashIndex(DataType.String, 7) } },
                    },
                    ExcludedPaths = new Collection<ExcludedPath> {
                        new ExcludedPath { Path = "/*" }
                    }
                }
            };

            Func<Container, Task> testFunction = async (Container container) =>
            {
                dynamic doc1 = new { id = Guid.NewGuid().ToString(), name = "Alexander Pushkin", pk = "test", address = "Russia 630090" };
                dynamic doc1Conflict = new { id = Guid.NewGuid().ToString(), name = doc1.name, pk = doc1.pk, address = doc1.address };
                dynamic doc1Conflict2 = new { id = Guid.NewGuid().ToString(), name = doc1.name, pk = doc1.pk, address = doc1.address };
                dynamic doc2 = new { id = Guid.NewGuid().ToString(), name = "Alexander Pushkin", pk = "test", address = "Russia 640000" };
                dynamic doc3 = new { id = Guid.NewGuid().ToString(), name = "Mihkail Lermontov", pk = "test", address = "Russia 630090" };

                await container.CreateItemAsync<dynamic>(doc1);

                try
                {
                    await container.CreateItemAsync<dynamic>(doc1Conflict);
                    Assert.Fail("Did not throw due to unique constraint (create)");
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.Conflict, ex.StatusCode, ex.ToString());
                }

                try
                {
                    await container.UpsertItemAsync<dynamic>(doc1Conflict2);
                    Assert.Fail("Did not throw due to unique constraint (upsert)");
                }
                catch (CosmosException ex)
                {
                    // Search for: L"For upsert insert, if it failed with E_RESOURCE_ALREADY_EXISTS, return E_CONCURRENCY_VIOLATION so that client will retry"
                    Assert.AreEqual(HttpStatusCode.Conflict, ex.StatusCode, $"Expected:Conflict, Actual:{ex.StatusCode}; Exception:ex.ToString()");
                }

                await container.CreateItemAsync<dynamic>(doc2);
                await container.CreateItemAsync<dynamic>(doc3);
            };

            await this.TestForEachClient(collectionSpec, testFunction, "InsertWithUniqueIndex");
        }

        [TestMethod]
        public async Task ReplaceAndDeleteWithUniqueIndex()
        {
            ContainerProperties collectionSpec = new ContainerProperties
            {
                Id = "InsertWithUniqueIndexConstraint_" + Guid.NewGuid(),
                PartitionKeyPath = defaultPartitionKeyDefinition,
                UniqueKeyPolicy = new UniqueKeyPolicy
                {
                    UniqueKeys = new Collection<UniqueKey> { new UniqueKey { Paths = new Collection<string> { "/name", "/address" } } }
                }
            };

            Func<Container, Task> testFunction = async (Container collection) =>
            {
                JObject doc1 = JObject.FromObject(new { id = Guid.NewGuid().ToString(), name = "Alexander Pushkin", pk = "test", address = "Russia 630090" });
                JObject doc2 = JObject.FromObject(new { id = Guid.NewGuid().ToString(), name = "Mihkail Lermontov", pk = "test", address = "Russia 630090" });
                JObject doc3 = JObject.FromObject(new { id = Guid.NewGuid().ToString(), name = "Alexander Pushkin", pk = "test", address = "Russia 640000" });

                ItemResponse<JObject> doc1InsertedResponse = await collection.CreateItemAsync<JObject>(doc1);
                JObject doc1Inserted = doc1InsertedResponse.Resource;

                await collection.ReplaceItemAsync<dynamic>(doc1Inserted, doc1Inserted["id"].ToString());     // Replace with same values -- OK.

                ItemResponse<JObject> doc2InsertedResponse = await collection.CreateItemAsync<JObject>(doc2);
                JObject doc2Inserted = doc2InsertedResponse.Resource;
                JObject doc2Replacement = JObject.Parse(JsonConvert.SerializeObject(doc1Inserted));
                doc2Replacement["id"] = doc2Inserted["id"];

                try
                {
                    await collection.ReplaceItemAsync<JObject>(doc2Replacement, doc2Inserted["id"].ToString()); // Replace doc2 with values from doc1 -- Conflict.
                    Assert.Fail("Did not throw due to unique constraint");
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.Conflict, ex.StatusCode, $"Expected:Conflict, Actual:{ex.StatusCode}; Exception:ex.ToString()");
                }

                doc3["id"] = doc1Inserted["id"].ToString();
                await collection.ReplaceItemAsync<dynamic>(doc3, doc1Inserted["id"].ToString());             // Replace with values from doc3 -- OK.

                await collection.DeleteItemAsync<dynamic>(doc1["id"].ToString(), new PartitionKey(doc1["pk"].ToString()));
                await collection.CreateItemAsync<dynamic>(doc1);
            };

            await this.TestForEachClient(collectionSpec, testFunction, "ReplaceAndDeleteWithUniqueIndex");
        }

        [TestMethod]
        [Description("Make sure that the pair (PK, unique key) is globally (not depending on partition/PK) unique")]
        public void TestGloballyUniquenessOfFieldAndPartitionedKeyPair()
        {
            using (CosmosClient client = TestCommon.CreateCosmosClient(true))
            {
                this.TestGloballyUniqueFieldForPartitionedCollectionHelperAsync(client).Wait();
            }
        }

        private async Task TestGloballyUniqueFieldForPartitionedCollectionHelperAsync(CosmosClient client)
        {
            ContainerProperties collectionSpec = new ContainerProperties
            {
                Id = "TestGloballyUniqueFieldForPartitionedCollection_" + Guid.NewGuid(),
                PartitionKeyPath = defaultPartitionKeyDefinition,
                UniqueKeyPolicy = new UniqueKeyPolicy
                {
                    UniqueKeys = new Collection<UniqueKey> {
                        new UniqueKey { Paths = new Collection<string> { "/name" } }
                    }
                },
                IndexingPolicy = new IndexingPolicy
                {
                    IndexingMode = IndexingMode.Consistent,
                    IncludedPaths = new Collection<IncludedPath> {
                        new IncludedPath { Path = "/name/?", Indexes = new Collection<Index> { new HashIndex(DataType.String, 7) } },
                    },
                    ExcludedPaths = new Collection<ExcludedPath> {
                        new ExcludedPath { Path = "/*" }
                    }
                }
            };

            Container collection = await this.database.CreateContainerAsync(
                collectionSpec,
                20000);

            const int partitionCount = 50;
            List<string> partitionKeyValues = new List<string>();
            for (int i = 0; i < partitionCount * 3; ++i)
            {
                partitionKeyValues.Add(Guid.NewGuid().ToString());
            }

            string documentTemplate = "{{ \"id\":\"{0}\",  \"pk\":\"{1}\", \"name\":\"{2}\" }}";
            foreach (string partitionKey in partitionKeyValues)
            {
                string document = string.Format(documentTemplate, Guid.NewGuid().ToString(), partitionKey, "Same Name");
                await collection.CreateItemAsync<dynamic>(JObject.Parse(document));
            }

            string conflictDocument = string.Format(documentTemplate, Guid.NewGuid().ToString(), partitionKeyValues[0], "Same Name");
            try
            {
                await collection.CreateItemAsync<dynamic>(JObject.Parse(conflictDocument));
                Assert.Fail("Did not throw due to unique constraint");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.Conflict, ex.StatusCode, $"Expected:Conflict, Actual:{ex.StatusCode}; Exception:ex.ToString()");
            }
        }

        private async Task TestForEachClient(ContainerProperties collectionSpec, Func<Container, Task> testFunction, string scenarioName)
        {
            Container collection = await this.database.CreateContainerAsync(collectionSpec);

            try
            {
                await testFunction(collection);
            }
            finally
            {
                using (await collection.DeleteContainerStreamAsync()) { }
            }
        }
    }
}
