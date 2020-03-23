//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class UniqueIndexTests
    {
        private DocumentClient client;  // This is only used for housekeeping this.database.
        private Database database;
        private readonly PartitionKeyDefinition defaultPartitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };

        [TestInitialize]
        public void TestInitialize()
        {
            this.client = TestCommon.CreateClient(true);
            this.database = TestCommon.CreateOrGetDatabase(this.client);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.client.DeleteDatabaseAsync(this.database).Wait();
        }

        [TestMethod]
        public void InsertWithUniqueIndex()
        {
            DocumentCollection collectionSpec = new DocumentCollection
            {
                Id = "InsertWithUniqueIndexConstraint_" + Guid.NewGuid(),
                PartitionKey = defaultPartitionKeyDefinition,
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

            Func<DocumentClient, DocumentCollection, Task> testFunction = async (DocumentClient client, DocumentCollection collection) =>
            {
                JObject doc1 = JObject.Parse("{\"name\":\"Alexander Pushkin\",\"address\":\"Russia 630090\"}");
                JObject doc2 = JObject.Parse("{\"name\":\"Alexander Pushkin\",\"address\":\"Russia 640000\"}");
                JObject doc3 = JObject.Parse("{\"name\":\"Mihkail Lermontov\",\"address\":\"Russia 630090\"}");

                await client.CreateDocumentAsync(collection, doc1);

                try
                {
                    await client.CreateDocumentAsync(collection, doc1);
                    Assert.Fail("Did not throw due to unique constraint (create)");
                }
                catch (DocumentClientException ex)
                {
                    Assert.AreEqual(StatusCodes.Conflict, (StatusCodes)ex.StatusCode);
                }

                try
                {
                    await client.UpsertDocumentAsync(collection.SelfLink, doc1);
                    Assert.Fail("Did not throw due to unique constraint (upsert)");
                }
                catch (DocumentClientException ex)
                {
                    // Search for: L"For upsert insert, if it failed with E_RESOURCE_ALREADY_EXISTS, return E_CONCURRENCY_VIOLATION so that client will retry"
                    Assert.AreEqual(StatusCodes.Conflict, (StatusCodes)ex.StatusCode);
                }

                await client.CreateDocumentAsync(collection, doc2);
                await client.CreateDocumentAsync(collection, doc3);
            };

            this.TestForEachClient(collectionSpec, testFunction, "InsertWithUniqueIndex");
        }

        [TestMethod]
        public void ReplaceAndDeleteWithUniqueIndex()
        {
            DocumentCollection collectionSpec = new DocumentCollection
            {
                Id = "InsertWithUniqueIndexConstraint_" + Guid.NewGuid(),
                PartitionKey = defaultPartitionKeyDefinition,
                UniqueKeyPolicy = new UniqueKeyPolicy
                {
                    UniqueKeys = new Collection<UniqueKey> { new UniqueKey { Paths = new Collection<string> { "/name", "/address" } } }
                }
            };
            RequestOptions requestOptions = new RequestOptions();
            requestOptions.PartitionKey = new PartitionKey("test");
            Func<DocumentClient, DocumentCollection, Task> testFunction = async (DocumentClient client, DocumentCollection collection) =>
            {
                JObject doc1 = JObject.Parse("{\"name\":\"Alexander Pushkin\",\"pk\":\"test\",\"address\":\"Russia 630090\"}");
                JObject doc2 = JObject.Parse("{\"name\":\"Mihkail Lermontov\",\"pk\":\"test\",\"address\":\"Russia 630090\"}");
                JObject doc3 = JObject.Parse("{\"name\":\"Alexander Pushkin\",\"pk\":\"test\",\"address\":\"Russia 640000\"}");

                Document doc1Inserted = await client.CreateDocumentAsync(collection, doc1);

                await client.ReplaceDocumentAsync(doc1Inserted.SelfLink, doc1Inserted, requestOptions);     // Replace with same values -- OK.

                Document doc2Inserted = await client.CreateDocumentAsync(collection, doc2);
                JObject doc2Replacement = JObject.Parse(JsonConvert.SerializeObject(doc1Inserted));
                doc2Replacement["id"] = doc2Inserted.Id;

                try
                {
                    await client.ReplaceDocumentAsync(doc2Inserted.SelfLink, doc2Replacement, requestOptions); // Replace doc2 with values from doc1 -- Conflict.
                    Assert.Fail("Did not throw due to unique constraint");
                }
                catch (DocumentClientException ex)
                {
                    Assert.AreEqual(StatusCodes.Conflict, (StatusCodes)ex.StatusCode);
                }

                doc3["id"] = doc1Inserted.Id;
                await client.ReplaceDocumentAsync(doc1Inserted.SelfLink, doc3, requestOptions);             // Replace with values from doc3 -- OK.

                await client.DeleteDocumentAsync(doc1Inserted.SelfLink, requestOptions);
                await client.CreateDocumentAsync(collection, doc1);
            };

            this.TestForEachClient(collectionSpec, testFunction, "ReplaceAndDeleteWithUniqueIndex");
        }

        [TestMethod]
        [Description("Make sure that the pair (PK, unique key) is globally (not depending on partition/PK) unique")]
        public void TestGloballyUniquenessOfFieldAndPartitionedKeyPair()
        {
            using (DocumentClient client = TestCommon.CreateClient(true, tokenType: AuthorizationTokenType.PrimaryMasterKey))
            {
                this.TestGloballyUniqueFieldForPartitionedCollectionHelperAsync(client).Wait();
            }
        }

        private async Task TestGloballyUniqueFieldForPartitionedCollectionHelperAsync(DocumentClient client)
        {
            DocumentCollection collectionSpec = new DocumentCollection
            {
                Id = "TestGloballyUniqueFieldForPartitionedCollection_" + Guid.NewGuid(),
                PartitionKey = new PartitionKeyDefinition
                {
                    Kind = PartitionKind.Hash,
                    Paths = new Collection<string> { "/pk" }
                },
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

            ResourceResponse<DocumentCollection> collection = await client.CreateDocumentCollectionAsync(
                this.database,
                collectionSpec,
                new RequestOptions { OfferThroughput = 20000 });

            const int partitionCount = 50;
            List<string> partitionKeyValues = new List<string>();
            for (int i = 0; i < partitionCount * 3; ++i)
            {
                partitionKeyValues.Add(Guid.NewGuid().ToString());
            }

            string documentTemplate = "{{ \"pk\":\"{0}\", \"name\":\"{1}\" }}";
            foreach (string partitionKey in partitionKeyValues)
            {
                string document = string.Format(documentTemplate, partitionKey, "Same Name");
                await client.CreateDocumentAsync(collection, JObject.Parse(document));
            }

            string conflictDocument = string.Format(documentTemplate, partitionKeyValues[0], "Same Name");
            try
            {
                await client.CreateDocumentAsync(collection, JObject.Parse(conflictDocument));
                Assert.Fail("Did not throw due to unique constraint");
            }
            catch (DocumentClientException ex)
            {
                Assert.AreEqual(StatusCodes.Conflict, (StatusCodes)ex.StatusCode);
            }
        }

        private void TestForEachClient(DocumentCollection collectionSpec, Func<DocumentClient, DocumentCollection, Task> testFunction, string scenarioName)
        {
            Func<DocumentClient, DocumentClientType, Task<int>> wrapperFunction = async (DocumentClient client, DocumentClientType clientType) =>
            {
                ResourceResponse<DocumentCollection> collection = await client.CreateDocumentCollectionAsync(this.database, collectionSpec);

                // Normally we would delete collection in in finally block, but can't await there.
                // Delete collection is needed so that next client from Util.TestForEachClient starts fresh.
                ExceptionDispatchInfo dispatchInfo = null;
                try
                {
                    await testFunction(client, collection);
                }
                catch (Exception ex)
                {
                    dispatchInfo = ExceptionDispatchInfo.Capture(ex);
                }

                try
                {
                    await client.DeleteDocumentCollectionAsync(collection);
                }
                finally
                {
                    if (dispatchInfo != null)
                    {
                        dispatchInfo.Throw();
                    }
                }

                return 0;
            };

            Util.TestForEachClient(wrapperFunction, scenarioName);
        }
    }
}
