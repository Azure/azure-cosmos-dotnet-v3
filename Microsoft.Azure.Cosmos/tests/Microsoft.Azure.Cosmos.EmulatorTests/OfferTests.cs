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
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class OfferTests
    {

        private readonly PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };

        private struct TestCase
        {
            public int? offerThroughput;
            public string offerType;
            public bool errorExpected;
            public bool disjointRangeExpected;
            public int lowerRange1;
            public int higherRange1;
            public int lowerRange2;
            public int higherRange2;
            public string clientVersion;
            public int expectedPartitions;

            public TestCase(
                int? offerThroughput,
                bool disjointRangeExpected,
                int lowerRange1,
                int higherRange1,
                int lowerRange2,
                int higherRange2,
                string clientVersion,
                int expectedPartitions)
            {
                this.errorExpected = true;
                this.offerThroughput = offerThroughput;
                this.disjointRangeExpected = disjointRangeExpected;
                this.lowerRange1 = lowerRange1;
                this.higherRange1 = higherRange1;
                this.lowerRange2 = lowerRange2;
                this.higherRange2 = higherRange2;
                this.offerType = null;
                this.clientVersion = clientVersion;
                this.expectedPartitions = expectedPartitions;
            }

            public TestCase(string offerType, string clientVersion, int expectedPartitions)
                : this(null, false, 0, 0, 0, 0, clientVersion, expectedPartitions)
            {
                this.offerType = offerType;
                this.errorExpected = false;
            }

            public TestCase(int? offerThroughput, string clientVersion, int expectedPartitions)
                : this(offerThroughput, false, 0, 0, 0, 0, clientVersion, expectedPartitions)
            {
                this.errorExpected = false;
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.CurrentCulture, "Test case: Version {7} OfferThroughput {0}, errorExpected {1}, disjointRangeExpected {2}, low1 {3}, high1 {4}, low2 {5}, high2 {6}, OfferType {7}",
                    this.offerThroughput, this.errorExpected, this.disjointRangeExpected, this.lowerRange1, this.higherRange1, this.lowerRange2, this.higherRange2, this.clientVersion, this.offerType ?? "null");
            }
        }

        private struct DefaultOfferThroughputTestCase
        {
            public int offerThroughput;
            public double provisionedStoragePerPartition;
            public int expectedDefaultOfferThroughput;

            public override string ToString()
            {
                this.provisionedStoragePerPartition = 0;
                this.expectedDefaultOfferThroughput = 0;
                this.offerThroughput = 0;
                return string.Format(CultureInfo.CurrentCulture, "Test case: OfferThroughput = {0}, provisionedStorage = {1} GB, expectedDefaultThroughput = {2}",
                    this.offerThroughput, this.provisionedStoragePerPartition, this.expectedDefaultOfferThroughput);
            }
        }

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            DocumentClientSwitchLinkExtension.Reset("GatewayTests");
        }

        [TestInitialize]
        public void TestInitialize()
        {
            using (DocumentClient client = TestCommon.CreateClient(true))
            {
                TestCommon.DeleteAllDatabasesAsync().Wait();
            }
        }

        [TestMethod]
        [Ignore] //Not a valid scenerio for V3 SDK onwards
        public async Task ValidateOfferCreateNegative_1()
        {
            DocumentClient client = TestCommon.CreateClient(false);

            Database database = (await client.CreateDatabaseAsync(new Database { Id = Guid.NewGuid().ToString("N") })).Resource;

            // create collection with partition key (1 partition) and single partition throughput.
            // Difference from the test ValidateOfferReplaceNegative_2 is the usage of throughput rather than offertype.
            try
            {
                DocumentCollection collection = await client.CreateDocumentCollectionAsync(
                            database.SelfLink,
                            new DocumentCollection { Id = Guid.NewGuid().ToString("N"), PartitionKey = partitionKeyDefinition },
                            new RequestOptions { OfferThroughput = 12000 }); // provision > 1 partition with no PK, not allowed
                Assert.Fail();
            }
            catch (DocumentClientException ex)
            {
                Assert.IsTrue(ex.Message.Contains("between 400 and 10000 inclusive in increments of 100"));
            }
        }

        [TestMethod]
        public async Task ValidateOfferDefaults()
        {
            DocumentClient client = TestCommon.CreateClient(false);

            Database database = (await client.CreateDatabaseAsync(new Database { Id = Guid.NewGuid().ToString("N") })).Resource;
            string collectionId = Guid.NewGuid().ToString("N");
            DocumentCollection collection = await client.CreateDocumentCollectionAsync(
                database.SelfLink,
                new DocumentCollection { Id = collectionId, PartitionKey = partitionKeyDefinition });

            Offer offer = (await client.ReadOffersFeedAsync()).Single(o => o.ResourceLink == collection.SelfLink);

            int numRetries = 3;
            for (int retry = 0;
                (retry < numRetries) && !(offer is Offer);
                retry++)
            {
                DefaultTrace.TraceInformation("Retry attempt {0}..", retry);
                await Task.Delay((retry + 1) * 10000);
                await client.DeleteDocumentCollectionAsync(collection);
                collectionId = Guid.NewGuid().ToString("N");
                collection = await client.CreateDocumentCollectionAsync(
                    database.SelfLink,
                    new DocumentCollection { Id = collectionId, PartitionKey = partitionKeyDefinition });
                offer = (await client.ReadOffersFeedAsync()).Single(o => o.ResourceLink == collection.SelfLink);
            }

            // let it fail despite retries if config override has not taken effect
            OfferV2 offerV2 = (OfferV2)offer;
            Assert.AreEqual("Invalid", offerV2.OfferType);
            Assert.AreEqual(offerV2.OfferVersion, Constants.Offers.OfferVersion_V2);
            Assert.AreEqual(400, offerV2.Content.OfferThroughput);

            await client.DeleteDatabaseAsync(database);
        }

        [TestMethod]
        public void ValidateOfferV2Read()
        {
            Func<DocumentClient, DocumentClientType, Task> testFunc = async (DocumentClient client, DocumentClientType clientType) =>
            {
                const int numCollections = 1;
                List<string> collectionsLink = new List<string>();
                List<Tuple<string, OfferV2>> offerList = new List<Tuple<string, OfferV2>>();

                ResourceResponse<Database> dbResponse = await client.CreateDatabaseAsync(new Database
                {
                    Id = Guid.NewGuid().ToString("N")
                });

                Assert.AreEqual(HttpStatusCode.Created, dbResponse.StatusCode);

                string databaseLink = dbResponse.Resource.SelfLink;

                for (int i = 0; i < numCollections; ++i)
                {
                    // Create collections.
                    ResourceResponse<DocumentCollection> collResponse =
                        await client.CreateDocumentCollectionAsync(databaseLink, new DocumentCollection
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            PartitionKey = partitionKeyDefinition
                        },
                        new RequestOptions
                        {
                            OfferThroughput = (i + 1) * 1000
                        });
                    Assert.AreEqual(HttpStatusCode.Created, collResponse.StatusCode);
                    collectionsLink.Add(collResponse.Resource.SelfLink);
                    offerList.Add(
                        Tuple.Create(collResponse.Resource.SelfLink, new OfferV2((i + 1) * 1000) { OfferType = "Invalid" }));
                }

                DocumentFeedResponse<Offer> offerResponse = await client.ReadOffersFeedAsync();
                OfferTests.ValidateOfferCount(offerResponse, collectionsLink);

                string collectionLink = null;
                for (int i = 0; i < numCollections; ++i)
                {
                    collectionLink = collectionsLink.SingleOrDefault(collLink => collLink == offerResponse.ElementAt(i).ResourceLink);
                    Offer offerForCollection = offerList.Single(offerTuple => offerTuple.Item1 == collectionLink).Item2;
                    Assert.IsNotNull(collectionLink);
                    OfferTests.ValidateOfferResponse(offerForCollection, offerResponse.ElementAt(i));

                    ResourceResponse<Offer> readResponse = await client.ReadOfferAsync(offerResponse.ElementAt(i).SelfLink);
                    OfferTests.ValidateOfferResponse(offerForCollection, readResponse.Resource);
                }

                foreach (string collLink in collectionsLink)
                {
                    await client.DeleteDocumentCollectionAsync(collLink);
                }

                await client.DeleteDatabaseAsync(databaseLink);
            };

            Util.TestForEachClient(testFunc, "ValidateOfferV2Read");
        }

        [TestMethod]
        public void QueryOfferV2WithLinq()
        {
            Func<DocumentClient, DocumentClientType, Task> testFunc = async (DocumentClient client, DocumentClientType clientType) =>
            {
                ResourceResponse<Database> dbResponse = await client.CreateDatabaseAsync(new Database
                {
                    Id = Guid.NewGuid().ToString("N")
                });
                Assert.AreEqual(HttpStatusCode.Created, dbResponse.StatusCode);

                string collPrefix = Guid.NewGuid().ToString("N");

                // V2 offer
                INameValueCollection headers = new DictionaryNameValueCollection();
                headers.Add("x-ms-offer-throughput", "8000");

                DocumentCollection[] collections = (from index in Enumerable.Range(1, 1)
                                                    select client.Create<DocumentCollection>(dbResponse.Resource.ResourceId,
                                                        new DocumentCollection
                                                        {
                                                            Id = string.Format(CultureInfo.InvariantCulture, "{0}{1}", collPrefix, index),
                                                            PartitionKey = partitionKeyDefinition
                                                        }, headers)).ToArray();

                List<Offer> queryResults = new List<Offer>();
                foreach (DocumentCollection collection in collections)
                {
                    queryResults.Clear();
                    IQueryable<Offer> offerQuery = from offer in client.CreateOfferQuery()
                                                   where offer.OfferResourceId == collection.ResourceId
                                                   select offer;
                    IDocumentQuery<Offer> documentQuery = offerQuery.AsDocumentQuery();

                    while (documentQuery.HasMoreResults)
                    {
                        DocumentFeedResponse<Offer> pagedResponse = await documentQuery.ExecuteNextAsync<Offer>();
                        Assert.IsNotNull(pagedResponse.ResponseHeaders, "ResponseHeaders cannot be null");
                        queryResults.AddRange(pagedResponse);
                    }

                    Assert.AreEqual(1, queryResults.Count, "Query replicaResult count not as expected");
                    Assert.AreEqual(collection.ResourceId, queryResults[0].OfferResourceId,
                        "Queried offer RID should match expected value");
                    Assert.AreEqual(8000, ((OfferV2)queryResults[0]).Content.OfferThroughput,
                        "Mismatched offer throughput");

                    Offer newOffer2 = client.CreateOfferQuery().Where
                        (o => o.ResourceLink == collection.SelfLink).AsEnumerable().SingleOrDefault();
                    Assert.AreEqual(collection.ResourceId, newOffer2.OfferResourceId,
                        "Queried offer RID should match expected value");
                    Assert.AreEqual(8000, ((OfferV2)newOffer2).Content.OfferThroughput,
                        "Mismatched offer throughput");
                }

                await client.DeleteDatabaseAsync(dbResponse.Resource.SelfLink);
            };

            Util.TestForEachClient(testFunc, "QueryOfferV2WithLinq");
        }

        [TestMethod]
        public void QueryOfferPropertiesV2()
        {
            Func<DocumentClient, DocumentClientType, Task> testFunc = async (DocumentClient client, DocumentClientType clientType) =>
            {
                string dbprefix = Guid.NewGuid().ToString("N");
                const int numDatabases = 1;
                const int numCollections = 1;

                Database[] databases = (from index in Enumerable.Range(1, numDatabases)
                                        select client.Create<Database>(null, new Database
                                        {
                                            Id = string.Format(CultureInfo.InvariantCulture, "{0}{1}", dbprefix, index)
                                        })).ToArray();

                dbprefix = Guid.NewGuid().ToString("N");

                // V2 offer
                INameValueCollection headers = new DictionaryNameValueCollection();
                headers.Add("x-ms-offer-throughput", "8000");

                List<DocumentCollection> collections = new List<DocumentCollection>();
                foreach (Database db in databases)
                {
                    collections.AddRange((from index in Enumerable.Range(1, numCollections)
                                          select client.Create<DocumentCollection>(db.ResourceId,
                                              new DocumentCollection
                                              {
                                                  Id = string.Format(CultureInfo.InvariantCulture, "{0}{1}", dbprefix, index),
                                                  PartitionKey = partitionKeyDefinition
                                              }, headers)).ToArray());
                }

                DocumentFeedResponse<Offer> offerResponse = await client.ReadOffersFeedAsync();
                OfferTests.ValidateOfferCount(offerResponse, collections.Select(collection => collection.SelfLink).ToList());
                List<Offer> offers = offerResponse.ToList<Offer>();

                // Query by offer resourceId
                for (int index = 0; index < offers.Count; ++index)
                {
                    string rid = collections[index].ResourceId;
                    IEnumerable<dynamic> queriedOffers =
                        client.CreateOfferQuery(@"select * from root r where r.offerResourceId = """ + rid +
                                                @""" and r.content.offerThroughput = 8000 ").AsEnumerable();

                    Assert.IsNotNull(((Offer)queriedOffers.Single()).OfferType);
                    Assert.IsNotNull(((Offer)queriedOffers.Single()).ResourceId);
                    Assert.IsNotNull(((Offer)queriedOffers.Single()).SelfLink);

                    Assert.AreEqual(collections[index].SelfLink, ((Offer)queriedOffers.Single()).ResourceLink,
                        "Expect queried ResourceLink to match the ResourceLink in the offer");
                    Assert.AreEqual(collections[index].ResourceId,
                        ((Offer)queriedOffers.Single()).OfferResourceId,
                        "Expect queried ResourceId to match the ResourceId in the offer");
                }

                foreach (Offer offer in offers)
                {
                    string id = offer.Id;
                    IEnumerable<dynamic> queriedOffers =
                        client.CreateOfferQuery(@"select r.id from root r where r.id = """ + id + @""" and r.content.offerThroughput = 8000 ")
                            .AsEnumerable();

                    Assert.AreEqual(id, ((Offer)queriedOffers.Single()).Id,
                        "Expect queried Id to match the Id in the offer");

                    string rid = offer.OfferResourceId;
                    queriedOffers =
                        client.CreateOfferQuery(
                            @"select r.offerResourceId from root r where r.offerResourceId = """ + rid + @""" and r.content.offerThroughput = 8000 ")
                            .AsEnumerable();

                    Assert.AreEqual(rid, ((Offer)queriedOffers.Single()).OfferResourceId,
                        "Expect queried offer resourceId to match the offer resourceId in the offer");
                }

                foreach (Database db in databases)
                {
                    await client.DeleteDatabaseAsync(db.SelfLink);
                }
            };

            Util.TestForEachClient(testFunc, "QueryOfferPropertiesV2");
        }

        [TestMethod]
        public void ValidateOfferRead()
        {
            Func<DocumentClient, DocumentClientType, Task> testFunc = async (DocumentClient client, DocumentClientType clientType) =>
            {
                const int numCollections = 1;
                List<string> collectionsLink = new List<string>();

                ResourceResponse<Database> dbResponse = await client.CreateDatabaseAsync(new Database
                {
                    Id = Guid.NewGuid().ToString("N")
                });

                Assert.AreEqual(HttpStatusCode.Created, dbResponse.StatusCode);

                string databaseLink = dbResponse.Resource.SelfLink;

                for (int i = 0; i < numCollections; ++i)
                {
                    // Create collections.
                    ResourceResponse<DocumentCollection> collResponse =
                        await client.CreateDocumentCollectionAsync(databaseLink, new DocumentCollection
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            PartitionKey = partitionKeyDefinition
                        });
                    Assert.AreEqual(HttpStatusCode.Created, collResponse.StatusCode);
                    collectionsLink.Add(collResponse.Resource.SelfLink);
                }

                DocumentFeedResponse<Offer> offerResponse = await client.ReadOffersFeedAsync();
                OfferTests.ValidateOfferCount(offerResponse, collectionsLink);

                string collectionLink = null;
                for (int i = 0; i < numCollections; ++i)
                {
                    collectionLink = collectionsLink.SingleOrDefault(collLink => collLink == offerResponse.ElementAt(i).ResourceLink);
                    Assert.IsNotNull(collectionLink);
                    OfferTests.ValidateOfferResponseBody(offerResponse.ElementAt(i), collectionLink);
                }

                // Read offers with feed options
                const int pageSize = 1;
                string continuation = null;
                int reads = 0;
                List<Offer> readOffers = new List<Offer>();

                do
                {
                    FeedOptions options = new FeedOptions
                    {
                        RequestContinuationToken = continuation,
                        MaxItemCount = pageSize
                    };

                    DocumentFeedResponse<Offer> response = await client.ReadOffersFeedAsync(options);
                    Assert.IsTrue(response.Count <= pageSize, string.Format(CultureInfo.InvariantCulture, "Number of offers read {0} greater than desired value {1}. Activity Id: {2}",
                        response.Count, pageSize, response.ActivityId));

                    readOffers.AddRange(response);
                    continuation = response.ResponseContinuation;
                    ++reads;
                }
                while (!String.IsNullOrEmpty(continuation));

                Assert.IsTrue(reads >= Math.Ceiling((double)numCollections / pageSize));
                Assert.AreEqual(numCollections, readOffers.Count);

                foreach (Offer offer in readOffers)
                {
                    collectionLink = collectionsLink.SingleOrDefault(collLink => collLink == offer.ResourceLink);
                    Assert.IsNotNull(collectionLink);
                    OfferTests.ValidateOfferResponseBody(offer, collectionLink);
                }

                Offer offerToRead = offerResponse.ElementAt(0);

                // Read the offer
                ResourceResponse<Offer> readResponse = await client.ReadOfferAsync(offerToRead.SelfLink);
                collectionLink = collectionsLink.SingleOrDefault(collLink => collLink == offerToRead.ResourceLink);
                Assert.IsNotNull(collectionLink);
                OfferTests.ValidateOfferResponseBody(readResponse.Resource, collectionLink, offerToRead.OfferType);

                // Check if the read resource is what we expect
                Assert.AreEqual(offerToRead.Id, readResponse.Resource.Id);
                Assert.AreEqual(offerToRead.ResourceId, readResponse.Resource.ResourceId);
                Assert.AreEqual(offerToRead.SelfLink, readResponse.Resource.SelfLink);
                Assert.AreEqual(offerToRead.ResourceLink, readResponse.Resource.ResourceLink);


                // Modify the SelfLink
                string offerLink = offerToRead.SelfLink + "x";

                // Read the offer
                try
                {
                    readResponse = await client.ReadOfferAsync(offerLink);
                    Assert.Fail("Expected an exception when reading offer with bad offer link");
                }
                catch (DocumentClientException ex)
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode, "Status code not as expected");
                }

                foreach (string collLink in collectionsLink)
                {
                    await client.DeleteDocumentCollectionAsync(collLink);
                }

                await client.DeleteDatabaseAsync(databaseLink);

                // Now try to get the read the offer after the collection is deleted
                try
                {
                    readResponse = await client.ReadOfferAsync(offerToRead.SelfLink);
                    Assert.Fail("Expected an exception when reading deleted offer");
                }
                catch (DocumentClientException ex)
                {
                    Util.ValidateClientException(ex, HttpStatusCode.NotFound);
                }

                // Make sure read feed returns 0 results.
                offerResponse = await client.ReadOffersFeedAsync();
                Assert.AreEqual(0, offerResponse.Count);
            };

            Util.TestForEachClient(testFunc, "ValidateOfferRead");
        }

        [TestMethod]
        public void ValidateOfferReplace()
        {
            this.ValidateOfferReplaceInternal(false);
        }

        private void ValidateOfferReplaceInternal(bool bSharedThroughput)
        {
            Func<DocumentClient, DocumentClientType, Task> testFunc = async (DocumentClient client, DocumentClientType clientType) =>
            {
                RequestOptions options = new RequestOptions { OfferThroughput = 50000 };
                ResourceResponse<Database> dbResponse;

                if (bSharedThroughput)
                {
                    dbResponse = await client.CreateDatabaseAsync(
                    new Database
                    {
                        Id = Guid.NewGuid().ToString("N")
                    },
                    options);
                }
                else
                {
                    dbResponse = await client.CreateDatabaseAsync(
                    new Database
                    {
                        Id = Guid.NewGuid().ToString("N")
                    });
                }
                Assert.AreEqual(HttpStatusCode.Created, dbResponse.StatusCode);

                string databaseLink = dbResponse.Resource.SelfLink;
                ISet<string> linksSet = new HashSet<string>();
                const int numCollections = 1;

                for (int i = 0; i < numCollections; ++i)
                {
                    ResourceResponse<DocumentCollection> collResponse;

                    if (bSharedThroughput)
                    {
                        collResponse = await client.CreateDocumentCollectionAsync(databaseLink, new DocumentCollection
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            PartitionKey = new PartitionKeyDefinition
                            {
                                Paths = new Collection<string> { "/pk" },
                                Kind = PartitionKind.Hash
                            }
                        });
                        linksSet.Add(databaseLink);
                    }
                    else
                    {
                        // Create two collections.
                        collResponse = await client.CreateDocumentCollectionAsync(databaseLink, new DocumentCollection
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            PartitionKey = partitionKeyDefinition
                        });

                        Assert.AreEqual(HttpStatusCode.Created, collResponse.StatusCode);
                        linksSet.Add(collResponse.Resource.SelfLink);
                    }
                }

                DocumentFeedResponse<Offer> offerResponse = await client.ReadOffersFeedAsync();
                OfferTests.ValidateOfferCount(offerResponse, linksSet);

                string offerLink = null;
                for (int i = 0; i < linksSet.Count; ++i)
                {
                    offerLink = linksSet.Single(link => link == offerResponse.ElementAt(i).ResourceLink);
                    OfferTests.ValidateOfferResponseBody(offerResponse.ElementAt(i), offerLink);
                }

                OfferV2 offerToReplace;

                if (bSharedThroughput)
                {
                    offerToReplace = new OfferV2(offerResponse.First<Offer>(), 25000);

                    await client.ReplaceOfferAsync(offerToReplace);
                }
                else
                {
                    List<Task> concurrentReplaceTasks = new List<Task>();
                    foreach (Offer offer in offerResponse)
                    {
                        OfferV2 replaceOffer = new OfferV2
                        {
                            Id = offer.Id,
                            ResourceLink = offer.ResourceLink,
                            SelfLink = offer.SelfLink,
                            ResourceId = offer.ResourceId,
                            OfferType = offer.OfferType,
                            ETag = offer.ETag,
                            OfferResourceId = offer.OfferResourceId,
                            Content = new OfferContentV2(10000)
                        };

                        concurrentReplaceTasks.Add(
                            this.ReplaceOfferAsync(
                            client,
                            replaceOffer,
                            linksSet.Single(link => link == offer.ResourceLink),
                            replaceOffer.OfferType));
                    }

                    await Task.WhenAll(concurrentReplaceTasks);

                    offerToReplace = (OfferV2)offerResponse.Last();
                }

                offerToReplace.ResourceId = "NotAllowed";

                try
                {
                    ResourceResponse<Offer> replaceResponse = await client.ReplaceOfferAsync(offerToReplace);
                    Assert.Fail("Expected an exception when replacing an offer with bad id");
                }
                catch (DocumentClientException ex)
                {
                    Util.ValidateClientException(ex, HttpStatusCode.BadRequest);
                }

                offerToReplace = (OfferV2)offerResponse.Last();
                offerToReplace.ResourceId = "InvalidRid";
                try
                {
                    ResourceResponse<Offer> replaceResponse = await client.ReplaceOfferAsync(offerToReplace);
                    Assert.Fail("Expected an exception when replacing an offer with bad Rid");
                }
                catch (DocumentClientException ex)
                {
                    Util.ValidateClientException(ex, HttpStatusCode.BadRequest);
                }

                offerToReplace = (OfferV2)offerResponse.Last();
                offerToReplace.Id = null;
                offerToReplace.ResourceId = null;
                try
                {
                    ResourceResponse<Offer> replaceResponse = await client.ReplaceOfferAsync(offerToReplace);
                    Assert.Fail("Expected an exception when replacing an offer with null id and rid");
                }
                catch (DocumentClientException ex)
                {
                    Util.ValidateClientException(ex, HttpStatusCode.BadRequest);
                }

                if (!bSharedThroughput)
                {
                    foreach (string collLink in linksSet)
                    {
                        await client.DeleteDocumentCollectionAsync(collLink);
                    }
                }

                await client.DeleteDatabaseAsync(databaseLink);
            };

            Util.TestForEachClient(testFunc, "ValidateOfferReplace");
        }

        [TestMethod]
        public void QueryOfferFailure()
        {
            using (DocumentClient client = TestCommon.CreateClient(true))
            {
                // Invalid continuation for query
                try
                {
                    IDocumentQuery<dynamic> invalidQuery = client.CreateOfferQuery(
                        "select * from root",
                        new FeedOptions() { RequestContinuationToken = "-tEGAI2wSgAoAAAAAAAAAA==#count3" })
                        .AsDocumentQuery();

                    DocumentFeedResponse<dynamic> invalidResult = invalidQuery.ExecuteNextAsync().Result;
                    Assert.Fail("Should throw an exception");
                }
                catch (Exception ex)
                {
                    DocumentClientException innerExcption = ex.InnerException as DocumentClientException;
                    Assert.IsTrue(innerExcption.StatusCode == HttpStatusCode.BadRequest, "Invalid status code");
                    Logger.LogLine(ex.StackTrace);
                }
            }
        }

        [TestMethod]
        public void QueryOfferPropertiesSuccess()
        {
            Func<DocumentClient, DocumentClientType, Task> testFunc = async (DocumentClient client, DocumentClientType clientType) =>
            {
                string dbprefix = Guid.NewGuid().ToString("N");
                const int numDatabases = 1;
                const int numCollections = 1;

                Database[] databases = (from index in Enumerable.Range(1, numDatabases)
                                        select client.Create<Database>(null, new Database
                                        {
                                            Id = string.Format(CultureInfo.InvariantCulture, "{0}{1}", dbprefix, index)
                                        })).ToArray();

                dbprefix = Guid.NewGuid().ToString("N");

                List<DocumentCollection> collections = new List<DocumentCollection>();
                foreach (Database db in databases)
                {
                    collections.AddRange((from index in Enumerable.Range(1, numCollections)
                                          select client.Create<DocumentCollection>(db.ResourceId,
                                              new DocumentCollection
                                              {
                                                  Id = string.Format(CultureInfo.InvariantCulture, "{0}{1}", dbprefix, index),
                                                  PartitionKey = partitionKeyDefinition
                                              })).ToArray());
                }

                DocumentFeedResponse<Offer> offerResponse = await client.ReadOffersFeedAsync();
                OfferTests.ValidateOfferCount(offerResponse, collections.Select(collection => collection.SelfLink).ToList());
                List<Offer> offers = offerResponse.ToList<Offer>();

                // Query by offer resourceId
                for (int index = 0; index < offers.Count; ++index)
                {
                    string rid = collections[index].ResourceId;
                    IEnumerable<dynamic> queriedOffers =
                        client.CreateOfferQuery(@"select * from root r where r.offerResourceId = """ + rid +
                                                @"""").AsEnumerable();

                    Assert.IsNotNull(((Offer)queriedOffers.Single()).OfferType);
                    Assert.IsNotNull(((Offer)queriedOffers.Single()).ResourceId);
                    Assert.IsNotNull(((Offer)queriedOffers.Single()).SelfLink);

                    Assert.AreEqual(collections[index].SelfLink, ((Offer)queriedOffers.Single()).ResourceLink,
                        "Expect queried ResourceLink to match the ResourceLink in the offer");
                    Assert.AreEqual(collections[index].ResourceId,
                        ((Offer)queriedOffers.Single()).OfferResourceId,
                        "Expect queried ResourceId to match the ResourceId in the offer");
                }

                foreach (Offer offer in offers)
                {
                    string id = offer.Id;
                    IEnumerable<dynamic> queriedOffers =
                        client.CreateOfferQuery(@"select r.id from root r where r.id = """ + id + @"""")
                            .AsEnumerable();

                    Assert.AreEqual(id, ((Offer)queriedOffers.Single()).Id,
                        "Expect queried Id to match the Id in the offer");

                    string rid = offer.OfferResourceId;
                    queriedOffers =
                        client.CreateOfferQuery(
                            @"select r.offerResourceId from root r where r.offerResourceId = """ + rid + @"""")
                            .AsEnumerable();

                    Assert.AreEqual(rid, ((Offer)queriedOffers.Single()).OfferResourceId,
                        "Expect queried offer resourceId to match the offer resourceId in the offer");
                }

                // Test pagination for query by offer type
                List<Offer> queryResults = new List<Offer>();
                const string defaultOfferType = "Invalid";
                const int pageSize = 1;
                IDocumentQuery<dynamic> paginationQuery =
                    client.CreateOfferQuery(
                        @"select * from root r where r.offerType = """ + defaultOfferType + @"""",
                        new FeedOptions
                        {
                            MaxItemCount = pageSize
                        }).AsDocumentQuery();

                while (paginationQuery.HasMoreResults)
                {
                    DocumentFeedResponse<Offer> response = await paginationQuery.ExecuteNextAsync<Offer>();
                    queryResults.AddRange(response);
                    Assert.IsTrue(response.Count <= pageSize, " No. of results greater than page size");
                }

                Assert.AreEqual(offers.Count, queryResults.Count);

                foreach (Offer offer in queryResults)
                {
                    DocumentCollection collection =
                        collections.SingleOrDefault(coll => coll.SelfLink == offer.ResourceLink);
                    if (collection != null)
                    {
                        string collectionLink = collection.SelfLink;
                        Assert.IsNotNull(collectionLink);
                        OfferTests.ValidateOfferResponseBody(offer, collectionLink);
                    }
                }

                // Test pagination for query by offer id
                foreach (Offer offer in offers)
                {
                    queryResults.Clear();

                    string id = offer.Id;

                    paginationQuery =
                        client.CreateOfferQuery(
                            @"select r.id from root r where r.id = """ + id + @"""", new FeedOptions
                            {
                                MaxItemCount = pageSize
                            }).AsDocumentQuery();

                    while (paginationQuery.HasMoreResults)
                    {
                        DocumentFeedResponse<Offer> response = await paginationQuery.ExecuteNextAsync<Offer>();
                        queryResults.AddRange(response);
                        Assert.IsTrue(response.Count <= pageSize, " No. of results greater than page size");
                    }

                    Assert.AreEqual(1, queryResults.Count);
                    Assert.AreEqual(id, queryResults[0].Id, "Expect queried Id to match the Id in the offer");
                }

                // Test pagination for query by offer resource id
                foreach (Offer offer in offers)
                {
                    queryResults.Clear();

                    string rid = offer.OfferResourceId;

                    paginationQuery =
                        client.CreateOfferQuery(
                            @"select r.offerResourceId from root r where r.offerResourceId = """ + rid + @"""",
                            new FeedOptions
                            {
                                MaxItemCount = pageSize
                            }).AsDocumentQuery();

                    while (paginationQuery.HasMoreResults)
                    {
                        DocumentFeedResponse<Offer> response = await paginationQuery.ExecuteNextAsync<Offer>();
                        queryResults.AddRange(response);
                        Assert.IsTrue(response.Count <= pageSize, " No. of results greater than page size");
                    }

                    Assert.AreEqual(1, queryResults.Count);
                    Assert.AreEqual(rid, queryResults[0].OfferResourceId,
                        "Expect queried offer resourceId to match the offer resourceId in the offer");
                }

                foreach (Database db in databases)
                {
                    await client.DeleteDatabaseAsync(db.SelfLink);
                }
            };

            Util.TestForEachClient(testFunc, "QueryOfferPropertiesSuccess");
        }

        [TestMethod]
        public void QueryOfferSuccessWithLinq()
        {
            Func<DocumentClient, DocumentClientType, Task> testFunc = async (DocumentClient client, DocumentClientType clientType) =>
            {
                ResourceResponse<Database> dbResponse = await client.CreateDatabaseAsync(new Database
                {
                    Id = Guid.NewGuid().ToString("N")
                });
                Assert.AreEqual(HttpStatusCode.Created, dbResponse.StatusCode);

                string collPrefix = Guid.NewGuid().ToString("N");
                DocumentCollection[] collections = (from index in Enumerable.Range(1, 1)
                                                    select client.Create<DocumentCollection>(dbResponse.Resource.ResourceId,
                                                        new DocumentCollection
                                                        {
                                                            Id = string.Format(CultureInfo.InvariantCulture, "{0}{1}", collPrefix, index),
                                                            PartitionKey = partitionKeyDefinition
                                                        })).ToArray();

                List<Offer> queryResults = new List<Offer>();

                //Simple Equality
                foreach (DocumentCollection collection in collections)
                {
                    queryResults.Clear();
                    IQueryable<Offer> offerQuery = from offer in client.CreateOfferQuery()
                                                   where offer.OfferResourceId == collection.ResourceId
                                                   select offer;
                    IDocumentQuery<Offer> documentQuery = offerQuery.AsDocumentQuery();

                    while (documentQuery.HasMoreResults)
                    {
                        DocumentFeedResponse<Offer> pagedResponse = await documentQuery.ExecuteNextAsync<Offer>();
                        Assert.IsNotNull(pagedResponse.ResponseHeaders, "ResponseHeaders cannot be null");
                        queryResults.AddRange(pagedResponse);
                    }

                    Assert.AreEqual(1, queryResults.Count, "Query replicaResult count not as expected");
                    Assert.AreEqual(collection.ResourceId, queryResults[0].OfferResourceId,
                        "Queried offer RID should match expected value");
                }

                string offerRidToQuery = null;

                //Logical Or 
                foreach (DocumentCollection collection in collections)
                {
                    queryResults.Clear();
                    IQueryable<Offer> offerQuery = from offer in client.CreateOfferQuery()
                                                   where offer.OfferResourceId == collection.ResourceId ||
                                                         offer.ResourceLink == collection.SelfLink
                                                   select offer;
                    IDocumentQuery<Offer> documentQuery = offerQuery.AsDocumentQuery();

                    while (documentQuery.HasMoreResults)
                    {
                        queryResults.AddRange(await documentQuery.ExecuteNextAsync<Offer>());
                    }

                    Assert.AreEqual(1, queryResults.Count, "Query replicaResult count not as expected");
                    Assert.AreEqual(collection.ResourceId, queryResults[0].OfferResourceId,
                        "Queried RID should match expected value");

                    offerRidToQuery = queryResults[0].ResourceId;
                }

                // Test negative case
                queryResults.Clear();
                IQueryable<Offer> query = from offer in client.CreateOfferQuery()
                                          where offer.OfferResourceId == "abcd" ||
                                                offer.ResourceLink == "dbd/ADSCX==/colls/EEFADSCX==/"
                                          select offer;
                IDocumentQuery<Offer> docQuery = query.AsDocumentQuery();

                while (docQuery.HasMoreResults)
                {
                    queryResults.AddRange(await docQuery.ExecuteNextAsync<Offer>());
                }

                Assert.AreEqual(0, queryResults.Count, "Query replicaResult count not as expected");

                //Select Property
                IQueryable<string> idQuery = from offer in client.CreateOfferQuery()
                                             where offer.ResourceId == offerRidToQuery
                                             select offer.ResourceId;
                IDocumentQuery<string> documentIdQuery = idQuery.AsDocumentQuery();

                List<string> idResults = new List<string>();
                while (documentIdQuery.HasMoreResults)
                {
                    idResults.AddRange(await documentIdQuery.ExecuteNextAsync<string>());
                }

                Assert.AreEqual(1, idResults.Count, "Query replicaResult count not as expected");
                Assert.AreEqual(offerRidToQuery, idResults[0], "Queried Rid should match expected value");

                // Test negative case
                idQuery = from offer in client.CreateOfferQuery()
                          where offer.ResourceId == "abcd"
                          select offer.ResourceId;
                documentIdQuery = idQuery.AsDocumentQuery();

                idResults.Clear();
                while (documentIdQuery.HasMoreResults)
                {
                    idResults.AddRange(await documentIdQuery.ExecuteNextAsync<string>());
                }

                Assert.AreEqual(0, idResults.Count, "Query replicaResult count not as expected");

                await client.DeleteDatabaseAsync(dbResponse.Resource.SelfLink);
            };

            Util.TestForEachClient(testFunc, "QueryOfferSuccessWithLinq");
        }

        [TestMethod]
        public void ValidateCreateCollectionWithOfferParam()
        {
            Func<DocumentClient, DocumentClientType, Task> testFunc = async (DocumentClient client, DocumentClientType clientType) =>
            {
                const int numCollections = 1;
                List<string> collectionsLink = new List<string>();

                // Current setting for offers.
                // Test needs to be updated when these settings change.
                Dictionary<string, double> offersMap = new Dictionary<string, double>
                    {
                        {"S1", 1000.0},  // Emulator is tweaked to have S1 throughput weight equal to 1000 (instead of 0.3), so tests go faster
                        {"S2", 1.2},
                        {"S3", 3.0}
                    };

                ResourceResponse<Database> dbResponse = await client.CreateDatabaseAsync(new Database
                {
                    Id = Guid.NewGuid().ToString("N")
                });

                Assert.AreEqual(HttpStatusCode.Created, dbResponse.StatusCode);

                string databaseLink = dbResponse.Resource.SelfLink;

                // Create collections with no offer param.
                for (int i = 0; i < numCollections; ++i)
                {
                    // Create collections.
                    ResourceResponse<DocumentCollection> collResponse =
                        await client.CreateDocumentCollectionAsync(databaseLink, new DocumentCollection
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            PartitionKey = partitionKeyDefinition
                        });

                    Assert.AreEqual(HttpStatusCode.Created, collResponse.StatusCode);
                    collectionsLink.Add(collResponse.Resource.SelfLink);
                }

                DocumentFeedResponse<Offer> offerResponse = await client.ReadOffersFeedAsync();
                OfferTests.ValidateOfferCount(offerResponse, collectionsLink);

                foreach (Offer offer in offerResponse)
                {
                    Assert.AreEqual("Invalid", offer.OfferType);
                }

                foreach (string collLink in collectionsLink)
                {
                    await client.DeleteDocumentCollectionAsync(collLink);
                }

                // Create collections with each offer type.
                foreach (string offer in offersMap.Keys)
                {
                    collectionsLink.Clear();
                    HashSet<string> offerTypes = new HashSet<string>();

                    for (int i = 0; i < numCollections; ++i)
                    {
                        // Create collections.
                        ResourceResponse<DocumentCollection> collResponse =
                            await client.CreateDocumentCollectionAsync(databaseLink, new DocumentCollection
                            {
                                Id = Guid.NewGuid().ToString("N"),
                                PartitionKey = partitionKeyDefinition
                            }, new RequestOptions
                            {
                                OfferType = offer
                            });

                        Assert.AreEqual(HttpStatusCode.Created, collResponse.StatusCode);
                        collectionsLink.Add(collResponse.Resource.SelfLink);

                        string offerType = TestCommon.GetCollectionOfferDetails(client, collResponse.Resource.ResourceId);

                        offerTypes.Add(offerType);
                    }

                    if (offerTypes.Count() != 1)
                    {
                        StringBuilder exceptionString = new StringBuilder();
                        foreach (string offerType in offerTypes)
                        {
                            exceptionString.AppendFormat("{0}, ", offerType);
                        }

                        Assert.Fail("Collection Offer type is not same for collections. it should be {0} but found", offer, exceptionString.ToString());
                    }

                    // Validate the offer type
                    Assert.AreEqual(offerTypes.First(), offer);

                    offerResponse = await client.ReadOffersFeedAsync();
                    OfferTests.ValidateOfferCount(offerResponse, collectionsLink);

                    foreach (Offer offerResp in offerResponse)
                    {
                        Assert.AreEqual(offer, offerResp.OfferType);
                    }

                    foreach (string collLink in collectionsLink)
                    {
                        await client.DeleteDocumentCollectionAsync(collLink);
                    }
                }

                await client.DeleteDatabaseAsync(databaseLink);
            };

            Util.TestForEachClient(testFunc, "ValidateCreateCollectionWithOfferParam");
        }

        private static bool IsThrottleDuetoOfferScaleDownRateLimit(DocumentClientException exception)
        {
            int substatuscode = Int32.Parse(exception.ResponseHeaders.Get(WFConstants.BackendHeaders.SubStatus));

            return exception.Message.Contains("Request rate is large") &&
                substatuscode == 3204;
        }
        private async Task ReplaceOfferAsync(DocumentClient client, Offer offerToReplace, string expectedCollLink, string expectedOfferType = null)
        {
            ResourceResponse<Offer> replaceResponse = await client.ReplaceOfferAsync(offerToReplace);
            OfferTests.ValidateOfferResponseBody(replaceResponse.Resource, expectedCollLink, expectedOfferType);
        }

        internal static void ValidateOfferResponseBody(Offer offer, string expectedCollLink, string expectedOfferType = null)
        {
            Assert.IsNotNull(offer.Id, "Id cannot be null");
            Assert.IsNotNull(offer.ResourceId, "Resource Id (Rid) cannot be null");
            Assert.IsNotNull(offer.SelfLink, "Self link cannot be null");
            Assert.IsNotNull(offer.ResourceLink, "Resource Link cannot be null");
            Assert.IsNotNull(offer.Timestamp, "Timestamp cannot be null");
            Assert.IsTrue(offer.SelfLink.Contains(offer.Id), "Offer id not contained in offer self link");
            Assert.AreEqual(expectedCollLink.Trim('/'), offer.ResourceLink.Trim('/'));
            Assert.IsNotNull(offer.OfferType);

            if (expectedOfferType != null)
            {
                Assert.AreEqual(expectedOfferType, offer.OfferType);
            }
        }

        private static void ValidateOfferResponse(Offer expectedOffer, Offer actualOffer)
        {
            string expectedOfferVersion = Constants.Offers.OfferVersion_V1;
            if (!String.IsNullOrEmpty(expectedOffer.OfferVersion))
            {
                expectedOfferVersion = expectedOffer.OfferVersion;
            }

            if (expectedOfferVersion == Constants.Offers.OfferVersion_V1)
            {
                // version in actual payload must be V1 or empty
                Assert.IsTrue(String.IsNullOrEmpty(actualOffer.OfferVersion) || (actualOffer.OfferVersion == expectedOfferVersion));
            }
            else
            {
                Assert.AreEqual(expectedOfferVersion, actualOffer.OfferVersion);
            }

            Assert.AreEqual(expectedOffer.OfferType, actualOffer.OfferType);

            switch (expectedOfferVersion ?? String.Empty)
            {
                case Constants.Offers.OfferVersion_V1:
                case Constants.Offers.OfferVersion_None:
                    {
                        Assert.IsTrue(expectedOffer.OfferType == actualOffer.OfferType);
                    }
                    break;

                case Constants.Offers.OfferVersion_V2:
                    {
                        OfferV2 expectedOfferV2 = (OfferV2)expectedOffer;
                        OfferV2 actualOfferV2 = (OfferV2)actualOffer;
                        Assert.AreEqual(expectedOfferV2.Content.OfferThroughput, actualOfferV2.Content.OfferThroughput);
                    }
                    break;

                default:
                    break;
            }
        }

        internal static void ValidateOfferCount(DocumentFeedResponse<Offer> offerResponse, ICollection<string> collectionsLink)
        {
            if (offerResponse.Count != collectionsLink.Count)
            {
                string error = string.Format(CultureInfo.InvariantCulture, "Number of offers {0} is not same as number of collections {1}",
                    offerResponse.Count, collectionsLink.Count);
                Logger.LogLine(error);

                foreach (Offer offer in offerResponse.Where(offer => !collectionsLink.Contains(offer.ResourceLink)))
                {
                    Logger.LogLine("Extra Offer resourceId: {0}, offer resourceLink: {1}", offer.OfferResourceId, offer.ResourceLink);
                }

                Assert.Fail(error);
            }
        }
    }
}
