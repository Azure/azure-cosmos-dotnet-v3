//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NameRoutingTests
    {

        [TestMethod]
        public async Task NameRoutingSmokeGatewayTest()
        {
            CosmosClient client = TestCommon.CreateCosmosClient(true);

            await SmokeTestForNameAPI(client);
        }

#if DIRECT_MODE
    // DIRECT MODE has ReadFeed issues in the Public emulator

        [TestMethod]
        public void NameRoutingSmokeDirectHttpsTest()
        {
            DocumentClient client;
            client = TestCommon.CreateClient(false, Protocol.Https, 10, ConsistencyLevel.Session);

            this.NameRoutingSmokeTestPrivateAsync(client).Wait();
        }

        [TestMethod]
        public void NameRoutingSmokeDirectTcpTest()
        {
            DocumentClient client;
            client = TestCommon.CreateClient(false, Protocol.Tcp);

            this.NameRoutingSmokeTestPrivateAsync(client).Wait();
        }
#endif
        private async Task NameRoutingSmokeTestPrivateAsync(CosmosClient client)
        {
            try
            {
                await SmokeTestForNameAPI(client);
            }
            catch (AggregateException e)
            {
                Assert.Fail("Caught Exception message: {0}, Exception {1},", e.InnerException.Message, e.InnerException.ToString());
            }
            catch (Exception e)
            {
                Assert.Fail("Caught Exception: {0}, Exception message: {1}", e.GetType().ToString(), e.Message);
            }
        }

        private async Task SmokeTestForNameAPI(CosmosClient client)
        {
            try
            {
                string suffix = Guid.NewGuid().ToString();
                // First to create a ton of named based resource object.
                string databaseId = "SmokeTestForNameAPI" + suffix;
                string collectionId = "collection" + suffix;
                string doc1Id = "document1" + suffix;
                string doc2Id = "document2" + suffix;
                string doc3Id = "document3" + suffix;
                string attachment1Id = "attachment1" + suffix;
                string attachment2Id = "attachment2" + suffix;
                string user1Id = "user1" + suffix;
                string permission1Id = "user1" + suffix;
                string udf1Id = "udf1" + suffix;
                string storedProcedure1Id = "storedProc1" + suffix;
                string trigger1Id = "trigger1" + suffix;
                string conflict1Id = "conflict1" + suffix;

                string resourceRandomId = "randomToDelete" + suffix;
                string resourceRandom2Id = "randomToDelete2" + suffix;

                // Delete database if exist:

                try
                {
                    Cosmos.Database databaseToDelete = await client.GetDatabase(databaseId).DeleteAsync();
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    //swallow
                }
                

                //1. Database CRUD
                Cosmos.Database database = await client.CreateDatabaseAsync(resourceRandomId);
                database = await database.DeleteAsync();

                database = await client.CreateDatabaseAsync(databaseId);
                database = await database.ReadAsync();

                // database = await client.ReadDatabaseByIdPrivateAsync(databaseId, null);

                //2. DocumentCollection CRUD
                Container container = await database.CreateContainerAsync(resourceRandomId, partitionKeyPath: "/id");
                await container.DeleteContainerAsync();

                container = await database.CreateContainerAsync(collectionId, partitionKeyPath: "/id");
                container = await container.ReadContainerAsync();

                // read documentCollection feed.
                FeedIterator<ContainerProperties> rr = database.GetContainerQueryIterator<ContainerProperties>();
                List<ContainerProperties> settings = new List<ContainerProperties>();
                while (rr.HasMoreResults)
                {
                    settings.AddRange(await rr.ReadNextAsync());
                }

                Assert.AreEqual(settings.Count, 1);
                Assert.AreEqual(settings.First().Id, collectionId);

                //3. Document CRUD
                Document doc1;
                {
                    doc1 = await container.CreateItemAsync<Document>(item: new Document() { Id = doc1Id });
                    doc1 = await container.ReadItemAsync<Document>(partitionKey: new Cosmos.PartitionKey(doc1Id), id: doc1Id);
                    Document doc2 = await container.CreateItemAsync<Document>(item: new Document() { Id = doc2Id });
                    Document doc3 = await container.CreateItemAsync<Document>(item: new Document() { Id = doc3Id });

                    // create conflict document
                    try
                    {
                        Document doc1Conflict = await container.CreateItemAsync<Document>(item: new Document() { Id = doc1Id });
                    }
                    catch (CosmosException e)
                    {
                        Assert.AreEqual(e.StatusCode, HttpStatusCode.Conflict, "Must return conflict code");
                    }

                    // 
                    await container.ReplaceItemAsync<dynamic>(id: doc3Id, item: new { id = doc3Id, Description = "test" });
                    try
                    {
                        doc3 = await container.DeleteItemAsync<Document>(partitionKey: new Cosmos.PartitionKey(resourceRandomId), id: resourceRandomId);
                        Assert.Fail();
                    }
                    catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        //swallow
                    }

                    // read databaseCollection feed.
                    using (FeedIterator<dynamic> itemIterator = container.GetItemQueryIterator<dynamic>(queryDefinition: null))
                    {
                        int count = 0;
                        while (itemIterator.HasMoreResults)
                        {
                            FeedResponse<dynamic> items = await itemIterator.ReadNextAsync();
                            count += items.Count();
                        }
                        Assert.AreEqual(3, count);
                    }

                    // query documents 
                    {
                        bool bFound = false;
                        QueryDefinition sqlQueryDefinition = new QueryDefinition("select * from c where c.id = @id").WithParameter("@id", doc1Id);
                        FeedIterator<Document> docServiceQuery = container.GetItemQueryIterator<Document>(
                            queryDefinition: sqlQueryDefinition);
                        while (docServiceQuery.HasMoreResults)
                        {
                            FeedResponse<Document> r = await docServiceQuery.ReadNextAsync();
                            if (r.Count() == 1)
                            {
                                bFound = true;
                                Assert.AreEqual(r.First().Id, doc1Id);
                            }
                        }
                        Assert.AreEqual(bFound, true);
                    }
                    //{
                    //    var docQuery = from book in client.CreateDocumentQuery<Document>(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId))
                    //                   where book.Id == doc1Id
                    //                   select book;

                    //    IEnumerable<Document> enums = docQuery.AsEnumerable();
                    //    if (enums.Count() == 0)
                    //    {
                    //        Assert.Fail("Not found the document " + doc1Id);
                    //    }
                    //    else
                    //    {
                    //        Assert.AreEqual(enums.Single().Id, doc1Id);
                    //    }
                    //}
                    //{
                    //    var docQuery = from book in client.CreateDocumentQuery(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId))
                    //                   where book.Id == doc1Id
                    //                   select book;
                    //    IEnumerable<dynamic> enums = docQuery.AsEnumerable();
                    //    if (enums.Count() == 0)
                    //    {
                    //        Assert.Fail("Not found the document " + doc1Id);
                    //    }
                    //    else
                    //    {
                    //        Assert.AreEqual(enums.Single().Id, doc1Id);
                    //    }
                    //}
                }

                //7. Trigger CRUD
                //{
                //    Trigger mytrigger = new Trigger
                //    {
                //        Id = resourceRandomId,
                //        Body = "function() {var x = 10;}",
                //        TriggerType = TriggerType.Pre,
                //        TriggerOperation = TriggerOperation.All
                //    };

                //    Trigger trigger1 = await client.CreateTriggerAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), mytrigger);
                //    mytrigger.Id = resourceRandom2Id;
                //    trigger1 = await client.ReplaceTriggerAsync(UriFactory.CreateTriggerUri(databaseId, collectionId, resourceRandomId), mytrigger);
                //    trigger1 = await client.DeleteTriggerAsync(UriFactory.CreateTriggerUri(databaseId, collectionId, resourceRandom2Id));

                //    mytrigger.Id = trigger1Id;
                //    trigger1 = await client.CreateTriggerAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), mytrigger);
                //    trigger1 = await client.ReadTriggerAsync(UriFactory.CreateTriggerUri(databaseId, collectionId, trigger1Id));

                //    // 
                //    // read trigger feed.
                //    DoucmentFeedResponse<Trigger> rr = await client.ReadTriggerFeedAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));
                //    Assert.AreEqual(rr.Count, 1);

                //    // query documents 
                //    var docQuery = from book in client.CreateTriggerQuery(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId))
                //                   where book.Id == trigger1Id
                //                   select book;
                //    Assert.AreEqual(docQuery.AsEnumerable().Single().Id, trigger1Id);

                //    bool bFound = false;
                //    IDocumentQuery<dynamic> docServiceQuery = client.CreateTriggerQuery(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId),
                //                string.Format("select * from c where c.id = \"{0}\"", trigger1Id.EscapeForSQL())).AsDocumentQuery();
                //    while (docServiceQuery.HasMoreResults)
                //    {
                //        DoucmentFeedResponse<dynamic> r = await docServiceQuery.ExecuteNextAsync();
                //        if (r.Count == 1)
                //        {
                //            bFound = true;
                //            Assert.AreEqual(r.First()["id"].ToString(), trigger1Id);
                //        }
                //    }
                //    Assert.AreEqual(bFound, true);
                //}

                //8. StoredProcedure CRUD
                {
                    StoredProcedure myStoredProcedure = new StoredProcedure
                    {
                        Id = resourceRandomId,
                        Body = "function() {var x = 10;}",
                    };

                    Scripts scripts = container.Scripts;

                    StoredProcedureProperties storedProcedure1 = await scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(myStoredProcedure.Id, myStoredProcedure.Body));
                    myStoredProcedure.Body = "function() {var x = 5;}";
                    storedProcedure1 = await scripts.ReplaceStoredProcedureAsync(new StoredProcedureProperties(myStoredProcedure.Id, myStoredProcedure.Body));
                    await scripts.DeleteStoredProcedureAsync(myStoredProcedure.Id);

                    storedProcedure1 = await scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(myStoredProcedure.Id, myStoredProcedure.Body));
                    storedProcedure1 = await scripts.ReadStoredProcedureAsync(myStoredProcedure.Id);

                    // 
                    // read databaseCollection feed.
                    FeedIterator<StoredProcedureProperties> storedProcedureIter = scripts.GetStoredProcedureQueryIterator<StoredProcedureProperties>();
                    List<StoredProcedureProperties> storedProcedures = new List<StoredProcedureProperties>();
                    while (storedProcedureIter.HasMoreResults)
                    {
                        storedProcedures.AddRange(await storedProcedureIter.ReadNextAsync());
                    }

                    Assert.AreEqual(storedProcedures.Count, 1);

                    // query documents 
                    //var docQuery = from book in client.CreateStoredProcedureQuery(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId))
                    //               where book.Id == storedProcedure1Id
                    //               select book;
                    //Assert.AreEqual(docQuery.AsEnumerable().Single().Id, storedProcedure1Id);

                    //bool bFound = false;
                    //IDocumentQuery<dynamic> docServiceQuery = client.CreateStoredProcedureQuery(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId),
                    //            string.Format("select * from c where c.id = \"{0}\"", storedProcedure1Id.EscapeForSQL())).AsDocumentQuery();
                    //while (docServiceQuery.HasMoreResults)
                    //{
                    //    DoucmentFeedResponse<dynamic> r = await docServiceQuery.ExecuteNextAsync();
                    //    if (r.Count == 1)
                    //    {
                    //        bFound = true;
                    //        Assert.AreEqual(r.First()["id"].ToString(), storedProcedure1Id);
                    //    }
                    //}
                    //Assert.AreEqual(bFound, true);
                }

                //9. Udf CRUD
                //{
                //    UserDefinedFunction myudf = new UserDefinedFunction
                //    {
                //        Id = resourceRandomId,
                //        Body = "function() {var x = 10;}",
                //    };

                //    UserDefinedFunction udf1 = await client.CreateUserDefinedFunctionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), myudf);
                //    myudf.Id = resourceRandom2Id;
                //    udf1 = await client.ReplaceUserDefinedFunctionAsync(UriFactory.CreateUserDefinedFunctionUri(databaseId, collectionId, resourceRandomId), myudf);
                //    udf1 = await client.DeleteUserDefinedFunctionAsync(UriFactory.CreateUserDefinedFunctionUri(databaseId, collectionId, resourceRandom2Id));

                //    myudf.Id = udf1Id;
                //    udf1 = await client.CreateUserDefinedFunctionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), myudf);
                //    udf1 = await client.ReadUserDefinedFunctionAsync(UriFactory.CreateUserDefinedFunctionUri(databaseId, collectionId, udf1Id));

                //    // 
                //    // read UserDefinedFunction feed.
                //    DoucmentFeedResponse<UserDefinedFunction> rr = await client.ReadUserDefinedFunctionFeedAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));
                //    Assert.AreEqual(rr.Count, 1);

                //    // query UserDefinedFunction 
                //    var docQuery = from book in client.CreateUserDefinedFunctionQuery(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId))
                //                   where book.Id == udf1Id
                //                   select book;
                //    Assert.AreEqual(docQuery.AsEnumerable().Single().Id, udf1Id);

                //    bool bFound = false;
                //    IDocumentQuery<dynamic> docServiceQuery = client.CreateUserDefinedFunctionQuery(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId),
                //                string.Format("select * from c where c.id = \"{0}\"", udf1Id.EscapeForSQL())).AsDocumentQuery();
                //    while (docServiceQuery.HasMoreResults)
                //    {
                //        DoucmentFeedResponse<dynamic> r = await docServiceQuery.ExecuteNextAsync();
                //        if (r.Count == 1)
                //        {
                //            bFound = true;
                //            Assert.AreEqual(r.First()["id"].ToString(), udf1Id);
                //        }
                //    }
                //    Assert.AreEqual(bFound, true);
                //}

                //10. Conflicts CRUD
                //{
                //    Conflict conflict1;
                //    try
                //    {
                //        conflict1 = await client.ReadConflictAsync(UriFactory.CreateConflictUri(databaseId, collectionId, resourceRandom2Id));
                //        Assert.Fail("Should have thrown exception in here");
                //    }
                //    catch (NotFoundException e)
                //    {
                //        Assert.IsNotNull(e.Message);
                //    }
                //    catch (DocumentClientException e)
                //    {
                //        // TODO: Backend return 0 length response back, gateway translate it ServiceUnavailable.
                //        Assert.IsNotNull(e.Message);
                //        Assert.IsNotNull(e.StatusCode == HttpStatusCode.ServiceUnavailable);
                //    }

                //    try
                //    {
                //        conflict1 = await client.DeleteConflictAsync(UriFactory.CreateConflictUri(databaseId, collectionId, resourceRandom2Id));
                //        Assert.Fail("Should have thrown exception in here");
                //    }
                //    catch (NotFoundException e)
                //    {
                //        Assert.IsNotNull(e.Message);
                //    }
                //    catch (DocumentClientException e)
                //    {
                //        // TODO: Backend return 0 length response back, gateway translate it ServiceUnavailable.
                //        Assert.IsNotNull(e.Message);
                //        Assert.IsNotNull(e.StatusCode == HttpStatusCode.ServiceUnavailable);
                //    }

                //    // 
                //    // read conflict feed.
                //    DoucmentFeedResponse<Conflict> rr = await client.ReadConflictFeedAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));
                //    Assert.AreEqual(rr.Count, 0);

                //    // query conflict 
                //    var docQuery = from book in client.CreateConflictQuery(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId))
                //                   select book;

                //    IDocumentQuery<dynamic> docServiceQuery = client.CreateConflictQuery(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId),
                //                string.Format("select * from c")).AsDocumentQuery();
                //    while (docServiceQuery.HasMoreResults)
                //    {
                //        DoucmentFeedResponse<dynamic> r = await docServiceQuery.ExecuteNextAsync();
                //    }
                //}

                // after all finish, delete the databaseAccount.
                await database.DeleteAsync();

            }
            catch (NotFoundException ex)
            {
                Assert.IsNotNull(ex.Message);
                throw;
            }
        }

        [TestMethod]
        public void ReplaceDocumentWithUri()
        {
            CosmosClient client;

            client = TestCommon.CreateCosmosClient(true);
            this.ReplaceDocumentWithUriPrivateAsync(client).Wait();
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            client = TestCommon.CreateClient(false, Protocol.Https);
            this.ReplaceDocumentWithUriPrivateAsync(client).Wait();            

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.ReplaceDocumentWithUriPrivateAsync(client).Wait();
#endif
        }

        private async Task ReplaceDocumentWithUriPrivateAsync(CosmosClient client)
        {
            string databaseId = "db_" + Guid.NewGuid().ToString();
            string collectionId = "coll_" + Guid.NewGuid().ToString();

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            ContainerProperties containerSetting = new ContainerProperties()
            {
                Id = collectionId,
                PartitionKey = partitionKeyDefinition
            };
            // Create database and create collection
            Cosmos.Database database = await client.CreateDatabaseAsync(databaseId);
            Container collection = await database.CreateContainerAsync(containerSetting);

            LinqGeneralBaselineTests.Book myDocument = new LinqGeneralBaselineTests.Book();
            myDocument.Id = Guid.NewGuid().ToString();
            myDocument.Title = "My Book"; //Simple Property.
            myDocument.Languages = new LinqGeneralBaselineTests.Language[] { new LinqGeneralBaselineTests.Language { Name = "English", Copyright = "London Publication" }, new LinqGeneralBaselineTests.Language { Name = "French", Copyright = "Paris Publication" } }; //Array Property
            myDocument.Author = new LinqGeneralBaselineTests.Author { Name = "Don", Location = "France" }; //Complex Property
            myDocument.Price = 9.99;

            await collection.CreateItemAsync<LinqGeneralBaselineTests.Book>(myDocument);

            myDocument.Title = "My new Book";
            // Testing the ReplaceDocumentAsync API with DocumentUri as the parameter
            ItemResponse<LinqGeneralBaselineTests.Book> replacedDocument = await collection.ReplaceItemAsync<LinqGeneralBaselineTests.Book>(myDocument, myDocument.Id);

            QueryRequestOptions options = new QueryRequestOptions() { MaxConcurrency = 1, MaxItemCount = 1 };
            string sqlQueryText = @"select * from root r where r.title = ""My Book""";
            FeedIterator<LinqGeneralBaselineTests.Book> cosmosResultSet = collection.GetItemQueryIterator<LinqGeneralBaselineTests.Book>(queryText: sqlQueryText, requestOptions: options);
            Assert.AreEqual(0, await GetCountFromIterator(cosmosResultSet), "Query Count doesnt match");

            sqlQueryText = @"select * from root r where r.title = ""My new Book""";
            cosmosResultSet = collection.GetItemQueryIterator<LinqGeneralBaselineTests.Book>(queryText: sqlQueryText, requestOptions: options);
            Assert.AreEqual(1, await GetCountFromIterator(cosmosResultSet), "Query Count doesnt match");

            myDocument.Title = "My old Book";
            // Testing the ReplaceDocumentAsync API with Document SelfLink as the parameter
            await collection.ReplaceItemAsync(myDocument, myDocument.Id);

            sqlQueryText = @"select * from root r where r.title = ""My old Book""";
            cosmosResultSet = collection.GetItemQueryIterator<LinqGeneralBaselineTests.Book>(queryText: sqlQueryText, requestOptions: options);
            Assert.AreEqual(1, await GetCountFromIterator(cosmosResultSet), "Query Count doesnt match");
        }

        [TestMethod]
        public async Task CollectionDeleteAndCreateWithSameNameTest()
        {
            // when collection name changes, the collectionName ->Id cache at the gateway need to get invalidated and refreshed.
            // This test is to verify this case is working well.
            DocumentClient client;
            client = TestCommon.CreateClient(true);
            await this.CollectionDeleteAndCreateWithSameNameTestPrivateAsync(client);

#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            client = TestCommon.CreateClient(false, Protocol.Https);
            await this.CollectionDeleteAndCreateWithSameNameTestPrivateAsync(client);
            

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            await this.CollectionDeleteAndCreateWithSameNameTestPrivateAsync(client);
#endif
        }

        private async Task CollectionDeleteAndCreateWithSameNameTestPrivateAsync(DocumentClient client)
        {
            string suffix = Guid.NewGuid().ToString();
            // First to create a ton of named based resource object.
            string databaseId = "CacheRefreshTest" + suffix;
            string collectionId = "collection" + suffix;
            string userId = "user" + suffix;
            string doc1Id = "document1" + suffix;
            string doc2Id = "document2" + suffix;

            // Create database and create collection
            Database database = await client.CreateDatabaseAsync(new Database() { Id = databaseId });
            DocumentCollection collectionDef = new DocumentCollection()
            {
                Id = collectionId,
                PartitionKey = new PartitionKeyDefinition()
                {
                    Paths = new Collection<string>() { "/id" }
                }
            };
            DocumentCollection coll1 = await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(databaseId), collectionDef);
            Document doc1 = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), new Document() { Id = doc1Id });
            Document anotherdoc = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), new Document() { Id = doc2Id });

            // doing a read, which cause the gateway has name->Id cache.
            Document docIgnore = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, doc1Id), 
                new RequestOptions() { PartitionKey = new PartitionKey(doc1Id) });

            // Now delete the collection:
            DocumentCollection collIgnore = await client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));

            // now re-create the collection (same name, with different Rid)
            DocumentCollection coll2 = await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(databaseId), collectionDef);
            Assert.AreNotEqual(coll2.ResourceId, coll1.ResourceId);
            Assert.AreEqual(coll2.Id, coll1.Id);

            Document doc2 = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), new Document() { Id = doc1Id });
            Assert.AreNotEqual(doc2.ResourceId, doc1.ResourceId);

            // Read collection, it should succeed:
            DocumentCollection coll2Temp1 = await client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));
            Assert.AreEqual(coll2Temp1.ResourceId, coll2.ResourceId);

            Document doc2Temp1 = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, doc1Id),
                    new RequestOptions() { PartitionKey = new PartitionKey(doc1Id) });
            Assert.AreEqual(doc2Temp1.ResourceId, doc2.ResourceId);

            //Read Document, it should fail with notFound
            try
            {
                Document doc3 = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, doc2Id), 
                    new RequestOptions() { PartitionKey = new PartitionKey(doc1Id) });
                Assert.Fail("Should have thrown exception in here");
            }
            catch (DocumentClientException e)
            {
                Assert.IsNotNull(e.Message);
                Assert.AreEqual(e.StatusCode, HttpStatusCode.NotFound);
            }

            finally
            {
                TestCommon.DeleteAllDatabasesAsync().Wait();
            }
        }

        /// GatewayNameIdCacheRefresh Test sequence:
        /// 1. Create 15 service and only leave 1 available
        //  2. Create follection dbs/foo/collections/foo
        //  3. Do a read which cause the Name-Id cache in collection.
        //  4. Delete dbs/foo/collections/foo
        /// *************With the service address is same:
        //  5. Recreate dbs/foo/collections/foo 
        ///   Cache still works, because all service address are same.
        /// 6. Read dbs/foo/collections/foo is uneventful.
        /// 7. Delete foo
        /// *************With the service address is different, old is served by bar (return InvalidPartition)
        /// 5. Recreate dbs/foo/collections/bar
        /// 6. Delete one server and recreate dbs/foo/collections/foo
        // 7. Do a foo read, which lookup old Rid, and then old Fabricaddress.
        // 8. Call Fabricaddress, backend transport return 410
        // 9. Comeback at GW, do OldRid fabric lookup, NotFoundException.
        // 10. GoneAndRetry: Set name->id cache refresh. 
        // 11. Now it succeed.
        // 12. Delete both foo and bar
        /// *************With the service address is different, old is not served by anybody (return 410)
        /// 5. Recreate dbs/foo/collections/bar
        /// 6. Delete one server and recreate dbs/foo/collections/foo
        /// 7. DeleteBar
        // 7. Do a read, which lookup old Rid, and then old Fabricaddress.
        // 8. Call Fabricaddress, backend transport return 410
        // 9. Comeback at GW, do OldRid fabric lookup, NotFoundException.
        // 10. GoneAndRetry: Set name->id cache refresh. 
        // 11. Now it succeed.
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator

        [TestMethod]
        public void VerifyGatewayNameIdCacheRefreshDirectHttp()
        {
            DocumentClient client = TestCommon.CreateClient(false, Protocol.Https);
            this.VerifyGatewayNameIdCacheRefreshPrivateAsync(client).Wait();
        }

        [TestMethod]
        public void VerifyGatewayNameIdCacheRefreshDirectTcp()
        {
            DocumentClient client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.VerifyGatewayNameIdCacheRefreshPrivateAsync(client).Wait();
        }
