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
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests;
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
                CosmosDatabase databaseToDelete = await client.Databases[databaseId].DeleteAsync();

                //1. Database CRUD
                CosmosDatabase database = await client.Databases.CreateDatabaseAsync(resourceRandomId);
                database = await database.DeleteAsync();

                database = await client.Databases.CreateDatabaseAsync(databaseId );
                database = await database.ReadAsync();

                // database = await client.ReadDatabaseByIdPrivateAsync(databaseId, null);

                //2. DocumentCollection CRUD
                CosmosContainer container = await database.Containers.CreateContainerAsync(resourceRandomId, partitionKeyPath: "/id");
                await container.DeleteAsync();

                container = await database.Containers.CreateContainerAsync(collectionId, partitionKeyPath: "/id");
                container = await container.ReadAsync();

                // read documentCollection feed.
                CosmosResultSetIterator<CosmosContainerSettings> rr = database.Containers.GetContainerIterator();
                List<CosmosContainerSettings> settings = new List<CosmosContainerSettings>();
                while (rr.HasMoreResults)
                {
                    settings.AddRange(await rr.FetchNextSetAsync());
                }

                Assert.AreEqual(settings.Count, 1);
                Assert.AreEqual(settings.First().Id, collectionId);

                //3. Document CRUD
                Document doc1;
                {
                    doc1 = await container.Items.CreateItemAsync<Document>(partitionKey: doc1Id, item: new Document() { Id = doc1Id });
                    doc1 = await container.Items.ReadItemAsync<Document>(partitionKey: doc1Id, id: doc1Id);
                    Document doc2 = await container.Items.CreateItemAsync<Document>(partitionKey: doc2Id, item: new Document() { Id = doc2Id });
                    Document doc3 = await container.Items.CreateItemAsync<Document>(partitionKey: doc3Id, item: new Document() { Id = doc3Id });

                    // create conflict document
                    try
                    {
                        Document doc1Conflict = await container.Items.CreateItemAsync<Document>(partitionKey: doc1Id, item: new Document() { Id = doc1Id });
                    }
                    catch (CosmosException e)
                    {
                        Assert.AreEqual(e.StatusCode, HttpStatusCode.Conflict, "Must return conflict code");
                    }

                    // 
                    await container.Items.ReplaceItemAsync<dynamic>(partitionKey: doc3Id, id: doc3Id, item: new { id = doc3Id, Description = "test" });
                    doc3 = await container.Items.DeleteItemAsync<Document>(partitionKey: resourceRandomId, id: resourceRandomId);

                    // read databaseCollection feed.
                    CosmosResultSetIterator<dynamic> itemIterator = container.Items.GetItemIterator<dynamic>();
                    int count = 0;
                    while (itemIterator.HasMoreResults)
                    {
                        CosmosQueryResponse<dynamic> items = await itemIterator.FetchNextSetAsync();
                        count += items.Count();
                    }
                    Assert.AreEqual(3, count);

                    // query documents 
                    {
                        bool bFound = false;
                        CosmosSqlQueryDefinition sqlQueryDefinition = new CosmosSqlQueryDefinition("select * from c where c.id = @id").UseParameter("@id", doc1Id);
                        CosmosResultSetIterator<Document> docServiceQuery = container.Items.CreateItemQuery<Document>(
                            sqlQueryDefinition: sqlQueryDefinition,
                            partitionKey: doc1.Id);
                        while (docServiceQuery.HasMoreResults)
                        {
                            CosmosQueryResponse<Document> r = await docServiceQuery.FetchNextSetAsync();
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
                //    FeedResponse<Trigger> rr = await client.ReadTriggerFeedAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));
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
                //        FeedResponse<dynamic> r = await docServiceQuery.ExecuteNextAsync();
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

                    CosmosStoredProcedure storedProcedure1 = await container.StoredProcedures.CreateStoredProcedureAsync(id: myStoredProcedure.Id, body: myStoredProcedure.Body);
                    myStoredProcedure.Body = "function() {var x = 5;}";
                    storedProcedure1 = await storedProcedure1.ReplaceAsync(body: myStoredProcedure.Body);
                    storedProcedure1 = await storedProcedure1.DeleteAsync();

                    storedProcedure1 = await container.StoredProcedures.CreateStoredProcedureAsync(id: myStoredProcedure.Id, body: myStoredProcedure.Body);
                    storedProcedure1 = await storedProcedure1.ReadAsync();

                    // 
                    // read databaseCollection feed.
                    CosmosResultSetIterator<CosmosStoredProcedureSettings> storedProcedureIter = container.StoredProcedures.GetStoredProcedureIterator();
                    List<CosmosStoredProcedureSettings> storedProcedures = new List<CosmosStoredProcedureSettings>();
                    while (storedProcedureIter.HasMoreResults)
                    {
                        storedProcedures.AddRange(await storedProcedureIter.FetchNextSetAsync());
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
                    //    FeedResponse<dynamic> r = await docServiceQuery.ExecuteNextAsync();
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
                //    FeedResponse<UserDefinedFunction> rr = await client.ReadUserDefinedFunctionFeedAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));
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
                //        FeedResponse<dynamic> r = await docServiceQuery.ExecuteNextAsync();
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
                //    FeedResponse<Conflict> rr = await client.ReadConflictFeedAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));
                //    Assert.AreEqual(rr.Count, 0);

                //    // query conflict 
                //    var docQuery = from book in client.CreateConflictQuery(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId))
                //                   select book;

                //    IDocumentQuery<dynamic> docServiceQuery = client.CreateConflictQuery(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId),
                //                string.Format("select * from c")).AsDocumentQuery();
                //    while (docServiceQuery.HasMoreResults)
                //    {
                //        FeedResponse<dynamic> r = await docServiceQuery.ExecuteNextAsync();
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
            DocumentClient client;

            client = TestCommon.CreateClient(true);
            this.ReplaceDocumentWithUriPrivateAsync(client).Wait();
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            client = TestCommon.CreateClient(false, Protocol.Https);
            this.ReplaceDocumentWithUriPrivateAsync(client).Wait();            

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.ReplaceDocumentWithUriPrivateAsync(client).Wait();
#endif
        }

        private async Task ReplaceDocumentWithUriPrivateAsync(DocumentClient client)
        {
            string databaseId = "db_" + Guid.NewGuid().ToString();
            string collectionId = "coll_" + Guid.NewGuid().ToString();

            // Create database and create collection
            Database database = await client.CreateDatabaseAsync(new Database() { Id = databaseId });
            DocumentCollection collection = await TestCommon.CreateCollectionAsync(client, database.SelfLink, new DocumentCollection() { Id = collectionId });

            LinqGeneralBaselineTests.Book myDocument = new LinqGeneralBaselineTests.Book();
            myDocument.Id = Guid.NewGuid().ToString();
            myDocument.Title = "My Book"; //Simple Property.
            myDocument.Languages = new LinqGeneralBaselineTests.Language[] { new LinqGeneralBaselineTests.Language { Name = "English", Copyright = "London Publication" }, new LinqGeneralBaselineTests.Language { Name = "French", Copyright = "Paris Publication" } }; //Array Property
            myDocument.Author = new LinqGeneralBaselineTests.Author { Name = "Don", Location = "France" }; //Complex Property
            myDocument.Price = 9.99;

            await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), myDocument);

            myDocument.Title = "My new Book";
            // Testing the ReplaceDocumentAsync API with DocumentUri as the parameter
            Document replacedDocument = await client.ReplaceDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, myDocument.Id), myDocument);

            IQueryable<LinqGeneralBaselineTests.Book> docQuery = from book in client.CreateDocumentQuery<LinqGeneralBaselineTests.Book>(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId))
                                                                 where book.Title == "My Book"
                                                                 select book;
            Assert.AreEqual(0, docQuery.AsEnumerable().Count(), "Query Count doesnt match");

            docQuery = from book in client.CreateDocumentQuery<LinqGeneralBaselineTests.Book>(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId))
                       where book.Title == "My new Book"
                       select book;
            Assert.AreEqual(1, docQuery.AsEnumerable().Count(), "Query Count doesnt match");

            myDocument.Title = "My old Book";
            // Testing the ReplaceDocumentAsync API with Document SelfLink as the parameter
            await client.ReplaceDocumentAsync(replacedDocument.SelfLink, myDocument);

            docQuery = from book in client.CreateDocumentQuery<LinqGeneralBaselineTests.Book>(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId))
                       where book.Title == "My old Book"
                       select book;
            Assert.AreEqual(1, docQuery.AsEnumerable().Count(), "Query Count doesnt match");
        }

        [TestMethod]
        [Ignore /* TODO: This tests throws a "The read session is not available for the input session token" */]
        public void CollectionDeleteAndCreateWithSameNameTest()
        {
            // when collection name changes, the collectionName ->Id cache at the gateway need to get invalidated and refreshed.
            // This test is to verify this case is working well.
            DocumentClient client;
            client = TestCommon.CreateClient(true);
            this.CollectionDeleteAndCreateWithSameNameTestPrivateAsync(client).Wait();

#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            client = TestCommon.CreateClient(false, Protocol.Https);
            this.CollectionDeleteAndCreateWithSameNameTestPrivateAsync(client).Wait();
            

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.CollectionDeleteAndCreateWithSameNameTestPrivateAsync(client).Wait();
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
            DocumentCollection coll1 = await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(databaseId), new DocumentCollection() { Id = collectionId });
            Document doc1 = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), new Document() { Id = doc1Id });
            Document anotherdoc = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), new Document() { Id = doc2Id });

            // doing a read, which cause the gateway has name->Id cache.
            Document docIgnore = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, doc1Id));

            // Now delete the collection:
            DocumentCollection collIgnore = await client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));

            // now re-create the collection (same name, with different Rid)
            DocumentCollection coll2 = await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(databaseId), new DocumentCollection() { Id = collectionId });
            Assert.AreNotEqual(coll2.ResourceId, coll1.ResourceId);
            Assert.AreEqual(coll2.Id, coll1.Id);

            Document doc2 = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), new Document() { Id = doc1Id });
            Assert.AreNotEqual(doc2.ResourceId, doc1.ResourceId);

            // Read collection, it should succeed:
            DocumentCollection coll2Temp1 = await client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));
            Assert.AreEqual(coll2Temp1.ResourceId, coll2.ResourceId);

            Document doc2Temp1 = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, doc1Id));
            Assert.AreEqual(doc2Temp1.ResourceId, doc2.ResourceId);

            //Read Document, it should fail with notFound
            try
            {
                Document doc3 = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, doc2Id));
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
        [Ignore]
        public void VerifyGatewayNameIdCacheRefreshDirectGateway()
        {
            // This test hits this issue: https://github.com/Azure/azure-documentdb-dotnet/issues/457
            // Ignoring it until this is fixed
            DocumentClient client = TestCommon.CreateClient(true);
            this.VerifyGatewayNameIdCacheRefreshPrivateAsync(client).Wait();
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

        private async Task VerifyGatewayNameIdCacheRefreshPrivateAsync(DocumentClient client)
        {
            Database database = null;
            try
            {
                // Create database and create collection
                database = await client.CreateDatabaseAsync(new Database() { Id = "GatewayNameIdCacheRefresh" + Guid.NewGuid().ToString() });

                int collectionsCount = 10;
                Logger.LogLine("Create {0} collections simultaneously.", collectionsCount);
                IList<DocumentCollection> collections = await this.CreateCollectionsAsync(client,
                    database,
                    collectionsCount - 1);

                await UsingSameFabircServiceTestAsync(database.Id, client, FabircServiceReuseType.BoundToSameName, null, CallAPIForStaleCacheTest.Document);
                await UsingSameFabircServiceTestAsync(database.Id, client, FabircServiceReuseType.BoundToSameName, null, CallAPIForStaleCacheTest.DocumentCollection);
                await UsingSameFabircServiceTestAsync(database.Id, client, FabircServiceReuseType.BoundToDifferentName, collections[0], CallAPIForStaleCacheTest.Document);
                await UsingSameFabircServiceTestAsync(database.Id, client, FabircServiceReuseType.BoundToDifferentName, collections[1], CallAPIForStaleCacheTest.DocumentCollection);
                await UsingSameFabircServiceTestAsync(database.Id, client, FabircServiceReuseType.Bindable, collections[2], CallAPIForStaleCacheTest.Document);
                await UsingSameFabircServiceTestAsync(database.Id, client, FabircServiceReuseType.Bindable, collections[3], CallAPIForStaleCacheTest.DocumentCollection);
            }
            finally
            {
                if(database != null)
                {
                    await client.DeleteDatabaseAsync(database);
                }
            }
        }

        private async Task UsingSameFabircServiceTestAsync(string databaseId, DocumentClient client, FabircServiceReuseType type,
            DocumentCollection collectionToDelete,
            CallAPIForStaleCacheTest eApiTest)
        {
            string suffix = Guid.NewGuid().ToString();
            // First to create a ton of named based resource object.
            string collectionFooId = "collectionFoo" + suffix;
            string collectionBarId = "collectionBar" + suffix;
            string doc1Id = "document1" + suffix;

            DocumentCollection collFoo = await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(databaseId), new DocumentCollection() { Id = collectionFooId });
            Document doc1 = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionFooId), new Document() { Id = doc1Id });

            // doing a read, which cause the gateway has name->Id cache (collectionFooId -> Rid)
            Document docIgnore = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionFooId, doc1Id));

            // Now delete the collection so we have 1 bindable collection left
            DocumentCollection collIgnore = await client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionFooId));

            DocumentCollection collBar = null;
            if (type == FabircServiceReuseType.BoundToSameName)
            {
                // do nothing
            }
            else if (type == FabircServiceReuseType.BoundToDifferentName || type == FabircServiceReuseType.Bindable)
            {
                // Now create collectionBar fist.
                collBar = await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(databaseId), new DocumentCollection() { Id = collectionBarId });
                // delete another random collection so we have 1 bindable collection left
                await client.DeleteDocumentCollectionAsync(collectionToDelete);
            }

            // Now create collectionFoo second time
            DocumentCollection collFoo2 = await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(databaseId), new DocumentCollection() { Id = collectionFooId });

            if (type == FabircServiceReuseType.Bindable)
            {
                await client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionBarId));
            }

            // Now verify the collectionFooId, the cache has collectionFooId -> OldRid cache
            if (eApiTest == CallAPIForStaleCacheTest.DocumentCollection)
            {
                DocumentCollection collFooRead = await client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionFooId));
            }
            else if (eApiTest == CallAPIForStaleCacheTest.Document)
            {
                string docFoo1Id = "docFoo1Id" + suffix;
                Document docFoo1 = await client.CreateDocumentAsync(collFoo2.SelfLink, new Document() { Id = docFoo1Id });
                Document docFoo1Back = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionFooId, docFoo1Id));
            }

            // Now delete the collection foo again
            await client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionFooId));

            if (type == FabircServiceReuseType.BoundToDifferentName)
            {
                await client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionBarId));
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
            DocumentClient client = TestCommon.CreateClient(true);
            await this.VerifyInvalidPartitionExceptionWithPopulateQuotaInfo(client);
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            client = TestCommon.CreateClient(false, Protocol.Https);
            await this.VerifyInvalidPartitionExceptionWithPopulateQuotaInfo(client);

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            await this.VerifyInvalidPartitionExceptionWithPopulateQuotaInfo(client);
#endif
        }

        private async Task VerifyInvalidPartitionExceptionWithPopulateQuotaInfo(DocumentClient client)
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
                await client.ReadDocumentCollectionAsync(collection.AltLink, new RequestOptions { PopulateQuotaInfo = true });
            }
            catch (DocumentClientException ex)
            {
                // make sure we throw notFound Exception
                Util.ValidateClientException(ex, HttpStatusCode.NotFound);
                Assert.IsNull(ex.Headers.GetValues(HttpConstants.HttpHeaders.RequestValidationFailure));
            }
        }

        [TestMethod]
        public void VerifyNameIdCacheTaskReuse()
        {
            DocumentClient client;

            client = TestCommon.CreateClient(true);
            this.VerifyNameIdCacheTaskReusePrivateAsync(client).Wait();
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            client = TestCommon.CreateClient(false, Protocol.Https);
            this.VerifyNameIdCacheTaskReusePrivateAsync(client).Wait();

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.VerifyNameIdCacheTaskReusePrivateAsync(client).Wait();
#endif
        }

        private async Task VerifyNameIdCacheTaskReusePrivateAsync(DocumentClient client)
        {
            string suffix = Guid.NewGuid().ToString();
            // First to create a ton of named based resource object.
            string databaseId = "VerifyNameIdCacheTask" + suffix;
            string collectionId = "collection" + suffix;
            string docId = "doc" + suffix;

            try
            {
                Document docIgnore = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, docId));
                Assert.Fail("Should have thrown exception in here");
            }
            catch (DocumentClientException e)
            {
                // without client validation.
                Assert.IsNotNull(e.Message);
                Assert.AreEqual(e.StatusCode, HttpStatusCode.NotFound);
            }

            // Create database and create collection
            Database database = await client.CreateDatabaseAsync(new Database() { Id = databaseId });
            DocumentCollection coll1 = await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(databaseId), new DocumentCollection() { Id = collectionId });
            Document doc1 = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), new Document() { Id = docId });

            Document docIgnore1 = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, docId));
        }

        [TestMethod]
        public void CrazyNameTest()
        {
            DocumentClient client;

            client = TestCommon.CreateClient(true);
            this.CrazyNameTestPrivateAsync(client, true).Wait();

#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator

            client = TestCommon.CreateClient(false, Protocol.Https);
            this.CrazyNameTestPrivateAsync(client, false).Wait();

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.CrazyNameTestPrivateAsync(client, false, true).Wait(); 
#endif
        }

        private async Task CrazyNameTestPrivateAsync(DocumentClient client, bool useGateway, bool useTcp = false)
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

            Database database = await client.CreateDatabaseAsync(new Database() { Id = databaseId });
            DocumentCollection coll1 = await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(databaseId), new DocumentCollection() { Id = collectionId });

            foreach (string documentId in crazyNameSupportList)
            {
                Document doc1 = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), new Document() { Id = documentId });

                // and then read it!
                Document docIgnore = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, documentId));
                Assert.AreEqual(docIgnore.Id, documentId);
            }

            await client.DeleteDatabaseAsync(database.AltLink);
            // Test #2: Try name for all resources
            List<string> nameList = new List<string>(new string[]{
                                    longestName,
                                    allCrazyChars,
                                    "Here", // it is special because it is value 3 char offer resouceId,
                                    "bvYI", // another offer resourceId
                        });
            foreach (string crazyName in nameList)
            {
                Database db = await client.CreateDatabaseAsync(new Database() { Id = crazyName });
                DocumentCollection coll = await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(crazyName), new DocumentCollection() { Id = crazyName });
                Document doc = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(crazyName, crazyName), new Document() { Id = crazyName });

                await client.DeleteDatabaseAsync(db.AltLink);
            }

            await TestCommon.DeleteAllDatabasesAsync();
        }


        [TestMethod]
        public void NameRoutingBadUrlTest()
        {
            DocumentClient client;

            client = TestCommon.CreateClient(true);
            this.NameRoutingBadUrlTestPrivateAsync(client, false).Wait();
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            client = TestCommon.CreateClient(false, Protocol.Https);
            this.NameRoutingBadUrlTestPrivateAsync(client, false).Wait();

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.NameRoutingBadUrlTestPrivateAsync(client, false, true).Wait();
#endif
        }

        private async Task NameRoutingBadUrlTestPrivateAsync(DocumentClient client, bool bypassClientValidation, bool useTcp = false)
        {
            string suffix = Guid.NewGuid().ToString();
            // First to create a ton of named based resource object.
            string databaseId = $"BadUrlTest" + suffix;
            string collectionId = "collection" + suffix;
            string doc1Id = "document1" + suffix;

            // Create database and create collection
            Database database = await client.CreateDatabaseAsync(new Database() { Id = databaseId });
            DocumentCollection coll = await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(databaseId), new DocumentCollection() { Id = collectionId });
            Document doc1 = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), new Document() { Id = doc1Id });

            try
            {
                // the url doesn't conform to the schema at at all.
                Document document1 = await client.ReadDocumentAsync("dba/what/colltions/abc");
                Assert.Fail("Should have thrown exception in here");
            }
            catch (DocumentClientException e)
            {
                // without client validation.
                Assert.IsNotNull(e.Message);
                Assert.AreEqual(e.StatusCode, HttpStatusCode.NotFound);
            }

            try
            {
                // the url doesn't conform to the schema at at all.
                Document document1 = await client.ReadDocumentAsync("dbs/what/colltions/abc");
                Assert.Fail("Should have thrown exception in here");
            }
            catch (DocumentClientException e)
            {
                Assert.IsNotNull(e.Message);
                Assert.AreEqual(e.StatusCode, HttpStatusCode.NotFound);
            }

            try
            {
                // doing a document read with collection link
                Document doc2 = await client.ReadDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));
                Assert.Fail("Should have thrown exception in here");
            }
            catch (DocumentClientException e)
            {
                Assert.IsNotNull(e.Message);
                Assert.IsTrue(e.StatusCode == HttpStatusCode.BadRequest || e.StatusCode == HttpStatusCode.Unauthorized);
            }

            try
            {
                // doing a collection read with Document link
                DocumentCollection collection1 = await client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, doc1Id));
                Assert.Fail("Should have thrown exception in here");
            }
            catch (DocumentClientException e)
            {
                Assert.IsNotNull(e.Message);
                Assert.IsTrue(e.StatusCode == HttpStatusCode.BadRequest || e.StatusCode == HttpStatusCode.Unauthorized);
            }
            finally
            {
                await client.DeleteDatabaseAsync(database);
            }
        }

        [TestMethod]
        public void VerifyInvalidNameTest()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            this.VerifyInvalidNameTestPrivateAsync(client).Wait();
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            client = TestCommon.CreateClient(false, Protocol.Https);
            this.VerifyInvalidNameTestPrivateAsync(client).Wait();

            client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.VerifyInvalidNameTestPrivateAsync(client).Wait();
