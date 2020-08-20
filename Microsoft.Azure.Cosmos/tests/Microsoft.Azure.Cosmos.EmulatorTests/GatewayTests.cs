//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Dynamic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.CSharp.RuntimeBinder;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;
    using BulkInsertStoredProcedureOptions = Microsoft.Azure.Cosmos.Interop.Mongo.BulkInsertStoredProcedureOptions;
    using BulkInsertStoredProcedureResult = Microsoft.Azure.Cosmos.Interop.Mongo.BulkInsertStoredProcedureResult;
    using ConsistencyLevel = Documents.ConsistencyLevel;
    using RequestOptions = Documents.Client.RequestOptions;

    [TestClass]
    public class GatewayTests
    {
        internal readonly Uri baseUri;
        internal readonly string masterKey;
        private readonly object randomLock = new object();

        public GatewayTests()
        {
            this.baseUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            this.masterKey = ConfigurationManager.AppSettings["MasterKey"];
        }

        private static Document CreateDocument(DocumentClient client, Uri baseUri, DocumentCollection collection, string documentName, string property1, int property2, string pretrigger = null, string posttrigger = null)
        {
            dynamic document = new Document
            {
                Id = documentName
            };

            document.CustomProperty1 = property1;
            document.CustomProperty2 = property2;

            Documents.Client.RequestOptions options = new Documents.Client.RequestOptions();
            if (pretrigger != null)
            {
                options.PreTriggerInclude = new[] { pretrigger };
            }

            if (posttrigger != null)
            {
                options.PostTriggerInclude = new[] { posttrigger };
            }

            try
            {
                return client.CreateDocumentAsync(collection, (Document)document, options).Result;
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException;
            }
        }

        internal static TValue CreateExecuteAndDeleteProcedure<TValue>(DocumentClient client, DocumentCollection collection, string transientProcedure, string partitionKey = null)
        {
            return GatewayTests.CreateExecuteAndDeleteProcedure(client, collection, transientProcedure, out StoredProcedureResponse<TValue> ignored, partitionKey);
        }

        internal static TValue CreateExecuteAndDeleteProcedure<TValue>(DocumentClient client,
            DocumentCollection collection,
            string transientProcedure,
            out StoredProcedureResponse<TValue> response,
            string partitionKey = null)
        {
            // create
            StoredProcedure storedProcedure = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid().ToString(),
                Body = transientProcedure
            };
            StoredProcedure retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection, storedProcedure).Result;

            // execute
            if (partitionKey != null)
            {
                RequestOptions requestOptions = new RequestOptions
                {
                    PartitionKey = new Documents.PartitionKey(partitionKey)
                };
                response = client.ExecuteStoredProcedureAsync<TValue>(retrievedStoredProcedure, requestOptions).Result;
            }
            else
            {
                response = client.ExecuteStoredProcedureAsync<TValue>(retrievedStoredProcedure).Result;
            }

            // delete
            client.Delete<StoredProcedure>(retrievedStoredProcedure.GetIdOrFullName());

            return response.Response;
        }

        internal static TValue CreateExecuteAndDeleteCosmosProcedure<TValue>(Container collection,
            string transientProcedure,
            out StoredProcedureExecuteResponse<TValue> response,
            string partitionKey = null)
        {
            // create
            StoredProcedureProperties storedProcedure = new StoredProcedureProperties
            {
                Id = "storedProcedure" + Guid.NewGuid().ToString(),
                Body = transientProcedure
            };
            StoredProcedureResponse retrievedStoredProcedure = collection.Scripts.CreateStoredProcedureAsync(storedProcedure).Result;
            Assert.IsNotNull(retrievedStoredProcedure);
            Assert.AreEqual(storedProcedure.Id, retrievedStoredProcedure.Resource.Id);

            response = collection.Scripts.ExecuteStoredProcedureAsync<TValue>(
                storedProcedure.Id, 
                new Cosmos.PartitionKey(partitionKey),
                null).Result;
            Assert.IsNotNull(response);

            // delete
            StoredProcedureResponse deleteResponse = collection.Scripts.DeleteStoredProcedureAsync(storedProcedure.Id).Result;
            Assert.IsNotNull(deleteResponse);

            return response;
        }

        internal static TValue GetStoredProcedureExecutionResult<TValue>(DocumentClient client, StoredProcedure storedProcedure, params dynamic[] paramsList)
        {
            return client.ExecuteStoredProcedureAsync<TValue>(storedProcedure, paramsList).Result;
        }

        private static IEnumerable<string> GetDynamicMembers(object d)
        {
            Type t = d.GetType();
            if (typeof(IDynamicMetaObjectProvider).IsAssignableFrom(t))
            {
                IDynamicMetaObjectProvider dynamicProvider = (IDynamicMetaObjectProvider)d;
                DynamicMetaObject metaObject = dynamicProvider.GetMetaObject(Expression.Constant(dynamicProvider));
                return metaObject.GetDynamicMemberNames();
            }
            return null;
        }

        private static object GetDynamicMember(object obj, string memberName)
        {
            CallSiteBinder binder = Binder.GetMember(CSharpBinderFlags.None, memberName, obj.GetType(),
                new[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) });
            CallSite<Func<CallSite, object, object>> callsite = CallSite<Func<CallSite, object, object>>.Create(binder);
            return callsite.Target(callsite, obj);
        }

        private void AssertCollectionMaxSizeFromScriptResult(string result)
        {
            Assert.IsTrue(result.Contains("\"maxCollectionSizeInMB\":\"documentSize=0;"));
        }

        private async Task<string> ReadChangeFeedToEnd(DocumentClient client, string collectionLink, ChangeFeedOptions options)
        {
            string accumulator = string.Empty;
            using (IDocumentQuery<Document> query = client.CreateDocumentChangeFeedQuery(collectionLink, options))
            {
                while (query.HasMoreResults)
                {
                    DocumentFeedResponse<Document> response = await query.ExecuteNextAsync<Document>();
                    foreach (Document doc in response)
                    {
                        accumulator += doc.Id + ".";
                    }
                }
            }

            return accumulator;
        }

        internal async Task<IList<DocumentCollection>> CreateCollectionsAsync(DocumentClient client, IList<Documents.Database> databases, int numberOfCollectionsPerDatabase)
        {
            List<DocumentCollection> result = new List<DocumentCollection>();

            if (numberOfCollectionsPerDatabase > 0 && databases.Count > 0)
            {
                IList<Task<IList<DocumentCollection>>> createTasks = new List<Task<IList<DocumentCollection>>>();
                foreach (Documents.Database database in databases)
                {
                    createTasks.Add(this.CreateCollectionsAsync(client, database, numberOfCollectionsPerDatabase, false));
                }
                IList<DocumentCollection>[] arrayOfCollections = await Task.WhenAll(createTasks);

                foreach (IList<DocumentCollection> collections in arrayOfCollections)
                {
                    result.AddRange(collections);
                }
            }
            return result;
        }

        internal async Task<IList<DocumentCollection>> CreateCollectionsAsync(DocumentClient client, Documents.Database database, int numberOfCollectionsPerDatabase, bool isCollectionElastic)
        {
            IList<DocumentCollection> documentCollections = new List<DocumentCollection>();
            if (numberOfCollectionsPerDatabase > 0)
            {
                for (int i = 0; i < numberOfCollectionsPerDatabase; ++i)
                {
                    Logger.LogLine("Creating {0} collection (number {1}) in the system", isCollectionElastic ? "Elastic" : "Non-Elastic", i + 1);
                    if (isCollectionElastic)
                    {
                        DocumentCollection collection = await TestCommon.CreateCollectionAsync(client,
                            database,
                            new DocumentCollection
                            {
                                Id = Guid.NewGuid().ToString(),
                                PartitionKey = new PartitionKeyDefinition
                                {
                                    Paths = new Collection<string> { "/customerid" }
                                }
                            },
                            new Documents.Client.RequestOptions { OfferThroughput = TestCommon.MinimumOfferThroughputToCreateElasticCollectionInTests });
                        documentCollections.Add(collection);
                    }
                    else
                    {
                        documentCollections.Add(await TestCommon.AsyncRetryRateLimiting(() => TestCommon.CreateCollectionAsync(client, database.CollectionsLink, new DocumentCollection { Id = Guid.NewGuid().ToString() })));
                    }
                }
            }
            return documentCollections;
        }

        private void Retry(Action action)
        {
            Queue<int> timeouts = new Queue<int>(new[] { 500, 1000, 1000, 1000, 1000, 1000, 5000, 10000 });

            while (true)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception)
                {
                    if (timeouts.Count == 0)
                    {
                        throw;
                    }
                    int retryMilliseconds = timeouts.Dequeue();
                    Logger.LogLine("Retry {0} milliseconds", retryMilliseconds);
                    Task.Delay(retryMilliseconds);
                }
            }
        }

        [TestMethod]
        public async Task ValidateStoredProcedureCrud_SessionGW()
        {
            await this.ValidateStoredProcedureCrudAsync(ConsistencyLevel.Session,
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Gateway });
        }

        [TestMethod]
        public async Task ValidateStoredProcedureCrud_SessionDirectTcp()
        {
            await this.ValidateStoredProcedureCrudAsync(ConsistencyLevel.Session,
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp });
        }

        [TestMethod]
        public async Task ValidateStoredProcedureCrud_SessionDirectHttps()
        {
            await this.ValidateStoredProcedureCrudAsync(ConsistencyLevel.Session,
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Https });
        }

        internal async Task ValidateStoredProcedureCrudAsync(ConsistencyLevel consistencyLevel, ConnectionPolicy connectionPolicy)
        {
            DocumentClient client = TestCommon.CreateClient(connectionPolicy.ConnectionMode == ConnectionMode.Gateway,
                connectionPolicy.ConnectionProtocol,
                defaultConsistencyLevel: consistencyLevel);

            Documents.Database database = null;
            DocumentCollection collection1 = TestCommon.CreateOrGetDocumentCollection(client, out database);

            Logger.LogLine("Listing StoredProcedures");
            DocumentFeedResponse<StoredProcedure> storedProcedureCollection1 = await client.ReadStoredProcedureFeedAsync(collection1.StoredProceduresLink);

            string storedProcedureName = "StoredProcedure" + Guid.NewGuid();
            StoredProcedure storedProcedure = new StoredProcedure
            {
                Id = storedProcedureName,
                Body = "function() {var x = 10;}"
            };

            Logger.LogLine("Adding StoredProcedure");
            StoredProcedure retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection1, storedProcedure).Result;
            Assert.IsNotNull(retrievedStoredProcedure);
            Assert.IsTrue(retrievedStoredProcedure.Id.Equals(storedProcedureName, StringComparison.OrdinalIgnoreCase), "Mismatch in storedProcedure name");
            Assert.IsTrue(retrievedStoredProcedure.Body.Equals("function() {var x = 10;}", StringComparison.OrdinalIgnoreCase), "Mismatch in storedProcedure content");

            Logger.LogLine("Listing StoredProcedures");
            DocumentFeedResponse<StoredProcedure> storedProcedureCollection2 = client.ReadFeed<StoredProcedure>(collection1.GetIdOrFullName());
            Assert.AreEqual(storedProcedureCollection1.Count + 1, storedProcedureCollection2.Count, "StoredProcedure Collections count dont match");

            Logger.LogLine("Listing StoredProcedures with FeedReader");
            ResourceFeedReader<StoredProcedure> feedReader = client.CreateStoredProcedureFeedReader(collection1);
            int count = 0;
            while (feedReader.HasMoreResults)
            {
                count += feedReader.ExecuteNextAsync().Result.Count;
            }

            Assert.AreEqual(storedProcedureCollection1.Count + 1, count, "StoredProcedure Collections count dont match for feedReader");

            Logger.LogLine("Querying StoredProcedure");
            this.Retry(() =>
            {
                IDocumentQuery<dynamic> queryService = client.CreateStoredProcedureQuery(collection1.StoredProceduresLink,
                    @"select * from root r where r.id=""" + storedProcedureName + @"""").AsDocumentQuery();

                DocumentFeedResponse<StoredProcedure> storedProcedureCollection3 = queryService.ExecuteNextAsync<StoredProcedure>().Result;

                Assert.IsNotNull(storedProcedureCollection3, "Query result is null");
                Assert.AreNotEqual(0, storedProcedureCollection3.Count, "Collection count dont match");

                foreach (StoredProcedure queryStoredProcedure in storedProcedureCollection3)
                {
                    Assert.AreEqual(storedProcedureName, queryStoredProcedure.Id, "StoredProcedure Name dont match");
                }
            });

            Logger.LogLine("Updating StoredProcedure");
            retrievedStoredProcedure.Body = "function() {var x = 20;}";
            StoredProcedure retrievedStoredProcedure2 = client.Update(retrievedStoredProcedure, null);
            Assert.IsNotNull(retrievedStoredProcedure2);
            Assert.IsTrue(retrievedStoredProcedure2.Id.Equals(storedProcedureName, StringComparison.OrdinalIgnoreCase), "Mismatch in storedProcedure name");
            Assert.IsTrue(retrievedStoredProcedure2.Body.Equals("function() {var x = 20;}", StringComparison.OrdinalIgnoreCase), "Mismatch in storedProcedure content");

            Logger.LogLine("Querying StoredProcedure");
            this.Retry(() =>
            {
                IDocumentQuery<dynamic> queryService = client.CreateStoredProcedureQuery(collection1.StoredProceduresLink,
                    @"select * from root r where r.id=""" + storedProcedureName + @"""").AsDocumentQuery();

                DocumentFeedResponse<StoredProcedure> storedProcedureCollection4 = queryService.ExecuteNextAsync<StoredProcedure>().Result;

                Assert.AreEqual(1, storedProcedureCollection4.Count); // name is always indexed
            });

            Logger.LogLine("Read StoredProcedure");
            StoredProcedure getStoredProcedure = client.Read<StoredProcedure>(retrievedStoredProcedure.ResourceId);
            Assert.IsNotNull(getStoredProcedure);
            Assert.IsTrue(getStoredProcedure.Id.Equals(storedProcedureName, StringComparison.OrdinalIgnoreCase), "Mismatch in storedProcedure name");

            Logger.LogLine("Deleting StoredProcedure");
            client.Delete<StoredProcedure>(retrievedStoredProcedure.ResourceId);

            Logger.LogLine("Listing StoredProcedures");
            DocumentFeedResponse<StoredProcedure> storedProcedureCollection5 = client.ReadFeed<StoredProcedure>(collection1.GetIdOrFullName());
            Assert.AreEqual(storedProcedureCollection5.Count, storedProcedureCollection1.Count, "StoredProcedure delete is not working.");

            Logger.LogLine("Try read deleted storedProcedure");
            try
            {
                client.Read<StoredProcedure>(retrievedStoredProcedure.ResourceId);
                Assert.Fail("Should have thrown exception in previous statement");
            }
            catch (DocumentClientException clientException)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, clientException.StatusCode, "StatusCode dont match");
            }

            await client.DeleteDocumentCollectionAsync(collection1);

            try
            {
                IDocumentQuery<dynamic> queryService1 =
                    client.CreateStoredProcedureQuery(
                        collection1.StoredProceduresLink,
                        @"select * from root r where r.id=""" + storedProcedureName + @"""").AsDocumentQuery();

                await queryService1.ExecuteNextAsync<StoredProcedure>();
                Assert.Fail("Should get not found");
            }
            catch (DocumentClientException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }

            try
            {
                await client.DeleteStoredProcedureAsync(collection1.StoredProceduresLink);

                Assert.Fail("Should get not found");
            }
            catch (DocumentClientException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }
        }

        [TestMethod]
        public void ValidateTriggerCrud_SessionGW()
        {
            this.ValidateTriggerCrud(ConsistencyLevel.Session,
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Gateway });
        }

        [TestMethod]
        public void ValidateTriggerCrud_SessionDirectTcp()
        {
            this.ValidateTriggerCrud(ConsistencyLevel.Session,
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp });
        }

        [TestMethod]
        public void ValidateTriggerCrud_SessionDirectHttps()
        {
            this.ValidateTriggerCrud(ConsistencyLevel.Session,
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Https });
        }

        internal void ValidateTriggerCrud(ConsistencyLevel consistencyLevel, ConnectionPolicy connectionPolicy)
        {
            DocumentClient client = TestCommon.CreateClient(connectionPolicy.ConnectionMode == ConnectionMode.Gateway,
                connectionPolicy.ConnectionProtocol,
                defaultConsistencyLevel: consistencyLevel);

            Documents.Database database = null;
            DocumentCollection collection1 = TestCommon.CreateOrGetDocumentCollection(client, out database);

            Logger.LogLine("Listing Triggers");
            DocumentFeedResponse<Trigger> triggerCollection1 = client.ReadFeed<Trigger>(collection1.GetIdOrFullName());

            string triggerName = "Trigger" + Guid.NewGuid();
            Trigger trigger = new Trigger
            {
                Id = triggerName,
                Body = "function() {var x = 10;}",
                TriggerType = Documents.TriggerType.Pre,
                TriggerOperation = Documents.TriggerOperation.All
            };

            Logger.LogLine("Adding Trigger");
            Trigger retrievedTrigger = client.CreateTriggerAsync(collection1, trigger).Result;
            Assert.IsNotNull(retrievedTrigger);
            Assert.IsTrue(retrievedTrigger.Id.Equals(triggerName, StringComparison.OrdinalIgnoreCase), "Mismatch in trigger name");
            Assert.IsTrue(retrievedTrigger.Body.Equals("function() {var x = 10;}", StringComparison.OrdinalIgnoreCase), "Mismatch in trigger content");
            Assert.IsTrue(retrievedTrigger.TriggerType.Equals(Documents.TriggerType.Pre), "Mismatch in trigger type");
            Assert.IsTrue(retrievedTrigger.TriggerOperation.Equals(Documents.TriggerOperation.All), "Mismatch in trigger CRUD type");

            Logger.LogLine("Listing Triggers");
            DocumentFeedResponse<Trigger> triggerCollection2 = client.ReadFeed<Trigger>(collection1.GetIdOrFullName());
            Assert.AreEqual(triggerCollection1.Count + 1, triggerCollection2.Count, "Trigger Collections count dont match");

            Logger.LogLine("Listing Triggers with FeedReader");
            ResourceFeedReader<Trigger> feedReader = client.CreateTriggerFeedReader(collection1);
            int count = 0;
            while (feedReader.HasMoreResults)
            {
                count += feedReader.ExecuteNextAsync().Result.Count;
            }

            Assert.AreEqual(triggerCollection1.Count + 1, count, "Trigger Collections count dont match for feedReader");


            Logger.LogLine("Querying Trigger");
            this.Retry(() =>
            {
                IDocumentQuery<dynamic> queryService = client.CreateTriggerQuery(collection1.TriggersLink,
                    @"select * from root r where r.id=""" + triggerName + @"""").AsDocumentQuery();

                DocumentFeedResponse<Trigger> triggerCollection3 = queryService.ExecuteNextAsync<Trigger>().Result;

                Assert.IsNotNull(triggerCollection3, "Query result is null");
                Assert.AreNotEqual(0, triggerCollection3.Count, "Collection count dont match");

                foreach (Trigger queryTrigger in triggerCollection3)
                {
                    Assert.AreEqual(triggerName, queryTrigger.Id, "Trigger Name dont match");
                }
            });

            Logger.LogLine("Updating Trigger");
            try
            {
                retrievedTrigger.Body = "function() {var x = 20;}";
                retrievedTrigger.TriggerOperation = Documents.TriggerOperation.Create;
                Trigger retrievedTrigger2 = client.Update(retrievedTrigger, null);
                Assert.IsNotNull(retrievedTrigger2);
                Assert.IsTrue(retrievedTrigger2.Id.Equals(triggerName, StringComparison.OrdinalIgnoreCase), "Mismatch in trigger name");
                Assert.IsTrue(retrievedTrigger2.Body.Equals("function() {var x = 20;}", StringComparison.OrdinalIgnoreCase), "Mismatch in trigger content");
                Assert.IsTrue(retrievedTrigger2.TriggerType.Equals(Documents.TriggerType.Pre), "Mismatch in trigger type");
                Assert.IsTrue(retrievedTrigger2.TriggerOperation.Equals(Documents.TriggerOperation.Create), "Mismatch in trigger CRUD type");
            }
            catch (Exception e)
            {
                Assert.IsNull(e);
            }

            Logger.LogLine("Querying Trigger");
            this.Retry(() =>
            {
                IDocumentQuery<dynamic> queryService = client.CreateTriggerQuery(collection1.TriggersLink,
                    @"select * from root r where r.id=""" + triggerName + @"""").AsDocumentQuery();

                DocumentFeedResponse<Trigger> triggerCollection4 = queryService.ExecuteNextAsync<Trigger>().Result;

                Assert.AreEqual(1, triggerCollection4.Count); // name is always indexed
            });

            Logger.LogLine("Read Trigger");
            Trigger getTrigger = client.Read<Trigger>(retrievedTrigger.ResourceId);
            Assert.IsNotNull(getTrigger);
            Assert.IsTrue(getTrigger.Id.Equals(triggerName, StringComparison.OrdinalIgnoreCase), "Mismatch in trigger name");

            Logger.LogLine("Deleting Trigger");
            client.Delete<Trigger>(retrievedTrigger.ResourceId);

            Logger.LogLine("Listing Triggers");
            DocumentFeedResponse<Trigger> triggerCollection5 = client.ReadFeed<Trigger>(collection1.ResourceId);
            Assert.AreEqual(triggerCollection5.Count, triggerCollection1.Count, "Trigger delete is not working.");

            Logger.LogLine("Try read deleted trigger");
            try
            {
                client.Read<Trigger>(retrievedTrigger.ResourceId);
                Assert.Fail("Should have thrown exception in previous statement");
            }
            catch (DocumentClientException clientException)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, clientException.StatusCode, "StatusCode dont match");
            }
        }

        [TestMethod]
        public void ValidateUserDefinedFunctionCrud_SessionGW()
        {
            this.ValidateUserDefinedFunctionCrud(ConsistencyLevel.Session,
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Gateway });
        }

        [TestMethod]
        public void ValidateUserDefinedFunctionCrud_SessionDirectTcp()
        {
            this.ValidateUserDefinedFunctionCrud(ConsistencyLevel.Session,
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp });
        }

        [TestMethod]

        public void ValidateUserDefinedFunctionCrud_SessionDirectHttps()
        {
            this.ValidateUserDefinedFunctionCrud(ConsistencyLevel.Session,
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Https });
        }

        [TestMethod]
        public void ValidateUserDefinedFunctionTimeout()
        {
            try
            {
                DocumentClient client = TestCommon.CreateClient(true);

                DocumentCollection collection1 = TestCommon.CreateOrGetDocumentCollection(client, out Documents.Database database);

                // udfName should fail if it is not a valid SQL token name.
                string udfName = "udf" + Guid.NewGuid().ToString().Replace("-", "");
                // infinite udf input:
                UserDefinedFunction udfInfinite = new UserDefinedFunction
                {
                    Id = udfName,
                    Body = @"function infinite_loop() { while(1 == 1) { a = 5; b = 6; c = a + b; } }",
                };

                UserDefinedFunction retrievedUdfInfinite = client.CreateUserDefinedFunctionAsync(collection1.UserDefinedFunctionsLink, udfInfinite).Result;
                IDocumentQuery<dynamic> docServiceQuery1 = client.CreateDocumentQuery(collection1.DocumentsLink, string.Format(CultureInfo.CurrentCulture, "select udf.{0}() as infinite", udfName)).AsDocumentQuery();

                DocumentFeedResponse<dynamic> docCollectionShouldTimeout = docServiceQuery1.ExecuteNextAsync().Result;
                Assert.Fail("Should have thrown exception in previous statement");
            }
            catch (AggregateException e)
            {
                DocumentClientException dce = e.InnerException as DocumentClientException;
                Assert.IsTrue(HttpStatusCode.RequestTimeout == dce.StatusCode || HttpStatusCode.Forbidden == dce.StatusCode, "ValidateUserDefinedFunctionTimeout should fail with RequestTimeout");
            }
        }

        internal void ValidateUserDefinedFunctionCrud(ConsistencyLevel consistencyLevel, ConnectionPolicy connectionPolicy)
        {
            DocumentClient client = TestCommon.CreateClient(connectionPolicy.ConnectionMode == ConnectionMode.Gateway,
                connectionPolicy.ConnectionProtocol,
                defaultConsistencyLevel: consistencyLevel);

            Documents.Database database = null;
            DocumentCollection collection1 = TestCommon.CreateOrGetDocumentCollection(client, out database);

            Logger.LogLine("Listing UserDefinedFunctions");
            DocumentFeedResponse<UserDefinedFunction> userDefinedFunctionCollection1 = client.ReadFeed<UserDefinedFunction>(collection1.ResourceId);

            string userDefinedFunctionName = "UserDefinedFunction" + Guid.NewGuid();
            UserDefinedFunction userDefinedFunction = new UserDefinedFunction
            {
                Id = userDefinedFunctionName,
                Body = "function userDefinedFunction() {var x = 10;}",
            };

            Logger.LogLine("Adding UserDefinedFunction");
            UserDefinedFunction retrievedUserDefinedFunction = new UserDefinedFunction { };

            try
            {
                retrievedUserDefinedFunction = client.CreateUserDefinedFunctionAsync(collection1, userDefinedFunction).Result;
                Assert.IsNotNull(retrievedUserDefinedFunction);
                Assert.IsTrue(retrievedUserDefinedFunction.Id.Equals(userDefinedFunctionName, StringComparison.OrdinalIgnoreCase), "Mismatch in userDefinedFunction name");
                Assert.IsTrue(retrievedUserDefinedFunction.Body.Equals("function userDefinedFunction() {var x = 10;}", StringComparison.OrdinalIgnoreCase), "Mismatch in userDefinedFunction content");
            }
            catch (Exception e)
            {
                Assert.IsNull(e);
            }

            Logger.LogLine("Listing UserDefinedFunctions");
            DocumentFeedResponse<UserDefinedFunction> userDefinedFunctionCollection2 = client.ReadFeed<UserDefinedFunction>(collection1.GetIdOrFullName());
            Assert.AreEqual(userDefinedFunctionCollection1.Count + 1, userDefinedFunctionCollection2.Count, "UserDefinedFunction Collections count dont match");

            Logger.LogLine("Listing UserDefinedFunctions with FeedReader");
            ResourceFeedReader<UserDefinedFunction> feedReader = client.CreateUserDefinedFunctionFeedReader(collection1);
            int count = 0;
            while (feedReader.HasMoreResults)
            {
                count += feedReader.ExecuteNextAsync().Result.Count;
            }

            Assert.AreEqual(userDefinedFunctionCollection1.Count + 1, count, "UserDefinedFunctions Collections count dont match for feedReader");

            Logger.LogLine("Querying UserDefinedFunction");
            this.Retry(() =>
            {
                IDocumentQuery<dynamic> queryService = client.CreateUserDefinedFunctionQuery(collection1,
                    @"select * from root r where r.id=""" + userDefinedFunctionName + @"""").AsDocumentQuery();

                DocumentFeedResponse<UserDefinedFunction> userDefinedFunctionCollection3 = queryService.ExecuteNextAsync<UserDefinedFunction>().Result;

                Assert.IsNotNull(userDefinedFunctionCollection3, "Query result is null");
                Assert.AreNotEqual(0, userDefinedFunctionCollection3.Count, "Collection count dont match");

                foreach (UserDefinedFunction queryUserDefinedFunction in userDefinedFunctionCollection3)
                {
                    Assert.AreEqual(userDefinedFunctionName, queryUserDefinedFunction.Id, "UserDefinedFunction Name dont match");
                }
            });

            Logger.LogLine("Updating UserDefinedFunction");
            retrievedUserDefinedFunction.Body = "function userDefinedFunction() {var x = 20;}";
            UserDefinedFunction retrievedUserDefinedFunction2 = client.Update(retrievedUserDefinedFunction, null);
            Assert.IsNotNull(retrievedUserDefinedFunction2);
            Assert.IsTrue(retrievedUserDefinedFunction2.Id.Equals(userDefinedFunctionName, StringComparison.OrdinalIgnoreCase), "Mismatch in userDefinedFunction name");
            Assert.IsTrue(retrievedUserDefinedFunction2.Body.Equals("function userDefinedFunction() {var x = 20;}", StringComparison.OrdinalIgnoreCase), "Mismatch in userDefinedFunction content");

            Logger.LogLine("Querying UserDefinedFunction");
            this.Retry(() =>
            {
                IDocumentQuery<dynamic> queryService = client.CreateUserDefinedFunctionQuery(collection1,
                    @"select * from root r where r.id=""" + userDefinedFunctionName + @"""").AsDocumentQuery();

                DocumentFeedResponse<UserDefinedFunction> userDefinedFunctionCollection4 = queryService.ExecuteNextAsync<UserDefinedFunction>().Result;

                Assert.AreEqual(1, userDefinedFunctionCollection4.Count); // name is always indexed
            });

            Logger.LogLine("Read UserDefinedFunction");
            UserDefinedFunction getUserDefinedFunction = client.Read<UserDefinedFunction>(retrievedUserDefinedFunction.GetIdOrFullName());
            Assert.IsNotNull(getUserDefinedFunction);
            Assert.IsTrue(getUserDefinedFunction.Id.Equals(userDefinedFunctionName, StringComparison.OrdinalIgnoreCase), "Mismatch in userDefinedFunction name");

            Logger.LogLine("Deleting UserDefinedFunction");
            client.Delete<UserDefinedFunction>(retrievedUserDefinedFunction.ResourceId);

            Logger.LogLine("Listing UserDefinedFunctions");
            DocumentFeedResponse<UserDefinedFunction> userDefinedFunctionCollection5 = client.ReadFeed<UserDefinedFunction>(collection1.GetIdOrFullName());
            Assert.AreEqual(userDefinedFunctionCollection5.Count, userDefinedFunctionCollection1.Count, "UserDefinedFunction delete is not working.");

            Logger.LogLine("Try read deleted userDefinedFunction");
            try
            {
                client.Read<UserDefinedFunction>(retrievedUserDefinedFunction.ResourceId);
                Assert.Fail("Should have thrown exception in previous statement");
            }
            catch (DocumentClientException clientException)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, clientException.StatusCode, "StatusCode dont match");
            }
        }

        [TestMethod]
        public void ValidateTriggersNameBased()
        {
            DocumentClient client = TestCommon.CreateClient(false);
            TestCommon.DeleteAllDatabasesAsync().Wait();
            Documents.Database database = TestCommon.CreateOrGetDatabase(client);

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
            DocumentCollection collection1 = TestCommon.CreateCollectionAsync(client, database.SelfLink, new DocumentCollection { Id = "TestTriggers" + Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition }).Result;

            // uppercase name
            Trigger t1 = new Trigger
            {
                Id = "t1",
                Body = @"function() {
                    var item = getContext().getRequest().getBody();
                    item.id = item.id.toUpperCase() + 't1';
                    getContext().getRequest().setBody(item);
                }",
                TriggerType = Documents.TriggerType.Pre,
                TriggerOperation = Documents.TriggerOperation.All
            };
            Trigger retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection1, t1).Result;

            string docId = Guid.NewGuid().ToString();
            dynamic document = new Document
            {
                Id = docId
            };

            document.CustomProperty1 = "a";
            document.CustomProperty2 = "b";

            ResourceResponse<Document> docResponse = client.CreateDocumentAsync(collection1.AltLink, document, new Documents.Client.RequestOptions { PreTriggerInclude = new List<string> { "t1" } }).Result;
            Assert.IsTrue((docId + "t1").Equals(docResponse.Resource.Id, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<Trigger> CreateTriggerAndValidateAsync(DocumentClient client, DocumentCollection documentCollection, Trigger trigger)
        {
            Trigger retrievedTrigger = await client.CreateTriggerAsync(documentCollection, trigger);
            Assert.AreEqual(trigger.Id, retrievedTrigger.Id);
            Assert.AreEqual(trigger.Body, retrievedTrigger.Body);
            Assert.AreEqual(trigger.TriggerType, retrievedTrigger.TriggerType);
            Assert.AreEqual(trigger.TriggerOperation, retrievedTrigger.TriggerOperation);

            return retrievedTrigger;
        }

        [TestMethod]
        public void ValidateTriggers()
        {
            this.ValidateTriggersInternal(Protocol.Https, ConsistencyLevel.Session);
            this.ValidateTriggersInternal(Protocol.Tcp, ConsistencyLevel.Session);
        }

        internal void ValidateTriggersInternal(Protocol protocol = Protocol.Https, ConsistencyLevel? consistencyLevel = null)
        {
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            DocumentClient client = TestCommon.CreateClient(false, protocol: protocol, defaultConsistencyLevel: consistencyLevel);
#endif
#if !DIRECT_MODE
            DocumentClient client = TestCommon.CreateClient(true, defaultConsistencyLevel: consistencyLevel);
#endif
            TestCommon.DeleteAllDatabasesAsync().Wait();
            Documents.Database database = TestCommon.CreateOrGetDatabase(client);
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
            DocumentCollection collection1 = TestCommon.CreateCollectionAsync(client, database, new DocumentCollection { Id = "TestTriggers" + Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition }).Result;

            // 1. Basic tests

            // uppercase name
            Trigger t1 = new Trigger
            {
                Id = "t1",
                Body = @"function() {
                    var item = getContext().getRequest().getBody();
                    item.id = item.id.toUpperCase() + 't1';
                    getContext().getRequest().setBody(item);
                }",
                TriggerType = Documents.TriggerType.Pre,
                TriggerOperation = Documents.TriggerOperation.All
            };
            Trigger retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection1, t1).Result;

            dynamic doct1 = GatewayTests.CreateDocument(client, this.baseUri, collection1, "Doc1", "empty", 0, pretrigger: "t1");
            Assert.AreEqual("DOC1t1", doct1.Id);

            // post trigger - get
            Trigger response1 = new Trigger
            {
                Id = "response1",
                Body = @"function() {
                    var prebody = getContext().getRequest().getBody();
                    if (prebody.id != 'TESTING POST TRIGGERt1') throw 'name mismatch';
                    var postbody = getContext().getResponse().getBody();
                    if (postbody.id != 'TESTING POST TRIGGERt1') throw 'name mismatch';
                };",
                TriggerType = Documents.TriggerType.Post,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection1, response1).Result;

            dynamic docresponse1 = GatewayTests.CreateDocument(client, this.baseUri, collection1, "testing post trigger", "empty", 0, pretrigger: "t1", posttrigger: "response1");
            Assert.AreEqual("TESTING POST TRIGGERt1", docresponse1.Id);

            // post trigger response
            Trigger response2 = new Trigger
            {
                Id = "response2",
                Body = @"function() {
                    var predoc = getContext().getRequest().getBody(); 
                    var postdoc = getContext().getResponse().getBody();
                    postdoc.id += predoc.id + 'response2'; 
                    getContext().getResponse().setBody(postdoc); 
                };",
                TriggerType = Documents.TriggerType.Post,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection1, response2).Result;

            dynamic docresponse2 = GatewayTests.CreateDocument(client, this.baseUri, collection1, "post trigger output", "empty", 0, pretrigger: "t1", posttrigger: "response2");
            Assert.AreEqual("POST TRIGGER OUTPUTt1POST TRIGGER OUTPUTt1response2", docresponse2.Id);

            // post trigger cannot set anything in request, cannot set headers in response
            Trigger response3 = new Trigger
            {
                Id = "response3",
                Body = @"function() {
                    var exceptionSeen = false;
                    try { getContext().getRequest().setBody('lol'); }
                    catch (err) { exceptionSeen = true; }
                    if(!exceptionSeen) throw 'expected exception not seen';
            
                    exceptionSeen = false;
                    try { getContext().getRequest().setValue('Body', 'lol'); }
                    catch (err) { exceptionSeen = true; }
                    if(!exceptionSeen) throw 'expected exception not seen';

                    exceptionSeen = false;
                    try { getContext().getRequest().setValue('Test', 'lol'); }
                    catch (err) { exceptionSeen = true; }
                    if(!exceptionSeen) throw 'expected exception not seen';

                    exceptionSeen = false;
                    try { getContext().getResponse().setValue('Test', 'lol'); }
                    catch (err) { exceptionSeen = true; }
                    if(!exceptionSeen) throw 'expected exception not seen';
                };",
                TriggerType = Documents.TriggerType.Post,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection1, response3).Result;

            dynamic docresponse3 = GatewayTests.CreateDocument(client, this.baseUri, collection1, "testing post trigger2", "empty", 0, pretrigger: "t1", posttrigger: "response3");
            Assert.AreEqual("TESTING POST TRIGGER2t1", docresponse3.Id);

            DocumentCollection collection2 = TestCommon.CreateCollectionAsync(client, database, new DocumentCollection { Id = "TestTriggers" + Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition }).Result;

            // empty trigger
            Trigger t2 = new Trigger
            {
                Id = "t2",
                Body = @"function() { }",
                TriggerType = Documents.TriggerType.Pre,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection2, t2).Result;

            dynamic doct2 = GatewayTests.CreateDocument(client, this.baseUri, collection2, "Doc2", "Prop1Value", 101, pretrigger: "t2");
            Assert.AreEqual("Doc2", doct2.Id);

            // lowercase name
            Trigger t3 = new Trigger
            {
                Id = "t3",
                Body = @"function() { 
                    var item = getContext().getRequest().getBody();
                    item.id = item.id.toLowerCase() + 't3';
                    getContext().getRequest().setBody(item);
                }",
                TriggerType = Documents.TriggerType.Pre,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection2, t3).Result;

            dynamic doct3 = GatewayTests.CreateDocument(client, this.baseUri, collection2, "Doc3", "empty", 0, pretrigger: "t3");
            Assert.AreEqual("doc3t3", doct3.Id);

            // trigger type mismatch - failure case
            Trigger triggerTypeMismatch = new Trigger
            {
                Id = "triggerTypeMismatch",
                Body = @"function() { }",
                TriggerType = Documents.TriggerType.Post,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection2, triggerTypeMismatch).Result;

            bool exceptionThrown = false;
            try
            {
                dynamic doctriggertype = GatewayTests.CreateDocument(client, this.baseUri, collection2, "Docoptype", "empty", 0, pretrigger: "triggerTypeMismatch");
            }
            catch (DocumentClientException e)
            {
                Assert.IsNotNull(e);
                exceptionThrown = true;
            }
            Assert.IsTrue(exceptionThrown, "mismatch in trigger type didn't cause failure");

            // pre-trigger throws - failure case
            Trigger preTriggerThatThrows = new Trigger
            {
                Id = "preTriggerThatThrows",
                Body = @"function() { throw new Error(409, 'Error 409'); }",
                TriggerType = Documents.TriggerType.Pre,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection2, preTriggerThatThrows).Result;

            try
            {
                GatewayTests.CreateDocument(client, this.baseUri, collection2, "Docoptype", "empty", 0, pretrigger: "preTriggerThatThrows");
                Assert.Fail("Should throw and not get here.");
            }
            catch (DocumentClientException e)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, e.StatusCode);
                Assert.AreEqual(409, (int)e.GetSubStatus());
                Assert.IsNotNull(e.Message);
            }

            // post-trigger throws - failure case
            Trigger postTriggerThatThrows = new Trigger
            {
                Id = "postTriggerThatThrows",
                Body = @"function() { throw new Error(4444, 'Error 4444'); }",
                TriggerType = Documents.TriggerType.Post,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection2, postTriggerThatThrows).Result;

            try
            {
                GatewayTests.CreateDocument(client, this.baseUri, collection2, "Docoptype", "empty", 0, posttrigger: "postTriggerThatThrows");
                Assert.Fail("Should throw and not get here.");
            }
            catch (DocumentClientException e)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, e.StatusCode);
                Assert.AreEqual(4444, (int)e.GetSubStatus());
                Assert.IsNotNull(e.Message);
            }

            // failure test - trigger without body
            Trigger triggerNoBody = new Trigger
            {
                Id = "trigger" + Guid.NewGuid(),
                TriggerType = Documents.TriggerType.Post,
                TriggerOperation = Documents.TriggerOperation.All
            };
            try
            {
                Trigger retrievedTriggerNoBody = client.CreateTriggerAsync(collection2, triggerNoBody).Result;
            }
            catch (Exception ex)
            {
                Assert.IsNotNull(ex);
                Assert.IsNotNull(ex.InnerException);
                Assert.IsTrue(ex.InnerException.Message.Contains("The input content is invalid because the required properties - 'body; ' - are missing"));
            }

            // failure test - trigger without trigger type
            Trigger triggerNoType = new Trigger
            {
                Id = "trigger" + Guid.NewGuid(),
                Body = @"function() { }",
                TriggerOperation = Documents.TriggerOperation.All
            };
            try
            {
                Trigger retrievedTriggerNoType = client.CreateTriggerAsync(collection2, triggerNoType).Result;
            }
            catch (Exception ex)
            {
                Assert.IsNotNull(ex);
                Assert.IsNotNull(ex.InnerException);
                Assert.IsTrue(ex.InnerException.Message.Contains("The input content is invalid because the required properties - 'triggerType; ' - are missing"));
            }

            // failure test - trigger without trigger operation
            Trigger triggerNoOperation = new Trigger
            {
                Id = "trigger" + Guid.NewGuid(),
                Body = @"function() { }",
                TriggerType = Documents.TriggerType.Post,
            };
            try
            {
                Trigger retrievedTriggerNoType = client.CreateTriggerAsync(collection2, triggerNoOperation).Result;
            }
            catch (Exception ex)
            {
                Assert.IsNotNull(ex);
                Assert.IsNotNull(ex.InnerException);
                Assert.IsTrue(ex.InnerException.Message.Contains("The input content is invalid because the required properties - 'triggerOperation; ' - are missing"));
            }

            // TODO: uncomment when preserializedScripts is enabled.
            //// failure test - trigger with empty body
            //Trigger triggerEmptyBody = new Trigger
            //{
            //    Id = "triggerEmptyBody",
            //    Body = @"",
            //    TriggerType = TriggerType.Pre,
            //    TriggerOperation = TriggerOperation.All
            //};
            //try
            //{
            //    retrievedTrigger = CreateTriggerAndValidateAsync(client, collection2, triggerEmptyBody).Result;
            //    Assert.Fail("Script with syntax error should have failed when being stored");
            //}
            //catch (AggregateException e)
            //{
            //    Assert.IsNotNull(e.InnerException);
            //    Assert.IsInstanceOfType(e.InnerException, typeof(DocumentClientException));
            //    TestCommon.AssertException((DocumentClientException)e.InnerException, HttpStatusCode.BadRequest);
            //    Assert.IsTrue(e.InnerException.Message.Contains("Encountered exception while compiling Javascript."));
            //}

            // failure test - trigger on resource other than document, say database
            Documents.Database dbToCreate = new Documents.Database
            {
                Id = "temp" + Guid.NewGuid().ToString()
            };
            try
            {
                dbToCreate = client.CreateDatabaseAsync(dbToCreate, new Documents.Client.RequestOptions { PreTriggerInclude = new List<string> { "t1" } }).Result;
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
                Assert.IsNotNull(e.InnerException);

                DocumentClientException de = e.InnerException as DocumentClientException;
                Assert.AreEqual(HttpStatusCode.BadRequest.ToString(), de.Error.Code);
            }

            // failure test - trigger on non-CRUD operation
            INameValueCollection headers = new DictionaryNameValueCollection
            {
                { "x-ms-pre-trigger-include", "t1" }
            };

            try
            {
                dynamic docFailure = client.ReadFeed<Document>(collection1.ResourceId, headers);
                // It actually always succeed here.
                // Assert.Fail("It should fail with trigger on non-CRUD operation");
            }
            catch (DocumentClientException e)
            {
                Assert.IsNotNull(e);
                Assert.AreEqual(HttpStatusCode.BadRequest.ToString(), e.Error.Code);
            }

            // TODO: uncomment when preserializeScripts is enabled.
            //            // precompilation should catch errors on create
            //            Trigger triggerSyntaxError = new Trigger
            //            {
            //                Id = "trigger" + Guid.NewGuid().ToString(),
            //                Body = @"
            //                    method() { // method is invalid identifier
            //                        for(var i = 0; i < 10; i++) getContext().getResponse().appendValue('Body', i);
            //                    }",
            //                TriggerType = TriggerType.Pre,
            //                TriggerOperation = TriggerOperation.All
            //            };

            //            try
            //            {
            //                Trigger failureTrigger = client.CreateTriggerAsync(collection2, triggerSyntaxError).Result;
            //                Assert.Fail("Script with syntax error should have failed when being stored");
            //            }
            //            catch (AggregateException e)
            //            {
            //                Assert.IsNotNull(e.InnerException);
            //                Assert.IsInstanceOfType(e.InnerException, typeof(DocumentClientException));
            //                TestCommon.AssertException((DocumentClientException)e.InnerException, HttpStatusCode.BadRequest);
            //                Assert.IsTrue(e.InnerException.Message.Contains("Encountered exception while compiling Javascript."));
            //            }

            // 2. Request and response objects

            // set request body - pretrigger
            Trigger request1 = new Trigger
            {
                Id = "request1",
                Body = @"function() {
                    var docBody = getContext().getRequest().getBody();
                    if(docBody.id != 'abc') throw 'name mismatch';
                    docBody.id = 'def';
                    getContext().getRequest().setBody(docBody);
                };",
                TriggerType = Documents.TriggerType.Pre,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection2, request1).Result;

            dynamic docrequest1 = GatewayTests.CreateDocument(client, this.baseUri, collection2, "abc", "empty", 0, pretrigger: "request1");
            Assert.AreEqual("def", docrequest1.Id);

            DocumentCollection collection3 = TestCommon.CreateCollectionAsync(client, database, new DocumentCollection { Id = "TestTriggers" + Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition }).Result;

            // set request body multiple times
            Trigger request2 = new Trigger
            {
                Id = "request2",
                Body = @"function() {
                    for (var i = 0; i < 200; i++)
                        { 
                        var item = getContext().getRequest().getBody();
                        item.id += 'a';
                        getContext().getRequest().setBody(item);
                        }
                };",
                TriggerType = Documents.TriggerType.Pre,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection3, request2).Result;

            dynamic docrequest2 = GatewayTests.CreateDocument(client, this.baseUri, collection3, "doc", "empty", 0, pretrigger: "request2");
            Assert.AreEqual(203, docrequest2.Id.Length);

            // no response in pre-trigger
            Trigger request3 = new Trigger
            {
                Id = "request3",
                Body = @"function() {
                    var exceptionSeen = false;
                    try { getContext().getResponse(); }
                    catch (err) { exceptionSeen = true; }
                    if (!exceptionSeen) throw 'expected exception not seen';

                    var item = getContext().getRequest().getBody();
                    item.id = 'noresponse';
                    getContext().getRequest().setValue('Body', item);
                }",
                TriggerType = Documents.TriggerType.Pre,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection3, request3).Result;

            dynamic docrequest3 = GatewayTests.CreateDocument(client, this.baseUri, collection3, "noname", "empty", 0, pretrigger: "request3");
            Assert.AreEqual("noresponse", docrequest3.Id);

            // not allowed to set headers in pre-trigger
            Trigger request4 = new Trigger
            {
                Id = "request4",
                Body = @"function() {
                    var exceptionSeen = false;
                    try { getContext().getRequest().setValue('Test', '123');; }
                    catch (err) { exceptionSeen = true; }
                    if (!exceptionSeen) throw 'expected exception not seen';

                    var item = getContext().getRequest().getBody();
                    item.id = 'noheaders';
                    getContext().getRequest().setValue('Body', item);
                }",
                TriggerType = Documents.TriggerType.Pre,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection3, request4).Result;

            ResourceResponse<Document> docrequest4 = client.CreateDocumentAsync(collection3, new Document { Id = "noname" }, new Documents.Client.RequestOptions { PreTriggerInclude = new List<string> { "request4" } }).Result;
            Assert.IsTrue(docrequest4.Resource.Id == "noheaders");
            Assert.IsTrue(docrequest4.ResponseHeaders["Test"] == null);

            // post-trigger response - contains quota details
            Trigger responseQuotaHeader = new Trigger
            {
                Id = "responseQuotaHeader",
                Body = @"function() {
                    var quotaCurrentUsage = getContext().getResponse().getResourceQuotaCurrentUsage();
                    var quotaMax = getContext().getResponse().getMaxResourceQuota();

                    var postdoc = getContext().getResponse().getBody();
                    postdoc.Title = quotaCurrentUsage;
                    postdoc.Author = quotaMax;
                    getContext().getResponse().setValue('Body', postdoc);
                }",
                TriggerType = Documents.TriggerType.Post,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection3, responseQuotaHeader).Result;

            Book docresponseQuotaHeader = (dynamic)client.CreateDocumentAsync(collection3, new Book { Id = "quotaDocument" }, new Documents.Client.RequestOptions { PostTriggerInclude = new List<string> { "responseQuotaHeader" } }).Result.Resource;
            Assert.IsTrue(docresponseQuotaHeader.Author.Contains("collectionSize"));
            Assert.IsTrue(docresponseQuotaHeader.Title.Contains("collectionSize"));

            // 3. CRUD

            // trigger operation type mismatch
            Trigger triggerOpType = new Trigger
            {
                Id = "triggerOpType",
                Body = @"function() { }",
                TriggerType = Documents.TriggerType.Post,
                TriggerOperation = Documents.TriggerOperation.Delete
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection3, triggerOpType).Result;

            exceptionThrown = false;
            try
            {
                dynamic docoptype = GatewayTests.CreateDocument(client, this.baseUri, collection3, "Docoptype", "empty", 0, null, posttrigger: "triggerOpType");
            }
            catch (DocumentClientException e)
            {
                Assert.IsNotNull(e);
                exceptionThrown = true;
            }
            Assert.IsTrue(exceptionThrown, "mismatch in trigger operation type didn't cause failure");

            // to test if post trigger can abort transaction
            Trigger triggerAbortTransaction = new Trigger
            {
                Id = "triggerAbortTransaction",
                Body = @"function() { throw 'always throw';}",
                TriggerType = Documents.TriggerType.Post,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection3, triggerAbortTransaction).Result;

            exceptionThrown = false;
            try
            {
                dynamic docabort = GatewayTests.CreateDocument(client, this.baseUri, collection3, "Docabort", "empty", 0, null, posttrigger: "triggerAbortTransaction");
            }
            catch
            {
                exceptionThrown = true;
            }
            Assert.IsTrue(exceptionThrown, "always throw post trigger didnt not cause create document to fail");

            DocumentFeedResponse<Document> docCollection = client.ReadFeed<Document>(collection3.GetIdOrFullName());

            foreach (Document doc in docCollection)
            {
                Assert.AreNotEqual(doc.Id, "Docabort"); // make sure the doc isnt present
            }

            DocumentCollection collection4 = TestCommon.CreateCollectionAsync(client, database, new DocumentCollection { Id = "TestTriggers" + Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition }).Result;

            // delete post trigger
            Trigger deletePostTrigger = new Trigger
            {
                Id = "deletePostTrigger",
                Body = @"function() {
                var client = getContext().getCollection();
                var item = getContext().getResponse().getBody();
                function callback(err, docFeed, responseOptions) 
                { 
                    if(err) throw 'Error while creating document'; 
                    if(docFeed.length > 0 )
                    {
                        var doc = null;
                        for(var i = 0; i < docFeed.length; i++)
                        {
                            //if(docFeed[i].id && docFeed[i].id != item.id)
                            if(docFeed[i].id && docFeed[i].id == item.id)
                            {
                            doc = docFeed[i];
                            break;
                            }
                        }
                        if(doc != null)
                        { 
                            //client.replaceDocument(doc._self, {newDocProperty : 1, etag : doc.etag}, {}, function(err, respons) { if (err) throw new Error('error');});
                            client.deleteDocument(doc._self, function(err) {if(err) throw 'Error while deleting document';});
                        }
                    }
                    if(responseOptions.continuation) client.readDocuments(client.getSelfLink(), { pageSize : 10, continuation : responseOptions.continuation }, callback);
                }; 
                client.readDocuments(client.getSelfLink(), { pageSize : 10}, callback);}",
                TriggerType = Documents.TriggerType.Post,
                TriggerOperation = Documents.TriggerOperation.All
            };
            retrievedTrigger = this.CreateTriggerAndValidateAsync(client, collection4, deletePostTrigger).Result;

            dynamic docDeletePostTrigger = null;
            try
            {
                docDeletePostTrigger = GatewayTests.CreateDocument(client, this.baseUri, collection4, "Doc4", "Prop1Value", 101, null, posttrigger: "deletePostTrigger");
            }
            catch (Exception e)
            {
                Logger.LogLine(e.Message);
                Assert.Fail(e.Message);
            }

            try
            {
                Document docRead = client.Read<Document>((string)docDeletePostTrigger.ResourceId);
                Assert.Fail("Delete in post trigger didn't succeed");
            }
            catch
            {
                Logger.LogLine("Exception thrown when trying to read deleted document as expected");
            }

            // 4. Multiple triggers
            exceptionThrown = false;
            try
            {
                Document doc5 = client.CreateDocumentAsync(collection4, new Document { Id = "Doc5" }, new Documents.Client.RequestOptions { PreTriggerInclude = new List<string> { "t1", "t3" } }).Result.Resource;
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
                DocumentClientException de = e.InnerException as DocumentClientException;
                Assert.IsNotNull(de);
                exceptionThrown = true;
            }
            Assert.IsTrue(exceptionThrown, "multiple pre-triggers didn't cause failure");

            exceptionThrown = false;
            try
            {
                ResourceResponse<Document> docMultiple1 = client.CreateDocumentAsync(collection4, new Document { Id = "multipleHeaders1" }, new Documents.Client.RequestOptions { PreTriggerInclude = new List<string> { "t1" }, PostTriggerInclude = new List<string> { "response2", "multiple1" } }).Result;
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
                DocumentClientException de = e.InnerException as DocumentClientException;
                Assert.IsNotNull(de);
                exceptionThrown = true;
            }
            Assert.IsTrue(exceptionThrown, "multiple post-triggers didn't cause failure");

            // re-enable these tests if we re-enable multiple triggers
            //            // pre-trigger request body
            //            Document doc5 = client.CreateDocumentAsync(collection1, new Document { Id =  "Doc5" }, new RequestOptions { PreTriggerInclude = new List<string> { "t1", "t3" } }).Result.Resource;
            //            Assert.AreEqual("doc5t1t3", doc5.Id);

            //            // check order
            //            Document doc6 = client.CreateDocumentAsync(collection1, new Document { Id =  "Doc6" }, new RequestOptions { PreTriggerInclude = new List<string> { "t3", "t1" } }).Result.Resource;
            //            Assert.AreEqual("DOC6T3t1", doc6.Id);

            //            // multiple of same name
            //            Document doc7 = client.CreateDocumentAsync(collection1, new Document { Id =  "Doc7" }, new RequestOptions { PreTriggerInclude = new List<string> { "t1", "t1", "t1" } }).Result.Resource;
            //            Assert.AreEqual("DOC7T1T1t1", doc7.Id);

            //            // post-trigger added headers
            //            Trigger multiple1 = new Trigger
            //            {
            //                Id =  "multiple1",
            //                Body = @"function() {
            //                    var predocname = getContext().getRequest().getValue('predocname');
            //                    var postdocname = getContext().getResponse().getValue('postdocname');
            //                    getContext().getResponse().setValue('predocname', predocname + 'multiple1');
            //                    getContext().getResponse().setValue('postdocnamenew', postdocname + 'multiple1');
            //                };",
            //                TriggerType = TriggerType.Post,
            //                TriggerOperation = TriggerOperation.All
            //            };
            //            retrievedTrigger = CreateTriggerAndValidateAsync(client, collection1, multiple1).Result;

            //            ResourceResponse<Document> docMultiple1 = client.CreateDocumentAsync(collection1, new Document { Id =  "multipleHeaders1" }, new RequestOptions { PreTriggerInclude = new List<string> { "t1" }, PostTriggerInclude = new List<string> { "response2", "multiple1" } }).Result;
            //            Assert.IsTrue(docMultiple1.Resource.Id == "MULTIPLEHEADERS1t1");
            //            Assert.IsTrue(docMultiple1.ResponseHeaders["predocname"] == "MULTIPLEHEADERS1t1response2multiple1");
            //            Assert.IsTrue(docMultiple1.ResponseHeaders["postdocname"] == "MULTIPLEHEADERS1t1response2");
            //            Assert.IsTrue(docMultiple1.ResponseHeaders["postdocnamenew"] == "MULTIPLEHEADERS1t1response2multiple1");

            //            // check order
            //            ResourceResponse<Document> docMultiple2 = client.CreateDocumentAsync(collection1, new Document { Id =  "multipleHeaders2" }, new RequestOptions { PreTriggerInclude = new List<string> { "t1" }, PostTriggerInclude = new List<string> { "multiple1", "response2" } }).Result;
            //            Assert.IsTrue(docMultiple2.Resource.Id == "MULTIPLEHEADERS2t1");
            //            Assert.IsTrue(docMultiple2.ResponseHeaders["predocname"] == "MULTIPLEHEADERS2t1response2");
            //            Assert.IsTrue(docMultiple2.ResponseHeaders["postdocname"] == "MULTIPLEHEADERS2t1response2");
            //            Assert.IsTrue(docMultiple2.ResponseHeaders["postdocnamenew"] == "undefinedmultiple1");

            //            // multiple of same
            //            ResourceResponse<Document> docMultiple3 = client.CreateDocumentAsync(collection1, new Document { Id =  "multipleHeaders3" }, new RequestOptions { PreTriggerInclude = new List<string> { "t1" }, PostTriggerInclude = new List<string> { "response2", "multiple1", "multiple1", "multiple1" } }).Result;
            //            Assert.IsTrue(docMultiple1.Resource.Id == "MULTIPLEHEADERS3t1");
            //            Assert.IsTrue(docMultiple1.ResponseHeaders["predocname"] == "MULTIPLEHEADERS3t1response2multiple1multiple1multiple1");
            //            Assert.IsTrue(docMultiple1.ResponseHeaders["postdocname"] == "MULTIPLEHEADERS3t1response2");
            //            Assert.IsTrue(docMultiple1.ResponseHeaders["postdocnamenew"] == "MULTIPLEHEADERS3t1response2multiple1");
        }

        [TestMethod]
        public async Task ValidateLongProcessingStoredProcedures()
        {
            CosmosClient client = TestCommon.CreateCosmosClient(true);

            Cosmos.Database database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            ContainerProperties inputCollection = new ContainerProperties
            {
                Id = "ValidateExecuteSprocs",
                PartitionKey = partitionKeyDefinition

            };
            Container collection = await database.CreateContainerAsync(inputCollection);

            Document documentDefinition = new Document() { Id = Guid.NewGuid().ToString() };
            Document document = await collection.CreateItemAsync<Document>(documentDefinition);
            string script = @"function() {
                var output = 0;
                function callback(err, docFeed, responseOptions) {
                    if(err) throw 'Error while reading document';
                    output++;
                    __.response.setBody(output);
                    __.readDocuments(__.getSelfLink(), { pageSize : 1, continuation : responseOptions.continuation }, callback);
                };
                __.readDocuments(__.getSelfLink(), { pageSize : 1, continuation : ''}, callback);
            }";
            //Script cannot timeout.
            Scripts scripts = collection.Scripts;
            StoredProcedureProperties storedProcedure = await scripts.CreateStoredProcedureAsync(new StoredProcedureProperties("scriptId", script));
            string result = await scripts.ExecuteStoredProcedureAsync<string>(
                storedProcedureId: "scriptId",
                partitionKey: new Cosmos.PartitionKey(documentDefinition.Id),
                parameters: null);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task ValidateSprocWithFailedUpdates()
        {
            CosmosClient client = TestCommon.CreateCosmosClient(true);

            Cosmos.Database database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            ContainerProperties inputCollection = new ContainerProperties
            {
                Id = "ValidateSprocWithFailedUpdates" + Guid.NewGuid().ToString(),
                PartitionKey = partitionKeyDefinition

            };
            Container collection = await database.CreateContainerAsync(inputCollection);


            dynamic document = new Document
            {
                Id = Guid.NewGuid().ToString()
            };

            await collection.CreateItemAsync(document);

            string script = string.Format(CultureInfo.InvariantCulture, @" function() {{
                var client = getContext().getCollection();                
                client.createDocument(client.getSelfLink(), {{ id: '{0}', a : 2, b : 'b', unindexed : 1}}, {{ }}, function(err, docCreated, options) {{ 
                   if(err) {{
                     getContext().getResponse().setBody('dummy response'); 
                   }} 
                   else {{
                     getContext().getResponse().setBody(docCreated);  
                     
                   }}
                }});
            }}", document.Id);


            //Script cannot timeout.
            try
            {
                Scripts scripts = collection.Scripts;
                StoredProcedureProperties storedProcedure = await scripts.CreateStoredProcedureAsync(new StoredProcedureProperties("scriptId", script));
                string result = await scripts.ExecuteStoredProcedureAsync<string>(
                    "scriptId", 
                    partitionKey: new Cosmos.PartitionKey(document.Id),
                    parameters: null);
            }
            catch (DocumentClientException exception)
            {
                Assert.Fail("Exception should not have occurred. {0}", exception.InnerException.ToString());
            }
            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task ValidateSystemSproc()
        {
            await this.ValidateSystemSprocInternal(true);
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            ValidateSystemSprocInternal(false, Protocol.Https);
            ValidateSystemSprocInternal(false, Protocol.Tcp);
#endif
        }

        internal async Task ValidateSystemSprocInternal(bool useGateway, Protocol protocol = Protocol.Tcp)
        {
            CosmosClient client = TestCommon.CreateCosmosClient(useGateway);
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
            Cosmos.Database database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            ContainerProperties collectionSpec = new ContainerProperties
            {
                Id = "ValidateSystemSproc" + Guid.NewGuid().ToString(),
                PartitionKey = partitionKeyDefinition
            };
            Container collection = await database.CreateContainerAsync(collectionSpec);

            Scripts scripts = collection.Scripts;            
            string input = "foobar";

            string result = string.Empty;
            try
            {
                result = scripts.ExecuteStoredProcedureAsync<string>("__.sys.echo", new Cosmos.PartitionKey("anyPk"), new dynamic[] { input }).Result;
            }
            catch (DocumentClientException exception)
            {
                Assert.Fail("Exception should not have occurred. {0}", exception.InnerException.ToString());
            }

            Assert.AreEqual(input, result);
            await database.DeleteAsync();
        }

        /*
        [TestMethod]
        public void ValidateStoredProcedures()
        {

            // RID routing
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            ValidateStoredProceduresInternal(false, false, Protocol.Tcp);
            ValidateStoredProceduresInternal(false, false, Protocol.Https);
#endif
            ValidateStoredProceduresInternal(true, false);

            // name routing
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            ValidateStoredProceduresInternal(false, true, Protocol.Tcp);
            ValidateStoredProceduresInternal(false, true, Protocol.Https);
#endif
            ValidateStoredProceduresInternal(true, true);
        }

        public void ValidateStoredProceduresInternal(bool useGateway, bool useNameRouting, Protocol protocol = Protocol.Tcp)
        {
            DocumentClient client = TestCommon.CreateClient(useGateway, protocol);

            TestCommon.DeleteAllDatabasesAsync().Wait();

            Database database = TestCommon.CreateOrGetDatabase(client);

            DocumentCollection inputCollection = new DocumentCollection { Id = "ValidateExecuteSprocs" + Guid.NewGuid().ToString() };

            IndexingPolicyOld indexingPolicyOld = new IndexingPolicyOld();
            indexingPolicyOld.IndexingMode = IndexingMode.Consistent;

            IndexingPath includedPath = new IndexingPath
            {
                Path = "/"
            };
            indexingPolicyOld.IncludedPaths.Add(includedPath);
            indexingPolicyOld.ExcludedPaths.Add(@"/""unindexed""/?");

            inputCollection.IndexingPolicy = IndexingPolicyTranslator.TranslateIndexingPolicyV1ToV2(indexingPolicyOld);
            DocumentCollection collection = client.Create(database.ResourceId, inputCollection);

#region basic capabilities
            // concat test
            string script = "function() { getContext().getResponse().setBody('a' + (1+2)); }";
            string result = GatewayTests.CreateExecuteAndDeleteProcedure<string>(client, collection, script);
            Assert.AreEqual("a3", result);

            // setting response body
            StoredProcedure storedProcedureResponseSetter = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid(),
                Body = @"
                    function() {
                        for(var i = 0; i < 1000; i++)
                        {
                            var item = getContext().getResponse().getBody();
                            if (i > 0 && item != i - 1) throw 'body mismatch';
                            getContext().getResponse().setBody(i);
                        }
                    }"
            };
            StoredProcedure retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection, storedProcedureResponseSetter).Result;
            int resultInteger = GetStoredProcedureExecutionResult<int>(client, retrievedStoredProcedure);
            Assert.IsTrue(resultInteger == 999);
            client.Delete<StoredProcedure>(retrievedStoredProcedure.ResourceId);

            // appending response body
            StoredProcedure storedProcedureResponseAppender = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid().ToString(),
                Body = @"
                    function() {
                        for(var i = 0; i < 10; i++) getContext().getResponse().appendValue('Body', i);
                    }"
            };
            retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection, storedProcedureResponseAppender).Result;
            resultInteger = GetStoredProcedureExecutionResult<int>(client, retrievedStoredProcedure);
            Assert.IsTrue(resultInteger == 123456789);
            client.Delete<StoredProcedure>(retrievedStoredProcedure.ResourceId);

            // TODO: uncomment when preserializeScripts is enalbed.
            //            // precompilation should catch errors on create
            //            StoredProcedure storedProcedureResponseSyntaxError = new StoredProcedure
            //            {
            //                Id =  "storedProcedure" + Guid.NewGuid().ToString(),
            //                Body = @"
            //                    method() { // method is invalid identifier
            //                        for(var i = 0; i < 10; i++) getContext().getResponse().appendValue('Body', i);
            //                    }"
            //            };

            //            try
            //            {
            //                retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection.SelfLink, storedProcedureResponseSyntaxError).Result;
            //                Assert.Fail("Script with syntax error should have failed when being stored");
            //            }
            //            catch (AggregateException e)
            //            {
            //                Assert.IsNotNull(e.InnerException);
            //                Assert.IsInstanceOfType(e.InnerException, typeof(DocumentClientException));
            //                TestCommon.AssertException((DocumentClientException)e.InnerException, HttpStatusCode.BadRequest);
            //                Assert.IsTrue(e.InnerException.Message.Contains("Encountered exception while compiling Javascript."));
            //            }

            // Parameter passing: validate that when we use dynamic fields of a document passed as parameter,
            // these fields are not lost.
            StoredProcedure storedProcedureValidateDynamicParams = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid().ToString(),
                Body = @"
                    function(doc) {
                        getContext().getResponse().setBody({ id: doc.id, dynamicField: doc.dynamicField, });
                    }"
            };

            retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection, storedProcedureValidateDynamicParams).Result;
            string documentWithDynamicFieldId = "doc" + Guid.NewGuid();
            dynamic documentWithDynamicField = new Document { Id = documentWithDynamicFieldId };
            string dynamicFieldValue = "find me if you can";
            documentWithDynamicField.dynamicField = dynamicFieldValue;
            dynamic dynamicResult = GetStoredProcedureExecutionResult<dynamic>(client, retrievedStoredProcedure, documentWithDynamicField);
            Assert.AreEqual(dynamicFieldValue, (string)dynamicResult.dynamicField, "Dynamic field didn't make the round-trip.");
            Assert.AreEqual(documentWithDynamicFieldId, (string)dynamicResult.id, "Document name got lost.");
            client.Delete<StoredProcedure>(retrievedStoredProcedure.ResourceId);

            // Exception with specific sub-status code.
            StoredProcedure storedProcedureSubStatusCode = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid().ToString(),
                Body = @"function() { throw new Error(1234, 'Error 1234'); }"
            };

            retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection, storedProcedureSubStatusCode).Result;
            try
            {
                client.ExecuteStoredProcedureAsync<object>(retrievedStoredProcedure).Wait();
                Assert.Fail("Should throw and not get here");
            }
            catch (AggregateException ex)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, ((DocumentClientException)ex.InnerException).StatusCode);
                Assert.AreEqual(1234, (int)((DocumentClientException)ex.InnerException).GetSubStatus());
                Assert.IsFalse(string.IsNullOrEmpty(ex.InnerException.Message));
            }

            client.Delete<StoredProcedure>(retrievedStoredProcedure.ResourceId);

            // Exception with no sub-status code.
            StoredProcedure storedProcedureNoSubStatusCode = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid().ToString(),
                Body = @"function() { throw new Error('Error'); }"
            };

            retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection, storedProcedureNoSubStatusCode).Result;
            try
            {
                client.ExecuteStoredProcedureAsync<object>(retrievedStoredProcedure).Wait();
                Assert.Fail("Should throw and not get here");
            }
            catch (AggregateException ex)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, ((DocumentClientException)ex.InnerException).StatusCode);
                Assert.AreEqual(SubStatusCodes.Unknown, ((DocumentClientException)ex.InnerException).GetSubStatus());
                Assert.IsFalse(string.IsNullOrEmpty(ex.InnerException.Message));
            }

            client.Delete<StoredProcedure>(retrievedStoredProcedure.ResourceId);

            // Exception due to compile error.
            StoredProcedure storedProcedureCompileError = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid().ToString(),
                Body = @"function() { This is to create a Compile Error! }"
            };

            retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection, storedProcedureCompileError).Result;
            try
            {
                client.ExecuteStoredProcedureAsync<object>(retrievedStoredProcedure).Wait();
                Assert.Fail("Should throw and not get here");
            }
            catch (AggregateException ex)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, ((DocumentClientException)ex.InnerException).StatusCode);
                Assert.AreEqual(SubStatusCodes.ScriptCompileError, ((DocumentClientException)ex.InnerException).GetSubStatus());
                Assert.IsFalse(string.IsNullOrEmpty(ex.InnerException.Message));
            }

            client.Delete<StoredProcedure>(retrievedStoredProcedure.ResourceId);
#endregion

#region document scripts
            // create document
            script = string.Format(CultureInfo.InvariantCulture, @" function() {{
                var client = getContext().getCollection();                
                client.createDocument(client.{0}, {{ id: 'testDoc', a : 2, b : 'b', unindexed : 1}}, {{}}, function(err, docCreated, options) {{ 
                   if(err) throw new Error('Error while creating document: ' + err.message); 
                   else {{
                     getContext().getResponse().setBody(docCreated);  
                   }}
                }});}}", useNameRouting ? "getAltLink()" : "getSelfLink()");

            Document createdDocument = GatewayTests.CreateExecuteAndDeleteProcedure<Document>(client, collection, script);
            Assert.IsTrue(createdDocument.GetPropertyValue<int>("a") == 2);
            Assert.IsTrue(createdDocument.GetPropertyValue<string>("b") == "b");

            // create second document
            script = string.Format(CultureInfo.InvariantCulture, @"function() {{
                var client = getContext().getCollection();
                client.createDocument(client.{0}, {{ id: 'testDoc2', aa : 22, bb : 'bb', unindexed : 1}}, {{}}, function(err, docCreated) {{ 
                   if(err) throw 'Error while creating document'; 
                   else getContext().getResponse().setBody(JSON.stringify(docCreated)); 
                }});}}", useNameRouting ? "getAltLink()" : "getSelfLink()");

            result = GatewayTests.CreateExecuteAndDeleteProcedure<string>(client, collection, script);

            Assert.IsTrue(result.Contains("\"aa\":22,\"bb\":\"bb\""));

            // list documents
            script = string.Format(CultureInfo.InvariantCulture, @"function() {{
                var client = getContext().getCollection();
                function callback(err, docFeed, responseOptions) {{
                    if(err) throw 'Error while reading documents';
                    docFeed.forEach(function(doc, i, arr) {{ getContext().getResponse().appendBody(JSON.stringify(doc));  }});
                        if(responseOptions.continuation) {{
                            client.readDocuments(client.{0}, {{ pageSize : 1, continuation : responseOptions.continuation }}, callback);
                        }};
                }};
                client.readDocuments(client.{0}, {{ pageSize : 1, continuation : ''}}, callback);}}", useNameRouting ? "getAltLink()" : "getSelfLink()");
            result = GatewayTests.CreateExecuteAndDeleteProcedure<string>(client, collection, script);
            Assert.IsTrue(result.Contains("\"a\":2,\"b\":\"b\""));
            Assert.IsTrue(result.Contains("\"aa\":22,\"bb\":\"bb\""));

            // query documents
            script = string.Format(CultureInfo.InvariantCulture, @"function() {{
                var client = getContext().getCollection();
                var recurseCount = 0;
                function callback(err, docFeed, responseOptions) {{
                    if(err) throw new Error('Error while querying documents' + err.message + ' after recursing: ' + recurseCount + ' times.');
                    recurseCount++;
                    docFeed.forEach(function(frag, i, arr) {{ getContext().getResponse().appendBody(JSON.stringify(frag));  }});
                        if(responseOptions.continuation) {{
                            client.queryDocuments(client.{0}, 'select * from root r where r.bb = ""bb""', {{ pageSize : 10, continuation : responseOptions.continuation }}, callback);
                        }};
                }};
                client.queryDocuments(client.{0}, 'select * from root r where r.bb = ""bb""', {{ pageSize : 10, continuation : ''}}, callback);}}", useNameRouting ? "getAltLink()" : "getSelfLink()");

            Retry(() =>
            {
                result = GatewayTests.CreateExecuteAndDeleteProcedure<string>(client, collection, script);

                if (string.IsNullOrEmpty(result))
                    throw new Exception("Not yet indexed");
            });
            Assert.IsTrue(result.Contains("bb"));

            // query documents - underscore
            script = @"function() {
                var variableForCapture = 'bb';
                __.filter(function(r) { return r.bb == variableForCapture; });
            }";

            Retry(() =>
            {
                result = GatewayTests.CreateExecuteAndDeleteProcedure<string>(client, collection, script);

                if (string.IsNullOrEmpty(result))
                    throw new Exception("Not yet indexed");
            });
            Assert.IsTrue(result.Contains("bb"));

            // query documents - fails without enable scan
            script = string.Format(CultureInfo.InvariantCulture, @"function() {{
                var client = getContext().getCollection();
                var recurseCount = 0;
                function callback(err, docFeed, responseOptions) {{
                    if(err) throw new Error('Error while querying documents' + err.message + ' after recursing: ' + recurseCount + ' times.');
                    recurseCount++;
                    docFeed.forEach(function(frag, i, arr) {{ getContext().getResponse().appendBody(JSON.stringify(frag));  }});
                        if(responseOptions.continuation) {{
                            client.queryDocuments(client.{0}, 'select * from root r where r.unindexed = 1', {{ pageSize : 10, continuation : responseOptions.continuation }}, callback);
                        }};
                }};
                client.queryDocuments(client.{0}, 'select * from root r where r.unindexed = 1', {{ pageSize : 10, continuation : ''}}, callback);}}", useNameRouting ? "getAltLink()" : "getSelfLink()");

            Retry(() =>
            {
                bool bRequiredExceptionSeen = false;
                try
                {
                    result = GatewayTests.CreateExecuteAndDeleteProcedure<string>(client, collection, script);
                }
                catch (Exception e)
                {
                    if (e != null &&
                        e.InnerException != null &&
                        e.InnerException.Message.Contains("An invalid query has been specified with filters against path(s) excluded from indexing. Consider adding allow scan header in the request"))
                    {
                        bRequiredExceptionSeen = true;
                    }
                    else
                    {
                        throw;
                    }
                }

                if (string.IsNullOrEmpty(result) && !bRequiredExceptionSeen)
                    throw new Exception("Not yet indexed");
            });

            // query documents - enable scan
            script = string.Format(CultureInfo.InvariantCulture, @"function() {{
                var client = getContext().getCollection();
                var recurseCount = 0;
                function callback(err, docFeed, responseOptions) {{
                    if(err) throw new Error('Error while querying documents' + err.message + ' after recursing: ' + recurseCount + ' times.');
                    recurseCount++;
                    docFeed.forEach(function(frag, i, arr) {{ getContext().getResponse().appendBody(JSON.stringify(frag));  }});
                        if(responseOptions.continuation) {{
                            client.queryDocuments(client.{0}, 'select * from root r where r.unindexed = 1', {{ pageSize : 10, continuation : responseOptions.continuation, enableScan : true }}, callback);
                        }};
                }};
                client.queryDocuments(client.{0}, 'select * from root r where r.unindexed = 1', {{ pageSize : 10, continuation : '', enableScan : true}}, callback);}}", useNameRouting ? "getAltLink()" : "getSelfLink()");

            Retry(() =>
            {
                result = GatewayTests.CreateExecuteAndDeleteProcedure<string>(client, collection, script);

                if (string.IsNullOrEmpty(result))
                    throw new Exception("Not yet indexed");
            });
            Assert.IsTrue(result.Contains(@"""testDoc"""));
            Assert.IsTrue(result.Contains(@"""testDoc2"""));
            Assert.IsTrue(result.Contains(@"""b"""));
            Assert.IsTrue(result.Contains(@"""bb"""));

            // replace documents
            script = string.Format(CultureInfo.InvariantCulture, @"function() {{
                var client = getContext().getCollection();
                function callback(err, docFeed, responseOptions) {{
                    if(err) throw new  Error('Error while reading documents: ' + err.message);
                    docFeed.forEach(function(doc, i, arr) {{
                        if('a' in doc) doc.a = doc.a+1;            // change value of a to 3
                        if('aa' in doc) doc.aa = doc.aa + 11;      // change value of aa to 33
                        if('b' in doc) doc.b = 'b2';                    // change value of b to b2
                        if('bb' in doc) doc.bb = 'bb2';                 // change value of bb to bb2
                        client.replaceDocument({0}, doc, {{indexAction:'exclude'}}, function(err, docReplaced, responseOptions) {{
                            if(err) throw new Error('Error while replacing document: ' + err.message);
                            getContext().getResponse().appendBody(JSON.stringify(docReplaced));
                            getContext().getResponse().appendBody(JSON.stringify(responseOptions));
                        }});
                    }});
                    if(responseOptions.continuation) {{
                        client.readDocuments(client.{1}, {{ pageSize : 10, continuation : responseOptions.continuation }}, callback);
                    }}
                }};
                client.readDocuments(client.{1}, {{ pageSize : 10}}, callback);}}",
                useNameRouting ? @"client.getAltLink() + '/docs/' + doc.id" : "doc._self",
                useNameRouting ? "getAltLink()" : "getSelfLink()");
            result = GatewayTests.CreateExecuteAndDeleteProcedure<string>(client, collection, script);
            Assert.IsTrue(result.Contains("\"a\":3,\"b\":\"b2\""));
            Assert.IsTrue(result.Contains("\"aa\":33,\"bb\":\"bb2\""));

            this.AssertCollectionMaxSizeFromScriptResult(result);
            Assert.IsTrue(result.Contains("\"currentCollectionSizeInMB\":\"documentSize=0;"));

            // query documents and see if the results dont exist because we said remove from index after replacing
            script = string.Format(CultureInfo.InvariantCulture, @"function() {{
                var client = getContext().getCollection();
                function callback(err, docFeed, responseOptions) {{
                    if(err) throw new Error('Error while querying documents: ' + err.message);
                    docFeed.forEach(function(frag, i, arr) {{ getContext().getResponse().appendBody(JSON.stringify(frag));  }});
                    if(responseOptions.continuation) {{
                        client.queryDocuments(client.{0}, 'select * from root r where r.bb = ""bb2""', {{ pageSize : 10, continuation : responseOptions.continuation }}, callback);
                    }}
                }};
                client.queryDocuments(client.{0}, 'select * from root r where r.bb = ""bb2""', {{ pageSize : 10}}, callback);}}", useNameRouting ? "getAltLink()" : "getSelfLink()");
            Retry(() =>
            {
                result = GatewayTests.CreateExecuteAndDeleteProcedure<string>(client, collection, script);
                Assert.AreNotEqual(result, "\"bb2\""); // we should not get query results now
            });

            // delete documents by enumerating them
            script = string.Format(CultureInfo.InvariantCulture, @"function() {{
                var client = getContext().getCollection();
                function callback(err, docFeed, responseOptions) {{
                    if(err) throw 'Error while creating document';
                    docFeed.forEach(function(doc, i, arr) {{
                        client.deleteDocument({0}, function(err) {{
                            if(err) throw 'Error while deleting document';
                        }});
                    }});
                    if(responseOptions.continuation) {{
                        client.readDocuments(client.{1}, {{ pageSize : 10, continuation : responseOptions.continuation }}, callback);
                    }}
                }};
                client.readDocuments(client.{1}, {{ pageSize : 10}}, callback);}}",
                useNameRouting ? @"client.getAltLink() + '/docs/' + doc.id" : "doc._self",
                useNameRouting ? "getAltLink()" : "getSelfLink()");
            result = GatewayTests.CreateExecuteAndDeleteProcedure<string>(client, collection, script);
            int numberOfDocuments = client.ReadDocumentFeedAsync(collection, new FeedOptions() { MaxItemCount = 100 }).Result.Count;
            Assert.AreEqual(0, numberOfDocuments);
#endregion

#region attachment scripts
            // create a document
            Book book = new Book() { Id = Guid.NewGuid().ToString(), Title = "War and Peace", Author = "Leo Tolstoy" };
            Document document = client.CreateDocumentAsync(collection, book).Result;
            string documentSelfLink = document.SelfLink;
            string documentAltLink = document.AltLink;

            // create a couple of media link attachments
            Random rand = new Random();
            string coverPhotoName = "CoverPhoto" + rand.Next();
            string ebookLinkName = "Ebooklink" + rand.Next();
            string audioBookLinkName = "Audiobook" + rand.Next();
            Attachment coverPhotoAttachment = new Attachment
            {
                Id = coverPhotoName,
                ContentType = "image/jpg",
                MediaLink = "http://upload.wikimedia.org/wikipedia/commons/a/af/Tolstoy_-_War_and_Peace_-_first_edition%2C_1869.jpg"
            };
            Attachment ebookLinkAttachment = new Attachment
            {
                Id = ebookLinkName,
                ContentType = "text/html",
                MediaLink = "http://gutenberg.org/ebooks/2600"
            };
            Attachment audioBookLinkAttachment = new Attachment
            {
                Id = audioBookLinkName,
                ContentType = "text/html",
                MediaLink = "http://www.booksshouldbefree.com/book/war-and-peace-book-01-by-leo-tolstoy"
            };

            coverPhotoAttachment = client.CreateAttachmentAsync(documentSelfLink, coverPhotoAttachment).Result;
            ebookLinkAttachment = client.CreateAttachmentAsync(documentSelfLink, ebookLinkAttachment).Result;
            audioBookLinkAttachment = client.CreateAttachmentAsync(documentSelfLink, audioBookLinkAttachment).Result;

            //test read attachment
            script = string.Format(CultureInfo.InvariantCulture, @"function() {{
                var client = getContext().getCollection();
                function callback(err, attachment) {{
                    if(err) throw new Error('Failed to read attachment: ' + err.message);
                    getContext().getResponse().setBody(attachment.media);
                }}
                client.readAttachment('{0}', callback);}}", useNameRouting ? coverPhotoAttachment.AltLink : coverPhotoAttachment.SelfLink);
            result = GatewayTests.CreateExecuteAndDeleteProcedure<string>(client, collection, script);
            Assert.AreEqual(coverPhotoAttachment.MediaLink, result);

            //test read attachments
            script = string.Format(CultureInfo.InvariantCulture, @"function() {{
                var client = getContext().getCollection();
                function callback(err, attachmentFeed, responseOptions) {{
                    if(err) throw new Error('Failed to read attachment feed: ' + err.message);
                    getContext().getResponse().appendBody(JSON.stringify(attachmentFeed) + '----');
                    if(responseOptions.continuation) {{
                        response = {{pageSize:1, continuation:responseOptions.continuation}};
                        client.readAttachments('{0}', response, callback);
                    }};
                }};
                client.readAttachments('{0}', {{pageSize:3}}, callback);}}", useNameRouting ? documentAltLink : documentSelfLink);
            result = GatewayTests.CreateExecuteAndDeleteProcedure<string>(client, collection, script);
            Assert.IsTrue(result.Contains(coverPhotoName));
            Assert.IsTrue(result.Contains(ebookLinkName));
            Assert.IsTrue(result.Contains(audioBookLinkName));

            //test query attachments
            script = string.Format(CultureInfo.InvariantCulture, @"function() {{
                var client = getContext().getCollection();
                function callback(err, attachmentFeed, responseOptions) {{
                    if(err) throw new Error('Failed to query attachment: ' + err.message);
                    if(responseOptions.continuation) throw new Error('Error in query result, only one attachment expected.');
                    getContext().getResponse().setBody(attachmentFeed[0].media);
                }};
                client.queryAttachments('{0}', 'select * from root r where r.id=""{1}""', {{pageSize:100}}, callback);}}", useNameRouting ? documentAltLink : documentSelfLink, ebookLinkName);
            result = GatewayTests.CreateExecuteAndDeleteProcedure<string>(client, collection, script);
            Assert.AreEqual(ebookLinkAttachment.MediaLink, result);

            //test replace attachments
            script = string.Format(CultureInfo.InvariantCulture, @"function() {{
                var client = getContext().getCollection();
                function callback(err, newAttachment) {{
                    if(err) throw new Error('Failed to replace attachment: ' + err.message);
                    getContext().getResponse().setBody(newAttachment);
                }};
                client.replaceAttachment('{0}', {1}, callback);}}",
                useNameRouting ? ebookLinkAttachment.AltLink : ebookLinkAttachment.SelfLink,
                string.Format(CultureInfo.InvariantCulture, "{{contentType: '{0}', id:'{1}', media:'anotherLink.html'}}", ebookLinkAttachment.ContentType, ebookLinkAttachment.Id));
            Attachment resultAttachment = GatewayTests.CreateExecuteAndDeleteProcedure<Attachment>(client, collection, script);
            Assert.AreEqual("anotherLink.html", resultAttachment.MediaLink);

            //test delete attachments
            script = string.Format(CultureInfo.InvariantCulture, @"function() {{
                var client = getContext().getCollection();
                function deleteCallback(err) {{
                    if(err) throw new Error('Failed to delete attachment: ' + err.message);
                }};
                client.readAttachments('{0}', function(readErr, attFeed, continuation) {{
                    if(readErr) throw new Error('Failed to read attachments feed: ' + readErr.message);
                    attFeed.forEach(function(e, i, arr) {{client.deleteAttachment({1}, deleteCallback);}});
                }});}}",
                useNameRouting ? documentAltLink : documentSelfLink,
                useNameRouting ? "'" + documentAltLink + "/attachments/' + " + "e.id" : "e._self");
            result = GatewayTests.CreateExecuteAndDeleteProcedure<string>(client, collection, script);
            Assert.AreEqual(0, client.ReadAttachmentFeedAsync(documentSelfLink).Result.Count);
#endregion

#region durable scripts
            StoredProcedure storedProcedure1 = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid().ToString(),
                Body = string.Format(CultureInfo.InvariantCulture, @"
                    function() {{
                        var client = getContext().getCollection();                
                        client.createDocument(client.{0}, {{ id: 'testDoc3', a : 2, b : 'b'}}, {{}}, function(err, docCreated, options) {{ 
                       if(err) throw new Error('Error while creating document: ' + err.message); 
                       else {{
                             getContext().getResponse().setBody(JSON.stringify(docCreated));  
                         if(options.maxCollectionSizeInMb == 0) throw 'max collection size not found'; 
                             getContext().getResponse().appendBody(JSON.stringify(options));  
                       }}
                        }});
                    }}", useNameRouting ? "getAltLink()" : "getSelfLink()")
            };
            retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection, storedProcedure1).Result;
            result = GetStoredProcedureExecutionResult<string>(client, retrievedStoredProcedure);
            Assert.IsTrue(result.Contains("\"a\":2,\"b\":\"b\""));
            this.AssertCollectionMaxSizeFromScriptResult(result);
            Assert.IsTrue(result.Contains("\"currentCollectionSizeInMB\":\"documentSize=0;"));
            client.Delete<StoredProcedure>(retrievedStoredProcedure.ResourceId);

            StoredProcedure storedProcedure2 = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid(),
                Body = @"function(input) {
                    getContext().getResponse().setBody('a' + input.temp);}"
            };
            retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection, storedProcedure2).Result;
            result = GetStoredProcedureExecutionResult<string>(client, retrievedStoredProcedure, JsonConvert.DeserializeObject("{\"temp\":\"so\"}"));
            Assert.AreEqual("aso", result);
            client.Delete<StoredProcedure>(retrievedStoredProcedure.ResourceId);

            StoredProcedure storedProcedure3 = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid(),
                Body = string.Format(CultureInfo.InvariantCulture, @" function(input) {{
                    var test = input;
                    var client = getContext().getCollection();                
                    client.createDocument(client.{0}, {{ id: 'testDoc4', a : test.a, b : test.b}}, {{}}, function(err, docCreated, options) {{ 
                       if(err) throw new Error('Error while creating document: ' + err.message); 
                       else {{
                         getContext().getResponse().setBody(JSON.stringify(docCreated));  
                         if(options.maxCollectionSizeInMb == 0) throw 'max collection size not found'; 
                         getContext().getResponse().appendBody(JSON.stringify(options));  
                        }}
                    }}); }}", useNameRouting ? "getAltLink()" : "getSelfLink()")
            };
            retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection, storedProcedure3).Result;
            result = GetStoredProcedureExecutionResult<string>(client, retrievedStoredProcedure, JsonConvert.DeserializeObject("{ \"a\" : 4, \"b\" : \"xyz\"}"));
            Assert.IsTrue(result.Contains("\"a\":4,\"b\":\"xyz\""));
            this.AssertCollectionMaxSizeFromScriptResult(result);
            Assert.IsTrue(result.Contains("\"currentCollectionSizeInMB\":\"documentSize=0;"));
            client.Delete<StoredProcedure>(retrievedStoredProcedure.GetIdOrFullName());

            // Stored procedure that doesn't return anything (does not add anything to response body).
            StoredProcedure storedProcedure4 = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid().ToString(),
                Body = "function(){}"
            };
            retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection, storedProcedure4).Result;
            result = GetStoredProcedureExecutionResult<string>(client, retrievedStoredProcedure, JsonConvert.DeserializeObject("{}"));
            Assert.AreEqual("", result);
            client.Delete<StoredProcedure>(retrievedStoredProcedure.GetIdOrFullName());

            // failure test - see what happens if we try to execute a resource other than a sproc
            try
            {
                string ignored = client.ExecuteStoredProcedureAsync<string>(retrievedStoredProcedure).Result;
                Assert.Fail("Should not succeed.");
            }
            catch (Exception ex)
            {
                Assert.IsNotNull(ex);
                if (!useGateway)
                {
                    // Resource NOT Found for name based
                    Assert.IsTrue(ex.InnerException.Message.Contains("One of the specified inputs is invalid")
                        || ex.InnerException.Message.Contains("Resource Not Found"));
                }
                else
                {
                    Assert.IsTrue(ex.InnerException.Message.Contains("Server could not parse the Url.")
                        || ex.InnerException.Message.Contains("Resource Not Found"));

                }
            }

            // failure test - stored procedure without body
            StoredProcedure storedProcNoBody = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid(),
            };
            try
            {
                StoredProcedure retrievedStoredProcedureNoBody = client.CreateStoredProcedureAsync(collection, storedProcNoBody).Result;
            }
            catch (Exception ex)
            {
                Assert.IsNotNull(ex);
                Assert.IsNotNull(ex.InnerException);
                Assert.IsTrue(ex.InnerException.Message.Contains("The input content is invalid because the required properties - 'body; ' - are missing"));
            }

            // failure test - stored procedure with empty body
            StoredProcedure storedProcEmptyBody = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid(),
                Body = @""
            };
            try
            {
                StoredProcedure retrievedStoredProcedureEmptyBody = client.CreateStoredProcedureAsync(collection, storedProcEmptyBody).Result;
            }
            catch (Exception ex)
            {
                Assert.IsNotNull(ex);
                Assert.IsNotNull(ex.InnerException);
                Assert.IsTrue(ex.InnerException.Message.Contains("Encountered exception while compiling Javascript."));
            }
#endregion
        }
        */

        [TestMethod]
        public async Task ValidateReadOnlyStoredProcedureExecution()
        {
            await this.ValidateReadOnlyStoredProcedureExecutionInternal(true);
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await ValidateReadOnlyStoredProcedureExecutionInternal(false, Protocol.Tcp);
            await ValidateReadOnlyStoredProcedureExecutionInternal(false, Protocol.Https);
#endif
        }

        internal async Task ValidateReadOnlyStoredProcedureExecutionInternal(bool useGateway, Protocol protocol = Protocol.Https)
        {
            DocumentClient masterClient = TestCommon.CreateClient(useGateway, protocol);

            List<DocumentClient> lockedClients = null;
            if (useGateway)
            {
                lockedClients = new List<DocumentClient> { masterClient };
            }
            else
            {
                // Creates individual client instances locked to separate replicas.
                lockedClients = ReplicationTests.GetClientsLocked(useGateway, protocol).ToList();
            }

            for (int index = 0; index < Math.Min(2, lockedClients.Count); index++)
            {
                DocumentClient lockedClient = lockedClients[index];

                await TestCommon.DeleteAllDatabasesAsync();

                Documents.Database database = TestCommon.CreateOrGetDatabase(masterClient);

                DocumentCollection collection = await TestCommon.CreateCollectionAsync(masterClient,
                    database,
                    new DocumentCollection
                    {
                        Id = "ValidateReadOnlySprocExecution" + Guid.NewGuid().ToString(),
                        PartitionKey = new PartitionKeyDefinition
                        {
                            Paths = new Collection<string> { "/partitionKey" }
                        }
                    },
                    new Documents.Client.RequestOptions { OfferThroughput = 12000 }); // Ensuring read-only scripts work on partitioned collecitons.

                // Create documents in the collection.
                List<DocumentWithPK> docBatch = new List<DocumentWithPK>();
                for (int i = 0; i < 10; ++i)
                {
                    await masterClient.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(database.Id, collection.Id),
                        new DocumentWithPK("doc_" + i, "0"));
                }

                string scriptBody = @"function() {
                var client = getContext().getCollection();
                function callback(err, docFeed, responseOptions) {
                    if(err) throw 'Error while reading document';
                    docFeed.forEach(function(doc, i, arr) { getContext().getResponse().appendBody(JSON.stringify(doc)); });
                    if(responseOptions.continuation) { client.readDocuments(client.getSelfLink(), { pageSize : 1, continuation : responseOptions.continuation }, callback); }
                };
                client.readDocuments(client.getSelfLink(), { }, callback);
                }";

                StoredProcedure sproc =
                    await masterClient.CreateStoredProcedureAsync(UriFactory.CreateDocumentCollectionUri(database.Id, collection.Id),
                        new StoredProcedure
                        {
                            Id = "ReadOnlySproc",
                            Body = scriptBody
                        });

                // Execute read-only stored procedure and verify all created documents are read.
                string result = await lockedClient.ExecuteStoredProcedureAsync<string>(sproc.SelfLink,
                    new Documents.Client.RequestOptions { PartitionKey = new Documents.PartitionKey("0") });

                for (int i = 0; i < 10; ++i)
                {
                    Assert.IsTrue(result.Contains("doc_" + i));
                }

                await masterClient.DeleteStoredProcedureAsync(UriFactory.CreateStoredProcedureUri(database.Id, collection.Id, sproc.Id));
            }
        }

        [TestMethod]
        public void ValidateStoredProceduresBlacklisting()
        {
            try
            {
                TestCommon.SetDoubleConfigurationProperty("StoredProcedureMaximumChargeInSeconds", 0.0);
                TestCommon.WaitForConfigRefresh();
                this.ValidateStoredProceduresBlacklistingInternal();
            }
            finally
            {
                TestCommon.SetDoubleConfigurationProperty("StoredProcedureMaximumChargeInSeconds", 2.0);
                TestCommon.WaitForConfigRefresh();
            }
        }

        public void ValidateStoredProceduresBlacklistingInternal()
        {
            DocumentClient client = TestCommon.CreateClient(true);

            Documents.Database database = TestCommon.CreateOrGetDatabase(client);
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
            DocumentCollection inputCollection = new DocumentCollection { Id = "ValidateStoredProceduresBlacklisting" + Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition };
            DocumentCollection collection = TestCommon.CreateCollectionAsync(client, database, inputCollection).Result;

            string badScript = @"function() { 
                var start = new Date();
                var end = start.getTime() + 4000; // 4000 msec
                while(true) {
                    var cur = new Date();
                    if (cur.getTime() > end) break;
                }
            }";
            StoredProcedure storedProcedure = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid().ToString(),
                Body = badScript
            };
            StoredProcedure retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection, storedProcedure).Result;
            Documents.Client.RequestOptions requestOptions = new Documents.Client.RequestOptions
            {
                PartitionKey = new Documents.PartitionKey("test")
            };
            for (int numExec = 0; numExec < 3; numExec++)
            {
                client.ExecuteStoredProcedureAsync<string>(retrievedStoredProcedure, requestOptions).Wait();
            }

            bool isBlacklisted = false;
            try
            {
                // 3 strikes and then out
                client.ExecuteStoredProcedureAsync<string>(retrievedStoredProcedure, requestOptions).Wait();
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
                DocumentClientException de = e.InnerException as DocumentClientException;
                Assert.IsNotNull(de);
                Assert.AreEqual(HttpStatusCode.Forbidden.ToString(), de.Error.Code);
                Assert.IsTrue(de.Message.Contains("is blocked for execution because it has violated its allowed resource limit several times."));
                isBlacklisted = true;
            }

            Assert.IsTrue(isBlacklisted);
        }

        [TestMethod]
        public void ValidateUserDefinedFunctions()
        {
            DocumentClient client = TestCommon.CreateClient(true);

            DocumentClient secondary1Client = TestCommon.CreateClient(false, Protocol.Tcp);
            secondary1Client.LockClient(1);

            DocumentClient secondary2Client = TestCommon.CreateClient(false, Protocol.Tcp);
            secondary1Client.LockClient(2);

            Documents.Database database = TestCommon.CreateOrGetDatabase(client);
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
            DocumentCollection inputCollection = new DocumentCollection { Id = "ValidateUserDefinedFunctions" + Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition };
            inputCollection.IndexingPolicy.IndexingMode = Documents.IndexingMode.Consistent;
            DocumentCollection collection = client.Create(database.ResourceId, inputCollection);

            // 1. UDF input
            Document queryDocument1 = new Document { Id = "Romulan" };
            queryDocument1.SetPropertyValue("pk", "test");
            Document retrievedDocument = client.Create(collection.ResourceId, queryDocument1);

            UserDefinedFunction udf1 = new UserDefinedFunction
            {
                Id = "udf1",
                Body = @"function(label, testLabel) { 
                            if(label.toLowerCase() == testLabel.toLowerCase()) return true;
                        };",
            };
            UserDefinedFunction retrievedUdf = client.CreateUserDefinedFunctionAsync(collection.UserDefinedFunctionsLink, udf1).Result;

            this.Retry(() =>
            {
                IDocumentQuery<dynamic> docServiceQuery = secondary1Client.CreateDocumentQuery(collection.DocumentsLink,
                    @"select * from root r where udf.udf1(r.id, ""Romulan"") = true", new FeedOptions { EnableCrossPartitionQuery = true }).AsDocumentQuery();

                DocumentFeedResponse<dynamic> docCollection = docServiceQuery.ExecuteNextAsync().Result;

                Logger.LogLine("Documents queried with token: {0}", docCollection.SessionToken);

                Assert.IsNotNull(docCollection, "Query result is null");
                Assert.AreNotEqual(0, docCollection.Count, "Collection count dont match");

                foreach (dynamic queryDocument in docCollection)
                {
                    Assert.AreEqual("Romulan", queryDocument["id"].Value, "Document Name dont match");
                }
            });

            // failure tests

            // failure test - UDF without body
            UserDefinedFunction udfNoBody = new UserDefinedFunction
            {
                Id = "udfNoBody"
            };
            try
            {
                UserDefinedFunction retrievedUdfNoBody = client.CreateUserDefinedFunctionAsync(collection.UserDefinedFunctionsLink, udfNoBody).Result;
            }
            catch (Exception ex)
            {
                Assert.IsNotNull(ex);
                Assert.IsNotNull(ex.InnerException);
                Assert.IsTrue(ex.InnerException.Message.Contains("The input content is invalid because the required properties - 'body; ' - are missing"));
            }

            // failure test - UDF that throws specific error number.
            UserDefinedFunction udfThatThrows = new UserDefinedFunction
            {
                Id = "udfThatThrows",
                Body = @"function() { throw new Error(32766, 'Error'); };",
            };
            retrievedUdf = client.CreateUserDefinedFunctionAsync(collection.UserDefinedFunctionsLink, udfThatThrows).Result;

            {
                IDocumentQuery<dynamic> docServiceQuery = secondary1Client.CreateDocumentQuery(collection.DocumentsLink,
                    @"select * from root r where udf.udfThatThrows() = true", new FeedOptions { EnableCrossPartitionQuery = true }).AsDocumentQuery();

                try
                {
                    DocumentFeedResponse<dynamic> docCollection = docServiceQuery.ExecuteNextAsync().Result;
                    Assert.Fail("Should throw and not get here");
                }
                catch (AggregateException ex)
                {
                    Assert.AreEqual(HttpStatusCode.BadRequest, ((DocumentClientException)ex.InnerException).StatusCode);
                    Assert.AreEqual(32766, (int)((DocumentClientException)ex.InnerException).GetSubStatus());
                    Assert.IsFalse(string.IsNullOrEmpty(ex.Message));
                }
            }

            //            // precompilation should catch errors on create
            //            UserDefinedFunction udfSyntaxError = new UserDefinedFunction
            //            {
            //                Id = "udf" + Guid.NewGuid().ToString(),
            //                Body = @"
            //                    method() { // method is invalid identifier
            //                        for(var i = 0; i < 10; i++) getContext().getResponse().appendValue('Body', i);
            //                    }"
            //            };

            //            try
            //            {
            //                UserDefinedFunction failureUdf = client.CreateUserDefinedFunctionAsync(collection, udfSyntaxError).Result;
            //                Assert.Fail("Script with syntax error should have failed when being stored");
            //            }
            //            catch (AggregateException e)
            //            {
            //                Assert.IsNotNull(e.InnerException);
            //                Assert.IsInstanceOfType(e.InnerException, typeof(DocumentClientException));
            //                TestCommon.AssertException((DocumentClientException)e.InnerException, HttpStatusCode.BadRequest);
            //                Assert.IsTrue(e.InnerException.Message.Contains("Encountered exception while compiling Javascript."));
            //            }
        }

        [TestMethod]
        public void ValidateUserDefinedFunctionsBlacklisting()
        {
            try
            {
                TestCommon.SetDoubleConfigurationProperty("UdfMaximumChargeInSeconds", 0.0);
                TestCommon.WaitForConfigRefresh();
                this.ValidateUserDefinedFunctionsBlacklistingInternal();
            }
            finally
            {
                TestCommon.SetDoubleConfigurationProperty("UdfMaximumChargeInSeconds", 0.1);
                TestCommon.WaitForConfigRefresh();
            }
        }

        public void ValidateUserDefinedFunctionsBlacklistingInternal()
        {
            DocumentClient client = TestCommon.CreateClient(true);

            Documents.Database database = TestCommon.CreateOrGetDatabase(client);
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
            DocumentCollection inputCollection = new DocumentCollection { Id = "ValidateUserDefinedFunctionsBlacklisting" + Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition };
            DocumentCollection collection = client.Create(database.ResourceId, inputCollection);

            UserDefinedFunction udfSpec = new UserDefinedFunction
            {
                Id = "badUdf",
                Body = @"function(name) { var start = new Date();
                    var end = start.getTime() + 4000; // 4000 msec
                    while(true) {
                        var cur = new Date();
                        if (cur.getTime() > end) break;
        }
                    return name;
                }"
            };
            UserDefinedFunction udf = client.CreateUserDefinedFunctionAsync(collection, udfSpec).Result;

            DocumentClient secondaryClient = TestCommon.CreateClient(false);
            secondaryClient.LockClient(1); // lock so we can get reliable blacklistling

            for (int i = 0; i < 10; i++)
            {
                IDocumentQuery<dynamic> docQuery = secondaryClient.CreateDocumentQuery(collection.DocumentsLink,
                "select udf.badUdf(r.id) from root r", new FeedOptions { EnableCrossPartitionQuery = true }).AsDocumentQuery();

                // with 0 docs, UDF shouldn't be blacklisted
                DocumentFeedResponse<dynamic> docCollection = docQuery.ExecuteNextAsync().Result;
                Assert.AreEqual(0, docCollection.Count);
            }

            // create one doc and try again
            client.CreateDocumentAsync(collection, new Document() { Id = "newdoc1" }).Wait();
            for (int i = 0; i < 3; i++)
            {
                IDocumentQuery<dynamic> docQuery2 = secondaryClient.CreateDocumentQuery(collection.DocumentsLink,
                    "select udf.badUdf(r.id) from root r", new FeedOptions { EnableCrossPartitionQuery = true }).AsDocumentQuery();

                DocumentFeedResponse<dynamic> docCollection2 = docQuery2.ExecuteNextAsync().Result;
            }

            IDocumentQuery<dynamic> docQuery2BlackListed = secondaryClient.CreateDocumentQuery(collection.DocumentsLink,
                "select udf.badUdf(r.id) from root r", new FeedOptions { EnableCrossPartitionQuery = true }).AsDocumentQuery();

            bool isBlacklisted = false;
            try
            {
                DocumentFeedResponse<dynamic> docCollection2 = docQuery2BlackListed.ExecuteNextAsync().Result;
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
                DocumentClientException de = e.InnerException as DocumentClientException;
                Assert.IsNotNull(de);
                Assert.AreEqual(HttpStatusCode.Forbidden.ToString(), de.Error.Code);
                Assert.IsTrue(de.Message.Contains("is blocked for execution because it has violated its allowed resources limit several times."));
                isBlacklisted = true;
            }
            Assert.IsTrue(isBlacklisted);

            // Test on new collection with correct throughput - good UDF shouldn't be blacklisted
            TestCommon.SetDoubleConfigurationProperty("UdfMaximumChargeInSeconds", 0.1);
            TestCommon.WaitForConfigRefresh();

            DocumentCollection inputCollection2 = new DocumentCollection { Id = "ValidateUserDefinedFunctionsBlacklisting" + Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition };
            DocumentCollection collection2 = client.Create(database.ResourceId, inputCollection2);

            // create lots of documents
            for (int i = 0; i < 1000; i++)
            {
                client.CreateDocumentAsync(collection2, new Document() { Id = "newdoc" + Guid.NewGuid().ToString() }).Wait();
            }

            UserDefinedFunction udfSpec2 = new UserDefinedFunction
            {
                Id = "goodUdf",
                Body = "function(name) { return name; }"
            };
            UserDefinedFunction udf2 = client.CreateUserDefinedFunctionAsync(collection2, udfSpec2).Result;

            IDocumentQuery<dynamic> docQuery3 = secondaryClient.CreateDocumentQuery(collection2.DocumentsLink,
                "select udf.goodUdf(r.id) from root r", new FeedOptions { MaxItemCount = 1000, EnableCrossPartitionQuery = true }).AsDocumentQuery();

            DocumentFeedResponse<dynamic> docCollection3 = docQuery3.ExecuteNextAsync().Result;
            Assert.AreEqual(1000, docCollection3.Count);
        }

        //ReadPartitionKeyRangeFeedAsync method not expose in V3
        [TestMethod]
        public async Task ValidateChangeFeedIfNoneMatch()
        {
            await this.ValidateChangeFeedIfNoneMatchHelper(true);
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await ValidateChangeFeedIfNoneMatchHelper(false, Protocol.Https);
            await ValidateChangeFeedIfNoneMatchHelper(false, Protocol.Tcp);
#endif
        }

        private async Task ValidateChangeFeedIfNoneMatchHelper(bool useGateway, Protocol protocol = Protocol.Tcp)
        {
            DocumentClient client = TestCommon.CreateClient(useGateway, protocol);
            ResourceResponse<Documents.Database> db = await client.CreateDatabaseAsync(new Documents.Database() { Id = Guid.NewGuid().ToString() });
            try
            {
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
                DocumentCollection coll = await TestCommon.CreateCollectionAsync(client, db, new DocumentCollection() { Id = Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition });
                string pkRangeId = (await client.ReadPartitionKeyRangeFeedAsync(coll.AltLink)).FirstOrDefault().Id;
                DocumentFeedResponse<Document> response1 = null;

                // Read change feed from current.
                using (IDocumentQuery<Document> query1 = client.CreateDocumentChangeFeedQuery(coll, new ChangeFeedOptions { PartitionKeyRangeId = pkRangeId }))
                {
                    response1 = await query1.ExecuteNextAsync<Document>();
                    Assert.AreEqual(0, response1.Count);
                    Assert.IsFalse(query1.HasMoreResults);
                    Assert.IsFalse(string.IsNullOrEmpty(response1.ResponseContinuation));
                }

                // Read change feed from beginning.
                using (IDocumentQuery<Document> query1 = client.CreateDocumentChangeFeedQuery(coll, new ChangeFeedOptions { PartitionKeyRangeId = pkRangeId, StartFromBeginning = true }))
                {
                    response1 = await query1.ExecuteNextAsync<Document>();
                    Assert.AreEqual(0, response1.Count);
                    Assert.IsFalse(query1.HasMoreResults);
                    Assert.IsFalse(string.IsNullOrEmpty(response1.ResponseContinuation));
                }

                // Read empty feed using dynamic binding.
                using (IDocumentQuery<Document> query1 = client.CreateDocumentChangeFeedQuery(coll, new ChangeFeedOptions { PartitionKeyRangeId = pkRangeId, StartFromBeginning = true }))
                {
                    DocumentFeedResponse<dynamic> response3 = await query1.ExecuteNextAsync();
                    Assert.AreEqual(0, response3.Count);
                    Assert.IsFalse(query1.HasMoreResults);
                    Assert.IsFalse(string.IsNullOrEmpty(response3.ResponseContinuation));
                }

                ResourceResponse<Document> docResult1 = await client.CreateDocumentAsync(coll, new Document() { Id = "doc1" });

                // Call to get an etag
                string continuationAfterFirstDoc = null;
                using (IDocumentQuery<Document> query2 = client.CreateDocumentChangeFeedQuery(coll, new ChangeFeedOptions() { PartitionKeyRangeId = pkRangeId, StartFromBeginning = true }))
                {
                    DocumentFeedResponse<Document> response2 = await query2.ExecuteNextAsync<Document>();
                    Assert.AreNotEqual(response1.ResponseContinuation, response2.ResponseContinuation);
                    Assert.IsNotNull(response2.FirstOrDefault());
                    Assert.AreEqual("doc1", response2.FirstOrDefault().Id);
                    Assert.AreEqual(1, response2.Count);
                    continuationAfterFirstDoc = response2.ResponseContinuation;
                }

                ResourceResponse<Document> docResult2 = await client.CreateDocumentAsync(coll, new Document() { Id = "doc2" });
                ResourceResponse<Document> docResult3 = await client.CreateDocumentAsync(coll, new Document() { Id = "doc3" });

                // Read change feed from doc1 and validate continuation.
                ChangeFeedOptions options = new ChangeFeedOptions()
                {
                    PartitionKeyRangeId = pkRangeId,
                    MaxItemCount = 1,
                    RequestContinuation = continuationAfterFirstDoc
                };

                const string lsnPropertyName = "_lsn";
                string accumulator = string.Empty;
                using (IDocumentQuery<Document> query1 = client.CreateDocumentChangeFeedQuery(coll, options))
                {
                    while (query1.HasMoreResults)   // both while-do and do-while should work. Use both patterns in different tests.
                    {
                        response1 = await query1.ExecuteNextAsync<Document>();
                        foreach (Document doc in response1)
                        {
                            Assert.AreNotEqual(default(int), doc.GetPropertyValue<int>(lsnPropertyName));
                            accumulator += doc.Id + ".";
                        }
                        Assert.IsNotNull(response1.ResponseContinuation);
                    }
                    Assert.AreEqual("doc2.doc3.", accumulator);
                }

                // Try to read change feed to the end in one shot.
                options.MaxItemCount = 100;
                options.RequestContinuation = continuationAfterFirstDoc;
                accumulator = string.Empty;

                using (IDocumentQuery<Document> query2 = client.CreateDocumentChangeFeedQuery(coll, options))
                {
                    DocumentFeedResponse<Document> response2 = null;
                    while (query2.HasMoreResults)
                    {
                        response2 = await query2.ExecuteNextAsync<Document>();
                        foreach (Document doc in response2)
                        {
                            Assert.AreNotEqual(default(int), doc.GetPropertyValue<int>(lsnPropertyName));
                            accumulator += doc.Id + ".";
                        }
                    }
                    Assert.IsNotNull(response2.ResponseContinuation);
                    Assert.AreEqual("doc2.doc3.", accumulator);

                    // Since we bump LSN to max(LSN from ChangeFeed, request LSN), for 2nd request, the LSN can be equal or greater.
                    if (response1.ResponseContinuation != response2.ResponseContinuation)
                    {
                        int lsn1 = int.Parse(response1.ResponseContinuation.Trim('"'));
                        int lsn2 = int.Parse(response2.ResponseContinuation.Trim('"'));
                        Assert.IsTrue(lsn1 <= lsn2);
                    }
                }

                // Read all one-by-one using dynamic binding.
                options = new ChangeFeedOptions()
                {
                    PartitionKeyRangeId = pkRangeId,
                    MaxItemCount = 1,
                    StartFromBeginning = true
                };
                using (IDocumentQuery<Document> query3 = client.CreateDocumentChangeFeedQuery(coll, options))
                {
                    accumulator = string.Empty;
                    do
                    {
                        DocumentFeedResponse<dynamic> response3 = await query3.ExecuteNextAsync();
                        foreach (dynamic doc in response3)
                        {
                            Assert.AreNotEqual(default(int), doc._lsn);
                            accumulator += doc.id + ".";    // Note that here we are using 'id' instead of 'Id'.
                        }
                        Assert.IsNotNull(response3.ResponseContinuation);
                    } while (query3.HasMoreResults);
                    Assert.AreEqual("doc1.doc2.doc3.", accumulator);
                }
            }
            finally
            {
                client.DeleteDatabaseAsync(db).Wait();
            }
        }

        //ReadPartitionKeyRangeFeedAsync method not expose in V3
        [TestMethod]
        public async Task ValidateChangeFeedIfModifiedSince()
        {
            await this.ValidateChangeFeedIfModifiedSinceHelper(true);
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await ValidateChangeFeedIfModifiedSinceHelper(false, Protocol.Https);
            await ValidateChangeFeedIfModifiedSinceHelper(false, Protocol.Tcp);
#endif
        }

        private async Task ValidateChangeFeedIfModifiedSinceHelper(bool useGateway, Protocol protocol = Protocol.Tcp)
        {
            DocumentClient client = TestCommon.CreateClient(useGateway, protocol);
            ResourceResponse<Documents.Database> db = await client.CreateDatabaseAsync(new Documents.Database() { Id = Guid.NewGuid().ToString() });
            try
            {
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
                DocumentCollection coll = await client.CreateDocumentCollectionAsync(db, new DocumentCollection() { Id = Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition });
                string pkRangeId = (await client.ReadPartitionKeyRangeFeedAsync(coll.AltLink)).FirstOrDefault().Id;
                DocumentFeedResponse<Document> response1 = null;
                ChangeFeedOptions options = new ChangeFeedOptions { PartitionKeyRangeId = pkRangeId };

                // 1. Read empty feed -- TS in the past.
                options.StartTime = DateTime.Now - TimeSpan.FromDays(100);
                string accumulator = await this.ReadChangeFeedToEnd(client, coll.SelfLink, options);
                Assert.AreEqual(string.Empty, accumulator);

                // 2. Read emty feed -- TS in the future.
                options.StartTime = DateTime.Now + TimeSpan.FromDays(100);
                accumulator = await this.ReadChangeFeedToEnd(client, coll.SelfLink, options);
                Assert.AreEqual(string.Empty, accumulator);

                // Create documents.
                ResourceResponse<Document> docResult1 = await client.CreateDocumentAsync(coll, new Document() { Id = "doc1" });
                await Task.Delay(TimeSpan.FromSeconds(2));  // Timestamp precision is 1 sec.
                ResourceResponse<Document> docResult2 = await client.CreateDocumentAsync(coll, new Document() { Id = "doc2" });

                // Call to get etags.
                const string lsnPropertyName = "_lsn";
                List<int> lsns = new List<int>();
                List<DateTime> timestamps = new List<DateTime>();

                using (IDocumentQuery<Document> query1 = client.CreateDocumentChangeFeedQuery(coll, new ChangeFeedOptions() { PartitionKeyRangeId = pkRangeId, StartFromBeginning = true }))
                {
                    while (query1.HasMoreResults)
                    {
                        response1 = await query1.ExecuteNextAsync<Document>();
                        foreach (Document doc in response1)
                        {
                            lsns.Add(doc.GetPropertyValue<int>(lsnPropertyName));
                            timestamps.Add(doc.Timestamp);
                            accumulator += doc.Id + ".";
                        }
                    }
                    Assert.AreEqual("doc1.doc2.", accumulator);
                }

                // 3. Read feed again with TS in the future.
                options.StartTime = DateTime.Now + TimeSpan.FromMinutes(100);
                using (IDocumentQuery<Document> query1 = client.CreateDocumentChangeFeedQuery(coll, options))
                {
                    response1 = await query1.ExecuteNextAsync<Document>();
                    Assert.AreEqual(0, response1.Count);
                    Assert.IsFalse(query1.HasMoreResults);
                    Assert.IsFalse(string.IsNullOrEmpty(response1.ResponseContinuation));
                }

                // 3. Read with TS before doc1.
                options.StartTime = timestamps[0] - TimeSpan.FromMinutes(1);
                accumulator = await this.ReadChangeFeedToEnd(client, coll.SelfLink, options);
                Assert.AreEqual("doc1.doc2.", accumulator);

                // 4. Read with TS = doc1.
                options.StartTime = timestamps[0];
                accumulator = await this.ReadChangeFeedToEnd(client, coll.SelfLink, options);
                Assert.AreEqual("doc2.", accumulator);

                // 5. Read with TS in between doc1 and doc2. Also validate StartFromBeginning = true/false is ignored.
                options.StartTime = new DateTime((timestamps[0].ToUniversalTime().Ticks + timestamps[1].ToUniversalTime().Ticks) / 2, DateTimeKind.Utc);
                bool[] startFromBeginningValues = { true, false };
                foreach (bool startFromBeginning in startFromBeginningValues)
                {
                    options.StartFromBeginning = startFromBeginning;
                    accumulator = await this.ReadChangeFeedToEnd(client, coll.SelfLink, options);
                    Assert.AreEqual("doc2.", accumulator);
                }

                // 6. Read with TS = doc2.
                options.StartTime = timestamps[1];
                using (IDocumentQuery<Document> query1 = client.CreateDocumentChangeFeedQuery(coll, options))
                {
                    response1 = await query1.ExecuteNextAsync<Document>();
                    Assert.AreEqual(0, response1.Count);
                    Assert.IsFalse(query1.HasMoreResults);
                    Assert.IsFalse(string.IsNullOrEmpty(response1.ResponseContinuation));
                }

                // 7. Read with TS later than doc2.
                options.StartTime = timestamps[1] + TimeSpan.FromSeconds(1);
                accumulator = await this.ReadChangeFeedToEnd(client, coll.SelfLink, options);
                Assert.AreEqual(string.Empty, accumulator);

                // 8. If-None-Match wins.
                options.StartTime = timestamps[1];
                options.RequestContinuation = lsns[0].ToString();
                accumulator = await this.ReadChangeFeedToEnd(client, coll.SelfLink, options);
                Assert.AreEqual("doc2.", accumulator);
            }
            finally
            {
                client.DeleteDatabaseAsync(db).Wait();
            }
        }

        [TestMethod]
        public async Task ValidateChangeFeedWithPartitionKey()
        {
            await this.ValidateChangeFeedWithPartitionKeyHelper(true);
            await this.ValidateChangeFeedWithPartitionKeyHelper(false, Protocol.Https);
            await this.ValidateChangeFeedWithPartitionKeyHelper(false, Protocol.Tcp);
        }

        private class DocumentWithPK : Document
        {
            public DocumentWithPK(string id, string pk)
            {
                this.Id = id;
                this.PartitionKey = pk;
            }

            [JsonProperty(PartitionKeyPropertyName)]
            public string PartitionKey { get; set; }

            public const string PartitionKeyPropertyName = "partitionKey";
        }

        private async Task<int> GetPKRangeIdForPartitionKey(
            DocumentClient client,
            string databaseId,
            string collectionId,
            PartitionKeyDefinition pkDefinition, string pkValue)
        {
            DocumentFeedResponse<PartitionKeyRange> pkRanges = await client.ReadPartitionKeyRangeFeedAsync(
                UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));
            List<string> maxExclusiveBoundaries = pkRanges.Select(pkRange => pkRange.MaxExclusive).ToList();

            string effectivePK1 = PartitionKeyInternal.FromJsonString(string.Format("['{0}']", pkValue)).GetEffectivePartitionKeyString(pkDefinition);
            int pkIndex = 0;
            while (pkIndex < maxExclusiveBoundaries.Count && string.Compare(effectivePK1, maxExclusiveBoundaries[pkIndex]) >= 0)
            {
                ++pkIndex;
            }

            if (pkIndex == maxExclusiveBoundaries.Count)
            {
                throw new Exception("Failed to find the range");
            }

            return pkIndex;
        }

        private async Task ValidateChangeFeedWithPartitionKeyHelper(bool useGateway, Protocol protocol = Protocol.Tcp)
        {
            DocumentClient client = TestCommon.CreateClient(useGateway, protocol);
            Documents.Database db = await client.CreateDatabaseAsync(new Documents.Database() { Id = Guid.NewGuid().ToString() });
            string pk1 = "4", pk2 = "6", pk3 = "22";    // The values are chosen in such a way that hash lands on the same range.

            try
            {
                DocumentCollection coll = await client.CreateDocumentCollectionAsync(
                    db,
                    new DocumentCollection()
                    {
                        Id = Guid.NewGuid().ToString(),
                        PartitionKey = new PartitionKeyDefinition { Paths = new Collection<string> { "/" + DocumentWithPK.PartitionKeyPropertyName } },
                    },
                    new Documents.Client.RequestOptions { OfferThroughput = 15000 });

                int pkRangeId = await this.GetPKRangeIdForPartitionKey(client, db.Id, coll.Id, coll.PartitionKey, pk1);
                Assert.AreEqual(pkRangeId, await this.GetPKRangeIdForPartitionKey(client, db.Id, coll.Id, coll.PartitionKey, pk2));

                ChangeFeedOptions options = new ChangeFeedOptions { StartFromBeginning = true, PartitionKeyRangeId = pkRangeId.ToString() };
                ChangeFeedOptions options1 = new ChangeFeedOptions { PartitionKey = new Documents.PartitionKey(pk1), StartFromBeginning = true };

                // 1. Read empty feed, PK = PK1.
                string accumulator = await this.ReadChangeFeedToEnd(client, coll.SelfLink, options);
                Assert.AreEqual(string.Empty, accumulator);

                // Create documents.
                List<DocumentWithPK> docs = new List<DocumentWithPK>
                {
                    new DocumentWithPK("doc1_1", pk1),    // These are created as one transaction per doc.
                    new DocumentWithPK("doc1_2", pk2),
                    new DocumentWithPK("doc1_3", pk1),
                    new DocumentWithPK("doc1_4", pk1),
                    new DocumentWithPK("doc2_1", pk2),    // These are created as one transaction per all 4 docs.
                    new DocumentWithPK("doc2_2", pk1),
                    new DocumentWithPK("doc2_3", pk2),
                    new DocumentWithPK("doc2_4", pk2)
                };

                List<DocumentWithPK> docBatch = new List<DocumentWithPK>();
                for (int i = 0; i < docs.Count / 2; ++i)
                {
                    await client.CreateDocumentAsync(coll, docs[i]);
                    docBatch.Add(docs[(docs.Count / 2) + i]);
                }

                Uri bulkInsertSprocUri = UriFactory.CreateStoredProcedureUri(db.Id, coll.Id, "__.sys.commonBulkInsert");
                BulkInsertStoredProcedureOptions sprocOptions = new BulkInsertStoredProcedureOptions(true, true, null, false, false);

                StoredProcedureResponse<BulkInsertStoredProcedureResult> sprocResponse = await client.ExecuteStoredProcedureAsync<BulkInsertStoredProcedureResult>(
                    bulkInsertSprocUri.ToString(),
                    new Documents.Client.RequestOptions { PartitionKeyRangeId = pkRangeId.ToString() },
                    new dynamic[] { docBatch, sprocOptions });

                Assert.AreEqual(0, sprocResponse.Response.ErrorCode);
                Assert.AreEqual(4, sprocResponse.Response.Count);

                // 2. Read without PK filter.
                accumulator = await this.ReadChangeFeedToEnd(client, coll.SelfLink, options);
                Assert.AreEqual("doc1_1.doc1_2.doc1_3.doc1_4.doc2_1.doc2_2.doc2_3.doc2_4.", accumulator);

                // 3. Read with PK = pk1.
                accumulator = await this.ReadChangeFeedToEnd(client, coll.SelfLink, options1);
                Assert.AreEqual("doc1_1.doc1_3.doc1_4.doc2_2.", accumulator);

                // 4. Read with PK = pk2.
                options1.PartitionKey = new Documents.PartitionKey(pk2);
                accumulator = await this.ReadChangeFeedToEnd(client, coll.SelfLink, options1);
                Assert.AreEqual("doc1_2.doc2_1.doc2_3.doc2_4.", accumulator);

                // 5. Read with PK = neither PK1, nor PK2 but falls into same range.
                options1.PartitionKey = new Documents.PartitionKey(pk3);
                accumulator = await this.ReadChangeFeedToEnd(client, coll.SelfLink, options1);
                Assert.AreEqual(string.Empty, accumulator);

                // 6. Both PK and PKRange are provided.
                options1.PartitionKeyRangeId = pkRangeId.ToString();
                try
                {
                    client.CreateDocumentChangeFeedQuery(coll.SelfLink, options1);
                    Assert.Fail("Didn't throw when both pk and pk range id are specified.");
                }
                catch (ArgumentException)
                {
                }
                await Task.Delay(2000);
            }
            finally
            {
                client.DeleteDatabaseAsync(db).Wait();
            }
        }

        [TestMethod]
        public void ValidateSettingChangeFeedOptionsStartTime()
        {
            ChangeFeedOptions options = new ChangeFeedOptions();

            DateTime dtNow = DateTime.Now;
            DateTime dtLocal = new DateTime(2017, 06, 14, 11, 23, 00, DateTimeKind.Local);
            DateTime dtUtc = new DateTime(2017, 06, 14, 19, 23, 00, DateTimeKind.Utc);

            foreach (DateTime? value in new DateTime?[] { null, dtNow, dtLocal, dtUtc })
            {
                options.StartTime = value;
                Assert.AreEqual(value, options.StartTime);
            }

            try
            {
                options.StartTime = new DateTime(1, DateTimeKind.Unspecified);
                Assert.Fail("Expected a throw but did not get it.");
            }
            catch (ArgumentException)
            {
            }

            try
            {
                options = new ChangeFeedOptions { StartTime = new DateTime(1, DateTimeKind.Unspecified) };
                Assert.Fail("Expected a throw but did not get it.");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        public async Task ValidateReadPartitionKeyRange()
        {
            await this.ValidateReadPartitionKeyRangeHelper(true);
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            await ValidateReadPartitionKeyRangeHelper(false, Protocol.Https);
            await ValidateReadPartitionKeyRangeHelper(false, Protocol.Tcp);
#endif
        }

        private async Task ValidateReadPartitionKeyRangeHelper(bool useGateway, Protocol protocol = Protocol.Tcp)
        {
            DocumentClient client = TestCommon.CreateClient(useGateway, protocol);
            Documents.Database db = await client.CreateDatabaseAsync(new Documents.Database() { Id = Guid.NewGuid().ToString() });
            try
            {
                DocumentCollection collSpec = new DocumentCollection() { Id = Guid.NewGuid().ToString() };
                collSpec.PartitionKey.Paths.Add("/id");
                DocumentCollection coll = await TestCommon.CreateCollectionAsync(client, db, collSpec, new Documents.Client.RequestOptions { OfferThroughput = 12000 });

                // Create a few docs.
                for (int i = 0; i < 10; ++i)
                {
                    await client.CreateDocumentAsync(coll, new Document { Id = Guid.NewGuid().ToString() });
                }

                string continuation = null;
                Func<FeedOptions> fnCreateFeedOptions = () => new FeedOptions { RequestContinuationToken = continuation };
                Task<DocumentFeedResponse<PartitionKeyRange>>[] tasks = new Task<DocumentFeedResponse<PartitionKeyRange>>[]
                {
                    client.ReadPartitionKeyRangeFeedAsync(coll.AltLink, fnCreateFeedOptions()),
                    client.ReadPartitionKeyRangeFeedAsync(coll.AltLink + "/pkranges", fnCreateFeedOptions()),
                    client.ReadPartitionKeyRangeFeedAsync(UriFactory.CreateDocumentCollectionUri(db.Id, coll.Id), fnCreateFeedOptions()),
                    client.ReadPartitionKeyRangeFeedAsync(UriFactory.CreatePartitionKeyRangesUri(db.Id, coll.Id), fnCreateFeedOptions()),
                    client.ReadPartitionKeyRangeFeedAsync(coll, fnCreateFeedOptions())
                };

                foreach (Task<DocumentFeedResponse<PartitionKeyRange>> task in tasks)
                {
                    continuation = null;
                    List<PartitionKeyRange> ranges = new List<PartitionKeyRange>();
                    do
                    {
                        DocumentFeedResponse<PartitionKeyRange> feedResponse = await task;
                        continuation = feedResponse.ResponseContinuation;
                        ranges.AddRange(feedResponse);
                    }
                    while (continuation != null);

                    Assert.IsTrue(ranges.Count > 1);
                }
            }
            finally
            {
                client.DeleteDatabaseAsync(db).Wait();
            }
        }

        [TestMethod]
        public async Task ValidateStoredProcedureExecutionWithPartitionKey()
        {
            DocumentClient client = TestCommon.CreateClient(true);

            await TestCommon.DeleteAllDatabasesAsync();
            Documents.Database database = await client.CreateDatabaseAsync(new Documents.Database { Id = "db" });

            DocumentCollection collection = await TestCommon.CreateCollectionAsync(client,
                database,
                new DocumentCollection
                {
                    Id = "mycoll",
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Paths = new Collection<string> { "/customerid" }
                    }
                },
                new Documents.Client.RequestOptions { OfferThroughput = 12000 });

            StoredProcedure sproc =
                client.CreateStoredProcedureAsync(UriFactory.CreateDocumentCollectionUri(database.Id, collection.Id),
                    new StoredProcedure
                    {
                        Id = "HelloWorld",
                        Body = @"function(name) { getContext().getResponse().setBody('Hello World, ' + name + '!'); }"
                    })
                    .Result;

            // Execute stored procedure by passing in stored procedure self-link
            string output = client.ExecuteStoredProcedureAsync<string>(sproc.SelfLink,
                new Documents.Client.RequestOptions { PartitionKey = new Documents.PartitionKey("1") }, "DocumentDB").Result;
            Assert.IsTrue(string.CompareOrdinal(output, "Hello World, DocumentDB!") == 0);

            // Execute stored procedure by passing in stored procedure URI
            output = client.ExecuteStoredProcedureAsync<string>(
                UriFactory.CreateStoredProcedureUri(database.Id, collection.Id, "HelloWorld"),
                new Documents.Client.RequestOptions { PartitionKey = new Documents.PartitionKey("1") }, "DocumentDB").Result;
            Assert.IsTrue(string.CompareOrdinal(output, "Hello World, DocumentDB!") == 0);

            client.DeleteStoredProcedureAsync(UriFactory.CreateStoredProcedureUri(database.Id, collection.Id, sproc.Id)).Wait();
        }

        [TestMethod]
        public void ValidateGenericReadDocumentGateway()
        {
            this.ValidateGenericReadDocument(true, Protocol.Https).Wait();
            this.ValidateGenericReadDocumentFromResource(true, Protocol.Https).Wait();
        }

        [TestMethod]
        public void ValidateGenericReadDocumentDirectTcp()
        {
            this.ValidateGenericReadDocument(false, Protocol.Tcp).Wait();
            this.ValidateGenericReadDocumentFromResource(false, Protocol.Tcp).Wait();
        }

        [TestMethod]
        public void ValidateGenericReadDocumentDirectHttps()
        {
            this.ValidateGenericReadDocument(false, Protocol.Https).Wait();
            this.ValidateGenericReadDocumentFromResource(false, Protocol.Tcp).Wait();
        }

        private async Task ValidateGenericReadDocument(bool useGateway, Protocol protocol)
        {
            CosmosClient client = TestCommon.CreateCosmosClient(useGateway);

            Cosmos.Database database = await client.CreateDatabaseIfNotExistsAsync(Guid.NewGuid().ToString());
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            Container collection = await database.CreateContainerAsync(new ContainerProperties() { Id = Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition });

            string guidId = Guid.NewGuid().ToString();
            CustomerPOCO poco = new CustomerPOCO()
            {
                id = guidId,
                pk = guidId,
                BookId = "isbn",
                PUBLISHTIME = DateTime.Now,
                authors = new List<string>()
            };
            poco.authors.Add("Mark Twain");

            ItemResponse<CustomerPOCO> doc = await collection.CreateItemAsync(poco);

            // This tests that the existing ReadDocumentAsync API works as expected, you can only access Document properties
            ItemResponse<CustomerPOCO> documentResponse = await collection.ReadItemAsync<CustomerPOCO>(partitionKey: new Cosmos.PartitionKey(poco.id), id: poco.id);
            Assert.AreEqual(documentResponse.StatusCode, HttpStatusCode.OK);
            Assert.IsNotNull(documentResponse.Resource);

            Assert.AreEqual(documentResponse.Resource.id, guidId);

            // This tests shows how you can extract the CustomerPOCO from ReadDocumentAsync API, to access POCO properties
            CustomerPOCO customerPOCO = (CustomerPOCO)(dynamic)documentResponse.Resource;
            Assert.AreEqual(customerPOCO.BookId, "isbn");

            // This tests the implicit operator for ReadDocumentAsync
            CustomerPOCO doc1 = await collection.ReadItemAsync<CustomerPOCO>(partitionKey: new Cosmos.PartitionKey(poco.id), id: poco.id);
            Assert.IsNotNull(doc1.id);
            await database.DeleteAsync();

        }

        private async Task ValidateGenericReadDocumentFromResource(bool useGateway, Protocol protocol)
        {
            CosmosClient client = TestCommon.CreateCosmosClient(useGateway);

            Cosmos.Database database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };

            Container collection = await database.CreateContainerAsync(new ContainerProperties() { Id = Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition });


            string guidId = Guid.NewGuid().ToString();
            CustomerObjectFromResource objectFromResource = new CustomerObjectFromResource()
            {
                id = guidId,
                pk = guidId,
                BookId = "isbn1",
                PUBLISHTIME = DateTime.Now,
                authors = new List<string>()
            };
            objectFromResource.authors.Add("Ernest Hemingway");

            ItemResponse<CustomerObjectFromResource> doc = await collection.CreateItemAsync(objectFromResource);
            // This tests that the existing ReadDocumentAsync API works as expected, you can only access Document properties
            ItemResponse<CustomerObjectFromResource> documentResponse = await collection.ReadItemAsync<CustomerObjectFromResource>(partitionKey: new Cosmos.PartitionKey(objectFromResource.pk), id: objectFromResource.id);
            Assert.AreEqual(documentResponse.StatusCode, HttpStatusCode.OK);
            Assert.IsNotNull(documentResponse.Resource);

            Assert.AreEqual(documentResponse.Resource.id, guidId);

            // This tests how you can extract the CustomerObjectFromResource from ReadDocumentAsync API, to access POCO properties
            CustomerObjectFromResource customerObjectFromResource = (CustomerObjectFromResource)(dynamic)documentResponse.Resource;
            Assert.AreEqual(customerObjectFromResource.BookId, "isbn1");

            // This tests the implicit operator for ReadDocumentAsync
            CustomerObjectFromResource doc1 = await collection.ReadItemAsync<CustomerObjectFromResource>(partitionKey: new Cosmos.PartitionKey(customerObjectFromResource.id), id: customerObjectFromResource.id);
            Assert.IsNotNull(doc1.id);
        }

        [TestMethod]
        public void ValidatePOCODocumentSerialization()
        {
            // 1. Verify the customer can serialize their POCO object in their own ways
            DocumentClient client = TestCommon.CreateClient(true);
            Documents.Database database = TestCommon.CreateOrGetDatabase(client);
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
            DocumentCollection collection1 = TestCommon.CreateCollectionAsync(client, database, new DocumentCollection { Id = "TestTriggers" + Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition }).Result;

            CustomerPOCO poco = new CustomerPOCO()
            {
                id = Guid.NewGuid().ToString(),
                BookId = "isbn",
                pk = "test",
                PUBLISHTIME = DateTime.Now,
                authors = new List<string>()
            };
            poco.authors.Add("Mark Twain");
            poco.authors.Add("Ernest Hemingway");

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                ContractResolver = new LowercaseContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                PreserveReferencesHandling = PreserveReferencesHandling.Arrays,
                DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate
            };

            string json = JsonConvert.SerializeObject(poco, settings);

            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                Document cusomerBookDoc = Resource.LoadFrom<Document>(ms);

                ResourceResponse<Document> returnedDoc = client.CreateDocumentAsync(collection1, cusomerBookDoc).Result;

                CustomerPOCO poco2 = (CustomerPOCO)JsonConvert.DeserializeObject(returnedDoc.Resource.ToString(), typeof(CustomerPOCO), settings);

                Assert.AreEqual(poco.BookId, poco2.BookId, "BookId dont match");
                Assert.AreEqual(poco.PUBLISHTIME, poco2.PUBLISHTIME, "PUBLISHTIME dont match");
                Assert.AreEqual(poco.id, poco2.id, "id dont match");
                Assert.AreEqual(poco.authors[0], poco2.authors[0], "authors dont match");
            }


            // 2. poco using our own serialization.
            poco.id = Guid.NewGuid().ToString();
            ResourceResponse<Document> pocoReturned = client.CreateDocumentAsync(collection1, poco).Result;
            CustomerPOCO pocoBack = (CustomerPOCO)JsonConvert.DeserializeObject(pocoReturned.Resource.ToString(), typeof(CustomerPOCO), settings);
            Assert.AreEqual(poco.BookId, pocoBack.BookId, "BookId dont match");
            Assert.AreEqual(poco.PUBLISHTIME, pocoBack.PUBLISHTIME, "PUBLISHTIME dont match");
            Assert.AreEqual(poco.id, pocoBack.id, "id dont match");
            Assert.AreEqual(poco.authors[0], pocoBack.authors[0], "authors dont match");


            // 3. Verify the customer object derived from Document is serialized properly.
            CustomerObjectFromDocument inheritFromDocument = new CustomerObjectFromDocument()
            {
                Id = Guid.NewGuid().ToString(),
                BookId = "isbn12345",
                pk = "test",
                PUBLISHTIME = DateTime.Now,
                lastTime = DateTime.Now,
                authors = new List<string>()
            };

            IEnumerable<string> dynamicMembers = GetDynamicMembers(inheritFromDocument);
            // No dynamic member, either all static
            Assert.AreEqual(0, dynamicMembers.Count());

            inheritFromDocument.authors.Add("Mark Twain");
            inheritFromDocument.authors.Add("Ernest Hemingway");

            ResourceResponse<Document> inheritFromDocumentReturned = client.CreateDocumentAsync(collection1, inheritFromDocument).Result;
            IEnumerable<string> dynamicMembers2 = GetDynamicMembers(inheritFromDocumentReturned.Resource);
            // three dynamic member,
            Assert.AreEqual(5, dynamicMembers2.Count());
            string dynamicProp1 = inheritFromDocumentReturned.Resource.GetValue<string>(dynamicMembers2.First());
            object dynamicProp1A = GetDynamicMember(inheritFromDocumentReturned.Resource, dynamicMembers2.First());

            CustomerObjectFromDocument inheritFromDocumentReturned2 = (CustomerObjectFromDocument)JsonConvert.DeserializeObject(inheritFromDocumentReturned.Resource.ToString(), typeof(CustomerObjectFromDocument));
            Assert.AreEqual(inheritFromDocument.BookId, inheritFromDocumentReturned2.BookId, "BookId dont match");
            Assert.AreEqual(inheritFromDocument.PUBLISHTIME, inheritFromDocumentReturned2.PUBLISHTIME, "PUBLISHTIME dont match");
            Assert.AreEqual(inheritFromDocument.authors[0], inheritFromDocumentReturned2.authors[0], "authors dont match");

            Documents.Client.RequestOptions requestOptions = new Documents.Client.RequestOptions
            {
                PartitionKey = new Documents.PartitionKey("test")
            };
            CustomerObjectFromDocument inheritFromDocumentReturned3 = (dynamic)client.ReadDocumentAsync(inheritFromDocumentReturned, requestOptions).Result.Resource;
            inheritFromDocumentReturned3.BookId = "isbn56789";
            inheritFromDocumentReturned3.pk = "test";
            string tostring = inheritFromDocumentReturned3.ToString();

            CustomerObjectFromDocument inheritFromDocumentReturned4 = (dynamic)client.ReplaceDocumentExAsync(inheritFromDocumentReturned3).Result.Resource;
            Assert.AreEqual(inheritFromDocumentReturned4.BookId, inheritFromDocumentReturned3.BookId);

            Assert.IsFalse(tostring.Contains("PUBLISHTIME"));

            // 4. Verify the dynamic properties from Document is serialized using regular JsonConvert
            dynamic dynamicObject = inheritFromDocument;
            dynamicObject.dynamicProperty1 = "dynamicProperty1";
            dynamicObject.dynamicProperty2 = 1234;

            IEnumerable<string> dynamicMembers3 = GetDynamicMembers(dynamicObject);
            // only two dynamic properties
            Assert.AreEqual(2, dynamicMembers3.Count());

            string dynamicObjectString = JsonConvert.SerializeObject(dynamicObject);
            JObject x = (Newtonsoft.Json.Linq.JObject)JsonConvert.DeserializeObject(dynamicObjectString);
            Assert.AreEqual((string)x["dynamicProperty1"], dynamicObject.dynamicProperty1, "dynamicProperty1 dont match");
            Assert.AreEqual((int)x["dynamicProperty2"], dynamicObject.dynamicProperty2, "dynamicProperty2 dont match");

            dynamic dymaticAttachment = new Attachment();
            dymaticAttachment.id = "AttachmentId";
            dymaticAttachment.dynamicProperty = "dynamicProperty";
            string dymaticAttachmentString = JsonConvert.SerializeObject(dymaticAttachment);
            JObject jo = (Newtonsoft.Json.Linq.JObject)JsonConvert.DeserializeObject(dymaticAttachmentString);
            Assert.AreEqual((string)jo["dynamicProperty"], dymaticAttachment.dynamicProperty, "dymaticAttachment.dynamicProperty dont match");


            FeedOptions options = new FeedOptions { EnableCrossPartitionQuery = true };
            IDocumentQuery<dynamic> docServiceQuery1 = client.CreateDocumentQuery(collection1.DocumentsLink,
                string.Format(CultureInfo.CurrentCulture, @"select * from root r where r.id=""{0}""", inheritFromDocument.Id), options).AsDocumentQuery();

            DocumentFeedResponse<dynamic> queryFeed = docServiceQuery1.ExecuteNextAsync().Result;
            dynamic queryResult = queryFeed.ElementAt(0);
            IEnumerable<string> dynamicMembersQueryResult = GetDynamicMembers(queryResult);
            // there are 6 system properties plus three user defined properties.
            Assert.AreEqual(11, dynamicMembersQueryResult.Count());
            object dynamicProp = GetDynamicMember(queryResult, dynamicMembersQueryResult.First());

            // 5. Serialize a class marked with JsonConverter
            CustomerObjectFromDocumentEx testobject5 = new CustomerObjectFromDocumentEx
            {
                TestProperty = "Test5"
            };
            CustomerObjectFromDocumentEx doc5 = (dynamic)client.CreateDocumentAsync(collection1, testobject5).Result.Resource;
            Assert.AreEqual(doc5.TestProperty, testobject5.TestProperty);
            Assert.IsFalse(doc5.ToString().Contains("TestProperty"));
        }

        [TestMethod]
        public async Task ValidateIndexingDirectives()
        {
#if DIRECT_MODE
            // DIRECT MODE has ReadFeed issues in the Public emulator
            DocumentClient client = TestCommon.CreateClient(false);
#endif
#if !DIRECT_MODE
            CosmosClient client = TestCommon.CreateCosmosClient(true);
#endif
            Cosmos.Database database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            Container collection = await database.CreateContainerAsync(new ContainerProperties() { Id = Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition });


            //Since this test starts with read operation first on fresh session client, it may start with stale reads.
            //Wait for server replication before excising this tests starting with read operation.
            TestCommon.WaitForServerReplication();

            string documentName = Guid.NewGuid().ToString("N");
            dynamic document = new Document
            {
                Id = documentName
            };
            document.StringField = "222";

            Logger.LogLine("Adding Document with exclusion");
            dynamic retrievedDocument = await collection.CreateItemAsync(
                document, requestOptions:
                new ItemRequestOptions { IndexingDirective = Cosmos.IndexingDirective.Exclude });

            Logger.LogLine("Querying Document to ensure that document is indexed");
            FeedIterator<Document> queriedDocuments = collection.GetItemQueryIterator<Document>(
                queryText: @"select * from root r where r.StringField=""222""",
                requestOptions: new QueryRequestOptions { MaxConcurrency = 1 });

            Assert.AreEqual(1, await this.GetCountFromIterator(queriedDocuments));

            Logger.LogLine("Replace document to include in index");
            retrievedDocument = await collection.ReplaceItemAsync(id: documentName, item: (Document)retrievedDocument, requestOptions: new ItemRequestOptions { IndexingDirective = Cosmos.IndexingDirective.Include });

            Logger.LogLine("Querying Document to ensure if document is not indexed");
            queriedDocuments = collection.GetItemQueryIterator<Document>(
                queryText: @"select * from root r where r.StringField=""222""",
                requestOptions: new QueryRequestOptions { MaxConcurrency = 1 });

            Assert.AreEqual(1, await this.GetCountFromIterator(queriedDocuments));

            Logger.LogLine("Replace document to not include in index");
            retrievedDocument = await collection.ReplaceItemAsync(id: documentName, item: (Document)retrievedDocument, requestOptions: new ItemRequestOptions { IndexingDirective = Cosmos.IndexingDirective.Exclude });

            Logger.LogLine("Querying Document to ensure that document is indexed");
            queriedDocuments = collection.GetItemQueryIterator<Document>(queryText: @"select * from root r where r.StringField=""222""", requestOptions: new QueryRequestOptions { MaxConcurrency = 1 });
            Assert.AreEqual(1, await this.GetCountFromIterator(queriedDocuments));

            Logger.LogLine("Replace document to not include in index");
            retrievedDocument = await collection.ReplaceItemAsync(id: documentName, item: (Document)retrievedDocument, requestOptions: new ItemRequestOptions { IndexingDirective = Cosmos.IndexingDirective.Exclude });

            Logger.LogLine("Querying Document to ensure that document is indexed");
            queriedDocuments = collection.GetItemQueryIterator<Document>(queryText: @"select * from root r where r.StringField=""222""", requestOptions: new QueryRequestOptions { MaxConcurrency = 1 });
            Assert.AreEqual(1, await this.GetCountFromIterator(queriedDocuments));

            Logger.LogLine("Delete document");
            await collection.DeleteItemAsync<Document>(partitionKey: new Cosmos.PartitionKey(documentName), id: documentName);

            Logger.LogLine("Querying Document to ensure if document is not indexed");
            queriedDocuments = collection.GetItemQueryIterator<Document>(queryText: @"select * from root r where r.StringField=""222""", requestOptions: new QueryRequestOptions { MaxConcurrency = 1 });
            Assert.AreEqual(0, await this.GetCountFromIterator(queriedDocuments));

            await database.DeleteAsync();
        }

        public static string DumpFullExceptionMessage(Exception e)
        {
            StringBuilder exceptionMessage = new StringBuilder();
            while (e != null)
            {
                DocumentClientException docException = e as DocumentClientException;
                if (docException != null && docException.Error != null)
                {
                    exceptionMessage.Append("Code : " + docException.Error.Code);
                    if (docException.Error.ErrorDetails != null)
                    {
                        exceptionMessage.AppendLine(" ; Details : " + docException.Error.ErrorDetails);
                    }
                    if (docException.Error.Message != null)
                    {
                        exceptionMessage.AppendLine(" ; Message : " + docException.Error.Message);
                    }
                    exceptionMessage.Append(" --> ");
                }

                e = e.InnerException;
            }

            return exceptionMessage.ToString();
        }

        private async Task ValidateCollectionQuotaTestsWithFailure(bool useGateway)
        {
            DocumentClient client = TestCommon.CreateClient(useGateway);
            Documents.Database database = null;
            try
            {
                Logger.LogLine("ValidateCollectionQuotaTestsWithFailure");
                Logger.LogLine("Deleting all databases in the system");
                database = client.CreateDatabaseAsync(new Documents.Database { Id = Guid.NewGuid().ToString() }).Result;

                string duplicateCollectionName = Guid.NewGuid().ToString("N");
                List<DocumentCollection> documentCollections = new List<DocumentCollection>();
                long failedCollectionCount = 0;
                // create collections and 1 document on all available server partition
                for (int i = 0; i < 10; ++i)
                {
                    if (i % 5 == 0 && i > 0)
                    {
                        try
                        {
                            // introduce failures
                            DocumentCollection documentCollection = await TestCommon.CreateCollectionAsync(client, database, new DocumentCollection { Id = duplicateCollectionName });
                            Assert.Fail("Creating collection with duplicate name should fail");
                        }
                        catch (DocumentClientException clientException)
                        {
                            failedCollectionCount++;
                            Logger.LogLine("Received expected exception with message {0}, failedCollectionCount = {1}", clientException.Message, failedCollectionCount);
                        }
                    }
                    else
                    {
                        DocumentCollection documentCollection = await TestCommon.CreateCollectionAsync(client, database, new DocumentCollection { Id = Guid.NewGuid().ToString("N") });
                        duplicateCollectionName = documentCollection.Id;
                        documentCollections.Add(documentCollection);
                    }
                }

                long collectionUsage = client.ReadDocumentCollectionFeedAsync(database).Result.CollectionUsage;

                // the quota count should be equal to the successful create requests
                Assert.AreEqual(10 - failedCollectionCount, collectionUsage);
            }
            catch (DocumentClientException e)
            {
                Logger.LogLine("Exception {0}, ActivityId:{1}",
                               e,
                               e.ActivityId);
                throw;
            }
            finally
            {
                if (database != null)
                {
                    await client.DeleteDatabaseAsync(database);
                }
            }
        }

        internal class MediaDocument : Document
        {
            public MediaDocument()
            {

            }

            protected MediaDocument(string mediaType)
            {
                this.MediaType = mediaType;
            }

            public string MediaType
            {
                get => base.GetValue<string>("MediaType");
                set => base.SetValue("MediaType", value);
            }
        }

        internal class Book : MediaDocument
        {
            public Book()
                : base("Book")
            {

            }


            public string Title
            {
                get => base.GetValue<string>("Title");
                set => base.SetValue("Title", value);
            }

            public string Author
            {
                get => base.GetValue<string>("Author");
                set => base.SetValue("Author", value);
            }

            public int NumericField
            {
                get => base.GetValue<int>("NumericField");
                set => base.SetValue("NumericField", value);
            }
        }

        internal sealed class CustomerPOCO
        {
            // intentionally here to have mixed capital case variables
            public string id;
            public string pk;
            public string BookId;
            public List<string> authors;
            public DateTime PUBLISHTIME;
        }

        internal sealed class CustomerObjectFromResource : Resource
        {
            // intentionally here to have mixed capital case variables
            public string id;
            public string pk;
            public string BookId;
            public List<string> authors;
            public DateTime PUBLISHTIME;
        }

        // when derived from Document, the object must be marked with JsonProperty
        internal sealed class CustomerObjectFromDocument : Document
        {
            // intentionally here to have mixed capital case variables
            [JsonProperty(PropertyName = "bookid")]
            public string BookId;
            [JsonProperty(PropertyName = "pk")]
            public string pk;
            [JsonProperty(PropertyName = "authors")]
            public List<string> authors;
            [JsonProperty(PropertyName = "publishtime")]
            public DateTime PUBLISHTIME;
            [JsonProperty(PropertyName = "timesaveasnumber")]
            [JsonConverter(typeof(UnixDateTimeConverter))]
            public DateTime lastTime;
        }

        public class JsonPocoClassSerializer : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                CustomerObjectFromDocumentEx o = value as CustomerObjectFromDocumentEx;

                JObject propertyBag = o.propertyBag;

                // move property from static to propertybag
                propertyBag["TestProperty"] = o.TestProperty;

                serializer.Serialize(writer, propertyBag);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                CustomerObjectFromDocumentEx o = new CustomerObjectFromDocumentEx
                {
                    propertyBag = JObject.Load(reader)
                };

                // move property from property bag to static
                o.TestProperty = (o as dynamic).TestProperty;
                o.propertyBag.Remove("TestProperty");
                return o;
            }

            public override bool CanConvert(Type objectType)
            {
                return typeof(CustomerObjectFromDocumentEx).IsAssignableFrom(objectType);
            }
        }

        [JsonConverter(typeof(JsonPocoClassSerializer))]
        internal class CustomerObjectFromDocumentEx : Document
        {
            public string TestProperty { get; set; }
        }

        internal sealed class LowercaseContractResolver : DefaultContractResolver
        {
            protected override string ResolvePropertyName(string propertyName)
            {
                return char.ToLowerInvariant(propertyName[0]).ToString() + propertyName.Substring(1);
            }
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