#endif
        [TestMethod]
        public async Task VerifyGatewayNameIdCacheRefreshDirectGateway()
        {
            // This test hits this issue: https://github.com/Azure/azure-documentdb-dotnet/issues/457
            // Ignoring it until this is fixed
            using CosmosClient client = TestCommon.CreateCosmosClient(true);
            await this.VerifyGatewayNameIdCacheRefreshPrivateAsync(client);
        }

        enum FabircServiceReuseType
        {
            // recreate the fabric service using same collection name, everything just works.
            BoundToSameName,
            // recreate the fabric service using different collection name, InvalidParitionException
            BoundToDifferentName,
            // Make the original fabric service bindable, --> Real GoneExceptioni.
            Bindable
        }

        enum CallAPIForStaleCacheTest
        {
            Document,
            DocumentCollection,
        }

        private async Task VerifyGatewayNameIdCacheRefreshPrivateAsync(CosmosClient client)
        {
            Cosmos.Database database = null;
            try
            {
                // Create database and create collection
                database = await client.CreateDatabaseAsync(id : "GatewayNameIdCacheRefresh" + Guid.NewGuid().ToString());

                int collectionsCount = 10;
                Logger.LogLine("Create {0} collections simultaneously.", collectionsCount);
                IList<Container> containers = await this.CreateContainerssAsync(database,
                    collectionsCount - 1);

                await UsingSameFabircServiceTestAsync(database, FabircServiceReuseType.BoundToSameName, null, CallAPIForStaleCacheTest.Document);
                await UsingSameFabircServiceTestAsync(database, FabircServiceReuseType.BoundToSameName, null, CallAPIForStaleCacheTest.DocumentCollection);
                await UsingSameFabircServiceTestAsync(database, FabircServiceReuseType.BoundToDifferentName, containers[0], CallAPIForStaleCacheTest.Document);
                await UsingSameFabircServiceTestAsync(database, FabircServiceReuseType.BoundToDifferentName, containers[1], CallAPIForStaleCacheTest.DocumentCollection);
                await UsingSameFabircServiceTestAsync(database, FabircServiceReuseType.Bindable, containers[2], CallAPIForStaleCacheTest.Document);
                await UsingSameFabircServiceTestAsync(database, FabircServiceReuseType.Bindable, containers[3], CallAPIForStaleCacheTest.DocumentCollection);
            }
            finally
            {
                if (database != null)
                {
                    using ResponseMessage message = await database.DeleteStreamAsync();
                }
            }
        }

        private async Task DeleteContainerIfExistsAsync(Container container)
        {
            try
            {
                await container.DeleteContainerAsync();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                //swallow
            }
        }

        private async Task UsingSameFabircServiceTestAsync(Cosmos.Database database, FabircServiceReuseType type,
            Container collectionToDelete,
            CallAPIForStaleCacheTest eApiTest)
        {
            string suffix = Guid.NewGuid().ToString();
            // First to create a ton of named based resource object.
            string collectionFooId = "collectionFoo" + suffix;
            string collectionBarId = "collectionBar" + suffix;
            string doc1Id = "document1" + suffix;
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
            Container collFoo = await database.CreateContainerAsync( new ContainerProperties() { Id = collectionFooId, PartitionKey = partitionKeyDefinition });
            Document documentDefinition = new Document() { Id = doc1Id };
            documentDefinition.SetPropertyValue("pk", "test");
            Document doc1 = await collFoo.CreateItemAsync<Document>(item: documentDefinition);

            RequestOptions requestOptions = new RequestOptions() { PartitionKey = new PartitionKey("test") };
            // doing a read, which cause the gateway has name->Id cache (collectionFooId -> Rid)
            Document docIgnore = await collFoo.ReadItemAsync<Document>(partitionKey: new Cosmos.PartitionKey("test"), id: doc1Id);

            // Now delete the collection so we have 1 bindable collection left
            //DocumentCollection collIgnore = await client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionFooId));
            await collFoo.DeleteContainerAsync();

            Container collBar = null;
            if (type == FabircServiceReuseType.BoundToSameName)
            {
                // do nothing
            }
            else if (type == FabircServiceReuseType.BoundToDifferentName || type == FabircServiceReuseType.Bindable)
            {
                // Now create collectionBar fist.
                collBar = await database.CreateContainerAsync( new ContainerProperties() { Id = collectionBarId, PartitionKey = partitionKeyDefinition });
                // delete another random collection so we have 1 bindable collection left
                await collBar.DeleteContainerAsync();
            }

            // Now create collectionFoo second time
            Container collFoo2 = await database.CreateContainerAsync( new ContainerProperties() { Id = collectionFooId, PartitionKey = partitionKeyDefinition });

            if (type == FabircServiceReuseType.Bindable)
            {
                await this.DeleteContainerIfExistsAsync(collBar);
            }

            // Now verify the collectionFooId, the cache has collectionFooId -> OldRid cache
            if (eApiTest == CallAPIForStaleCacheTest.DocumentCollection)
            {
                Container collFooRead = await collFoo2.ReadContainerAsync();
            }
            else if (eApiTest == CallAPIForStaleCacheTest.Document)
            {
                documentDefinition = new Document() { Id = "docFoo1Id" + suffix };
                documentDefinition.SetPropertyValue("pk", "test");
                Document docFoo1 = await collFoo.CreateItemAsync<Document>(item: documentDefinition);
                Document docFoo1Back = await collFoo.ReadItemAsync<Document>(partitionKey: new Cosmos.PartitionKey("test"), id: documentDefinition.Id);
            }

            // Now delete the collection foo again
            await collFoo2.DeleteContainerAsync();

            if (type == FabircServiceReuseType.BoundToDifferentName)
            {
                await this.DeleteContainerIfExistsAsync(collBar);
            }
        }

        [TestMethod]
        public void VerifyInvalidPartitionException()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            this.VerifyInvalidPartitionExceptionPrivateAsync(client).Wait();
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            client = TestCommon.CreateClient(false, Protocol.Https);
            this.VerifyInvalidPartitionExceptionPrivateAsync(client).Wait();

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.VerifyInvalidPartitionExceptionPrivateAsync(client).Wait();
#endif
        }

        private async Task VerifyInvalidPartitionExceptionPrivateAsync(DocumentClient client)
        {
            DocumentCollection collection = TestCommon.CreateOrGetDocumentCollection(client);
            await client.CreateDocumentAsync(collection, new Document());
            await client.DeleteDocumentCollectionAsync(collection.SelfLink);

            try
            {
                //Sequence
                // 1. DC -> GW: address resolver to resolve collectionFullName. 
                // 2. GW -> MC: call mc to resolve collectionFullName.
                // 3. MC : return NotFoundException
                await client.ReadDocumentCollectionAsync(collection.AltLink);
            }
            catch (DocumentClientException ex)
            {
                // make sure we throw notFound Exception
                Util.ValidateClientException(ex, HttpStatusCode.NotFound);
                Assert.IsNull(ex.Headers.GetValues(HttpConstants.HttpHeaders.RequestValidationFailure));
            }
        }

        [TestMethod]
        public async Task VerifyInvalidPartitionExceptionWithPopulateQuotaInfo()
        {
            CosmosClient client = TestCommon.CreateCosmosClient(true);
            await this.VerifyInvalidPartitionExceptionWithPopulateQuotaInfo(client);
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            client = TestCommon.CreateClient(false, Protocol.Https);
            await this.VerifyInvalidPartitionExceptionWithPopulateQuotaInfo(client);

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            await this.VerifyInvalidPartitionExceptionWithPopulateQuotaInfo(client);
#endif
        }

        private async Task VerifyInvalidPartitionExceptionWithPopulateQuotaInfo(CosmosClient client)
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            ContainerProperties containerSetting = new ContainerProperties()
            {
                Id = Guid.NewGuid().ToString(),
                PartitionKey = partitionKeyDefinition
            };

            Cosmos.Database cosmosDatabase = await client.CreateDatabaseIfNotExistsAsync(Guid.NewGuid().ToString());
            ContainerResponse containerResponse = await cosmosDatabase.CreateContainerAsync(containerSetting);
            Document documentDefinition = new Document { Id = Guid.NewGuid().ToString() };
            await containerResponse.Container.CreateItemAsync(documentDefinition);
            await containerResponse.Container.DeleteContainerAsync();

            try
            {
                //Sequence
                // 1. DC -> GW: address resolver to resolve collectionFullName. 
                // 2. GW -> MC: call mc to resolve collectionFullName.
                // 3. MC : return NotFoundException
                ContainerResponse readContainerResponse = await containerResponse.Container.ReadContainerAsync(requestOptions: new ContainerRequestOptions { PopulateQuotaInfo = true });
                Assert.IsNull(readContainerResponse.Resource);
                Assert.AreEqual(readContainerResponse.StatusCode, HttpStatusCode.NotFound);
                Assert.IsNull(readContainerResponse.Headers[HttpConstants.HttpHeaders.RequestValidationFailure]);

            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(ex.StatusCode, HttpStatusCode.NotFound);
                string validationFailure;
                ex.TryGetHeader(HttpConstants.HttpHeaders.RequestValidationFailure, out validationFailure);
                Assert.IsNull(validationFailure);
            }
        }

        [TestMethod]
        public void VerifyNameIdCacheTaskReuse()
        {
            CosmosClient client;

            client = TestCommon.CreateCosmosClient(true);
            this.VerifyNameIdCacheTaskReusePrivateAsync(client).Wait();
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            client = TestCommon.CreateClient(false, Protocol.Https);
            this.VerifyNameIdCacheTaskReusePrivateAsync(client).Wait();

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.VerifyNameIdCacheTaskReusePrivateAsync(client).Wait();
#endif
        }

        private async Task VerifyNameIdCacheTaskReusePrivateAsync(CosmosClient client)
        {
            string suffix = Guid.NewGuid().ToString();
            // First to create a ton of named based resource object.
            string databaseId = "VerifyNameIdCacheTask" + suffix;
            string collectionId = "collection" + suffix;
            string docId = "doc" + suffix;
            // Create database and create collection
            Cosmos.Database database = await client.CreateDatabaseAsync(databaseId);
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            ContainerProperties containerSetting = new ContainerProperties()
            {
                Id = collectionId,
                PartitionKey = partitionKeyDefinition
            };
            Container container = await database.CreateContainerAsync(containerSetting);
            try
            {
                ItemResponse<Document> docIgnore = await container.ReadItemAsync<Document>(docId, new Cosmos.PartitionKey(docId));
                Assert.IsNull(docIgnore.Resource);
                Assert.AreEqual(docIgnore.StatusCode, HttpStatusCode.NotFound);
            }
            catch (CosmosException e)
            {
                // without client validation.
                Assert.IsNotNull(e.Message);
                Assert.AreEqual(e.StatusCode, HttpStatusCode.NotFound);
            }


            Document doc1 = await container.CreateItemAsync<Document>(new Document() { Id = docId });

            Document docIgnore1 = await container.ReadItemAsync<Document>(docId, new Cosmos.PartitionKey(docId));
        }

        [TestMethod]
        public void CrazyNameTest()
        {
            CosmosClient client;

            client = TestCommon.CreateCosmosClient(true);
            this.CrazyNameTestPrivateAsync(client, true).Wait();

#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator

            client = TestCommon.CreateClient(false, Protocol.Https);
            this.CrazyNameTestPrivateAsync(client, false).Wait();

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.CrazyNameTestPrivateAsync(client, false, true).Wait(); 
#endif
        }

        private async Task CrazyNameTestPrivateAsync(CosmosClient client, bool useGateway, bool useTcp = false)
        {
            await TestCommon.DeleteAllDatabasesAsync();

            // Try longest name, note if the name is unicode, the number of character available might become less.
            string longestName = "Try longest name 253. At general availability, DocumentDB will be available in three standard performance levels: S1, S2, and S3, Vibhor Kapoor, director of product marketing for Azure, wrote in a blog post today. Collections of data within a DocumentDB database can be assigned to different performance levels, allowing customers to purchase only the performance they need";
            longestName = longestName.Substring(0, 253);
            if (longestName.EndsWith(" "))
            {
                longestName = longestName.Remove(longestName.Length - 1, 1) + "=";
            }

            string allCrazyChars = "“”!@$%^&*()-~`_[]{}|;':,.<>第67届奥斯卡トサカ";

            // Test #1: Try name for only the document resource
            // all following character supported in all transport
            List<string> crazyNameSupportList = new List<string>(new string[]{
                                      "   startwithSpace",
                                      "!@$%^&*()-=",
                                      "~`_[]{}|;':,.<>",
                                      "Contains \" character",
                                      "<>==<<<<<<==",
                                      //Chinese
                                      "第67届奥斯卡颁奖典礼是美国电影艺术与科学学院旨在奖励1994年最优秀电影的一场晚会", 
                                      // Japanese
                                      "トサカハゲミツスイは、スズメ目ミツスイ科に属する鳥類の1種である。", 
                                      // Hindi
                                      "नालापत बालमणि अम्मा भारत से मलयालम भाषा की प्रतिभावान कवयित्रियों में",
                                      // Arabic
                                      "وغالباً ما يعرف اختصاراً باسم إيه سي ميلان أو الميلان فقط، هو نادي كرة قدم إيطالي محترف، تأسس بتاريخ 1", 
                                      // Russian
                                      "Свято-Никольский монастырь (ранее широко известен как Средне-Никольский монастырь)", 
                                      // Korean
                                      "라부아지에의 새로운 연소 이론은 산소와 연관된 여러 가지 반응에 적용되었으며 호흡,",
                                      // Spanish
                                      "La República Soviética Húngara fue un efímero régimen de dictadura del proletariado en Hungría, instaurad",
                                      // Vietnamese
                                      "Mọi người đều có thể biên tập bài ngay lập tức, chỉ cần nhớ vài quy tắc. Có sẵn rất nhiều trang trợ giúp",
                                      // Papua New Guinea Official languages
                                      "ر اچیچا فوجی سمان چکن آلا جہاز اے۔ ایہ 50 سالاں توں ہن تک 50 توں زیادہ دیساں دے ورتن وچ اے۔ اینوں امریکی",
                                      longestName,
                                      allCrazyChars,
                                      "Contains + character",
                        });

            string suffix = Guid.NewGuid().ToString();
            // First to create a ton of named based resource object.
            string databaseId = "CrazyNameTest" + suffix;
            string collectionId = "collection" + suffix;

            Cosmos.Database database = await client.CreateDatabaseAsync(databaseId);
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
            ContainerProperties containerSetting = new ContainerProperties()
            {
                Id = collectionId,
                PartitionKey = partitionKeyDefinition
            };
            Container coll1 = await database.CreateContainerAsync(containerSetting);

            foreach (string documentId in crazyNameSupportList)
            {
                Document documentDefinition = new Document() { Id = documentId };
                documentDefinition.SetPropertyValue("pk", "test");
                Document doc1 = await coll1.CreateItemAsync<Document>(documentDefinition);

                // and then read it!
                Document docIgnore = await coll1.ReadItemAsync<Document>(documentId, new Cosmos.PartitionKey("test"), null);
                Assert.AreEqual(docIgnore.Id, documentId);
            }

            await database.DeleteAsync();
            // Test #2: Try name for all resources
            List<string> nameList = new List<string>(new string[]{
                                    longestName,
                                    allCrazyChars,
                                    "Here", // it is special because it is value 3 char offer resouceId,
                                    "bvYI", // another offer resourceId
                        });
            foreach (string crazyName in nameList)
            {
                Cosmos.Database db = await client.CreateDatabaseAsync(crazyName);
                containerSetting = new ContainerProperties()
                {
                    Id = crazyName,
                    PartitionKey = partitionKeyDefinition
                };
                Container coll = await db.CreateContainerAsync(containerSetting);
                Document documentDefinition = new Document() { Id = crazyName };
                documentDefinition.SetPropertyValue("pk", "test");
                Document doc = await coll.CreateItemAsync<Document>(documentDefinition);

                await db.DeleteAsync();
            }

            await TestCommon.DeleteAllDatabasesAsync();
        }


        [TestMethod]
        public void NameRoutingBadUrlTest()
        {
            CosmosClient client;

            client = TestCommon.CreateCosmosClient(true);
            this.NameRoutingBadUrlTestPrivateAsync(client, false).Wait();
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            client = TestCommon.CreateClient(false, Protocol.Https);
            this.NameRoutingBadUrlTestPrivateAsync(client, false).Wait();

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.NameRoutingBadUrlTestPrivateAsync(client, false, true).Wait();
#endif
        }

        private async Task NameRoutingBadUrlTestPrivateAsync(CosmosClient client, bool bypassClientValidation, bool useTcp = false)
        {
            string suffix = Guid.NewGuid().ToString();
            // First to create a ton of named based resource object.
            string databaseId = $"BadUrlTest" + suffix;
            string collectionId = "collection" + suffix;
            string doc1Id = "document1" + suffix;

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            ContainerProperties containerSetting = new ContainerProperties()
            {
                Id = collectionId,
                PartitionKey = partitionKeyDefinition
            };
            // Create database and create collection
            Cosmos.Database database = await client.CreateDatabaseAsync(databaseId);
            Container coll = await database.CreateContainerAsync(containerSetting);
            Document doc1 = await coll.CreateItemAsync<Document>(new Document { Id = doc1Id });

            try
            {
                // the url doesn't conform to the schema at at all.
                ItemResponse<Document> response = await coll.ReadItemAsync<Document>("dba/what/colltions/abc", new Cosmos.PartitionKey(doc1Id));
            }            
            catch (CosmosException e)
            {
                // without client validation.
                Assert.IsNotNull(e.Message);
                Assert.AreEqual(e.StatusCode, HttpStatusCode.NotFound);
            }

            try
            {
                // the url doesn't conform to the schema at at all.
                ItemResponse<Document> response = await coll.ReadItemAsync<Document>("dbs/what/colltions/abc", new Cosmos.PartitionKey(doc1Id));
            }
            catch (CosmosException e)
            {
                Assert.IsNotNull(e.Message);
                Assert.AreEqual(e.StatusCode, HttpStatusCode.NotFound);
            }

            try
            {
                // doing a document read with collection link
                ItemResponse<Document> response = await coll.ReadItemAsync<Document>(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId).ToString(), new Cosmos.PartitionKey(doc1Id));
                Assert.IsNull(response.Resource);
                Assert.AreEqual(response.StatusCode, HttpStatusCode.NotFound);
            }
            catch (CosmosException e)
            {
                Assert.IsNotNull(e.Message);
                Assert.IsTrue(e.StatusCode == HttpStatusCode.BadRequest || e.StatusCode == HttpStatusCode.Unauthorized || e.StatusCode == HttpStatusCode.NotFound);
            }

            try
            {
                // doing a collection read with Document link
                ContainerResponse collection1 = await database.GetContainer(UriFactory.CreateDocumentUri(databaseId, collectionId, doc1Id).ToString()).ReadContainerAsync();
            }
            catch (CosmosException e)
            {
                Assert.IsNotNull(e.Message);
                Assert.IsTrue(e.StatusCode == HttpStatusCode.BadRequest || e.StatusCode == HttpStatusCode.Unauthorized || e.StatusCode == HttpStatusCode.NotFound);
            }
            finally
            {
                await database.DeleteAsync();
            }
        }

        [TestMethod]
        [Ignore] //TODO once V3 SDK have validation on item id, make this test active
        public void VerifyInvalidNameTest()
        {
            CosmosClient client = TestCommon.CreateCosmosClient(true);
            this.VerifyInvalidNameTestPrivateAsync(client).Wait();
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            client = TestCommon.CreateClient(false, Protocol.Https);
            this.VerifyInvalidNameTestPrivateAsync(client).Wait();

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.VerifyInvalidNameTestPrivateAsync(client).Wait();
#endif

        }

        private async Task VerifyInvalidNameTestPrivateAsync(CosmosClient client)
        {
            try
            {
                Cosmos.Database database = await client.CreateDatabaseAsync("abcdef=se123");
                Assert.Fail("Should have thrown exception in here");
            }
            catch (CosmosException e)
            {
                Assert.AreEqual(e.StatusCode, HttpStatusCode.BadRequest);
                Assert.IsTrue(e.Message.Contains("contains invalid character"));
            }

            string[] forbiddenCharInNameList = {
                                      "Contains / character",
                                      "Contains # character",
                                      "Contains \\ character",
                                      "Contains ? character",
                                      "endWithSpace  ",
                            };

            string suffix = Guid.NewGuid().ToString();
            // First to create a ton of named based resource object.
            string databaseId = "VerifyInvalidNameTest" + suffix;
            string collectionId = "VerifyInvalidNameTest" + suffix;

            // Create database and create collection
            Cosmos.Database database1 = await client.CreateDatabaseAsync(databaseId);
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            ContainerProperties containerSetting = new ContainerProperties()
            {
                Id = collectionId,
                PartitionKey = partitionKeyDefinition
            };
            Container coll1 = await database1.CreateContainerAsync(containerSetting);

            // create should fail.
            foreach (string resourceName in forbiddenCharInNameList)
            {
                try
                {
                    Document document = await coll1.CreateItemAsync<Document>(new Document() { Id = resourceName });
                    Assert.Fail("Should have thrown exception in here");
                }
                catch (ArgumentException e)
                {
                    Assert.IsTrue(e.Message.Contains("invalid character") || e.Message.Contains("end with space"));
                }
            }

            // replace should fail
            Document documentDefinition = new Document() { Id = Guid.NewGuid().ToString() };
            Document documentCreated = await coll1.CreateItemAsync(documentDefinition);
            foreach (string resourceName in forbiddenCharInNameList)
            {
                try
                {
                    documentCreated.Id = resourceName;
                    Document document = await coll1.ReplaceItemAsync<Document>(documentCreated, documentCreated.Id);
                    Assert.Fail("Should have thrown exception in here");
                }
                catch (ArgumentException e)
                {
                    Assert.IsTrue(e.Message.Contains("invalid character") || e.Message.Contains("end with space"));
                }
            }
        }

        [TestMethod]
        public void NameParsingTest()
        {
            bool isFeed = false;
            string resourceType;
            string resourceIdorFullName;
            bool isNameBased = false;

            string suffix = Guid.NewGuid().ToString();
            // First to create a ton of named based resource object.
            string databaseId = "database" + suffix;
            string collectionId = "collection" + suffix;
            string doc1Id = "document1" + suffix;
            string doc2Id = "document2" + suffix;
            string doc3Id = "document3" + suffix;
            string attachment1Id = "attachment1" + suffix;
            string attachment2Id = "attachment2" + suffix;
            string user1Id = "user1" + suffix;
            string permission1Id = "user1" + suffix;

            Uri uri = UriFactory.CreateDatabaseUri(databaseId);
            Uri baseuri = new Uri("http://localhost");
            bool tryParse;

            tryParse = PathsHelper.TryParsePathSegments(uri.OriginalString, out isFeed, out resourceType, out resourceIdorFullName, out isNameBased);
            Assert.IsTrue(tryParse);
            Assert.IsTrue(isNameBased);
            Assert.IsFalse(isFeed);
            Assert.IsTrue(resourceType == "dbs");
            Assert.IsTrue(resourceIdorFullName == "dbs/" + databaseId);

            tryParse = PathsHelper.TryParsePathSegments(new Uri(baseuri, uri).PathAndQuery, out isFeed, out resourceType, out resourceIdorFullName, out isNameBased);
            Assert.IsTrue(tryParse);
            Assert.IsTrue(isNameBased);
            Assert.IsFalse(isFeed);
            Assert.IsTrue(resourceType == "dbs");
            Assert.IsTrue(resourceIdorFullName == "dbs/" + databaseId);

            tryParse = PathsHelper.TryParsePathSegments(new Uri(baseuri, uri).AbsolutePath, out isFeed, out resourceType, out resourceIdorFullName, out isNameBased);
            Assert.IsTrue(tryParse);
            Assert.IsTrue(isNameBased);
            Assert.IsFalse(isFeed);
            Assert.IsTrue(resourceType == "dbs");

            // media/xxx is always Id based
            // alEBAMZlTQABAAAAAAAAACnfXFUB (storageIndex = 1) so it is not valid resourceId but a valid mediaId
            tryParse = PathsHelper.TryParsePathSegments(new Uri(baseuri, new Uri("media/alEBAMZlTQABAAAAAAAAACnfXFUB", UriKind.Relative)).AbsolutePath, out isFeed, out resourceType, out resourceIdorFullName, out isNameBased);
            Assert.IsTrue(tryParse);
            Assert.IsFalse(isNameBased);
            Assert.IsFalse(isFeed);
            Assert.IsTrue(resourceType == "media");
            Assert.IsTrue(resourceIdorFullName == "alEBAMZlTQABAAAAAAAAACnfXFUB");

            DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Unknown, new Uri("http://localhost/dbs/asdf asfasdf/colls/abcdddwer"), AuthorizationTokenType.PrimaryMasterKey, null);
            Assert.IsFalse(request.IsFeed);
            Assert.IsTrue(request.ResourceType == ResourceType.Collection);
            Assert.IsTrue(request.ResourceAddress == "dbs/asdf asfasdf/colls/abcdddwer");

            request = DocumentServiceRequest.Create(OperationType.ReadFeed, ResourceType.Unknown, new Uri("http://localhost/dbs/asdf asfasdf/colls/abcdddwer/docs"), AuthorizationTokenType.PrimaryMasterKey, null);
            Assert.IsTrue(request.IsFeed);
            Assert.IsTrue(request.ResourceType == ResourceType.Document);
            Assert.IsTrue(request.ResourceAddress == "dbs/asdf asfasdf/colls/abcdddwer");
        }