#endif

        }

        private async Task VerifyInvalidNameTestPrivateAsync(DocumentClient client)
        {
            try
            {
                Database database = await client.CreateDatabaseAsync(new Database() { Id = "abcdef=se123" });
                Assert.Fail("Should have thrown exception in here");
            }
            catch (DocumentClientException e)
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
            Database database1 = await client.CreateDatabaseAsync(new Database() { Id = databaseId });
            DocumentCollection coll1 = await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(databaseId), new DocumentCollection() { Id = collectionId });

            // create should fail.
            foreach (string resourceName in forbiddenCharInNameList)
            {
                try
                {
                    Document document = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), new Document() { Id = resourceName });
                    Assert.Fail("Should have thrown exception in here");
                }
                catch (ArgumentException e)
                {
                    Assert.IsTrue(e.Message.Contains("invalid character") || e.Message.Contains("end with space"));
                }
            }

            // replace should fail
            Document documentCreated = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), new Document() { Id = Guid.NewGuid().ToString() });
            foreach (string resourceName in forbiddenCharInNameList)
            {
                try
                {
                    documentCreated.Id = resourceName;
                    Document document = await client.ReplaceDocumentAsync(documentCreated);
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
            DocumentClient client = TestCommon.CreateClient(true);
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
        /// Tests that collection cache is refreshed when collection is recreated.
        /// The test just ensures that client retries and completes successfully.
        /// It verifies case when original collection is not partitioned and new collection is partitioned.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitioned()
        {
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitioned(TestCommon.CreateClient(true));
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitioned(TestCommon.CreateClient(false, protocol: Protocol.Tcp));
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitioned(TestCommon.CreateClient(false, protocol: Protocol.Https));
#endif
        }

        internal async Task TestPartitionKeyDefinitionOnCollectionRecreateFromNonPartitionedToPartitioned(DocumentClient client)
        {
            await TestCommon.DeleteAllDatabasesAsync();
            await client.CreateDatabaseAsync(new Database { Id = "db1" });
            await TestCommon.CreateCollectionAsync(client, "/dbs/db1", new DocumentCollection { Id = "coll1" });
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

            PartitionKeyRangeCache routingMapProvider = await client.GetPartitionKeyRangeCacheAsync();
            Assert.AreNotEqual(sessionToken1.Split(':')[0], (await routingMapProvider.TryGetOverlappingRangesAsync(coll.ResourceId, fullRange)).First().Id);

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

            PartitionKeyRangeCache routingMapProvider = await client.GetPartitionKeyRangeCacheAsync();
            Assert.AreNotEqual(sessionToken1.Split(':')[0], (await routingMapProvider.TryGetOverlappingRangesAsync(coll.ResourceId, fullRange)).First().Id);

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
        [Ignore]
        [TestMethod]
        public async Task TestRouteToNonExistentRangeAfterCollectionRecreate()
        {
            await this.TestRouteToNonExistentRangeAfterCollectionRecreate(TestCommon.CreateClient(true));
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestRouteToNonExistentRangeAfterCollectionRecreate(TestCommon.CreateClient(false, protocol: Protocol.Tcp));
            await this.TestRouteToNonExistentRangeAfterCollectionRecreate(TestCommon.CreateClient(false, protocol: Protocol.Https));
#endif
        }

        internal async Task TestRouteToNonExistentRangeAfterCollectionRecreate(DocumentClient client)
        {
            await TestCommon.DeleteAllDatabasesAsync();
            await client.CreateDatabaseAsync(new Database { Id = "db1" });
            PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field1" }), Kind = PartitionKind.Hash };
            DocumentCollection collection = await TestCommon.CreateCollectionAsync(client, "/dbs/db1", new DocumentCollection { Id = "coll1", PartitionKey = partitionKeyDefinition1 }, new RequestOptions { OfferThroughput = 12000 });
            var partitionKeyRangeCache = await client.GetPartitionKeyRangeCacheAsync();
            var ranges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                collection.ResourceId,
                new Range<string>(
                    PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                    PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                    true,
                    false));

            Assert.AreEqual(5, ranges.Count());

            Document document1 = new Document { Id = "doc1" };
            document1.SetPropertyValue("field1", 1);
            await client.CreateDocumentAsync("/dbs/db1/colls/coll1", document1);

            try
            {
                await client.ReadDocumentFeedAsync("/dbs/db1/colls/coll1", new FeedOptions { PartitionKeyRangeId = "foo" });
                Assert.Fail();
            }
            catch (DocumentClientException ex)
            {
                Assert.AreEqual(HttpStatusCode.Gone, ex.StatusCode);
                Assert.AreEqual(SubStatusCodes.PartitionKeyRangeGone, ex.GetSubStatus());
            }

            DocumentClient otherClient = TestCommon.CreateClient(false);
            await otherClient.DeleteDocumentCollectionAsync("/dbs/db1/colls/coll1");
            await TestCommon.CreateCollectionAsync(otherClient, "/dbs/db1", new DocumentCollection { Id = "coll1" });

            try
            {
                await client.ReadDocumentFeedAsync("/dbs/db1/colls/coll1", new FeedOptions { PartitionKeyRangeId = ranges[ranges.Count - 1].Id });
                Assert.Fail();
            }
            catch (DocumentClientException ex)
            {
                Assert.AreEqual(HttpStatusCode.Gone, ex.StatusCode);
                Assert.AreEqual(SubStatusCodes.PartitionKeyRangeGone, ex.GetSubStatus());
            }

            await client.ReadDocumentFeedAsync("/dbs/db1/colls/coll1", new FeedOptions { PartitionKeyRangeId = ranges[0].Id });
        }

        /// <summary>
        /// Tests that routing to non-existent range throws PartitionKeyRangeGoneException even after collection recreate.
        /// </summary>
        /// <returns></returns>
        [Ignore]
        [TestMethod]
        public async Task TestRouteToExistentRangeAfterCollectionRecreate()
        {
            await this.TestRouteToExistentRangeAfterCollectionRecreate(TestCommon.CreateClient(true));
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestRouteToExistentRangeAfterCollectionRecreate(TestCommon.CreateClient(false, protocol: Protocol.Tcp));
            await this.TestRouteToExistentRangeAfterCollectionRecreate(TestCommon.CreateClient(false, protocol: Protocol.Https));
#endif
        }

        internal async Task TestRouteToExistentRangeAfterCollectionRecreate(DocumentClient client)
        {
            await TestCommon.DeleteAllDatabasesAsync();
            await client.CreateDatabaseAsync(new Database { Id = "db1" });
            await TestCommon.CreateCollectionAsync(client, "/dbs/db1", new DocumentCollection { Id = "coll1" });

            Document document1 = new Document { Id = "doc1" };
            document1.SetPropertyValue("field1", 1);
            await client.CreateDocumentAsync("/dbs/db1/colls/coll1", document1);

            try
            {
                await client.ReadDocumentFeedAsync("/dbs/db1/colls/coll1", new FeedOptions { PartitionKeyRangeId = "4" });
                Assert.Fail();
            }
            catch (DocumentClientException ex)
            {
                Assert.AreEqual(HttpStatusCode.Gone, ex.StatusCode);
                Assert.AreEqual(SubStatusCodes.PartitionKeyRangeGone, ex.GetSubStatus());
            }

            DocumentClient otherClient = TestCommon.CreateClient(false);
            await otherClient.DeleteDocumentCollectionAsync("/dbs/db1/colls/coll1");
            PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field1" }), Kind = PartitionKind.Hash };
            DocumentCollection collection = await TestCommon.CreateCollectionAsync(otherClient, "/dbs/db1", new DocumentCollection { Id = "coll1", PartitionKey = partitionKeyDefinition1 }, new RequestOptions { OfferThroughput = 12000 });
            var partitionKeyRangeCache = await client.GetPartitionKeyRangeCacheAsync();
            var ranges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                collection.ResourceId,
                new Range<string>(
                    PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                    PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                    true,
                    false));

            Assert.AreEqual(5, ranges.Count());

            await client.ReadDocumentFeedAsync("/dbs/db1/colls/coll1", new FeedOptions { PartitionKeyRangeId = "4" });
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
        /// Tests that partition key definition cache is refreshed when collection is recreated.
        /// The test just ensures that client retries and completes successfully.
        /// It verifies case when original collection is partitioned and new collection is not partitioned.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestPartitionKeyDefinitionOnCollectionRecreateFromPartitionedToNonPartitioned()
        {
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromPartitionedToNonPartitioned(TestCommon.CreateClient(true));
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromPartitionedToNonPartitioned(TestCommon.CreateClient(false, protocol: Protocol.Tcp));
            await this.TestPartitionKeyDefinitionOnCollectionRecreateFromPartitionedToNonPartitioned(TestCommon.CreateClient(false, protocol: Protocol.Https));
#endif
        }

        internal async Task TestPartitionKeyDefinitionOnCollectionRecreateFromPartitionedToNonPartitioned(DocumentClient client)
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
            await TestCommon.CreateCollectionAsync(otherClient, "/dbs/db1", new DocumentCollection { Id = "coll1" });

            Document document2 = new Document { Id = "doc1" };
            document2.SetPropertyValue("field2", 1);
            await client.CreateDocumentAsync("/dbs/db1/colls/coll1", document2);
        }

        /// <summary>
        /// Tests that partition key definition cache is refreshed when collection is recreated.
        /// The test just ensures that Gateway successfully creates script when its partitionkeydefinition cache is outdated..
        /// It verifies case when original collection is partitioned and new collection is not partitioned.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestScriptCreateOnCollectionRecreateFromPartitionedToNonPartitioned()
        {
            await this.TestScriptCreateOnCollectionRecreateFromPartitionedToNonPartitioned(TestCommon.CreateClient(true));
        }

        internal async Task TestScriptCreateOnCollectionRecreateFromPartitionedToNonPartitioned(DocumentClient client)
        {
            await TestCommon.DeleteAllDatabasesAsync();
            await client.CreateDatabaseAsync(new Database { Id = "db1" });
            PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field1" }), Kind = PartitionKind.Hash };
            await TestCommon.CreateCollectionAsync(client, "/dbs/db1", new DocumentCollection { Id = "coll1", PartitionKey = partitionKeyDefinition1 }, new RequestOptions { OfferThroughput = 12000 });
            Document document1 = new Document { Id = "doc1" };
            document1.SetPropertyValue("field1", 1);
            await client.CreateDocumentAsync("/dbs/db1/colls/coll1", document1);

            DocumentClient otherClient = TestCommon.CreateClient(false);
            await otherClient.DeleteDocumentCollectionAsync("/dbs/db1/colls/coll1");
            await TestCommon.CreateCollectionAsync(otherClient, "/dbs/db1", new DocumentCollection { Id = "coll1" });

            await client.CreateStoredProcedureAsync("/dbs/db1/colls/coll1", new StoredProcedure { Id = "sproc1", Body = "function() {return 1}" });
        }

        /// <summary>
        /// Tests that partition key definition cache is refreshed when collection is recreated.
        /// The test just ensures that Gateway successfully creates script when its partitionkeydefinition cache is outdated..
        /// It verifies case when original collection is not partitioned and new collection is partitioned.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestScriptCreateOnCollectionRecreateFromNotPartitionedToPartitioned()
        {
            await this.TestScriptCreateOnCollectionRecreateFromNotPartitionedToPartitioned(TestCommon.CreateClient(true));
        }

        internal async Task TestScriptCreateOnCollectionRecreateFromNotPartitionedToPartitioned(DocumentClient client)
        {
            await TestCommon.DeleteAllDatabasesAsync();
            await client.CreateDatabaseAsync(new Database { Id = "db1" });
            await TestCommon.CreateCollectionAsync(client, "/dbs/db1", new DocumentCollection { Id = "coll1" });
            Document document1 = new Document { Id = "doc1" };
            document1.SetPropertyValue("field1", 1);
            await client.CreateDocumentAsync("/dbs/db1/colls/coll1", document1);

            DocumentClient otherClient = TestCommon.CreateClient(false);
            await otherClient.DeleteDocumentCollectionAsync("/dbs/db1/colls/coll1");
            PartitionKeyDefinition partitionKeyDefinition2 = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/field1" }), Kind = PartitionKind.Hash };
            await TestCommon.CreateCollectionAsync(otherClient, "/dbs/db1", new DocumentCollection { Id = "coll1", PartitionKey = partitionKeyDefinition2 }, new RequestOptions { OfferThroughput = 12000 });

            await client.CreateStoredProcedureAsync("/dbs/db1/colls/coll1", new StoredProcedure { Id = "sproc1", Body = "function() {return 1}" });
            for (int i = 0; i < 10; i++)
            {
                await client.ExecuteStoredProcedureAsync<object>("/dbs/db1/colls/coll1/sprocs/sproc1", new RequestOptions { PartitionKey = new PartitionKey(i) });
            }
        }

        private async Task VerifyNameBasedCollectionCRUDOperationsAsync(DocumentClient client)
        {
            try
            {
                await TestCommon.DeleteAllDatabasesAsync();

                // Scenario 1: name based collection read.

                Database database = (await client.CreateDatabaseAsync(new Database { Id = "ValidateNameBasedCollectionCRUDOperations_DB" })).Resource;
                DocumentCollection collection = (await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(database.Id), new DocumentCollection { Id = "ValidateNameBasedCollectionCRUDOperations_COLL" }));
                Uri collectionUri = UriFactory.CreateDocumentCollectionUri(database.Id, collection.Id);
                await client.CreateDocumentAsync(collectionUri, new
                {
                    Id = "Id_" + Guid.NewGuid().ToString(),
                    Author = "Author_" + Guid.NewGuid().ToString(),
                });

                // Update collection.
                collection = new DocumentCollection { Id = collection.Id };
                collection.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/" });
                collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/\"Author\"/?" });
                collection = (await client.ReplaceDocumentCollectionAsync(collectionUri, collection)).Resource;

                // Read collection.
                ResourceResponse<DocumentCollection> response = await client.ReadDocumentCollectionAsync(collectionUri);
                Assert.IsTrue(response.IndexTransformationProgress >= 0);

                // Delete and re-create the collection with the same name.
                await client.DeleteDocumentCollectionAsync(collectionUri);
                collection = (await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(database.Id), new DocumentCollection { Id = "ValidateNameBasedCollectionCRUDOperations_COLL" }));

                // Read the new collection.
                // The gateway's cache is stale at this point. This test verifies that the gateway should be able to refresh the cache and returns the response.
                response = await client.ReadDocumentCollectionAsync(collectionUri);
                Assert.AreEqual(100, response.IndexTransformationProgress);

                // Scenario 2: name based collection put.

                await client.CreateDocumentAsync(collectionUri, new
                {
                    Id = "Id_" + Guid.NewGuid().ToString(),
                    Author = "Author_" + Guid.NewGuid().ToString(),
                });

                // Delete and re-create the collection with the same name.
                await client.DeleteDocumentCollectionAsync(collectionUri);
                collection = (await TestCommon.CreateCollectionAsync(client, UriFactory.CreateDatabaseUri(database.Id), new DocumentCollection { Id = "ValidateNameBasedCollectionCRUDOperations_COLL" }));

                // Update collection.
                collection = new DocumentCollection { Id = collection.Id };
                collection.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/" });
                collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/\"Author\"/?" });
                await client.ReplaceDocumentCollectionAsync(collectionUri, collection);
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
                new StringKeyValueCollection(),
                SerializationFormattingPolicy.None))
            {
                request.Headers[HttpConstants.HttpHeaders.PartitionKey] = PartitionKeyInternal.Empty.ToJsonString();

                return new ResourceResponse<Document>(await client.CreateAsync(request));
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

        private async Task<IList<DocumentCollection>> CreateCollectionsAsync(DocumentClient client, Database database, int numberOfCollectionsPerDatabase)
        {
            IList<DocumentCollection> documentCollections = new List<DocumentCollection>();
            if (numberOfCollectionsPerDatabase > 0)
            {
                for (int i = 0; i < numberOfCollectionsPerDatabase; ++i)
                {
                    documentCollections.Add(await AsyncRetryRateLimiting(() => TestCommon.CreateCollectionAsync(client, database.CollectionsLink, new DocumentCollection { Id = Guid.NewGuid().ToString() })));
                }
            }
            return documentCollections;
        }
    }
}
