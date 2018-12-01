//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents.Services.Management.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class QueryTests
    {
        private DocumentClient client;
        private DocumentClient primaryReadonlyClient;
        private DocumentClient secondaryReadonlyClient;
        
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
                         ConfigurationManager.AppSettings["MasterKey"], connectionPolicy: null);

            this.secondaryReadonlyClient = new DocumentClient(
                         new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                         ConfigurationManager.AppSettings["MasterKey"], connectionPolicy: null);

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
            CosmosDatabaseSettings database = TestCommon.RetryRateLimiting<CosmosDatabaseSettings>(() =>
            {
                return this.client.CreateDatabaseAsync(new CosmosDatabaseSettings() { Id = Guid.NewGuid().ToString() }).Result.Resource;
            });

            CosmosContainerSettings collection = TestCommon.RetryRateLimiting<CosmosContainerSettings>(() =>
            {
                return TestCommon.CreateCollectionAsync(this.client, database, new CosmosContainerSettings() { Id = Guid.NewGuid().ToString() }).Result;
            });

            for (int i = 0; i < 200; i++)
            {
                TestCommon.RetryRateLimiting<Document>(() =>
                {
                    return this.client.CreateDocumentAsync(collection, new Document() { Id = Guid.NewGuid().ToString() }).Result.Resource;
                });
            }

            // default page size, expect 100 documents
            FeedResponse<dynamic> result = this.client.CreateDocumentQuery<Document>(collection, "SELECT r.id FROM root r").AsDocumentQuery().ExecuteNextAsync().Result;
            Assert.AreEqual(100, result.Count);

            // dynamic page size (-1), expect all documents to be returned
            result = this.client.CreateDocumentQuery<Document>(collection, "SELECT r.id FROM root r", new FeedOptions() { MaxItemCount = -1 }).AsDocumentQuery().ExecuteNextAsync().Result;
            Assert.AreEqual(200, result.Count);

            // page size 10
            result = this.client.CreateDocumentQuery<Document>(collection, "SELECT r.id FROM root r", new FeedOptions() { MaxItemCount = 10 }).AsDocumentQuery().ExecuteNextAsync().Result;
            Assert.AreEqual(10, result.Count);

            TestCommon.RetryRateLimiting<ResourceResponse<CosmosDatabaseSettings>>(() =>
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

                CosmosDatabaseSettings[] databases = (from index in Enumerable.Range(1, 3)
                                        select this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = string.Format(CultureInfo.InvariantCulture, "{0}{1}", dbprefix, index) })).ToArray();

                Action<DocumentClient> queryAction = (documentClient) =>
                {
                    // query by  name
                    foreach (var index in Enumerable.Range(1, 3))
                    {
                        string name = string.Format(CultureInfo.InvariantCulture, "{0}{1}", dbprefix, index);
                        IEnumerable<dynamic> queriedDatabases = documentClient.CreateDatabaseQuery(@"select * from root r where r.id = """ + name + @"""").AsEnumerable();
                        Assert.AreEqual(databases[index - 1].ResourceId, ((CosmosDatabaseSettings)queriedDatabases.Single()).ResourceId, "Expect queried id to match the id with the same name in the created database");
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

                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryCollection" + Guid.NewGuid().ToString() });

                CosmosContainerSettings[] collections = (from index in Enumerable.Range(1, 3)
                                                    select this.client.Create<CosmosContainerSettings>(database.GetIdOrFullName(), new CosmosContainerSettings { Id = string.Format(CultureInfo.InvariantCulture, "{0}{1}", collprefix, index) })).ToArray();
                Action<DocumentClient> queryAction = (documentClient) =>
                {
                    // query by  name
                    foreach (var index in Enumerable.Range(1, 3))
                    {
                        string name = string.Format(CultureInfo.InvariantCulture, "{0}{1}", collprefix, index);
                        IEnumerable<dynamic> queriedCollections = documentClient.CreateDocumentCollectionQuery(database, @"select * from root r where r.id = """ + name + @"""").AsEnumerable();
                        Assert.AreEqual(collections[index - 1].ResourceId, ((CosmosContainerSettings)queriedCollections.Single()).ResourceId, "Expect queried id to match the id with the same name in the created documents");
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
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentSecondaryIndexDatabase" + Guid.NewGuid().ToString() });
                CosmosContainerSettings collectionDefinition = new CosmosContainerSettings { Id = "TestQueryDocumentsSecondaryIndexCollection" + Guid.NewGuid().ToString() };
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

                CosmosContainerSettings collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;

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
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                CosmosContainerSettings collectionDefinition = new CosmosContainerSettings { Id = "TestQueryDocumentWithPathsCollection" + Guid.NewGuid().ToString() };

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

                CosmosContainerSettings collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;

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
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                CosmosContainerSettings collectionDefinition = new CosmosContainerSettings { Id = "TestQueryDocumentWithPathsCollection" };

                IndexingPolicyOld indexingPolicyOld = new IndexingPolicyOld();
                indexingPolicyOld.IncludedPaths.Add(new IndexingPath { IndexType = IndexType.Range, Path = @"/""NumericField""/?" });
                indexingPolicyOld.IncludedPaths.Add(new IndexingPath { IndexType = IndexType.Hash, Path = @"/" });
                indexingPolicyOld.IndexingMode = IndexingMode.Consistent;
                indexingPolicyOld.Automatic = true;

                indexingPolicyOld.ExcludedPaths.Add(@"/""A""/""B""/?");

                collectionDefinition.IndexingPolicy = IndexingPolicyTranslator.TranslateIndexingPolicyV1ToV2(indexingPolicyOld);

                Console.WriteLine("Count = {0}", collectionDefinition.IndexingPolicy.IncludedPaths.Count);
                CosmosContainerSettings collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;

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
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                CosmosContainerSettings collectionDefinition = new CosmosContainerSettings { Id = "TestQueryDocumentWithPathsCollection" };

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
                    CosmosContainerSettings collection = this.client.Create<CosmosContainerSettings>(database.GetIdOrFullName(), collectionDefinition);
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
                    CosmosContainerSettings collection = this.client.Create<CosmosContainerSettings>(database.GetIdOrFullName(), collectionDefinition);
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
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                CosmosContainerSettings collectionDefinition = new CosmosContainerSettings { Id = "TestQueryDocumentWithPathsCollection" };

                IndexingPolicyOld indexingPolicyOld = new IndexingPolicyOld();

                indexingPolicyOld.IncludedPaths.Add(new IndexingPath { IndexType = IndexType.Hash, Path = @"/" });
                indexingPolicyOld.IndexingMode = IndexingMode.Consistent;
                indexingPolicyOld.Automatic = true;
                indexingPolicyOld.ExcludedPaths.Add(@"/""A""/""B""/?");

                collectionDefinition.IndexingPolicy = IndexingPolicyTranslator.TranslateIndexingPolicyV1ToV2(indexingPolicyOld);

                CosmosContainerSettings collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;
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
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                CosmosContainerSettings collectionDefinition = new CosmosContainerSettings { Id = "TestQueryDocumentWithPathsCollection" };

                IndexingPolicyOld indexingPolicyOld = new IndexingPolicyOld();

                indexingPolicyOld.IncludedPaths.Add(new IndexingPath { IndexType = IndexType.Range, Path = @"/""NumericField""/?", NumericPrecision = -1 });
                indexingPolicyOld.IncludedPaths.Add(new IndexingPath { IndexType = IndexType.Hash, Path = @"/" });
                indexingPolicyOld.IndexingMode = IndexingMode.Consistent;
                indexingPolicyOld.Automatic = true;
                indexingPolicyOld.ExcludedPaths.Add(@"/""A""/""B""/?");

                collectionDefinition.IndexingPolicy = IndexingPolicyTranslator.TranslateIndexingPolicyV1ToV2(indexingPolicyOld);

                CosmosContainerSettings collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;
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
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                CosmosContainerSettings collectionDefinition = new CosmosContainerSettings { Id = "TestQueryDocumentWithPathsCollection" };

                CosmosContainerSettings collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;
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
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                CosmosContainerSettings collectionDefinition = new CosmosContainerSettings { Id = "TestQueryDocumentWithPathsCollection" + Guid.NewGuid().ToString() };

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

                CosmosContainerSettings collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;

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

                    INameValueCollection headers = new StringKeyValueCollection();
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

        /// <summary>
        /// Test for invalid precision
        /// </summary>
        [TestMethod]
        public void TestIndexingPolicyNegative2()
        {
            try
            {
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentWithPaths" + Guid.NewGuid().ToString() });
                CosmosContainerSettings collectionDefinition = new CosmosContainerSettings { Id = "TestQueryDocumentWithPathsCollection" + Guid.NewGuid().ToString() };
                collectionDefinition.IndexingPolicy.Automatic = true;
                collectionDefinition.IndexingPolicy.IndexingMode = IndexingMode.Consistent;

                IncludedPath includedPath = new IncludedPath();
                includedPath.Path = @"/Field/?";
                includedPath.Indexes.Add(new RangeIndex() { DataType = DataType.Number, Precision = 0 });
                includedPath.Indexes.Add(new RangeIndex() { DataType = DataType.String, Precision = -1 });
                collectionDefinition.IndexingPolicy.IncludedPaths.Add(includedPath);
                collectionDefinition.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath() { Path = @"/" });

                bool bException = false;
                try
                {
                    CosmosContainerSettings collection = this.client.Create<CosmosContainerSettings>(database.GetIdOrFullName(), collectionDefinition);
                }
                catch (DocumentClientException e)
                {
                    bException = true;
                    Assert.IsTrue(e.Message.Contains("The specified precision value 0 is not valid."));
                }
                Assert.IsTrue(bException);

                ((RangeIndex)collectionDefinition.IndexingPolicy.IncludedPaths[0].Indexes[0]).Precision = 9;
                bException = false;
                try
                {
                    CosmosContainerSettings collection = this.client.Create<CosmosContainerSettings>(database.GetIdOrFullName(), collectionDefinition);
                }
                catch (DocumentClientException e)
                {
                    bException = true;
                    Assert.IsTrue(e.Message.Contains("Please provide a value between 1 and 8, or -1 for maximum precision"));
                }
                Assert.IsTrue(bException);
                ((RangeIndex)collectionDefinition.IndexingPolicy.IncludedPaths[0].Indexes[0]).Precision = -1;

                ((RangeIndex)collectionDefinition.IndexingPolicy.IncludedPaths[0].Indexes[1]).Precision = 101;
                bException = false;
                try
                {
                    CosmosContainerSettings collection = this.client.Create<CosmosContainerSettings>(database.GetIdOrFullName(), collectionDefinition);
                }
                catch (DocumentClientException e)
                {
                    bException = true;
                    Assert.IsTrue(e.Message.Contains("Please provide a value between 1 and 100, or -1 for maximum precision"));
                }
                Assert.IsTrue(bException);
            }
            catch (Exception e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [TestMethod]
        public void TestQueryDocumentsSecondaryIndex()
        {
            try
            {
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentSecondaryIndexDatabase" + Guid.NewGuid().ToString() });
                CosmosContainerSettings collectionDefinition = new CosmosContainerSettings { Id = "TestQueryDocumentsSecondaryIndexCollection" + Guid.NewGuid().ToString() };
                collectionDefinition.IndexingPolicy.Automatic = true;
                collectionDefinition.IndexingPolicy.IndexingMode = IndexingMode.Consistent;

                CosmosContainerSettings collection = TestCommon.CreateCollectionAsync(this.client, database, collectionDefinition).Result;

                TestQueryDocuments(collection);
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
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentsDatabase" + Guid.NewGuid().ToString() });
                CosmosContainerSettings documentCollection = new CosmosContainerSettings { Id = "TestQueryDocumentsCollection" + Guid.NewGuid().ToString() };
                documentCollection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
                CosmosContainerSettings collection = TestCommon.CreateCollectionAsync(this.client, database, documentCollection).Result;

                TestQueryDocuments(collection);
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [TestMethod]
        public void TestQueryDocumentNoIndex()
        {
            try
            {
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentNoIndex" + Guid.NewGuid().ToString() });

                CosmosContainerSettings sourceCollection = new CosmosContainerSettings
                {
                    Id = "TestQueryDocumentNoIndex" + Guid.NewGuid().ToString(),
                };
                sourceCollection.IndexingPolicy.Automatic = false;
                sourceCollection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;

                CosmosContainerSettings collection = TestCommon.CreateCollectionAsync(this.client, database, sourceCollection).Result;

                dynamic doc = new Document()
                {
                    Id = string.Format(CultureInfo.InvariantCulture, "doc{0}", 111)
                };
                doc.NumericField = 111;
                doc.StringField = "111";

                this.client.Create<Document>(collection.GetIdOrFullName(), (Document)doc);
                Assert.AreEqual(0, this.client.CreateDocumentQuery(collection.SelfLink, @"select * from root r where r.StringField= ""111""").AsEnumerable().Count());
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
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentManualRemoveIndex" + Guid.NewGuid().ToString() });

                CosmosContainerSettings sourceCollection = new CosmosContainerSettings
                {
                    Id = "TestQueryDocumentManualRemoveIndex" + Guid.NewGuid().ToString(),
                };
                sourceCollection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
                CosmosContainerSettings collection = TestCommon.CreateCollectionAsync(this.client, database, sourceCollection).Result;

                dynamic doc = new Document()
                {
                    Id = string.Format(CultureInfo.InvariantCulture, "doc{0}", 222)
                };
                doc.NumericField = 222;
                doc.StringField = "222";

                INameValueCollection requestHeaders = new StringKeyValueCollection();
                requestHeaders.Add("x-ms-indexing-directive", "exclude");
                this.client.Create<Document>(collection.GetIdOrFullName(), (Document)doc, requestHeaders);

                IEnumerable<Document> queriedDocuments = this.client.CreateDocumentQuery<Document>(collection.GetLink(), @"select * from root r where r.StringField = ""222""");
                Assert.AreEqual(0, queriedDocuments.Count());
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
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentManualAddRemoveIndex" + Guid.NewGuid().ToString() });

                CosmosContainerSettings sourceCollection = new CosmosContainerSettings
                {
                    Id = "TestQueryDocumentManualAddRemoveIndex" + Guid.NewGuid().ToString(),
                };
                sourceCollection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
                CosmosContainerSettings collection = TestCommon.CreateCollectionAsync(this.client, database, sourceCollection).Result;

                QueryDocument doc = new QueryDocument()
                {
                    Id = string.Format(CultureInfo.InvariantCulture, "doc{0}", 333),
                    NumericField = 333,
                    StringField = "333",
                };

                INameValueCollection requestHeaders = new StringKeyValueCollection();
                requestHeaders.Add("x-ms-indexing-directive", "include");

                QueryDocument docCreated = this.client.Create<QueryDocument>(collection.GetIdOrFullName(), doc, requestHeaders);
                Assert.IsNotNull(this.client.CreateDocumentQuery(collection.SelfLink, @"select * from root r where r.StringField=""333""").AsEnumerable().Single());

                docCreated.NumericField = 3333;
                docCreated.StringField = "3333";

                requestHeaders.Remove("x-ms-indexing-directive");
                requestHeaders.Add("x-ms-indexing-directive", "exclude");

                QueryDocument docReplaced = this.client.Update<QueryDocument>(docCreated, requestHeaders);

                Assert.AreEqual(docReplaced.NumericField, 3333);

                // query for changed string value
                Assert.AreEqual(0, this.client.CreateDocumentQuery(collection.SelfLink, @"select * from root r where r.StringField=""3333""").AsEnumerable().Count());

                requestHeaders.Remove("x-ms-indexing-directive");
                requestHeaders.Add("x-ms-indexing-directive", "include");
                docReplaced = this.client.Update<QueryDocument>(docReplaced, requestHeaders);

                Assert.IsNotNull(this.client.CreateDocumentQuery(collection.SelfLink, @"select * from root r where r.StringField=""3333""").AsEnumerable().Single());

                this.client.Delete<QueryDocument>(docReplaced.ResourceId);

                Assert.AreEqual(0, this.client.CreateDocumentQuery(collection.SelfLink, @"select * from root r where r.id=""" + doc.Id + @"""").AsEnumerable().Count());
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
                CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryDocumentsDatabaseManualIndex" + Guid.NewGuid().ToString() });

                CosmosContainerSettings sourceCollection = new CosmosContainerSettings
                {
                    Id = "TestQueryDocumentsCollectionNoIndex" + Guid.NewGuid().ToString(),
                };
                sourceCollection.IndexingPolicy.Automatic = false;
                sourceCollection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;

                CosmosContainerSettings collection = TestCommon.CreateCollectionAsync(this.client, database, sourceCollection).Result;

                TestQueryDocuments(collection, true);
            }
            catch (DocumentClientException e)
            {
                Assert.Fail("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
            }
        }

        [TestMethod]
        public void TestSessionTokenControlThroughFeedOptions()
        {
            CosmosDatabaseSettings database = this.client.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestSessionTokenControlThroughFeedOptions" + Guid.NewGuid().ToString() });

            CosmosContainerSettings collection = new CosmosContainerSettings
            {
                Id = "SessionTokenControlThroughFeedOptionsCollection",
            };
            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            collection = TestCommon.CreateCollectionAsync(this.client, database, collection).Result;

            try
            {
                string sessionTokenBeforeReplication = null;
                dynamic myDocument = new Document();
                myDocument.Id = "doc0";
                myDocument.Title = "TestSessionTokenControlThroughFeedOptions";
                ResourceResponse<Document> response = client.CreateDocumentAsync(collection.GetLink(), myDocument).Result;
                sessionTokenBeforeReplication = response.SessionToken;

                Assert.IsNotNull(sessionTokenBeforeReplication);

                IQueryable<dynamic> documentIdQuery = client.CreateDocumentQuery(collection.GetLink(), @"select * from root r where r.Title=""TestSessionTokenControlThroughFeedOptions""",
                    new FeedOptions() { SessionToken = sessionTokenBeforeReplication });

                Assert.AreEqual(1, documentIdQuery.AsEnumerable().Count());

                string sessionTokenAfterReplication = null;
                int maxRetryCount = 5;
                for (int retryCounter = 1; retryCounter < maxRetryCount; retryCounter++)
                {
                    TestCommon.WaitForServerReplication();

                    myDocument = new Document();
                    myDocument.Id = "doc" + retryCounter;
                    myDocument.Title = "TestSessionTokenControlThroughFeedOptions";
                    response = client.CreateDocumentAsync(collection.SelfLink, myDocument).Result;

                    sessionTokenAfterReplication = response.SessionToken;
                    Assert.IsNotNull(sessionTokenAfterReplication);

                    if (!string.Equals(sessionTokenAfterReplication, sessionTokenBeforeReplication))
                    {
                        documentIdQuery = client.CreateDocumentQuery(collection.SelfLink, @"select * from root r where r.Title=""TestSessionTokenControlThroughFeedOptions""",
                            new FeedOptions() { SessionToken = sessionTokenAfterReplication });

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
                client.DeleteDocumentCollectionAsync(collection).Wait();
            }
        }

        [TestMethod]
        public void TestQueryUnicodeDocumentHttpsGateway()
        {
            TestQueryUnicodeDocument(useGateway: true, protocol: Protocol.Https);
        }

#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
        [TestMethod]
        public void TestQueryUnicodeDocumentHttpsDirect()
        {
            TestQueryUnicodeDocument(useGateway: false, protocol: Protocol.Https);
        }
#endif
        private void TestQueryUnicodeDocument(bool useGateway, Protocol protocol)
        {
            try
            {
                using (var testClient = TestCommon.CreateClient(useGateway, protocol: protocol, defaultConsistencyLevel: ConsistencyLevel.Session))
                {
                    CosmosDatabaseSettings database = testClient.Create<CosmosDatabaseSettings>(null, new CosmosDatabaseSettings { Id = "TestQueryUnicodeDocument" + Guid.NewGuid().ToString() });

                    CosmosContainerSettings sourceCollection = new CosmosContainerSettings
                    {
                        Id = "TestQueryUnicodeDocument" + Guid.NewGuid().ToString(),
                    };
                    sourceCollection.IndexingPolicy.Automatic = true;
                    sourceCollection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
                    CosmosContainerSettings collection = testClient.Create<CosmosContainerSettings>(database.GetIdOrFullName(), sourceCollection);

                    INameValueCollection requestHeaders = new StringKeyValueCollection();
                    requestHeaders.Add("x-ms-indexing-directive", "include");

                    Action<string, string, string> testDocumentSQL = (name, rawValue, escapedValue) =>
                    {
                        escapedValue = escapedValue ?? rawValue;

                        var document = new QueryDocument()
                        {
                            Id = name,
                            StringField = rawValue,
                        };

                        QueryDocument docCreated = testClient.Create<QueryDocument>(collection.GetIdOrFullName(), document, requestHeaders);

                        {
                            IEnumerable<JObject> result = testClient
                                .CreateDocumentQuery(collection,
                                string.Format(CultureInfo.InvariantCulture, "SELECT r.StringField FROM ROOT r WHERE r.StringField=\"{0}\"", rawValue),
                                null).AsDocumentQuery().ExecuteNextAsync<JObject>().Result;

                            Assert.AreEqual(document.StringField, result.Single()["StringField"].Value<string>());
                        }

                        {
                            IEnumerable<JObject> result = testClient
                                .CreateDocumentQuery(collection,
                                string.Format(CultureInfo.InvariantCulture, "SELECT r.StringField FROM ROOT r WHERE r.StringField=\"{0}\"", escapedValue),
                                null).AsDocumentQuery().ExecuteNextAsync<JObject>().Result;

                            Assert.AreEqual(document.StringField, result.Single()["StringField"].Value<string>());
                        }

                        {
                            IEnumerable<JObject> result = testClient
                                .CreateDocumentQuery(collection,
                                string.Format(CultureInfo.InvariantCulture, "SELECT * FROM ROOT r WHERE r.StringField=\"{0}\"", rawValue),
                                null).AsDocumentQuery().ExecuteNextAsync<JObject>().Result;

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

        [Ignore]
        [TestMethod]
        public async Task TestRouteToSpecificPartition()
        {
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestRoutToSpecificPartition(false);
#endif
            await this.TestRoutToSpecificPartition(true);
        }

        private async Task TestRoutToSpecificPartition(bool useGateway)
        {
            DocumentClient client = TestCommon.CreateClient(useGateway);

            await TestCommon.DeleteAllDatabasesAsync(client);
            string guid = Guid.NewGuid().ToString();

            CosmosDatabaseSettings database = await client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = "db" + guid });

            CosmosContainerSettings coll = await TestCommon.CreateCollectionAsync(client,
                database,
                new CosmosContainerSettings
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

            IRoutingMapProvider routingMapProvider = await client.GetPartitionKeyRangeCacheAsync();
            IReadOnlyList<PartitionKeyRange> ranges =
                await routingMapProvider.TryGetOverlappingRangesAsync(coll.ResourceId, fullRange);
            Assert.AreEqual(5, ranges.Count());

            Document document = new Document { Id = "id1" };
            document.SetPropertyValue("key", "hello");
            ResourceResponse<Document> doc = await client.CreateDocumentAsync(coll.SelfLink, document);
            string partitionKeyRangeId = doc.SessionToken.Split(':')[0];
            Assert.AreNotEqual(ranges.First().Id, partitionKeyRangeId);

            FeedResponse<dynamic> response = await client.ReadDocumentFeedAsync(coll.SelfLink, new FeedOptions { PartitionKeyRangeId = partitionKeyRangeId });
            Assert.AreEqual(1, response.Count);

            response = await client.ReadDocumentFeedAsync(coll.SelfLink, new FeedOptions { PartitionKeyRangeId = ranges.First(r => r.Id != partitionKeyRangeId).Id });
            Assert.AreEqual(0, response.Count);
        }

        [Ignore]
        [TestMethod]
        public async Task TestQueryForRoutingMapSanity()
        {
            string guid = Guid.NewGuid().ToString();
            await this.CreateDataSet(true, "db" + guid, "coll" + guid, 5000, 35000);
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestQueryForRoutingMapSanity("db" + guid, "coll" + guid, true, 5000, false);
            await this.TestQueryForRoutingMapSanity("db" + guid, "coll" + guid, false, 5000, true);
#endif
#if !DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestQueryForRoutingMapSanity("db" + guid, "coll" + guid, true, 5000, true);
#endif
        }

        private async Task TestQueryForRoutingMapSanity(string inputDatabaseId, string inputCollectionId, bool useGateway, int numDocuments, bool isDeleteDB)
        {
            System.Diagnostics.Trace.TraceInformation(
                "Start TestQueryForRoutingMapSanity in {0} mode",
                useGateway ? ConnectionMode.Gateway.ToString() : ConnectionMode.Direct.ToString());

            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            DocumentClient client = TestCommon.CreateClient(useGateway);
            CosmosDatabaseSettings database = await client.ReadDatabaseAsync(string.Format(CultureInfo.InvariantCulture, "dbs/{0}", inputDatabaseId));
            CosmosContainerSettings coll = await client.ReadDocumentCollectionAsync(string.Format(CultureInfo.InvariantCulture, "dbs/{0}/colls/{1}", inputDatabaseId, inputCollectionId));

            Range<string> fullRange = new Range<string>(
                        PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                        true,
                        false);

            IRoutingMapProvider routingMapProvider = await client.GetPartitionKeyRangeCacheAsync();
            IReadOnlyList<PartitionKeyRange> ranges =
                await routingMapProvider.TryGetOverlappingRangesAsync(coll.ResourceId, fullRange);
            Assert.AreEqual(5, ranges.Count);

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

            await TestCommon.DeleteAllDatabasesAsync(client);
            Random random = new Random();

            CosmosDatabaseSettings database = await client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = dbName });

            CosmosContainerSettings coll = await TestCommon.CreateCollectionAsync(client,
                database,
                new CosmosContainerSettings
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
                    sb.Append("{\"id\":\"documentId" + (100 * i + j));
                    sb.Append("\",\"partitionKey\":" + (100 * i + j));
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
        public async Task TestRUsCalculationForParallelQuery()
        {
            string guid = Guid.NewGuid().ToString();
            await this.CreateDataSet(true, "db" + guid, "coll" + guid, 1000, 35000);
            await this.TestRUsCalculationForParallelQuery("db" + guid, "coll" + guid, true, Protocol.Https, false);
            await this.TestRUsCalculationForParallelQuery("db" + guid, "coll" + guid, false, Protocol.Tcp, true);
        }

        internal async Task TestRUsCalculationForParallelQuery(string inputDatabaseId, string inputCollectionId, bool useGateway, Protocol protocol, bool isDeleteDB)
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            double errorMargin = 0.10;
            System.Diagnostics.Trace.TraceInformation(
                "Start TestRUsCalculationForParallelQuery in {0} mode with seed{1}",
                useGateway ? ConnectionMode.Gateway : ConnectionMode.Direct,
                seed);

            DocumentClient client = TestCommon.CreateClient(useGateway);
            String queryText = "select root.partitionKey from root";
            CosmosDatabaseSettings database = await client.ReadDatabaseAsync(string.Format(CultureInfo.InvariantCulture, "dbs/{0}", inputDatabaseId));
            CosmosContainerSettings coll = await client.ReadDocumentCollectionAsync(string.Format(CultureInfo.InvariantCulture, "dbs/{0}/colls/{1}", inputDatabaseId, inputCollectionId));

            int countSerial = 0;
            double totalRUsSerial = 0;
            FeedOptions feedOptionsSerial = new FeedOptions { EnableCrossPartitionQuery = true, MaxBufferedItemCount = 7000, MaxDegreeOfParallelism = 0 };
            var query = client.CreateDocumentQuery(coll.AltLink, queryText, feedOptionsSerial).AsDocumentQuery();

            while (query.HasMoreResults)
            {
                FeedResponse<dynamic> page = await query.ExecuteNextAsync().ConfigureAwait(false);
                totalRUsSerial += page.RequestCharge;
                countSerial += page.Count;
            }

            int countParallelOne = 0;
            double totalRUsParallelOne = 0;
            FeedOptions feedOptionsParallelOne = new FeedOptions { EnableCrossPartitionQuery = true, MaxBufferedItemCount = 7000, MaxDegreeOfParallelism = 1 };
            query = client.CreateDocumentQuery(coll.AltLink, queryText, feedOptionsParallelOne).AsDocumentQuery();

            while (query.HasMoreResults)
            {
                FeedResponse<dynamic> page = await query.ExecuteNextAsync().ConfigureAwait(false);
                totalRUsParallelOne += page.RequestCharge;
                countParallelOne += page.Count;
            }

            int countParallelMany = 0;
            double totalRUsParallelMany = 0;
            FeedOptions feedOptionsparallelMany = new FeedOptions { EnableCrossPartitionQuery = true, MaxBufferedItemCount = 7000, MaxDegreeOfParallelism = 100 };
            query = client.CreateDocumentQuery(coll.AltLink, queryText, feedOptionsparallelMany).AsDocumentQuery();

            while (query.HasMoreResults)
            {
                FeedResponse<dynamic> page = await query.ExecuteNextAsync().ConfigureAwait(false);
                totalRUsParallelMany += page.RequestCharge;
                countParallelMany += page.Count;
            }

            double delta = totalRUsSerial * errorMargin;

            Assert.IsTrue(
                Math.Abs(totalRUsSerial - totalRUsParallelOne) < delta,
                this.getParallelRUCalculationDebugInfo(queryText, totalRUsSerial, totalRUsParallelOne, feedOptionsSerial.MaxDegreeOfParallelism, feedOptionsParallelOne.MaxDegreeOfParallelism));

            Assert.IsTrue(
                Math.Abs(totalRUsParallelOne - totalRUsParallelMany) < delta,
                this.getParallelRUCalculationDebugInfo(queryText, totalRUsParallelOne, totalRUsParallelMany, feedOptionsParallelOne.MaxDegreeOfParallelism, feedOptionsparallelMany.MaxDegreeOfParallelism));

            Assert.IsTrue(
                Math.Abs(totalRUsSerial - totalRUsParallelMany) < delta,
                this.getParallelRUCalculationDebugInfo(queryText, totalRUsSerial, totalRUsParallelMany, feedOptionsSerial.MaxDegreeOfParallelism, feedOptionsparallelMany.MaxDegreeOfParallelism));

            if (isDeleteDB)
            {
                client.DeleteDatabaseAsync(database).Wait();
            }
        }

        private string getParallelRUCalculationDebugInfo(string queryText, double ru1, double ru2, int dop1, int dop2)
        {
            return string.Format(
               CultureInfo.InvariantCulture,
               "Unequals RUs {0} (MaxDOP {1}) and {2} ( MaxDOP {3}). QueryText = {4}",
               ru1,
               dop1,
               ru2,
               dop2,
               queryText);
        }

        [Ignore]
        [TestMethod]
        public async Task TestQueryParallelExecution()
        {
            string guid = Guid.NewGuid().ToString();
            await this.CreateDataSet(true, "db" + guid, "coll" + guid, 5000, 35000);
            await this.TestQueryParallelExecution("db" + guid, "coll" + guid, true, Protocol.Https, false);
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestQueryParallelExecution("db" + guid, "coll" + guid, false, Protocol.Tcp, false);
#endif
            await this.TestReadFeedParallelQuery("db" + guid, "coll" + guid, true, Protocol.Https, false);
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await this.TestReadFeedParallelQuery("db" + guid, "coll" + guid, false, Protocol.Tcp, true);
#endif
        }

        private async Task TestQueryParallelExecution(string inputDatabaseId, string inputCollectionId, bool useGateway, Protocol protocol, bool isDeleteDB)
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            System.Diagnostics.Trace.TraceInformation(
                "Start TestQueryParallelExecution in {0} mode with seed{1}",
                useGateway ? ConnectionMode.Gateway.ToString() : ConnectionMode.Direct.ToString(),
                seed);

            DocumentClient client = TestCommon.CreateClient(useGateway, protocol);
            CosmosDatabaseSettings database = await client.ReadDatabaseAsync(string.Format(CultureInfo.InvariantCulture, "dbs/{0}", inputDatabaseId));
            CosmosContainerSettings coll = await client.ReadDocumentCollectionAsync(string.Format(CultureInfo.InvariantCulture, "dbs/{0}/colls/{1}", inputDatabaseId, inputCollectionId));

            Range<string> fullRange = new Range<string>(
                        PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                        true,
                        false);

            IRoutingMapProvider routingMapProvider = await client.GetPartitionKeyRangeCacheAsync();
            IReadOnlyList<PartitionKeyRange> ranges =
                await routingMapProvider.TryGetOverlappingRangesAsync(coll.ResourceId, fullRange);
            Assert.AreEqual(5, ranges.Count);

            // Query Number 1
            List<string> expected = new List<string> { "documentId123", "documentId124", "documentId125" };
            List<string> result = new List<string>();

            string queryText = @"SELECT * FROM Root r WHERE r.partitionKey = 123 OR r.partitionKey = 124 OR r.partitionKey = 125";
            var feedOptions = new FeedOptions
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
            var enumerableIds = rangeDocumentQuery.ToList().Select(doc => doc.Id).ToArray();
            Assert.AreEqual(
                (endRange - startRange + 1),
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
            var enumerableIdsOneTask = rangeDocumentQuery.ToList().Select(doc => doc.Id).ToArray();
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
            var enumerableIdsTwoTasks = rangeDocumentQuery.ToList().Select(doc => doc.Id).ToArray();
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
            var rangeQuery = client.CreateDocumentQuery(coll.AltLink, queryText, feedOptions).AsDocumentQuery();
            var ids1 = new List<dynamic>();

            while (rangeQuery.HasMoreResults)
            {
                var page = await rangeQuery.ExecuteNextAsync().ConfigureAwait(false);
                if (page != null)
                {
                    ids1.AddRange(page.AsEnumerable());
                }
            }

            var enumerableIdsAutoTasks = ids1.Select(doc => ((Document)doc).Id).ToArray();
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
            var enumerableIdsLink = valueDocumentQuery.ToList();
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
            CosmosDatabaseSettings database = await client.ReadDatabaseAsync(string.Format("dbs/{0}", inputDatabaseId));
            CosmosContainerSettings coll = await client.ReadDocumentCollectionAsync(string.Format("dbs/{0}/colls/{1}", inputDatabaseId, inputCollectionId));

            Range<string> fullRange = new Range<string>(
                        PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                        true,
                        false);

            IRoutingMapProvider routingMapProvider = await client.GetPartitionKeyRangeCacheAsync();
            IReadOnlyList<PartitionKeyRange> ranges =
                await routingMapProvider.TryGetOverlappingRangesAsync(coll.ResourceId, fullRange);
            Assert.AreEqual(5, ranges.Count);

            var feedOptions = new FeedOptions
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
            var enumerableIdsSelectStar = selectStarDocumentQuery.ToList().Select(doc => doc.Id).ToArray();
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
            var enumerableIds = feedReader.Select(doc => doc.Id).ToArray();
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
            FeedResponse<dynamic> response = null;
            startTime = DateTime.Now;
            List<dynamic> result = new List<dynamic>();
            do
            {
                response = await client.ReadDocumentFeedAsync(coll, feedOptions);
                result.AddRange(response);
                feedOptions.RequestContinuation = response.ResponseContinuation;
            } while (!string.IsNullOrEmpty(feedOptions.RequestContinuation));
            double totalMillParallelReedFeed2 = (DateTime.Now - startTime).TotalMilliseconds;

            var enumerableIds2 = result.Select(doc => ((Document)doc).Id).ToArray();
            Assert.AreEqual(
                string.Join(",", enumerableIds2),
                string.Join(",", enumerableIdsSelectStar),
                this.getQueryExecutionDebugInfo(queryText, seed, feedOptions));

            if (isDeleteDB)
            {
                client.DeleteDatabaseAsync(database).Wait();
            }
        }

        private void AssertQueryMetricsPublicMembers(QueryMetrics queryMetrics)
        {
            Assert.IsNotNull(queryMetrics.TotalQueryExecutionTime.TotalMilliseconds);
            Assert.IsNotNull(queryMetrics.RetrievedDocumentCount);
            Assert.IsNotNull(queryMetrics.RetrievedDocumentSize);
            Assert.IsNotNull(queryMetrics.OutputDocumentCount);
            Assert.IsNotNull(queryMetrics.IndexHitRatio);
            Assert.IsNotNull(queryMetrics.ClientSideMetrics.Retries);

            Assert.IsNotNull(queryMetrics.QueryPreparationTimes.QueryCompilationTime.TotalMilliseconds);
            Assert.IsNotNull(queryMetrics.QueryPreparationTimes.LogicalPlanBuildTime.TotalMilliseconds);
            Assert.IsNotNull(queryMetrics.QueryPreparationTimes.PhysicalPlanBuildTime.TotalMilliseconds);
            Assert.IsNotNull(queryMetrics.QueryPreparationTimes.QueryOptimizationTime.TotalMilliseconds);

            Assert.IsNotNull(queryMetrics.IndexLookupTime.TotalMilliseconds);
            Assert.IsNotNull(queryMetrics.DocumentLoadTime.TotalMilliseconds);
            Assert.IsNotNull(queryMetrics.DocumentWriteTime.TotalMilliseconds);

            Assert.IsNotNull(queryMetrics.RuntimeExecutionTimes.QueryEngineExecutionTime.TotalMilliseconds);
            Assert.IsNotNull(queryMetrics.RuntimeExecutionTimes.SystemFunctionExecutionTime.TotalMilliseconds);
            Assert.IsNotNull(queryMetrics.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime.TotalMilliseconds);
        }

        /// <summary>
        /// Ensures that there are no breaking changes to the public api
        /// </summary>
        [TestMethod]
        public void TestQueryMetricsAPI()
        {
            // Checking all public members
            QueryMetrics queryMetrics = QueryMetrics.CreateFromDelimitedString("totalExecutionTimeInMs=33.67;queryCompileTimeInMs=0.06;queryLogicalPlanBuildTimeInMs=0.02;queryPhysicalPlanBuildTimeInMs=0.10;queryOptimizationTimeInMs=0.00;VMExecutionTimeInMs=32.56;indexLookupTimeInMs=0.36;documentLoadTimeInMs=9.58;systemFunctionExecuteTimeInMs=0.00;userFunctionExecuteTimeInMs=0.00;retrievedDocumentCount=2000;retrievedDocumentSize=1125600;outputDocumentCount=2000;outputDocumentSize=1125600;writeOutputTimeInMs=18.10;indexUtilizationRatio=1.00");
            AssertQueryMetricsPublicMembers(queryMetrics);

            // Checking to see if you can serialize and deserialize using ToString and the constructor.
            QueryMetrics queryMetrics2 = QueryMetrics.CreateFromDelimitedString(queryMetrics.ToDelimitedString());
            AssertQueryMetricsPublicMembers(queryMetrics2);

            AssertQueryMetricsEquality(queryMetrics, queryMetrics2);
        }

        /// <summary>
        /// Ensures that QueryMetrics Serialization function is accesible.
        /// </summary>
        [TestMethod]
        public void TestQueryMetricsToStrings()
        {
            QueryMetrics queryMetrics = QueryMetrics.CreateFromDelimitedString("totalExecutionTimeInMs=33.67;queryCompileTimeInMs=0.06;queryLogicalPlanBuildTimeInMs=0.02;queryPhysicalPlanBuildTimeInMs=0.10;queryOptimizationTimeInMs=0.00;VMExecutionTimeInMs=32.56;indexLookupTimeInMs=0.36;documentLoadTimeInMs=9.58;systemFunctionExecuteTimeInMs=0.00;userFunctionExecuteTimeInMs=0.00;retrievedDocumentCount=2000;retrievedDocumentSize=1125600;outputDocumentCount=2000;outputDocumentSize=1125600;writeOutputTimeInMs=18.10;indexUtilizationRatio=1.00");

            string queryMetricsToTextString = queryMetrics.ToTextString();
            Assert.IsFalse(string.IsNullOrWhiteSpace(queryMetricsToTextString));

            string queryMetricsToJsonString = queryMetrics.ToJsonString();
            Assert.IsFalse(string.IsNullOrWhiteSpace(queryMetricsToJsonString));

            string queryMetricsToDelimitedString = queryMetrics.ToDelimitedString();
            Assert.IsFalse(string.IsNullOrWhiteSpace(queryMetricsToDelimitedString));
        }

        private void AssertQueryMetricsEquality(QueryMetrics m1, QueryMetrics m2)
        {
            Assert.AreEqual(m1.IndexHitRatio, m2.IndexHitRatio);
            Assert.AreEqual(m1.OutputDocumentCount, m2.OutputDocumentCount);
            Assert.AreEqual(m1.OutputDocumentSize, m2.OutputDocumentSize);
            Assert.AreEqual(m1.RetrievedDocumentCount, m2.RetrievedDocumentCount);
            Assert.AreEqual(m1.RetrievedDocumentSize, m2.RetrievedDocumentSize);
            Assert.AreEqual(m1.TotalQueryExecutionTime, m2.TotalQueryExecutionTime);

            Assert.AreEqual(m1.DocumentLoadTime, m2.DocumentLoadTime);
            Assert.AreEqual(m1.DocumentWriteTime, m2.DocumentWriteTime);
            Assert.AreEqual(m1.IndexLookupTime, m2.IndexLookupTime);
            Assert.AreEqual(m1.VMExecutionTime, m2.VMExecutionTime);

            Assert.AreEqual(m1.QueryPreparationTimes.LogicalPlanBuildTime, m2.QueryPreparationTimes.LogicalPlanBuildTime);
            Assert.AreEqual(m1.QueryPreparationTimes.PhysicalPlanBuildTime, m2.QueryPreparationTimes.PhysicalPlanBuildTime);
            Assert.AreEqual(m1.QueryPreparationTimes.QueryCompilationTime, m2.QueryPreparationTimes.QueryCompilationTime);
            Assert.AreEqual(m1.QueryPreparationTimes.QueryOptimizationTime, m2.QueryPreparationTimes.QueryOptimizationTime);

            Assert.AreEqual(m1.RuntimeExecutionTimes.QueryEngineExecutionTime, m2.RuntimeExecutionTimes.QueryEngineExecutionTime);
            Assert.AreEqual(m1.RuntimeExecutionTimes.SystemFunctionExecutionTime, m2.RuntimeExecutionTimes.SystemFunctionExecutionTime);
            Assert.AreEqual(m1.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime, m2.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime);

            Assert.AreEqual(m1.ClientSideMetrics.FetchExecutionRanges.Count(), m2.ClientSideMetrics.FetchExecutionRanges.Count());
            Assert.AreEqual(m1.ClientSideMetrics.RequestCharge, m2.ClientSideMetrics.RequestCharge);
            Assert.AreEqual(m1.ClientSideMetrics.Retries, m2.ClientSideMetrics.Retries);
        }

        /// <summary>
        /// Ensures that QueryMetrics Serialization function is accesible.
        /// </summary>
        [TestMethod]
        public void TestQueryMetricsCreateAPI()
        {
            TimeSpan totalExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 33.67));
            TimeSpan queryCompileTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.06));
            TimeSpan logicalPlanBuildTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.02));
            TimeSpan queryPhysicalPlanBuildTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.10));
            TimeSpan queryOptimizationTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.01));
            TimeSpan vmExecutionTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 32.56));
            TimeSpan indexLookupTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.36));
            TimeSpan documentLoadTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 9.58));
            TimeSpan systemFunctionExecuteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.05));
            TimeSpan userFunctionExecuteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 0.07));
            TimeSpan documentWriteTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 18.10));
            long retrievedDocumentCount = 2000;
            long retrievedDocumentSize = 1125600;
            long outputDocumentCount = 2000;
            long outputDocumentSize = 1125600;
            double indexUtilizationRatio = 1.00;

            double requestCharge = 42;
            long retries = 5;
            List<FetchExecutionRange> fetchExecutionRanges = new List<FetchExecutionRange>
            {
                new FetchExecutionRange(new DateTime(), new DateTime(), null, 5, 5)
            };

            Guid guid = Guid.NewGuid();

            QueryMetrics queryMetrics = QueryMetrics.CreateFromDelimitedStringAndClientSideMetrics("totalExecutionTimeInMs=33.67;queryCompileTimeInMs=0.06;queryLogicalPlanBuildTimeInMs=0.02;queryPhysicalPlanBuildTimeInMs=0.10;queryOptimizationTimeInMs=0.01;VMExecutionTimeInMs=32.56;indexLookupTimeInMs=0.36;documentLoadTimeInMs=9.58;systemFunctionExecuteTimeInMs=0.05;userFunctionExecuteTimeInMs=0.07;retrievedDocumentCount=2000;retrievedDocumentSize=1125600;outputDocumentCount=2000;outputDocumentSize=1125600;writeOutputTimeInMs=18.10;indexUtilizationRatio=1.00",
                new ClientSideMetrics(retries, requestCharge, fetchExecutionRanges, new List<Tuple<string, SchedulingTimeSpan>>()), guid);

            QueryMetrics queryMetrics2 = QueryMetrics.CreateFromDelimitedStringAndClientSideMetrics(queryMetrics.ToDelimitedString(), queryMetrics.ClientSideMetrics, guid);
            this.AssertQueryMetricsEquality(queryMetrics, queryMetrics2);

            QueryMetrics queryMetricsFromIEnumberable = QueryMetrics.CreateFromIEnumerable(new List<QueryMetrics> { queryMetrics, queryMetrics });
            QueryMetrics queryMetricsFromAddition = queryMetrics + queryMetrics2;

            this.AssertQueryMetricsEquality(queryMetricsFromIEnumberable, queryMetricsFromAddition);

            Assert.AreEqual(queryMetricsFromAddition.IndexHitRatio, indexUtilizationRatio);
            Assert.AreEqual(queryMetricsFromAddition.OutputDocumentCount, outputDocumentCount * 2);
            Assert.AreEqual(queryMetricsFromAddition.OutputDocumentSize, outputDocumentSize * 2);
            Assert.AreEqual(queryMetricsFromAddition.RetrievedDocumentCount, retrievedDocumentCount * 2);
            Assert.AreEqual(queryMetricsFromAddition.RetrievedDocumentSize, retrievedDocumentSize * 2);
            Assert.AreEqual(queryMetricsFromAddition.TotalQueryExecutionTime, totalExecutionTime + totalExecutionTime);

            Assert.AreEqual(queryMetricsFromAddition.DocumentLoadTime, documentLoadTime + documentLoadTime);
            Assert.AreEqual(queryMetricsFromAddition.DocumentWriteTime, documentWriteTime + documentWriteTime);
            Assert.AreEqual(queryMetricsFromAddition.IndexLookupTime, indexLookupTime + indexLookupTime);
            Assert.AreEqual(queryMetricsFromAddition.VMExecutionTime, vmExecutionTime + vmExecutionTime);

            Assert.AreEqual(queryMetricsFromAddition.QueryPreparationTimes.LogicalPlanBuildTime, logicalPlanBuildTime + logicalPlanBuildTime);
            Assert.AreEqual(queryMetricsFromAddition.QueryPreparationTimes.PhysicalPlanBuildTime, queryPhysicalPlanBuildTime + queryPhysicalPlanBuildTime);
            Assert.AreEqual(queryMetricsFromAddition.QueryPreparationTimes.QueryCompilationTime, queryCompileTime + queryCompileTime);
            Assert.AreEqual(queryMetricsFromAddition.QueryPreparationTimes.QueryOptimizationTime, queryOptimizationTime + queryOptimizationTime);

            //Assert.AreEqual(queryMetricsFromAddition.RuntimeExecutionTimes.QueryEngineExecutionTime, RuntimeExecutionTimes.QueryEngineExecutionTime);
            Assert.AreEqual(queryMetricsFromAddition.RuntimeExecutionTimes.SystemFunctionExecutionTime, systemFunctionExecuteTime + systemFunctionExecuteTime);
            Assert.AreEqual(queryMetricsFromAddition.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime, userFunctionExecuteTime + userFunctionExecuteTime);

            Assert.AreEqual(queryMetricsFromAddition.ClientSideMetrics.FetchExecutionRanges.Count(), 2 * fetchExecutionRanges.Count());
            Assert.AreEqual(queryMetricsFromAddition.ClientSideMetrics.RequestCharge, requestCharge * 2);
            Assert.AreEqual(queryMetricsFromAddition.ClientSideMetrics.Retries, retries * 2);
            Assert.AreEqual(queryMetricsFromAddition.ActivityIds[0], guid);
            Assert.AreEqual(queryMetricsFromAddition.ActivityIds[1], guid);
        }

        [TestMethod]
        public void TestQueryMetricsHeaders()
        {

            CosmosDatabaseSettings database = TestCommon.RetryRateLimiting<CosmosDatabaseSettings>(() =>
            {
                return this.client.CreateDatabaseAsync(new CosmosDatabaseSettings() { Id = Guid.NewGuid().ToString() }).Result.Resource;
            });

            TestQueryMetricsHeaders(database, false);
            TestQueryMetricsHeaders(database, true);

            TestCommon.RetryRateLimiting<ResourceResponse<CosmosDatabaseSettings>>(() =>
            {
                return this.client.DeleteDatabaseAsync(database).Result;
            });
        }

        /// <summary>
        /// Tests to see if the RequestCharge from the query metrics from each partition sums up to the total request charge of the feedresponse for each continuation of the query.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [TestCategory("Quarantine") /* Used to filter out quarantined tests in gated runs */]
        public async Task TestQueryMetricsRUPerPartition()
        {
            DocumentClient client = TestCommon.CreateClient(true);

            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 1000;
            string partitionKey = "field_0";

            QueryOracleUtil util = new QueryOracle2(seed);

            await TestCommon.DeleteAllDatabasesAsync(client);
            string guid = Guid.NewGuid().ToString();
            CosmosDatabaseSettings database = await client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = "db" + guid });

            CosmosContainerSettings coll = await TestCommon.CreateCollectionAsync(client,
                database,
                new CosmosContainerSettings
                {
                    Id = "coll" + guid,
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Paths = new Collection<string> { "/" + partitionKey },
                    }
                },
                new RequestOptions { OfferThroughput = 35000 });

            IEnumerable<string> serializedDocuments = util.GetDocuments(numberOfDocuments);
            IList<Document> documents = new List<Document>(serializedDocuments.Count());
            foreach (string document in serializedDocuments)
            {
                ResourceResponse<Document> response = await client.CreateDocumentAsync(coll.SelfLink, JsonConvert.DeserializeObject(document));
                documents.Add(response.Resource);
            }

            FeedOptions feedOptions = new FeedOptions
            {
                EnableCrossPartitionQuery = true,
                PopulateQueryMetrics = true,
                MaxItemCount = 1,
                MaxDegreeOfParallelism = 0,
            };

            IDocumentQuery<dynamic> documentQuery = client.CreateDocumentQuery(coll, "SELECT TOP 5 * FROM c ORDER BY c._ts", feedOptions).AsDocumentQuery();

            List<FeedResponse<dynamic>> feedResponses = new List<FeedResponse<dynamic>>();
            while (documentQuery.HasMoreResults)
            {
                FeedResponse<dynamic> feedResonse = await documentQuery.ExecuteNextAsync();
                feedResponses.Add(feedResonse);
            }

            List<QueryMetrics> queryMetricsList = new List<QueryMetrics>();
            double aggregatedRequestCharge = 0;
            bool firstFeedResponse = true;
            foreach (FeedResponse<dynamic> feedResponse in feedResponses)
            {
                aggregatedRequestCharge += feedResponse.RequestCharge;
                foreach (KeyValuePair<string, QueryMetrics> kvp in feedResponse.QueryMetrics)
                {
                    string partitionKeyRangeId = kvp.Key;
                    QueryMetrics queryMetrics = kvp.Value;
                    if (firstFeedResponse)
                    {
                        // For an orderby query the first execution should fan out to every partition
                        Assert.IsTrue(queryMetrics.ClientSideMetrics.RequestCharge > 0, "queryMetrics.RequestCharge was not > 0 for PKRangeId: {0}", partitionKeyRangeId);
                    }

                    queryMetricsList.Add(queryMetrics);
                }
                firstFeedResponse = false;
            }

            QueryMetrics aggregatedQueryMetrics = QueryMetrics.CreateFromIEnumerable(queryMetricsList);
            double requestChargeFromMetrics = aggregatedQueryMetrics.ClientSideMetrics.RequestCharge;

            Assert.IsTrue(aggregatedRequestCharge > 0, "aggregatedRequestCharge was not > 0");
            Assert.IsTrue(requestChargeFromMetrics > 0, "requestChargeFromMetrics was not > 0");
            Assert.AreEqual(aggregatedRequestCharge, requestChargeFromMetrics, 0.1 * aggregatedRequestCharge, "Request Charge from FeedResponse and QueryMetrics do not equal.");

            await client.DeleteDatabaseAsync(database);
        }

        [TestMethod]
        [Ignore /* Failing with Assert.AreEqual failed. Expected:<00:00:00>. Actual:<00:00:00.0000900> */]
        public void TestForceQueryScanHeaders()
        {
            CosmosDatabaseSettings database = TestCommon.RetryRateLimiting<CosmosDatabaseSettings>(() =>
            {
                return this.client.CreateDatabaseAsync(new CosmosDatabaseSettings() { Id = Guid.NewGuid().ToString() }).Result.Resource;
            });

            TestForceQueryScanHeaders(database, false);
            TestForceQueryScanHeaders(database, true);

            TestCommon.RetryRateLimiting<ResourceResponse<CosmosDatabaseSettings>>(() =>
            {
                return this.client.DeleteDatabaseAsync(database).Result;
            });
        }

        private void TestForceQueryScanHeaders(CosmosDatabaseSettings database, bool partitionedCollection)
        {
            CosmosContainerSettings collection;
            RequestOptions options = new RequestOptions();
            if (!partitionedCollection)
            {
                collection = new CosmosContainerSettings()
                {
                    Id = Guid.NewGuid().ToString()
                };

                options.OfferThroughput = 10000;
            }
            else
            {
                collection = new CosmosContainerSettings()
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

            collection = TestCommon.RetryRateLimiting<CosmosContainerSettings>(() =>
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
            FeedResponse<dynamic> result;
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
            Assert.AreEqual(TimeSpan.Zero, queryMetrics.IndexLookupTime);

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
            Assert.AreNotEqual(TimeSpan.Zero, queryMetrics.IndexLookupTime);
        }

        [TestMethod]
        public void TestContinuationLimitHeaders()
        {

            CosmosDatabaseSettings database = TestCommon.RetryRateLimiting<CosmosDatabaseSettings>(() =>
            {
                return this.client.CreateDatabaseAsync(new CosmosDatabaseSettings() { Id = Guid.NewGuid().ToString() }).Result.Resource;
            });

            TestContinuationLimitHeaders(database, false);
            TestContinuationLimitHeaders(database, true);

            TestCommon.RetryRateLimiting<ResourceResponse<CosmosDatabaseSettings>>(() =>
            {
                return this.client.DeleteDatabaseAsync(database).Result;
            });
        }

        private void TestContinuationLimitHeaders(CosmosDatabaseSettings database, bool partitionedCollection)
        {
            CosmosContainerSettings collection;
            RequestOptions options = new RequestOptions();
            if (!partitionedCollection)
            {
                collection = new CosmosContainerSettings()
                {
                    Id = Guid.NewGuid().ToString()
                };

                options.OfferThroughput = 10000;
            }
            else
            {
                collection = new CosmosContainerSettings()
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

            collection = TestCommon.RetryRateLimiting<CosmosContainerSettings>(() =>
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
            FeedResponse<dynamic> result = null;

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
                DocumentClientException exception = e.InnerException as DocumentClientException;

                if (exception == null)
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
                DocumentClientException exception = e.InnerException as DocumentClientException;

                if (exception == null)
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
            Assert.IsTrue(!continuation.Contains("#FPC") && !continuation.Contains("#FPP"));

            feedOptions.ResponseContinuationTokenLimitInKb = 2;
            result = this.client.CreateDocumentQuery<Document>(collection, "SELECT r.id FROM root r WHERE r._ts > 0", feedOptions).AsDocumentQuery().ExecuteNextAsync().Result;
            continuation = result.ResponseContinuation;
            Assert.IsTrue(continuation.Contains("#FPC") || continuation.Contains("#FPP"));
        }

        private void TestQueryMetricsHeaders(CosmosDatabaseSettings database, bool partitionedCollection)
        {
            CosmosContainerSettings collection;
            RequestOptions options = new RequestOptions();
            if (!partitionedCollection)
            {
                collection = new CosmosContainerSettings()
                {
                    Id = Guid.NewGuid().ToString()
                };

                options.OfferThroughput = 10000;
            }
            else
            {
                collection = new CosmosContainerSettings()
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

            collection = TestCommon.RetryRateLimiting<CosmosContainerSettings>(() =>
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
            FeedResponse<dynamic> result = this.client.CreateDocumentQuery<Document>(collection, "SELECT r.id FROM root r", new FeedOptions() { EnableCrossPartitionQuery = true }).AsDocumentQuery().ExecuteNextAsync().Result;
            Assert.IsNull(result.ResponseHeaders[WFConstants.BackendHeaders.QueryMetrics], "Expected no metrics headers for query");

            result = this.client.CreateDocumentQuery<Document>(collection, "SELECT r.id FROM root r", new FeedOptions() { PopulateQueryMetrics = true, EnableCrossPartitionQuery = true }).AsDocumentQuery().ExecuteNextAsync().Result;
            Assert.IsNotNull(result.ResponseHeaders[WFConstants.BackendHeaders.QueryMetrics], "Expected metrics headers for query");

            ValidateQueryMetricsHeadersOverContinuations(collection, maxDocumentCount).Wait();
        }

        private async Task ValidateQueryMetricsHeadersOverContinuations(
            CosmosContainerSettings coll,
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

            foreach (var query in queries)
            {
                foreach (int pageSize in pageSizes)
                {
                    List<Document> retrievedDocuments = new List<Document>();

                    string continuationToken = default(string);
                    bool hasMoreResults;

                    do
                    {
                        var feedOptions = new FeedOptions
                        {
                            EnableCrossPartitionQuery = true,
                            MaxDegreeOfParallelism = -1,
                            RequestContinuation = continuationToken,
                            MaxItemCount = pageSize,
                            PopulateQueryMetrics = true
                        };

                        using (IDocumentQuery<Document> documentQuery = client.CreateDocumentQuery<Document>(
                            coll,
                            query,
                                feedOptions).AsDocumentQuery())
                        {
                            FeedResponse<Document> response = await documentQuery.ExecuteNextAsync<Document>();
                            string responseQueryMetrics = response.ResponseHeaders[WFConstants.BackendHeaders.QueryMetrics];

                            ValidateQueryMetrics(QueryMetrics.CreateFromDelimitedString(responseQueryMetrics));

                            foreach (KeyValuePair<string, QueryMetrics> pair in response.QueryMetrics)
                            {
                                System.Diagnostics.Trace.TraceInformation(JsonConvert.SerializeObject(pair));
                                ValidateQueryMetrics(pair.Value);
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
            Assert.IsTrue(metrics.QueryPreparationTimes.QueryCompilationTime.TotalMilliseconds > 0, "Expected CompileTimeInMs to be > 0, metrics = {0}", metrics);
            //We are not checking DocumentLoadTime and RetrievedDocumentCount, since some queries don't return any documents (especially in the last continuation).
            //Assert.IsTrue(metrics.QueryEngineTimes.DocumentLoadTime.TotalMilliseconds > 0, "Expected DocumentLoadTimeInMs to be > 0, metrics = {0}", metrics);
            //Assert.IsTrue(metrics.RetrievedDocumentCount > 0, "Expected RetrievedDocumentCount to be > 0, metrics = {0}", metrics);
            Assert.IsTrue(metrics.TotalQueryExecutionTime.TotalMilliseconds > 0, "Expected TotalExecutionTimeInMs to be > 0, metrics = {0}", metrics);
            //Assert.IsTrue(metrics.QueryEngineTimes.WriteOutputTime.TotalMilliseconds > 0, "Expected WriteOutputTimeInMs to be > 0, metrics = {0}", metrics);
            //Assert.IsTrue(metrics.RetrievedDocumentSize > 0, "Expected RetrievedDocumentSize to be > 0, metrics = {0}", metrics);
            Assert.IsTrue(metrics.IndexLookupTime.TotalMilliseconds > 0, "Expected IndexLookupTimeInMs to be > 0, metrics = {0}", metrics);
            Assert.IsTrue(metrics.QueryPreparationTimes.LogicalPlanBuildTime.TotalMilliseconds > 0, "Expected LogicalPlanBuildTimeInMs to be > 0, metrics = {0}", metrics);
            //Assert.AreEqual(metrics.QueryEngineTimes.VMExecutionTime - metrics.QueryEngineTimes.IndexLookupTime - metrics.QueryEngineTimes.DocumentLoadTime - metrics.QueryEngineTimes.WriteOutputTime,
            //    metrics.QueryEngineTimes.RuntimeExecutionTimes.TotalTime);
            Assert.IsTrue(metrics.RuntimeExecutionTimes.QueryEngineExecutionTime >= metrics.RuntimeExecutionTimes.SystemFunctionExecutionTime + metrics.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime,
                "Expected Query VM Execution Time to be > {0}, metrics = {1}", metrics.RuntimeExecutionTimes.SystemFunctionExecutionTime + metrics.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime, metrics);
            //Assert.IsTrue(metrics.QueryEngineTimes.VMExecutionTime >= metrics.QueryEngineTimes.RuntimeExecutionTimes.TotalTime,
            //    "Expected Query VM Execution Time to be > {0}, metrics = {1}", metrics.QueryEngineTimes.RuntimeExecutionTimes.TotalTime, metrics);
        }

        private async Task UpdateCollectionIndexingPolicyRandomlyAsync(DocumentClient client, CosmosContainerSettings collection, Random random)
        {
            Logger.LogLine("Start to update indexing policy.");

            // Compute the new index policy based on the random number generator.
            collection = new CosmosContainerSettings { Id = collection.Id, SelfLink = collection.SelfLink };

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

                foreach (var path in candidatePaths)
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
                foreach (var indexPath in collection.IndexingPolicy.IncludedPaths)
                {
                    Logger.LogLine(" * {0}", indexPath.Path);
                }

                Logger.LogLine("Excluded paths: ({0})", collection.IndexingPolicy.ExcludedPaths.Count);
                foreach (var path in collection.IndexingPolicy.ExcludedPaths)
                {
                    Logger.LogLine(" * {0}", path);
                }
            }

            Logger.LogLine("Updating collection indexing policy.");
            await this.RetryActionAsync(() => client.ReplaceDocumentCollectionAsync(collection), 10, TimeSpan.FromSeconds(5));

            Logger.LogLine("Waiting for reindexing to finish.");
            await Util.WaitForReIndexingToFinish(300, collection, client);
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

        private async Task LoadDocuments(CosmosContainerSettings coll)
        {
            await LoadDocuments(coll, File.ReadAllLines(@"Documents\MillionSong1KDocuments.json"));
        }

        private async Task LoadDocuments(CosmosContainerSettings coll, IEnumerable<string> serializedDocuments)
        {
            var script = MakeCreateDocumentsScript();
            var sproc = await Util.GetOrCreateStoredProcedureAsync(client, coll, new CosmosStoredProcedureSettings { Id = "bulkInsert", Body = script });

            List<string> documents = new List<string>();
            foreach (string line in serializedDocuments)
            {
                documents.Add(line);
                if (documents.Count == 15)
                {
                    await TestCommon.AsyncRetryRateLimiting(() =>
                        client.ExecuteStoredProcedureAsync<dynamic>(sproc, new[] { documents.ToArray() }));
                    documents = new List<string>();
                }
            }

            if (documents.Count != 0)
            {
                await TestCommon.AsyncRetryRateLimiting(() =>
                    client.ExecuteStoredProcedureAsync<dynamic>(sproc, new[] { documents.ToArray() })); ;
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

        internal void TestQueryDocuments(CosmosContainerSettings collection, bool manualIndex = false)
        {
            List<QueryDocument> listQueryDocuments = new List<QueryDocument>();
            foreach (var index in Enumerable.Range(1, 3))
            {
                QueryDocument doc = new QueryDocument()
                {
                    Id = string.Format(CultureInfo.InvariantCulture, "doc{0}", index),
                    NumericField = index,
                    StringField = index.ToString(CultureInfo.InvariantCulture),
                };

                INameValueCollection headers = new StringKeyValueCollection();
                if (!collection.IndexingPolicy.Automatic && manualIndex)
                {
                    headers.Add("x-ms-indexing-directive", "include");
                }


                listQueryDocuments.Add(this.client.Create<QueryDocument>(collection.GetIdOrFullName(), doc, headers));
            }

            QueryDocument[] documents = listQueryDocuments.ToArray();
            TestSQLQuery(collection, documents);

        }

        private void TestSQLQuery(CosmosContainerSettings collection, QueryDocument[] documents)
        {
            Action<DocumentClient> queryAction = (documentClient) =>
            {
                foreach (var index in Enumerable.Range(1, 3))
                {
                    string name = string.Format(CultureInfo.InvariantCulture, "doc{0}", index);

                    IEnumerable<Document> result = documentClient.CreateDocumentQuery<Document>(collection, @"SELECT r._rid FROM root r WHERE r.id=""" + name + @"""", null);
                    Assert.AreEqual(documents[index - 1].ResourceId, result.Single().ResourceId, "Expect queried id to match the id with the same name in the created documents");

                    result = documentClient.CreateDocumentQuery<Document>(collection, @"SELECT r._rid FROM root r WHERE r.NumericField=" + index.ToString(CultureInfo.InvariantCulture), null);
                    Assert.AreEqual(documents[index - 1].ResourceId, result.Single().ResourceId, "Expect queried id to match the id with the same name in the created documents");

                    result = documentClient.CreateDocumentQuery<Document>(collection, @"SELECT r._rid FROM root r WHERE r.StringField=""" + index.ToString(CultureInfo.InvariantCulture) + @"""", null);
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
            IEnumerable<CosmosDatabaseSettings> allDatabases = from database in this.client.CreateDatabaseQuery()
                                                 select database;

            foreach (CosmosDatabaseSettings database in allDatabases)
            {
                this.client.DeleteDatabaseAsync(database.SelfLink).Wait();
            }
        }

        internal void TestQueryDocumentsWithIndexPaths(
            CosmosContainerSettings collection,
            bool manualIndex = false,
            bool bExpectExcludedPathError = true,
            bool bExpectRangePathError = true)
        {
            List<QueryDocument> listQueryDocuments = new List<QueryDocument>();
            foreach (var index in Enumerable.Range(1, 3))
            {
                QueryDocument doc = new QueryDocument()
                {
                    Id = string.Format(CultureInfo.InvariantCulture, "doc{0}", index),
                    NumericField = index,
                    StringField = index.ToString(CultureInfo.InvariantCulture),
                };

                INameValueCollection headers = new StringKeyValueCollection();
                if (!collection.IndexingPolicy.Automatic && manualIndex)
                {
                    headers.Add("x-ms-indexing-directive", "include");
                }


                listQueryDocuments.Add(this.client.Create<QueryDocument>(collection.GetIdOrFullName(), doc, headers));
            }

            QueryDocument[] documents = listQueryDocuments.ToArray();

            foreach (var index in Enumerable.Range(1, 3))
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

        private async Task TestQueryWithTimestampOnCollectionAsync(CosmosContainerSettings collection)
        {
            // Basic CRUD
            Document document = (await this.client.CreateDocumentAsync(collection, new Document { Id = "doc01" })).Resource;
            if (collection.IndexingPolicy.IndexingMode == IndexingMode.Lazy)
            {
                await Util.WaitForLazyIndexingToCompleteAsync(this.client, collection);
            }

            this.VerifyQueryWithTimestampShouldReturnDocument(collection, document.GetPropertyValue<long>("_ts"), document.Id);

            document = (await this.client.ReplaceDocumentAsync(document)).Resource;
            if (collection.IndexingPolicy.IndexingMode == IndexingMode.Lazy)
            {
                await Util.WaitForLazyIndexingToCompleteAsync(this.client, collection);
            }

            this.VerifyQueryWithTimestampShouldReturnDocument(collection, document.GetPropertyValue<long>("_ts"), document.Id);

            await this.client.DeleteDocumentAsync(document);
            if (collection.IndexingPolicy.IndexingMode == IndexingMode.Lazy)
            {
                await Util.WaitForLazyIndexingToCompleteAsync(this.client, collection);
            }

            this.VerifyQueryWithTimestampShouldReturnNothing(collection, document.GetPropertyValue<long>("_ts"));

            // Bulk insert
            var script = MakeCreateDocumentsScript();
            var sproc = await Util.GetOrCreateStoredProcedureAsync(client, collection, new CosmosStoredProcedureSettings { Id = "bulkInsert", Body = script });

            Document[] documents = Enumerable.Repeat(new Document(), 10).ToArray();
            await this.client.ExecuteStoredProcedureAsync<dynamic>(sproc, new[] { documents });
            if (collection.IndexingPolicy.IndexingMode == IndexingMode.Lazy)
            {
                await Util.WaitForLazyIndexingToCompleteAsync(this.client, collection);
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

        private void VerifyQueryWithTimestampShouldReturnDocument(CosmosContainerSettings collection, long timestamp, string expectedDocumentId)
        {
            foreach (DocumentClient lockedClient in ReplicationTests.GetClientsLocked())
            {
                string query = "SELECT * FROM c where c._ts = " + timestamp.ToString(CultureInfo.InvariantCulture);
                Document[] queryResult = lockedClient.CreateDocumentQuery<Document>(collection, query).ToArray();
                Assert.AreEqual(1, queryResult.Length);
                Assert.AreEqual(expectedDocumentId, queryResult[0].Id);
            }
        }

        private void VerifyQueryWithTimestampShouldReturnNothing(CosmosContainerSettings collection, long timestamp)
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
                get
                {
                    return base.GetValue<int>("NumericField");
                }
                set
                {
                    base.SetValue("NumericField", value);
                }
            }

            public int NumericField2
            {
                get
                {
                    return base.GetValue<int>("NumericField2");
                }
                set
                {
                    base.SetValue("NumericField2", value);
                }
            }

            public string StringField
            {
                get
                {
                    return base.GetValue<string>("StringField");
                }
                set
                {
                    base.SetValue("StringField", value);
                }
            }
        }
    }
}