#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
        [TestMethod]
        [TestCategory("Ignore")]
        public void VerifyMasterNodeThrottlingDirectHttp()
        {
            DocumentClient client = TestCommon.CreateClient(false, Protocol.Https);
            this.VerifyMasterNodeThrottlingPrivateAsync(client).Wait();
        }        

        [TestMethod]
        public void VerifyMasterNodeThrottlingDirectTcp()
        {
            DocumentClient client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.VerifyMasterNodeThrottlingPrivateAsync(client).Wait();
        }
#endif
        [TestMethod]
        public async Task VerifyNameBasedCollectionCRUDOperations()
        {
            CosmosClient client = TestCommon.CreateCosmosClient(true);
            await this.VerifyNameBasedCollectionCRUDOperationsAsync(client);
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            client = TestCommon.CreateClient(false, Protocol.Https);
            await this.VerifyNameBasedCollectionCRUDOperationsAsync(client);

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            await this.VerifyNameBasedCollectionCRUDOperationsAsync(client);
#endif
        }

        /// <summary>
        /// Tests that partition key definition cache is refreshed when collection is recreated.
        /// The test just ensures that client retries and completes successfully.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestPartitionKeyDefinitionOnCollectionRecreate()
        {
            await this.TestPartitionKeyDefinitionOnCollectionRecreate(TestCommon.CreateClient(true));
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestPartitionKeyDefinitionOnCollectionRecreate(TestCommon.CreateClient(false, protocol: Protocol.Tcp));
            await this.TestPartitionKeyDefinitionOnCollectionRecreate(TestCommon.CreateClient(false, protocol: Protocol.Https));
#endif
        }

        internal async Task TestPartitionKeyDefinitionOnCollectionRecreate(DocumentClient client)
        {
            await TestCommon.DeleteAllDatabasesAsync();
            await client.CreateDatabaseAsync(new Database { Id = "db1" });
            PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field1" }), Kind = PartitionKind.Hash };
            await TestCommon.CreateCollectionAsync(client, "/dbs/db1", new DocumentCollection { Id = "coll1", PartitionKey = partitionKeyDefinition1 });
            Document document1 = new Document { Id = "doc1" };
            document1.SetPropertyValue("field1", 1);
            await client.CreateDocumentAsync("/dbs/db1/colls/coll1", document1);

            DocumentClient otherClient = TestCommon.CreateClient(false);
            await otherClient.DeleteDocumentCollectionAsync("/dbs/db1/colls/coll1");
            PartitionKeyDefinition partitionKeyDefinition2 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field2" }), Kind = PartitionKind.Hash };
            await TestCommon.CreateCollectionAsync(otherClient, "/dbs/db1", new DocumentCollection { Id = "coll1", PartitionKey = partitionKeyDefinition2 });

            Document document2 = new Document { Id = "doc1" };
            document2.SetPropertyValue("field2", 1);
            await client.CreateDocumentAsync("/dbs/db1/colls/coll1", document2);
        }

        /// <summary>
        /// Tests that container cache is refreshed when container is recreated.
        /// The test just ensures that client retries and completes successfully.
        /// It verifies case when original and new container have different partition key path.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestPartitionKeyDefinitionOnContainerRecreateFromDifferentPartitionKeyPath()
        {
            await this.TestPartitionKeyDefinitionOnContainerRecreateFromDifferentPartitionKeyPath(TestCommon.CreateCosmosClient(true));
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestPartitionKeyDefinitionOnContainerRecreateFromDifferentPartitionKeyPath(TestCommon.CreateClient(false, protocol: Protocol.Tcp));
            await this.TestPartitionKeyDefinitionOnContainerRecreateFromDifferentPartitionKeyPath(TestCommon.CreateClient(false, protocol: Protocol.Https));
#endif
        }

        internal async Task TestPartitionKeyDefinitionOnContainerRecreateFromDifferentPartitionKeyPath(CosmosClient client)
        {
            await TestCommon.DeleteAllDatabasesAsync();
            Cosmos.Database database = null;
            try
            {
                database = await client.CreateDatabaseAsync("db1");
                PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field1" }), Kind = PartitionKind.Hash };
                Container container = await database.CreateContainerAsync(new ContainerProperties { Id = "coll1", PartitionKey = partitionKeyDefinition1 });
                Document document1 = new Document { Id = "doc1" };
                document1.SetPropertyValue("field1", 1);
                await container.CreateItemAsync(document1);

                CosmosClient otherClient = TestCommon.CreateCosmosClient(false);
                database = otherClient.GetDatabase("db1");
                container = database.GetContainer("coll1");
                await container.DeleteContainerAsync();
                PartitionKeyDefinition partitionKeyDefinition2 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field2" }), Kind = PartitionKind.Hash };
                container = await database.CreateContainerAsync(new ContainerProperties { Id = "coll1", PartitionKey = partitionKeyDefinition2 });

                Document document2 = new Document { Id = "doc1" };
                document2.SetPropertyValue("field2", 1);
                container = client.GetDatabase("db1").GetContainer("coll1");
                await container.CreateItemAsync(document2);
            }
            finally
            {
                if(database != null)
                {
                    await database.DeleteAsync();
                }
            }
        }

        [TestMethod]
        public async Task TestInvalidPartitionKeyException()
        {
            RequestHandlerHelper testHandler = new RequestHandlerHelper();
            int createItemCount = 0;
            string partitionKey = null;
            testHandler.UpdateRequestMessage = (requestMessage) =>
            {
                if (requestMessage.ResourceType == ResourceType.Document)
                {
                    createItemCount++;
                    string pk = requestMessage.Headers.PartitionKey;
                    Assert.AreNotEqual(partitionKey, pk, $"Same PK value should not be sent again. PK value:{pk}");
                    partitionKey = pk;
                }
            };

            using (CosmosClient client = TestCommon.CreateCosmosClient((builder) => builder.AddCustomHandlers(testHandler)))
            {
                Cosmos.Database database = null;
                try
                {
                    database = await client.CreateDatabaseAsync("TestInvalidPartitionKey" + Guid.NewGuid().ToString());
                    Container container = await database.CreateContainerAsync(id: "coll1", partitionKeyPath: "/doesnotexist");
                    ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();

                    ItemResponse<ToDoActivity> response = await container.CreateItemAsync(toDoActivity, partitionKey: new Cosmos.PartitionKey(toDoActivity.pk));
                    Assert.Fail("Create item should fail with wrong partition key value");
                }
                catch (CosmosException ce)
                {
                    Assert.AreEqual(HttpStatusCode.BadRequest, ce.StatusCode);
                    Assert.AreEqual(SubStatusCodes.PartitionKeyMismatch, (SubStatusCodes)ce.SubStatusCode);
                }
                finally
                {
                    if (database != null)
                    {
                        await database.DeleteAsync();
                    }
                }

                Assert.AreEqual(1, createItemCount, $"Request should use the custom handler, and it should only be used once. Count {createItemCount}");
            }
        }

        /// <summary>
        /// Tests that collection cache is refreshed when collection is recreated.
        /// The test just ensures that client retries and completes successfully for query - request which doesn't target single partition key.
        /// It verifies case when original collection is not partitioned and new collection is partitioned.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Ignore /* TODO: This tests throws a "The read session is not available for the input session token" */]
        public async Task TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitionedForQuery()
        {
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitionedForQuery(TestCommon.CreateClient(true));
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitionedForQuery(TestCommon.CreateClient(false, protocol: Protocol.Tcp));
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitionedForQuery(TestCommon.CreateClient(false, protocol: Protocol.Https));
#endif
        }

        internal async Task TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitionedForQuery(DocumentClient client)
        {
            await TestCommon.DeleteAllDatabasesAsync();
            await client.CreateDatabaseAsync(new Database { Id = "db1" });
            await TestCommon.CreateCollectionAsync(client, "/dbs/db1", new DocumentCollection { Id = "coll1" });
            Document document1 = new Document { Id = "doc1" };
            document1.SetPropertyValue("field1", 1);
            await client.CreateDocumentAsync("/dbs/db1/colls/coll1", document1);
            Assert.AreEqual(1, client.CreateDocumentQuery("/dbs/db1/colls/coll1", "SELECT * FROM c WHERE c.field1 IN (1, 2)", new FeedOptions { EnableCrossPartitionQuery = true }).AsEnumerable().Count());

            DocumentClient otherClient = TestCommon.CreateClient(false);
            await otherClient.DeleteDocumentCollectionAsync("/dbs/db1/colls/coll1");
            PartitionKeyDefinition partitionKeyDefinition2 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field2" }), Kind = PartitionKind.Hash };
            DocumentCollection coll = await TestCommon.CreateCollectionAsync(otherClient, "/dbs/db1", new DocumentCollection { Id = "coll1", PartitionKey = partitionKeyDefinition2 }, new RequestOptions { OfferThroughput = 12000 });

            DocumentClient directClient = TestCommon.CreateClient(false);
            string sessionToken1 = (await directClient.CreateDocumentAsync("/dbs/db1/colls/coll1", document1)).SessionToken;
            document1 = new Document { Id = "doc2" };
            document1.SetPropertyValue("field1", 2);
            string sessionToken2 = (await directClient.CreateDocumentAsync("/dbs/db1/colls/coll1", document1)).SessionToken;

            // Both documents are expected to land at the same partition, which is not first one.
            Assert.AreEqual(sessionToken1.Split(':')[0], sessionToken2.Split(':')[0]);
            Range<string> fullRange = new Range<string>(
               PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
               PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
               true,
               false);

            PartitionKeyRangeCache routingMapProvider = await client.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            Assert.AreNotEqual(sessionToken1.Split(':')[0], (await routingMapProvider.TryGetOverlappingRangesAsync(coll.ResourceId, fullRange, NoOpTrace.Singleton)).First().Id);

            Assert.AreEqual(2, client.CreateDocumentQuery("/dbs/db1/colls/coll1", "SELECT * FROM c WHERE c.field1 IN (1, 2)", new FeedOptions { EnableCrossPartitionQuery = true }).AsEnumerable().Count());

            DocumentClient newClient = TestCommon.CreateClient(false);
            Assert.AreEqual(2, newClient.CreateDocumentQuery("/dbs/db1/colls/coll1", "SELECT * FROM c WHERE c.field1 IN (1, 2)", new FeedOptions { EnableCrossPartitionQuery = true }).AsEnumerable().Count());
        }

        /// <summary>
        /// Tests that collection cache is refreshed when collection is recreated.
        /// For parallel query first request after collection recreated will fail with NotFound, because it doesn't know how to retry.
        /// But consequent queries will succeed.
        /// The test just ensures that client retries and completes successfully for query - request which doesn't target single partition key.
        /// It verifies case when original collection is not partitioned and new collection is partitioned.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Ignore /* TODO: This tests throws a "The read session is not available for the input session token" */]
        public async Task TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitionedForParallelQuery()
        {
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitionedForParallelQuery(TestCommon.CreateClient(true));
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitionedForParallelQuery(TestCommon.CreateClient(false, protocol: Protocol.Tcp));
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitionedForParallelQuery(TestCommon.CreateClient(false, protocol: Protocol.Https));
#endif
        }

        internal async Task TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitionedForParallelQuery(DocumentClient client)
        {
            await TestCommon.DeleteAllDatabasesAsync();
            await client.CreateDatabaseAsync(new Database { Id = "db1" });
            await TestCommon.CreateCollectionAsync(client, "/dbs/db1", new DocumentCollection { Id = "coll1" });
            Document document1 = new Document { Id = "doc1" };
            document1.SetPropertyValue("field1", 1);
            await client.CreateDocumentAsync("/dbs/db1/colls/coll1", document1);
            Assert.AreEqual(1, client.CreateDocumentQuery("/dbs/db1/colls/coll1", "SELECT * FROM c WHERE c.field1 IN (1, 2)", new FeedOptions { EnableCrossPartitionQuery = true }).AsEnumerable().Count());

            DocumentClient otherClient = TestCommon.CreateClient(false);
            await otherClient.DeleteDocumentCollectionAsync("/dbs/db1/colls/coll1");
            PartitionKeyDefinition partitionKeyDefinition2 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field2" }), Kind = PartitionKind.Hash };
            DocumentCollection coll = await TestCommon.CreateCollectionAsync(otherClient, "/dbs/db1", new DocumentCollection { Id = "coll1", PartitionKey = partitionKeyDefinition2 }, new RequestOptions { OfferThroughput = 12000 });

            DocumentClient directClient = TestCommon.CreateClient(false);
            string sessionToken1 = (await directClient.CreateDocumentAsync("/dbs/db1/colls/coll1", document1)).SessionToken;
            document1 = new Document { Id = "doc2" };
            document1.SetPropertyValue("field1", 2);
            string sessionToken2 = (await directClient.CreateDocumentAsync("/dbs/db1/colls/coll1", document1)).SessionToken;

            // Both documents are expected to land at the same partition, which is not first one.
            Assert.AreEqual(sessionToken1.Split(':')[0], sessionToken2.Split(':')[0]);
            Range<string> fullRange = new Range<string>(
               PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
               PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
               true,
               false);

            PartitionKeyRangeCache routingMapProvider = await client.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            Assert.AreNotEqual(sessionToken1.Split(':')[0], (await routingMapProvider.TryGetOverlappingRangesAsync(coll.ResourceId, fullRange, NoOpTrace.Singleton)).First().Id);

            Assert.AreEqual(2, client.CreateDocumentQuery("/dbs/db1/colls/coll1", "SELECT * FROM c WHERE c.field1 IN (1, 2)", new FeedOptions { EnableCrossPartitionQuery = true }).AsEnumerable().Count());

            DocumentClient newClient = TestCommon.CreateClient(false);
            Assert.AreEqual(2, newClient.CreateDocumentQuery("/dbs/db1/colls/coll1", "SELECT * FROM c WHERE c.field1 IN (1, 2)", new FeedOptions { EnableCrossPartitionQuery = true }).AsEnumerable().Count());
        }

        /// <summary>
        /// If collection is created with multiple partitions, cross partition query is processing nth partition,
        /// then we create collection with one partition but same name, queyr must fail with NotFound.
        /// </summary>
        /// <returns></returns>
        [Ignore]
        [TestMethod]
        public async Task TestCollectionRecreateFromMultipartitionToSinglePartitionedForQuery()
        {
            await this.TestCollectionRecreateFromMultipartitionToSinglePartitionedForQuery(TestCommon.CreateClient(true));
            await this.TestCollectionRecreateFromMultipartitionToSinglePartitionedForQuery(TestCommon.CreateClient(false, protocol: Protocol.Tcp));
            await this.TestCollectionRecreateFromMultipartitionToSinglePartitionedForQuery(TestCommon.CreateClient(false, protocol: Protocol.Https));
        }

        internal async Task TestCollectionRecreateFromMultipartitionToSinglePartitionedForQuery(DocumentClient client)
        {
            await TestCommon.DeleteAllDatabasesAsync();
            await client.CreateDatabaseAsync(new Database { Id = "db1" });
            PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field2" }) };
            await TestCommon.CreateCollectionAsync(client, "/dbs/db1", new DocumentCollection { Id = "coll1", PartitionKey = partitionKeyDefinition1 }, new RequestOptions { OfferThroughput = 12000 });

            Document document1 = new Document { Id = "doc1" };
            document1.SetPropertyValue("field1", 1);
            await client.CreateDocumentAsync("/dbs/db1/colls/coll1", document1);

            var query = client.CreateDocumentQuery("/dbs/db1/colls/coll1", "SELECT * FROM c", new FeedOptions { EnableCrossPartitionQuery = true }).AsDocumentQuery();
            await query.ExecuteNextAsync();
            await query.ExecuteNextAsync();
            var result = await query.ExecuteNextAsync();
            Assert.AreEqual("2", result.SessionToken.Split(':')[0], result.SessionToken);

            DocumentClient otherClient = TestCommon.CreateClient(false);
            await otherClient.DeleteDocumentCollectionAsync("/dbs/db1/colls/coll1");
            PartitionKeyDefinition partitionKeyDefinition2 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field2" }) };
            await TestCommon.CreateCollectionAsync(otherClient, "/dbs/db1", new DocumentCollection { Id = "coll1", PartitionKey = partitionKeyDefinition2 });

            try
            {
                await query.ExecuteNextAsync();
                Assert.Fail("Expected exception");
            }
            catch (DocumentClientException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }
        }

        /// <summary>
        /// Tests that routing to non-existent range throws PartitionKeyRangeGoneException even after collection recreate.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestRouteToNonExistentRangeAfterCollectionRecreate()
        {
            await this.TestRouteToNonExistentRangeAfterCollectionRecreate(TestCommon.CreateCosmosClient(true));
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestRouteToNonExistentRangeAfterCollectionRecreate(TestCommon.CreateClient(false, protocol: Protocol.Tcp));
            await this.TestRouteToNonExistentRangeAfterCollectionRecreate(TestCommon.CreateClient(false, protocol: Protocol.Https));
#endif
        }

        internal async Task TestRouteToNonExistentRangeAfterCollectionRecreate(CosmosClient client)
        {
            const int partitionCount = 5;
            const int federationDefaultRUsPerPartition = 6000;
            await TestCommon.DeleteAllDatabasesAsync();
            Cosmos.Database database = null;
            try
            {
                database = await client.CreateDatabaseAsync("db1");
                PartitionKeyDefinition pKDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field1" }), Kind = PartitionKind.Hash };
                Container container = await database.CreateContainerAsync(containerProperties: new ContainerProperties { Id = "coll1", PartitionKey = pKDefinition }, throughput: partitionCount * federationDefaultRUsPerPartition);

                ContainerInternal containerCore = (ContainerInlineCore)container;
                CollectionRoutingMap collectionRoutingMap = await containerCore.GetRoutingMapAsync(default(CancellationToken));

                Assert.AreEqual(partitionCount, collectionRoutingMap.OrderedPartitionKeyRanges.Count());
            } finally
            {
                if (database != null)
                {
                    await database.DeleteAsync();
                }
            }

            //PartitionKeyRangeId is not supported reed feed from V3 SDK onwards
            //Document document1 = new Document { Id = "doc1" };
            //document1.SetPropertyValue("field1", 1);
            //await client.CreateDocumentAsync("/dbs/db1/colls/coll1", document1);

            //try
            //{
            //    await client.ReadDocumentFeedAsync("/dbs/db1/colls/coll1", new FeedOptions { PartitionKeyRangeId = "foo" });
            //    Assert.Fail();
            //}
            //catch (DocumentClientException ex)
            //{
            //    Assert.AreEqual(HttpStatusCode.Gone, ex.StatusCode);
            //    Assert.AreEqual(SubStatusCodes.PartitionKeyRangeGone, ex.GetSubStatus());
            //}

            //DocumentClient otherClient = TestCommon.CreateClient(false);
            //await otherClient.DeleteDocumentCollectionAsync("/dbs/db1/colls/coll1");
            //await TestCommon.CreateCollectionAsync(otherClient, "/dbs/db1", new DocumentCollection { Id = "coll1", PartitionKey = partitionKeyDefinition1 });

            //try
            //{
            //    await client.ReadDocumentFeedAsync("/dbs/db1/colls/coll1", new FeedOptions { PartitionKeyRangeId = ranges[ranges.Count - 1].Id });
            //    Assert.Fail();
            //}
            //catch (DocumentClientException ex)
            //{
            //    Assert.AreEqual(HttpStatusCode.Gone, ex.StatusCode);
            //    Assert.AreEqual(SubStatusCodes.PartitionKeyRangeGone, ex.GetSubStatus());
            //}

            //await client.ReadDocumentFeedAsync("/dbs/db1/colls/coll1", new FeedOptions { PartitionKeyRangeId = ranges[0].Id });
        }

        /// <summary>
        /// Tests that routing to non-existent range throws PartitionKeyRangeGoneException even after collection recreate.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestRouteToExistentRangeAfterCollectionRecreate()
        {
            await this.TestRouteToExistentRangeAfterCollectionRecreate(TestCommon.CreateCosmosClient(true));
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestRouteToExistentRangeAfterCollectionRecreate(TestCommon.CreateClient(false, protocol: Protocol.Tcp));
            await this.TestRouteToExistentRangeAfterCollectionRecreate(TestCommon.CreateClient(false, protocol: Protocol.Https));
#endif
        }

        internal async Task TestRouteToExistentRangeAfterCollectionRecreate(CosmosClient client)
        {
            const int partitionCount = 5;
            const int federationDefaultRUsPerPartition = 6000;
            await TestCommon.DeleteAllDatabasesAsync();
            Cosmos.Database database = null;
            try
            {
                database = await client.CreateDatabaseAsync("db1");
                PartitionKeyDefinition pKDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field1" }), Kind = PartitionKind.Hash };
                Container container = await database.CreateContainerAsync(containerProperties: new ContainerProperties { Id = "coll1", PartitionKey = pKDefinition }, throughput: partitionCount * federationDefaultRUsPerPartition);

                ContainerInternal containerCore = (ContainerInlineCore)container;
                CollectionRoutingMap collectionRoutingMap = await containerCore.GetRoutingMapAsync(default(CancellationToken));

                Assert.AreEqual(partitionCount, collectionRoutingMap.OrderedPartitionKeyRanges.Count());
            }
            finally
            {
                if (database != null)
                {
                    await database.DeleteAsync();
                }
            }
            //PartitionKeyRangeId is not supported reed feed from V3 SDK onwards
            //await client.CreateDatabaseAsync(new Database { Id = "db1" });
            //await TestCommon.CreateCollectionAsync(client, "/dbs/db1", new DocumentCollection { Id = "coll1" });

            //Document document1 = new Document { Id = "doc1" };
            //document1.SetPropertyValue("field1", 1);
            //await client.CreateDocumentAsync("/dbs/db1/colls/coll1", document1);

            //try
            //{
            //    await client.ReadDocumentFeedAsync("/dbs/db1/colls/coll1", new FeedOptions { PartitionKeyRangeId = "4" });
            //    Assert.Fail();
            //}
            //catch (DocumentClientException ex)
            //{
            //    Assert.AreEqual(HttpStatusCode.Gone, ex.StatusCode);
            //    Assert.AreEqual(SubStatusCodes.PartitionKeyRangeGone, ex.GetSubStatus());
            //}

            //DocumentClient otherClient = TestCommon.CreateClient(false);
            //await otherClient.DeleteDocumentCollectionAsync("/dbs/db1/colls/coll1");
            //PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field1" }), Kind = PartitionKind.Hash };
            //DocumentCollection collection = await TestCommon.CreateCollectionAsync(otherClient, "/dbs/db1", new DocumentCollection { Id = "coll1", PartitionKey = partitionKeyDefinition1 }, new RequestOptions { OfferThroughput = 12000 });
            //var partitionKeyRangeCache = await client.GetPartitionKeyRangeCacheAsync();
            //var ranges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
            //    collection.ResourceId,
            //    new Range<string>(
            //        PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
            //        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
            //        true,
            //        false));

            //Assert.AreEqual(5, ranges.Count());

            //await client.ReadDocumentFeedAsync("/dbs/db1/colls/coll1", new FeedOptions { PartitionKeyRangeId = "4" });
        }

        /// <summary>
        /// Tests that collection cache is refreshed when collection is recreated.
        /// The test just ensures that client retries and completes successfully for query - request which doesn't target single partition key.
        /// It verifies case when original collection is partitioned and new collection is not partitioned.
        /// </summary>
        /// <returns></returns>
        [Ignore]
        [TestMethod]
        public async Task TestPartitionKeyDefinitionOnCollectionRecreateFromPartitionedToNonPartitionedForQuery()
        {
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromPartitionedToNonPartitionedForQuery(TestCommon.CreateClient(true));
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromPartitionedToNonPartitionedForQuery(TestCommon.CreateClient(false, protocol: Protocol.Tcp));
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromPartitionedToNonPartitionedForQuery(TestCommon.CreateClient(false, protocol: Protocol.Https));
#endif
        }

        internal async Task TestPartitionKeyDefinitionOnCollectionRecreateFromPartitionedToNonPartitionedForQuery(DocumentClient client)
        {
            await TestCommon.DeleteAllDatabasesAsync();
            await client.CreateDatabaseAsync(new Database { Id = "db1" });
            PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field1" }), Kind = PartitionKind.Hash };
            await TestCommon.CreateCollectionAsync(client, "/dbs/db1", new DocumentCollection { Id = "coll1", PartitionKey = partitionKeyDefinition1 }, new RequestOptions { OfferThroughput = 12000 });
            Document document1 = new Document { Id = "doc1" };
            document1.SetPropertyValue("field1", 1);
            await client.CreateDocumentAsync("/dbs/db1/colls/coll1", document1);
            Assert.AreEqual(1, client.CreateDocumentQuery("/dbs/db1/colls/coll1", "SELECT * FROM c WHERE c.field1 IN (1, 2)", new FeedOptions { EnableCrossPartitionQuery = true })
            .AsEnumerable()
            .Count());

            DocumentClient otherClient = TestCommon.CreateClient(false);
            await otherClient.DeleteDocumentCollectionAsync("/dbs/db1/colls/coll1");
            await TestCommon.CreateCollectionAsync(otherClient, "/dbs/db1", new DocumentCollection { Id = "coll1" });

            DocumentClient directClient = TestCommon.CreateClient(false);
            await directClient.CreateDocumentAsync("/dbs/db1/colls/coll1", document1);
            document1 = new Document { Id = "doc2" };
            document1.SetPropertyValue("field1", 2);
            await directClient.CreateDocumentAsync("/dbs/db1/colls/coll1", document1);

            //todo:elasticcollections We don't need to set EnableCrosspartitionQuery here, but because it is checked in frontend
            //if cache is stale, it will wrongly classify query as crosspartition.
            client.CreateDocumentQuery("/dbs/db1/colls/coll1", "SELECT * FROM c WHERE c.field1 IN (1, 2)", new FeedOptions { EnableCrossPartitionQuery = true })
                .AsEnumerable()
                .ToList();

            Assert.AreEqual(
                    2, client.CreateDocumentQuery("/dbs/db1/colls/coll1", "SELECT * FROM c WHERE c.field1 IN (1, 2)")
                        .AsEnumerable()
                        .Count());
        }

        /// <summary>
        /// Tests that partition key definition cache is refreshed when container is recreated.
        /// The test just ensures that Gateway successfully creates script when its partitionkeydefinition cache is outdated..
        /// It verifies case when original container and new container have different partition key path.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestScriptCreateOnContainerRecreateFromDifferentPartitionKeyPath()
        {
            await this.TestScriptCreateOnContainerRecreateFromDifferentPartitionKeyPath(TestCommon.CreateCosmosClient(true));
        }

        internal async Task TestScriptCreateOnContainerRecreateFromDifferentPartitionKeyPath(CosmosClient client)
        {
            await TestCommon.DeleteAllDatabasesAsync();
            Cosmos.Database database = null;
            try
            {
                database = await client.CreateDatabaseAsync("db1");
                PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field1" }), Kind = PartitionKind.Hash };
                Container container = await database.CreateContainerAsync(new ContainerProperties { Id = "coll1", PartitionKey = partitionKeyDefinition1 });
                Document document1 = new Document { Id = "doc1" };
                document1.SetPropertyValue("field1", 1);
                await container.CreateItemAsync(document1);

                CosmosClient otherClient = TestCommon.CreateCosmosClient(false);
                database = otherClient.GetDatabase("db1");
                container = database.GetContainer("coll1");
                await container.DeleteContainerAsync();
                PartitionKeyDefinition partitionKeyDefinition2 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field2" }), Kind = PartitionKind.Hash };
                container = await database.CreateContainerAsync(new ContainerProperties { Id = "coll1", PartitionKey = partitionKeyDefinition2 });

                container = client.GetDatabase("db1").GetContainer("coll1");
                Scripts scripts = container.Scripts;
                StoredProcedureProperties storedProcedure = await scripts.CreateStoredProcedureAsync(new StoredProcedureProperties("sproc1", "function() {return 1}"));
                for (int i = 0; i < 10; i++)
                {
                    await scripts.ExecuteStoredProcedureAsync<object>(
                        storedProcedureId: "sproc1", 
                        partitionKey: new Cosmos.PartitionKey(i),  
                        parameters: null);
                }
            }
            finally
            {
                if (database != null)
                {
                    await database.DeleteAsync();
                }
            }
        }

        private async Task VerifyNameBasedCollectionCRUDOperationsAsync(CosmosClient client)
        {
            try
            {
                await TestCommon.DeleteAllDatabasesAsync();

                // Scenario 1: name based collection read.

                Cosmos.Database database = await client.CreateDatabaseAsync("ValidateNameBasedCollectionCRUDOperations_DB");
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
                ContainerProperties containerSetting = new ContainerProperties()
                {
                    Id = "ValidateNameBasedCollectionCRUDOperations_COLL",
                    PartitionKey = partitionKeyDefinition
                };

                Container collection = await database.CreateContainerAsync(containerSetting);
                Uri collectionUri = UriFactory.CreateDocumentCollectionUri(database.Id, collection.Id);

                var payload = new
                {
                    id = "id_" + Guid.NewGuid().ToString(),
                    Author = "Author_" + Guid.NewGuid().ToString(),
                };
                await collection.CreateItemAsync(payload);

                // Update collection.
                containerSetting.Id = collection.Id;
                containerSetting.IndexingPolicy.IncludedPaths.Add(new Cosmos.IncludedPath { Path = "/" });
                containerSetting.IndexingPolicy.ExcludedPaths.Add(new Cosmos.ExcludedPath { Path = "/\"Author\"/?" });
                await collection.ReplaceContainerAsync(containerSetting);

                // Read collection.
                ContainerResponse containerResponse = await collection.ReadContainerAsync(requestOptions: new ContainerRequestOptions { PopulateQuotaInfo = true });
                Assert.IsTrue(int.Parse(containerResponse.Headers[HttpConstants.HttpHeaders.CollectionIndexTransformationProgress], CultureInfo.InvariantCulture) >= 0);

                // Delete and re-create the collection with the same name.
                await collection.DeleteContainerAsync();
                collection = await database.CreateContainerAsync(containerSetting);

                // Read the new collection.
                // The gateway's cache is stale at this point. This test verifies that the gateway should be able to refresh the cache and returns the response.
                containerResponse = await collection.ReadContainerAsync(requestOptions: new ContainerRequestOptions { PopulateQuotaInfo = true });
                Assert.AreEqual(100, int.Parse(containerResponse.Headers[HttpConstants.HttpHeaders.CollectionIndexTransformationProgress], CultureInfo.InvariantCulture));

                // Scenario 2: name based collection put.

                payload = new
                {
                    id = "Id_" + Guid.NewGuid().ToString(),
                    Author = "Author_" + Guid.NewGuid().ToString(),
                };
                await collection.CreateItemAsync(payload);

                // Delete and re-create the collection with the same name.
                await collection.DeleteContainerAsync();
                collection = await database.CreateContainerAsync(containerSetting);

                // Update collection.
                containerSetting = new ContainerProperties()
                {
                    Id = collection.Id,
                    PartitionKey = partitionKeyDefinition
                };
                containerSetting.IndexingPolicy.IncludedPaths.Add(new Cosmos.IncludedPath { Path = "/" });
                containerSetting.IndexingPolicy.ExcludedPaths.Add(new Cosmos.ExcludedPath { Path = "/\"Author\"/?" });
                await collection.ReplaceContainerAsync(containerSetting);
            }
            finally
            {
                TestCommon.DeleteAllDatabasesAsync().Wait();
            }
        }

        /// <summary>
        /// Document client retrieves collection before sending document creation request,
        /// while pre-populates collection cache in gateway.
        /// This method sends document creation request directly, so we can test that collection
        /// cache doesn't issue too many requests.
        /// </summary>
        private async Task<ResourceResponse<Document>> CreateDocumentAsync(
            DocumentClient client,
            string collectionLink,
            Document document)
        {
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Create,
                collectionLink,
                document,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey,
                new StoreRequestNameValueCollection(),
                SerializationFormattingPolicy.None))
            {
                request.Headers[HttpConstants.HttpHeaders.PartitionKey] = PartitionKeyInternal.Empty.ToJsonString();

                return new ResourceResponse<Document>(await client.CreateAsync(request, null));
            }
        }

        private async Task DeleteAllDatabaseAsync(DocumentClient client)
        {
            IList<Database> databases = TestCommon.RetryRateLimiting(() => TestCommon.ListAll<Database>(client, null));
            Logger.LogLine("Number of database to delete {0}", databases.Count);

            foreach (Database db in databases)
            {
                await client.DeleteDatabaseAsync(db);
            }
        }

        private async Task<T> AsyncRetryRateLimiting<T>(Func<Task<T>> work)
        {
            return await TestCommon.AsyncRetryRateLimiting<T>(work);
        }

        private async Task<IList<Container>> CreateContainerssAsync(Cosmos.Database database, int numberOfCollectionsPerDatabase)
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            IList<Container> containers = new List<Container>();
            if (numberOfCollectionsPerDatabase > 0)
            {
                for (int i = 0; i < numberOfCollectionsPerDatabase; ++i)
                {
                    containers.Add(await AsyncRetryRateLimiting(() => database.CreateContainerAsync(new ContainerProperties { Id = Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition })));
                }
            }
            return containers;
        }

        private async Task<int> GetCountFromIterator<T>(FeedIterator<T> iterator)
        {
            int count = 0;
            while (iterator.HasMoreResults)
            {
                FeedResponse<T> countiter = await iterator.ReadNextAsync();
                count += countiter.Count();

            }
            return count;
        }
    }
}
