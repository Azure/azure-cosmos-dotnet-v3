//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Newtonsoft.Json.Linq;
    using VisualStudio.TestTools.UnitTesting;

    public class ClientTestsUtils
    {
        internal static Task<List<dynamic>> SqlQueryDatabases(DocumentClient client, string query, int trys = 1)
        {
            Func<Task<IQueryable<dynamic>>> queryFn = () => Task.FromResult(client.CreateDatabaseQuery(query));
            Console.WriteLine("QueryDatabases {0}", query);
            return QueryAllRetry(queryFn, trys);
        }

        internal static Task<List<dynamic>> SqlQueryCollections(DocumentClient client, string databaseLink, string query, int trys = 1)
        {
            Func<Task<IQueryable<dynamic>>> queryFn =
                () => Task.FromResult(client.CreateDocumentCollectionQuery(databaseLink, query));
            Console.WriteLine("QueryCollections {0}", query);
            return QueryAllRetry(queryFn, trys);
        }

        internal static Task<List<dynamic>> SqlQueryDocuments(DocumentClient client, string collectionsLink, string query, int trys, FeedOptions feedOptions = null)
        {
            Func<Task<IQueryable<dynamic>>> queryFn = delegate
            {
                return Task.FromResult(client.CreateDocumentQuery(collectionsLink, query, feedOptions));
            };
            Console.WriteLine("QueryDocuments {0}", query);
            return QueryAllRetry(queryFn, trys);
        }
        internal static Task<List<dynamic>> SqlQueryDocuments(DocumentClient client, string collectionsLink, SqlQuerySpec query, int trys, FeedOptions feedOptions = null)
        {
            Func<Task<IQueryable<dynamic>>> queryFn = delegate
            {
                return Task.FromResult(client.CreateDocumentQuery(collectionsLink, query, feedOptions));
            };
            Console.WriteLine("QueryDocuments {0}", query);
            return QueryAllRetry(queryFn, trys);
        }

        internal static Task<List<dynamic>> SqlQueryStoredProcedures(DocumentClient client, string collectionsLink, string query, int trys = 1, FeedOptions feedOptions = null)
        {
            Func<Task<IQueryable<dynamic>>> queryFn =
                () => Task.FromResult(client.CreateStoredProcedureQuery(collectionsLink, query, feedOptions));

            Console.WriteLine("QueryStoredProcedures {0}", query);
            return QueryAllRetry(queryFn, trys);
        }

        internal static Task<List<dynamic>> SqlQueryUserDefinedFunctions(DocumentClient client, string collectionsLink, string query, int trys = 1)
        {
            Func<Task<IQueryable<dynamic>>> queryFn = delegate
            {
                return Task.FromResult(client.CreateUserDefinedFunctionQuery(collectionsLink, query));
            };
            Console.WriteLine("QueryUserDefinedFunctions {0}", query);
            return QueryAllRetry(queryFn, trys);
        }

        internal static Task<List<dynamic>> SqlQueryTriggers(DocumentClient client, string collectionsLink, string query, int trys = 1)
        {
            Func<Task<IQueryable<dynamic>>> queryFn =
                () => Task.FromResult(client.CreateTriggerQuery(collectionsLink, query));

            Console.WriteLine("QueryTriggers {0}", query);
            return QueryAllRetry(queryFn, trys);
        }
        internal static Task<List<Database>> ReadFeedDatabases(DocumentClient client, int trys = 1)
        {
            Func<string, Task<DocumentFeedResponse<Database>>> listFn = (continuation) =>
                client.ReadDatabaseFeedAsync(new FeedOptions { RequestContinuationToken = continuation });

            Console.WriteLine("ReadFeedDatabases");
            return ReadFeedAllRetry<Database>(listFn, trys);
        }

        internal static Task<List<DocumentCollection>> ReadFeedCollections(DocumentClient client, string databaseLink, int trys = 1)
        {
            Func<string, Task<DocumentFeedResponse<DocumentCollection>>> listFn = (continuation) =>
                client.ReadDocumentCollectionFeedAsync(databaseLink, new FeedOptions { RequestContinuationToken = continuation });

            Console.WriteLine("ReadFeedCollections");
            return ReadFeedAllRetry(listFn, trys);
        }

        internal static Task<List<dynamic>> ReadFeedDocuments(DocumentClient client, string collectionLink, int trys = 1)
        {
            Func<string, Task<DocumentFeedResponse<dynamic>>> listFn =
                continuation =>
                    client.ReadDocumentFeedAsync(collectionLink, new FeedOptions { RequestContinuationToken = continuation });

            Console.WriteLine("ReadFeedDocuments");
            return ReadFeedAllRetry<dynamic>(listFn, trys);
        }

        internal static Task<List<StoredProcedure>> ReadFeedStoredProcedures(DocumentClient client, string collectionLink, int trys = 1)
        {
            Func<string, Task<DocumentFeedResponse<StoredProcedure>>> listFn =
                continuation =>
                    client.ReadStoredProcedureFeedAsync(collectionLink,
                        new FeedOptions { RequestContinuationToken = continuation });

            Console.WriteLine("ReadFeedStoredProcedures");
            return ReadFeedAllRetry(listFn, trys);
        }

        internal static Task<List<UserDefinedFunction>> ReadFeedUserDefinedFunctions(DocumentClient client, string collectionLink, int trys = 1)
        {
            Func<string, Task<DocumentFeedResponse<UserDefinedFunction>>> listFn =
                continuation =>
                    client.ReadUserDefinedFunctionFeedAsync(collectionLink,
                        new FeedOptions { RequestContinuationToken = continuation });

            Console.WriteLine("ReadFeedUserDefinedFunctions");
            return ReadFeedAllRetry(listFn, trys);
        }

        internal static Task<List<Trigger>> ReadFeedTriggers(DocumentClient client, string collectionLink, int trys = 1)
        {
            Func<string, Task<DocumentFeedResponse<Trigger>>> listFn =
                continuation =>
                    client.ReadTriggerFeedAsync(collectionLink, new FeedOptions { RequestContinuationToken = continuation });

            Console.WriteLine("ReadFeedTriggers");
            return ReadFeedAllRetry(listFn, trys);
        }


        public static string GenerateAltLink(string dbName)
        {
            return Paths.DatabasesPathSegment + "/" + dbName;
        }

        public static string GenerateAltLink(string dbName, string collOrUserName, Type resourceType)
        {
            if (resourceType == typeof(DocumentCollection))
            {
                return Paths.DatabasesPathSegment + "/" + dbName + "/" + Paths.CollectionsPathSegment + "/" + collOrUserName;
            }
            else if (resourceType == typeof(User))
            {
                return Paths.DatabasesPathSegment + "/" + dbName + "/" + Paths.UsersPathSegment + "/" + collOrUserName;
            }
            return null;
        }

        public static string GenerateAltLink(string dbName, string collOrUserName, string resourceName, Type resourceType)
        {
            if (resourceType == typeof(Permission))
            {
                return Paths.DatabasesPathSegment + "/" + dbName + "/" + Paths.UsersPathSegment + "/" + collOrUserName + "/" +
                    Paths.PermissionsPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(Document))
            {
                return Paths.DatabasesPathSegment + "/" + dbName + "/" + Paths.CollectionsPathSegment + "/" + collOrUserName + "/" +
                            Paths.DocumentsPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(UserDefinedFunction))
            {
                return Paths.DatabasesPathSegment + "/" + dbName + "/" + Paths.CollectionsPathSegment + "/" + collOrUserName + "/" +
                            Paths.UserDefinedFunctionsPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(Trigger))
            {
                return Paths.DatabasesPathSegment + "/" + dbName + "/" + Paths.CollectionsPathSegment + "/" + collOrUserName + "/" +
                            Paths.TriggersPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(StoredProcedure))
            {
                return Paths.DatabasesPathSegment + "/" + dbName + "/" + Paths.CollectionsPathSegment + "/" + collOrUserName + "/" +
                            Paths.StoredProceduresPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(Conflict))
            {
                return Paths.DatabasesPathSegment + "/" + dbName + "/" + Paths.CollectionsPathSegment + "/" + collOrUserName + "/" +
                            Paths.ConflictsPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(Schema))
            {
                return Paths.DatabasesPathSegment + "/" + dbName + "/" + Paths.CollectionsPathSegment + "/" + collOrUserName + "/" +
                            Paths.SchemasPathSegment + "/" + resourceName;
            }

            return null;
        }

        public static string GenerateAltLink(string dbName, string collName, string docName, string attachmentName)
        {
            return Paths.DatabasesPathSegment + "/" + dbName + "/" + Paths.CollectionsPathSegment + "/" + collName + "/" +
                        Paths.DocumentsPathSegment + "/" + docName + "/" +
                        Paths.AttachmentsPathSegment + "/" + attachmentName;
        }

        private static async Task<List<dynamic>> QueryAllRetry(Func<Task<IQueryable<dynamic>>> queryFn, int trys = 1)
        {
            List<dynamic> results = new List<dynamic>();

            for (int t = 1; t <= trys; t++)
            {
                IDocumentQuery<dynamic> DocumentQuery = (await queryFn()).AsDocumentQuery();

                while (DocumentQuery.HasMoreResults)
                {
                    results.AddRange(await DocumentQuery.ExecuteNextAsync());
                }
                if (results.Count > 0)
                {
                    return results;
                }

                await Task.Delay(1000);

                Console.WriteLine("QueryAllRetry {0}", t);
            }
            return results;
        }

        private static async Task<List<T>> ReadFeedAllRetry<T>(Func<string, Task<DocumentFeedResponse<T>>> listFn, int trys = 1)
        {
            List<T> results = null;

            for (int t = 1; t <= trys; t++)
            {
                results = await ReadFeedAll(listFn);
                if (results.Count > 0)
                {
                    return results;
                }

                await Task.Delay(1000);

                Console.WriteLine("ReadFeedAllRetry {0}", t);
            }

            return results;
        }

        private static async Task<List<T>> ReadFeedAll<T>(Func<string, Task<DocumentFeedResponse<T>>> listFn)
        {
            List<T> results = new List<T>();
            string continuation = null;

            do
            {
                DocumentFeedResponse<T> response = await listFn(continuation);
                results.AddRange(response);
                continuation = response.ResponseContinuation;
            } while (!string.IsNullOrEmpty(continuation));

            return results;
        }

        internal static async Task PartitionedCollectionSmokeTest(DocumentClient client, bool sharedOffer = false, bool sharedThroughputCollections = false, int numberOfCollections = 1)
        {
            if (!sharedOffer && sharedThroughputCollections)
            {
                throw new ArgumentException("Shared throughput collections are not supported without shared offer");
            }

            string uniqDatabaseName = string.Format("SmokeTest_{0}", Guid.NewGuid().ToString("N"));
            RequestOptions options = new RequestOptions { OfferThroughput = 50000 };
            Database database = sharedOffer ? await client.CreateDatabaseAsync(new Database { Id = uniqDatabaseName }, options) : await client.CreateDatabaseAsync(new Database { Id = uniqDatabaseName });
            Assert.AreEqual(database.AltLink, ClientTestsUtils.GenerateAltLink(uniqDatabaseName));
            Database readbackdatabase = await client.ReadDatabaseAsync(database.SelfLink);
            List<dynamic> results = await ClientTestsUtils.SqlQueryDatabases(client, string.Format(@"select r._rid from root r where r.id = ""{0}""", uniqDatabaseName), 10);
            Assert.AreEqual(1, results.Count, "Should have queried and found 1 document");
            Assert.AreEqual(database.ResourceId, ((QueryResult)results[0]).ResourceId);
            Assert.IsTrue((await ClientTestsUtils.ReadFeedDatabases(client)).Any((db) => db.Id == uniqDatabaseName));
            results = await ClientTestsUtils.SqlQueryDatabases(client, string.Format(@"select r._rid, r.id from root r where r.id = ""{0}""", uniqDatabaseName), 10);
            Assert.AreEqual(1, results.Count, "Should have queried and found 1 database");
            Assert.AreEqual(database.ResourceId, ((QueryResult)results[0]).ResourceId);
            Assert.AreEqual(database.ResourceId, (await client.ReadDatabaseAsync(database.SelfLink)).Resource.ResourceId);
            Assert.AreEqual(((Database)results[0]).AltLink, ClientTestsUtils.GenerateAltLink(uniqDatabaseName));

            ArrayList testCollections = new ArrayList();
            for (int i = 0; i < numberOfCollections; i++)
            {
                string uniqCollectionName = "SmokeTestCollection" + Guid.NewGuid().ToString("N");
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition
                {
                    Paths = new System.Collections.ObjectModel.Collection<string> { "/id" },
                    Kind = PartitionKind.Hash
                };

                DocumentCollection collection;
                if (sharedThroughputCollections)
                {
                    collection = await TestCommon.CreateCollectionAsync(client, database.SelfLink, new DocumentCollection { Id = uniqCollectionName, PartitionKey = partitionKeyDefinition });
                }
                else
                {
                    collection = await TestCommon.CreateCollectionAsync(client, database.SelfLink, new DocumentCollection { Id = uniqCollectionName, PartitionKey = partitionKeyDefinition }, options);
                }

                Assert.AreEqual(collection.AltLink, ClientTestsUtils.GenerateAltLink(uniqDatabaseName, uniqCollectionName, typeof(DocumentCollection)));
                results = await SqlQueryCollections(client, database.SelfLink, string.Format(@"select r._rid from root r where r.id = ""{0}""", uniqCollectionName), 10);  // query through database link
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 collection");
                Assert.AreEqual(collection.ResourceId, ((QueryResult)results[0]).ResourceId);
                results = await SqlQueryCollections(client, database.CollectionsLink, string.Format(@"select r._rid, r.id from root r where r.id = ""{0}""", uniqCollectionName), 10);  // query through CollectionsLink
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 collection");
                Assert.AreEqual(collection.ResourceId, ((QueryResult)results[0]).ResourceId);
                Assert.AreEqual(1, (await ReadFeedCollections(client, database.SelfLink)).Count(item => item.Id == uniqCollectionName));  // read through database link
                Assert.AreEqual(1, (await ReadFeedCollections(client, database.CollectionsLink)).Count(item => item.Id == uniqCollectionName));  // read through CollectionsLink
                Assert.AreEqual(collection.ResourceId, (await client.ReadDocumentCollectionAsync(collection.SelfLink)).Resource.ResourceId);
                Assert.AreEqual(((DocumentCollection)results[0]).AltLink, ClientTestsUtils.GenerateAltLink(uniqDatabaseName, uniqCollectionName, typeof(DocumentCollection)));
                testCollections.Add(collection);

                string uniqDocumentName = "SmokeTestDocument" + Guid.NewGuid().ToString("N");
                LinqGeneralBaselineTests.Book myDocument = new LinqGeneralBaselineTests.Book
                {
                    Id = uniqDocumentName,
                    Title = "My Book", //Simple Property.
                    Languages = new LinqGeneralBaselineTests.Language[] { new LinqGeneralBaselineTests.Language { Name = "English", Copyright = "London Publication" }, new LinqGeneralBaselineTests.Language { Name = "French", Copyright = "Paris Publication" } }, //Array Property
                    Author = new LinqGeneralBaselineTests.Author { Name = "Don", Location = "France" }, //Complex Property
                    Price = 9.99,
                    Editions = new List<LinqGeneralBaselineTests.Edition>() { new LinqGeneralBaselineTests.Edition() { Name = "First", Year = 2001 }, new LinqGeneralBaselineTests.Edition() { Name = "Second", Year = 2005 } }
                };
                Document document = await client.CreateDocumentAsync(collection.SelfLink, myDocument);
                Assert.AreEqual(document.AltLink, ClientTestsUtils.GenerateAltLink(uniqDatabaseName, uniqCollectionName, uniqDocumentName, typeof(Document)));
                results = await SqlQueryDocuments(client, collection.SelfLink, string.Format(@"select r._rid from root r where r.id = ""{0}""", uniqDocumentName), 10);  // query through collection link
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 document");
                Assert.AreEqual(document.ResourceId, ((QueryResult)results[0]).ResourceId);
                results = await SqlQueryDocuments(client, collection.DocumentsLink, string.Format(@"select r._rid from root r where r.id = ""{0}""", uniqDocumentName), 10);  // query through DocumentsLink
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 document");
                Assert.AreEqual(document.ResourceId, ((QueryResult)results[0]).ResourceId);
                Assert.AreEqual(1, (await ReadFeedDocuments(client, collection.SelfLink)).Count(item => item.Id == uniqDocumentName));  // read through collection link
                Assert.AreEqual(1, (await ReadFeedDocuments(client, collection.DocumentsLink)).Count(item => item.Id == uniqDocumentName));  // read through DocumentsLink

                if (client.QueryCompatibilityMode != QueryCompatibilityMode.SqlQuery)
                {
                    //Test query with parameters
                    results = await SqlQueryDocuments(client, collection.SelfLink,
                        new SqlQuerySpec
                        {
                            QueryText = @"select r._rid from root r where r.id = @id",
                            Parameters = new SqlParameterCollection() { new SqlParameter("@id", uniqDocumentName) }
                        }, 10);  // query through collection link
                    Assert.AreEqual(1, results.Count, "Should have queried and found 1 document");
                    Assert.AreEqual(document.ResourceId, ((QueryResult)results[0]).ResourceId);
                }

                RequestOptions docReplaceRequestOptions = new RequestOptions { PartitionKey = new PartitionKey(document.Id) };
                FeedOptions docReplaceFeedOptions = new FeedOptions { EnableCrossPartitionQuery = true, PartitionKey = new PartitionKey(document.Id) };

                myDocument.Title = "My_Book_v2";

                document = await client.ReplaceDocumentAsync(document.AltLink, myDocument);
                results = await SqlQueryDocuments(client, collection.SelfLink, string.Format(@"select r._rid from root r where r.id = ""{0}""", uniqDocumentName), 10, docReplaceFeedOptions);
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 document");
                Assert.AreEqual(document.ResourceId, ((QueryResult)results[0]).ResourceId);
                results = await SqlQueryDocuments(client, collection.SelfLink, string.Format(@"SELECT r.id, r._rid FROM root r WHERE r.id=""{0}""", uniqDocumentName), 10);  // query through collection
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 document");
                results = await SqlQueryDocuments(client, collection.DocumentsLink, string.Format(@"SELECT r.id, r._rid FROM root r WHERE r.id=""{0}""", uniqDocumentName), 10);  // query through DocumentsLink
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 document");
                Assert.AreEqual(((Document)results[0]).AltLink, ClientTestsUtils.GenerateAltLink(uniqDatabaseName, uniqCollectionName, uniqDocumentName, typeof(Document)));


                // No Range Index on ts - override with scan
                FeedOptions queryFeedOptions1 = new FeedOptions() { EnableScanInQuery = true, EnableCrossPartitionQuery = true };
                results = await SqlQueryDocuments(client, collection.SelfLink, string.Format(@"SELECT r.name FROM root r WHERE r.Price>0"), 10, queryFeedOptions1);
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 document");

                FeedOptions queryFeedOptions2 = new FeedOptions() { EmitVerboseTracesInQuery = true, EnableCrossPartitionQuery = true };
                results = await SqlQueryDocuments(client, collection.SelfLink, string.Format(@"SELECT r.name FROM root r WHERE r.Price=9.99"), 10, queryFeedOptions2);
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 document");

                FeedOptions queryFeedOptions3 = new FeedOptions() { EmitVerboseTracesInQuery = false, EnableCrossPartitionQuery = true };
                results = await SqlQueryDocuments(client, collection.SelfLink, string.Format(@"SELECT r.name FROM root r WHERE r.Price=9.99"), 10, queryFeedOptions3);
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 document");

                string uniqStoredProcedureName = "SmokeTestStoredProcedure" + Guid.NewGuid().ToString();
                StoredProcedure storedProcedure = await client.CreateStoredProcedureAsync(collection.SelfLink, new StoredProcedure { Id = uniqStoredProcedureName, Body = "function f() {var x = 10;}" });
                results = await SqlQueryStoredProcedures(client, collection.SelfLink, string.Format(@"SELECT r.id, r._rid FROM root r WHERE r.id = ""{0}""", uniqStoredProcedureName), 10);  // query through collection link
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 storedProcedure");
                Assert.AreEqual(storedProcedure.ResourceId, ((QueryResult)results[0]).ResourceId);
                results = await SqlQueryStoredProcedures(client, collection.StoredProceduresLink, string.Format(@"SELECT r.id, r._rid FROM root r WHERE r.id = ""{0}""", uniqStoredProcedureName), 10);  // query through StoredProceduresLink
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 storedProcedure");
                Assert.AreEqual(storedProcedure.ResourceId, ((QueryResult)results[0]).ResourceId);
                Assert.AreEqual(1, (await ReadFeedStoredProcedures(client, collection.SelfLink)).Count(item => item.Id == uniqStoredProcedureName));  // read through collection link
                Assert.AreEqual(1, (await ReadFeedStoredProcedures(client, collection.StoredProceduresLink)).Count(item => item.Id == uniqStoredProcedureName));  // read through StoredProceduresLink


                storedProcedure.Body = "function f() {var x= 20;}";
                storedProcedure = await client.ReplaceStoredProcedureAsync(storedProcedure);
                results = await SqlQueryStoredProcedures(client, collection.StoredProceduresLink, string.Format(@"SELECT r.id, r._rid FROM root r WHERE r.id=""{0}""", storedProcedure.Id), 10);  // query through StoredProceduresLink
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 storedProcedure");
                Assert.AreEqual(storedProcedure.ResourceId, ((QueryResult)results[0]).ResourceId);
                Assert.AreEqual(storedProcedure.ResourceId, (await client.ReadStoredProcedureAsync(storedProcedure.SelfLink)).Resource.ResourceId);
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 storedProcedure");

                string uniqTriggerName = "SmokeTestTrigger" + Guid.NewGuid().ToString("N");
                Trigger trigger = await client.CreateTriggerAsync(collection.SelfLink, new Trigger { Id = uniqTriggerName, Body = "function f() {var x = 10;}", TriggerOperation = TriggerOperation.All, TriggerType = TriggerType.Pre });
                results = await SqlQueryTriggers(client, collection.SelfLink, string.Format(@"select r._rid from root r where r.id = ""{0}""", uniqTriggerName), 10);  // query through collection link
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 trigger");
                Assert.AreEqual(trigger.ResourceId, ((QueryResult)results[0]).ResourceId);
                results = await SqlQueryTriggers(client, collection.TriggersLink, string.Format(@"select r._rid from root r where r.id = ""{0}""", uniqTriggerName), 10);  // query through TriggersLink
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 trigger");
                Assert.AreEqual(trigger.ResourceId, ((QueryResult)results[0]).ResourceId);
                Assert.AreEqual(1, (await ReadFeedTriggers(client, collection.SelfLink)).Count(item => item.Id == uniqTriggerName));  // read through collection link
                Assert.AreEqual(1, (await ReadFeedTriggers(client, collection.TriggersLink)).Count(item => item.Id == uniqTriggerName));  // read through TriggersLink

                trigger.Body = "function f() {var x = 10;}";
                trigger = await client.ReplaceTriggerAsync(trigger);
                results = await SqlQueryTriggers(client, collection.SelfLink, string.Format(@"select r._rid from root r where r.id = ""{0}""", uniqTriggerName), 10);
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 trigger");
                Assert.AreEqual(trigger.ResourceId, ((QueryResult)results[0]).ResourceId);
                Assert.AreEqual(trigger.ResourceId, (await client.ReadTriggerAsync(trigger.SelfLink)).Resource.ResourceId);
                results = await SqlQueryTriggers(client, collection.SelfLink, string.Format(@"SELECT r.id, r._rid FROM root r WHERE r.id=""{0}""", uniqTriggerName), 10);  // query through collection link
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 trigger");
                results = await SqlQueryTriggers(client, collection.TriggersLink, string.Format(@"SELECT r.id, r._rid FROM root r WHERE r.id=""{0}""", uniqTriggerName), 10);  // query through TriggersLink
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 trigger");

                string uniqUserDefinedFunctionName = "SmokeTestUserDefinedFunction" + Guid.NewGuid().ToString("N");
                UserDefinedFunction userDefinedFunction = await client.CreateUserDefinedFunctionAsync(collection.SelfLink, new UserDefinedFunction { Id = uniqUserDefinedFunctionName, Body = "function (){ var x = 10;}" });
                results = await SqlQueryUserDefinedFunctions(client, collection.SelfLink, string.Format(@"select r._rid from root r where r.id = ""{0}""", uniqUserDefinedFunctionName), 10);  // query through collection link
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 userDefinedFunction");
                Assert.AreEqual(userDefinedFunction.ResourceId, ((QueryResult)results[0]).ResourceId);
                results = await SqlQueryUserDefinedFunctions(client, collection.UserDefinedFunctionsLink, string.Format(@"select r._rid from root r where r.id = ""{0}""", uniqUserDefinedFunctionName), 10);  // query through UserDefinedFunctionsLink
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 userDefinedFunction");
                Assert.AreEqual(userDefinedFunction.ResourceId, ((QueryResult)results[0]).ResourceId);
                Assert.AreEqual(1, (await ReadFeedUserDefinedFunctions(client, collection.SelfLink)).Count(item => item.Id == uniqUserDefinedFunctionName));  // read through collection link
                Assert.AreEqual(1, (await ReadFeedUserDefinedFunctions(client, collection.UserDefinedFunctionsLink)).Count(item => item.Id == uniqUserDefinedFunctionName));  // read through UserDefinedFunctionsLink
                userDefinedFunction.Body = "function (){ var x = 10;}";
                userDefinedFunction = await client.ReplaceUserDefinedFunctionAsync(userDefinedFunction);
                results = await SqlQueryUserDefinedFunctions(client, collection.SelfLink, string.Format(@"select r._rid from root r where r.id = ""{0}""", uniqUserDefinedFunctionName), 10);
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 userDefinedFunction");
                Assert.AreEqual(userDefinedFunction.ResourceId, ((QueryResult)results[0]).ResourceId);
                Assert.AreEqual(userDefinedFunction.ResourceId, (await client.ReadUserDefinedFunctionAsync(userDefinedFunction.SelfLink)).Resource.ResourceId);
                results = await SqlQueryUserDefinedFunctions(client, collection.SelfLink, string.Format(@"SELECT r.id, r._rid FROM root r WHERE r.id=""{0}""", uniqUserDefinedFunctionName), 10);  // query through collection link
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 userDefinedFunction");
                results = await SqlQueryUserDefinedFunctions(client, collection.UserDefinedFunctionsLink, string.Format(@"SELECT r.id, r._rid FROM root r WHERE r.id=""{0}""", uniqUserDefinedFunctionName), 10);  // query through UserDefinedFunctionsLink
                Assert.AreEqual(1, results.Count, "Should have queried and found 1 userDefinedFunction");

                //Test select array
                IDocumentQuery<dynamic> queryArray = client.CreateDocumentQuery(collection.SelfLink, "SELECT VALUE [1, 2, 3, 4]").AsDocumentQuery();
                JArray result = queryArray.ExecuteNextAsync().Result.FirstOrDefault();

                Assert.AreEqual(result[0], 1);
                Assert.AreEqual(result[1], 2);
                Assert.AreEqual(result[2], 3);
                Assert.AreEqual(result[3], 4);

                RequestOptions requestOptions = new RequestOptions { PartitionKey = new PartitionKey(document.Id) };
                await client.DeleteDocumentAsync(document.SelfLink, requestOptions);
            }

            foreach (DocumentCollection collection in testCollections)
            {
                await client.DeleteDocumentCollectionAsync(collection.SelfLink);
            }
            await client.DeleteDatabaseAsync(database.SelfLink);
        }

        private class QueryResult : Resource
        {
        }
    }
}
