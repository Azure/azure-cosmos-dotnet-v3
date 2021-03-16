//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("Query")]
    public class QueryTests
    {
        private DocumentClient client;
        private DocumentClient primaryReadonlyClient;
        private DocumentClient secondaryReadonlyClient;
        private readonly PartitionKeyDefinition defaultPartitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };

        private enum PrecisionType
        {
            Numeric,
            String
        }

        [TestInitialize]
        public void TestInitialize()
        {
            this.client = TestCommon.CreateClient(true, defaultConsistencyLevel: ConsistencyLevel.Session);

            // The Public emulator has only 1 MasterKey, no read-only keys
            this.primaryReadonlyClient = new DocumentClient(
                         new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                         ConfigurationManager.AppSettings["MasterKey"],
                         (HttpMessageHandler)null,
                         connectionPolicy: null);

            this.secondaryReadonlyClient = new DocumentClient(
                         new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                         ConfigurationManager.AppSettings["MasterKey"],
                         (HttpMessageHandler)null,
                         connectionPolicy: null);

            this.CleanUp();
        }

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            DocumentClientSwitchLinkExtension.Reset("QueryTests");
        }

        [TestMethod]
        public void TestQueryWithPageSize()
        {
            // Create collection and insert 200 small documents
            Database database = TestCommon.RetryRateLimiting<Database>(() =>
            {
                return this.client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() }).Result.Resource;
            });

            DocumentCollection collection = TestCommon.RetryRateLimiting<DocumentCollection>(() =>
            {
                return TestCommon.CreateCollectionAsync(this.client, database, new DocumentCollection() { Id = Guid.NewGuid().ToString(), PartitionKey = defaultPartitionKeyDefinition }).Result;
            });

            for (int i = 0; i < 200; i++)
            {
                TestCommon.RetryRateLimiting<Document>(() =>
                {
                    return this.client.CreateDocumentAsync(collection, new Document() { Id = Guid.NewGuid().ToString() }).Result.Resource;
                });
            }

            // Arbitrary count of elements up to int.MaxValue.
            DocumentFeedResponse<dynamic> result = this.client.CreateDocumentQuery<Document>(collection, "SELECT r.id FROM root r", new FeedOptions() { EnableCrossPartitionQuery = true }).AsDocumentQuery().ExecuteNextAsync().Result;
            Assert.IsTrue(result.Count <= 200, $"{result.Count} elements returned. It is more than available on collection");

            // dynamic page size (-1), expect arbitrary count of elements up to int.MaxValue.
            result = this.client.CreateDocumentQuery<Document>(collection, "SELECT r.id FROM root r", new FeedOptions() { MaxItemCount = -1, EnableCrossPartitionQuery = true }).AsDocumentQuery().ExecuteNextAsync().Result;
            Assert.IsTrue(result.Count <= 200, $"{result.Count} elements returned. It is more than available on collection");

            // page size 10
            result = this.client.CreateDocumentQuery<Document>(collection, "SELECT r.id FROM root r", new FeedOptions() { MaxItemCount = 10, EnableCrossPartitionQuery = true }).AsDocumentQuery().ExecuteNextAsync().Result;
            Assert.IsTrue(result.Count <= 10, $"{result.Count} elements returned. It is more than MaxItemCount = 10");

            TestCommon.RetryRateLimiting<ResourceResponse<Database>>(() =>
            {
                return this.client.DeleteDatabaseAsync(database).Result;
            });
        }

        [TestMethod]
        public void TestQueryDatabase()
        {
            try
            {
                string dbprefix = Guid.NewGuid().ToString("N");

                Database[] databases = (from index in Enumerable.Range(1, 3)
                                        select this.client.Create<Database>(null, new Database { Id = string.Format(CultureInfo.InvariantCulture, "{0}{1}", dbprefix, index) })).ToArray();

                Action<DocumentClient> queryAction = (documentClient) =>
                {
                    // query by  name
                    foreach (int index in Enumerable.Range(1, 3))
                    {
                        string name = string.Format(CultureInfo.InvariantCulture, "{0}{1}", dbprefix, index);
                        DatabaseProperties queriedDatabases = documentClient.CreateDatabaseQuery(@"select * from root r where r.id = """ + name + @"""").AsEnumerable().Single().ToObject<DatabaseProperties>();
                        Assert.AreEqual(databases[index - 1].ResourceId, queriedDatabases.ResourceId, "Expect queried id to match the id with the same name in the created database");
                    }
                };

                Logger.LogLine("Primary Client");
                queryAction(this.client);

                Logger.LogLine("Primary ReadonlyClient");
                queryAction(this.primaryReadonlyClient);

                Logger.LogLine("Secondary ReadonlyClient");
                queryAction(this.secondaryReadonlyClient);


            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [TestMethod]
        public void TestQueryCollection()
        {
            try
            {
                string collprefix = Guid.NewGuid().ToString("N");

                Database database = this.client.Create<Database>(null, new Database { Id = "TestQueryCollection" + Guid.NewGuid().ToString() });

                DocumentCollection[] collections = (from index in Enumerable.Range(1, 3)
                                                    select this.client.Create<DocumentCollection>(database.GetIdOrFullName(), new DocumentCollection { Id = string.Format(CultureInfo.InvariantCulture, "{0}{1}", collprefix, index), PartitionKey = defaultPartitionKeyDefinition })).ToArray();
                Action<DocumentClient> queryAction = (documentClient) =>
                {
                    // query by  name
                    foreach (int index in Enumerable.Range(1, 3))
                    {
                        string name = string.Format(CultureInfo.InvariantCulture, "{0}{1}", collprefix, index);
                        ContainerProperties queriedCollections = documentClient.CreateDocumentCollectionQuery(database, @"select * from root r where r.id = """ + name + @"""").AsEnumerable().Single().ToObject<ContainerProperties>();
                        Assert.AreEqual(collections[index - 1].ResourceId, queriedCollections.ResourceId, "Expect queried id to match the id with the same name in the created documents");
                    }
                };


                Logger.LogLine("Primary Client");
                queryAction(this.client);

                Logger.LogLine("Primary ReadonlyClient");
                queryAction(this.primaryReadonlyClient);

                Logger.LogLine("Secondary ReadonlyClient");
                queryAction(this.secondaryReadonlyClient);
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        /*
        [TestMethod]
        public void TestIndexingPolicyOnCollection()
        {
            try
            {
                Database database = this.client.Create<Database>(null, new Database { Id = "TestQueryDocumentSecondaryIndexDatabase" + Guid.NewGuid().ToString() });
                DocumentCollection collectionDefinition = new DocumentCollection { Id = "TestQueryDocumentsSecondaryIndexCollection" + Guid.NewGuid().ToString() };
                collectionDefinition.IndexingPolicy.Automatic = true;

                IndexingPath includedPath = new IndexingPath();
                includedPath.IndexType = IndexType.Hash;
                includedPath.NumericPrecision = 4;
                includedPath.Path = @"/""NumericField""/?";

                IndexingPath includedPath2 = new IndexingPath();
                includedPath2.IndexType = IndexType.Range;
                includedPath2.NumericPrecision = 4;
                includedPath2.Path = @"/""A""/""C""/?";

                IndexingPolicyOld indexingPolicyOld = new IndexingPolicyOld();

                indexingPolicyOld.IncludedPaths.Add(includedPath);
                indexingPolicyOld.IncludedPaths.Add(includedPath2);
                indexingPolicyOld.ExcludedPaths.Add(@"/""A""/""B""/?");
                indexingPolicyOld.ExcludedPaths.Add(@"/");

                collectionDefinition.IndexingPolicy = IndexingPolicyTranslator.TranslateIndexingPolicyV1ToV2(indexingPolicyOld);

                DocumentCollection collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;

                Assert.IsTrue(IsValidIndexingPath(collection.IndexingPolicy.IncludedPaths, @"/""NumericField""/?", IndexKind.Hash), "Invalid precision for NumericField");
                Assert.IsTrue(IsValidIndexingPath(collection.IndexingPolicy.IncludedPaths, @"/""A""/""C""/?", IndexKind.Range), "Invalid Precision for /A/C/?");
                Assert.IsTrue(collection.IndexingPolicy.IncludedPaths.Count == 2, "Unexpected frequent path count");
                Assert.IsTrue(collection.IndexingPolicy.ExcludedPaths.Count == 2, "Unexpected excluded path count");
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [TestMethod]
        public void TestQueryOnIndexingPaths()
        {
            try
            {
                Database database = this.client.Create<Database>(null, new Database { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                DocumentCollection collectionDefinition = new DocumentCollection { Id = "TestQueryDocumentWithPathsCollection" + Guid.NewGuid().ToString() };

                IndexingPath includedPath = new IndexingPath();
                includedPath.IndexType = IndexType.Hash;
                includedPath.NumericPrecision = 4;
                includedPath.Path = @"/""NumericField""/?";

                IndexingPolicyOld indexingPolicyOld = new IndexingPolicyOld();
                indexingPolicyOld.Automatic = true;
                indexingPolicyOld.IncludedPaths.Add(includedPath);
                indexingPolicyOld.ExcludedPaths.Add(@"/");
                indexingPolicyOld.IndexingMode = IndexingMode.Consistent;

                collectionDefinition.IndexingPolicy = IndexingPolicyTranslator.TranslateIndexingPolicyV1ToV2(indexingPolicyOld);

                DocumentCollection collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;

                //includes /ts as well
                Assert.IsTrue(IsValidIndexingPath(collection.IndexingPolicy.IncludedPaths, @"/""NumericField""/?", IndexKind.Hash), "Invalid precision for NumericField");
                Assert.IsTrue(collection.IndexingPolicy.IncludedPaths.Count == 1, "Unexpected included path count");
                Assert.IsTrue(collection.IndexingPolicy.ExcludedPaths.Count == 1, "Unexpected excluded path count");

                TestQueryDocumentsWithIndexPaths(collection);
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [TestMethod]
        public void TestQueryOnIndexingPaths2()
        {
            try
            {
                Database database = this.client.Create<Database>(null, new Database { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                DocumentCollection collectionDefinition = new DocumentCollection { Id = "TestQueryDocumentWithPathsCollection" };

                IndexingPolicyOld indexingPolicyOld = new IndexingPolicyOld();
                indexingPolicyOld.IncludedPaths.Add(new IndexingPath { IndexType = IndexType.Range, Path = @"/""NumericField""/?" });
                indexingPolicyOld.IncludedPaths.Add(new IndexingPath { IndexType = IndexType.Hash, Path = @"/" });
                indexingPolicyOld.IndexingMode = IndexingMode.Consistent;
                indexingPolicyOld.Automatic = true;

                indexingPolicyOld.ExcludedPaths.Add(@"/""A""/""B""/?");

                collectionDefinition.IndexingPolicy = IndexingPolicyTranslator.TranslateIndexingPolicyV1ToV2(indexingPolicyOld);

                Console.WriteLine("Count = {0}", collectionDefinition.IndexingPolicy.IncludedPaths.Count);
                DocumentCollection collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;

                Assert.IsTrue(collection.IndexingPolicy.IncludedPaths.Count == 2, "Unexpected included path count");

                TestQueryDocumentsWithIndexPaths(collection, false, false, false);
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [TestMethod]
        public void TestCreateIndexingPaths1()
        {
            try
            {
                Database database = this.client.Create<Database>(null, new Database { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                DocumentCollection collectionDefinition = new DocumentCollection { Id = "TestQueryDocumentWithPathsCollection" };

                IndexingPolicyOld indexingPolicyOld = new IndexingPolicyOld();

                indexingPolicyOld.IncludedPaths.Add(new IndexingPath { IndexType = IndexType.Range, Path = @"/?" });
                indexingPolicyOld.IncludedPaths.Add(new IndexingPath { IndexType = IndexType.Hash, Path = @"/" });
                indexingPolicyOld.IndexingMode = IndexingMode.Consistent;
                indexingPolicyOld.Automatic = true;
                indexingPolicyOld.ExcludedPaths.Add(@"/""A""/""B""/?");

                collectionDefinition.IndexingPolicy = IndexingPolicyTranslator.TranslateIndexingPolicyV1ToV2(indexingPolicyOld);

                bool bException = false;
                try
                {
                    DocumentCollection collection = this.client.Create<DocumentCollection>(database.GetIdOrFullName(), collectionDefinition);
                }
                catch (DocumentClientException e)
                {
                    Assert.IsTrue(e.Message.Contains("Please ensure that the path has at least"));
                    bException = true;
                }
                Assert.IsTrue(bException);

                collectionDefinition.IndexingPolicy.IncludedPaths[0].Path = @"/?/Age/?";
                bException = false;
                try
                {
                    DocumentCollection collection = this.client.Create<DocumentCollection>(database.GetIdOrFullName(), collectionDefinition);
                }
                catch (DocumentClientException e)
                {
                    Assert.IsTrue(e.Message.Contains("Please ensure that the path is a valid path"));
                    bException = true;
                }
                Assert.IsTrue(bException);
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [TestMethod]
        public void TestCreateIndexingPaths4()
        {
            try
            {
                Database database = this.client.Create<Database>(null, new Database { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                DocumentCollection collectionDefinition = new DocumentCollection { Id = "TestQueryDocumentWithPathsCollection" };

                IndexingPolicyOld indexingPolicyOld = new IndexingPolicyOld();

                indexingPolicyOld.IncludedPaths.Add(new IndexingPath { IndexType = IndexType.Hash, Path = @"/" });
                indexingPolicyOld.IndexingMode = IndexingMode.Consistent;
                indexingPolicyOld.Automatic = true;
                indexingPolicyOld.ExcludedPaths.Add(@"/""A""/""B""/?");

                collectionDefinition.IndexingPolicy = IndexingPolicyTranslator.TranslateIndexingPolicyV1ToV2(indexingPolicyOld);

                DocumentCollection collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;
                Assert.AreEqual(1, collection.IndexingPolicy.IncludedPaths.Count, "Unexpected included path count");
                Assert.AreEqual(1, collection.IndexingPolicy.ExcludedPaths.Count, "Unexpected included path count");
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [TestMethod]
        public void TestQueryOnIndexingPaths_DynamicPrecision()
        {
            try
            {
                Database database = this.client.Create<Database>(null, new Database { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                DocumentCollection collectionDefinition = new DocumentCollection { Id = "TestQueryDocumentWithPathsCollection" };

                IndexingPolicyOld indexingPolicyOld = new IndexingPolicyOld();

                indexingPolicyOld.IncludedPaths.Add(new IndexingPath { IndexType = IndexType.Range, Path = @"/""NumericField""/?", NumericPrecision = -1 });
                indexingPolicyOld.IncludedPaths.Add(new IndexingPath { IndexType = IndexType.Hash, Path = @"/" });
                indexingPolicyOld.IndexingMode = IndexingMode.Consistent;
                indexingPolicyOld.Automatic = true;
                indexingPolicyOld.ExcludedPaths.Add(@"/""A""/""B""/?");

                collectionDefinition.IndexingPolicy = IndexingPolicyTranslator.TranslateIndexingPolicyV1ToV2(indexingPolicyOld);

                DocumentCollection collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;
                Assert.IsTrue(IsValidIndexingPath(collection.IndexingPolicy.IncludedPaths, @"/""NumericField""/?", IndexKind.Range), "Invalid Precision for NumericField");
                Assert.AreEqual(2, collection.IndexingPolicy.IncludedPaths.Count, "Unexpected included path count");
                Assert.AreEqual(1, collection.IndexingPolicy.ExcludedPaths.Count, "Unexpected included path count");

                TestQueryDocumentsWithIndexPaths(collection, false, false, false);
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [TestMethod]
        public void TestNoIndexingPaths1()
        {
            try
            {
                Database database = this.client.Create<Database>(null, new Database { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                DocumentCollection collectionDefinition = new DocumentCollection { Id = "TestQueryDocumentWithPathsCollection" };

                DocumentCollection collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;
                Assert.IsTrue(collection.IndexingPolicy.IncludedPaths.Count == 1, "Unexpected included path count");
                Assert.IsTrue(collection.IndexingPolicy.ExcludedPaths.Count == 0, "Unexpected included path count");
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        /// <summary>
        /// Run tests with combination of Hash+range query(invalid)
        /// </summary>
        [TestMethod]
        public void TestQueryNegative1()
        {
            try
            {
                Database database = this.client.Create<Database>(null, new Database { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                DocumentCollection collectionDefinition = new DocumentCollection { Id = "TestQueryDocumentWithPathsCollection" + Guid.NewGuid().ToString() };

                IndexingPath includedPath = new IndexingPath();
                includedPath.IndexType = IndexType.Hash;
                includedPath.NumericPrecision = 4;
                includedPath.StringPrecision = 4;
                includedPath.Path = @"/""NumericField""/?";

                IndexingPath includedPath2 = new IndexingPath();
                includedPath2.IndexType = IndexType.Range;
                includedPath2.NumericPrecision = 4;
                includedPath2.Path = @"/""NumericField2""/?";

                IndexingPolicyOld indexingPolicyOld = new IndexingPolicyOld();

                indexingPolicyOld.Automatic = true;
                indexingPolicyOld.IncludedPaths.Add(includedPath);
                indexingPolicyOld.IncludedPaths.Add(includedPath2);
                indexingPolicyOld.ExcludedPaths.Add(@"/");
                indexingPolicyOld.IndexingMode = IndexingMode.Consistent;

                collectionDefinition.IndexingPolicy = IndexingPolicyTranslator.TranslateIndexingPolicyV1ToV2(indexingPolicyOld);

                DocumentCollection collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;

                //path count includes /ts
                Assert.IsTrue(collection.IndexingPolicy.IncludedPaths.Count == 2, "Unexpected included path count");
                Assert.IsTrue(collection.IndexingPolicy.ExcludedPaths.Count == 1, "Unexpected excluded path count");

                List<QueryDocument> listQueryDocuments = new List<QueryDocument>();
                foreach (var index in Enumerable.Range(1, 3))
                {
                    QueryDocument doc = new QueryDocument()
                    {
                        Id = string.Format(CultureInfo.InvariantCulture, "doc{0}", index),
                        NumericField = index,
                        StringField = index.ToString(CultureInfo.InvariantCulture),
                        NumericField2 = index
                    };

                    INameValueCollection headers = new StoreRequestHeaders();
                    if (!collection.IndexingPolicy.Automatic)
                    {
                        headers.Add("x-ms-indexing-directive", "include");
                    }

                    listQueryDocuments.Add(this.client.Create<QueryDocument>(collection.GetIdOrFullName(), doc, headers));
                }

                QueryDocument[] documents = listQueryDocuments.ToArray();

                Action<DocumentClient> queryAction = (documentClient) =>
                {
                    foreach (var index in Enumerable.Range(1, 3))
                    {
                        string name = string.Format(CultureInfo.InvariantCulture, "doc{0}", index);

                        //independent of paths, must succeed.
                        bool bException = false;
                        IEnumerable<QueryDocument> result;
                        try
                        {
                            result = documentClient.CreateDocumentQuery<QueryDocument>(collection, @"select * from root r where r.id = """ + name + @"""", null).AsEnumerable();
                            Assert.AreEqual(documents[index - 1].ResourceId, result.Single().ResourceId, "Expect queried id to match the id with the same name in the created documents");
                        }
                        catch (AggregateException e)
                        {
                            bException = true;
                            Assert.IsTrue(e.InnerException.Message.Contains("An invalid query has been specified with filters against path(s) excluded from indexing"));
                        }
                        Assert.IsFalse(bException);

                        result = documentClient.CreateDocumentQuery<QueryDocument>(collection, @"select * from root r where r.NumericField=" + index.ToString(CultureInfo.InvariantCulture), null).AsEnumerable();
                        Assert.AreEqual(documents[index - 1].ResourceId, result.Single().ResourceId, "Expect queried id to match the id with the same name in the created documents");

                        //range should fail.
                        bException = false;
                        try
                        {
                            result = documentClient.CreateDocumentQuery<QueryDocument>(collection, @"select * from root r where r.NumericField >=" + index.ToString(CultureInfo.InvariantCulture) + @" and r.NumericField <=" + (index + 1).ToString(CultureInfo.InvariantCulture), null).AsEnumerable();
                            Assert.AreEqual(documents[index - 1].ResourceId, result.Single().ResourceId, "Expect queried id to match the id with the same name in the created documents");
                        }
                        catch (AggregateException e)
                        {
                            bException = true;
                            Assert.IsTrue(e.InnerException.Message.Contains("An invalid query has been specified with filters against path(s) that are not range-indexed"));
                        }
                        Assert.IsTrue(bException);

                        result = documentClient.CreateDocumentQuery<QueryDocument>(collection.SelfLink, @"select * from root r where r.NumericField2 >=" + index.ToString(CultureInfo.InvariantCulture) + @" and r.NumericField2 <=" + index.ToString(CultureInfo.InvariantCulture), null).AsEnumerable();
                        Assert.AreEqual(documents[index - 1].ResourceId, result.Single().ResourceId, "Expect queried id to match the id with the same name in the created documents");
                    }
                };

                Logger.LogLine("Primary Client");
                queryAction(this.client);

                Logger.LogLine("Primary ReadonlyClient");
                queryAction(this.primaryReadonlyClient);

                Logger.LogLine("Secondary ReadonlyClient");
                queryAction(this.secondaryReadonlyClient);
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }
        */

        [TestMethod]
        public void TestQueryDocumentsSecondaryIndex()
        {
            try
            {
                Database database = this.client.Create<Database>(null, new Database { Id = "TestQueryDocumentSecondaryIndexDatabase" + Guid.NewGuid().ToString() });
                DocumentCollection collectionDefinition = new DocumentCollection { Id = "TestQueryDocumentsSecondaryIndexCollection" + Guid.NewGuid().ToString(), PartitionKey = defaultPartitionKeyDefinition };
                collectionDefinition.IndexingPolicy.Automatic = true;
                collectionDefinition.IndexingPolicy.IndexingMode = IndexingMode.Consistent;

                DocumentCollection collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;

                this.TestQueryDocuments(collection);
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [TestMethod]
        public void TestQueryDocumentsIndex()
        {
            try
            {
                Database database = this.client.Create<Database>(null, new Database { Id = "TestQueryDocumentsDatabase" + Guid.NewGuid().ToString() });
                DocumentCollection documentCollection = new DocumentCollection { Id = "TestQueryDocumentsCollection" + Guid.NewGuid().ToString(), PartitionKey = defaultPartitionKeyDefinition };
                documentCollection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
                DocumentCollection collection = TestCommon.CreateCollectionAsync(this.client, database, documentCollection).Result;

                this.TestQueryDocuments(collection);
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [TestMethod]
        public void TestQueryDocumentManualRemoveIndex()
        {
            try
            {
                Database database = this.client.Create<Database>(null, new Database { Id = "TestQueryDocumentManualRemoveIndex" + Guid.NewGuid().ToString() });

                DocumentCollection sourceCollection = new DocumentCollection
                {
                    Id = "TestQueryDocumentManualRemoveIndex" + Guid.NewGuid().ToString(),
                    PartitionKey = defaultPartitionKeyDefinition
                };
                sourceCollection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
                DocumentCollection collection = TestCommon.CreateCollectionAsync(this.client, database, sourceCollection).Result;
                JObject property = new JObject
                {
                    ["pk"] = JToken.FromObject("test")
                };
                dynamic doc = new Document()
                {
                    Id = string.Format(CultureInfo.InvariantCulture, "doc{0}", 222)
                };
                doc.NumericField = 222;
                doc.StringField = "222";
                Document documentDefinition = (Document)doc;
                documentDefinition.SetPropertyValue("pk", "test");
                INameValueCollection requestHeaders = new StoreRequestNameValueCollection
                {
                    { "x-ms-indexing-directive", "exclude" }
                };
                this.client.Create<Document>(collection.GetIdOrFullName(), documentDefinition, requestHeaders);

                IEnumerable<Document> queriedDocuments = this.client.CreateDocumentQuery<Document>(collection.GetLink(), @"select * from root r where r.StringField = ""222""", new FeedOptions { EnableCrossPartitionQuery = true });
                Assert.AreEqual(1, queriedDocuments.Count());
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }


        [TestMethod]
        public void TestQueryDocumentManualAddRemoveIndex()
        {
            try
            {
                Database database = this.client.Create<Database>(null, new Database { Id = "TestQueryDocumentManualAddRemoveIndex" + Guid.NewGuid().ToString() });

                DocumentCollection sourceCollection = new DocumentCollection
                {
                    Id = "TestQueryDocumentManualAddRemoveIndex" + Guid.NewGuid().ToString(),
                    PartitionKey = defaultPartitionKeyDefinition
                };
                sourceCollection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
                DocumentCollection collection = TestCommon.CreateCollectionAsync(this.client, database, sourceCollection).Result;

                QueryDocument doc = new QueryDocument()
                {
                    Id = string.Format(CultureInfo.InvariantCulture, "doc{0}", 333),
                    NumericField = 333,
                    StringField = "333",
                };
                doc.SetPropertyValue("pk", "test");
                INameValueCollection requestHeaders = new StoreRequestNameValueCollection
                {
                    { "x-ms-indexing-directive", "include" }
                };

                QueryDocument docCreated = this.client.Create<QueryDocument>(collection.GetIdOrFullName(), doc, requestHeaders);
                Assert.IsNotNull(this.client.CreateDocumentQuery(collection.SelfLink, @"select * from root r where r.StringField=""333""", new FeedOptions { EnableCrossPartitionQuery = true }).AsEnumerable().Single());

                docCreated.NumericField = 3333;
                docCreated.StringField = "3333";

                requestHeaders.Remove("x-ms-indexing-directive");
                requestHeaders.Add("x-ms-indexing-directive", "exclude");

                QueryDocument docReplaced = this.client.Update<QueryDocument>(docCreated, requestHeaders);

                Assert.AreEqual(docReplaced.NumericField, 3333);

                // query for changed string value
                Assert.AreEqual(1, this.client.CreateDocumentQuery(collection.SelfLink, @"select * from root r where r.StringField=""3333""", new FeedOptions { EnableCrossPartitionQuery = true }).AsEnumerable().Count());

                requestHeaders.Remove("x-ms-indexing-directive");
                requestHeaders.Add("x-ms-indexing-directive", "include");
                docReplaced = this.client.Update<QueryDocument>(docReplaced, requestHeaders);

                Assert.IsNotNull(this.client.CreateDocumentQuery(collection.SelfLink, @"select * from root r where r.StringField=""3333""", new FeedOptions { EnableCrossPartitionQuery = true }).AsEnumerable().Single());

                this.client.Delete<QueryDocument>(docReplaced.ResourceId);

                Assert.AreEqual(0, this.client.CreateDocumentQuery(collection.SelfLink, @"select * from root r where r.id=""" + doc.Id + @"""", new FeedOptions { EnableCrossPartitionQuery = true }).AsEnumerable().Count());
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [TestMethod]
        public void TestQueryDocumentsManualIndex()
        {
            try
            {
                Database database = this.client.Create<Database>(null, new Database { Id = "TestQueryDocumentsDatabaseManualIndex" + Guid.NewGuid().ToString() });

                DocumentCollection sourceCollection = new DocumentCollection
                {
                    Id = "TestQueryDocumentsCollectionNoIndex" + Guid.NewGuid().ToString(),
                    PartitionKey = defaultPartitionKeyDefinition
                };
                sourceCollection.IndexingPolicy.Automatic = false;
                sourceCollection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;

                DocumentCollection collection = TestCommon.CreateCollectionAsync(this.client, database, sourceCollection).Result;

                this.TestQueryDocuments(collection, true);
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [TestMethod]
        public void TestSessionTokenControlThroughFeedOptions()
        {
            Database database = this.client.Create<Database>(null, new Database { Id = "TestSessionTokenControlThroughFeedOptions" + Guid.NewGuid().ToString() });

            DocumentCollection collection = new DocumentCollection
            {
                Id = "SessionTokenControlThroughFeedOptionsCollection",
                PartitionKey = defaultPartitionKeyDefinition
            };
            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            collection = TestCommon.CreateCollectionAsync(this.client, database, collection).Result;

            try
            {
                string sessionTokenBeforeReplication = null;
                dynamic myDocument = new Document();
                myDocument.Id = "doc0";
                myDocument.Title = "TestSessionTokenControlThroughFeedOptions";
                ResourceResponse<Document> response = this.client.CreateDocumentAsync(collection.GetLink(), myDocument).Result;
                sessionTokenBeforeReplication = response.SessionToken;

                Assert.IsNotNull(sessionTokenBeforeReplication);

                IQueryable<dynamic> documentIdQuery = this.client.CreateDocumentQuery(collection.GetLink(), @"select * from root r where r.Title=""TestSessionTokenControlThroughFeedOptions""",
                    new FeedOptions() { SessionToken = sessionTokenBeforeReplication, EnableCrossPartitionQuery = true });

                Assert.AreEqual(1, documentIdQuery.AsEnumerable().Count());

                string sessionTokenAfterReplication = null;
                int maxRetryCount = 5;
                for (int retryCounter = 1; retryCounter < maxRetryCount; retryCounter++)
                {
                    TestCommon.WaitForServerReplication();

                    myDocument = new Document();
                    myDocument.Id = "doc" + retryCounter;
                    myDocument.Title = "TestSessionTokenControlThroughFeedOptions";
                    response = this.client.CreateDocumentAsync(collection.SelfLink, myDocument).Result;

                    sessionTokenAfterReplication = response.SessionToken;
                    Assert.IsNotNull(sessionTokenAfterReplication);

                    if (!string.Equals(sessionTokenAfterReplication, sessionTokenBeforeReplication))
                    {
                        documentIdQuery = this.client.CreateDocumentQuery(collection.SelfLink, @"select * from root r where r.Title=""TestSessionTokenControlThroughFeedOptions""",
                            new FeedOptions() { SessionToken = sessionTokenAfterReplication, EnableCrossPartitionQuery = true });

                        Assert.AreEqual(1 + retryCounter, documentIdQuery.AsEnumerable().Count());
                        break;
                    }

                    if (retryCounter == maxRetryCount - 1)
                    {
                        Assert.Fail("Unable to create Documents within Collection {0} with different LSN", collection.ResourceId);
                    }
                }
            }
            finally
            {
                this.client.DeleteDocumentCollectionAsync(collection).Wait();
            }
        }

        [TestMethod]
        public void TestQueryUnicodeDocumentHttpsGateway()
        {
            this.TestQueryUnicodeDocument(useGateway: true, protocol: Protocol.Https);
        }

        [TestMethod]
        public void TestQueryUnicodeDocumentHttpsDirect()
        {
            this.TestQueryUnicodeDocument(useGateway: false, protocol: Protocol.Https);
        }

        private void TestQueryUnicodeDocument(bool useGateway, Protocol protocol)
        {
            try
            {
                using (DocumentClient testClient = TestCommon.CreateClient(useGateway, protocol: protocol, defaultConsistencyLevel: Documents.ConsistencyLevel.Session))
                {
                    Database database = testClient.Create<Database>(null, new Database { Id = "TestQueryUnicodeDocument" + Guid.NewGuid().ToString() });

                    DocumentCollection sourceCollection = new DocumentCollection
                    {
                        Id = "TestQueryUnicodeDocument" + Guid.NewGuid().ToString(),
                        PartitionKey = defaultPartitionKeyDefinition
                    };
                    sourceCollection.IndexingPolicy.Automatic = true;
                    sourceCollection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
                    DocumentCollection collection = testClient.Create<DocumentCollection>(database.GetIdOrFullName(), sourceCollection);

                    INameValueCollection requestHeaders = new StoreRequestNameValueCollection
                    {
                        { "x-ms-indexing-directive", "include" }
                    };

                    Action<string, string, string> testDocumentSQL = (name, rawValue, escapedValue) =>
                    {
                        escapedValue = escapedValue ?? rawValue;

                        QueryDocument document = new QueryDocument()
                        {
                            Id = name,
                            StringField = rawValue,
                        };
                        document.SetPropertyValue("pk", "test");
                        QueryDocument docCreated = testClient.Create<QueryDocument>(collection.GetIdOrFullName(), document, requestHeaders);

                        {
                            IEnumerable<JObject> result = testClient
                                .CreateDocumentQuery(collection,
                                string.Format(CultureInfo.InvariantCulture, "SELECT r.StringField FROM ROOT r WHERE r.StringField=\"{0}\"", rawValue),
                                new FeedOptions { EnableCrossPartitionQuery = true }).AsDocumentQuery().ExecuteNextAsync<JObject>().Result;

                            Assert.AreEqual(document.StringField, result.Single()["StringField"].Value<string>());
                        }

                        {
                            IEnumerable<JObject> result = testClient
                                .CreateDocumentQuery(collection,
                                string.Format(CultureInfo.InvariantCulture, "SELECT r.StringField FROM ROOT r WHERE r.StringField=\"{0}\"", escapedValue),
                                new FeedOptions { EnableCrossPartitionQuery = true }).AsDocumentQuery().ExecuteNextAsync<JObject>().Result;

                            Assert.AreEqual(document.StringField, result.Single()["StringField"].Value<string>());
                        }

                        {
                            IEnumerable<JObject> result = testClient
                                .CreateDocumentQuery(collection,
                                string.Format(CultureInfo.InvariantCulture, "SELECT * FROM ROOT r WHERE r.StringField=\"{0}\"", rawValue),
                                new FeedOptions { EnableCrossPartitionQuery = true }).AsDocumentQuery().ExecuteNextAsync<JObject>().Result;

                            Assert.AreEqual(document.Id, result.Single()["id"].Value<string>());
                        }
                    };

                    testDocumentSQL("doc00", "simple", null);
                    testDocumentSQL("doc10", "\uD83D\uDE03", @"\uD83D\uDE03");
                    testDocumentSQL("doc20", "\uD83D\uDE03\t\u0005\uD83D\uDE03", @"\uD83D\uDE03\t\u0005\uD83D\uDE03");
                    testDocumentSQL("doc30", "Små ord", null);
                    testDocumentSQL("doc40", "contains space and other white characters like \t\r\n", null);
                    testDocumentSQL("CJK Ext A0", "㐀㐁㨀㨁䶴䶵", null);
                    testDocumentSQL("doc5CJK Ext B0", "������������", null);
                    testDocumentSQL("Tibetan0", "དབྱངས་ཅན་སྒྲོལ་དཀར། བཀྲ་ཤིས་རྒྱལ།", null);
                    testDocumentSQL("Uighur0", "ۋېڭكقق ھس قك كدسدق د كوكو الضعيف بقي قوي", null);
                    testDocumentSQL("Yi0", "ꉬꄒꐵꄓꐨꐵꄓꐨ", null);
                }
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [Ignore] // Flaky
        [TestMethod]
        public void TestLazyIndexAllTerms()
        {
            try
            {
                // Let the lazy indexer do force checkpointing frequently as possible.
                TestCommon.SetFederationWideConfigurationProperty("lazyIndexForceCheckpointIntervalInSeconds", 1);

                Database db = this.client.CreateDatabaseAsync(new Database
                {
                    Id = System.Reflection.MethodBase.GetCurrentMethod().Name + Guid.NewGuid().ToString("N")
                }).Result.Resource;

                DocumentCollection coll = new DocumentCollection { Id = db.Id, PartitionKey = defaultPartitionKeyDefinition };
                coll.IndexingPolicy.Automatic = true;
                coll.IndexingPolicy.IndexingMode = IndexingMode.Lazy;

                coll = TestCommon.CreateCollectionAsync(this.client, db, coll).Result;

                DateTime startTime = DateTime.Now;
                this.LoadDocuments(coll).Wait();
                System.Diagnostics.Trace.TraceInformation("Load documents took {0} ms", (DateTime.Now - startTime).TotalMilliseconds);

                startTime = DateTime.Now;

                Util.WaitForLazyIndexingToCompleteAsync(coll).Wait();
                System.Diagnostics.Trace.TraceInformation("Indexing took {0} ms", (DateTime.Now - startTime).TotalMilliseconds);

                QueryOracle.QueryOracle qo =
                    new QueryOracle.QueryOracle(this.client, coll.SelfLink, true,
                                                targetNumberOfQueriesToValidate: 20000);
                Assert.AreEqual(0, qo.IndexAndValidate(100), "Query oracle validation failed");
                this.client.DeleteDatabaseAsync(db).Wait();
            }
            finally
            {
                TestCommon.SetFederationWideConfigurationProperty("lazyIndexForceCheckpointIntervalInSeconds", 300);
            }
        }

        [Ignore]
        ////[DataRow(true)]
        ////[DataRow(false)]
        ////[DataTestMethod]
        public async Task TestRoutToSpecificPartition(bool useGateway)
        {
            DocumentClient client = TestCommon.CreateClient(useGateway);

            string guid = Guid.NewGuid().ToString();

            Database database = await client.CreateDatabaseAsync(new Database { Id = "db" + guid });

            DocumentCollection coll = await TestCommon.CreateCollectionAsync(client,
                database,
                new DocumentCollection
                {
                    Id = "coll" + guid,
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Paths = new Collection<string> { "/key" },
                        Kind = PartitionKind.Hash
                    }
                },
                new RequestOptions { OfferThroughput = 12000 });

            Range<string> fullRange = new Range<string>(
                        PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                        true,
                        false);

            IRoutingMapProvider routingMapProvider = await client.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            IReadOnlyList<PartitionKeyRange> ranges =
                await routingMapProvider.TryGetOverlappingRangesAsync(coll.ResourceId, fullRange, NoOpTrace.Singleton);
            Assert.IsTrue(ranges.Count() > 1);

            Document document = new Document { Id = "id1" };
            document.SetPropertyValue("key", "hello");
            ResourceResponse<Document> doc = await client.CreateDocumentAsync(coll.SelfLink, document);
            string partitionKeyRangeId = doc.SessionToken.Split(':')[0];
            Assert.AreNotEqual(ranges.First().Id, partitionKeyRangeId);

            DocumentFeedResponse<dynamic> response = await client.ReadDocumentFeedAsync(coll.SelfLink, new FeedOptions { PartitionKeyRangeId = partitionKeyRangeId });
            Assert.AreEqual(1, response.Count);

            response = await client.ReadDocumentFeedAsync(coll.SelfLink, new FeedOptions { PartitionKeyRangeId = ranges.First(r => r.Id != partitionKeyRangeId).Id });
            Assert.AreEqual(0, response.Count);

            await client.DeleteDatabaseAsync(database);
        }

        [Ignore("Native dll dependency")]
        [TestMethod]
        public async Task TestQueryMultiplePartitions()
        {
            await this.TestQueryMultiplePartitions(false);
            await this.TestQueryMultiplePartitions(true);
        }

        private async Task TestQueryMultiplePartitions(bool useGateway)
        {
            System.Diagnostics.Trace.TraceInformation(
                "Start TestQueryMultiplePartitions in {0} mode",
                useGateway ? ConnectionMode.Gateway.ToString() : ConnectionMode.Direct.ToString());

            uint numberOfDocuments = 1000;
            uint numberOfQueries = 10;
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            QueryOracle.QueryOracleUtil util = new QueryOracle.QueryOracle2(seed);

            DocumentClient client = TestCommon.CreateClient(useGateway);
            string guid = Guid.NewGuid().ToString();

            Database database = await client.CreateDatabaseAsync(new Database { Id = guid + "db" });

            DocumentCollection coll = await TestCommon.CreateCollectionAsync(client,
                database,
                new DocumentCollection
                {
                    Id = guid + "coll",
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Paths = new Collection<string> { "/field_0" },
                        Kind = PartitionKind.Hash
                    }
                },
                new RequestOptions { OfferThroughput = 35000 });

            Range<string> fullRange = new Range<string>(
                       PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                       PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                       true,
                       false);

            IRoutingMapProvider routingMapProvider = await client.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            IReadOnlyList<PartitionKeyRange> ranges =
                await routingMapProvider.TryGetOverlappingRangesAsync(coll.ResourceId, fullRange, NoOpTrace.Singleton);
            Assert.IsTrue(ranges.Count() > 1);

            DateTime startTime = DateTime.Now;
            IEnumerable<string> documents = util.GetDocuments(numberOfDocuments);
            foreach (string document in documents)
            {
                ResourceResponse<Document> response = await client.CreateDocumentAsync(coll.SelfLink, JsonConvert.DeserializeObject(document));
                System.Diagnostics.Trace.TraceInformation("Document: {0}, SessionToken: {1}", document, response.SessionToken);
            }

            System.Diagnostics.Trace.TraceInformation("Load documents took {0} ms", (DateTime.Now - startTime).TotalMilliseconds);

            string[] links = new[] { coll.AltLink, coll.SelfLink };
            foreach (string link in links)
            {
                int result = await util.QueryAndVerifyDocuments(client, link, util.GetQueries(numberOfQueries, false), 100, 0);
                Assert.AreEqual(0, result, string.Format(CultureInfo.InvariantCulture, "Query oracle validation failed with seed {0}", seed));
            }

            await client.DeleteDatabaseAsync(database);
        }

        [TestMethod]
        public async Task TestQueryForRoutingMapSanity()
        {
            string guid = Guid.NewGuid().ToString();
            await this.CreateDataSet(true, "db" + guid, "coll" + guid, 5000, 35000);
            await this.TestQueryForRoutingMapSanity("db" + guid, "coll" + guid, true, 5000, false);
            await this.TestQueryForRoutingMapSanity("db" + guid, "coll" + guid, false, 5000, true);
        }

        private async Task TestQueryForRoutingMapSanity(string inputDatabaseId, string inputCollectionId, bool useGateway, int numDocuments, bool isDeleteDB)
        {
            System.Diagnostics.Trace.TraceInformation(
                "Start TestQueryForRoutingMapSanity in {0} mode",
                useGateway ? ConnectionMode.Gateway.ToString() : ConnectionMode.Direct.ToString());

            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            DocumentClient client = TestCommon.CreateClient(useGateway);
            Database database = await client.ReadDatabaseAsync(string.Format(CultureInfo.InvariantCulture, "dbs/{0}", inputDatabaseId));
            DocumentCollection coll = await client.ReadDocumentCollectionAsync(string.Format(CultureInfo.InvariantCulture, "dbs/{0}/colls/{1}", inputDatabaseId, inputCollectionId));

            Range<string> fullRange = new Range<string>(
                        PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                        true,
                        false);

            IRoutingMapProvider routingMapProvider = await client.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            IReadOnlyList<PartitionKeyRange> ranges =
                await routingMapProvider.TryGetOverlappingRangesAsync(coll.ResourceId, fullRange, NoOpTrace.Singleton);
            Assert.IsTrue(ranges.Count > 1);

            // Query Number 1, that failed before
            List<string> expected = new List<string> { "documentId123", "documentId124", "documentId125" };
            List<string> result = new List<string>();
            string queryText = @"SELECT * FROM Root r WHERE r.partitionKey = 125 OR r.partitionKey = 124 OR r.partitionKey = 123";
            FeedOptions feedOptions = new FeedOptions { MaxItemCount = 5, EnableCrossPartitionQuery = true };
            IQueryable<Document> query = client.CreateDocumentQuery<Document>(
                coll.AltLink,
                queryText,
                feedOptions);

            result = query.ToList().Select(doc => doc.Id).ToList();
            result.Sort();
            expected.Sort();

            Assert.AreEqual(
                string.Join(",", expected),
                string.Join(",", result),
                this.getQueryExecutionDebugInfo(queryText, seed, feedOptions));

            result.Clear();
            expected.Clear();

            // A set of 100 random queries with 3 predicates each (3 because more number of predicates may mask the error)
            int numberOfPredicates = 3;
            int numberOfRandomQueries = 100;
            Random random = new Random();
            StringBuilder sb = new StringBuilder();
            HashSet<int> hashSet = new HashSet<int>();

            for (int i = 0; i < numberOfRandomQueries; i++)
            {
                int randomNumber;
                result.Clear();
                expected.Clear();
                sb.Clear();
                hashSet.Clear();

                sb = sb.Append(@"SELECT * FROM Root r WHERE r.partitionKey = ");

                for (int j = 0; j < numberOfPredicates - 1; j++)
                {
                    randomNumber = random.Next(numDocuments);
                    if (hashSet.Contains(randomNumber))
                    {
                        j--;
                        break;
                    }
                    expected.Add("documentId" + randomNumber);
                    sb.Append(randomNumber + " OR r.partitionKey = ");
                    hashSet.Add(randomNumber);
                }

                while (true)
                {
                    randomNumber = random.Next(numDocuments);
                    if (hashSet.Contains(randomNumber))
                    {
                        continue;
                    }
                    expected.Add("documentId" + randomNumber);
                    sb.Append(randomNumber);
                    break;
                }

                query = client.CreateDocumentQuery<Document>(
                    coll.AltLink,
                        sb.ToString(),
                        feedOptions);

                result = query.ToList().Select(doc => doc.Id).ToList();
                result.Sort();
                expected.Sort();

                Assert.AreEqual(
                    string.Join(",", expected),
                    string.Join(",", result),
                    this.getQueryExecutionDebugInfo(sb.ToString(), seed, feedOptions));
            }

            //3. Delete Database
            if (isDeleteDB)
            {
                client.DeleteDatabaseAsync(database).Wait();
            }
        }

        private async Task CreateDataSet(bool useGateway, string dbName, string collName, int numberOfDocuments, int inputThroughputOffer)
        {
            System.Diagnostics.Trace.TraceInformation(
                "Start TestQueryParallelExecution in {0} mode",
                useGateway ? ConnectionMode.Gateway.ToString() : ConnectionMode.Direct.ToString());

            DocumentClient client = TestCommon.CreateClient(useGateway);

            await TestCommon.DeleteAllDatabasesAsync();
            Random random = new Random();

            Database database = await client.CreateDatabaseAsync(new Database { Id = dbName });

            DocumentCollection coll = await TestCommon.CreateCollectionAsync(client,
                database,
                new DocumentCollection
                {
                    Id = collName,
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Paths = new Collection<string> { "/partitionKey" },
                        Kind = PartitionKind.Hash,
                    }
                },
                new RequestOptions { OfferThroughput = inputThroughputOffer });

            StringBuilder sb = new StringBuilder();

            List<Task<ResourceResponse<Document>>> taskList = new List<Task<ResourceResponse<Document>>>();
            for (int i = 0; i < numberOfDocuments / 100; i++)
            {

                for (int j = 0; j < 100; j++)
                {
                    sb.Append("{\"id\":\"documentId" + ((100 * i) + j));
                    sb.Append("\",\"partitionKey\":" + ((100 * i) + j));
                    for (int k = 1; k < 20; k++)
                    {
                        sb.Append(",\"field_" + k + "\":" + random.Next(100000));
                    }
                    sb.Append("}");
                    string a = sb.ToString();
                    Task<ResourceResponse<Document>> task = client.CreateDocumentAsync(coll.SelfLink, JsonConvert.DeserializeObject(sb.ToString()));
                    taskList.Add(task);
                    sb.Clear();
                }


                while (taskList.Count > 0)
                {
                    Task<ResourceResponse<Document>> firstFinishedTask = await Task.WhenAny(taskList);
                    await firstFinishedTask;
                    taskList.Remove(firstFinishedTask);
                }
            }
        }

        [Ignore]
        [TestMethod]
        public async Task TestQueryParallelExecution()
        {
            string guid = Guid.NewGuid().ToString();
            await this.CreateDataSet(true, "db" + guid, "coll" + guid, 5000, 35000);
            await this.TestQueryParallelExecution("db" + guid, "coll" + guid, true, Protocol.Https, false);
            await this.TestQueryParallelExecution("db" + guid, "coll" + guid, false, Protocol.Tcp, false);
            await this.TestReadFeedParallelQuery("db" + guid, "coll" + guid, true, Protocol.Https, false);
            await this.TestReadFeedParallelQuery("db" + guid, "coll" + guid, false, Protocol.Tcp, true);
        }

        private async Task TestQueryParallelExecution(string inputDatabaseId, string inputCollectionId, bool useGateway, Protocol protocol, bool isDeleteDB)
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            System.Diagnostics.Trace.TraceInformation(
                "Start TestQueryParallelExecution in {0} mode with seed{1}",
                useGateway ? ConnectionMode.Gateway.ToString() : ConnectionMode.Direct.ToString(),
                seed);

            DocumentClient client = TestCommon.CreateClient(useGateway, protocol);
            Database database = await client.ReadDatabaseAsync(string.Format(CultureInfo.InvariantCulture, "dbs/{0}", inputDatabaseId));
            DocumentCollection coll = await client.ReadDocumentCollectionAsync(string.Format(CultureInfo.InvariantCulture, "dbs/{0}/colls/{1}", inputDatabaseId, inputCollectionId));

            Range<string> fullRange = new Range<string>(
                        PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                        true,
                        false);

            IRoutingMapProvider routingMapProvider = await client.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            IReadOnlyList<PartitionKeyRange> ranges =
                await routingMapProvider.TryGetOverlappingRangesAsync(coll.ResourceId, fullRange, NoOpTrace.Singleton);
            Assert.AreEqual(5, ranges.Count);

            // Query Number 1
            List<string> expected = new List<string> { "documentId123", "documentId124", "documentId125" };
            List<string> result = new List<string>();

            string queryText = @"SELECT * FROM Root r WHERE r.partitionKey = 123 OR r.partitionKey = 124 OR r.partitionKey = 125";
            FeedOptions feedOptions = new FeedOptions
            {
                MaxItemCount = 5,
                MaxDegreeOfParallelism = 3,
                MaxBufferedItemCount = 500,
                EnableCrossPartitionQuery = true
            };
            IQueryable<Document> query = client.CreateDocumentQuery<Document>(
                coll.AltLink,
                queryText,
                feedOptions);

            result = query.ToList().Select(doc => doc.Id).ToList();
            result.Sort();
            expected.Sort();

            Assert.AreEqual(
                string.Join(",", expected),
                string.Join(",", result),
                this.getQueryExecutionDebugInfo(queryText, seed, feedOptions));

            int startRange = 100;
            int endRange = 120;
            queryText = string.Format(CultureInfo.InvariantCulture, @"SELECT * FROM Root r WHERE r.partitionKey BETWEEN {0} AND {1}", startRange, endRange);

            // Query Number 2, Serial, No prefetching
            feedOptions = new FeedOptions
            {
                MaxItemCount = -1,
                EnableCrossPartitionQuery = true,
                MaxDegreeOfParallelism = 0,
                MaxBufferedItemCount = 100
            };
            IQueryable<Document> rangeDocumentQuery = client.CreateDocumentQuery<Document>(coll.AltLink, queryText, feedOptions);
            string[] enumerableIds = rangeDocumentQuery.ToList().Select(doc => doc.Id).ToArray();
            Assert.AreEqual(
                endRange - startRange + 1,
                enumerableIds.Count(),
                this.getQueryExecutionDebugInfo(queryText, seed, feedOptions));

            // Query Number 3, User specified parallelism, 1 task with prefetching
            feedOptions = new FeedOptions
            {
                MaxItemCount = -1,
                EnableCrossPartitionQuery = true,
                MaxDegreeOfParallelism = 1,
                MaxBufferedItemCount = 100
            };
            rangeDocumentQuery = client.CreateDocumentQuery<Document>(coll.AltLink, queryText, feedOptions);
            string[] enumerableIdsOneTask = rangeDocumentQuery.ToList().Select(doc => doc.Id).ToArray();
            Assert.AreEqual(
                string.Join(",", enumerableIds),
                string.Join(",", enumerableIdsOneTask),
                this.getQueryExecutionDebugInfo(queryText, seed, feedOptions));

            // Query Number 4, Parallel, user specified parallelism
            feedOptions = new FeedOptions
            {
                MaxItemCount = 10,
                MaxDegreeOfParallelism = 2,
                MaxBufferedItemCount = 200,
                EnableCrossPartitionQuery = true
            };
            rangeDocumentQuery = client.CreateDocumentQuery<Document>(coll.AltLink, queryText, feedOptions);
            string[] enumerableIdsTwoTasks = rangeDocumentQuery.ToList().Select(doc => doc.Id).ToArray();
            Assert.AreEqual(
                string.Join(",", enumerableIds),
                string.Join(",", enumerableIdsTwoTasks),
                this.getQueryExecutionDebugInfo(queryText, seed, feedOptions));

            // Query Number 5, Automatic parallel query
            feedOptions = new FeedOptions
            {
                MaxItemCount = 10,
                MaxDegreeOfParallelism = -1,
                MaxBufferedItemCount = 100,
                EnableCrossPartitionQuery = true
            };
            IDocumentQuery<dynamic> rangeQuery = client.CreateDocumentQuery(coll.AltLink, queryText, feedOptions).AsDocumentQuery();
            List<dynamic> ids1 = new List<dynamic>();

            while (rangeQuery.HasMoreResults)
            {
                DocumentFeedResponse<dynamic> page = await rangeQuery.ExecuteNextAsync().ConfigureAwait(false);
                if (page != null)
                {
                    ids1.AddRange(page.AsEnumerable());
                }
            }

            string[] enumerableIdsAutoTasks = ids1.Select(doc => ((Document)doc).Id).ToArray();
            Assert.AreEqual(
                string.Join(",", enumerableIds),
                string.Join(",", enumerableIdsAutoTasks),
                this.getQueryExecutionDebugInfo(queryText, seed, feedOptions));

            // Query Number Link Value Test
            string valueQueryText = string.Format(CultureInfo.InvariantCulture, @"SELECT VALUE r.id FROM Root r WHERE r.partitionKey BETWEEN {0} AND {1}", startRange, endRange);
            feedOptions = new FeedOptions
            {
                MaxItemCount = 10,
                MaxDegreeOfParallelism = 2,
                MaxBufferedItemCount = 10,
                EnableCrossPartitionQuery = true
            };
            IQueryable<dynamic> valueDocumentQuery = client.CreateDocumentQuery<dynamic>(coll.AltLink, valueQueryText, feedOptions);
            List<dynamic> enumerableIdsLink = valueDocumentQuery.ToList();
            Assert.AreEqual(
                enumerableIds.Count(),
                enumerableIdsLink.Count,
                this.getQueryExecutionDebugInfo(valueQueryText, seed, feedOptions));

            if (isDeleteDB)
            {
                client.DeleteDatabaseAsync(database).Wait();
            }
        }

        private string getQueryExecutionDebugInfo(string queryText, int seed, FeedOptions feedOptions)
        {
            return string.Format(
               CultureInfo.InvariantCulture,
               "Query: {0}, Seed: {1}, MaxDegreeOfParallelism: {2}, MaxBufferedItemCount: {3}, MaxItemCount: {4}, IsEnabledCrossPartitionQuery: {5}",
               queryText,
               seed,
               feedOptions.MaxDegreeOfParallelism,
               feedOptions.MaxBufferedItemCount,
               feedOptions.MaxItemCount.GetValueOrDefault(),
               feedOptions.EnableCrossPartitionQuery);
        }

        private async Task TestReadFeedParallelQuery(string inputDatabaseId, string inputCollectionId, bool useGateway, Protocol protocol, bool isDeleteDB)
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            System.Diagnostics.Trace.TraceInformation(
                "Start TestQueryParallelExecution in {0} mode with seed{1}",
                useGateway ? ConnectionMode.Gateway : ConnectionMode.Direct,
                seed);

            DocumentClient client = TestCommon.CreateClient(useGateway, protocol);
            Database database = await client.ReadDatabaseAsync(string.Format("dbs/{0}", inputDatabaseId));
            DocumentCollection coll = await client.ReadDocumentCollectionAsync(string.Format("dbs/{0}/colls/{1}", inputDatabaseId, inputCollectionId));

            Range<string> fullRange = new Range<string>(
                        PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                        true,
                        false);

            IRoutingMapProvider routingMapProvider = await client.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            IReadOnlyList<PartitionKeyRange> ranges =
                await routingMapProvider.TryGetOverlappingRangesAsync(coll.ResourceId, fullRange, NoOpTrace.Singleton);
            Assert.AreEqual(5, ranges.Count);

            FeedOptions feedOptions = new FeedOptions
            {
                MaxItemCount = 5,
                MaxDegreeOfParallelism = 50,
                MaxBufferedItemCount = 500,
                EnableCrossPartitionQuery = true
            };

            // Select * 
            feedOptions = new FeedOptions
            {
                MaxItemCount = -1,
                EnableCrossPartitionQuery = true,
                MaxDegreeOfParallelism = 0,
                MaxBufferedItemCount = 5000
            };
            string queryText = @"SELECT * FROM Root r";
            IQueryable<Document> selectStarDocumentQuery = client.CreateDocumentQuery<Document>(coll.AltLink, queryText, feedOptions);
            DateTime startTime = DateTime.Now;
            string[] enumerableIdsSelectStar = selectStarDocumentQuery.ToList().Select(doc => doc.Id).ToArray();
            double totalMillParallelOneTask = (DateTime.Now - startTime).TotalMilliseconds;

            // Read feed 1
            feedOptions = new FeedOptions
            {
                MaxItemCount = -1,
                EnableCrossPartitionQuery = true,
                MaxDegreeOfParallelism = 10,
                MaxBufferedItemCount = 5000
            };
            ResourceFeedReader<Document> feedReader = client.CreateDocumentFeedReader(coll, feedOptions);
            startTime = DateTime.Now;
            string[] enumerableIds = feedReader.Select(doc => doc.Id).ToArray();
            double totalMillParallelReedFeed1 = (DateTime.Now - startTime).TotalMilliseconds;

            Assert.AreEqual(
                string.Join(",", enumerableIds),
                string.Join(",", enumerableIdsSelectStar),
                this.getQueryExecutionDebugInfo(queryText, seed, feedOptions));

            // Read feed 2
            feedOptions = new FeedOptions
            {
                MaxItemCount = -1,
                EnableCrossPartitionQuery = true,
                MaxDegreeOfParallelism = 10,
                MaxBufferedItemCount = 5000
            };
            DocumentFeedResponse<dynamic> response = null;
            startTime = DateTime.Now;
            List<dynamic> result = new List<dynamic>();
            do
            {
                response = await client.ReadDocumentFeedAsync(coll, feedOptions);
                result.AddRange(response);
                feedOptions.RequestContinuationToken = response.ResponseContinuation;
            } while (!string.IsNullOrEmpty(feedOptions.RequestContinuationToken));
            double totalMillParallelReedFeed2 = (DateTime.Now - startTime).TotalMilliseconds;

            string[] enumerableIds2 = result.Select(doc => ((Document)doc).Id).ToArray();
            Assert.AreEqual(
                string.Join(",", enumerableIds2),
                string.Join(",", enumerableIdsSelectStar),
                this.getQueryExecutionDebugInfo(queryText, seed, feedOptions));

            if (isDeleteDB)
            {
                client.DeleteDatabaseAsync(database).Wait();
            }
        }

        /*
        [TestMethod]
        public async Task TestQueryCrossPartitionWithUpdatingConfig()
        {
            const int partitionCount = 5;
            DocumentClient originalClient = TestCommon.CreateClient(true);

            await TestCommon.DeleteAllDatabasesAsync(originalClient);
            string guid = Guid.NewGuid().ToString();
            Database database = await originalClient.CreateDatabaseAsync(new Database { Id = "db" + guid });

            DocumentCollection coll = await originalClient.CreateDocumentCollectionAsync(
                database,
                new DocumentCollection
                {
                    Id = "coll" + guid,
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Paths = new Collection<string> { "/key" },
                        Kind = PartitionKind.Hash
                    }
                },
                new RequestOptions { OfferThroughput = 35000 });

            Range<string> fullRange = new Range<string>(
                        PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                        true,
                        false);

            IRoutingMapProvider routingMapProvider = await originalClient.GetPartitionKeyRangeCacheAsync();
            IReadOnlyList<PartitionKeyRange> ranges =
                await routingMapProvider.TryGetOverlappingRangesAsync(coll.ResourceId, fullRange);
            Assert.AreEqual(partitionCount, ranges.Count);

            DocumentClient[] clients = new[]
            {
                TestCommon.CreateClient(true),
                TestCommon.CreateClient(false),
            };

            foreach (var client in clients)
            {
                await client.CreateDocumentQuery(
                    coll,
                    "SELECT * FROM r WHERE r.age IN (1, 2)",
                    new FeedOptions { EnableCrossPartitionQuery = true }).AsDocumentQuery().ExecuteNextAsync();
            }

            IDictionary<string, object> queryEngineConfiguration = await originalClient.GetQueryEngineConfiguration();
            string propertyName = NamingServiceConfig.QueryEngineConfiguration.ConfigurationProperties.MaxInExpressionItemsCount;
            int oldValue = Convert.ToInt32(queryEngineConfiguration[propertyName]);
            int newValue = 1;
            try
            {
                TestCommon.SetIntConfigurationProperty(propertyName, newValue);

                DocumentClient[] clients2 = new[]
                {
                    TestCommon.CreateClient(false),
                    TestCommon.CreateClient(true),
                };

                foreach (var client2 in clients2)
                {
                    queryEngineConfiguration = await client2.GetQueryEngineConfiguration();

                    int actualNewValue = Convert.ToInt32(queryEngineConfiguration[propertyName]);

                    Assert.AreEqual(newValue, actualNewValue);
                }

                foreach (var client in clients)
                {
                    try
                    {
                        await client.CreateDocumentQuery(
                            coll,
                            "SELECT * FROM r WHERE r.age IN (1, 2)",
                            new FeedOptions { EnableCrossPartitionQuery = true }).AsDocumentQuery().ExecuteNextAsync();

                        Assert.Fail("Expect exception");
                    }
                    catch (DocumentClientException ex)
                    {
                        if (ex.StatusCode != HttpStatusCode.BadRequest)
                        {
                            throw;
                        }
                    }
                }

                foreach (var client2 in clients2)
                {
                    try
                    {
                        await client2.CreateDocumentQuery(
                            coll,
                            "SELECT * FROM r WHERE r.age IN (1, 2)",
                            new FeedOptions { EnableCrossPartitionQuery = true }).AsDocumentQuery().ExecuteNextAsync();

                        Assert.Fail("Expect exception");
                    }
                    catch (DocumentClientException ex)
                    {
                        if (ex.StatusCode != HttpStatusCode.BadRequest)
                        {
                            throw;
                        }
                    }
                }
            }
            finally
            {
                TestCommon.SetIntConfigurationProperty(propertyName, oldValue);
            }
        }
        */

        // This test makes an assumption about the continuations so ignoring for now.
        [Ignore]
        [TestMethod]
        public async Task TestQueryNonExistentRangeInContinuationToken()
        {
            DocumentClient originalClient = TestCommon.CreateClient(true);

            string guid = Guid.NewGuid().ToString();
            Database database = await originalClient.CreateDatabaseAsync(new Database { Id = "db" + guid });

            DocumentCollection coll = await TestCommon.CreateCollectionAsync(originalClient,
                database,
                new DocumentCollection
                {
                    Id = "coll" + guid,
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Paths = new Collection<string> { "/id" },
                        Kind = PartitionKind.Hash
                    }
                },
                new RequestOptions { OfferThroughput = 1000 });

            DocumentClient client = TestCommon.CreateClient(true);

            await client.CreateDocumentAsync(coll, new Document());
            await client.CreateDocumentAsync(coll, new Document());

            IDocumentQuery<dynamic> seqQuery = client.CreateDocumentQuery(
                coll,
                "SELECT * FROM r",
                new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = 1 }).AsDocumentQuery();
            DocumentFeedResponse<dynamic> resultSeq = null;
            while (true)
            {
                resultSeq = await seqQuery.ExecuteNextAsync();
                if (resultSeq.Count == 2)
                {
                    Assert.IsTrue(resultSeq.ResponseContinuation.Contains("\"FF\""));
                    //Assert.IsTrue(resultSeq.ResponseContinuation.Contains("\"\""));
                    break;
                }
            }

            IDocumentQuery<dynamic> parallelQuery = client.CreateDocumentQuery(
                coll,
                "SELECT * FROM r",
                new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = 1, MaxDegreeOfParallelism = 1 }).AsDocumentQuery();
            DocumentFeedResponse<dynamic> resultParallel = null;
            while (true)
            {
                resultParallel = await parallelQuery.ExecuteNextAsync();
                if (resultParallel.Count == 2)
                {
                    Assert.IsTrue(resultParallel.ResponseContinuation.Contains("\"FF\""));
                    //Assert.IsTrue(resultParallel.ResponseContinuation.Contains("\"\""));
                    break;
                }
            }

            Func<string, int, Task> query = async (string continuationToken, int maxDop) =>
            {
                try
                {
                    DocumentFeedResponse<dynamic> r = await client.CreateDocumentQuery(
                            coll,
                            "SELECT * FROM r",
                            new FeedOptions
                            {
                                EnableCrossPartitionQuery = true,
                                MaxItemCount = 1,
                                RequestContinuationToken = continuationToken,
                                MaxDegreeOfParallelism = maxDop
                            }).AsDocumentQuery().ExecuteNextAsync();
                    Assert.Fail("Expected exception");
                }
                catch (DocumentClientException ex)
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
                }
            };

            await query(resultSeq.ResponseContinuation.Replace("\"FF\"", "\"AA\""), 0);
            await query(resultSeq.ResponseContinuation.Replace("\"\"", "\"00\""), 0);

            //todo:jmondal
            //await query(resultParallel.ResponseContinuation.Replace("\"FF\"", "\"AA\""), 1);
            //await query(resultParallel.ResponseContinuation.Replace("\"\"", "\"00\""), 1);

            Func<string, Task> gwQuery = async (string continuationToken) =>
            {
                Uri baseUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
                string masterKey = ConfigurationManager.AppSettings["MasterKey"];

                Uri uri = new Uri(baseUri, new Uri(coll.SelfLink + "docs", UriKind.Relative));
                SqlQuerySpec querySpec = new SqlQuerySpec(string.Format("SELECT * FROM r"));
                using (HttpClient httpClient = new HttpClient())
                {
                    StoreRequestNameValueCollection headers = new StoreRequestNameValueCollection();
                    httpClient.AddMasterAuthorizationHeader("post", coll.ResourceId, "docs", headers, masterKey);
                    httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                    httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.EnableScanInQuery, bool.TrueString);
                    httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.EnableCrossPartitionQuery, bool.TrueString);
                    httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Version, HttpConstants.Versions.v2017_01_19);
                    httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Continuation, continuationToken);

                    StringContent stringContent = new StringContent(JsonConvert.SerializeObject(querySpec), Encoding.UTF8, "application/query+json");
                    stringContent.Headers.ContentType.CharSet = null;
                    using (HttpResponseMessage message = await httpClient.PostAsync(uri, stringContent))
                    {
                        string responseContent = await message.Content.ReadAsStringAsync();
                        Assert.AreEqual(HttpStatusCode.NotFound, message.StatusCode);
                    }
                }
            };


            await gwQuery(resultParallel.ResponseContinuation.Replace("\"FF\"", "\"AA\""));
            await gwQuery(resultParallel.ResponseContinuation.Replace("\"\"", "\"00\""));

        }

        /*
        [TestMethod]
        public async Task TestUpdateCollectionIndexingPolicyWhenAddingDocsAndRecyclingReplicas()
        {
            try
            {
                var clients = ReplicationTests.GetClientsLocked(tokenType: AuthorizationTokenType.SystemAll);
                var primaryClient = clients[0];
                await TestCommon.DeleteAllDatabasesAsync(primaryClient);

                string uniqDatabaseName = "ValidateUpdateCollectionIndexingPolicy_DB_" + Guid.NewGuid().ToString("N");
                Database database = await primaryClient.CreateDatabaseAsync(new Database { Id = uniqDatabaseName });

                string uniqCollectionName = "ValidateUpdateCollectionIndexingPolicy_COLL_" + Guid.NewGuid().ToString("N");
                DocumentCollection collection = await primaryClient.CreateDocumentCollectionAsync(
                    database.SelfLink,
                    new DocumentCollection { Id = uniqCollectionName },
                    new RequestOptions { OfferThroughput = 10000 });

                var loadDocsTask = Task.Run(async () =>
                {
                    Logger.LogLine("Adding documents to collection.");
                    await LoadDocuments(collection);
                    Logger.LogLine("All the documents are added to collection.");
                });

                var random = new Random();
                const int iterations = 5;

                var updateIndexingPolicyTask = Task.Run(async () =>
                {
                    for (int i = 0; i < iterations; ++i)
                    {
                        Logger.LogLine("Update indexing policy iteration: #{0}.", i + 1);
                        await this.UpdateCollectionIndexingPolicyRandomlyAsync(primaryClient, collection, random);
                    }
                });

                var recycleReplicaTask = this.RecycleReplicaRandomlyAsync(clients, collection, random, iterations, excludePrimary: true);

                await Task.WhenAll(loadDocsTask, updateIndexingPolicyTask, recycleReplicaTask);

                Logger.LogLine("Final iteration: updating collection indexing policy to consistent.");
                collection = new DocumentCollection { Id = collection.Id, SelfLink = collection.SelfLink };
                await TestCommon.AsyncRetryRateLimiting(() => primaryClient.ReplaceDocumentCollectionAsync(collection));

                Logger.LogLine("Waiting for reindexing to finish on all the replicas.");
                await Task.WhenAll(clients.Select(c => Task.Run(async () =>
                {
                    await Util.WaitForReIndexingToFinish(300, collection);
                    Logger.LogLine("Reindexer finished on {0}.", c.GetAddress());
                })));

                Logger.LogLine("Running query oracle on all the replicas.");
                await Task.WhenAll(clients.Select(c => Task.Run(() =>
                {
                    QueryOracle.QueryOracle qo = new QueryOracle.QueryOracle(c, collection.SelfLink, false, 20000);
                    Assert.AreEqual(0, qo.IndexAndValidate(100), "Query oracle validation failed");
                })));
                Logger.LogLine("Query oracle on all the replicas is completed.");

                await TestCommon.DeleteAllDatabasesAsync(primaryClient);
            }
            finally
            {
                DocumentClient client = TestCommon.CreateClient(true, Protocol.Tcp);
                TestCommon.DeleteAllDatabasesAsync().Wait();
            }
        }

        [TestMethod]
        public async Task TestQueryWithTimestamp()
        {
            try
            {
                await TestCommon.DeleteAllDatabasesAsync(this.client);

                using (await TestCommon.OverrideFederationWideConfigurationsAsync(
                    Tuple.Create<string, object>("lazyIndexForceCheckpointIntervalInSeconds", 1)))
                {
                    Database database = (await this.client.CreateDatabaseAsync(new Database { Id = "db01" })).Resource;

                    DocumentCollection collection = new DocumentCollection { Id = "coll01" };
                    collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
                    collection = (await this.client.CreateDocumentCollectionAsync(database, collection)).Resource;
                    await TestQueryWithTimestampOnCollectionAsync(collection);

                    collection = new DocumentCollection { Id = "coll02" };
                    collection.IndexingPolicy.IndexingMode = IndexingMode.Lazy;
                    collection = (await this.client.CreateDocumentCollectionAsync(database, collection)).Resource;
                    await TestQueryWithTimestampOnCollectionAsync(collection);
                }
            }
            finally
            {
                TestCommon.DeleteAllDatabasesAsync(this.client).Wait();
            }
        }
        */

        //Query metrics are not on by default anymore, but turned on hin Feed options. This to be quarantined until a recent FI from master to direct and sdk is completed
        [Ignore] // Need to use v3 pipeline
        [TestMethod]
        public void TestQueryMetricsHeaders()
        {

            Database database = TestCommon.RetryRateLimiting<Database>(() =>
            {
                return this.client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() }).Result.Resource;
            });

            this.TestQueryMetricsHeaders(database, true);

            TestCommon.RetryRateLimiting<ResourceResponse<Database>>(() =>
            {
                return this.client.DeleteDatabaseAsync(database).Result;
            });
        }

        [Ignore] // Ignore until backend index utilization is on by default and other query metrics test are completed
        [TestMethod]
        public async Task TestIndexUtilizationParsing()
        {

            Database database = await this.client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() });

            DocumentCollection collection;
            RequestOptions options = new RequestOptions();
            
            collection = new DocumentCollection()
            {
                Id = Guid.NewGuid().ToString()
            };

            options.OfferThroughput = 10000;

            collection = await TestCommon.CreateCollectionAsync(this.client, database, collection, options);

            int maxDocumentCount = 2000;
            for (int i = 0; i < maxDocumentCount; i++)
            {
                QueryDocument doc = new QueryDocument()
                {
                    Id = Guid.NewGuid().ToString(),
                    NumericField = i,
                    StringField = i.ToString(CultureInfo.InvariantCulture),
                };

                await this.client.CreateDocumentAsync(collection, doc);
            }

            DocumentFeedResponse<dynamic> result = await this.client.CreateDocumentQuery<Document>(collection, "SELECT r.id FROM root r WHERE r.name = 'Julien' and r.age > 12", new FeedOptions() { PopulateQueryMetrics = true, EnableCrossPartitionQuery = true }).AsDocumentQuery().ExecuteNextAsync();
            Assert.IsNotNull(result.ResponseHeaders[WFConstants.BackendHeaders.QueryMetrics], "Expected metrics headers for query");
            Assert.IsNotNull(result.ResponseHeaders[WFConstants.BackendHeaders.IndexUtilization], "Expected index utilization headers for query"); 

            QueryMetrics queryMetrics = new QueryMetrics(
                BackendMetrics.ParseFromDelimitedString(result.ResponseHeaders[WFConstants.BackendHeaders.QueryMetrics]),
                IndexUtilizationInfo.CreateFromString(result.ResponseHeaders[WFConstants.BackendHeaders.IndexUtilization]),
                ClientSideMetrics.Empty);
            
            // If these fields populate then the parsing is successful and correct.
            Assert.AreEqual("/name/?", queryMetrics.IndexUtilizationInfo.UtilizedSingleIndexes[0].IndexDocumentExpression);
            Assert.AreEqual(String.Join(", ", new object[] { "/name ASC", "/age ASC" }), String.Join(", ", queryMetrics.IndexUtilizationInfo.PotentialCompositeIndexes[0].IndexDocumentExpressions));
            
            await this.client.DeleteDatabaseAsync(database);
        }

        [TestMethod]
        public async Task TestQueryMetricsNonZero()
        {
            DocumentClient client = TestCommon.CreateClient(false);

            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 1;

            QueryOracle.QueryOracleUtil util = new QueryOracle.QueryOracle2(seed);

            string guid = Guid.NewGuid().ToString();
            Database database = await client.CreateDatabaseAsync(new Database { Id = "db" + guid });
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            DocumentCollection coll = await TestCommon.CreateCollectionAsync(client,
                database,
                new DocumentCollection
                {
                    Id = "coll" + guid,
                    PartitionKey = partitionKeyDefinition
                },
                new RequestOptions { OfferThroughput = 5000 });

            IEnumerable<string> serializedDocuments = util.GetDocuments(numberOfDocuments);
            IList<Document> documents = new List<Document>(serializedDocuments.Count());
            foreach (string document in serializedDocuments)
            {
                ResourceResponse<Document> response = await client.CreateDocumentAsync(coll.SelfLink, JsonConvert.DeserializeObject(document));
                documents.Add(response.Resource);
            }

            FeedOptions feedOptions = new FeedOptions
            {
                PopulateQueryMetrics = true,
                PartitionKey = new PartitionKey("1")
            };

            IDocumentQuery<dynamic> documentQuery = client.CreateDocumentQuery(coll, "SELECT TOP 1 * FROM c", feedOptions).AsDocumentQuery();

            DocumentFeedResponse<dynamic> feedResonse = await documentQuery.ExecuteNextAsync();

            QueryMetrics queryMetrics = QueryMetrics.CreateFromIEnumerable(feedResonse.QueryMetrics.Values);

            Assert.IsTrue(queryMetrics.BackendMetrics.RetrievedDocumentCount > 0);
            Assert.IsTrue(queryMetrics.BackendMetrics.RetrievedDocumentSize > 0);
            Assert.IsTrue(queryMetrics.BackendMetrics.OutputDocumentCount > 0);
            Assert.IsTrue(queryMetrics.BackendMetrics.OutputDocumentSize > 0);
            Assert.IsTrue(queryMetrics.BackendMetrics.IndexHitRatio > 0);

            await client.DeleteDatabaseAsync(database);
        }

        [TestMethod]
        [Ignore] //Ignore until v3 support query metrics
        public void TestForceQueryScanHeaders()
        {
            Database database = TestCommon.RetryRateLimiting<Database>(() =>
            {
                return this.client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() }).Result.Resource;
            });

            this.TestForceQueryScanHeaders(database, true);

            TestCommon.RetryRateLimiting<ResourceResponse<Database>>(() =>
            {
                return this.client.DeleteDatabaseAsync(database).Result;
            });
        }

        private void TestForceQueryScanHeaders(Database database, bool partitionedCollection)
        {
            DocumentCollection collection;
            RequestOptions options = new RequestOptions();
            if (!partitionedCollection)
            {
                collection = new DocumentCollection()
                {
                    Id = Guid.NewGuid().ToString()
                };

                options.OfferThroughput = 10000;
            }
            else
            {
                collection = new DocumentCollection()
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Paths = new Collection<string> { "/id" },
                        Kind = PartitionKind.Hash
                    }
                };

                options.OfferThroughput = 20000;
            }

            collection = TestCommon.RetryRateLimiting<DocumentCollection>(() =>
            {
                return TestCommon.CreateCollectionAsync(this.client, database, collection, options).Result;
            });

            int maxDocumentCount = 200;
            for (int i = 0; i < maxDocumentCount; i++)
            {
                TestCommon.RetryRateLimiting<Document>(() =>
                {
                    return this.client.CreateDocumentAsync(collection, new Document() { Id = Guid.NewGuid().ToString() }).Result.Resource;
                });
            }

            // Run the query once with the flag and once without.
            // The index utilization should be 0 with force scan and close to a 100 without.
            string query = "SELECT r.id FROM root r WHERE r._ts > 0";

            FeedOptions feedOptions;
            DocumentFeedResponse<dynamic> result;
            QueryMetrics queryMetrics;

            // With ForceQueryScan
            feedOptions = new FeedOptions()
            {
                ForceQueryScan = true,
                MaxItemCount = -1,
                EnableCrossPartitionQuery = partitionedCollection,
                PopulateQueryMetrics = true,
                MaxDegreeOfParallelism = 10,
            };
            result = this.client.CreateDocumentQuery<Document>(
                    collection,
                    query,
                    feedOptions).AsDocumentQuery().ExecuteNextAsync().Result;
            queryMetrics = result.QueryMetrics.Values.Aggregate((curr, acc) => curr + acc);
            Assert.AreEqual(TimeSpan.Zero, queryMetrics.BackendMetrics.IndexLookupTime);

            // Without ForceQueryScan
            feedOptions = new FeedOptions()
            {
                ForceQueryScan = false,
                MaxItemCount = -1,
                EnableCrossPartitionQuery = partitionedCollection,
                PopulateQueryMetrics = true,
                MaxDegreeOfParallelism = 10,
            };
            result = this.client.CreateDocumentQuery<Document>(
                    collection,
                    query,
                    feedOptions).AsDocumentQuery().ExecuteNextAsync().Result;
            queryMetrics = result.QueryMetrics.Values.Aggregate((curr, acc) => curr + acc);
            Assert.AreNotEqual(TimeSpan.Zero, queryMetrics.BackendMetrics.IndexLookupTime);
        }

        private void TestFeedOptionInput(
            string feedOptionPropertyName,
            string componentPropertyName,
            List<Tuple<int?, int>> inputOutputs)
        {
            Database database = TestCommon.RetryRateLimiting<Database>(() =>
            {
                return this.client.CreateDatabaseAsync(
                    new Database()
                    {
                        Id = Guid.NewGuid().ToString()
                    }).Result.Resource;
            });

            DocumentCollection documentCollection = this.client.CreateDocumentCollectionAsync(
                database.SelfLink,
                new DocumentCollection()
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = new PartitionKeyDefinition()
                    {
                        Kind = PartitionKind.Hash,
                        Paths = new Collection<string>()
                        {
                            "/id",
                        }
                    }
                }).Result.Resource;

            foreach (Tuple<int?, int> inputOutput in inputOutputs)
            {
                int? input = inputOutput.Item1;
                int output = inputOutput.Item2;

                FeedOptions feedOptions = new FeedOptions()
                {
                    EnableCrossPartitionQuery = true
                };

                if (input.HasValue)
                {
                    PropertyInfo propertyInfo = feedOptions.GetType().GetProperty(feedOptionPropertyName);
                    propertyInfo.SetValue(feedOptions, input.Value);
                }

                IDocumentQuery<Document> documentQuery = this.client
                    .CreateDocumentQuery<Document>(documentCollection, "SELECT * FROM c ORDER BY c._ts", feedOptions)
                    .AsDocumentQuery();

                // Execute Once to force the execution context to initialize
                DocumentFeedResponse<dynamic> garbage = documentQuery.ExecuteNextAsync().Result;

                // Get the value using reflection.
                Type documentQueryType = documentQuery.GetType();
                object queryExecutionContext = documentQueryType
                    .GetField("queryExecutionContext", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(documentQuery);
                Type queryExecutionContextType = queryExecutionContext.GetType();
                if (queryExecutionContextType == typeof(ProxyDocumentQueryExecutionContext))
                {
                    // UnWrap the inner context
                    queryExecutionContext = queryExecutionContextType
                        .GetField("innerExecutionContext", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(queryExecutionContext);
                    queryExecutionContextType = queryExecutionContext.GetType();
                }

                object component = queryExecutionContextType
                    .GetField("component", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(queryExecutionContext);
                Type componentType = component.GetType();
                int feedOptionsValue = (int)componentType
                    .GetProperty(componentPropertyName, BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(component);

                Assert.AreEqual(
                    output,
                    feedOptionsValue,
                    $"Expected {feedOptionPropertyName} to be {output} when FeedOptions.{feedOptionPropertyName} = {input}, but instead got {feedOptionsValue}");
            }
        }

        [TestMethod]
        public void TestContinuationLimitHeaders()
        {

            Database database = TestCommon.RetryRateLimiting<Database>(() =>
            {
                return this.client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() }).Result.Resource;
            });

            this.TestContinuationLimitHeaders(database, true);

            TestCommon.RetryRateLimiting<ResourceResponse<Database>>(() =>
            {
                return this.client.DeleteDatabaseAsync(database).Result;
            });
        }

        private void TestContinuationLimitHeaders(Database database, bool partitionedCollection)
        {
            DocumentCollection collection;
            RequestOptions options = new RequestOptions();
            if (!partitionedCollection)
            {
                collection = new DocumentCollection()
                {
                    Id = Guid.NewGuid().ToString()
                };

                options.OfferThroughput = 10000;
            }
            else
            {
                collection = new DocumentCollection()
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Paths = new Collection<string> { "/id" },
                        Kind = PartitionKind.Hash
                    }
                };

                options.OfferThroughput = 20000;
            }

            collection = TestCommon.RetryRateLimiting<DocumentCollection>(() =>
            {
                return TestCommon.CreateCollectionAsync(this.client, database, collection, options).Result;
            });

            int maxDocumentCount = 200;
            for (int i = 0; i < maxDocumentCount; i++)
            {
                TestCommon.RetryRateLimiting<Document>(() =>
                {
                    return this.client.CreateDocumentAsync(collection, new Document() { Id = Guid.NewGuid().ToString() }).Result.Resource;
                });
            }

            FeedOptions feedOptions = new FeedOptions() { ResponseContinuationTokenLimitInKb = 0, MaxItemCount = 10, EnableCrossPartitionQuery = partitionedCollection };
            DocumentFeedResponse<dynamic> result = null;

            try
            {
                result = this.client.CreateDocumentQuery<Document>(
                    collection,
                    "SELECT r.id FROM root r WHERE r._ts > 0",
                    feedOptions).AsDocumentQuery().ExecuteNextAsync().Result;
                Assert.Fail("Expected query to fail");
            }
            catch (AggregateException e)
            {
                if (!(e.InnerException is DocumentClientException exception))
                {
                    throw e;
                }

                if (exception.StatusCode != HttpStatusCode.BadRequest)
                {
                    throw e;
                }

                Assert.IsTrue(exception.Message.Contains("continuation token limit specified is not large enough"));
            }

            feedOptions.ResponseContinuationTokenLimitInKb = -1;
            try
            {
                result = this.client.CreateDocumentQuery<Document>(
                    collection,
                    "SELECT r.id FROM root r WHERE r._ts > 0",
                    feedOptions).AsDocumentQuery().ExecuteNextAsync().Result;
                Assert.Fail("Expected query to fail");
            }
            catch (AggregateException e)
            {
                if (!(e.InnerException is DocumentClientException exception))
                {
                    throw e;
                }

                if (exception.StatusCode != HttpStatusCode.BadRequest)
                {
                    throw e;
                }

                Assert.IsTrue(exception.Message.Contains("Please pass in a valid continuation token size limit which must be a positive integer"));
            }

            feedOptions.ResponseContinuationTokenLimitInKb = 1;
            result = this.client.CreateDocumentQuery<Document>(collection, "SELECT r.id FROM root r WHERE r._ts > 0", feedOptions).AsDocumentQuery().ExecuteNextAsync().Result;
            string continuation = result.ResponseContinuation;
            Assert.IsTrue(
                continuation.StartsWith("CGW") || (!continuation.Contains("#FPC") && !continuation.Contains("#FPP")),
                $"{continuation} neither constructed by Compute nor proper BE token");

            feedOptions.ResponseContinuationTokenLimitInKb = 2;
            result = this.client.CreateDocumentQuery<Document>(collection, "SELECT r.id FROM root r WHERE r._ts > 0", feedOptions).AsDocumentQuery().ExecuteNextAsync().Result;
            continuation = result.ResponseContinuation;
            Assert.IsTrue(
                continuation.StartsWith("CGW") || (continuation.Contains("#FPC") || continuation.Contains("#FPP")),
                $"{continuation} neither constructed by Compute nor proper BE token");
        }

        private void TestQueryMetricsHeaders(Database database, bool partitionedCollection)
        {
            DocumentCollection collection;
            RequestOptions options = new RequestOptions();
            if (!partitionedCollection)
            {
                collection = new DocumentCollection()
                {
                    Id = Guid.NewGuid().ToString()
                };

                options.OfferThroughput = 10000;
            }
            else
            {
                collection = new DocumentCollection()
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Paths = new Collection<string> { "/id" },
                        Kind = PartitionKind.Hash
                    }
                };

                options.OfferThroughput = 20000;
            }

            collection = TestCommon.RetryRateLimiting<DocumentCollection>(() =>
            {
                return TestCommon.CreateCollectionAsync(this.client, database, collection, options).Result;
            });

            int maxDocumentCount = 2000;
            for (int i = 0; i < maxDocumentCount; i++)
            {
                QueryDocument doc = new QueryDocument()
                {
                    Id = Guid.NewGuid().ToString(),
                    NumericField = i,
                    StringField = i.ToString(CultureInfo.InvariantCulture),
                };

                TestCommon.RetryRateLimiting<Document>(() =>
                {
                    return this.client.CreateDocumentAsync(collection, doc).Result.Resource;
                });
            }

            // simple validations - existence - yes & no
            DocumentFeedResponse<dynamic> result = this.client.CreateDocumentQuery<Document>(collection, "SELECT r.id FROM root r", new FeedOptions() { EnableCrossPartitionQuery = true }).AsDocumentQuery().ExecuteNextAsync().Result;
            Assert.IsNull(result.ResponseHeaders[WFConstants.BackendHeaders.QueryMetrics], "Expected no metrics headers for query");
            Assert.IsNull(result.ResponseHeaders[WFConstants.BackendHeaders.IndexUtilization], "Expected no index utilization headers for query");

            result = this.client.CreateDocumentQuery<Document>(collection, "SELECT r.id FROM root r", new FeedOptions() { PopulateQueryMetrics = true, EnableCrossPartitionQuery = true }).AsDocumentQuery().ExecuteNextAsync().Result;
            Assert.IsNotNull(result.ResponseHeaders[WFConstants.BackendHeaders.QueryMetrics], "Expected metrics headers for query");
            Assert.IsNull(result.ResponseHeaders[WFConstants.BackendHeaders.IndexUtilization], "Expected index utilization headers for query"); // False for now

            this.ValidateQueryMetricsHeadersOverContinuations(collection, maxDocumentCount).Wait();
        }

        private async Task ValidateQueryMetricsHeadersOverContinuations(
            DocumentCollection coll,
            int documentCount)
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            System.Diagnostics.Trace.TraceInformation("seed: " + seed);
            Random rand = new Random(seed);

            int[] numericFieldFilters = new int[] { rand.Next(documentCount), rand.Next(documentCount), rand.Next(documentCount) };
            string inClauseArgument = string.Join(",", numericFieldFilters);
            string[] queries = {
                "SELECT * FROM r ORDER BY r._ts",
                "SELECT r.id FROM root r WHERE r.NumericField in (" + inClauseArgument + ") ORDER BY r._ts" };
            int[] pageSizes = { 100, 200, 2000 };

            foreach (string query in queries)
            {
                foreach (int pageSize in pageSizes)
                {
                    List<Document> retrievedDocuments = new List<Document>();

                    string continuationToken = default(string);
                    bool hasMoreResults;

                    do
                    {
                        FeedOptions feedOptions = new FeedOptions
                        {
                            EnableCrossPartitionQuery = true,
                            MaxDegreeOfParallelism = -1,
                            RequestContinuationToken = continuationToken,
                            MaxItemCount = pageSize,
                            PopulateQueryMetrics = true
                        };

                        using (IDocumentQuery<Document> documentQuery = this.client.CreateDocumentQuery<Document>(
                            coll,
                            query,
                                feedOptions).AsDocumentQuery())
                        {
                            DocumentFeedResponse<Document> response = await documentQuery.ExecuteNextAsync<Document>();
                            string responseQueryMetrics = response.ResponseHeaders[WFConstants.BackendHeaders.QueryMetrics];
                            string indexUtilization = response.ResponseHeaders[WFConstants.BackendHeaders.IndexUtilization];

                            QueryMetrics queryMetrics = new QueryMetrics(
                                BackendMetrics.ParseFromDelimitedString(responseQueryMetrics),
                                IndexUtilizationInfo.CreateFromString(indexUtilization),
                                ClientSideMetrics.Empty);
                            this.ValidateQueryMetrics(queryMetrics);

                            foreach (KeyValuePair<string, QueryMetrics> pair in response.QueryMetrics)
                            {
                                System.Diagnostics.Trace.TraceInformation(JsonConvert.SerializeObject(pair));
                                this.ValidateQueryMetrics(pair.Value);
                            }

                            continuationToken = response.ResponseContinuation;
                            hasMoreResults = documentQuery.HasMoreResults;
                        }
                    } while (hasMoreResults);
                }
            }
        }

        private void ValidateQueryMetrics(QueryMetrics metrics)
        {
            Assert.AreEqual(0, metrics.ClientSideMetrics.Retries);
            //We are not checking VMExecutionTime, since that is not a public property
            //Assert.IsTrue(metrics.QueryEngineTimes.VMExecutionTime.TotalMilliseconds > 0, "Expected VMExecutionTimeInMs to be > 0, metrics = {0}", metrics);
            Assert.IsTrue(metrics.BackendMetrics.QueryPreparationTimes.QueryCompilationTime.TotalMilliseconds > 0, "Expected CompileTimeInMs to be > 0, metrics = {0}", metrics);
            //We are not checking DocumentLoadTime and RetrievedDocumentCount, since some queries don't return any documents (especially in the last continuation).
            //Assert.IsTrue(metrics.QueryEngineTimes.DocumentLoadTime.TotalMilliseconds > 0, "Expected DocumentLoadTimeInMs to be > 0, metrics = {0}", metrics);
            //Assert.IsTrue(metrics.RetrievedDocumentCount > 0, "Expected RetrievedDocumentCount to be > 0, metrics = {0}", metrics);
            Assert.IsTrue(metrics.BackendMetrics.TotalTime.TotalMilliseconds > 0, "Expected TotalExecutionTimeInMs to be > 0, metrics = {0}", metrics);
            //Assert.IsTrue(metrics.QueryEngineTimes.WriteOutputTime.TotalMilliseconds > 0, "Expected WriteOutputTimeInMs to be > 0, metrics = {0}", metrics);
            //Assert.IsTrue(metrics.RetrievedDocumentSize > 0, "Expected RetrievedDocumentSize to be > 0, metrics = {0}", metrics);
            Assert.IsTrue(metrics.BackendMetrics.IndexLookupTime.TotalMilliseconds > 0, "Expected IndexLookupTimeInMs to be > 0, metrics = {0}", metrics);
            Assert.IsTrue(metrics.BackendMetrics.QueryPreparationTimes.LogicalPlanBuildTime.TotalMilliseconds > 0, "Expected LogicalPlanBuildTimeInMs to be > 0, metrics = {0}", metrics);
            //Assert.AreEqual(metrics.QueryEngineTimes.VMExecutionTime - metrics.QueryEngineTimes.IndexLookupTime - metrics.QueryEngineTimes.DocumentLoadTime - metrics.QueryEngineTimes.WriteOutputTime,
            //    metrics.QueryEngineTimes.RuntimeExecutionTimes.TotalTime);
            Assert.IsTrue(metrics.BackendMetrics.RuntimeExecutionTimes.QueryEngineExecutionTime >= metrics.BackendMetrics.RuntimeExecutionTimes.SystemFunctionExecutionTime + metrics.BackendMetrics.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime,
                "Expected Query VM Execution Time to be > {0}, metrics = {1}", metrics.BackendMetrics.RuntimeExecutionTimes.SystemFunctionExecutionTime + metrics.BackendMetrics.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime, metrics);
            //Assert.IsTrue(metrics.QueryEngineTimes.VMExecutionTime >= metrics.QueryEngineTimes.RuntimeExecutionTimes.TotalTime,
            //    "Expected Query VM Execution Time to be > {0}, metrics = {1}", metrics.QueryEngineTimes.RuntimeExecutionTimes.TotalTime, metrics);
        }

        private async Task UpdateCollectionIndexingPolicyRandomlyAsync(DocumentClient client, DocumentCollection collection, Random random)
        {
            Logger.LogLine("Start to update indexing policy.");

            // Compute the new index policy based on the random number generator.
            collection = new DocumentCollection { Id = collection.Id, SelfLink = collection.SelfLink };

            // Higher probability to get consistent indexing mode.
            switch (random.Next(4))
            {
                case 0:
                    collection.IndexingPolicy.IndexingMode = IndexingMode.None;
                    break;

                case 1:
                    collection.IndexingPolicy.IndexingMode = IndexingMode.Lazy;
                    break;

                default:
                    collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
                    break;
            }

            if (collection.IndexingPolicy.IndexingMode == IndexingMode.None)
            {
                collection.IndexingPolicy.Automatic = false;
            }
            else
            {
                // Higher probability to get automatic indexing policy.
                collection.IndexingPolicy.Automatic = random.Next(3) != 0;
            }

            Logger.LogLine("IndexingMode: {0}, Automatic: {1}", collection.IndexingPolicy.IndexingMode, collection.IndexingPolicy.Automatic);

            if (collection.IndexingPolicy.IndexingMode != IndexingMode.None)
            {
                if (random.Next(2) == 0)
                {
                    collection.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/" });
                }
                else
                {
                    collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath() { Path = "/" });
                }

                string[] candidatePaths =
                {
                    "/\"artist\"/?",
                    "/\"timestamp\"/?",
                    "/\"track_id\"/?",
                    "/\"title\"/?",
                    "/\"similars\"/*",
                    "/\"tags\"/*",
                };

                foreach (string path in candidatePaths)
                {
                    switch (random.Next(3))
                    {
                        case 0:
                            collection.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = path });
                            break;

                        case 1:
                            collection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath() { Path = path });
                            break;

                        default:
                            // Neither included nor excluded.
                            break;
                    }
                }

                Logger.LogLine("Included paths: ({0})", collection.IndexingPolicy.IncludedPaths.Count);
                foreach (IncludedPath indexPath in collection.IndexingPolicy.IncludedPaths)
                {
                    Logger.LogLine(" * {0}", indexPath.Path);
                }

                Logger.LogLine("Excluded paths: ({0})", collection.IndexingPolicy.ExcludedPaths.Count);
                foreach (ExcludedPath path in collection.IndexingPolicy.ExcludedPaths)
                {
                    Logger.LogLine(" * {0}", path);
                }
            }

            Logger.LogLine("Updating collection indexing policy.");
            await this.RetryActionAsync(() => client.ReplaceDocumentCollectionAsync(collection), 10, TimeSpan.FromSeconds(5));

            Logger.LogLine("Waiting for reindexing to finish.");
            await Util.WaitForReIndexingToFinish(300, collection);
        }

        private async Task RetryActionAsync(Func<Task> action, int maxRetries, TimeSpan waitInterval)
        {
            int retries = 0;
            do
            {
                Exception exception = null;
                try
                {
                    await action();
                    return;
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                if (++retries < maxRetries)
                {
                    Logger.LogLine("Retry Exception: {0}.", exception);
                    await Task.Delay(waitInterval);
                }
                else
                {
                    Assert.Fail("Failed after {0} retries (wait interval: {1}s). Exception: {2}.", retries, waitInterval.TotalSeconds, exception);
                }
            } while (true);
        }

        private async Task LoadDocuments(DocumentCollection coll)
        {
            await this.LoadDocuments(coll, File.ReadAllLines(@"Documents\MillionSong1KDocuments.txt"));
        }

        private async Task LoadDocuments(DocumentCollection coll, IEnumerable<string> serializedDocuments)
        {
            string script = this.MakeCreateDocumentsScript();
            StoredProcedure sproc = await Util.GetOrCreateStoredProcedureAsync(this.client, coll, new StoredProcedure { Id = "bulkInsert", Body = script });

            List<string> documents = new List<string>();
            RequestOptions requestOptions = new RequestOptions
            {
                PartitionKey = new PartitionKey("test")
            };
            foreach (string line in serializedDocuments)
            {
                documents.Add(line);
                if (documents.Count == 15)
                {
                    await TestCommon.AsyncRetryRateLimiting(() =>
                        this.client.ExecuteStoredProcedureAsync<dynamic>(sproc, requestOptions, new[] { documents.ToArray() }));
                    documents = new List<string>();
                }
            }

            if (documents.Count != 0)
            {
                await TestCommon.AsyncRetryRateLimiting(() =>
                    this.client.ExecuteStoredProcedureAsync<dynamic>(sproc, requestOptions, new[] { documents.ToArray() })); ;
            }
        }

        private string MakeCreateDocumentsScript()
        {
            const string scriptTemplate = @"
function sproc(feed) {
    if (!feed) feed = new Array();
    var links = new Array();
    var i = 0;

    var collection = getContext().getCollection();
    var collectionLink = collection.getSelfLink();
    AddDocument();

    function AddDocument() {
        var document = feed[i];

        if (!collection.createDocument(collectionLink, document, onCreateDocument)) {
            setResponseBody();
        } else {
            i++;
        }
    }

    function onCreateDocument(err, responseBody, responseOptions) {
        if (err) {
            if (err.number == 409) {
                // The doc is already in the collection. Ignore the error and continue to add the next doc.
                moveNext();
                return;
            }

            throw JSON.stringify(err);
        }

        links.push(responseBody._self)

        moveNext();
    }

    function moveNext() {
        if (i < feed.length) {
            AddDocument();
        } else {
            setResponseBody();
        }
    }

    function setResponseBody() {
        getContext().getResponse().setBody({ links : links });
    }
}
";
            return scriptTemplate;
        }

        internal void TestQueryDocuments(DocumentCollection collection, bool manualIndex = false)
        {
            List<QueryDocument> listQueryDocuments = new List<QueryDocument>();
            foreach (int index in Enumerable.Range(1, 3))
            {
                QueryDocument doc = new QueryDocument()
                {
                    Id = string.Format(CultureInfo.InvariantCulture, "doc{0}", index),
                    NumericField = index,
                    StringField = index.ToString(CultureInfo.InvariantCulture),
                };
                doc.SetPropertyValue("pk", "test");
                INameValueCollection headers = new StoreRequestNameValueCollection();
                if (!collection.IndexingPolicy.Automatic && manualIndex)
                {
                    headers.Add("x-ms-indexing-directive", "include");
                }


                listQueryDocuments.Add(this.client.Create<QueryDocument>(collection.GetIdOrFullName(), doc, headers));
            }

            QueryDocument[] documents = listQueryDocuments.ToArray();
            this.TestSQLQuery(collection, documents);

        }

        private void TestSQLQuery(DocumentCollection collection, QueryDocument[] documents)
        {
            Action<DocumentClient> queryAction = (documentClient) =>
            {
                foreach (int index in Enumerable.Range(1, 3))
                {
                    string name = string.Format(CultureInfo.InvariantCulture, "doc{0}", index);

                    IEnumerable<Document> result = documentClient.CreateDocumentQuery<Document>(collection, @"SELECT r._rid FROM root r WHERE r.id=""" + name + @"""", new FeedOptions { EnableCrossPartitionQuery = true });
                    Assert.AreEqual(documents[index - 1].ResourceId, result.Single().ResourceId, "Expect queried id to match the id with the same name in the created documents");

                    result = documentClient.CreateDocumentQuery<Document>(collection, @"SELECT r._rid FROM root r WHERE r.NumericField=" + index.ToString(CultureInfo.InvariantCulture), new FeedOptions { EnableCrossPartitionQuery = true });
                    Assert.AreEqual(documents[index - 1].ResourceId, result.Single().ResourceId, "Expect queried id to match the id with the same name in the created documents");

                    result = documentClient.CreateDocumentQuery<Document>(collection, @"SELECT r._rid FROM root r WHERE r.StringField=""" + index.ToString(CultureInfo.InvariantCulture) + @"""", new FeedOptions { EnableCrossPartitionQuery = true });
                    Assert.AreEqual(documents[index - 1].ResourceId, result.Single().ResourceId, "Expect queried id to match the id with the same name in the created documents");
                }
            };

            Logger.LogLine("Primary Client");
            queryAction(this.client);

            Logger.LogLine("Primary ReadonlyClient");
            queryAction(this.primaryReadonlyClient);

            Logger.LogLine("Secondary ReadonlyClient");
            queryAction(this.secondaryReadonlyClient);
        }

        private void CleanUp()
        {
            IEnumerable<Database> allDatabases = from database in this.client.CreateDatabaseQuery()
                                                 select database;

            foreach (Database database in allDatabases)
            {
                this.client.DeleteDatabaseAsync(database.SelfLink).Wait();
            }
        }

        internal void TestQueryDocumentsWithIndexPaths(
            DocumentCollection collection,
            bool manualIndex = false,
            bool bExpectExcludedPathError = true,
            bool bExpectRangePathError = true)
        {
            List<QueryDocument> listQueryDocuments = new List<QueryDocument>();
            foreach (int index in Enumerable.Range(1, 3))
            {
                QueryDocument doc = new QueryDocument()
                {
                    Id = string.Format(CultureInfo.InvariantCulture, "doc{0}", index),
                    NumericField = index,
                    StringField = index.ToString(CultureInfo.InvariantCulture),
                };

                INameValueCollection headers = new StoreRequestNameValueCollection();
                if (!collection.IndexingPolicy.Automatic && manualIndex)
                {
                    headers.Add("x-ms-indexing-directive", "include");
                }


                listQueryDocuments.Add(this.client.Create<QueryDocument>(collection.GetIdOrFullName(), doc, headers));
            }

            QueryDocument[] documents = listQueryDocuments.ToArray();

            foreach (int index in Enumerable.Range(1, 3))
            {
                string name = string.Format(CultureInfo.InvariantCulture, "doc{0}", index);

                //independent of paths, must succeed.
                IEnumerable<QueryDocument> result;
                bool bException = false;
                try
                {
                    result = this.client.CreateDocumentQuery<QueryDocument>(collection.SelfLink, @"select * from root r where r.id =""" + name + @"""");
                    Assert.AreEqual(documents[index - 1].ResourceId, result.Single().ResourceId, "Expect queried id to match the id with the same name in the created documents");
                }
                catch (AggregateException e)
                {
                    bException = true;
                    Assert.IsTrue(e.InnerException.Message.Contains("An invalid query has been specified with filters against path(s) excluded from indexing"));
                }
                Assert.IsTrue(bException == false);

                result = this.client.CreateDocumentQuery<QueryDocument>(collection.SelfLink, @"select * from root r where r.NumericField =" + index.ToString(CultureInfo.InvariantCulture));
                Assert.AreEqual(documents[index - 1].ResourceId, result.Single().ResourceId, "Expect queried id to match the id with the same name in the created documents");

                bException = false;
                try
                {
                    result = this.client.CreateDocumentQuery<QueryDocument>(collection.SelfLink, @"select * from root r where r.NumericField >=" + index.ToString(CultureInfo.InvariantCulture) + @" and r.NumericField <=" + (index + 1).ToString(CultureInfo.InvariantCulture));
                    Assert.AreEqual(documents[index - 1].ResourceId, result.First().ResourceId, "Expect queried id to match the id with the same name in the created documents");
                }
                catch (AggregateException e)
                {
                    bException = true;
                    Assert.IsTrue(e.InnerException.Message.Contains("An invalid query has been specified with filters against path(s) that are not range-indexed"));
                }
                Assert.IsTrue(bException == bExpectRangePathError);

                try
                {
                    result = this.client.CreateDocumentQuery<QueryDocument>(collection.SelfLink, @"select * from root r where r.StringField=""" + index.ToString(CultureInfo.InvariantCulture) + @"""");
                    Assert.AreEqual(documents[index - 1].ResourceId, result.Single().ResourceId, "Expect queried id to match the id with the same name in the created documents");
                }
                catch (AggregateException e)
                {
                    bException = true;
                    Assert.IsTrue(e.InnerException.Message.Contains("An invalid query has been specified with filters against path(s) excluded from indexing"));
                }
                Assert.IsTrue(bException == bExpectExcludedPathError);
            }

        }

        private bool IsValidIndexingPath(Collection<IncludedPath> includedPaths, string expectedPathName, IndexKind expextedIndexKind)
        {
            bool bFound = false;

            foreach (IncludedPath path in includedPaths)
            {
                if (path.Path.Equals(expectedPathName))
                {
                    foreach (Index index in path.Indexes)
                    {
                        if (index.Kind.Equals(expextedIndexKind))
                        {
                            bFound = true;
                        }
                    }
                }
            }

            return bFound;
        }

        private async Task TestQueryWithTimestampOnCollectionAsync(DocumentCollection collection)
        {
            // Basic CRUD
            Document document = (await this.client.CreateDocumentAsync(collection, new Document { Id = "doc01" })).Resource;
            if (collection.IndexingPolicy.IndexingMode == IndexingMode.Lazy)
            {
                await Util.WaitForLazyIndexingToCompleteAsync(collection);
            }

            this.VerifyQueryWithTimestampShouldReturnDocument(collection, document.GetPropertyValue<long>("_ts"), document.Id);

            document = (await this.client.ReplaceDocumentAsync(document)).Resource;
            if (collection.IndexingPolicy.IndexingMode == IndexingMode.Lazy)
            {
                await Util.WaitForLazyIndexingToCompleteAsync(collection);
            }

            this.VerifyQueryWithTimestampShouldReturnDocument(collection, document.GetPropertyValue<long>("_ts"), document.Id);

            await this.client.DeleteDocumentAsync(document);
            if (collection.IndexingPolicy.IndexingMode == IndexingMode.Lazy)
            {
                await Util.WaitForLazyIndexingToCompleteAsync(collection);
            }

            this.VerifyQueryWithTimestampShouldReturnNothing(collection, document.GetPropertyValue<long>("_ts"));

            // Bulk insert
            string script = this.MakeCreateDocumentsScript();
            StoredProcedure sproc = await Util.GetOrCreateStoredProcedureAsync(this.client, collection, new StoredProcedure { Id = "bulkInsert", Body = script });

            Document[] documents = Enumerable.Repeat(new Document(), 10).ToArray();
            await this.client.ExecuteStoredProcedureAsync<dynamic>(sproc, new[] { documents });
            if (collection.IndexingPolicy.IndexingMode == IndexingMode.Lazy)
            {
                await Util.WaitForLazyIndexingToCompleteAsync(collection);
            }

            long minTimestamp = (await this.client.ReadDocumentFeedAsync(collection))
                .Min(doc => doc.GetPropertyValue<long>("_ts"));
            Assert.IsTrue(minTimestamp > 0);
            string query = "SELECT * FROM c where c._ts >= " + minTimestamp.ToString(CultureInfo.InvariantCulture);
            foreach (DocumentClient lockedClient in ReplicationTests.GetClientsLocked())
            {
                Document[] queryResult = lockedClient.CreateDocumentQuery<Document>(collection, query).ToArray();
                Assert.AreEqual(documents.Length, queryResult.Length);
            }
        }

        private void VerifyQueryWithTimestampShouldReturnDocument(DocumentCollection collection, long timestamp, string expectedDocumentId)
        {
            foreach (DocumentClient lockedClient in ReplicationTests.GetClientsLocked())
            {
                string query = "SELECT * FROM c where c._ts = " + timestamp.ToString(CultureInfo.InvariantCulture);
                Document[] queryResult = lockedClient.CreateDocumentQuery<Document>(collection, query).ToArray();
                Assert.AreEqual(1, queryResult.Length);
                Assert.AreEqual(expectedDocumentId, queryResult[0].Id);
            }
        }

        private void VerifyQueryWithTimestampShouldReturnNothing(DocumentCollection collection, long timestamp)
        {
            foreach (DocumentClient lockedClient in ReplicationTests.GetClientsLocked())
            {
                string query = "SELECT * FROM c where c._ts = " + timestamp.ToString(CultureInfo.InvariantCulture);
                Document[] queryResult = lockedClient.CreateDocumentQuery<Document>(collection, query).ToArray();
                Assert.AreEqual(0, queryResult.Length);
            }
        }

        internal class QueryDocument : Document
        {
            public int NumericField
            {
                get => base.GetValue<int>("NumericField");
                set => base.SetValue("NumericField", value);
            }

            public int NumericField2
            {
                get => base.GetValue<int>("NumericField2");
                set => base.SetValue("NumericField2", value);
            }

            public string StringField
            {
                get => base.GetValue<string>("StringField");
                set => base.SetValue("StringField", value);
            }
        }
    }
}