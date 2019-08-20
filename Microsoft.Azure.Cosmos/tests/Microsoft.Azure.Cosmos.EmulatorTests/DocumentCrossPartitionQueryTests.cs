//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Configuration;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent;
    using Microsoft.Azure.Cosmos.Query.Core.ParallelQuery;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryOracle;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;
    using CompositePath = Documents.CompositePath;
    using CompositePathSortOrder = Documents.CompositePathSortOrder;
    using ConsistencyLevel = Documents.ConsistencyLevel;
    using Database = Documents.Database;
    using DataType = Documents.DataType;
    using IncludedPath = Documents.IncludedPath;
    using Index = Documents.Index;
    using IndexingPolicy = Documents.IndexingPolicy;
    using PartitionKey = Documents.PartitionKey;
    using RangeIndex = Documents.RangeIndex;
    using RequestOptions = Documents.Client.RequestOptions;

    /// <summary>
    /// Tests for DocumentCrossPartitionQueryTests.
    /// </summary>
    [TestClass]
    public class DocumentCrossPartitionQueryTests
    {
        private static readonly string[] NoDocuments = new string[] { };
        private static readonly JsonSerializerSettings NoDateHandelingSerializerSettings = new JsonSerializerSettings()
        {
            DateParseHandling = DateParseHandling.None,
        };
        private static readonly RequestOptions NoDateHandelingRequestOptions = new RequestOptions()
        {
            JsonSerializerSettings = NoDateHandelingSerializerSettings,
        };

        private static DocumentClient GatewayClient;
        private static DocumentClient DirectClient;
        private static DocumentClient Client;
        private static Database database;
        private static readonly AsyncLocal<LocalCounter> responseLengthBytes = new AsyncLocal<LocalCounter>();
        private static readonly AsyncLocal<Guid> outerDocumentFeedResponseActivityId = new AsyncLocal<Guid>();

        static DocumentCrossPartitionQueryTests()
        {
            (string endpoint, string authKey) account =TestCommon.GetAccountInfo();
            GatewayClient = new DocumentClient(
               new Uri(account.endpoint),
               account.authKey,
               new ConnectionPolicy() { ConnectionMode = ConnectionMode.Gateway, ConnectionProtocol = Protocol.Https });
            DirectClient = new DocumentClient(
                new Uri(account.endpoint),
                account.authKey,
                new ConnectionPolicy() { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp });
            Client = DirectClient;
        }

        [FlagsAttribute]
        private enum ConnectionModes
        {
            None = 0,
            Direct = 0x1,
            Gateway = 0x2,
        }

        [FlagsAttribute]
        private enum CollectionTypes
        {
            None = 0,
            Partitioned = 0x1,
            NonPartitioned = 0x2,
        }

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            DocumentCrossPartitionQueryTests.CleanUp();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            DocumentCrossPartitionQueryTests.database = DocumentCrossPartitionQueryTests.CreateDatabase();
        }

        [TestCleanup]
        public void Cleanup()
        {
            DocumentCrossPartitionQueryTests.DirectClient.DeleteDatabaseAsync(DocumentCrossPartitionQueryTests.database).Wait();
        }

        private static string GetApiVersion()
        {
            return Microsoft.Azure.Documents.HttpConstants.Versions.CurrentVersion;
        }

        private static void SetApiVersion(string apiVersion)
        {
            Microsoft.Azure.Documents.HttpConstants.Versions.CurrentVersion = apiVersion;
            Microsoft.Azure.Documents.HttpConstants.Versions.CurrentVersionUTF8 = Encoding.UTF8.GetBytes(apiVersion);
        }

        private static Database CreateDatabase()
        {
            return DocumentCrossPartitionQueryTests.Client.CreateDatabaseAsync(
                new Database
                {
                    Id = Guid.NewGuid().ToString() + "db"
                }).Result;
        }

        private static IReadOnlyList<PartitionKeyRange> GetPartitionKeyRanges(DocumentCollection documentCollection)
        {
            Range<string> fullRange = new Range<string>(
                PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                true,
                false);
            IRoutingMapProvider routingMapProvider = DocumentCrossPartitionQueryTests.Client.GetPartitionKeyRangeCacheAsync().Result;
            IReadOnlyList<PartitionKeyRange> ranges = routingMapProvider.TryGetOverlappingRangesAsync(documentCollection.ResourceId, fullRange).Result;
            return ranges;
        }

        private static DocumentCollection CreatePartitionCollection(string partitionKey = "/id", IndexingPolicy indexingPolicy = null)
        {
            DocumentCollection documentCollection = DocumentCrossPartitionQueryTests.Client.CreateDocumentCollectionAsync(
                UriFactory.CreateDatabaseUri(DocumentCrossPartitionQueryTests.database.Id),
                new DocumentCollection
                {
                    Id = Guid.NewGuid().ToString() + "collection",
                    IndexingPolicy = indexingPolicy == null ? new IndexingPolicy
                    {
                        IncludedPaths = new Collection<IncludedPath>
                        {
                            new IncludedPath
                            {
                                Path = "/*",
                                Indexes = new Collection<Index>
                                {
                                    RangeIndex.Range(DataType.Number),
                                    RangeIndex.Range(DataType.String),
                                }
                            }
                        }
                    } : indexingPolicy,
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Paths = new Collection<string> { partitionKey },
                        Kind = PartitionKind.Hash
                    }
                },
                // This throughput needs to be about half the max with multi master
                // otherwise it will create about twice as many partitions.
                new RequestOptions { OfferThroughput = 25000 }).Result;

            IReadOnlyList<PartitionKeyRange> ranges = DocumentCrossPartitionQueryTests.GetPartitionKeyRanges(documentCollection);
            Assert.AreEqual(5, ranges.Count());

            return documentCollection;
        }

        private static DocumentCollection CreateNonPartitionedCollection(IndexingPolicy indexingPolicy = null)
        {
            return DocumentCrossPartitionQueryTests.Client.CreateDocumentCollectionAsync(
                UriFactory.CreateDatabaseUri(DocumentCrossPartitionQueryTests.database.Id),
                new DocumentCollection
                {
                    Id = Guid.NewGuid().ToString() + "collection",
                    IndexingPolicy = indexingPolicy == null ? new IndexingPolicy
                    {
                        IncludedPaths = new Collection<IncludedPath>
                        {
                            new IncludedPath
                            {
                                Path = "/*",
                                Indexes = new Collection<Index>
                                {
                                    RangeIndex.Range(DataType.Number, -1),
                                    RangeIndex.Range(DataType.String, -1),
                                }
                            }
                        }
                    } : indexingPolicy,
                },
                new RequestOptions { OfferThroughput = 10000 }).Result;
        }

        private static async Task<Tuple<DocumentCollection, List<Document>>> CreatePartitionedCollectionAndIngestDocuments(IEnumerable<string> documents, string partitionKey = "/id", IndexingPolicy indexingPolicy = null)
        {
            DocumentCollection partitionedCollection = DocumentCrossPartitionQueryTests.CreatePartitionCollection(partitionKey, indexingPolicy);
            List<Document> insertedDocuments = new List<Document>();
            foreach (string document in documents)
            {
                JsonTextReader textReader = new JsonTextReader(new StringReader(document))
                {
                    DateParseHandling = DateParseHandling.None
                };
                object deserializedDocument = JToken.Load(textReader);
                Document insertedDocument = await Client.CreateDocumentAsync(partitionedCollection.SelfLink, deserializedDocument, NoDateHandelingRequestOptions);
                insertedDocuments.Add(insertedDocument);
            }

            return new Tuple<DocumentCollection, List<Document>>(partitionedCollection, insertedDocuments);
        }

        private static async Task<Tuple<DocumentCollection, List<Document>>> CreateNonPartitionedCollectionAndIngestDocuments(IEnumerable<string> documents, IndexingPolicy indexingPolicy = null)
        {
            DocumentCollection nonPartitionedCollection = DocumentCrossPartitionQueryTests.CreateNonPartitionedCollection(indexingPolicy);
            List<Document> insertedDocuments = new List<Document>();
            foreach (string document in documents)
            {
                JsonTextReader textReader = new JsonTextReader(new StringReader(document))
                {
                    DateParseHandling = DateParseHandling.None
                };
                object deserializedDocument = JToken.Load(textReader);
                Document insertedDocument = await Client.CreateDocumentAsync(nonPartitionedCollection.SelfLink, deserializedDocument, NoDateHandelingRequestOptions);
                insertedDocuments.Add(insertedDocument);
            }

            return new Tuple<DocumentCollection, List<Document>>(nonPartitionedCollection, insertedDocuments);
        }

        private static void CleanUp()
        {
            IEnumerable<Database> allDatabases = from database in DocumentCrossPartitionQueryTests.Client.CreateDatabaseQuery()
                                                 select database;

            foreach (Database database in allDatabases)
            {
                DocumentCrossPartitionQueryTests.Client.DeleteDatabaseAsync(database.SelfLink).Wait();
            }
        }

        private static async Task RunWithApiVersion(string apiVersion, Func<Task> function)
        {
            string originalApiVersion = GetApiVersion();
            DocumentClient originalDocumentClient = DocumentCrossPartitionQueryTests.Client;
            DocumentClient originalGatewayClient = DocumentCrossPartitionQueryTests.GatewayClient;
            DocumentClient originalDirectClient = DocumentCrossPartitionQueryTests.DirectClient;

            try
            {
                SetApiVersion(apiVersion);
                if (apiVersion != originalApiVersion)
                {
                    DocumentCrossPartitionQueryTests.Client = TestCommon.CreateClient(false, defaultConsistencyLevel: ConsistencyLevel.Session);
                    DocumentCrossPartitionQueryTests.GatewayClient = TestCommon.CreateClient(true, defaultConsistencyLevel: ConsistencyLevel.Session);
                    DocumentCrossPartitionQueryTests.DirectClient = TestCommon.CreateClient(false, defaultConsistencyLevel: ConsistencyLevel.Session);
                }

                await function();
            }
            finally
            {
                DocumentCrossPartitionQueryTests.Client = originalDocumentClient;
                DocumentCrossPartitionQueryTests.GatewayClient = originalGatewayClient;
                DocumentCrossPartitionQueryTests.DirectClient = originalDirectClient;
                SetApiVersion(originalApiVersion);
            }
        }

        internal delegate Task Query(
            DocumentClient documentClient,
            DocumentCollection collection,
            IEnumerable<Document> documents);

        internal delegate Task Query<T>(
            DocumentClient documentClient,
            DocumentCollection collection,
            IEnumerable<Document> documents,
            T testArgs);

        internal delegate DocumentClient DocumentClientFactory(ConnectionMode connectionMode);

        private static Task CreateIngestQueryDelete(
            ConnectionModes connectionModes,
            CollectionTypes collectionTypes,
            IEnumerable<string> documents,
            Query query,
            string partitionKey = "/id",
            IndexingPolicy indexingPolicy = null,
            DocumentClientFactory documentClientFactory = null)
        {
            Query<object> queryWrapper = (documentClient, documentCollection, inputDocuments, throwaway) =>
            {
                return query(documentClient, documentCollection, inputDocuments);
            };

            return DocumentCrossPartitionQueryTests.CreateIngestQueryDelete<object>(
                connectionModes,
                collectionTypes,
                documents,
                queryWrapper,
                null,
                partitionKey,
                indexingPolicy,
                documentClientFactory);
        }

        private static Task CreateIngestQueryDelete<T>(
            ConnectionModes connectionModes,
            CollectionTypes collectionTypes,
            IEnumerable<string> documents,
            Query<T> query,
            T testArgs,
            string partitionKey = "/id",
            IndexingPolicy indexingPolicy = null,
            DocumentClientFactory documentClientFactory = null)
        {
            return DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                connectionModes,
                collectionTypes,
                documents,
                query,
                documentClientFactory ?? DocumentCrossPartitionQueryTests.CreateDefaultDocumentClient,
                testArgs,
                partitionKey,
                indexingPolicy);
        }

        /// <summary>
        /// Task that wraps boiler plate code for query tests (collection create -> ingest documents -> query documents -> delete collections).
        /// Note that this function will take the cross product connectionModes and collectionTypes.
        /// </summary>
        /// <param name="connectionModes">The connection modes to use.</param>
        /// <param name="collectionTypes">The type of collections to create.</param>
        /// <param name="documents">The documents to ingest</param>
        /// <param name="query">
        /// The callback for the queries.
        /// All the standard arguments will be passed in.
        /// Please make sure that this function is idempotent, since a collection will be reused for each connection mode.
        /// </param>
        /// <param name="documentClientFactory">
        /// The callback for the create DocumentClient. This is invoked for the different ConnectionModes that the query is targeting.
        /// If DocumentClient instantiated by this does not apply the expected ConnectionMode, an assert is thrown.
        /// </param>
        /// <param name="partitionKey">The partition key for the partition collection.</param>
        /// <param name="testArgs">The optional args that you want passed in to the query.</param>
        /// <returns>A task to await on.</returns>
        private static async Task CreateIngestQueryDelete<T>(
           ConnectionModes connectionModes,
           CollectionTypes collectionTypes,
           IEnumerable<string> documents,
           Query<T> query,
           DocumentClientFactory documentClientFactory,
           T testArgs,
           string partitionKey = "/id",
           IndexingPolicy indexingPolicy = null)
        {
            int retryCount = 5;
            bool passed = false;
            List<Exception> exceptionHistory = new List<Exception>();
            while (retryCount-- > 0)
            {
                try
                {
                    List<Task<Tuple<DocumentCollection, List<Document>>>> createDocumentCollectionTasks = new List<Task<Tuple<DocumentCollection, List<Document>>>>();

                    foreach (CollectionTypes collectionType in Enum.GetValues(collectionTypes.GetType()).Cast<Enum>().Where(collectionTypes.HasFlag))
                    {
                        switch (collectionType)
                        {
                            case CollectionTypes.Partitioned:
                                createDocumentCollectionTasks.Add(DocumentCrossPartitionQueryTests.CreatePartitionedCollectionAndIngestDocuments(documents, partitionKey, indexingPolicy));
                                break;
                            case CollectionTypes.NonPartitioned:
                                createDocumentCollectionTasks.Add(DocumentCrossPartitionQueryTests.CreateNonPartitionedCollectionAndIngestDocuments(documents, indexingPolicy));
                                break;
                            case CollectionTypes.None:
                                break;
                            default:
                                throw new ArgumentException($"Unexpected CollectionType {collectionType}");
                        }
                    }

                    Tuple<DocumentCollection, List<Document>>[] collectionsAndDocuments = await Task.WhenAll(createDocumentCollectionTasks);

                    List<DocumentClient> documentClients = new List<DocumentClient>();
                    foreach (ConnectionModes connectionMode in Enum.GetValues(connectionModes.GetType()).Cast<Enum>().Where(connectionModes.HasFlag))
                    {
                        if (connectionMode == ConnectionModes.None)
                        {
                            continue;
                        }

                        ConnectionMode targetConnectionMode = GetTargetConnectionMode(connectionMode);
                        DocumentClient documentClient = documentClientFactory(targetConnectionMode);

                        Assert.AreEqual(targetConnectionMode, documentClient.ConnectionPolicy.ConnectionMode, "Test setup: Invalid connection policy applied to DocumentClient");
                        documentClients.Add(documentClient);
                    }

                    bool succeeded = false;
                    while (!succeeded)
                    {
                        try
                        {
                            List<Task> queryTasks = new List<Task>();
                            foreach (DocumentClient documentClient in documentClients)
                            {
                                foreach (Tuple<DocumentCollection, List<Document>> collectionAndDocuments in collectionsAndDocuments)
                                {
                                    queryTasks.Add(query(documentClient, collectionAndDocuments.Item1, collectionAndDocuments.Item2, testArgs));
                                }
                            }

                            await Task.WhenAll(queryTasks);
                            succeeded = true;
                        }
                        catch (TaskCanceledException)
                        {
                            // SDK throws TaskCanceledException every now and then
                        }
                    }

                    List<Task<ResourceResponse<DocumentCollection>>> deleteCollectionTasks = new List<Task<ResourceResponse<DocumentCollection>>>();
                    foreach (DocumentCollection documentCollection in collectionsAndDocuments.Select(tuple => tuple.Item1))
                    {
                        deleteCollectionTasks.Add(DocumentCrossPartitionQueryTests.Client.DeleteDocumentCollectionAsync(documentCollection));
                    }

                    await Task.WhenAll(deleteCollectionTasks);

                    // If you made it here then it's all good
                    passed = true;
                    break;
                }
                catch (ServiceUnavailableException)
                {
                    throw new AssertInconclusiveException("Test was inconclusive since all service was unavailable.");
                }
                catch (AggregateException ae) when (ae.InnerException is ServiceUnavailableException)
                {
                    throw new AssertInconclusiveException("Test was inconclusive since all service was unavailable.");
                }
                catch (DocumentClientException)
                {
                    // Add logic for if dce is retryable
                    throw;
                }
                catch (AggregateException ae) when (ae.InnerException is DocumentClientException)
                {
                    // Add logic for if dce is retryable
                    throw;
                }
            }

            Assert.IsTrue(passed, $"Exception History: {string.Join(Environment.NewLine, exceptionHistory)}");
        }

        private static ConnectionMode GetTargetConnectionMode(ConnectionModes connectionMode)
        {
            ConnectionMode targetConnectionMode = ConnectionMode.Gateway;
            switch (connectionMode)
            {
                case ConnectionModes.Gateway:
                    targetConnectionMode = ConnectionMode.Gateway;
                    break;

                case ConnectionModes.Direct:
                    targetConnectionMode = ConnectionMode.Direct;
                    break;

                default:
                    throw new ArgumentException($"Unexpected connection mode: {connectionMode}");
            }

            return targetConnectionMode;
        }

        private static DocumentClient CreateDefaultDocumentClient(ConnectionMode connectionMode)
        {
            switch (connectionMode)
            {
                case ConnectionMode.Gateway:
                    return DocumentCrossPartitionQueryTests.GatewayClient;
                case ConnectionMode.Direct:
                    return DocumentCrossPartitionQueryTests.DirectClient;
                default:
                    throw new ArgumentException($"Unexpected connection mode: {connectionMode}");
            }
        }

        private static DocumentClientFactory CreateDocumentClientFactoryForApiVersion(string apiVersion)
        {
            return (ConnectionMode connectionMode) =>
            {
                string originalApiVersion = GetApiVersion();
                try
                {
                    SetApiVersion(apiVersion);
                    return CreateNewDocumentClient(connectionMode);
                }
                finally
                {
                    SetApiVersion(originalApiVersion);
                }
            };
        }

        private static DocumentClient CreateNewDocumentClient(ConnectionMode connectionMode)
        {
            switch (connectionMode)
            {
                case ConnectionMode.Gateway:
                    return TestCommon.CreateClient(true, defaultConsistencyLevel: ConsistencyLevel.Session);
                case ConnectionMode.Direct:
                    return TestCommon.CreateClient(false, defaultConsistencyLevel: ConsistencyLevel.Session);
                default:
                    throw new ArgumentException($"Unexpected connection mode: {connectionMode}");
            }
        }

        private static async Task<List<T>> QueryWithoutContinuationTokens<T>(DocumentClient documentClient, DocumentCollection documentCollection, string query, FeedOptions feedOptions = null)
        {
            feedOptions = new FeedOptions(feedOptions);
            List<T> results = new List<T>();
            QueryMetrics queryMetrics = QueryMetrics.Zero;
            IDocumentQuery<T> documentQuery = documentClient.CreateDocumentQuery<T>(
                documentCollection,
                query,
                feedOptions).AsDocumentQuery();

            while (documentQuery.HasMoreResults)
            {
                DocumentFeedResponse<T> feedResponse = await documentQuery.ExecuteNextAsync<T>();
                // TODO: Fix for v3
                //foreach (QueryMetrics partitionedQueryMetrics in feedResponse.QueryMetrics.Values)
                //{
                //    queryMetrics += partitionedQueryMetrics;
                //}

                results.AddRange(feedResponse);
            }

            return results;
        }

        private static async Task<List<T>> QueryWithContinuationTokens<T>(DocumentClient documentClient, DocumentCollection documentCollection, string query, FeedOptions feedOptions)
        {
            feedOptions = new FeedOptions(feedOptions);
            List<T> results = new List<T>();
            QueryMetrics queryMetrics = QueryMetrics.Zero;
            string continuationToken = null;
            do
            {
                feedOptions.RequestContinuationToken = continuationToken;
                IDocumentQuery<T> documentQuery = documentClient.CreateDocumentQuery<T>(
                    documentCollection,
                    query,
                    feedOptions).AsDocumentQuery();

                DocumentFeedResponse<T> feedResponse = await documentQuery.ExecuteNextAsync<T>();
                // TODO: Fix for v3
                //foreach (QueryMetrics partitionedQueryMetrics in feedResponse.QueryMetrics.Values)
                //{
                //    queryMetrics += partitionedQueryMetrics;
                //}

                results.AddRange(feedResponse);
                continuationToken = feedResponse.ResponseContinuation;
            } while (continuationToken != null);

            return results;
        }

        private static async Task NoOp()
        {
            await Task.Delay(0);
        }

        [TestMethod]
        [TestCategory("Ignore") /* Used to filter out ignored tests in lab runs */]
        [Owner("brchon")]
        public void CheckThatAllTestsAreRunning()
        {
            // In general I don't want any of these tests being ignored or quarentined.
            // Please work with me if it needs to be.
            // I do not want these tests turned off for being "flaky", since they have been 
            // very stable and if they fail it's because something lower level is probably going wrong.

            Assert.AreEqual(0, typeof(DocumentCrossPartitionQueryTests)
                .GetMethods()
                .Where(method => method.GetCustomAttributes(typeof(TestMethodAttribute), true).Length != 0)
                .Where(method => method.GetCustomAttributes(typeof(TestCategoryAttribute), true).Length != 0)
                .Count(), $"One the {nameof(DocumentCrossPartitionQueryTests)} is not being run.");
        }

        [TestMethod]
        [TestCategory("Ignore")]
        [Owner("brchon")]
        public async Task TestExceptionCatching()
        {
            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete<Exception>(
                ConnectionModes.Direct,
                CollectionTypes.Partitioned,
                DocumentCrossPartitionQueryTests.NoDocuments,
                this.RandomlyThrowException,
                new ServiceUnavailableException());
        }

        private async Task RandomlyThrowException(DocumentClient documentClient, DocumentCollection collection, IEnumerable<Document> documents = null, Exception exception = null)
        {
            await DocumentCrossPartitionQueryTests.NoOp();
            Random random = new Random();
            if (random.Next(0, 2) == 0)
            {
                throw exception;
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestBadQueriesOverMultiplePartitions()
        {
            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned | CollectionTypes.NonPartitioned,
                DocumentCrossPartitionQueryTests.NoDocuments,
                this.TestBadQueriesOverMultiplePartitions);
        }

        [TestMethod]
        [Owner("brchon")]
        public void TestContinuationTokenSerialization()
        {
            CompositeContinuationToken compositeContinuationToken = new CompositeContinuationToken()
            {
                Token = "asdf",
                Range = new Range<string>("asdf", "asdf", false, false),
            };
            string serializedCompositeContinuationToken = JsonConvert.SerializeObject(compositeContinuationToken);
            CompositeContinuationToken deserializedCompositeContinuationToken = JsonConvert.DeserializeObject<CompositeContinuationToken>(serializedCompositeContinuationToken);
            Assert.AreEqual(compositeContinuationToken.Token, deserializedCompositeContinuationToken.Token);
            //Assert.IsTrue(compositeContinuationToken.Range.Equals(deserializedCompositeContinuationToken.Range));

            string orderByItemSerialized = @"{""item"" : 1337 }";
            byte[] bytes = Encoding.UTF8.GetBytes(orderByItemSerialized);
            OrderByItem orderByItem = new OrderByItem(CosmosElement.Create(bytes));
            OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                compositeContinuationToken,
                new List<OrderByItem> { orderByItem },
                "asdf",
                42,
                "asdf");
            string serializedOrderByContinuationToken = JsonConvert.SerializeObject(orderByContinuationToken);
            OrderByContinuationToken deserializedOrderByContinuationToken = JsonConvert.DeserializeObject<OrderByContinuationToken>(serializedOrderByContinuationToken);
            Assert.AreEqual(
                orderByContinuationToken.CompositeContinuationToken.Token,
                deserializedOrderByContinuationToken.CompositeContinuationToken.Token);
            //Assert.IsTrue(
            //    orderByContinuationToken.CompositeContinuationToken.Range.Equals(
            //    deserializedOrderByContinuationToken.CompositeContinuationToken.Range));
            Assert.AreEqual(orderByContinuationToken.Filter, deserializedOrderByContinuationToken.Filter);
            //Assert.AreEqual(orderByContinuationToken.OrderByItems, deserializedOrderByContinuationToken.OrderByItems);
            Assert.AreEqual(orderByContinuationToken.Rid, deserializedOrderByContinuationToken.Rid);
            Assert.AreEqual(orderByContinuationToken.SkipCount, deserializedOrderByContinuationToken.SkipCount);
        }

        private async Task TestBadQueriesOverMultiplePartitions(DocumentClient documentClient, DocumentCollection collection, IEnumerable<Document> documents)
        {
            await DocumentCrossPartitionQueryTests.NoOp();
            try
            {
                documentClient.CreateDocumentQuery<Document>(
                    collection.AltLink,
                    @"SELECT * FROM Root r WHERE a = 1",
                    new FeedOptions { EnableCrossPartitionQuery = true }).ToList();

                Assert.Fail("Expected DocumentClientException");
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

                if (!exception.Message.StartsWith("Message: {\"errors\":[{\"severity\":\"Error\",\"location\":{\"start\":27,\"end\":28},\"code\":\"SC2001\",\"message\":\"Identifier 'a' could not be resolved.\"}]}"))
                {
                    throw e;
                }
            }
        }

        /// <summary>
        //"SELECT c._ts, c.id, c.TicketNumber, c.PosCustomerNumber, c.CustomerId, c.CustomerUserId, c.ContactEmail, c.ContactPhone, c.StoreCode, c.StoreUid, c.PoNumber, c.OrderPlacedOn, c.OrderType, c.OrderStatus, c.Customer.UserFirstName, c.Customer.UserLastName, c.Customer.Name, c.UpdatedBy, c.UpdatedOn, c.ExpirationDate, c.TotalAmountFROM c ORDER BY c._ts"' created an ArgumentOutofRangeException since ServiceInterop was returning DISP_E_BUFFERTOOSMALL in the case of an invalid query that is also really long.
        /// This test case just double checks that you get the appropriate document client exception instead of just failing.
        /// </summary>
        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryCrossParitionPartitionProviderInvalid()
        {
            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned | CollectionTypes.NonPartitioned,
                DocumentCrossPartitionQueryTests.NoDocuments,
                this.TestQueryCrossParitionPartitionProviderInvalid);
        }

        private async Task TestQueryCrossParitionPartitionProviderInvalid(DocumentClient documentClient, DocumentCollection collection, IEnumerable<Document> documents)
        {
            await DocumentCrossPartitionQueryTests.NoOp();
            try
            {
                /// note that there is no space before the from clause thus this query should fail 
                /// '"code":"SC2001","message":"Identifier 'c' could not be resolved."'
                string query = "SELECT c._ts, c.id, c.TicketNumber, c.PosCustomerNumber, c.CustomerId, c.CustomerUserId, c.ContactEmail, c.ContactPhone, c.StoreCode, c.StoreUid, c.PoNumber, c.OrderPlacedOn, c.OrderType, c.OrderStatus, c.Customer.UserFirstName, c.Customer.UserLastName, c.Customer.Name, c.UpdatedBy, c.UpdatedOn, c.ExpirationDate, c.TotalAmountFROM c ORDER BY c._ts";
                List<Document> expectedValues;
                expectedValues = documentClient.CreateDocumentQuery<Document>(
                    collection,
                    query,
                    new FeedOptions { MaxDegreeOfParallelism = 0, EnableCrossPartitionQuery = true }).ToList();

                Assert.Fail("Expected to get an exception for this query.");
            }
            catch (AggregateException e)
            {
                bool gotBadRequest = false;
                foreach (Exception inner in e.InnerExceptions)
                {
                    if (inner is BadRequestException)
                    {
                        gotBadRequest = true;
                    }

                    DocumentClientException dce = inner as DocumentClientException;
                    if (dce != null && dce.StatusCode == HttpStatusCode.BadRequest)
                    {
                        gotBadRequest = true;
                    }
                }

                Assert.IsTrue(gotBadRequest);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestForRaceConditions()
        {
            int numberOfDocuments = 1000;
            List<string> documents = new List<string>();
            for (int i = 0; i < numberOfDocuments; i++)
            {
                Document doc = new Document();
                string id = i.ToString("D8");
                doc.SetPropertyValue("id", id);
                documents.Add(doc.ToString());
            }

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete<int>(
                ConnectionModes.Direct,
                CollectionTypes.Partitioned,
                documents,
                this.TestForRaceConditions,
                numberOfDocuments);
        }

        private async Task TestForRaceConditions(
            DocumentClient documentClient,
            DocumentCollection documentCollection,
            IEnumerable<Document> documents,
            int numberOfDocuments)
        {
            FeedOptions feedOptions = new FeedOptions()
            {
                MaxBufferedItemCount = 50,
                MaxItemCount = 10,
                MaxDegreeOfParallelism = 10,
                EnableCrossPartitionQuery = true,
            };

            for (int iteration = 0; iteration < 100; iteration++)
            {
                foreach (string query in new string[]
                {
                    "SELECT * FROM c",
                    $"SELECT TOP {numberOfDocuments} * FROM c",
                    "SELECT * FROM c ORDER BY c._ts",
                })
                {
                    List<Document> results = await QueryWithoutContinuationTokens<Document>(
                        documentClient,
                        documentCollection,
                        query,
                        feedOptions);

                    Assert.IsTrue(results.Count == numberOfDocuments);

                    HashSet<string> ids = new HashSet<string>(results.Select(document => document.Id));
                    Assert.IsTrue(ids.Count == numberOfDocuments);
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryAndReadFeedWithPartitionKey()
        {
            string[] documents = new[]
            {
                @"{""id"":""documentId1"",""key"":""A"",""prop"":3,""shortArray"":[{""a"":5}]}",
                @"{""id"":""documentId2"",""key"":""A"",""prop"":2,""shortArray"":[{""a"":6}]}",
                @"{""id"":""documentId3"",""key"":""A"",""prop"":1,""shortArray"":[{""a"":7}]}",
                @"{""id"":""documentId4"",""key"":5,""prop"":3,""shortArray"":[{""a"":5}]}",
                @"{""id"":""documentId5"",""key"":5,""prop"":2,""shortArray"":[{""a"":6}]}",
                @"{""id"":""documentId6"",""key"":5,""prop"":1,""shortArray"":[{""a"":7}]}",
                @"{""id"":""documentId7"",""key"":null,""prop"":3,""shortArray"":[{""a"":5}]}",
                @"{""id"":""documentId8"",""key"":null,""prop"":2,""shortArray"":[{""a"":6}]}",
                @"{""id"":""documentId9"",""key"":null,""prop"":1,""shortArray"":[{""a"":7}]}",
                @"{""id"":""documentId10"",""prop"":3,""shortArray"":[{""a"":5}]}",
                @"{""id"":""documentId11"",""prop"":2,""shortArray"":[{""a"":6}]}",
                @"{""id"":""documentId12"",""prop"":1,""shortArray"":[{""a"":7}]}",
            };

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned,
                documents,
                this.TestQueryAndReadFeedWithPartitionKey,
                "/key");
        }

        private async Task TestQueryAndReadFeedWithPartitionKey(DocumentClient documentClient, DocumentCollection collection, IEnumerable<Document> documents)
        {
            // Read feed 1
            ResourceFeedReader<Document> feedReader = documentClient.CreateDocumentFeedReader(collection, new FeedOptions { MaxItemCount = 1 });
            string[] enumerable1 = feedReader.Select(doc => doc.Id).OrderBy(id => id).ToArray();
            string[] enumerable2 = documents.Select(doc => doc.Id).OrderBy(id => id).ToArray();
            Assert.IsTrue(enumerable1.SequenceEqual(enumerable2));

            // Read feed 2
            FeedOptions options = new FeedOptions { MaxItemCount = 1 };
            DocumentFeedResponse<dynamic> response = null;
            List<dynamic> result = new List<dynamic>();
            do
            {
                response = await documentClient.ReadDocumentFeedAsync(collection, options);
                result.AddRange(response);
                options.RequestContinuationToken = response.ResponseContinuation;
            } while (!string.IsNullOrEmpty(options.RequestContinuationToken));

            string[] enumerable3 = result.Select<dynamic, string>(doc => doc.id.ToString()).OrderBy(id => id).ToArray();
            string[] enumerable4 = documents.Select<Document, string>(doc => doc.Id.ToString()).OrderBy(id => id).ToArray();
            Assert.IsTrue(enumerable3.SequenceEqual(enumerable4));

            Assert.AreEqual(0, documentClient.CreateDocumentQuery<Document>(
                collection.AltLink,
                @"SELECT * FROM Root r WHERE false").ToList().Count);

            Assert.AreEqual(0, documentClient.CreateDocumentQuery<Document>(
                collection.AltLink,
                @"SELECT * FROM Root r WHERE false",
                new FeedOptions { EnableCrossPartitionQuery = true }).ToList().Count);

            object[] keys = new object[] { "A", 5, null, Undefined.Value };
            for (int i = 0; i < keys.Length; ++i)
            {
                List<string> expected = documents.Skip(i * 3).Take(3).Select(doc => doc.Id).ToList();
                string expectedResult = string.Join(",", expected);

                DocumentFeedResponse<dynamic> feed = await documentClient.ReadDocumentFeedAsync(
                    collection.AltLink,
                    new FeedOptions { PartitionKey = new PartitionKey(keys[i]), MaxItemCount = 3 });
                Assert.AreEqual(expectedResult, string.Join(",", feed.ToList().Select(doc => doc.Id)));

                IQueryable<Document> query = documentClient.CreateDocumentQuery<Document>(
                    collection.AltLink,
                    new FeedOptions { PartitionKey = new PartitionKey(keys[i]), MaxItemCount = 1 });
                Assert.AreEqual(expectedResult, string.Join(",", query.ToList().Select(doc => doc.Id)));

                query = documentClient.CreateDocumentQuery<Document>(
                    collection.AltLink,
                    $@"SELECT * FROM Root r WHERE r.id IN (""{expected[0]}"", ""{expected[1]}"", ""{expected[2]}"")",
                    new FeedOptions { PartitionKey = new PartitionKey(keys[i]), MaxItemCount = 1 });
                Assert.AreEqual(expectedResult, string.Join(",", query.ToList().Select(doc => doc.Id)));

                query = documentClient.CreateDocumentQuery<Document>(
                    collection.AltLink,
                    @"SELECT * FROM Root r WHERE r.prop BETWEEN 1 AND 3",
                    new FeedOptions { PartitionKey = new PartitionKey(keys[i]), MaxItemCount = 1 });
                Assert.AreEqual(expectedResult, string.Join(",", query.ToList().Select(doc => doc.Id)));

                query = documentClient.CreateDocumentQuery<Document>(
                    collection.AltLink,
                    @"SELECT VALUE r FROM Root r JOIN c IN r.shortArray WHERE c.a BETWEEN 5 and 7",
                    new FeedOptions { PartitionKey = new PartitionKey(keys[i]), MaxItemCount = 1 });
                Assert.AreEqual(expectedResult, string.Join(",", query.ToList().Select(doc => doc.Id)));

                // TOP
                query = documentClient.CreateDocumentQuery<Document>(
                    collection.AltLink,
                    $@"SELECT TOP 10 * FROM Root r WHERE r.id IN (""{expected[0]}"", ""{expected[1]}"", ""{expected[2]}"")",
                    new FeedOptions { PartitionKey = new PartitionKey(keys[i]), MaxItemCount = 1 });
                Assert.AreEqual(expectedResult, string.Join(",", query.ToList().Select(doc => doc.Id)));

                query = documentClient.CreateDocumentQuery<Document>(
                    collection.AltLink,
                    @"SELECT TOP 10 * FROM Root r WHERE r.prop BETWEEN 1 AND 3",
                    new FeedOptions { PartitionKey = new PartitionKey(keys[i]), MaxItemCount = 1 });
                Assert.AreEqual(expectedResult, string.Join(",", query.ToList().Select(doc => doc.Id)));

                query = documentClient.CreateDocumentQuery<Document>(
                    collection.AltLink,
                    @"SELECT TOP 10 VALUE r FROM Root r JOIN c IN r.shortArray WHERE c.a BETWEEN 5 and 7",
                    new FeedOptions { PartitionKey = new PartitionKey(keys[i]), MaxItemCount = 1 });
                Assert.AreEqual(expectedResult, string.Join(",", query.ToList().Select(doc => doc.Id)));

                // Order-by
                expected.Reverse();
                expectedResult = string.Join(",", expected);

                query = documentClient.CreateDocumentQuery<Document>(
                    collection.AltLink,
                    $@"SELECT * FROM Root r WHERE r.id IN (""{expected[0]}"", ""{expected[1]}"", ""{expected[2]}"") ORDER BY r.prop",
                    new FeedOptions { PartitionKey = new PartitionKey(keys[i]), MaxItemCount = 1 });
                Assert.AreEqual(expectedResult, string.Join(",", query.ToList().Select(doc => doc.Id)));

                query = documentClient.CreateDocumentQuery<Document>(
                    collection.AltLink,
                    @"SELECT * FROM Root r WHERE r.prop BETWEEN 1 AND 3 ORDER BY r.prop",
                    new FeedOptions { PartitionKey = new PartitionKey(keys[i]), MaxItemCount = 1 });
                Assert.AreEqual(expectedResult, string.Join(",", query.ToList().Select(doc => doc.Id)));

                query = documentClient.CreateDocumentQuery<Document>(
                    collection.AltLink,
                    @"SELECT VALUE r FROM Root r JOIN c IN r.shortArray WHERE c.a BETWEEN 5 and 7 ORDER BY r.prop",
                    new FeedOptions { PartitionKey = new PartitionKey(keys[i]), MaxItemCount = 1 });
                Assert.AreEqual(expectedResult, string.Join(",", query.ToList().Select(doc => doc.Id)));

                if (i < keys.Length - 1)
                {
                    string key;
                    if (keys[i] is string)
                    {
                        key = "'" + keys[i].ToString() + "'";
                    }
                    else if (keys[i] == null)
                    {
                        key = "null";
                    }
                    else
                    {
                        key = keys[i].ToString();
                    }

                    query = documentClient.CreateDocumentQuery<Document>(
                        collection.AltLink,
                        string.Format(CultureInfo.InvariantCulture, @"SELECT * FROM Root r WHERE r.key = {0} ORDER BY r.prop", key),
                        new FeedOptions { MaxItemCount = 1 });
                    Assert.AreEqual(expectedResult, string.Join(",", query.ToList().Select(doc => doc.Id)));
                }
                else
                {
                    // $TODO-elasticcollection-felixfan-2016-01-08: Cannot route implicit undefined key now
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryMultiplePartitionsSinglePartitionKey()
        {
            string[] documents = new[]
            {
                @"{""pk"":""doc1""}",
                @"{""pk"":""doc2""}",
                @"{""pk"":""doc3""}",
                @"{""pk"":""doc4""}",
                @"{""pk"":""doc5""}",
                @"{""pk"":""doc6""}",
            };

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned,
                documents,
                this.TestQueryMultiplePartitionsSinglePartitionKey,
                "/pk");
        }

        private async Task TestQueryMultiplePartitionsSinglePartitionKey(DocumentClient documentClient, DocumentCollection collection, IEnumerable<Document> documents)
        {
            // Query with partition key should be done in one roundtrip.
            IDocumentQuery<dynamic> query = documentClient.CreateDocumentQuery(collection.SelfLink, "SELECT * FROM c WHERE c.pk = 'doc5'").AsDocumentQuery();
            DocumentFeedResponse<dynamic> response = await query.ExecuteNextAsync();
            Assert.AreEqual(1, response.Count);
            Assert.IsNull(response.ResponseContinuation);

            query = documentClient.CreateDocumentQuery(collection.SelfLink, "SELECT * FROM c WHERE c.pk = 'doc10'").AsDocumentQuery();
            response = await query.ExecuteNextAsync();
            Assert.AreEqual(0, response.Count);
            Assert.IsNull(response.ResponseContinuation);
        }

        private struct QueryWithSpecialPartitionKeysArgs
        {
            public string Name;
            public object Value;
            public Func<object, object> ValueToPartitionKey;
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryWithSpecialPartitionKeys()
        {
            await DocumentCrossPartitionQueryTests.NoOp();
            QueryWithSpecialPartitionKeysArgs[] queryWithSpecialPartitionKeyArgsList = new QueryWithSpecialPartitionKeysArgs[]
            {
                new QueryWithSpecialPartitionKeysArgs()
                {
                    Name = "Guid",
                    Value = Guid.NewGuid(),
                    ValueToPartitionKey = val => val.ToString(),
                },
                new QueryWithSpecialPartitionKeysArgs()
                {
                    Name = "DateTime",
                    Value = DateTime.Now,
                    ValueToPartitionKey = val =>
                    {
                        string str = JsonConvert.SerializeObject(
                            val,
                            new JsonSerializerSettings()
                            {
                                Converters = new List<JsonConverter> { new IsoDateTimeConverter() }
                            });
                        return str.Substring(1, str.Length - 2);
                    },
                },
                new QueryWithSpecialPartitionKeysArgs()
                {
                    Name = "Enum",
                    Value = HttpStatusCode.OK,
                    ValueToPartitionKey = val => (int)val,
                },
                new QueryWithSpecialPartitionKeysArgs()
                {
                    Name = "CustomEnum",
                    Value = HttpStatusCode.OK,
                    ValueToPartitionKey = val => val.ToString(),
                },
                new QueryWithSpecialPartitionKeysArgs()
                {
                    Name = "ResourceId",
                    Value = "testid",
                    ValueToPartitionKey = val => val,
                },
                new QueryWithSpecialPartitionKeysArgs()
                {
                    Name = "CustomDateTime",
                    Value = new DateTime(2016, 11, 12),
                    ValueToPartitionKey = val => EpochDateTimeConverter.DateTimeToEpoch((DateTime)val),
                },
            };

            foreach (QueryWithSpecialPartitionKeysArgs testArg in queryWithSpecialPartitionKeyArgsList)
            {
                // For this test we need to split direct and gateway runs into seperate collections,
                // since the query callback inserts some documents (thus has side effects).
                await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete<QueryWithSpecialPartitionKeysArgs>(
                    ConnectionModes.Direct,
                    CollectionTypes.Partitioned,
                    DocumentCrossPartitionQueryTests.NoDocuments,
                    this.TestQueryWithSpecialPartitionKeys,
                    testArg,
                    "/" + testArg.Name);

                await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete<QueryWithSpecialPartitionKeysArgs>(
                    ConnectionModes.Gateway,
                    CollectionTypes.Partitioned,
                    DocumentCrossPartitionQueryTests.NoDocuments,
                    this.TestQueryWithSpecialPartitionKeys,
                    testArg,
                    "/" + testArg.Name);
            }
        }

        private async Task TestQueryWithSpecialPartitionKeys(DocumentClient documentClient, DocumentCollection collection, IEnumerable<Document> documents, QueryWithSpecialPartitionKeysArgs testArgs)
        {
            QueryWithSpecialPartitionKeysArgs args = testArgs;
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.Converters.Add(new StringEnumConverter());
            settings.Converters.Add(new IsoDateTimeConverter());

            SpecialPropertyDocument specialPropertyDocument = new SpecialPropertyDocument();
            specialPropertyDocument.GetType().GetProperty(args.Name).SetValue(specialPropertyDocument, args.Value);
            Func<SpecialPropertyDocument, object> getPropertyValueFunction = d => d.GetType().GetProperty(args.Name).GetValue(d);

            ResourceResponse<Document> response = await Client.CreateDocumentAsync(collection, Document.FromObject(specialPropertyDocument));
            dynamic returnedDoc = response.Resource;
            Assert.AreEqual(args.Value, getPropertyValueFunction((SpecialPropertyDocument)returnedDoc));

            PartitionKey key = new PartitionKey(args.ValueToPartitionKey(args.Value));
            response = await Client.ReadDocumentAsync(returnedDoc.SelfLink, new RequestOptions { PartitionKey = key });
            returnedDoc = response.Resource;
            Assert.AreEqual(args.Value, getPropertyValueFunction((SpecialPropertyDocument)returnedDoc));

            returnedDoc = Client.CreateDocumentQuery<Document>(collection, new FeedOptions { PartitionKey = key }).AsEnumerable().Single();
            Assert.AreEqual(args.Value, getPropertyValueFunction((SpecialPropertyDocument)returnedDoc));

            returnedDoc = Client.CreateDocumentQuery<Document>(
                collection,
                $"SELECT * FROM r WHERE r.{args.Name} = {JsonConvert.SerializeObject(args.ValueToPartitionKey(args.Value), settings)}"
                ).AsEnumerable().Single();
            Assert.AreEqual(args.Value, getPropertyValueFunction((SpecialPropertyDocument)returnedDoc));

            switch (args.Name)
            {
                case "Guid":
                    returnedDoc = Client.CreateDocumentQuery<SpecialPropertyDocument>(
                        collection)
                        .Where(doc => doc.Guid == (Guid)args.Value)
                        .AsEnumerable()
                        .Single();
                    Assert.AreEqual(args.Value, getPropertyValueFunction(returnedDoc));
                    break;
                case "Enum":
                    returnedDoc = Client.CreateDocumentQuery<SpecialPropertyDocument>(
                        collection)
                        .Where(doc => doc.Enum == (HttpStatusCode)args.Value)
                        .AsEnumerable()
                        .Single();
                    Assert.AreEqual(args.Value, getPropertyValueFunction(returnedDoc));
                    break;
                case "DateTime":
                    returnedDoc = Client.CreateDocumentQuery<SpecialPropertyDocument>(
                        collection)
                        .Where(doc => doc.DateTime == (DateTime)args.Value)
                        .AsEnumerable()
                        .Single();
                    Assert.AreEqual(args.Value, getPropertyValueFunction(returnedDoc));
                    break;
                case "CustomEnum":
                    returnedDoc = Client.CreateDocumentQuery<SpecialPropertyDocument>(
                        collection)
                        .Where(doc => doc.CustomEnum == (HttpStatusCode)args.Value)
                        .AsEnumerable()
                        .Single();
                    Assert.AreEqual(args.Value, getPropertyValueFunction(returnedDoc));
                    break;
                case "ResourceId":
                    returnedDoc = Client.CreateDocumentQuery<SpecialPropertyDocument>(
                        collection)
                        .Where(doc => doc.ResourceId == (string)args.Value)
                        .AsEnumerable()
                        .Single();
                    Assert.AreEqual(args.Value, getPropertyValueFunction(returnedDoc));
                    break;
                case "CustomDateTime":
                    returnedDoc = Client.CreateDocumentQuery<SpecialPropertyDocument>(
                        collection)
                        .Where(doc => doc.CustomDateTime == (DateTime)args.Value)
                        .AsEnumerable()
                        .Single();
                    Assert.AreEqual(args.Value, getPropertyValueFunction(returnedDoc));
                    break;
                default:
                    break;
            }
        }

        private sealed class SpecialPropertyDocument
        {
            public Guid Guid
            {
                get;
                set;
            }

            [JsonConverter(typeof(IsoDateTimeConverter))]
            public DateTime DateTime
            {
                get;
                set;
            }

            [JsonConverter(typeof(EpochDateTimeConverter))]
            public DateTime CustomDateTime
            {
                get;
                set;
            }


            public HttpStatusCode Enum
            {
                get;
                set;
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public HttpStatusCode CustomEnum
            {
                get;
                set;
            }

            public string ResourceId
            {
                get;
                set;
            }
        }

        private sealed class EpochDateTimeConverter : JsonConverter
        {
            public static int DateTimeToEpoch(DateTime dt)
            {
                if (!dt.Equals(DateTime.MinValue))
                {
                    DateTime epoch = new DateTime(1970, 1, 1);
                    TimeSpan epochTimeSpan = dt - epoch;
                    return (int)epochTimeSpan.TotalSeconds;
                }
                else
                {
                    return int.MinValue;
                }
            }

            public override bool CanConvert(Type objectType)
            {
                return true;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.None || reader.TokenType == JsonToken.Null)
                {
                    return null;
                }

                if (reader.TokenType != JsonToken.Integer)
                {
                    throw new Exception(
                        string.Format(
                        CultureInfo.InvariantCulture,
                        "Unexpected token parsing date. Expected Integer, got {0}.",
                        reader.TokenType));
                }

                int seconds = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                return new DateTime(1970, 1, 1).AddSeconds(seconds);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                int seconds;
                if (value is DateTime)
                {
                    seconds = DateTimeToEpoch((DateTime)value);
                }
                else
                {
                    throw new Exception("Expected date object value.");
                }

                writer.WriteValue(seconds);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryCrossPartitionWithPartitionKeyRangeId()
        {
            int numberOfDocuments = 1000;
            List<string> documents = new List<string>();
            for (int i = 0; i < numberOfDocuments; i++)
            {
                Document doc = new Document();
                string id = i.ToString("D8");
                doc.SetPropertyValue("id", id);
                documents.Add(doc.ToString());
            }

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete<string>(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned,
                documents,
                this.TestQueryCrossPartitionWithPartitionKeyRangeId,
                "id",
                "/id");
        }

        private async Task TestQueryCrossPartitionWithPartitionKeyRangeId(DocumentClient documentClient, DocumentCollection collection, IEnumerable<Document> documents, dynamic testArgs = null)
        {
            string partitionKey = testArgs;

            // Figure out the physical partition to set of logical partitions
            IRoutingMapProvider routingMapProvider = await documentClient.GetPartitionKeyRangeCacheAsync();
            Dictionary<string, HashSet<string>> partitionKeyRangeIdToIdMap = new Dictionary<string, HashSet<string>>();
            foreach (Document document in documents)
            {
                IReadOnlyList<PartitionKeyRange> targetRanges = await routingMapProvider.TryGetOverlappingRangesAsync(
                collection.ResourceId,
                Range<string>.GetPointRange(
                    PartitionKeyInternal.FromObjectArray(
                        new object[]
                        {
                            document.GetValue<string>(partitionKey)
                        },
                        true).GetEffectivePartitionKeyString(collection.PartitionKey)));
                Debug.Assert(targetRanges.Count == 1);

                string partitionKeyRangeId = targetRanges.First().Id;
                if (!partitionKeyRangeIdToIdMap.ContainsKey(partitionKeyRangeId))
                {
                    partitionKeyRangeIdToIdMap[partitionKeyRangeId] = new HashSet<string>();
                }

                partitionKeyRangeIdToIdMap[partitionKeyRangeId].Add(document.GetValue<string>(partitionKey));
            }

            foreach (KeyValuePair<string, HashSet<string>> kvp in partitionKeyRangeIdToIdMap)
            {
                foreach (bool enableCrossPartitionQuery in new[] { false, true })
                {
                    // Query a particular partitionkeyrangeid and see what logical partitions are in there.
                    HashSet<string> response = new HashSet<string>();
                    using (IDocumentQuery<string> query = documentClient.CreateDocumentQuery<string>(
                        collection,
                        $"SELECT VALUE r.{partitionKey} FROM r",
                        new FeedOptions
                        {
                            MaxItemCount = (int)1e3,
                            PartitionKeyRangeId = kvp.Key,
                            EnableCrossPartitionQuery = enableCrossPartitionQuery
                        }).AsDocumentQuery())
                    {
                        while (query.HasMoreResults)
                        {
                            foreach (string item in await query.ExecuteNextAsync())
                            {
                                response.Add(item);
                            }
                        }
                    }

                    Assert.IsTrue(
                        kvp.Value.SetEquals(response),
                        $"expected: {JsonConvert.SerializeObject(kvp.Value)}, actual: {JsonConvert.SerializeObject(response)}, enableCrossPartitionQuery: {enableCrossPartitionQuery}");
                }
            }
        }

        private struct QueryCrossPartitionWithLargeNumberOfKeysArgs
        {
            public int NumberOfDocuments;
            public string PartitionKey;
            public HashSet<int> ExpectedPartitionKeyValues;
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryCrossPartitionWithLargeNumberOfKeys()
        {
            int numberOfDocuments = 1000;
            string partitionKey = "key";
            HashSet<int> expectedPartitionKeyValues = new HashSet<int>();
            List<string> documents = new List<string>();
            for (int i = 0; i < numberOfDocuments; i++)
            {
                Document doc = new Document();
                doc.SetPropertyValue(partitionKey, i);
                documents.Add(doc.ToString());

                expectedPartitionKeyValues.Add(i);
            }

            Assert.AreEqual(numberOfDocuments, expectedPartitionKeyValues.Count);

            QueryCrossPartitionWithLargeNumberOfKeysArgs args = new QueryCrossPartitionWithLargeNumberOfKeysArgs()
            {
                NumberOfDocuments = numberOfDocuments,
                PartitionKey = partitionKey,
                ExpectedPartitionKeyValues = expectedPartitionKeyValues,
            };

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete<QueryCrossPartitionWithLargeNumberOfKeysArgs>(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned,
                documents,
                this.TestQueryCrossPartitionWithLargeNumberOfKeys,
                args,
                "/" + partitionKey);
        }

        private async Task TestQueryCrossPartitionWithLargeNumberOfKeys(DocumentClient documentClient, DocumentCollection documentCollection, IEnumerable<Document> documents, QueryCrossPartitionWithLargeNumberOfKeysArgs args)
        {
            SqlQuerySpec query = new SqlQuerySpec(
                $"SELECT VALUE r.{args.PartitionKey} FROM r WHERE ARRAY_CONTAINS(@keys, r.{args.PartitionKey})",
                new SqlParameterCollection
                {
                    new SqlParameter
                    {
                        Name = "@keys",
                        Value = args.ExpectedPartitionKeyValues,
                    }
                });

            HashSet<int> actualPartitionKeyValues = new HashSet<int>();
            using (IDocumentQuery<int> documentQuery = Client.CreateDocumentQuery<int>(
                documentCollection,
                query,
                new FeedOptions
                {
                    MaxItemCount = -1,
                    MaxDegreeOfParallelism = 100,
                    EnableCrossPartitionQuery = true,
                }).AsDocumentQuery())
            {
                while (documentQuery.HasMoreResults)
                {
                    DocumentFeedResponse<int> response = await documentQuery.ExecuteNextAsync<int>();
                    foreach (int item in response)
                    {
                        actualPartitionKeyValues.Add(item);
                    }
                }
            }

            Assert.IsTrue(actualPartitionKeyValues.SetEquals(args.ExpectedPartitionKeyValues));
        }

        private struct AggregateTestArgs
        {
            public int NumberOfDocumentsDifferentPartitionKey;
            public int NumberOfDocsWithSamePartitionKey;
            public string PartitionKey;
            public string UniquePartitionKey;
            public string Field;
            public object[] Values;
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryCrossPartitionAggregateFunctions()
        {
            AggregateTestArgs aggregateTestArgs = new AggregateTestArgs()
            {
                NumberOfDocsWithSamePartitionKey = 100,
                NumberOfDocumentsDifferentPartitionKey = 100,
                PartitionKey = "key",
                UniquePartitionKey = "uniquePartitionKey",
                Field = "field",
                Values = new object[] { null, false, true, "abc", "cdfg", "opqrs", "ttttttt", "xyz" },
            };

            List<string> documents = new List<string>(aggregateTestArgs.NumberOfDocumentsDifferentPartitionKey + aggregateTestArgs.NumberOfDocsWithSamePartitionKey);
            foreach (object val in aggregateTestArgs.Values)
            {
                Document doc;
                if (val == null)
                {
                    doc = JsonSerializable.LoadFrom<Document>(
                        new MemoryStream(Encoding.UTF8.GetBytes(string.Format(CultureInfo.InvariantCulture, "{{'{0}':null}}", aggregateTestArgs.PartitionKey))));
                }
                else
                {
                    doc = new Document();
                    doc.SetPropertyValue(aggregateTestArgs.PartitionKey, val);
                }

                documents.Add(doc.ToString());
            }


            for (int i = 0; i < aggregateTestArgs.NumberOfDocsWithSamePartitionKey; ++i)
            {
                Document doc = new Document();
                doc.SetPropertyValue(aggregateTestArgs.PartitionKey, aggregateTestArgs.UniquePartitionKey);
                doc.ResourceId = i.ToString(CultureInfo.InvariantCulture);
                doc.SetPropertyValue(aggregateTestArgs.Field, i + 1);

                documents.Add(doc.ToString());
            }

            for (int i = 0; i < aggregateTestArgs.NumberOfDocumentsDifferentPartitionKey; ++i)
            {
                Document doc = new Document();
                doc.SetPropertyValue(aggregateTestArgs.PartitionKey, i + 1);
                documents.Add(doc.ToString());
            }

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete<AggregateTestArgs>(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned | CollectionTypes.NonPartitioned,
                documents,
                this.TestQueryCrossPartitionAggregateFunctionsAsync,
                aggregateTestArgs,
                "/" + aggregateTestArgs.PartitionKey);
        }

        private struct AggregateQueryArguments
        {
            public string AggregateOperator;
            public object ExpectedValue;
            public string Predicate;
        }

        private async Task TestQueryCrossPartitionAggregateFunctionsAsync(DocumentClient documentClient, DocumentCollection collection, IEnumerable<Document> documents, AggregateTestArgs aggregateTestArgs)
        {
            int numberOfDocumentsDifferentPartitionKey = aggregateTestArgs.NumberOfDocumentsDifferentPartitionKey;
            int numberOfDocumentSamePartitionKey = aggregateTestArgs.NumberOfDocsWithSamePartitionKey;
            int numberOfDocuments = aggregateTestArgs.NumberOfDocumentsDifferentPartitionKey + aggregateTestArgs.NumberOfDocsWithSamePartitionKey;
            object[] values = aggregateTestArgs.Values;
            string partitionKey = aggregateTestArgs.PartitionKey;

            double samePartitionSum = numberOfDocumentSamePartitionKey * (numberOfDocumentSamePartitionKey + 1) / 2;
            double differentPartitionSum = numberOfDocumentsDifferentPartitionKey * (numberOfDocumentsDifferentPartitionKey + 1) / 2;
            double partitionSum = samePartitionSum + differentPartitionSum;
            AggregateQueryArguments[] aggregateQueryArgumentsList = new AggregateQueryArguments[]
            {
                new AggregateQueryArguments()
                {
                    AggregateOperator = "AVG",
                    ExpectedValue = partitionSum / numberOfDocuments,
                    Predicate = $"IS_NUMBER(r.{partitionKey})",
                },
                new AggregateQueryArguments()
                {
                    AggregateOperator = "AVG",
                    ExpectedValue = Undefined.Value,
                    Predicate = "true",
                },
                new AggregateQueryArguments()
                {
                    AggregateOperator = "COUNT",
                    ExpectedValue = (long)numberOfDocuments + values.Length,
                    Predicate = "true",
                },
                new AggregateQueryArguments()
                {
                    AggregateOperator = "MAX",
                    ExpectedValue = "xyz",
                    Predicate = "true",
                },
                new AggregateQueryArguments()
                {
                    AggregateOperator = "MIN",
                    ExpectedValue = null,
                    Predicate = "true",
                },
                new AggregateQueryArguments()
                {
                    AggregateOperator = "SUM",
                    ExpectedValue = differentPartitionSum,
                    Predicate = $"IS_NUMBER(r.{partitionKey})",
                },
                new AggregateQueryArguments()
                {
                    AggregateOperator = "SUM",
                    ExpectedValue = Undefined.Value,
                    Predicate = $"true",
                },
            };

            foreach (int maxDoP in new[] { 0, 10 })
            {
                foreach (AggregateQueryArguments argument in aggregateQueryArgumentsList)
                {
                    string[] queryFormats = new[]
                    {
                        "SELECT VALUE {0}(r.{1}) FROM r WHERE {2}",
                        "SELECT VALUE {0}(r.{1}) FROM r WHERE {2} ORDER BY r.{1}"
                    };

                    foreach (string queryFormat in queryFormats)
                    {
                        string query = string.Format(CultureInfo.InvariantCulture, queryFormat, argument.AggregateOperator, partitionKey, argument.Predicate);
                        string message = string.Format(CultureInfo.InvariantCulture, "query: {0}, data: {1}", query, JsonConvert.SerializeObject(argument));
                        List<dynamic> items = documentClient.CreateDocumentQuery(
                            collection,
                            query,
                            new FeedOptions { EnableCrossPartitionQuery = true, MaxDegreeOfParallelism = maxDoP }).ToList();

                        if (Undefined.Value.Equals(argument.ExpectedValue))
                        {
                            Assert.AreEqual(0, items.Count, message);
                        }
                        else
                        {
                            object expected = argument.ExpectedValue;
                            object actual = items.Single();

                            if (expected is long)
                            {
                                expected = (double)((long)expected);
                            }

                            if (actual is long)
                            {
                                actual = (double)((long)actual);
                            }

                            Assert.AreEqual(expected, actual, message);
                        }
                    }
                }

                // Single partition queries
                double singlePartitionSum = samePartitionSum;
                Tuple<string, object>[] datum = new[]
                {
                    Tuple.Create<string, object>("AVG", singlePartitionSum / numberOfDocumentSamePartitionKey),
                    Tuple.Create<string, object>("COUNT", (long)numberOfDocumentSamePartitionKey),
                    Tuple.Create<string, object>("MAX", (long)numberOfDocumentSamePartitionKey),
                    Tuple.Create<string, object>("MIN", (long)1),
                    Tuple.Create<string, object>("SUM", (long)singlePartitionSum),
                };

                string field = aggregateTestArgs.Field;
                string uniquePartitionKey = aggregateTestArgs.UniquePartitionKey;
                foreach (Tuple<string, object> data in datum)
                {
                    string query = $"SELECT VALUE {data.Item1}(r.{field}) FROM r WHERE r.{partitionKey} = '{uniquePartitionKey}'";
                    dynamic aggregate = documentClient.CreateDocumentQuery(collection, query).ToList().Single();
                    object expected = data.Item2;

                    if (aggregate is long)
                    {
                        aggregate = (long)aggregate;
                    }

                    if (expected is long)
                    {
                        expected = (long)expected;
                    }

                    Assert.AreEqual(
                        expected,
                        aggregate,
                        $"query: {query}, data: {JsonConvert.SerializeObject(data)}");

                    // Make sure ExecuteNextAsync works for unsupported aggregate projection
                    DocumentFeedResponse<dynamic> page = await documentClient
                        .CreateDocumentQuery(collection, query)
                        .AsDocumentQuery()
                        .ExecuteNextAsync();
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryCrossPartitionAggregateFunctionsEmptyPartitions()
        {
            AggregateQueryEmptyPartitionsArgs args = new AggregateQueryEmptyPartitionsArgs()
            {
                NumDocuments = 100,
                PartitionKey = "key",
                UniqueField = "UniqueField",
            };

            List<string> documents = new List<string>(args.NumDocuments);
            for (int i = 0; i < args.NumDocuments; ++i)
            {
                Document doc = new Document();
                doc.SetPropertyValue(args.PartitionKey, Guid.NewGuid());
                doc.SetPropertyValue(args.UniqueField, i);
                documents.Add(doc.ToString());
            }

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete<AggregateQueryEmptyPartitionsArgs>(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned | CollectionTypes.NonPartitioned,
                documents,
                this.TestQueryCrossPartitionAggregateFunctionsEmptyPartitions,
                args,
                "/" + args.PartitionKey);
        }

        private struct AggregateQueryEmptyPartitionsArgs
        {
            public int NumDocuments;
            public string PartitionKey;
            public string UniqueField;
        }

        private async Task TestQueryCrossPartitionAggregateFunctionsEmptyPartitions(DocumentClient documentClient, DocumentCollection collection, IEnumerable<Document> documents, AggregateQueryEmptyPartitionsArgs args)
        {
            await DocumentCrossPartitionQueryTests.NoOp();
            int numDocuments = args.NumDocuments;
            string partitionKey = args.PartitionKey;
            string uniqueField = args.UniqueField;

            // Perform full fanouts but only match a single value that isn't the partition key.
            // This leads to all other partitions returning { "<aggregate>" = UNDEFINDED, "count" = 0 }
            // which should be ignored from the aggregation.
            int valueOfInterest = args.NumDocuments / 2;
            string[] queries = new string[]
            {
                $"SELECT VALUE AVG(c.{uniqueField}) FROM c WHERE c.{uniqueField} = {valueOfInterest}",
                $"SELECT VALUE MIN(c.{uniqueField}) FROM c WHERE c.{uniqueField} = {valueOfInterest}",
                $"SELECT VALUE MAX(c.{uniqueField}) FROM c WHERE c.{uniqueField} = {valueOfInterest}",
                $"SELECT VALUE SUM(c.{uniqueField}) FROM c WHERE c.{uniqueField} = {valueOfInterest}",
            };

            foreach (string query in queries)
            {
                try
                {
                    List<dynamic> items = documentClient.CreateDocumentQuery(
                    collection,
                    query,
                    new FeedOptions
                    {
                        EnableCrossPartitionQuery = true,
                        MaxDegreeOfParallelism = 10
                    }).ToList();
                    Assert.AreEqual(valueOfInterest, items.Single());
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Something went wrong with query: {query}, ex: {ex}");
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryCrossPartitionAggregateFunctionsWithMixedTypes()
        {
            AggregateQueryMixedTypes args = new AggregateQueryMixedTypes()
            {
                PartitionKey = "key",
                Field = "field",
                DoubleOnlyKey = "doubleOnly",
                StringOnlyKey = "stringOnly",
                BoolOnlyKey = "boolOnly",
                NullOnlyKey = "nullOnly",
                ObjectOnlyKey = "objectOnlyKey",
                ArrayOnlyKey = "arrayOnlyKey",
                OneObjectKey = "oneObjectKey",
                OneArrayKey = "oneArrayKey",
                UndefinedKey = "undefinedKey",
            };

            List<string> documents = new List<string>();
            Random random = new Random(1234);
            for (int i = 0; i < 20; ++i)
            {
                Document doubleDoc = new Document();
                doubleDoc.SetPropertyValue(args.PartitionKey, Guid.NewGuid());
                doubleDoc.SetPropertyValue(args.Field, random.Next(1, 100000));
                documents.Add(doubleDoc.ToString());
                doubleDoc.SetPropertyValue(args.PartitionKey, args.DoubleOnlyKey);
                documents.Add(doubleDoc.ToString());

                Document stringDoc = new Document();
                stringDoc.SetPropertyValue(args.PartitionKey, Guid.NewGuid());
                stringDoc.SetPropertyValue(args.Field, random.NextDouble().ToString());
                documents.Add(stringDoc.ToString());
                stringDoc.SetPropertyValue(args.PartitionKey, args.StringOnlyKey);
                documents.Add(stringDoc.ToString());

                Document boolDoc = new Document();
                boolDoc.SetPropertyValue(args.PartitionKey, Guid.NewGuid());
                boolDoc.SetPropertyValue(args.Field, random.Next() % 2 == 0);
                documents.Add(boolDoc.ToString());
                boolDoc.SetPropertyValue(args.PartitionKey, args.BoolOnlyKey);
                documents.Add(boolDoc.ToString());

                Document nullDoc = new Document();
                nullDoc.SetPropertyValue(args.PartitionKey, Guid.NewGuid());
                nullDoc.propertyBag.Add(args.Field, null);
                documents.Add(nullDoc.ToString());
                nullDoc.SetPropertyValue(args.PartitionKey, args.NullOnlyKey);
                documents.Add(nullDoc.ToString());

                Document objectDoc = new Document();
                objectDoc.SetPropertyValue(args.PartitionKey, Guid.NewGuid());
                objectDoc.SetPropertyValue(args.Field, new object { });
                documents.Add(objectDoc.ToString());
                objectDoc.SetPropertyValue(args.PartitionKey, args.ObjectOnlyKey);
                documents.Add(objectDoc.ToString());

                Document arrayDoc = new Document();
                arrayDoc.SetPropertyValue(args.PartitionKey, Guid.NewGuid());
                arrayDoc.SetPropertyValue(args.Field, new object[] { });
                documents.Add(arrayDoc.ToString());
                arrayDoc.SetPropertyValue(args.PartitionKey, args.ArrayOnlyKey);
                documents.Add(arrayDoc.ToString());
            }

            Document oneObjectDoc = new Document();
            oneObjectDoc.SetPropertyValue(args.PartitionKey, args.OneObjectKey);
            oneObjectDoc.SetPropertyValue(args.Field, new object { });
            documents.Add(oneObjectDoc.ToString());

            Document oneArrayDoc = new Document();
            oneArrayDoc.SetPropertyValue(args.PartitionKey, args.OneArrayKey);
            oneArrayDoc.SetPropertyValue(args.Field, new object[] { });
            documents.Add(oneArrayDoc.ToString());

            Document undefinedDoc = new Document();
            undefinedDoc.SetPropertyValue(args.PartitionKey, args.UndefinedKey);
            // This doc does not have the field key set
            documents.Add(undefinedDoc.ToString());

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete<AggregateQueryMixedTypes>(
                ConnectionModes.Direct,
                CollectionTypes.Partitioned,
                documents,
                this.TestQueryCrossPartitionAggregateFunctionsWithMixedTypes,
                args,
                "/" + args.PartitionKey);
        }

        private struct AggregateQueryMixedTypes
        {
            public string PartitionKey;
            public string Field;
            public string DoubleOnlyKey;
            public string StringOnlyKey;
            public string BoolOnlyKey;
            public string NullOnlyKey;
            public string ObjectOnlyKey;
            public string ArrayOnlyKey;
            public string OneObjectKey;
            public string OneArrayKey;
            public string UndefinedKey;
        }

        private async Task TestQueryCrossPartitionAggregateFunctionsWithMixedTypes(DocumentClient documentClient, DocumentCollection collection, IEnumerable<Document> documents, AggregateQueryMixedTypes args)
        {
            await DocumentCrossPartitionQueryTests.NoOp();
            string partitionKey = args.PartitionKey;
            string field = args.Field;
            string[] typeOnlyPartitionKeys = new string[]
            {
                args.DoubleOnlyKey,
                args.StringOnlyKey,
                args.BoolOnlyKey,
                args.NullOnlyKey,
                args.ObjectOnlyKey,
                args.ArrayOnlyKey,
                args.OneArrayKey,
                args.OneObjectKey,
                args.UndefinedKey
            };

            string[] aggregateOperators = new string[] { "AVG", "MIN", "MAX", "SUM", "COUNT" };
            string[] typeCheckFunctions = new string[] { "IS_ARRAY", "IS_BOOL", "IS_NULL", "IS_NUMBER", "IS_OBJECT", "IS_STRING", "IS_DEFINED", "IS_PRIMITIVE" };
            List<string> queries = new List<string>();
            foreach (string aggregateOperator in aggregateOperators)
            {
                foreach (string typeCheckFunction in typeCheckFunctions)
                {
                    queries.Add(
                    $@"
                        SELECT VALUE {aggregateOperator} (c.{field}) 
                        FROM c 
                        WHERE {typeCheckFunction}(c.{field})
                    ");
                }

                foreach (string typeOnlyPartitionKey in typeOnlyPartitionKeys)
                {
                    queries.Add(
                    $@"
                        SELECT VALUE {aggregateOperator} (c.{field}) 
                        FROM c 
                        WHERE c.{partitionKey} = ""{typeOnlyPartitionKey}""
                    ");
                }
            };

            // mixing primitive and non primitives
            foreach (string minmaxop in new string[] { "MIN", "MAX" })
            {
                foreach (string key in new string[] { args.OneObjectKey, args.OneArrayKey })
                {
                    queries.Add(
                    $@"
                        SELECT VALUE {minmaxop} (c.{field}) 
                        FROM c 
                        WHERE c.{partitionKey} IN (""{key}"", ""{args.DoubleOnlyKey}"")
                    ");
                }
            }


            string filename = $"DocumentCrossPartitionQueryTests.AggregateMixedTypes";
            string outputPath = $"{filename}_output.xml";
            string baselinePath = $"{filename}_baseline.xml";
            XmlWriterSettings settings = new XmlWriterSettings()
            {
                OmitXmlDeclaration = true,
                Indent = true,
                NewLineOnAttributes = true,
            };
            using (XmlWriter writer = XmlWriter.Create(outputPath, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Results");
                foreach (string query in queries)
                {
                    string formattedQuery = string.Join(
                        Environment.NewLine,
                        query.Trim().Split(
                            new[] { Environment.NewLine },
                            StringSplitOptions.None)
                            .Select(x => x.Trim()));

                    List<dynamic> items = documentClient.CreateDocumentQuery(
                        collection,
                        query,
                        new FeedOptions
                        {
                            EnableCrossPartitionQuery = true,
                            MaxDegreeOfParallelism = 10
                        }).ToList();
                    writer.WriteStartElement("Result");
                    writer.WriteStartElement("Query");
                    writer.WriteCData(formattedQuery);
                    writer.WriteEndElement();
                    writer.WriteStartElement("Aggregation");
                    if (items.Count > 0)
                    {
                        writer.WriteCData(JsonConvert.SerializeObject(items.Single()));
                    }
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            Assert.AreEqual(File.ReadAllText(baselinePath), File.ReadAllText(outputPath));
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestNonValueAggregates()
        {
            string[] documents = new string[]
            {
                @"{""first"":""Good"",""last"":""Trevino"",""age"":23,""height"":61,""income"":59848}",
                @"{""first"":""Charles"",""last"":""Decker"",""age"":31,""height"":64,""income"":55970}",
                @"{""first"":""Holden"",""last"":""Cotton"",""age"":30,""height"":66,""income"":57075}",
                @"{""first"":""Carlene"",""last"":""Cabrera"",""age"":26,""height"":72,""income"":98018}",
                @"{""first"":""Gates"",""last"":""Spence"",""age"":38,""height"":53,""income"":12338}",
                @"{""first"":""Camacho"",""last"":""Singleton"",""age"":40,""height"":52,""income"":76973}",
                @"{""first"":""Rachel"",""last"":""Tucker"",""age"":27,""height"":68,""income"":28116}",
                @"{""first"":""Kristi"",""last"":""Robertson"",""age"":32,""height"":53,""income"":61687}",
                @"{""first"":""Poole"",""last"":""Petty"",""age"":22,""height"":75,""income"":53381}",
                @"{""first"":""Lacey"",""last"":""Carlson"",""age"":38,""height"":78,""income"":63989}",
                @"{""first"":""Rosario"",""last"":""Mendez"",""age"":21,""height"":64,""income"":20300}",
                @"{""first"":""Estrada"",""last"":""Collins"",""age"":28,""height"":74,""income"":6926}",
                @"{""first"":""Ursula"",""last"":""Burton"",""age"":26,""height"":66,""income"":32870}",
                @"{""first"":""Rochelle"",""last"":""Sanders"",""age"":24,""height"":56,""income"":47564}",
                @"{""first"":""Darcy"",""last"":""Herring"",""age"":27,""height"":52,""income"":67436}",
                @"{""first"":""Carole"",""last"":""Booth"",""age"":34,""height"":60,""income"":50177}",
                @"{""first"":""Cruz"",""last"":""Russell"",""age"":25,""height"":52,""income"":95072}",
                @"{""first"":""Wilma"",""last"":""Robbins"",""age"":36,""height"":50,""income"":53008}",
                @"{""first"":""Mcdaniel"",""last"":""Barlow"",""age"":21,""height"":78,""income"":85441}",
                @"{""first"":""Leann"",""last"":""Blackwell"",""age"":40,""height"":79,""income"":900}",
                @"{""first"":""Hoffman"",""last"":""Hoffman"",""age"":31,""height"":76,""income"":1208}",
                @"{""first"":""Pittman"",""last"":""Shepherd"",""age"":35,""height"":61,""income"":26887}",
                @"{""first"":""Wright"",""last"":""Rojas"",""age"":35,""height"":73,""income"":76487}",
                @"{""first"":""Lynne"",""last"":""Waters"",""age"":27,""height"":60,""income"":22926}",
                @"{""first"":""Corina"",""last"":""Shelton"",""age"":29,""height"":78,""income"":67379}",
                @"{""first"":""Alvarez"",""last"":""Barr"",""age"":29,""height"":59,""income"":34698}",
                @"{""first"":""Melinda"",""last"":""Mccoy"",""age"":24,""height"":63,""income"":69811}",
                @"{""first"":""Chelsea"",""last"":""Bolton"",""age"":20,""height"":63,""income"":47698}",
                @"{""first"":""English"",""last"":""Ingram"",""age"":28,""height"":50,""income"":94977}",
                @"{""first"":""Vance"",""last"":""Thomas"",""age"":30,""height"":49,""income"":67638}",
                @"{""first"":""Howell"",""last"":""Joyner"",""age"":34,""height"":78,""income"":65547}",
                @"{""first"":""Ofelia"",""last"":""Chapman"",""age"":23,""height"":82,""income"":85049}",
                @"{""first"":""Downs"",""last"":""Adams"",""age"":28,""height"":76,""income"":19373}",
                @"{""first"":""Terrie"",""last"":""Bryant"",""age"":32,""height"":55,""income"":79024}",
                @"{""first"":""Jeanie"",""last"":""Carson"",""age"":26,""height"":52,""income"":68293}",
                @"{""first"":""Hazel"",""last"":""Bean"",""age"":40,""height"":70,""income"":46028}",
                @"{""first"":""Dominique"",""last"":""Norman"",""age"":25,""height"":50,""income"":59445}",
                @"{""first"":""Lyons"",""last"":""Patterson"",""age"":36,""height"":64,""income"":71748}",
                @"{""first"":""Catalina"",""last"":""Cantrell"",""age"":30,""height"":78,""income"":16999}",
                @"{""first"":""Craft"",""last"":""Head"",""age"":30,""height"":49,""income"":10542}",
                @"{""first"":""Suzanne"",""last"":""Gilliam"",""age"":36,""height"":77,""income"":7511}",
                @"{""first"":""Pamela"",""last"":""Merritt"",""age"":30,""height"":81,""income"":80653}",
                @"{""first"":""Haynes"",""last"":""Ayala"",""age"":38,""height"":65,""income"":85832}",
                @"{""first"":""Teri"",""last"":""Martin"",""age"":40,""height"":83,""income"":27839}",
                @"{""first"":""Susanne"",""last"":""Short"",""age"":25,""height"":57,""income"":48957}",
                @"{""first"":""Rosalie"",""last"":""Camacho"",""age"":24,""height"":83,""income"":30313}",
                @"{""first"":""Walls"",""last"":""Bray"",""age"":28,""height"":74,""income"":21616}",
                @"{""first"":""Norris"",""last"":""Bates"",""age"":23,""height"":59,""income"":13631}",
                @"{""first"":""Wendy"",""last"":""King"",""age"":38,""height"":48,""income"":19845}",
                @"{""first"":""Deena"",""last"":""Ramsey"",""age"":20,""height"":66,""income"":49665}",
                @"{""first"":""Richmond"",""last"":""Meadows"",""age"":36,""height"":59,""income"":43244}",
                @"{""first"":""Burks"",""last"":""Whitley"",""age"":25,""height"":55,""income"":39974}",
                @"{""first"":""Gilliam"",""last"":""George"",""age"":37,""height"":82,""income"":47114}",
                @"{""first"":""Marcy"",""last"":""Harding"",""age"":33,""height"":80,""income"":20316}",
                @"{""first"":""Curtis"",""last"":""Gomez"",""age"":31,""height"":50,""income"":69085}",
                @"{""first"":""Lopez"",""last"":""Burt"",""age"":34,""height"":79,""income"":37577}",
                @"{""first"":""Nell"",""last"":""Nixon"",""age"":37,""height"":58,""income"":67999}",
                @"{""first"":""Sonja"",""last"":""Lamb"",""age"":37,""height"":53,""income"":92553}",
                @"{""first"":""Owens"",""last"":""Fischer"",""age"":40,""height"":48,""income"":75199}",
                @"{""first"":""Ortega"",""last"":""Padilla"",""age"":28,""height"":55,""income"":29126}",
                @"{""first"":""Stacie"",""last"":""Velez"",""age"":20,""height"":56,""income"":45292}",
                @"{""first"":""Brennan"",""last"":""Craig"",""age"":38,""height"":65,""income"":37445}"
            };
            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.Partitioned,
                documents,
                this.TestNonValueAggregates);
        }

        private async Task TestNonValueAggregates(
            DocumentClient documentClient,
            DocumentCollection documentCollection,
            IEnumerable<Document> documents)
        {
            IEnumerable<JToken> documentsAsJTokens = documents.Select(document => JToken.FromObject(document));

            // ------------------------------------------
            // Positive
            // ------------------------------------------

            List<Tuple<string, JToken>> queryAndExpectedAggregation = new List<Tuple<string, JToken>>()
            {
                // ------------------------------------------
                // Simple Aggregates without a value
                // ------------------------------------------

                new Tuple<string, JToken>(
                    "SELECT SUM(c.age) FROM c",
                    new JObject
                    {
                        {
                            "$1",
                            documentsAsJTokens.Sum(document => document["age"].Value<double>())
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT COUNT(c.age) FROM c",
                    new JObject
                    {
                        {
                            "$1",
                            documentsAsJTokens.Where(document => document["age"] != null).Count()
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT MIN(c.age) FROM c",
                    new JObject
                    {
                        {
                            "$1",
                            documentsAsJTokens.Min(document => document["age"].Value<double>())
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT MAX(c.age) FROM c",
                    new JObject
                    {
                        {
                            "$1",
                            documentsAsJTokens.Max(document => document["age"].Value<double>())
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT AVG(c.age) FROM c",
                    new JObject
                    {
                        {
                            "$1",
                            documentsAsJTokens.Average(document => document["age"].Value<double>())
                        }
                    }),
                
                // ------------------------------------------
                // Simple aggregates with alias
                // ------------------------------------------

                new Tuple<string, JToken>(
                    "SELECT SUM(c.age) as sum_age FROM c",
                    new JObject
                    {
                        {
                            "sum_age",
                            documentsAsJTokens.Sum(document => document["age"].Value<double>())
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT COUNT(c.age) as count_age FROM c",
                    new JObject
                    {
                        {
                            "count_age",
                            documentsAsJTokens.Where(document => document["age"] != null).Count()
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT MIN(c.age) as min_age FROM c",
                    new JObject
                    {
                        {
                            "min_age",
                            documentsAsJTokens.Min(document => document["age"].Value<double>())
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT MAX(c.age) as max_age FROM c",
                    new JObject
                    {
                        {
                            "max_age",
                            documentsAsJTokens.Max(document => document["age"].Value<double>())
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT AVG(c.age) as avg_age FROM c",
                    new JObject
                    {
                        {
                            "avg_age",
                            documentsAsJTokens.Average(document => document["age"].Value<double>())
                        }
                    }),
                
                // ------------------------------------------
                // Multiple Aggregates without alias
                // ------------------------------------------

                new Tuple<string, JToken>(
                    "SELECT MIN(c.age), MAX(c.age) FROM c",
                    new JObject
                    {
                        {
                            "$1",
                            documentsAsJTokens.Min(document => document["age"].Value<double>())
                        },
                        {
                            "$2",
                            documentsAsJTokens.Max(document => document["age"].Value<double>())
                        }
                    }),

                // ------------------------------------------
                // Multiple Aggregates with alias
                // ------------------------------------------

                new Tuple<string, JToken>(
                    "SELECT MIN(c.age) as min_age, MAX(c.age) as max_age FROM c",
                    new JObject
                    {
                        {
                            "min_age",
                            documentsAsJTokens.Min(document => document["age"].Value<double>())
                        },
                        {
                            "max_age",
                            documentsAsJTokens.Max(document => document["age"].Value<double>())
                        }
                    }),

                // ------------------------------------------
                // Multiple Aggregates with and without alias
                // ------------------------------------------

                new Tuple<string, JToken>(
                    "SELECT MIN(c.age), MAX(c.age) as max_age FROM c",
                    new JObject
                    {
                        {
                            "$1",
                            documentsAsJTokens.Min(document => document["age"].Value<double>())
                        },
                        {
                            "max_age",
                            documentsAsJTokens.Max(document => document["age"].Value<double>())
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT MIN(c.age) as min_age, MAX(c.age) FROM c",
                    new JObject
                    {
                        {
                            "min_age",
                            documentsAsJTokens.Min(document => document["age"].Value<double>())
                        },
                        {
                            "$1",
                            documentsAsJTokens.Max(document => document["age"].Value<double>())
                        }
                    }),
            };

            // Test query correctness.
            foreach ((string query, JToken expectedAggregation) in queryAndExpectedAggregation)
            {
                foreach (int maxItemCount in new int[] { 1, 5, 10 })
                {
                    List<JToken> actual = await QueryWithoutContinuationTokens<JToken>(
                        documentClient,
                        documentCollection,
                        query,
                        new FeedOptions()
                        {
                            EnableCrossPartitionQuery = true,
                            MaxItemCount = maxItemCount,
                            MaxBufferedItemCount = 100,
                            MaxDegreeOfParallelism = 100
                        });

                    Assert.AreEqual(1, actual.Count());

                    Assert.IsTrue(
                       CrossPartitionQueryTests.JsonTokenEqualityComparer.Value.Equals(actual.First(), expectedAggregation),
                       $"Results did not match for query: {query} with maxItemCount: {maxItemCount}" +
                       $"Actual: {JsonConvert.SerializeObject(actual.First())}" +
                       $"Expected: {JsonConvert.SerializeObject(expectedAggregation)}");
                }
            }

            // ------------------------------------------
            // Negative
            // ------------------------------------------

            List<string> notSupportedQueries = new List<string>()
            {
                "SELECT MIN(c.age) + MAX(c.age) FROM c",
                "SELECT MIN(c.age) / 2 FROM c",
            };

            foreach (string query in notSupportedQueries)
            {
                try
                {
                    List<JToken> actual = await QueryWithoutContinuationTokens<JToken>(
                        documentClient,
                        documentCollection,
                        query,
                        new FeedOptions()
                        {
                            EnableCrossPartitionQuery = true,
                            MaxBufferedItemCount = 100,
                            MaxDegreeOfParallelism = 100
                        });

                    Assert.Fail("Expected Query To Fail");
                }
                catch (Exception)
                {
                    // Do Nothing
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryDistinct()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;

            Random rand = new Random(seed);
            List<DistinctMapTests.Person> people = new List<DistinctMapTests.Person>();

            for (int i = 0; i < numberOfDocuments; i++)
            {
                DistinctMapTests.Person person = DistinctMapTests.GetRandomPerson(rand);
                for (int j = 0; j < rand.Next(0, 4); j++)
                {
                    people.Add(person);
                }
            }

            List<string> documents = new List<string>();
            people = people.OrderBy((person) => Guid.NewGuid()).ToList();
            foreach (DistinctMapTests.Person person in people)
            {
                documents.Add(JsonConvert.SerializeObject(person));
            }

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned | CollectionTypes.NonPartitioned,
                documents,
                this.TestQueryDistinct,
                "/id");
        }

        private async Task TestQueryDistinct(DocumentClient client, DocumentCollection collection, IEnumerable<Document> documents, dynamic testArgs = null)
        {
            #region Queries
            // To verify distint queries you can run it once without the distinct clause and run it through a hash set 
            // then compare to the query with the distinct clause.
            List<string> queries = new List<string>()
            {
                // basic distinct queries
                "SELECT {0} VALUE null",
                "SELECT {0} VALUE false",
                "SELECT {0} VALUE true",
                "SELECT {0} VALUE 1",
                "SELECT {0} VALUE 'a'",
                "SELECT {0} VALUE [null, true, false, 1, 'a']",
                "SELECT {0} VALUE {{p1:null, p2:true, p3:false, p4:1, p5:'a'}}",
                "SELECT {0} false AS p",
                "SELECT {0} 1 AS p",
                "SELECT {0} 'a' AS p",
                "SELECT {0} [null, true, false, 1, 'a'] AS p",
                "SELECT {0} {{p1:null, p2:true, p3:false, p4:1, p5:'a'}} AS p",
                "SELECT {0} VALUE {{p1:null, p2:true, p3:false, p4:1, p5:'a'}}",
                "SELECT {0} VALUE null FROM c",
                "SELECT {0} VALUE false FROM c",
                "SELECT {0} VALUE 1 FROM c",
                "SELECT {0} VALUE 'a' FROM c",
                "SELECT {0} VALUE [null, true, false, 1, 'a'] FROM c",
                "SELECT {0} null AS p FROM c",
                "SELECT {0} false AS p FROM c",
                "SELECT {0} 1 AS p FROM c",
                "SELECT {0} 'a' AS p FROM c",
                "SELECT {0} [null, true, false, 1, 'a'] AS p FROM c",
                "SELECT {0} {{p1:null, p2:true, p3:false, p4:1, p5:'a'}} AS p FROM c",

                // number value distinct queries
                "SELECT {0} VALUE c.income from c",
                "SELECT {0} VALUE c.age from c",
                "SELECT {0} c.income, c.income AS income2 from c",
                "SELECT {0} c.income, c.age from c",
                "SELECT {0} VALUE [c.income, c.age] from c",

                // string value distinct queries
                "SELECT {0} VALUE c.name from c",
                "SELECT {0} VALUE c.city from c",
                "SELECT {0} VALUE c.partitionKey from c",
                "SELECT {0} c.name, c.name AS name2 from c",
                "SELECT {0} c.name, c.city from c",
                "SELECT {0} VALUE [c.name, c.city] from c",

                // array value distinct queries
                "SELECT {0} VALUE c.children from c",
                "SELECT {0} c.children, c.children AS children2 from c",
                "SELECT {0} VALUE [c.name, c.age, c.pet] from c",

                // object value distinct queries
                "SELECT {0} VALUE c.pet from c",
                "SELECT {0} c.pet, c.pet AS pet2 from c",

                // scalar expressions distinct query
                "SELECT {0} VALUE c.age % 2 FROM c",
                "SELECT {0} VALUE ABS(c.age) FROM c",
                "SELECT {0} VALUE LEFT(c.name, 1) FROM c",
                "SELECT {0} VALUE c.name || ', ' || (c.city ?? '') FROM c",
                "SELECT {0} VALUE ARRAY_LENGTH(c.children) FROM c",
                "SELECT {0} VALUE IS_DEFINED(c.city) FROM c",
                "SELECT {0} VALUE (c.children[0].age ?? 0) + (c.children[1].age ?? 0) FROM c",

                // distinct queries with order by
                "SELECT {0} VALUE c.age FROM c ORDER BY c.age",
                "SELECT {0} VALUE c.name FROM c ORDER BY c.name",
                "SELECT {0} VALUE c.city FROM c ORDER BY c.city",
                "SELECT {0} VALUE c.city FROM c ORDER BY c.age",
                "SELECT {0} VALUE LEFT(c.name, 1) FROM c ORDER BY c.name",

                // distinct queries with top and no matching order by
                "SELECT {0} TOP 2147483647 VALUE c.age FROM c",

                // distinct queries with top and  matching order by
                "SELECT {0} TOP 2147483647 VALUE c.age FROM c ORDER BY c.age",

                // distinct queries with aggregates
                "SELECT {0} VALUE MAX(c.age) FROM c",

                // distinct queries with joins
                "SELECT {0} VALUE c.age FROM p JOIN c IN p.children",
                "SELECT {0} p.age AS ParentAge, c.age ChildAge FROM p JOIN c IN p.children",
                "SELECT {0} VALUE c.name FROM p JOIN c IN p.children",
                "SELECT {0} p.name AS ParentName, c.name ChildName FROM p JOIN c IN p.children",

                // distinct queries in subqueries
                "SELECT {0} r.age, s FROM r JOIN (SELECT DISTINCT VALUE c FROM (SELECT 1 a) c) s WHERE r.age > 25",
                "SELECT {0} p.name, p.age FROM (SELECT DISTINCT * FROM r) p WHERE p.age > 25",

                // distinct queries in scalar subqeries
                "SELECT {0} p.name, (SELECT DISTINCT VALUE p.age) AS Age FROM p",
                "SELECT {0} p.name, p.age FROM p WHERE (SELECT DISTINCT VALUE LEFT(p.name, 1)) > 'A' AND (SELECT DISTINCT VALUE p.age) > 21",
                "SELECT {0} p.name, (SELECT DISTINCT VALUE p.age) AS Age FROM p WHERE (SELECT DISTINCT VALUE p.name) > 'A' OR (SELECT DISTINCT VALUE p.age) > 21",

                // select *
                "SELECT {0} * FROM c",
            };
            #endregion
            #region ExecuteNextAsync API
            // run the query with distinct and without + MockDistinctMap
            // Should recieve same results
            // PageSize = 1 guarentees that the backend will return some duplicates.
            foreach (string query in queries)
            {
                foreach (int pageSize in new int[] { 1, 10, 100 })
                {
                    string queryWithDistinct = string.Format(query, "DISTINCT");
                    string queryWithoutDistinct = string.Format(query, "");
                    CrossPartitionQueryTests.MockDistinctMap documentsSeen = new CrossPartitionQueryTests.MockDistinctMap();
                    List<JToken> documentsFromWithDistinct = new List<JToken>();
                    List<JToken> documentsFromWithoutDistinct = new List<JToken>();

                    FeedOptions feedOptions = new FeedOptions()
                    {
                        MaxDegreeOfParallelism = 100,
                        MaxItemCount = pageSize,
                        EnableCrossPartitionQuery = true,
                    };

                    IDocumentQuery<JToken> documentQueryWithoutDistinct = client.CreateDocumentQuery<JToken>(
                        collection,
                        queryWithoutDistinct,
                        feedOptions).AsDocumentQuery();

                    while (documentQueryWithoutDistinct.HasMoreResults)
                    {
                        DocumentFeedResponse<JToken> feedResponse = await documentQueryWithoutDistinct.ExecuteNextAsync<JToken>();
                        foreach (JToken document in feedResponse)
                        {
                            if (documentsSeen.Add(document, out UInt192? hash))
                            {
                                documentsFromWithoutDistinct.Add(document);
                            }
                            else
                            {
                                // No Op for debugging purposes.
                            }
                        }
                    }

                    IDocumentQuery<JToken> documentQueryWithDistinct = client.CreateDocumentQuery<JToken>(
                        collection,
                        queryWithDistinct,
                        feedOptions).AsDocumentQuery();

                    while (documentQueryWithDistinct.HasMoreResults)
                    {
                        DocumentFeedResponse<JToken> feedResponse = await documentQueryWithDistinct.ExecuteNextAsync<JToken>();
                        documentsFromWithDistinct.AddRange(feedResponse);
                    }

                    string collectionTypePrefix = collection.HasPartitionKey ? "Partitioned" : "NonPartitioned";
                    Assert.IsTrue(
                        documentsFromWithDistinct.SequenceEqual(documentsFromWithoutDistinct, JToken.EqualityComparer),
                        $"Documents didn't match for {queryWithDistinct}, with page size: {pageSize} on a {collectionTypePrefix} collection");
                }
            }
            #endregion
            #region Unordered Continuation
            // Run the unordered distinct query through the continuation api should result in the same set (but maybe some duplicates)
            foreach (string query in new string[]
            {
                "SELECT {0} VALUE c.name from c",
                "SELECT {0} VALUE c.age from c",
                "SELECT {0} TOP 2147483647 VALUE c.city from c",
                "SELECT {0} VALUE c.age from c ORDER BY c.name",
                "SELECT {0} VALUE c.name from c OFFSET 1 LIMIT 1",
            })
            {
                string queryWithDistinct = string.Format(query, "DISTINCT");
                string queryWithoutDistinct = string.Format(query, "");
                HashSet<JToken> documentsFromWithDistinct = new HashSet<JToken>(CrossPartitionQueryTests.JsonTokenEqualityComparer.Value);
                HashSet<JToken> documentsFromWithoutDistinct = new HashSet<JToken>(CrossPartitionQueryTests.JsonTokenEqualityComparer.Value);

                FeedOptions feedOptions = new FeedOptions()
                {
                    MaxDegreeOfParallelism = 100,
                    MaxItemCount = 10,
                    EnableCrossPartitionQuery = true,
                    // EnableCrossPartitionSkipTake = true,
                };

                IDocumentQuery<JToken> documentQueryWithoutDistinct = client.CreateDocumentQuery<JToken>(
                        collection,
                        queryWithoutDistinct,
                        feedOptions).AsDocumentQuery();

                while (documentQueryWithoutDistinct.HasMoreResults)
                {
                    DocumentFeedResponse<JToken> feedResponse = await documentQueryWithoutDistinct.ExecuteNextAsync<JToken>();
                    foreach (JToken jToken in feedResponse)
                    {
                        documentsFromWithoutDistinct.Add(jToken);
                    }
                }

                IDocumentQuery<JToken> documentQueryWithDistinct = client.CreateDocumentQuery<JToken>(
                    collection,
                    queryWithDistinct,
                    feedOptions).AsDocumentQuery();

                // For now we are blocking the use of continuation 
                // This try catch can be removed if we do allow the continuation token.
                try
                {
                    string continuationToken = null;
                    do
                    {
                        feedOptions.RequestContinuationToken = continuationToken;
                        using (IDocumentQuery<JToken> documentQuery = client.CreateDocumentQuery<JToken>(
                            collection,
                            queryWithDistinct,
                            feedOptions).AsDocumentQuery())
                        {
                            DocumentFeedResponse<JToken> feedResponse = await documentQuery.ExecuteNextAsync<JToken>();
                            foreach (JToken jToken in feedResponse)
                            {
                                documentsFromWithDistinct.Add(jToken);
                            }
                            continuationToken = feedResponse.ResponseContinuation;
                        }
                    }
                    while (continuationToken != null);
                    string collectionTypePrefix = collection.HasPartitionKey ? "Partitioned" : "NonPartitioned";
                    Assert.IsTrue(
                        documentsFromWithDistinct.IsSubsetOf(documentsFromWithoutDistinct),
                        $"Documents didn't match for {queryWithDistinct} on a {collectionTypePrefix} collection");

                    Assert.Fail("Expected an exception when using continuation tokens on an unordered distinct query.");
                }
                catch (ArgumentException ex)
                {
                    string disallowContinuationErrorMessage = RMResources.UnorderedDistinctQueryContinuationToken;
                    Assert.AreEqual(disallowContinuationErrorMessage, ex.Message);
                }
            }
            #endregion
            #region Ordered Region
            // Run the ordered distinct query through the continuation api, should result in the same set
            // since the previous hash is passed in the continuation token.
            foreach (string query in new string[]
            {
                "SELECT {0} VALUE c.age FROM c ORDER BY c.age",
                "SELECT {0} VALUE c.name FROM c ORDER BY c.name",
            })
            {
                foreach (int pageSize in new int[] { 1, 10, 100 })
                {
                    string queryWithDistinct = string.Format(query, "DISTINCT");
                    string queryWithoutDistinct = string.Format(query, "");
                    CrossPartitionQueryTests.MockDistinctMap documentsSeen = new CrossPartitionQueryTests.MockDistinctMap();
                    List<JToken> documentsFromWithDistinct = new List<JToken>();
                    List<JToken> documentsFromWithoutDistinct = new List<JToken>();

                    FeedOptions feedOptions = new FeedOptions()
                    {
                        MaxDegreeOfParallelism = 100,
                        MaxItemCount = 1,
                        EnableCrossPartitionQuery = true,
                    };

                    IDocumentQuery<JToken> documentQueryWithoutDistinct = client.CreateDocumentQuery<JToken>(
                            collection,
                            queryWithoutDistinct,
                            feedOptions).AsDocumentQuery();

                    while (documentQueryWithoutDistinct.HasMoreResults)
                    {
                        DocumentFeedResponse<JToken> feedResponse = await documentQueryWithoutDistinct.ExecuteNextAsync<JToken>();
                        foreach (JToken document in feedResponse)
                        {
                            if (documentsSeen.Add(document, out UInt192? hash))
                            {
                                documentsFromWithoutDistinct.Add(document);
                            }
                            else
                            {
                                // No Op for debugging purposes.
                            }
                        }
                    }

                    IDocumentQuery<JToken> documentQueryWithDistinct = client.CreateDocumentQuery<JToken>(
                        collection,
                        queryWithDistinct,
                        feedOptions).AsDocumentQuery();

                    string continuationToken = null;
                    do
                    {
                        feedOptions.RequestContinuationToken = continuationToken;
                        using (IDocumentQuery<JToken> documentQuery = client.CreateDocumentQuery<JToken>(
                            collection,
                            queryWithDistinct,
                            feedOptions).AsDocumentQuery())
                        {
                            DocumentFeedResponse<JToken> feedResponse = await documentQuery.ExecuteNextAsync<JToken>();
                            documentsFromWithDistinct.AddRange(feedResponse);
                            continuationToken = feedResponse.ResponseContinuation;
                        }
                    }
                    while (continuationToken != null);

                    string collectionTypePrefix = collection.HasPartitionKey ? "Partitioned" : "NonPartitioned";
                    Assert.IsTrue(
                        documentsFromWithDistinct.SequenceEqual(documentsFromWithoutDistinct, JToken.EqualityComparer),
                        $"Documents didn't match for {queryWithDistinct} on a {collectionTypePrefix} collection");
                }
            }
            #endregion
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryCrossPartitionTopOrderByDifferentDimension()
        {
            string[] documents = new[]
            {
                @"{""id"":""documentId1"",""key"":""A""}",
                @"{""id"":""documentId2"",""key"":""A"",""prop"":3}",
                @"{""id"":""documentId3"",""key"":""A""}",
                @"{""id"":""documentId4"",""key"":5}",
                @"{""id"":""documentId5"",""key"":5,""prop"":2}",
                @"{""id"":""documentId6"",""key"":5}",
                @"{""id"":""documentId7"",""key"":null}",
                @"{""id"":""documentId8"",""key"":null,""prop"":1}",
                @"{""id"":""documentId9"",""key"":null}",
            };

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned,
                documents,
                this.TestQueryCrossPartitionTopOrderByDifferentDimension,
                "/key");
        }

        private async Task TestQueryCrossPartitionTopOrderByDifferentDimension(DocumentClient client, DocumentCollection collection, IEnumerable<Document> documents)
        {
            await DocumentCrossPartitionQueryTests.NoOp();

            string[] expected = new[] { "documentId2", "documentId5", "documentId8" };
            IQueryable<Document> query = client.CreateDocumentQuery<Document>(
                collection.AltLink,
                "SELECT r.id FROM r ORDER BY r.prop DESC",
                new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = 1 });
            Assert.AreEqual(string.Join(", ", expected), string.Join(", ", query.ToList().Select(doc => doc.Id)));
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryCrossPartitionValueUndefinedOrderBy()
        {
            // For cross partition queries the following class of queries did not execute correctly:
            //
            //  SELECT VALUE r.a FROM r ORDER BY r.b
            // 
            // The reason why is because we were not successfully removing the undefined results.
            //
            // The query gets rewritten to:
            //
            //  SELECT r._rid, [{ "item": r.b}] as orderByItems, r.a AS payload
            //
            // if `IS_DEFINED(r.a) == false`, then the result of the query is:
            //
            //  {
            //     "_rid": <_rid>,
            //     "orderByItems": [{"item": <r.b>}],
            //  }
            //
            // The payload field is undefined and in the cross partition code that evaluates to c# null (not JSON null).
            // That null is then added to the order by results and to the user UNDEFINED becomes JSON null.
            // The almost correct solution is to add a check to see if the value is defined before adding it to the result set, 
            // but that falls for the following set of queries:
            //
            //  SELECT TOP n VALUE r.a FROM r ORDER BY r.b
            //
            // since we might grab n results from the backend that includes undefined which gets thrown away client side, 
            // but then we might end up with less than n documents when we could have gotten n documents in the non partitioned scenario.
            // We opted to inject `IS_DEFINED(r.a)` into the filter to avoid these scenarios.
            // 
            // This test is to make sure we don't regress on that code path.


            // Create a bunch of documents that possibly have a field
            List<string> documents = new List<string>();
            int numDocuments = 100;
            Random random = new Random(1234);
            for (int i = 0; i < numDocuments; i++)
            {
                Dictionary<string, int> dictionary = new Dictionary<string, int>();
                if (random.Next() % 2 == 0)
                {
                    dictionary["field"] = random.Next(0, 100);
                }

                string serializedDocument = JsonConvert.SerializeObject(dictionary);
                documents.Add(serializedDocument);
            }

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned,
                documents,
                this.TestQueryCrossPartitionValueUndefinedOrderBy);
        }

        private async Task TestQueryCrossPartitionValueUndefinedOrderBy(
            DocumentClient documentClient,
            DocumentCollection documentCollection,
            IEnumerable<Document> documents)
        {
            bool isPartitioned = documentCollection.PartitionKey.Paths.Count() != 0;
            FeedOptions feedOptions = new FeedOptions()
            {
                EnableCrossPartitionQuery = isPartitioned,
                MaxItemCount = 16,
                MaxDegreeOfParallelism = isPartitioned ? 10 : 0,
                MaxBufferedItemCount = isPartitioned ? 1000 : 0,
            };

            //------------------------------------------------------------------------------------------
            // SELECT VALUE c.field FROM c ORDER BY c._ts 
            // vs 
            // SELECT c.field FROM c ORDER BY c._ts
            // We want to make sure that no nulls are emitted
            //------------------------------------------------------------------------------------------
            string queryWithValue = "SELECT VALUE c.field FROM c ORDER BY c._ts";
            List<JToken> valueWithContinuations = await QueryWithContinuationTokens<JToken>(
                documentClient,
                documentCollection,
                queryWithValue,
                feedOptions);

            List<JToken> valueWithoutContinuations = await QueryWithoutContinuationTokens<JToken>(
                documentClient,
                documentCollection,
                queryWithValue,
                feedOptions);

            Assert.IsTrue(
                valueWithContinuations.SequenceEqual(
                    valueWithoutContinuations, CrossPartitionQueryTests.JsonTokenEqualityComparer.Value));
            //------------------------------------------------------------------------------------------
            string queryWithoutValue = "SELECT c.field FROM c ORDER BY c._ts";
            List<JToken> withoutvalueWithContinuations = await QueryWithContinuationTokens<JToken>(
                documentClient,
                documentCollection,
                queryWithoutValue,
                feedOptions);

            List<JToken> withoutValueWithoutContinuations = await QueryWithoutContinuationTokens<JToken>(
                documentClient,
                documentCollection,
                queryWithoutValue,
                feedOptions);

            Assert.IsTrue(
                withoutvalueWithContinuations.SequenceEqual(
                    withoutValueWithoutContinuations, CrossPartitionQueryTests.JsonTokenEqualityComparer.Value));
            //------------------------------------------------------------------------------------------
            // COUNT(SELECT VALUE c.field FROM c ORDER BY c._ts) < COUNT(SELECT c.field FROM c ORDER BY c._ts)
            Assert.IsTrue(valueWithContinuations.Count < withoutvalueWithContinuations.Count);
            //------------------------------------------------------------------------------------------


            //------------------------------------------------------------------------------------------
            // SELECT TOP n / 4 VALUE c.field From c ORDER BY c._ts
            // vs 
            // TAKE(SELECT VALUE c.field From c ORDER BY c._ts, n / 4)
            // We want make that we actually get all the documents 
            // and not just throwing away the undefined ones
            //------------------------------------------------------------------------------------------
            string queryWithValueTop = $"SELECT TOP {documents.Count() / 4} VALUE c.field FROM c ORDER BY c._ts";
            List<JToken> valueWithTopContinuations = await QueryWithContinuationTokens<JToken>(
                documentClient,
                documentCollection,
                queryWithValueTop,
                feedOptions);

            List<JToken> valueWithTopWithoutContinuations = await QueryWithoutContinuationTokens<JToken>(
                documentClient,
                documentCollection,
                queryWithValueTop,
                feedOptions);

            Assert.IsTrue(
                valueWithTopContinuations.SequenceEqual(
                    valueWithTopWithoutContinuations, CrossPartitionQueryTests.JsonTokenEqualityComparer.Value));

            Assert.IsTrue(valueWithTopContinuations.Count == documents.Count() / 4);
            //------------------------------------------------------------------------------------------
            // Just do a client side skip for the expected 
            //------------------------------------------------------------------------------------------
            IEnumerable<JToken> expectedValueWithTop = valueWithContinuations.Take(documents.Count() / 4);
            Assert.IsTrue(
                expectedValueWithTop.SequenceEqual(
                    valueWithTopContinuations, CrossPartitionQueryTests.JsonTokenEqualityComparer.Value));
            //------------------------------------------------------------------------------------------
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestCrossPartitionOrderByWithDeletes()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 1000;

            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> documents = util.GetDocuments(numberOfDocuments);

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.Partitioned,
                documents,
                this.TestCrossPartitionOrderByWithDeletes);
        }

        private async Task TestCrossPartitionOrderByWithDeletes(
            DocumentClient documentClient,
            DocumentCollection documentCollection,
            IEnumerable<Document> documents)
        {
            List<JToken> results = new List<JToken>();
            FeedOptions feedOptions = new FeedOptions()
            {
                MaxItemCount = 10,
                EnableCrossPartitionQuery = true,
            };
            string query = "SELECT * FROM c ORDER BY c._ts";

            // Query one iteration
            string continuationToken = null;
            feedOptions.RequestContinuationToken = continuationToken;
            IDocumentQuery<JToken> documentQuery = documentClient.CreateDocumentQuery<JToken>(
                documentCollection,
                query,
                feedOptions).AsDocumentQuery();
            DocumentFeedResponse<JToken> feedResponse = await documentQuery.ExecuteNextAsync<JToken>();
            results.AddRange(feedResponse);
            continuationToken = feedResponse.ResponseContinuation;

            // Thanos half the documents
            Random random = new Random();
            foreach (Document document in documents)
            {
                if (random.Next() % 2 == 0)
                {
                    await documentClient.DeleteDocumentAsync(
                        document,
                        new RequestOptions()
                        {
                            PartitionKey = new PartitionKey(document.Id)
                        });
                }
            }

            // Drain the rest of the query
            do
            {
                feedOptions.RequestContinuationToken = continuationToken;
                documentQuery = documentClient.CreateDocumentQuery<JToken>(
                    documentCollection,
                    query,
                    feedOptions).AsDocumentQuery();
                feedResponse = await documentQuery.ExecuteNextAsync<JToken>();
                results.AddRange(feedResponse);
                continuationToken = feedResponse.ResponseContinuation;
            } while (continuationToken != null);
        }

        [TestMethod]
        public async Task TestOrderByNonAsciiCharacters()
        {
            string[] specialStrings = new string[]
            {
                // Strings which may be used elsewhere in code
                "undefined",
                // Numeric Strings
                "-9223372036854775808/-1",
                // Non-whitespace C0 controls: U+0001 through U+0008, U+000E through U+001F,
                "\u0001",
                // "Byte order marks"
                "U+FEFF",
                // Unicode Symbols
                "ЁЂЃЄЅІЇЈЉЊЋЌЍЎЏАБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдежзийклмнопрстуфхцчшщъыьэюя",
                // Quotation Marks
                "<foo val=“bar” />",
                // Strings which contain two-byte characters: can cause rendering issues or character-length issues
                "찦차를 타고 온 펲시맨과 쑛다리 똠방각하",
                // Changing length when lowercased
                "Ⱥ",
                // Japanese Emoticons
                "ﾟ･✿ヾ╲(｡◕‿◕｡)╱✿･ﾟ",
                // Emoji
                "❤️ 💔 💌 💕 💞 💓 💗 💖 💘 💝 💟 💜 💛 💚 💙",
                // Strings which contain "corrupted" text. The corruption will not appear in non-HTML text, however. (via http://www.eeemo.net)
                "Ṱ̺̺̕o͞ ̷i̲̬͇̪͙n̝̗͕v̟̜̘̦͟o̶̙̰̠kè͚̮̺̪̹̱̤ ̖t̝͕̳̣̻̪͞h̼͓̲̦̳̘̲e͇̣̰̦̬͎ ̢̼̻̱̘h͚͎͙̜̣̲ͅi̦̲̣̰̤v̻͍e̺̭̳̪̰-m̢iͅn̖̺̞̲̯̰d̵̼̟͙̩̼̘̳ ̞̥̱̳̭r̛̗̘e͙p͠r̼̞̻̭̗e̺̠̣͟s̘͇̳͍̝͉e͉̥̯̞̲͚̬͜ǹ̬͎͎̟̖͇̤t͍̬̤͓̼̭͘ͅi̪̱n͠g̴͉ ͏͉ͅc̬̟h͡a̫̻̯͘o̫̟̖͍̙̝͉s̗̦̲.̨̹͈̣"
             };

            IEnumerable<string> documents = specialStrings.Select((specialString) => $@"{{ ""field"" : ""{specialString}""}}");
            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
               ConnectionModes.Direct,
               CollectionTypes.Partitioned,
               documents,
               this.TestOrderByNonAsciiCharactersHelper);
        }

        private async Task TestOrderByNonAsciiCharactersHelper(
           DocumentClient documentClient,
           DocumentCollection documentCollection,
           IEnumerable<Document> documents)
        {
            foreach (int maxDegreeOfParallelism in new int[] { 1, 100 })
            {
                foreach (int maxItemCount in new int[] { 10, 100 })
                {
                    FeedOptions feedOptions = new FeedOptions
                    {
                        EnableCrossPartitionQuery = true,
                        MaxBufferedItemCount = 7000,
                        MaxDegreeOfParallelism = maxDegreeOfParallelism
                    };

                    List<JToken> actualFromQueryWithoutContinutionTokens = await QueryWithContinuationTokens<JToken>(
                        documentClient,
                        documentCollection,
                        "SELECT * FROM c ORDER BY c.field",
                        feedOptions);

                    Assert.AreEqual(documents.Count(), actualFromQueryWithoutContinutionTokens.Count);
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestMixedTypeOrderBy()
        {
            int numberOfDocuments = 1 << 4;
            int numberOfDuplicates = 1 << 2;

            List<string> documents = new List<string>(numberOfDocuments * numberOfDuplicates);
            Random random = new Random(1234);
            for (int i = 0; i < numberOfDocuments; ++i)
            {
                MixedTypedDocument mixedTypeDocument = DocumentCrossPartitionQueryTests.GenerateMixedTypeDocument(random);
                for (int j = 0; j < numberOfDuplicates; j++)
                {
                    documents.Add(JsonConvert.SerializeObject(mixedTypeDocument)); ;
                }
            }

            // Just have range indexes
            IndexingPolicy indexV1Policy = new IndexingPolicy()
            {
                IncludedPaths = new Collection<IncludedPath>()
                {
                    new IncludedPath()
                    {
                        Path = "/*",
                        Indexes = new Collection<Index>()
                        {
                            Index.Range(DataType.String, -1),
                            Index.Range(DataType.Number, -1),
                        }
                    }
                }
            };

            // Add a composite index to force an index v2 collection to be made.
            IndexingPolicy indexV2Policy = new IndexingPolicy()
            {
                IncludedPaths = new Collection<IncludedPath>()
                {
                    new IncludedPath()
                    {
                        Path = "/*",
                    }
                },

                CompositeIndexes = new Collection<Collection<CompositePath>>()
                {
                    // Simple
                    new Collection<CompositePath>()
                    {
                        new CompositePath()
                        {
                            Path = "/_ts",
                        },
                        new CompositePath()
                        {
                            Path = "/_etag",
                        }
                    }
                }
            };

            string indexV2Api = HttpConstants.Versions.v2018_09_17;
            string indexV1Api = HttpConstants.Versions.v2017_11_15;

            Func<bool, OrderByTypes[], Action<Exception>, Task> runWithAllowMixedTypeOrderByFlag = (allowMixedTypeOrderByTestFlag, orderByTypes, expectedExcpetionHandler) =>
            {
                bool allowMixedTypeOrderByTestFlagOriginalValue = OrderByConsumeComparer.AllowMixedTypeOrderByTestFlag;
                string apiVersion = allowMixedTypeOrderByTestFlag ? indexV2Api : indexV1Api;
                IndexingPolicy indexingPolicy = allowMixedTypeOrderByTestFlag ? indexV2Policy : indexV1Policy;
                try
                {
                    OrderByConsumeComparer.AllowMixedTypeOrderByTestFlag = allowMixedTypeOrderByTestFlag;
                    return DocumentCrossPartitionQueryTests.RunWithApiVersion(
                        apiVersion,
                        () =>
                        {
                            return DocumentCrossPartitionQueryTests.CreateIngestQueryDelete<Tuple<OrderByTypes[], Action<Exception>>>(
                                ConnectionModes.Direct,
                                CollectionTypes.Partitioned,
                                documents,
                                this.TestMixedTypeOrderBy,
                                new Tuple<OrderByTypes[], Action<Exception>>(orderByTypes, expectedExcpetionHandler),
                                "/id",
                                indexingPolicy);
                        });
                }
                finally
                {
                    OrderByConsumeComparer.AllowMixedTypeOrderByTestFlag = allowMixedTypeOrderByTestFlagOriginalValue;
                }
            };

            bool dontAllowMixedTypes = false;
            bool doAllowMixedTypes = true;

            OrderByTypes primitives = OrderByTypes.Bool | OrderByTypes.Null | OrderByTypes.Number | OrderByTypes.String;
            OrderByTypes nonPrimitives = OrderByTypes.Array | OrderByTypes.Object;
            OrderByTypes all = primitives | nonPrimitives | OrderByTypes.Undefined;

            // Don't allow mixed types but single type order by should still work
            await runWithAllowMixedTypeOrderByFlag(
                dontAllowMixedTypes,
                new OrderByTypes[]
                {
                    OrderByTypes.Array,
                    OrderByTypes.Bool,
                    OrderByTypes.Null,
                    OrderByTypes.Number,
                    OrderByTypes.Object,
                    OrderByTypes.String,
                    OrderByTypes.Undefined,
                }, null);

            // If you don't allow mixed types but you run a mixed type query then you should get an exception or the results are just wrong.
            await runWithAllowMixedTypeOrderByFlag(
                dontAllowMixedTypes,
                new OrderByTypes[]
                {
                    all,
                    primitives,
                },
                (exception) =>
                {
                    Assert.IsTrue(
                        // Either we get the weird client exception for having mixed types
                        exception.Message.Contains("Cannot execute cross partition order-by queries on mix types.")
                        // Or the results are just messed up since the pages in isolation were not mixed typed.
                        || exception.GetType() == typeof(AssertFailedException));
                });

            // Mixed type orderby should work for all scenarios, since for now the primitives are accepted to not be served from the index.
            await runWithAllowMixedTypeOrderByFlag(
                doAllowMixedTypes,
                new OrderByTypes[]
                {
                    OrderByTypes.Array,
                    OrderByTypes.Bool,
                    OrderByTypes.Null,
                    OrderByTypes.Number,
                    OrderByTypes.Object,
                    OrderByTypes.String,
                    OrderByTypes.Undefined,
                    primitives,
                    nonPrimitives,
                    all,
                }, null);
        }

        private sealed class MixedTypedDocument
        {
            public object MixedTypeField { get; set; }
        }

        private static MixedTypedDocument GenerateMixedTypeDocument(Random random)
        {
            return new MixedTypedDocument()
            {
                MixedTypeField = GenerateRandomJsonValue(random),
            };
        }

        private static object GenerateRandomJsonValue(Random random)
        {
            switch (random.Next(0, 6))
            {
                // Number
                case 0:
                    return random.Next();
                // String
                case 1:
                    return new string('a', random.Next(0, 100));
                // Null
                case 2:
                    return null;
                // Bool
                case 3:
                    return (random.Next() % 2) == 0;
                // Object
                case 4:
                    return new object();
                // Array
                case 5:
                    return new List<object>();
                default:
                    throw new ArgumentException();
            }
        }

        private sealed class MockOrderByComparer : IComparer<object>
        {
            public static readonly MockOrderByComparer Value = new MockOrderByComparer();

            public int Compare(object obj1, object obj2)
            {
                if (object.ReferenceEquals(obj1, obj2))
                {
                    return 0;
                }

                if (obj1 is ItemComparer.MinValueItem)
                {
                    return -1;
                }

                if (obj2 is ItemComparer.MinValueItem)
                {
                    return 1;
                }

                if (obj1 is ItemComparer.MaxValueItem)
                {
                    return 1;
                }

                if (obj2 is ItemComparer.MaxValueItem)
                {
                    return -1;
                }

                ItemType type1 = ItemTypeHelper.GetItemType(obj1);
                ItemType type2 = ItemTypeHelper.GetItemType(obj2);

                int cmp = type1.CompareTo(type2);
                if (cmp != 0)
                {
                    // If one item type comes before another item type, then just return that.
                    return cmp;
                }

                // If they are the same type then you need to break the tie.
                switch (type1)
                {
                    case ItemType.NoValue:
                    case ItemType.Null:
                        // All null and no values are not distinguishable.
                        return 0;
                    case ItemType.Bool:
                        return Comparer<bool>.Default.Compare((bool)obj1, (bool)obj2);
                    case ItemType.Number:
                        return Comparer<double>.Default.Compare(
                            Convert.ToDouble(obj1, CultureInfo.InvariantCulture),
                            Convert.ToDouble(obj2, CultureInfo.InvariantCulture));
                    case ItemType.String:
                        return string.CompareOrdinal((string)obj1, (string)obj2);
                    default:
                        string errorMessage = string.Format(CultureInfo.InvariantCulture, "Unexpected type: {0}", type1);
                        Debug.Assert(false, errorMessage);
                        throw new InvalidCastException(errorMessage);
                }
            }
        }

        [Flags]
        private enum OrderByTypes
        {
            Number = 1 << 0,
            String = 1 << 1,
            Null = 1 << 2,
            Bool = 1 << 3,
            Object = 1 << 4,
            Array = 1 << 5,
            Undefined = 1 << 6,
        };

        private async Task TestMixedTypeOrderBy(DocumentClient documentClient, DocumentCollection documentCollection, IEnumerable<Document> documents, Tuple<OrderByTypes[], Action<Exception>> args)
        {
            OrderByTypes[] orderByTypesList = args.Item1;
            Action<Exception> expectedExceptionHandler = args.Item2;
            try
            {
                foreach (bool isDesc in new bool[] { true, false })
                {
                    foreach (OrderByTypes orderByTypes in orderByTypesList)
                    {
                        string orderString = isDesc ? "DESC" : "ASC";
                        List<string> mixedTypeFilters = new List<string>();
                        if (orderByTypes.HasFlag(OrderByTypes.Array))
                        {
                            mixedTypeFilters.Add($"IS_ARRAY(c.{nameof(MixedTypedDocument.MixedTypeField)})");
                        }

                        if (orderByTypes.HasFlag(OrderByTypes.Bool))
                        {
                            mixedTypeFilters.Add($"IS_BOOL(c.{nameof(MixedTypedDocument.MixedTypeField)})");
                        }

                        if (orderByTypes.HasFlag(OrderByTypes.Null))
                        {
                            mixedTypeFilters.Add($"IS_NULL(c.{nameof(MixedTypedDocument.MixedTypeField)})");
                        }

                        if (orderByTypes.HasFlag(OrderByTypes.Number))
                        {
                            mixedTypeFilters.Add($"IS_NUMBER(c.{nameof(MixedTypedDocument.MixedTypeField)})");
                        }

                        if (orderByTypes.HasFlag(OrderByTypes.Object))
                        {
                            mixedTypeFilters.Add($"IS_OBJECT(c.{nameof(MixedTypedDocument.MixedTypeField)})");
                        }

                        if (orderByTypes.HasFlag(OrderByTypes.String))
                        {
                            mixedTypeFilters.Add($"IS_STRING(c.{nameof(MixedTypedDocument.MixedTypeField)})");
                        }

                        if (orderByTypes.HasFlag(OrderByTypes.Undefined))
                        {
                            mixedTypeFilters.Add($"not IS_DEFINED(c.{nameof(MixedTypedDocument.MixedTypeField)})");
                        }

                        string filter = mixedTypeFilters.Count() == 0 ? "true" : string.Join(" OR ", mixedTypeFilters);

                        string query = $@"
                            SELECT VALUE c.{nameof(MixedTypedDocument.MixedTypeField)} 
                            FROM c
                            WHERE {filter}
                            ORDER BY c.{nameof(MixedTypedDocument.MixedTypeField)} {orderString}";

                        bool isPartitioned = documentCollection.PartitionKey.Paths.Count() != 0;
                        FeedOptions feedOptions = new FeedOptions()
                        {
                            EnableCrossPartitionQuery = isPartitioned,
                            MaxItemCount = 16,
                            MaxDegreeOfParallelism = isPartitioned ? 10 : 0,
                            MaxBufferedItemCount = isPartitioned ? 1000 : 0,
                        };

                        List<JToken> actualFromQueryWithoutContinutionTokens;
                        actualFromQueryWithoutContinutionTokens = await QueryWithoutContinuationTokens<JToken>(
                            documentClient,
                            documentCollection,
                            query,
                            feedOptions);
#if false
                        For now we can not serve the query through continuation tokens correctly.
                        This is because we allow order by on mixed types but not comparisions across types
                        For example suppose the following query:
                            SELECT c.MixedTypeField FROM c ORDER BY c.MixedTypeField
                        returns:
                        [
                            {"MixedTypeField":null},
                            {"MixedTypeField":false},
                            {"MixedTypeField":true},
                            {"MixedTypeField":303093052},
                            {"MixedTypeField":438985130},
                            {"MixedTypeField":"aaaaaaaaaaa"}
                        ]
                        and we left off on 303093052 then at some point the cross partition code resumes the query by running the following:
                            SELECT c.MixedTypeField FROM c WHERE c.MixedTypeField > 303093052 ORDER BY c.MixedTypeField
                        which will only return the following:
                            { "MixedTypeField":438985130}
                        and that is because comparision across types is undefined so "aaaaaaaaaaa" > 303093052 never got emitted
#endif
                        List<JToken> actualFromQueryWithContinutionTokens;
                        actualFromQueryWithContinutionTokens = await QueryWithContinuationTokens<JToken>(
                            documentClient,
                            documentCollection,
                            query,
                            feedOptions);

                        IEnumerable<object> insertedDocs = documents
                            .Select(document => document.GetPropertyValue<object>(nameof(MixedTypedDocument.MixedTypeField)));

                        // Build the expected results using LINQ
                        IEnumerable<object> expected = new List<object>();

                        // Filter based on the mixedOrderByType enum
                        if (orderByTypes.HasFlag(OrderByTypes.Array))
                        {
                            // no arrays should be served from the range index
                        }

                        if (orderByTypes.HasFlag(OrderByTypes.Bool))
                        {
                            expected = expected.Concat(insertedDocs.Where(x => x is bool));
                        }

                        if (orderByTypes.HasFlag(OrderByTypes.Null))
                        {
                            expected = expected.Concat(insertedDocs.Where(x => x == null));
                        }

                        if (orderByTypes.HasFlag(OrderByTypes.Number))
                        {
                            expected = expected.Concat(insertedDocs.Where(x => ItemTypeHelper.IsNumeric(x)));
                        }

                        if (orderByTypes.HasFlag(OrderByTypes.Object))
                        {
                            // no objects should be served from the range index
                        }

                        if (orderByTypes.HasFlag(OrderByTypes.String))
                        {
                            expected = expected.Concat(insertedDocs.Where(x => x is string));
                        }

                        if (orderByTypes.HasFlag(OrderByTypes.Undefined))
                        {
                            // no undefined should be served from the range index
                        }

                        // Order using the mock order by comparer
                        if (isDesc)
                        {
                            expected = expected.OrderByDescending(x => x, MockOrderByComparer.Value);
                        }
                        else
                        {
                            expected = expected.OrderBy(x => x, MockOrderByComparer.Value);
                        }

                        // bind all the value to JTokens so they can be compared agaisnt the actual.
                        List<JToken> expectedBinded = expected.Select(x => x == null ? JValue.CreateNull() : JToken.FromObject(x)).ToList();

                        List<string> test = new List<string>()
                        {
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        };

                        List<string> testDesc = test.OrderByDescending(x => x).ToList();
                        List<string> testAsc = test.OrderBy(x => x).ToList();

                        Assert.IsTrue(
                            expectedBinded.SequenceEqual(actualFromQueryWithoutContinutionTokens, CrossPartitionQueryTests.JsonTokenEqualityComparer.Value),
                            $@" queryWithoutContinuations: {query},
                            expected:{JsonConvert.SerializeObject(expected)},
                            actual: {JsonConvert.SerializeObject(actualFromQueryWithoutContinutionTokens)}");

                        // Can't assert for reasons mentioned above
                        //Assert.IsTrue(
                        //    expected.SequenceEqual(actualFromQueryWithContinutionTokens, CosmosElementEqualityComparer.Value),
                        //    $@" queryWithContinuations: {query},
                        //    expected:{JsonConvert.SerializeObject(expected)},
                        //    actual: {JsonConvert.SerializeObject(actualFromQueryWithContinutionTokens)}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (expectedExceptionHandler != null)
                {
                    expectedExceptionHandler(ex);
                }
                else
                {
                    throw;
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryCrossPartitionTopOrderBy()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 1000;
            string partitionKey = "field_0";

            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> documents = util.GetDocuments(numberOfDocuments);

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete<string>(
                ConnectionModes.Direct,
                CollectionTypes.Partitioned,
                documents,
                this.TestQueryCrossPartitionTopOrderBy,
                partitionKey,
                "/" + partitionKey);
        }

        private async Task TestQueryCrossPartitionTopOrderBy(DocumentClient documentClient, DocumentCollection collection, IEnumerable<Document> documents, string testArg)
        {
            string partitionKey = testArg;
            IDictionary<string, string> idToRangeMinKeyMap = new Dictionary<string, string>();
            Dictionary<string, HashSet<string>> rangeToIdMap = new Dictionary<string, HashSet<string>>();
            IRoutingMapProvider routingMapProvider = await documentClient.GetPartitionKeyRangeCacheAsync();

            foreach (Document document in documents)
            {
                IReadOnlyList<PartitionKeyRange> targetRanges = await routingMapProvider.TryGetOverlappingRangesAsync(
                collection.ResourceId,
                Range<string>.GetPointRange(
                    PartitionKeyInternal.FromObjectArray(
                        new object[]
                        {
                            document.GetValue<int>(partitionKey)
                        },
                        true).GetEffectivePartitionKeyString(collection.PartitionKey)));
                Debug.Assert(targetRanges.Count == 1);
                idToRangeMinKeyMap.Add(document.Id, targetRanges[0].MinInclusive);

                string id = targetRanges[0].Id;
                if (!rangeToIdMap.TryGetValue(id, out HashSet<string> ids))
                {
                    ids = new HashSet<string>();
                    rangeToIdMap[id] = ids;
                }

                ids.Add(document.Id);
            }

            IList<int> partitionKeyValues = new HashSet<int>(documents.Select(doc => doc.GetValue<int>(partitionKey))).ToList();

            // Test Empty Results
            List<string> expectedResults = new List<string> { };
            List<string> computedResults = new List<string>();

            string emptyQueryText = @"
                SELECT TOP 5 * 
                FROM Root r 
                WHERE r.partitionKey = 9991123 OR r.partitionKey = 9991124 OR r.partitionKey = 99991125";
            FeedOptions feedOptionsEmptyResult = new FeedOptions
            {
                EnableCrossPartitionQuery = true
            };

            IQueryable<Document> queryEmptyResult = Client.CreateDocumentQuery<Document>(
                collection.AltLink,
                emptyQueryText,
                feedOptionsEmptyResult);

            computedResults = queryEmptyResult.ToList().Select(doc => doc.Id).ToList();
            computedResults.Sort();
            expectedResults.Sort();

            Random rand = new Random();
            Assert.AreEqual(string.Join(",", expectedResults), string.Join(",", computedResults));
            List<Task> tasks = new List<Task>();
            for (int trial = 0; trial < 1; ++trial)
            {
                foreach (bool fanOut in new[] { true, false })
                {
                    foreach (bool isParametrized in new[] { true, false })
                    {
                        foreach (bool useDocumentQuery in new[] { true, false })
                        {
                            foreach (bool hasTop in new[] { false, true })
                            {
                                foreach (bool hasOrderBy in new[] { false, true })
                                {
                                    foreach (string sortOrder in new[] { string.Empty, "ASC", "DESC" })
                                    {
                                        #region Expected Documents
                                        string topValueName = "@topValue";
                                        int top = rand.Next(4) * rand.Next(partitionKeyValues.Count);
                                        string queryText;
                                        string orderByField = "field_" + rand.Next(10);
                                        IEnumerable<Document> filteredDocuments;

                                        Func<string> getTop = () =>
                                            hasTop ? string.Format(CultureInfo.InvariantCulture, "TOP {0} ", isParametrized ? topValueName : top.ToString()) : string.Empty;

                                        Func<string> getOrderBy = () =>
                                            hasOrderBy ? string.Format(CultureInfo.InvariantCulture, " ORDER BY r.{0} {1}", orderByField, sortOrder) : string.Empty;

                                        if (fanOut)
                                        {
                                            queryText = string.Format(
                                                CultureInfo.InvariantCulture,
                                                "SELECT {0}r.id, r.{1} FROM r{2}",
                                                getTop(),
                                                partitionKey,
                                                getOrderBy());

                                            filteredDocuments = documents;
                                        }
                                        else
                                        {
                                            HashSet<int> selectedPartitionKeyValues = new HashSet<int>(partitionKeyValues
                                                .OrderBy(x => rand.Next())
                                                .ThenBy(x => x)
                                                .Take(rand.Next(1, Math.Min(100, partitionKeyValues.Count) + 1)));

                                            queryText = string.Format(
                                                CultureInfo.InvariantCulture,
                                                "SELECT {0}r.id, r.{1} FROM r WHERE r.{2} IN ({3}){4}",
                                                getTop(),
                                                partitionKey,
                                                partitionKey,
                                                string.Join(", ", selectedPartitionKeyValues),
                                                getOrderBy());

                                            filteredDocuments = documents
                                                .AsParallel()
                                                .Where(doc => selectedPartitionKeyValues.Contains(doc.GetValue<int>(partitionKey)));
                                        }

                                        if (hasOrderBy)
                                        {
                                            switch (sortOrder)
                                            {
                                                case "":
                                                case "ASC":
                                                    filteredDocuments = filteredDocuments
                                                        .AsParallel()
                                                        .OrderBy(doc => doc.GetValue<int>(orderByField))
                                                        .ThenBy(doc => idToRangeMinKeyMap[doc.Id])
                                                        .ThenBy(doc => int.Parse(doc.Id, CultureInfo.InvariantCulture));
                                                    break;
                                                case "DESC":
                                                    filteredDocuments = filteredDocuments
                                                        .AsParallel()
                                                        .OrderByDescending(doc => doc.GetValue<int>(orderByField))
                                                        .ThenBy(doc => idToRangeMinKeyMap[doc.Id])
                                                        .ThenByDescending(doc => int.Parse(doc.Id, CultureInfo.InvariantCulture));
                                                    break;
                                            }
                                        }
                                        else
                                        {
                                            filteredDocuments = filteredDocuments
                                                .AsParallel()
                                                .OrderBy(doc => idToRangeMinKeyMap[doc.Id])
                                                .ThenBy(doc => int.Parse(doc.Id, CultureInfo.InvariantCulture));
                                        }

                                        if (hasTop)
                                        {
                                            filteredDocuments = filteredDocuments.Take(top);
                                        }
                                        #endregion
                                        #region Actual Documents
                                        IEnumerable<Document> actualDocuments;
                                        FeedOptions feedOptions = new FeedOptions
                                        {
                                            EnableCrossPartitionQuery = true,
                                            MaxDegreeOfParallelism = hasTop ? rand.Next(4) : (rand.Next(2) == 0 ? -1 : (1 + rand.Next(0, 10))),
                                            MaxItemCount = rand.Next(2) == 0 ? -1 : rand.Next(1, documents.Count()),
                                            MaxBufferedItemCount = rand.Next(2) == 0 ? -1 : rand.Next(Math.Min(100, documents.Count()), documents.Count() + 1),
                                        };

                                        if (rand.Next(3) == 0)
                                        {
                                            feedOptions.MaxItemCount = null;
                                        }

                                        SqlParameterCollection parameters = new SqlParameterCollection();
                                        if (isParametrized)
                                        {
                                            if (hasTop)
                                            {
                                                parameters.Add(new SqlParameter
                                                {
                                                    Name = topValueName,
                                                    Value = top,
                                                });
                                            }
                                        }

                                        SqlQuerySpec querySpec = new SqlQuerySpec(queryText, parameters);

                                        DateTime startTime = DateTime.Now;
                                        double totalRU = 0;
                                        if (useDocumentQuery)
                                        {
                                            List<Document> result = new List<Document>();
                                            IDocumentQuery<Document> query = Client.CreateDocumentQuery<Document>(
                                                collection.AltLink,
                                                querySpec,
                                                feedOptions).AsDocumentQuery();

                                            while (query.HasMoreResults)
                                            {
                                                DocumentFeedResponse<Document> response = await query.ExecuteNextAsync<Document>();
                                                result.AddRange(response);
                                                totalRU += response.RequestCharge;
                                            }

                                            actualDocuments = result;
                                        }
                                        else
                                        {
                                            IQueryable<Document> query = Client.CreateDocumentQuery<Document>(
                                                collection.AltLink,
                                                querySpec,
                                                feedOptions);

                                            actualDocuments = query.ToList();
                                        }
                                        #endregion

                                        double time = (DateTime.Now - startTime).TotalMilliseconds;
                                        string allDocs = JsonConvert.SerializeObject(documents);

                                        string expectedResultDocs = JsonConvert.SerializeObject(filteredDocuments);
                                        IEnumerable<string> expectedResult = filteredDocuments.Select(doc => doc.Id);

                                        string actualResultDocs = JsonConvert.SerializeObject(actualDocuments);
                                        IEnumerable<string> actualResult = actualDocuments.Select(doc => doc.Id);

                                        Assert.AreEqual(
                                            string.Join(", ", expectedResult),
                                            string.Join(", ", actualResult),
                                            $"" +
                                            $"query: {JsonConvert.SerializeObject(querySpec)}, " +
                                            $"trial: {trial}, " +
                                            $"feedOptions: {JsonConvert.SerializeObject(feedOptions)}" +
                                            $"rangeToIdMap: {JsonConvert.SerializeObject(rangeToIdMap)}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestOrderByDateTime()
        {
            List<DateTimeDocument> dateTimeDocuments = new List<DateTimeDocument>();
            Random random = new Random();
            int numberOfDocuments = 128;

            // random dates
            for (int i = 0; i < numberOfDocuments; i++)
            {
                DateTime dateTime = new DateTime();
                dateTime = dateTime.AddTicks(random.Next());
                dateTimeDocuments.Add(new DateTimeDocument(dateTime));
            }

            // common prefix dates
            const int MonthsInYear = 12;
            const int DaysInMonth = 28; /*feburay has only 28 days*/
            const int HoursInDay = 24;
            const int MinutesInHour = 60;
            const int SecondsInMinute = 60;
            const int MillisecondsInSecond = 1000;

            foreach (int year in new int[] { 1, 9, 10, 100 })
            {
                for (int month = 1; month <= MonthsInYear; month += MonthsInYear / 4)
                {
                    for (int day = 1; day <= DaysInMonth; day += DaysInMonth / 4)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            int hour = random.Next(HoursInDay);
                            int minute = random.Next(MinutesInHour);
                            int second = random.Next(SecondsInMinute);
                            int millisecond = random.Next(MillisecondsInSecond);
                            DateTime dateTime = new DateTime(
                                2000,
                                month,
                                day,
                                hour,
                                minute,
                                second,
                                millisecond);
                            dateTimeDocuments.Add(new DateTimeDocument(dateTime));
                        }
                    }
                }
            }

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.Partitioned,
                dateTimeDocuments.Select(document => JsonConvert.SerializeObject(document)),
                this.TestOrderByDateTime);
        }

        private sealed class DateTimeDocument
        {
            public DateTime DateTime { get; }

            // Backend does not support Number64 / high enough precision
            //[JsonConverter(typeof(TicksDateTimeConverter))]
            //public DateTime Ticks { get { return this.DateTime; } }

            // Backend does not support Number64 / high enough precision
            //[JsonConverter(typeof(UnixDateTimeConverter))]
            //public DateTime Unix { get { return this.DateTime; } }

            [JsonConverter(typeof(IsoDateTimeConverter))]
            public DateTime Iso => this.DateTime;

            // "JavaScript":new Date(-62135567870444) is not a valid JSON
            //[JsonConverter(typeof(JavaScriptDateTimeConverter))]
            //public DateTime JavaScript { get { return this.DateTime; } }

            public DateTimeDocument(DateTime dateTime)
            {
                this.DateTime = dateTime;
            }
        }

        private sealed class TicksDateTimeConverter : DateTimeConverterBase
        {
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                long ticks = Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture);
                DateTime dateTime = new DateTime();
                dateTime = dateTime.AddTicks(ticks);

                return dateTime;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteValue(((DateTime)value).Ticks);
            }
        }

        private async Task TestOrderByDateTime(DocumentClient documentClient, DocumentCollection collection, IEnumerable<Document> documents)
        {
            IEnumerable<DateTimeDocument> dateTimeDocuments = documents
                .Select(document => JsonConvert.DeserializeObject<DateTimeDocument>(document.ToString()));

            foreach (PropertyInfo property in typeof(DateTimeDocument).GetProperties())
            {
                foreach (string sortOrder in new string[] { "ASC", "DESC" })
                {
                    IEnumerable<DateTime> expectedOrder = dateTimeDocuments
                        .Select((dateTimeDocument) => dateTimeDocument.DateTime);
                    if (sortOrder == "ASC")
                    {
                        expectedOrder = expectedOrder
                            .OrderBy((dateTime) => dateTime.Ticks);
                    }
                    else
                    {
                        expectedOrder = expectedOrder
                            .OrderByDescending((dateTime) => dateTime.Ticks);
                    }

                    string query = $@"
                        SELECT VALUE c.{nameof(DateTimeDocument.DateTime)}
                        FROM c
                        ORDER BY c.{property.Name} {sortOrder}";

                    FeedOptions feedOptions = new FeedOptions()
                    {
                        MaxBufferedItemCount = 1000,
                        MaxDegreeOfParallelism = 10,
                        MaxItemCount = documents.Count() / 10,
                        EnableCrossPartitionQuery = true,
                    };

                    List<DateTime> queryWithContinuationTokens = await QueryWithContinuationTokens<DateTime>(
                        documentClient,
                        collection,
                        query,
                        feedOptions);

                    List<DateTime> queryWithoutContinuationTokens = await QueryWithoutContinuationTokens<DateTime>(
                        documentClient,
                        collection,
                        query,
                        feedOptions);

                    Assert.IsTrue(
                        expectedOrder.SequenceEqual(queryWithContinuationTokens),
                        $@"
                        Query With Continuations: {query}
                        Expected: {JsonConvert.SerializeObject(expectedOrder)}
                        Actual: {JsonConvert.SerializeObject(queryWithContinuationTokens)}");

                    Assert.IsTrue(
                        expectedOrder.SequenceEqual(queryWithoutContinuationTokens),
                        $@"
                        Query Without Continuations: {query}
                        Expected: {JsonConvert.SerializeObject(expectedOrder)}
                        Actual: {JsonConvert.SerializeObject(queryWithoutContinuationTokens)}");
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryCrossPartitionTop()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            string partitionKey = "field_0";

            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> documents = util.GetDocuments(numberOfDocuments);

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.Partitioned,
                documents,
                this.TestQueryCrossPartitionTop,
                "/" + partitionKey);
        }

        private async Task TestQueryCrossPartitionTop(DocumentClient documentClient, DocumentCollection collection, IEnumerable<Document> documents)
        {
            List<string> queryFormats = new List<string>()
            {
                "SELECT {0} TOP {1} * FROM c",
                // Can't do order by since order by needs to look at all partitions before returning a single document =>
                // thus we can't tell how many documents the SDK needs to recieve.
                //"SELECT {0} TOP {1} * FROM c ORDER BY c._ts",

                // Can't do aggregates since that also retrieves more documents than the user sees
                //"SELECT {0} TOP {1} VALUE AVG(c._ts) FROM c",
            };

            foreach (string queryFormat in queryFormats)
            {
                foreach (bool useDistinct in new bool[] { true, false })
                {
                    foreach (int topCount in new int[] { 0, 1, 10 })
                    {
                        foreach (int pageSize in new int[] { 1, 10 })
                        {
                            // Run the query and use the query metrics to make sure the query didn't grab more documents
                            // than needed.

                            string query = string.Format(queryFormat, useDistinct ? "DISTINCT" : string.Empty, topCount);
                            FeedOptions feedOptions = new FeedOptions
                            {
                                MaxBufferedItemCount = 1000,
                                // Max DOP needs to be 0 since the query needs to run in serial => 
                                // otherwise the parallel code will prefetch from other partitions,
                                // since the first N-1 partitions might be empty.
                                MaxDegreeOfParallelism = 0,
                                MaxItemCount = pageSize,
                                EnableCrossPartitionQuery = true,
                                PopulateQueryMetrics = true,
                            };

                            IDocumentQuery<dynamic> documentQuery = documentClient
                                .CreateDocumentQuery<dynamic>(
                                    collection,
                                    query,
                                    feedOptions)
                                .AsDocumentQuery();

                            QueryMetrics aggregatedQueryMetrics = QueryMetrics.Zero;
                            int numberOfDocuments = 0;
                            while (documentQuery.HasMoreResults)
                            {
                                DocumentFeedResponse<dynamic> feedResponse = await documentQuery.ExecuteNextAsync<dynamic>();

                                numberOfDocuments += feedResponse.Count;

                                //v3 query metrics
                                //foreach (QueryMetrics queryMetrics in feedResponse.QueryMetrics.Values)
                                //{
                                //    aggregatedQueryMetrics += queryMetrics;
                                //}
                            }

                            Assert.IsTrue(
                                numberOfDocuments <= topCount,
                                $"Recieved {numberOfDocuments} documents with query: {query} and pageSize: {pageSize}");
                            if (!useDistinct)
                            {
                                Assert.IsTrue(
                                    aggregatedQueryMetrics.OutputDocumentCount <= topCount,
                                    $"Recieved {aggregatedQueryMetrics.OutputDocumentCount} documents query: {query} and pageSize: {pageSize}");
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryCrossPartitionOffsetLimit()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;

            Random rand = new Random(seed);
            List<DistinctMapTests.Person> people = new List<DistinctMapTests.Person>();

            for (int i = 0; i < numberOfDocuments; i++)
            {
                DistinctMapTests.Person person = DistinctMapTests.GetRandomPerson(rand);
                for (int j = 0; j < rand.Next(0, 4); j++)
                {
                    people.Add(person);
                }
            }

            List<string> documents = new List<string>();
            people = people.OrderBy((person) => Guid.NewGuid()).ToList();
            foreach (DistinctMapTests.Person person in people)
            {
                documents.Add(JsonConvert.SerializeObject(person));
            }

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned | CollectionTypes.NonPartitioned,
                documents,
                this.TestQueryCrossPartitionOffsetLimit,
                "/id");
        }

        private async Task TestQueryCrossPartitionOffsetLimit(
            DocumentClient documentClient,
            DocumentCollection documentCollection,
            IEnumerable<Document> documents)
        {
            // Check error on skip take for partitioned collection.
            bool isPartitioned = documentCollection.PartitionKey.Paths.Count() != 0;

            foreach (int offsetCount in new int[] { 0, 1, 10, 100, documents.Count() })
            {
                foreach (int limitCount in new int[] { 0, 1, 10, 100, documents.Count() })
                {
                    foreach (int pageSize in new int[] { 1, 10, 100, documents.Count() })
                    {
                        string query = $@"
                            SELECT VALUE c.guid 
                            FROM c
                            ORDER BY c.guid
                            OFFSET {offsetCount} LIMIT {limitCount}";

                        FeedOptions feedOptions = new FeedOptions()
                        {
                            EnableCrossPartitionQuery = isPartitioned,
                            MaxDegreeOfParallelism = isPartitioned ? 10 : 0,
                            MaxItemCount = 3,
                            MaxBufferedItemCount = isPartitioned ? 1000 : 0,
                            PopulateQueryMetrics = true,
                            // EnableCrossPartitionSkipTake = true,
                        };

                        IEnumerable<JToken> expectedResults = documents.Select(document => document.propertyBag);
                        // ORDER BY 
                        expectedResults = expectedResults.OrderBy(x => x["guid"].Value<string>(), StringComparer.Ordinal);

                        // SELECT VALUE c.name
                        expectedResults = expectedResults.Select(document => document["guid"]);

                        // SKIP TAKE
                        expectedResults = expectedResults.Skip(offsetCount);
                        expectedResults = expectedResults.Take(limitCount);

                        List<JToken> actualFromQueryWithoutContinutionToken = await QueryWithoutContinuationTokens<JToken>(
                            documentClient,
                            documentCollection,
                            query,
                            feedOptions);
                        Assert.IsTrue(
                            expectedResults.SequenceEqual(actualFromQueryWithoutContinutionToken, CrossPartitionQueryTests.JsonTokenEqualityComparer.Value),
                            $@"
                                {query} (without continuations) didn't match 
                                expected: {JsonConvert.SerializeObject(expectedResults)}
                                actual: {JsonConvert.SerializeObject(actualFromQueryWithoutContinutionToken)}");

                        List<JToken> actualFromQueryWithContinutionToken = await QueryWithContinuationTokens<JToken>(
                            documentClient,
                            documentCollection,
                            query,
                            feedOptions);
                        Assert.IsTrue(
                            expectedResults.SequenceEqual(actualFromQueryWithContinutionToken, CrossPartitionQueryTests.JsonTokenEqualityComparer.Value),
                            $@"
                                {query} (with continuations) didn't match 
                                expected: {JsonConvert.SerializeObject(expectedResults)}
                                actual: {JsonConvert.SerializeObject(actualFromQueryWithContinutionToken)}");
                    }
                }
            }
        }

        private struct CrossPartitionWithContinuationsArgs
        {
            public int NumberOfDocuments;
            public string PartitionKey;
            public string NumberField;
            public string BoolField;
            public string StringField;
            public string NullField;
            public string Children;
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryCrossPartitionWithContinuations()
        {
            int numberOfDocuments = 4;
            string partitionKey = "key";
            string numberField = "numberField";
            string boolField = "boolField";
            string stringField = "stringField";
            string nullField = "nullField";
            string children = "children";

            List<string> documents = new List<string>(numberOfDocuments);
            for (int i = 0; i < numberOfDocuments; ++i)
            {
                Document doc = new Document();
                doc.SetPropertyValue(partitionKey, i);
                doc.SetPropertyValue(numberField, i % 8);
                doc.SetPropertyValue(boolField, (i % 2) == 0 ? bool.TrueString : bool.FalseString);
                doc.SetPropertyValue(stringField, (i % 8).ToString());
                doc.SetPropertyValue(nullField, null);
                doc.SetPropertyValue(children, new[] { i % 2, i % 2, i % 3, i % 3, i });
                documents.Add(doc.ToString());
            }

            CrossPartitionWithContinuationsArgs args = new CrossPartitionWithContinuationsArgs()
            {
                NumberOfDocuments = numberOfDocuments,
                PartitionKey = partitionKey,
                NumberField = numberField,
                BoolField = boolField,
                StringField = stringField,
                NullField = nullField,
                Children = children,
            };

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete<CrossPartitionWithContinuationsArgs>(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned,
                documents,
                this.TestQueryCrossPartitionWithContinuations,
                args,
                "/" + partitionKey);
        }

        private async Task TestQueryCrossPartitionWithContinuations(DocumentClient documentClient, DocumentCollection collection, IEnumerable<Document> documents, CrossPartitionWithContinuationsArgs args)
        {
            int documentCount = args.NumberOfDocuments;
            string partitionKey = args.PartitionKey;
            string numberField = args.NumberField;
            string boolField = args.BoolField;
            string stringField = args.StringField;
            string nullField = args.NullField;
            string children = args.Children;

            // Try resuming from bad continuation token
            #region BadContinuations
            try
            {
                await Client.CreateDocumentQuery<Document>(
                    collection,
                    new FeedOptions
                    {
                        EnableCrossPartitionQuery = true,
                        MaxDegreeOfParallelism = -1,
                        RequestContinuationToken = Guid.NewGuid().ToString(),
                        MaxItemCount = 10,
                    }).AsDocumentQuery().ExecuteNextAsync();

                Assert.Fail("Expect exception");
            }
            catch (BadRequestException)
            {
            }

            try
            {
                CompositeContinuationToken[] tokens = new CompositeContinuationToken[1];
                tokens[0] = new CompositeContinuationToken { Range = Range<string>.GetPointRange(string.Empty) };
                await Client.CreateDocumentQuery<Document>(
                    collection,
                    new FeedOptions
                    {
                        EnableCrossPartitionQuery = true,
                        MaxDegreeOfParallelism = -1,
                        RequestContinuationToken = JsonConvert.SerializeObject(tokens),
                        MaxItemCount = 10,
                    }).AsDocumentQuery().ExecuteNextAsync();

                Assert.Fail("Expect exception");
            }
            catch (BadRequestException)
            {
            }

            try
            {
                await Client.CreateDocumentQuery<Document>(
                    collection,
                    "SELECT TOP 10 * FROM r",
                    new FeedOptions
                    {
                        EnableCrossPartitionQuery = true,
                        MaxDegreeOfParallelism = -1,
                        RequestContinuationToken = "{'top':11}",
                        MaxItemCount = 10,
                    }).AsDocumentQuery().ExecuteNextAsync();

                Assert.Fail("Expect exception");
            }
            catch (BadRequestException)
            {
            }

            try
            {
                await Client.CreateDocumentQuery<Document>(
                    collection,
                    "SELECT * FROM r ORDER BY r.field1",
                    new FeedOptions
                    {
                        EnableCrossPartitionQuery = true,
                        MaxDegreeOfParallelism = -1,
                        RequestContinuationToken = "{'compositeToken':{'range':{'min':'05C1E9CD673398','max':'FF'}}, 'orderByItems':[{'item':2}, {'item':1}]}",
                        MaxItemCount = 10,
                    }).AsDocumentQuery().ExecuteNextAsync();

                Assert.Fail("Expect exception");
            }
            catch (BadRequestException)
            {
            }

            try
            {
                await Client.CreateDocumentQuery<Document>(
                    collection,
                    "SELECT * FROM r ORDER BY r.field1, r.field2",
                    new FeedOptions
                    {
                        EnableCrossPartitionQuery = true,
                        MaxDegreeOfParallelism = -1,
                        RequestContinuationToken = "{'compositeToken':{'range':{'min':'05C1E9CD673398','max':'FF'}}, 'orderByItems':[{'item':2}, {'item':1}]}",
                        MaxItemCount = 10,
                    }).AsDocumentQuery().ExecuteNextAsync();

                Assert.Fail("Expect exception");
            }
            catch (BadRequestException)
            {
            }
            #endregion

            DocumentFeedResponse<dynamic> responseWithEmptyContinuationExpected = await Client.CreateDocumentQuery<Document>(
                collection,
                string.Format(CultureInfo.InvariantCulture, "SELECT TOP 1 * FROM r ORDER BY r.{0}", partitionKey),
                new FeedOptions
                {
                    EnableCrossPartitionQuery = true,
                    MaxDegreeOfParallelism = 10,
                    MaxItemCount = -1,
                }).AsDocumentQuery().ExecuteNextAsync();

            Assert.AreEqual(null, responseWithEmptyContinuationExpected.ResponseContinuation);

            string[] queries = new[]
            {
                null,
                $"SELECT * FROM r",
                $"SELECT * FROM r WHERE r.{partitionKey} BETWEEN 0 AND {documentCount}",
                $"SELECT r.{partitionKey} FROM r JOIN c in r.{children}",
                $"SELECT * FROM r ORDER BY r.{partitionKey}",
                $"SELECT * FROM r WHERE r.{partitionKey} BETWEEN 0 AND {documentCount} ORDER BY r.{numberField} DESC",
                $"SELECT r.{partitionKey} FROM r JOIN c in r.{children} ORDER BY r.{numberField}",
                $"SELECT TOP 10 * FROM r",
                $"SELECT TOP 10 * FROM r WHERE r.{partitionKey} BETWEEN 0 AND {documentCount} ORDER BY r.{partitionKey} DESC",
                $"SELECT TOP 10 * FROM r ORDER BY r.{numberField}",
                $"SELECT TOP 40 r.{partitionKey} FROM r JOIN c in r.{children} ORDER BY r.{numberField} DESC",
                $"SELECT * FROM r WHERE r.{partitionKey} BETWEEN 0 AND {documentCount} ORDER BY r.{boolField} DESC",
                $"SELECT * FROM r WHERE r.{partitionKey} BETWEEN 0 AND {documentCount} ORDER BY r.{stringField} DESC",
                $"SELECT * FROM r WHERE r.{partitionKey} BETWEEN 0 AND {documentCount} ORDER BY r.{nullField} DESC",
            };

            foreach (string query in queries)
            {
                List<Document> expectedValues;
                if (string.IsNullOrEmpty(query))
                {
                    expectedValues = Client.CreateDocumentQuery<Document>(
                        collection,
                        new FeedOptions { MaxDegreeOfParallelism = 0, EnableCrossPartitionQuery = true }).ToList();
                }
                else
                {
                    expectedValues = Client.CreateDocumentQuery<Document>(
                        collection,
                        query,
                        new FeedOptions { MaxDegreeOfParallelism = 0, EnableCrossPartitionQuery = true }).ToList();
                }

                foreach (int pageSize in new int[] { 1, documentCount / 2, documentCount })
                {
                    List<Document> retrievedDocuments = new List<Document>();

                    IDocumentQuery<Document> documentQuery;
                    string continuationToken = default(string);
                    bool hasMoreResults;

                    do
                    {
                        FeedOptions feedOptions = new FeedOptions
                        {
                            EnableCrossPartitionQuery = true,
                            MaxDegreeOfParallelism = 10000,
                            MaxBufferedItemCount = 10000,
                            RequestContinuationToken = continuationToken,
                            MaxItemCount = pageSize,
                        };

                        if (string.IsNullOrEmpty(query))
                        {
                            documentQuery = Client.CreateDocumentQuery<Document>(
                                collection,
                                feedOptions).AsDocumentQuery();
                        }
                        else
                        {
                            documentQuery = Client.CreateDocumentQuery<Document>(
                                collection,
                                query,
                                feedOptions).AsDocumentQuery();
                        }
                        DocumentFeedResponse<Document> response;
                        try
                        {
                            response = await documentQuery.ExecuteNextAsync<Document>();
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }

                        Assert.IsTrue(
                            response.Count <= pageSize,
                            string.Format(
                            CultureInfo.InvariantCulture,
                            "Actual result count {0} should be less or equal to requested page size {1}. Query: {2}, Continuation: {3}, Results.Count: {4}",
                            response.Count,
                            pageSize,
                            query,
                            continuationToken,
                            retrievedDocuments.Count));
                        continuationToken = response.ResponseContinuation;
                        retrievedDocuments.AddRange(response);

                        hasMoreResults = documentQuery.HasMoreResults;
                        documentQuery.Dispose();
                    } while (hasMoreResults);

                    Assert.AreEqual(
                        string.Join(", ", expectedValues.Select(doc => doc.GetPropertyValue<int>(partitionKey))),
                        string.Join(", ", retrievedDocuments.Select(doc => doc.GetPropertyValue<int>(partitionKey))),
                        string.Format(CultureInfo.InvariantCulture, "query: {0}, page size: {1}", query, pageSize));
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestMultiOrderByQueries()
        {
            int numberOfDocuments = 4;

            List<string> documents = new List<string>(numberOfDocuments);
            Random random = new Random(1234);
            for (int i = 0; i < numberOfDocuments; ++i)
            {
                MultiOrderByDocument multiOrderByDocument = DocumentCrossPartitionQueryTests.GenerateMultiOrderByDocument(random);
                int numberOfDuplicates = 5;

                for (int j = 0; j < numberOfDuplicates; j++)
                {
                    // Add the document itself for exact duplicates
                    documents.Add(JsonConvert.SerializeObject(multiOrderByDocument));

                    // Permute all the fields so that there are duplicates with tie breaks
                    MultiOrderByDocument numberClone = MultiOrderByDocument.GetClone(multiOrderByDocument);
                    numberClone.NumberField = random.Next(0, 5);
                    documents.Add(JsonConvert.SerializeObject(numberClone));

                    MultiOrderByDocument stringClone = MultiOrderByDocument.GetClone(multiOrderByDocument);
                    stringClone.StringField = random.Next(0, 5).ToString();
                    documents.Add(JsonConvert.SerializeObject(stringClone));

                    MultiOrderByDocument boolClone = MultiOrderByDocument.GetClone(multiOrderByDocument);
                    boolClone.BoolField = random.Next(0, 2) % 2 == 0;
                    documents.Add(JsonConvert.SerializeObject(boolClone));

                    // Also fuzz what partition it goes to
                    MultiOrderByDocument partitionClone = MultiOrderByDocument.GetClone(multiOrderByDocument);
                    partitionClone.PartitionKey = random.Next(0, 5);
                    documents.Add(JsonConvert.SerializeObject(partitionClone));
                }
            }

            IndexingPolicy indexingPolicy = new IndexingPolicy()
            {
                CompositeIndexes = new Collection<Collection<CompositePath>>()
                {
                    // Simple
                    new Collection<CompositePath>()
                    {
                        new CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.NumberField),
                            Order = CompositePathSortOrder.Ascending,
                        },
                        new CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.StringField),
                            Order = CompositePathSortOrder.Descending,
                        }
                    },

                    // Max Columns
                    new Collection<CompositePath>()
                    {
                        new CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.NumberField),
                            Order = CompositePathSortOrder.Descending,
                        },
                        new CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.StringField),
                            Order = CompositePathSortOrder.Ascending,
                        },
                        new CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.NumberField2),
                            Order = CompositePathSortOrder.Descending,
                        },
                        new CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.StringField2),
                            Order = CompositePathSortOrder.Ascending,
                        }
                    },

                    // All primitive values
                    new Collection<CompositePath>()
                    {
                        new CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.NumberField),
                            Order = CompositePathSortOrder.Descending,
                        },
                        new CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.StringField),
                            Order = CompositePathSortOrder.Ascending,
                        },
                        new CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.BoolField),
                            Order = CompositePathSortOrder.Descending,
                        },
                        new CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.NullField),
                            Order = CompositePathSortOrder.Ascending,
                        }
                    },

                    // Primitive and Non Primitive (waiting for composite on objects and arrays)
                    //new Collection<CompositePath>()
                    //{
                    //    new CompositePath()
                    //    {
                    //        Path = "/" + nameof(MultiOrderByDocument.NumberField),
                    //    },
                    //    new CompositePath()
                    //    {
                    //        Path = "/" + nameof(MultiOrderByDocument.ObjectField),
                    //    },
                    //    new CompositePath()
                    //    {
                    //        Path = "/" + nameof(MultiOrderByDocument.StringField),
                    //    },
                    //    new CompositePath()
                    //    {
                    //        Path = "/" + nameof(MultiOrderByDocument.ArrayField),
                    //    },
                    //},

                    // Long strings
                    new Collection<CompositePath>()
                    {
                        new CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.StringField),
                        },
                        new CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.ShortStringField),
                        },
                        new CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.MediumStringField),
                        },
                        new CompositePath()
                        {
                            Path = "/" + nameof(MultiOrderByDocument.LongStringField),
                        }
                    },

                    // System Properties 
                    //new Collection<CompositePath>()
                    //{
                    //    new CompositePath()
                    //    {
                    //        Path = "/id",
                    //    },
                    //    new CompositePath()
                    //    {
                    //        Path = "/_ts",
                    //    },
                    //    new CompositePath()
                    //    {
                    //        Path = "/_etag",
                    //    },

                    //    // _rid is not allowed
                    //    //new CompositePath()
                    //    //{
                    //    //    Path = "/_rid",
                    //    //},
                    //},
                }
            };

            await DocumentCrossPartitionQueryTests.RunWithApiVersion(
                HttpConstants.Versions.v2018_09_17,
                () =>
                {
                    return DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                        ConnectionModes.Direct,
                        CollectionTypes.Partitioned | CollectionTypes.NonPartitioned,
                        documents,
                        this.TestMultiOrderByQueries,
                        "/" + nameof(MultiOrderByDocument.PartitionKey),
                        indexingPolicy,
                        DocumentCrossPartitionQueryTests.CreateNewDocumentClient);
                });
        }

        private sealed class MultiOrderByDocument
        {
            public double NumberField { get; set; }
            public double NumberField2 { get; set; }
            public bool BoolField { get; set; }
            public string StringField { get; set; }
            public string StringField2 { get; set; }
            public object NullField { get; set; }
            public object ObjectField { get; set; }
            public List<object> ArrayField { get; set; }
            public string ShortStringField { get; set; }
            public string MediumStringField { get; set; }
            public string LongStringField { get; set; }
            public int PartitionKey { get; set; }

            public static MultiOrderByDocument GetClone(MultiOrderByDocument other)
            {
                return JsonConvert.DeserializeObject<MultiOrderByDocument>(JsonConvert.SerializeObject(other));
            }
        }

        private static MultiOrderByDocument GenerateMultiOrderByDocument(Random random)
        {
            return new MultiOrderByDocument()
            {
                NumberField = random.Next(0, 5),
                NumberField2 = random.Next(0, 5),
                BoolField = (random.Next() % 2) == 0,
                StringField = random.Next(0, 5).ToString(),
                StringField2 = random.Next(0, 5).ToString(),
                NullField = null,
                ObjectField = new object(),
                ArrayField = new List<object>(),
                ShortStringField = new string('a', random.Next(0, 100)),
                MediumStringField = new string('a', random.Next(100, 128)),
                //Max precisions is 2kb / number of terms
                LongStringField = new string('a', random.Next(128, 255)),
                PartitionKey = random.Next(0, 5),
            };
        }

        private async Task TestMultiOrderByQueries(
            DocumentClient documentClient,
            DocumentCollection documentCollection,
            IEnumerable<Document> documents)
        {
            // For every composite index
            foreach (Collection<CompositePath> compositeIndex in documentCollection.IndexingPolicy.CompositeIndexes)
            {
                // for every order
                foreach (bool invert in new bool[] { false, true })
                {
                    foreach (bool hasTop in new bool[] { false, true })
                    {
                        foreach (bool hasFilter in new bool[] { false, true })
                        {
                            // Generate a multi order by from that index
                            List<string> orderByItems = new List<string>();
                            List<string> selectItems = new List<string>();
                            bool isDesc;
                            foreach (CompositePath compositePath in compositeIndex)
                            {
                                isDesc = compositePath.Order == CompositePathSortOrder.Descending ? true : false;
                                if (invert)
                                {
                                    isDesc = !isDesc;
                                }

                                string isDescString = isDesc ? "DESC" : "ASC";
                                orderByItems.Add($"root.{compositePath.Path.Replace("/", "")} { isDescString }");
                                selectItems.Add($"root.{compositePath.Path.Replace("/", "")}");
                            }

                            const int topCount = 10;
                            string topString = hasTop ? $"TOP {topCount}" : string.Empty;
                            string whereString = hasFilter ? $"WHERE root.{nameof(MultiOrderByDocument.NumberField)} % 2 = 0" : string.Empty;
                            string query = $@"
                                    SELECT { topString } VALUE [{string.Join(", ", selectItems)}] 
                                    FROM root { whereString }
                                    ORDER BY {string.Join(", ", orderByItems)}";
#if false
                            // Used for debugging which partitions have which documents
                            IReadOnlyList<PartitionKeyRange> pkranges = GetPartitionKeyRanges(documentCollection);
                            foreach (PartitionKeyRange pkrange in pkranges)
                            {
                                List<dynamic> documentsWithinPartition = documentClient.CreateDocumentQuery(
                                    documentCollection,
                                    query,
                                    new FeedOptions()
                                    {
                                        EnableScanInQuery = true,
                                        PartitionKeyRangeId = pkrange.Id
                                    }).ToList();
                            }
#endif

                            #region ExpectedUsingLinq
                            List<MultiOrderByDocument> castedDocuments = documents
                                .Select(x => JsonConvert.DeserializeObject<MultiOrderByDocument>(JsonConvert.SerializeObject(x)))
                                .ToList();

                            if (hasFilter)
                            {
                                castedDocuments = castedDocuments.Where(document => document.NumberField % 2 == 0).ToList();
                            }

                            IOrderedEnumerable<MultiOrderByDocument> oracle;
                            CompositePath firstCompositeIndex = compositeIndex.First();

                            isDesc = firstCompositeIndex.Order == CompositePathSortOrder.Descending ? true : false;
                            if (invert)
                            {
                                isDesc = !isDesc;
                            }

                            if (isDesc)
                            {
                                oracle = castedDocuments.OrderByDescending(x => x.GetType().GetProperty(firstCompositeIndex.Path.Replace("/", "")).GetValue(x, null));
                            }
                            else
                            {
                                oracle = castedDocuments.OrderBy(x => x.GetType().GetProperty(firstCompositeIndex.Path.Replace("/", "")).GetValue(x, null));
                            }

                            foreach (CompositePath compositePath in compositeIndex.Skip(1))
                            {
                                isDesc = compositePath.Order == CompositePathSortOrder.Descending ? true : false;
                                if (invert)
                                {
                                    isDesc = !isDesc;
                                }

                                if (isDesc)
                                {
                                    oracle = oracle.ThenByDescending(x => x.GetType().GetProperty(compositePath.Path.Replace("/", "")).GetValue(x, null));
                                }
                                else
                                {
                                    oracle = oracle.ThenBy(x => x.GetType().GetProperty(compositePath.Path.Replace("/", "")).GetValue(x, null));
                                }
                            }

                            List<List<object>> expected = new List<List<object>>();
                            foreach (MultiOrderByDocument document in oracle)
                            {
                                List<object> projectedItems = new List<object>();
                                foreach (CompositePath compositePath in compositeIndex)
                                {
                                    projectedItems.Add(typeof(MultiOrderByDocument).GetProperty(compositePath.Path.Replace("/", "")).GetValue(document, null));
                                }

                                expected.Add(projectedItems);
                            }

                            if (hasTop)
                            {
                                expected = expected.Take(topCount).ToList();
                            }

                            #endregion

                            bool isPartitioned = documentCollection.PartitionKey.Paths.Count() != 0;
                            FeedOptions feedOptions = new FeedOptions()
                            {
                                EnableCrossPartitionQuery = isPartitioned,
                                MaxDegreeOfParallelism = isPartitioned ? 10 : 0,
                                MaxItemCount = 3,
                                MaxBufferedItemCount = isPartitioned ? 1000 : 0,
                                PopulateQueryMetrics = true,
                            };

                            List<List<object>> actualFromQueryWithoutContinutionToken = await QueryWithoutContinuationTokens<List<object>>(
                                documentClient,
                                documentCollection,
                                query,
                                feedOptions);
                            this.AssertMultiOrderByResults(expected, actualFromQueryWithoutContinutionToken, query + "(without continuations)");

                            List<List<object>> actualFromQueryWithContinutionToken = await QueryWithContinuationTokens<List<object>>(
                                documentClient,
                                documentCollection,
                                query,
                                feedOptions);
                            this.AssertMultiOrderByResults(expected, actualFromQueryWithContinutionToken, query + "(with continuations)");
                        }
                    }
                }
            }
        }

        private void AssertMultiOrderByResults(List<List<object>> expected, List<List<object>> actual, string query)
        {
            IEnumerable<Tuple<List<JToken>, List<JToken>>> expectedZippedWithActual = expected
                .Zip(actual, (first, second) =>
                new Tuple<List<JToken>, List<JToken>>(
                    first.Select(x => x == null ? null : JToken.FromObject(x)).ToList(),
                    second.Select(x => x == null ? null : JToken.FromObject(x)).ToList()));

            foreach (Tuple<List<JToken>, List<JToken>> expectedAndActual in expectedZippedWithActual)
            {
                List<JToken> first = expectedAndActual.Item1;
                List<JToken> second = expectedAndActual.Item2;
                Assert.IsTrue(
                    first.SequenceEqual(second, CrossPartitionQueryTests.JsonTokenEqualityComparer.Value),
                    $@"
                        query: {query}: 
                        first: {JsonConvert.SerializeObject(first)}
                        second: {JsonConvert.SerializeObject(second)}
                        expected: {JsonConvert.SerializeObject(expected).Replace(".0", "")}
                        actual: {JsonConvert.SerializeObject(actual).Replace(".0", "")}");
            }
        }

        /// <summary>
        /// Tests to see if the RequestCharge from the query metrics from each partition sums up to the total request charge of the feedresponse for each continuation of the query.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryMetricsRUPerPartition()
        {
            int seed = 1234;
            uint numberOfDocuments = 128;

            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> documents = util.GetDocuments(numberOfDocuments);

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned | CollectionTypes.NonPartitioned,
                documents,
                this.TestQueryMetricsRUPerPartition);
        }

        private async Task TestQueryMetricsRUPerPartition(
            DocumentClient documentClient,
            DocumentCollection documentCollection,
            IEnumerable<Document> documents)
        {
            for (int iteration = 0; iteration < 10; iteration++)
            {
                FeedOptions feedOptions = new FeedOptions
                {
                    EnableCrossPartitionQuery = true,
                    PopulateQueryMetrics = true,
                    MaxItemCount = 10,
                    MaxDegreeOfParallelism = 10,
                };

                IDocumentQuery<dynamic> documentQuery = documentClient.CreateDocumentQuery(
                    documentCollection,
                    "SELECT * FROM c ORDER BY c._ts",
                    feedOptions).AsDocumentQuery();

                List<DocumentFeedResponse<dynamic>> feedResponses = new List<DocumentFeedResponse<dynamic>>();
                while (documentQuery.HasMoreResults)
                {
                    DocumentFeedResponse<dynamic> feedResonse = await documentQuery.ExecuteNextAsync();
                    feedResponses.Add(feedResonse);
                }

                List<QueryMetrics> queryMetricsList = new List<QueryMetrics>();
                double aggregatedRequestCharge = 0;
                bool firstDocumentFeedResponse = true;
                foreach (DocumentFeedResponse<dynamic> feedResponse in feedResponses)
                {
                    aggregatedRequestCharge += feedResponse.RequestCharge;
                    foreach (KeyValuePair<string, QueryMetrics> kvp in feedResponse.QueryMetrics)
                    {
                        string partitionKeyRangeId = kvp.Key;
                        QueryMetrics queryMetrics = kvp.Value;
                        if (firstDocumentFeedResponse)
                        {
                            // For an orderby query the first execution should fan out to every partition
                            Assert.IsTrue(queryMetrics.ClientSideMetrics.RequestCharge > 0, "queryMetrics.RequestCharge was not > 0 for PKRangeId: {0}", partitionKeyRangeId);
                        }

                        queryMetricsList.Add(queryMetrics);
                    }
                    firstDocumentFeedResponse = false;
                }

                QueryMetrics aggregatedQueryMetrics = QueryMetrics.CreateFromIEnumerable(queryMetricsList);
                double requestChargeFromMetrics = aggregatedQueryMetrics.ClientSideMetrics.RequestCharge;

                Assert.IsTrue(aggregatedRequestCharge > 0, "aggregatedRequestCharge was not > 0");
                Assert.IsTrue(requestChargeFromMetrics > 0, "requestChargeFromMetrics was not > 0");
                Assert.AreEqual(aggregatedRequestCharge, requestChargeFromMetrics, 0.1 * aggregatedRequestCharge, "Request Charge from DocumentFeedResponse and QueryMetrics do not equal.");
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestHeadersAcrossPartitionsAndParallelism()
        {
            int seed = 1234;
            uint numberOfDocuments = 128;

            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> documents = util.GetDocuments(numberOfDocuments);

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.Partitioned,
                documents,
                this.TestHeadersAcrossPartitionsAndParallelism);
        }

        private sealed class Headers
        {
            public double TotalRUs { get; set; }
            public long NumberOfDocuments { get; set; }
            public long RetrievedDocumentCount { get; set; }
            public long RetrievedDocumentSize { get; set; }
            public long OutputDocumentCount { get; set; }
            public long OutputDocumentSize { get; set; }

            public override bool Equals(object obj)
            {
                Headers headers = obj as Headers;
                if (headers != null)
                {
                    return Headers.Equals(this, headers);
                }
                else
                {
                    return false;
                }
            }

            public override int GetHashCode()
            {
                return 0;
            }

            private static bool Equals(Headers headers1, Headers headers2)
            {
                return Math.Abs(headers1.TotalRUs - headers2.TotalRUs) < 10E-4 &&
                    headers1.NumberOfDocuments == headers2.NumberOfDocuments &&
                    headers1.RetrievedDocumentCount == headers2.RetrievedDocumentCount &&
                    headers1.RetrievedDocumentSize == headers2.RetrievedDocumentSize &&
                    headers1.OutputDocumentCount == headers2.OutputDocumentCount &&
                    headers1.OutputDocumentSize == headers2.OutputDocumentSize;
            }
        }

        private async Task TestHeadersAcrossPartitionsAndParallelism(
            DocumentClient documentClient,
            DocumentCollection documentCollection,
            IEnumerable<Document> documents)
        {
            for (int iteration = 0; iteration < 10; iteration++)
            {
                Dictionary<FeedOptions, Headers> feedOptionsToHeaders = new Dictionary<FeedOptions, Headers>();
                foreach (int maxDegreeOfParallelism in new int[] { 0, 1, 100 })
                {
                    FeedOptions feedOptions = new FeedOptions
                    {
                        EnableCrossPartitionQuery = true,
                        MaxBufferedItemCount = 7000,
                        MaxDegreeOfParallelism = maxDegreeOfParallelism,
                        PopulateQueryMetrics = true,
                    };
                    IDocumentQuery<dynamic> query = documentClient.CreateDocumentQuery(
                        documentCollection,
                        "SELECT * FROM c ORDER BY c._ts",
                        feedOptions).AsDocumentQuery();

                    Headers headers = new Headers();
                    while (query.HasMoreResults)
                    {
                        DocumentFeedResponse<dynamic> page = await query.ExecuteNextAsync().ConfigureAwait(false);
                        headers.TotalRUs += page.RequestCharge;
                        headers.NumberOfDocuments += page.Count;

                        // QueryMetrics
                        //foreach (QueryMetrics queryMetrics in page.QueryMetrics.Values)
                        //{
                        //    headers.RetrievedDocumentCount += queryMetrics.RetrievedDocumentCount;
                        //    headers.RetrievedDocumentSize += queryMetrics.RetrievedDocumentSize;
                        //    headers.OutputDocumentCount += queryMetrics.OutputDocumentCount;
                        //    headers.OutputDocumentSize += queryMetrics.OutputDocumentSize;
                        //}
                    }

                    feedOptionsToHeaders[feedOptions] = headers;
                }

                foreach (FeedOptions key1 in feedOptionsToHeaders.Keys)
                {
                    foreach (FeedOptions key2 in feedOptionsToHeaders.Keys)
                    {
                        Headers headers1 = feedOptionsToHeaders[key1];
                        Headers headers2 = feedOptionsToHeaders[key2];
                        Assert.AreEqual(
                            headers1,
                            headers2,
                            $"FeedOptions1: {JsonConvert.SerializeObject(key1)} resulted in: {JsonConvert.SerializeObject(headers1, Newtonsoft.Json.Formatting.Indented)} while" +
                            $"FeedOptions2: {JsonConvert.SerializeObject(key2)} resulted in: {JsonConvert.SerializeObject(headers2, Newtonsoft.Json.Formatting.Indented)}");
                    }
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestQueryMetricsPartitionKeyRangeId()
        {
            // For partitioned collections that run through default document query execution context
            // The partition key range id is always "0".
            int seed = 1234;
            uint numberOfDocuments = 128;

            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> documents = util.GetDocuments(numberOfDocuments);

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.Partitioned,
                documents,
                this.TestQueryMetricsPartitionKeyRangeId);
        }

        private async Task TestQueryMetricsPartitionKeyRangeId(
            DocumentClient documentClient,
            DocumentCollection documentCollection,
            IEnumerable<Document> documents)
        {
            //--------------------------------------------------------------------------------
            // Physical PartitionKeyRangeId
            //--------------------------------------------------------------------------------
            {
                bool isPartitioned = documentCollection.PartitionKey.Paths.Count() != 0;
                FeedOptions feedOptions = new FeedOptions
                {
                    EnableCrossPartitionQuery = false,
                    MaxBufferedItemCount = 0,
                    MaxDegreeOfParallelism = 0,
                    PopulateQueryMetrics = true,
                    PartitionKeyRangeId = "3",
                };

                QueryMetrics aggregatedQueryMetrics = QueryMetrics.Zero;
                using (IDocumentQuery<JToken> query = documentClient
                    .CreateDocumentQuery<JToken>(documentCollection, "SELECT * FROM c", feedOptions)
                    .AsDocumentQuery())
                {
                    while (query.HasMoreResults)
                    {
                        DocumentFeedResponse<JToken> page = await query.ExecuteNextAsync<JToken>();
                        foreach (QueryMetrics queryMetrics in page.QueryMetrics.Values)
                        {
                            aggregatedQueryMetrics += queryMetrics;
                        }
                    }
                }

                IEnumerable<string> partitionIdentifiers = aggregatedQueryMetrics
                    .ClientSideMetrics
                    .FetchExecutionRanges
                    .Select(fetch => fetch.PartitionId)
                    .Distinct();

                int numberOfPartitionKeyRangesInQueryMetrics = partitionIdentifiers.Count();

                Assert.AreEqual(1, numberOfPartitionKeyRangesInQueryMetrics);
                Assert.AreEqual("3", partitionIdentifiers.Single());
            }

            //--------------------------------------------------------------------------------
            // Logical Partition Key
            //--------------------------------------------------------------------------------
            {
                FeedOptions feedOptions = new FeedOptions
                {
                    EnableCrossPartitionQuery = false,
                    MaxBufferedItemCount = 0,
                    MaxDegreeOfParallelism = 0,
                    PopulateQueryMetrics = true,
                    PartitionKey = new PartitionKey("New York"),
                };

                QueryMetrics aggregatedQueryMetrics = QueryMetrics.Zero;
                using (IDocumentQuery<JToken> query = documentClient
                    .CreateDocumentQuery<JToken>(documentCollection, "SELECT * FROM c", feedOptions)
                    .AsDocumentQuery())
                {
                    while (query.HasMoreResults)
                    {
                        DocumentFeedResponse<JToken> page = await query.ExecuteNextAsync<JToken>();
                        foreach (QueryMetrics queryMetrics in page.QueryMetrics.Values)
                        {
                            aggregatedQueryMetrics += queryMetrics;
                        }
                    }
                }

                IEnumerable<string> partitionIdentifiers = aggregatedQueryMetrics
                    .ClientSideMetrics
                    .FetchExecutionRanges
                    .Select(fetch => fetch.PartitionId)
                    .Distinct();

                int numberOfPartitionKeyRangesInQueryMetrics = partitionIdentifiers.Count();

                Assert.AreEqual(1, numberOfPartitionKeyRangesInQueryMetrics);
                Assert.AreEqual(@"PKId([""New York""])", partitionIdentifiers.Single());
            }

            //--------------------------------------------------------------------------------
            // Many Partitions Using Service Interop
            //--------------------------------------------------------------------------------
            {
                FeedOptions feedOptions = new FeedOptions
                {
                    EnableCrossPartitionQuery = true,
                    MaxBufferedItemCount = 0,
                    MaxDegreeOfParallelism = 0,
                    PopulateQueryMetrics = true
                };

                QueryMetrics aggregatedQueryMetrics = QueryMetrics.Zero;
                using (IDocumentQuery<JToken> query = documentClient
                    .CreateDocumentQuery<JToken>(documentCollection, "SELECT * FROM c", feedOptions)
                    .AsDocumentQuery())
                {
                    while (query.HasMoreResults)
                    {
                        DocumentFeedResponse<JToken> page = await query.ExecuteNextAsync<JToken>();
                        foreach (QueryMetrics queryMetrics in page.QueryMetrics.Values)
                        {
                            aggregatedQueryMetrics += queryMetrics;
                        }
                    }
                }

                int numberOfPartitionKeyRangesInQueryMetrics = aggregatedQueryMetrics
                    .ClientSideMetrics
                    .FetchExecutionRanges
                    .Select(fetch => fetch.PartitionId)
                    .Distinct()
                    .Count();

                int numberOfPartitionKeyRanges = DocumentCrossPartitionQueryTests.GetPartitionKeyRanges(documentCollection).Count;

                Assert.AreEqual(numberOfPartitionKeyRanges, numberOfPartitionKeyRangesInQueryMetrics);
            }
        }

        [TestMethod]
        public async Task TestGroupByQuery()
        {
            string[] documents = new string[]
            {
                @" { ""id"": ""01"", ""name"": ""John"", ""age"": 11, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""02"", ""name"": ""Mady"", ""age"": 15, ""gender"": ""F"", ""team"": ""C"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60292 }, ""scores"": [52, 13, 94, 31] } ",
                @" { ""id"": ""03"", ""name"": ""John"", ""age"": 13, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60292 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""04"", ""name"": ""Mary"", ""age"": 18, ""gender"": ""F"", ""team"": ""D"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [23, 11, 11, 66] } ",
                @" { ""id"": ""05"", ""name"": ""Fred"", ""age"": 17, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60292 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""06"", ""name"": ""Adam"", ""age"": 16, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""07"", ""name"": ""Alex"", ""age"": 13, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [52, 13, 94, 31] } ",
                @" { ""id"": ""08"", ""name"": ""Fred"", ""age"": 12, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""09"", ""name"": ""Fred"", ""age"": 15, ""gender"": ""M"", ""team"": ""D"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""10"", ""name"": ""Mary"", ""age"": 18, ""gender"": ""F"", ""team"": ""A"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [23, 11, 11, 66] } ",
                @" { ""id"": ""11"", ""name"": ""Fred"", ""age"": 18, ""gender"": ""M"", ""team"": ""D"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""12"", ""name"": ""Abby"", ""age"": 17, ""gender"": ""F"", ""team"": ""C"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""13"", ""name"": ""John"", ""age"": 16, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32801 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""14"", ""name"": ""Ella"", ""age"": 16, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60291 }, ""scores"": [23, 11, 11, 66] } ",
                @" { ""id"": ""15"", ""name"": ""Mary"", ""age"": 18, ""gender"": ""F"", ""team"": ""D"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [23, 11, 11, 66] } ",
                @" { ""id"": ""16"", ""name"": ""Carl"", ""age"": 17, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [52, 13, 94, 31] } ",
                @" { ""id"": ""17"", ""name"": ""Mady"", ""age"": 18, ""gender"": ""F"", ""team"": ""C"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60292 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""18"", ""name"": ""Mike"", ""age"": 15, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98101 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""19"", ""name"": ""Eric"", ""age"": 16, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32801 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""20"", ""name"": ""Ryan"", ""age"": 11, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""21"", ""name"": ""Alex"", ""age"": 14, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""22"", ""name"": ""Mike"", ""age"": 15, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""23"", ""name"": ""John"", ""age"": 14, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""24"", ""name"": ""Dave"", ""age"": 15, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""25"", ""name"": ""Lisa"", ""age"": 11, ""gender"": ""F"", ""team"": ""A"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32801 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""26"", ""name"": ""Zara"", ""age"": 11, ""gender"": ""F"", ""team"": ""D"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""27"", ""name"": ""Abby"", ""age"": 17, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98101 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""28"", ""name"": ""Abby"", ""age"": 13, ""gender"": ""F"", ""team"": ""C"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60291 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""29"", ""name"": ""Lucy"", ""age"": 14, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60292 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""30"", ""name"": ""Lucy"", ""age"": 14, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""31"", ""name"": ""Bill"", ""age"": 13, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60292 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""32"", ""name"": ""Bill"", ""age"": 11, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""33"", ""name"": ""Zara"", ""age"": 12, ""gender"": ""F"", ""team"": ""C"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60291 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""34"", ""name"": ""Adam"", ""age"": 13, ""gender"": ""M"", ""team"": ""D"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60291 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""35"", ""name"": ""Bill"", ""age"": 13, ""gender"": ""M"", ""team"": ""D"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98101 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""36"", ""name"": ""Alex"", ""age"": 15, ""gender"": ""M"", ""team"": ""D"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60291 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""37"", ""name"": ""Lucy"", ""age"": 14, ""gender"": ""F"", ""team"": ""A"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""38"", ""name"": ""Alex"", ""age"": 11, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""39"", ""name"": ""Mike"", ""age"": 15, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32801 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""40"", ""name"": ""Eric"", ""age"": 11, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""41"", ""name"": ""John"", ""age"": 12, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60291 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""42"", ""name"": ""Ella"", ""age"": 17, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60291 }, ""scores"": [23, 11, 11, 66] } ",
                @" { ""id"": ""43"", ""name"": ""Lucy"", ""age"": 12, ""gender"": ""F"", ""team"": ""D"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""44"", ""name"": ""Mady"", ""age"": 14, ""gender"": ""F"", ""team"": ""A"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [23, 11, 11, 66] } ",
                @" { ""id"": ""45"", ""name"": ""Lori"", ""age"": 17, ""gender"": ""F"", ""team"": ""D"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""46"", ""name"": ""Gary"", ""age"": 17, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""47"", ""name"": ""Eric"", ""age"": 18, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32801 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""48"", ""name"": ""Mary"", ""age"": 15, ""gender"": ""F"", ""team"": ""C"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [23, 11, 11, 66] } ",
                @" { ""id"": ""49"", ""name"": ""Zara"", ""age"": 17, ""gender"": ""F"", ""team"": ""A"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""50"", ""name"": ""Carl"", ""age"": 17, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98101 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""51"", ""name"": ""Lori"", ""age"": 11, ""gender"": ""F"", ""team"": ""D"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""52"", ""name"": ""Adam"", ""age"": 13, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32801 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""53"", ""name"": ""Bill"", ""age"": 16, ""gender"": ""M"", ""team"": ""D"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""54"", ""name"": ""Zara"", ""age"": 12, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""55"", ""name"": ""Lisa"", ""age"": 16, ""gender"": ""F"", ""team"": ""A"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98101 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""56"", ""name"": ""Ryan"", ""age"": 12, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60292 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""57"", ""name"": ""Abby"", ""age"": 12, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""58"", ""name"": ""John"", ""age"": 16, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32801 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""59"", ""name"": ""Mary"", ""age"": 15, ""gender"": ""F"", ""team"": ""A"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98101 }, ""scores"": [52, 13, 94, 31] } ",
                @" { ""id"": ""60"", ""name"": ""John"", ""age"": 16, ""gender"": ""M"", ""team"": ""D"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""61"", ""name"": ""Mary"", ""age"": 17, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""62"", ""name"": ""Lucy"", ""age"": 12, ""gender"": ""F"", ""team"": ""C"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""63"", ""name"": ""Rose"", ""age"": 14, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""64"", ""name"": ""Gary"", ""age"": 14, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [88, 47, 90, 76] } ",
            };


            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.Partitioned,
                documents,
                this.TestGroupByQueryHelper);
        }

        private async Task TestGroupByQueryHelper(
            DocumentClient documentClient,
            DocumentCollection documentCollection,
            IEnumerable<Document> documents)
        {
            IEnumerable<JToken> documentsAsJTokens = documents.Select(document => JToken.FromObject(document));
            List<Tuple<string, IEnumerable<JToken>>> queryAndExpectedResultsList = new List<Tuple<string, IEnumerable<JToken>>>()
            {
                // ------------------------------------------
                // Simple property reference
                // ------------------------------------------

                new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.age FROM c GROUP BY c.age",
                    documentsAsJTokens
                        .GroupBy(document => document["age"], CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(new JProperty("age", grouping.Key)))),

                 new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.name FROM c GROUP BY c.name",
                    documentsAsJTokens
                        .GroupBy(document => document["name"], CrossPartitionQueryTests.JsonTokenEqualityComparer.Value).
                        Select(grouping => new JObject(new JProperty("name", grouping.Key)))),

                 new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.team FROM c GROUP BY c.team",
                    documentsAsJTokens
                        .GroupBy(document => document["team"], CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(new JProperty("team", grouping.Key)))),

                 new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.gender FROM c GROUP BY c.gender",
                    documentsAsJTokens
                        .GroupBy(document => document["gender"], CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(new JProperty("gender", grouping.Key)))),

                 new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.id FROM c GROUP BY c.id",
                    documentsAsJTokens
                        .GroupBy(document => document["id"], CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(new JProperty("id", grouping.Key)))),

                  new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.age, c.name FROM c GROUP BY c.age, c.name",
                    documentsAsJTokens
                        .GroupBy(document => new JObject(
                            new JProperty("age", document["age"]),
                            new JProperty("name", document["name"])),
                            CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(
                            new JProperty("age", grouping.Key["age"]),
                            new JProperty("name", grouping.Key["name"])))),

                 // ------------------------------------------
                 // With Aggregates
                 // ------------------------------------------

                  new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.age, COUNT(1) as count FROM c GROUP BY c.age",
                    documentsAsJTokens
                        .GroupBy(document => document["age"], CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(
                            new JProperty("age", grouping.Key),
                            new JProperty("count", grouping.Count())))),

                  new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.name, MIN(c.age) AS min_age FROM c GROUP BY c.name",
                    documentsAsJTokens
                        .GroupBy(document => document["name"], CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(
                            new JProperty("name", grouping.Key),
                            new JProperty("min_age", grouping.Select(document => document["age"]).Min(jToken => jToken.Value<double>()))))),

                 new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.name, MAX(c.age) AS max_age FROM c GROUP BY c.name",
                    documentsAsJTokens
                        .GroupBy(document => document["name"], CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(
                            new JProperty("name", grouping.Key),
                            new JProperty("max_age", grouping.Select(document => document["age"]).Max(jToken => jToken.Value<double>()))))),

                 new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.name, SUM(c.age) AS sum_age FROM c GROUP BY c.name",
                    documentsAsJTokens
                        .GroupBy(document => document["name"], CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(
                            new JProperty("name", grouping.Key),
                            new JProperty("sum_age", grouping.Select(document => document["age"]).Sum(jToken => jToken.Value<double>()))))),

                 new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.name, AVG(c.age) AS avg_age FROM c GROUP BY c.name",
                    documentsAsJTokens
                        .GroupBy(document => document["name"], CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(
                            new JProperty("name", grouping.Key),
                            new JProperty("avg_age", grouping.Select(document => document["age"]).Average(jToken => jToken.Value<double>()))))),

                  new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.name, Count(1) AS count, Min(c.age) AS min_age, Max(c.age) AS max_age FROM c GROUP BY c.name",
                    documentsAsJTokens
                        .GroupBy(document => document["name"], CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(
                            new JProperty("name", grouping.Key),
                            new JProperty("count", grouping.Count()),
                            new JProperty("min_age", grouping.Select(document => document["age"]).Min(jToken => jToken.Value<double>())),
                            new JProperty("max_age", grouping.Select(document => document["age"]).Max(jToken => jToken.Value<double>()))))),

                // ------------------------------------------
                // SELECT VALUE
                // ------------------------------------------

                new Tuple<string, IEnumerable<JToken>>(
                        "SELECT VALUE c.age FROM c GROUP BY c.age",
                        documentsAsJTokens
                            .GroupBy(document => document["age"], CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                            .Select(grouping => grouping.Key)),

                // ------------------------------------------
                // Corner Cases
                // ------------------------------------------

                new Tuple<string, IEnumerable<JToken>>(
                    "SELECT AVG(\"asdf\") as avg_asdf FROM c GROUP BY c.age",
                        documentsAsJTokens
                            .GroupBy(document => document["age"], CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                            .Select(grouping => new JObject())),

                new Tuple<string, IEnumerable<JToken>>(
                    @"SELECT 
                        c.age, 
                        AVG(c.doesNotExist) as undefined_avg,
                        MIN(c.doesNotExist) as undefined_min,
                        MAX(c.doesNotExist) as undefined_max,
                        COUNT(c.doesNotExist) as undefined_count,
                        SUM(c.doesNotExist) as undefined_sum
                    FROM c 
                    GROUP BY c.age",
                        documentsAsJTokens
                            .GroupBy(document => document["age"], CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                            .Select(grouping => new JObject(
                                new JProperty("age", grouping.Key),
                                // sum and count default the counter at 0
                                new JProperty("undefined_sum", 0),
                                new JProperty("undefined_count", 0)))),

                new Tuple<string, IEnumerable<JToken>>(
                    @"SELECT 
                        c.age, 
                        c.doesNotExist
                    FROM c 
                    GROUP BY c.age, c.doesNotExist",
                        documentsAsJTokens
                            .GroupBy(document => new JObject(
                                new JProperty("age", document["age"]),
                                new JProperty("doesNotExist", document["doesNotExist"])),
                                CrossPartitionQueryTests.JsonTokenEqualityComparer.Value)
                            .Select(grouping => new JObject(
                                new JProperty("age", grouping.Key["age"])))),
            };

            // Test query correctness.
            foreach ((string query, IEnumerable<JToken> expectedResults) in queryAndExpectedResultsList)
            {
                foreach (int maxItemCount in new int[] { 1, 5, 10 })
                {
                    List<JToken> actual = await QueryWithoutContinuationTokens<JToken>(
                        documentClient,
                        documentCollection,
                        query,
                        new FeedOptions()
                        {
                            //EnableCrossPartitionGroupBy = true,
                            EnableCrossPartitionQuery = true,
                            MaxItemCount = maxItemCount,
                            MaxBufferedItemCount = 100,
                            MaxDegreeOfParallelism = 100,
                            EnableGroupBy = true
                        });
                    HashSet<JToken> actualSet = new HashSet<JToken>(actual, CrossPartitionQueryTests.JsonTokenEqualityComparer.Value);

                    List<JToken> expected = expectedResults.ToList();
                    HashSet<JToken> expectedSet = new HashSet<JToken>(expected, CrossPartitionQueryTests.JsonTokenEqualityComparer.Value);

                    Assert.IsTrue(
                       actualSet.SetEquals(expectedSet),
                       $"Results did not match for query: {query} with maxItemCount: {maxItemCount}" +
                       $"Actual {JsonConvert.SerializeObject(actual)}" +
                       $"Expected: {JsonConvert.SerializeObject(expected)}");
                }
            }

            // Test that continuation token is blocked
            {
                try
                {
                    List<JToken> actual = await QueryWithContinuationTokens<JToken>(
                        documentClient,
                        documentCollection,
                        "SELECT c.age FROM c GROUP BY c.age",
                        new FeedOptions()
                        {
                            //EnableCrossPartitionGroupBy = true,
                            EnableCrossPartitionQuery = true
                        });
                    Assert.Fail("Expected an error when trying to drain a GROUP BY query with continuation tokens.");
                }
                catch (Exception e) when (e.GetBaseException().Message.Contains(GroupByDocumentQueryExecutionComponent.ContinuationTokenNotSupportedWithGroupBy))
                {
                }
            }
        }

        /// <summary>
        /// Tests DocumentFeedResponse.ResponseLengthInBytes is populated with the correct value for queries on Direct connection.
        /// The expected response length is determined by capturing DocumentServiceResponse events and aggreagte their lengths.
        /// Queries covered are standard/Top/Aggregate/Distinct and use MaxItemCount to force smaller page sizes, Max DOP and MaxBufferedItems to
        /// validate producer query threads are handled properly. Note: TOP has known non-deterministic behavior for non-zero Max DOP, so the setting
        /// is set to zero to avoid these cases.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Owner("olivert")]
        public async Task TestResponseLengthOverMultiplePartitions()
        {
            EventHandler<ReceivedResponseEventArgs> responseHandler = DocumentResponseLengthHandler;

            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            string partitionKey = "field_0";

            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> documents = util.GetDocuments(numberOfDocuments);

            await DocumentCrossPartitionQueryTests.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.Partitioned | CollectionTypes.NonPartitioned,
                documents,
                this.ExceuteResponseLengthQueriesAndValidation,
                (connectionMode) =>
                {
                    return TestCommon.CreateClient(
                        useGateway: connectionMode == ConnectionMode.Gateway ? true : false,
                        recievedResponseEventHandler: responseHandler);
                },
                partitionKey: "/" + partitionKey,
                testArgs: partitionKey);
        }

        private static void DocumentResponseLengthHandler(object sender, ReceivedResponseEventArgs e)
        {
            if (!e.IsHttpResponse())
            {
                List<object> headerKeyValues = new List<object>();
                foreach (string key in e.DocumentServiceRequest.Headers)
                {
                    headerKeyValues.Add(new { Key = key, Values = e.DocumentServiceRequest.Headers.GetValues(key)?.ToList() });
                }

                DocumentCrossPartitionQueryTests.responseLengthBytes.Value.IncrementBy(e.DocumentServiceResponse.ResponseBody.Length);
                Console.WriteLine("{0} : DocumentServiceResponse: Query {1}, OuterActivityId: {2}, Length: {3}, Request op type: {4}, resource type: {5}, continuation: {6}, headers: {7}",
                    DateTime.UtcNow,
                    e.DocumentServiceRequest.QueryString,
                    DocumentCrossPartitionQueryTests.outerDocumentFeedResponseActivityId.Value,
                    e.DocumentServiceResponse.ResponseBody.Length,
                    e.DocumentServiceRequest.OperationType,
                    e.DocumentServiceRequest.ResourceType,
                    e.DocumentServiceRequest.Continuation,
                    JsonConvert.SerializeObject(headerKeyValues));
            }
        }

        private async Task ExceuteResponseLengthQueriesAndValidation(DocumentClient queryClient, DocumentCollection coll, IEnumerable<Document> documents, dynamic testArgs)
        {
            string partitionKey = testArgs;

            await this.AssertResponseLength(queryClient, coll, "SELECT * FROM r");
            await this.AssertResponseLength(queryClient, coll, "SELECT VALUE COUNT(1) FROM c");
            await this.AssertResponseLength(queryClient, coll, "SELECT * FROM r", maxItemCount: 10);
            await this.AssertResponseLength(queryClient, coll, "SELECT * FROM r", maxItemCount: 10, maxBufferedCount: 100);
            await this.AssertResponseLength(queryClient, coll, "SELECT VALUE MAX(c._ts) FROM c", maxItemCount: 10);
            await this.AssertResponseLength(queryClient, coll, $"SELECT DISTINCT VALUE r.{partitionKey} FROM r", maxItemCount: 10);

            await this.AssertResponseLength(queryClient, coll, "SELECT TOP 5 * FROM c ORDER BY c._ts", isTopQuery: true);
            await this.AssertResponseLength(queryClient, coll, "SELECT TOP 32 * FROM r", isTopQuery: true, maxItemCount: 10);
        }

        private async Task AssertResponseLength(DocumentClient client, DocumentCollection coll, string query, bool isTopQuery = false, int maxItemCount = 1, int maxBufferedCount = -1, int maxReadItemCount = -1)
        {
            long expectedResponseLength = 0;
            long actualResponseLength = 0;

            // NOTE: For queries with 'TOP' clause and non-zero Max DOP, it is possible for additional backend responses to return
            // after the target item limit has been reached and the final DocumentFeedResponse is being percolated to the caller. 
            // As a result, the stats from these responses will not be included in the aggregated results on the DocumentFeedResponses.
            // To avoid this non-determism in the test cases, we force Max DOP to zero if the query is a 'top' query.
            FeedOptions feedOptions = new FeedOptions
            {
                EnableCrossPartitionQuery = true,
                MaxItemCount = maxItemCount,
                MaxDegreeOfParallelism = isTopQuery ? 0 : 50,
                MaxBufferedItemCount = isTopQuery ? 0 : maxBufferedCount,
            };

            DocumentCrossPartitionQueryTests.responseLengthBytes.Value = new LocalCounter();
            DocumentCrossPartitionQueryTests.outerDocumentFeedResponseActivityId.Value = Guid.NewGuid();

            Console.WriteLine("{0} : Running query: {1}, maxitemcount: {2}, maxBufferedCount: {3}, max read count: {4}, OuterActivityId: {5}",
                DateTime.UtcNow,
                query,
                maxItemCount,
                maxBufferedCount,
                maxReadItemCount,
                DocumentCrossPartitionQueryTests.outerDocumentFeedResponseActivityId.Value);

            int totalReadCount = 0;

            using (IDocumentQuery<dynamic> docQuery = client.CreateDocumentQuery(coll, query, feedOptions)
                .AsDocumentQuery())
            {
                while (docQuery.HasMoreResults && (maxReadItemCount < 0 || maxReadItemCount > totalReadCount))
                {
                    DocumentFeedResponse<dynamic> response = await docQuery.ExecuteNextAsync();

                    Console.WriteLine("{0} : DocumentFeedResponse: Query: {1}, ActivityId: {2}, OuterActivityId: {3}, RequestCharge: {4}, ResponseLength: {5}, ItemCount: {6}",
                        DateTime.UtcNow,
                        query,
                        response.ActivityId,
                        DocumentCrossPartitionQueryTests.outerDocumentFeedResponseActivityId.Value,
                        response.RequestCharge,
                        response.ResponseLengthBytes,
                        response.Count);

                    actualResponseLength += response.ResponseLengthBytes;
                    totalReadCount += response.Count;
                }
            }

            expectedResponseLength = DocumentCrossPartitionQueryTests.responseLengthBytes.Value.Value;
            Console.WriteLine("Completed query: {0}, response length: {1}, total item count: {2}, document service response length: {3}, OuterActivityId: {4}",
                query,
                actualResponseLength,
                totalReadCount,
                expectedResponseLength,
                DocumentCrossPartitionQueryTests.outerDocumentFeedResponseActivityId.Value);

            Assert.AreNotEqual(0, expectedResponseLength);

            // Top queries don't necessarily return a response length that matches the DocumentServiceResponses.
            // To avoid the discrepancies, skip exact response length validation for these queries.
            // We still run the query to ensure there are no exceptions.
            if (!isTopQuery)
            {
                Assert.AreEqual(expectedResponseLength, actualResponseLength, "Aggregate DocumentFeedResponse length did not match document service response.");
            }

            DocumentCrossPartitionQueryTests.responseLengthBytes.Value = null;
        }

        private class LocalCounter
        {
            private long value;

            public long Value => this.value;

            public long IncrementBy(long incrementBy)
            {
                return Interlocked.Add(ref this.value, incrementBy);
            }
        }
    }
}