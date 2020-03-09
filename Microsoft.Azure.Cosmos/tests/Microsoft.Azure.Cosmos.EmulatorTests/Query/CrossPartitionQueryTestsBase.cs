//-----------------------------------------------------------------------
// <copyright file="CrossPartitionQueryTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Tests for CrossPartitionQueryTests.
    /// </summary>
    [TestClass]
    [TestCategory("Query")]
    public abstract class CrossPartitionQueryTestsBase
    {
        protected static readonly string[] NoDocuments = new string[] { };
        protected CosmosClient GatewayClient = TestCommon.CreateCosmosClient(true);
        protected CosmosClient Client = TestCommon.CreateCosmosClient(false);
        protected Cosmos.Database database;

        [FlagsAttribute]
        internal enum ConnectionModes
        {
            None = 0,
            Direct = 0x1,
            Gateway = 0x2,
        }

        [FlagsAttribute]
        internal enum CollectionTypes
        {
            None = 0,
            NonPartitioned = 0x1,
            SinglePartition = 0x2,
            MultiPartition = 0x4,
        }

        [ClassInitialize]
        [ClassCleanup]
        public static void ClassSetup(TestContext testContext = null)
        {
            CosmosClient client = TestCommon.CreateCosmosClient(false);
            CrossPartitionQueryTestsBase.CleanUp(client).Wait();
        }

        [TestInitialize]
        public async Task Initialize()
        {
            this.database = await this.Client.CreateDatabaseAsync(Guid.NewGuid().ToString() + "db");
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await this.database.DeleteStreamAsync();
        }

        private static string GetApiVersion()
        {
            return HttpConstants.Versions.CurrentVersion;
        }

        private static void SetApiVersion(string apiVersion)
        {
            HttpConstants.Versions.CurrentVersion = apiVersion;
            HttpConstants.Versions.CurrentVersionUTF8 = Encoding.UTF8.GetBytes(apiVersion);
        }

        private async Task<IReadOnlyList<PartitionKeyRange>> GetPartitionKeyRanges(ContainerProperties container)
        {
            Range<string> fullRange = new Range<string>(
                PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                true,
                false);
            IRoutingMapProvider routingMapProvider = await this.Client.DocumentClient.GetPartitionKeyRangeCacheAsync();
            Assert.IsNotNull(routingMapProvider);

            IReadOnlyList<PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(container.ResourceId, fullRange);
            return ranges;
        }

        private async Task<Container> CreateMultiPartitionContainer(
            string partitionKey = "/id",
            Microsoft.Azure.Cosmos.IndexingPolicy indexingPolicy = null)
        {
            ContainerResponse containerResponse = await this.CreatePartitionedContainer(
                throughput: 25000,
                partitionKey: partitionKey,
                indexingPolicy: indexingPolicy);

            IReadOnlyList<PartitionKeyRange> ranges = await this.GetPartitionKeyRanges(containerResponse);
            Assert.IsTrue(
                ranges.Count() > 1,
                $"{nameof(CreateMultiPartitionContainer)} failed to create a container with more than 1 physical partition.");

            return containerResponse;
        }

        private async Task<Container> CreateSinglePartitionContainer(
            string partitionKey = "/id",
            Microsoft.Azure.Cosmos.IndexingPolicy indexingPolicy = null)
        {
            ContainerResponse containerResponse = await this.CreatePartitionedContainer(
                throughput: 4000,
                partitionKey: partitionKey,
                indexingPolicy: indexingPolicy);

            Assert.IsNotNull(containerResponse);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.IsNotNull(containerResponse.Resource);
            Assert.IsNotNull(containerResponse.Resource.ResourceId);

            IReadOnlyList<PartitionKeyRange> ranges = await this.GetPartitionKeyRanges(containerResponse);
            Assert.AreEqual(1, ranges.Count());

            return containerResponse;
        }

        private async Task<Container> CreateNonPartitionedContainer(
            Microsoft.Azure.Cosmos.IndexingPolicy indexingPolicy = null)
        {
            string containerName = Guid.NewGuid().ToString() + "container";
            await NonPartitionedContainerHelper.CreateNonPartitionedContainer(
                this.database,
                containerName,
                indexingPolicy == null ? null : JsonConvert.SerializeObject(indexingPolicy));

            return this.database.GetContainer(containerName);
        }

        private async Task<ContainerResponse> CreatePartitionedContainer(
            int throughput,
            string partitionKey = "/id",
            Microsoft.Azure.Cosmos.IndexingPolicy indexingPolicy = null)
        {
            // Assert that database exists (race deletes are possible when used concurrently)
            ResponseMessage responseMessage = await this.database.ReadStreamAsync();
            Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);

            ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                new ContainerProperties
                {
                    Id = Guid.NewGuid().ToString() + "container",
                    IndexingPolicy = indexingPolicy ?? new Cosmos.IndexingPolicy
                    {
                        IncludedPaths = new Collection<Cosmos.IncludedPath>
                        {
                            new Cosmos.IncludedPath
                            {
                                Path = "/*",
                                Indexes = new Collection<Cosmos.Index>
                                {
                                    Cosmos.Index.Range(Cosmos.DataType.Number),
                                    Cosmos.Index.Range(Cosmos.DataType.String),
                                }
                            }
                        }
                    },
                    PartitionKey = partitionKey == null ? null : new PartitionKeyDefinition
                    {
                        Paths = new Collection<string> { partitionKey },
                        Kind = PartitionKind.Hash
                    }
                },
                // This throughput needs to be about half the max with multi master
                // otherwise it will create about twice as many partitions.
                throughput);

            Assert.IsNotNull(containerResponse);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.IsNotNull(containerResponse.Resource);
            Assert.IsNotNull(containerResponse.Resource.ResourceId);

            return containerResponse;
        }

        private async Task<Tuple<Container, List<Document>>> CreateNonPartitionedContainerAndIngestDocuments(
            IEnumerable<string> documents,
            Cosmos.IndexingPolicy indexingPolicy = null)
        {
            return await this.CreateContainerAndIngestDocuments(
                CollectionTypes.NonPartitioned,
                documents,
                partitionKey: null,
                indexingPolicy: indexingPolicy);
        }

        private async Task<Tuple<Container, List<Document>>> CreateSinglePartitionContainerAndIngestDocuments(
            IEnumerable<string> documents,
            string partitionKey = "/id",
            Cosmos.IndexingPolicy indexingPolicy = null)
        {
            return await this.CreateContainerAndIngestDocuments(
                CollectionTypes.SinglePartition,
                documents,
                partitionKey,
                indexingPolicy);
        }

        private async Task<Tuple<Container, List<Document>>> CreateMultiPartitionContainerAndIngestDocuments(
            IEnumerable<string> documents,
            string partitionKey = "/id",
            Cosmos.IndexingPolicy indexingPolicy = null)
        {
            return await this.CreateContainerAndIngestDocuments(
                CollectionTypes.MultiPartition,
                documents,
                partitionKey,
                indexingPolicy);
        }

        private async Task<Tuple<Container, List<Document>>> CreateContainerAndIngestDocuments(
            CollectionTypes collectionType,
            IEnumerable<string> documents,
            string partitionKey = "/id",
            Cosmos.IndexingPolicy indexingPolicy = null)
        {
            Container container;
            switch (collectionType)
            {
                case CollectionTypes.NonPartitioned:
                    container = await this.CreateNonPartitionedContainer(indexingPolicy);
                    break;

                case CollectionTypes.SinglePartition:
                    container = await this.CreateSinglePartitionContainer(partitionKey, indexingPolicy);
                    break;

                case CollectionTypes.MultiPartition:
                    container = await this.CreateMultiPartitionContainer(partitionKey, indexingPolicy);
                    break;

                default:
                    throw new ArgumentException($"Unknown {nameof(CollectionTypes)} : {collectionType}");
            }

            List<Document> insertedDocuments = new List<Document>();
            foreach (string document in documents)
            {
                JObject documentObject = JsonConvert.DeserializeObject<JObject>(document);
                // Add an id
                if (documentObject["id"] == null)
                {
                    documentObject["id"] = Guid.NewGuid().ToString();
                }

                // Get partition key value.
                Cosmos.PartitionKey pkValue;
                if (partitionKey != null)
                {
                    string jObjectPartitionKey = partitionKey.Remove(0, 1);
                    JValue pkToken = (JValue)documentObject[jObjectPartitionKey];
                    if (pkToken == null)
                    {
                        pkValue = Cosmos.PartitionKey.None;
                    }
                    else
                    {
                        switch (pkToken.Type)
                        {
                            case JTokenType.Integer:
                            case JTokenType.Float:
                                pkValue = new Cosmos.PartitionKey(pkToken.Value<double>());
                                break;
                            case JTokenType.String:
                                pkValue = new Cosmos.PartitionKey(pkToken.Value<string>());
                                break;
                            case JTokenType.Boolean:
                                pkValue = new Cosmos.PartitionKey(pkToken.Value<bool>());
                                break;
                            case JTokenType.Null:
                                pkValue = Cosmos.PartitionKey.Null;
                                break;
                            default:
                                throw new ArgumentException("Unknown partition key type");
                        }
                    }
                }
                else
                {
                    pkValue = Cosmos.PartitionKey.None;
                }

                JObject createdDocument = await container.CreateItemAsync<JObject>(documentObject, pkValue);
                Document insertedDocument = Document.FromObject(createdDocument);
                insertedDocuments.Add(insertedDocument);
            }

            return new Tuple<Container, List<Document>>(container, insertedDocuments);
        }

        private static async Task CleanUp(CosmosClient client)
        {
            FeedIterator<DatabaseProperties> allDatabases = client.GetDatabaseQueryIterator<DatabaseProperties>();

            while (allDatabases.HasMoreResults)
            {
                foreach (DatabaseProperties db in await allDatabases.ReadNextAsync())
                {
                    await client.GetDatabase(db.Id).DeleteAsync();
                }
            }
        }

        protected async Task RunWithApiVersion(string apiVersion, Func<Task> function)
        {
            string originalApiVersion = GetApiVersion();
            CosmosClient originalCosmosClient = this.Client;
            CosmosClient originalGatewayClient = this.GatewayClient;
            Cosmos.Database originalDatabase = this.database;

            try
            {
                SetApiVersion(apiVersion);
                if (apiVersion != originalApiVersion)
                {
                    this.Client = TestCommon.CreateCosmosClient(false);
                    this.GatewayClient = TestCommon.CreateCosmosClient(true);
                    this.database = this.Client.GetDatabase(this.database.Id);
                }

                await function();
            }
            finally
            {
                this.Client = originalCosmosClient;
                this.GatewayClient = originalGatewayClient;
                this.database = originalDatabase;
                SetApiVersion(originalApiVersion);
            }
        }

        internal delegate Task Query(
            Container container,
            IEnumerable<Document> documents);

        internal delegate Task Query<T>(
            Container container,
            IEnumerable<Document> documents,
            T testArgs);

        internal delegate CosmosClient CosmosClientFactory(ConnectionMode connectionMode);

        internal Task CreateIngestQueryDelete(
            ConnectionModes connectionModes,
            CollectionTypes collectionTypes,
            IEnumerable<string> documents,
            Query query,
            string partitionKey = "/id",
            Cosmos.IndexingPolicy indexingPolicy = null,
            CosmosClientFactory cosmosClientFactory = null)
        {
            Task queryWrapper(Container container, IEnumerable<Document> inputDocuments, object throwaway)
            {
                return query(container, inputDocuments);
            }

            return this.CreateIngestQueryDelete<object>(
                connectionModes,
                collectionTypes,
                documents,
                queryWrapper,
                null,
                partitionKey,
                indexingPolicy,
                cosmosClientFactory);
        }

        internal Task CreateIngestQueryDelete<T>(
            ConnectionModes connectionModes,
            CollectionTypes collectionTypes,
            IEnumerable<string> documents,
            Query<T> query,
            T testArgs,
            string partitionKey = "/id",
            Cosmos.IndexingPolicy indexingPolicy = null,
            CosmosClientFactory cosmosClientFactory = null)
        {
            return this.CreateIngestQueryDelete(
                connectionModes,
                collectionTypes,
                documents,
                query,
                cosmosClientFactory ?? this.CreateDefaultCosmosClient,
                testArgs,
                partitionKey,
                indexingPolicy);
        }

        /// <summary>
        /// Task that wraps boiler plate code for query tests (container create -> ingest documents -> query documents -> delete collections).
        /// Note that this function will take the cross product connectionModes
        /// </summary>
        /// <param name="connectionModes">The connection modes to use.</param>
        /// <param name="documents">The documents to ingest</param>
        /// <param name="query">
        /// The callback for the queries.
        /// All the standard arguments will be passed in.
        /// Please make sure that this function is idempotent, since a container will be reused for each connection mode.
        /// </param>
        /// <param name="cosmosClientFactory">
        /// The callback for the create CosmosClient. This is invoked for the different ConnectionModes that the query is targeting.
        /// If CosmosClient instantiated by this does not apply the expected ConnectionMode, an assert is thrown.
        /// </param>
        /// <param name="partitionKey">The partition key for the partition container.</param>
        /// <param name="testArgs">The optional args that you want passed in to the query.</param>
        /// <returns>A task to await on.</returns>
        internal async Task CreateIngestQueryDelete<T>(
           ConnectionModes connectionModes,
           CollectionTypes collectionTypes,
           IEnumerable<string> documents,
           Query<T> query,
           CosmosClientFactory cosmosClientFactory,
           T testArgs,
           string partitionKey = "/id",
           Cosmos.IndexingPolicy indexingPolicy = null)
        {
            try
            {
                List<Tuple<Container, List<Document>>> collectionsAndDocuments = new List<Tuple<Container, List<Document>>>();
                foreach (CollectionTypes collectionType in Enum.GetValues(collectionTypes.GetType()).Cast<Enum>().Where(collectionTypes.HasFlag))
                {
                    if (collectionType == CollectionTypes.None)
                    {
                        continue;
                    }

                    Task<Tuple<Container, List<Document>>> createContainerTask;
                    switch (collectionType)
                    {
                        case CollectionTypes.NonPartitioned:
                            createContainerTask = this.CreateNonPartitionedContainerAndIngestDocuments(
                                documents,
                                indexingPolicy);
                            break;

                        case CollectionTypes.SinglePartition:
                            createContainerTask = this.CreateSinglePartitionContainerAndIngestDocuments(
                                documents,
                                partitionKey,
                                indexingPolicy);
                            break;

                        case CollectionTypes.MultiPartition:
                            createContainerTask = this.CreateMultiPartitionContainerAndIngestDocuments(
                                documents,
                                partitionKey,
                                indexingPolicy);
                            break;

                        default:
                            throw new ArgumentException($"Unknown {nameof(CollectionTypes)} : {collectionType}");
                    }

                    collectionsAndDocuments.Add(await createContainerTask);
                }

                List<CosmosClient> cosmosClients = new List<CosmosClient>();
                foreach (ConnectionModes connectionMode in Enum.GetValues(connectionModes.GetType()).Cast<Enum>().Where(connectionModes.HasFlag))
                {
                    if (connectionMode == ConnectionModes.None)
                    {
                        continue;
                    }

                    ConnectionMode targetConnectionMode = GetTargetConnectionMode(connectionMode);
                    CosmosClient cosmosClient = cosmosClientFactory(targetConnectionMode);

                    Assert.AreEqual(
                        targetConnectionMode,
                        cosmosClient.ClientOptions.ConnectionMode,
                        "Test setup: Invalid connection policy applied to CosmosClient");
                    cosmosClients.Add(cosmosClient);
                }

                List<Task> queryTasks = new List<Task>();
                foreach (CosmosClient cosmosClient in cosmosClients)
                {
                    foreach (Tuple<Container, List<Document>> containerAndDocuments in collectionsAndDocuments)
                    {
                        Container container = cosmosClient.GetContainer(((ContainerCore)(ContainerInlineCore)containerAndDocuments.Item1).Database.Id, containerAndDocuments.Item1.Id);
                        Task queryTask = Task.Run(() => query(container, containerAndDocuments.Item2, testArgs));
                        queryTasks.Add(queryTask);
                    }
                }

                await Task.WhenAll(queryTasks);

                List<Task<ContainerResponse>> deleteContainerTasks = new List<Task<ContainerResponse>>();
                foreach (Container container in collectionsAndDocuments.Select(tuple => tuple.Item1))
                {
                    deleteContainerTasks.Add(container.DeleteContainerAsync());
                }

                await Task.WhenAll(deleteContainerTasks);
            }
            catch (Exception ex) when (ex.GetType() != typeof(AssertFailedException))
            {
                while (ex.InnerException != null) ex = ex.InnerException;

                ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        static ConnectionMode GetTargetConnectionMode(ConnectionModes connectionMode)
        {
            ConnectionMode targetConnectionMode;
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

        protected CosmosClient CreateDefaultCosmosClient(ConnectionMode connectionMode)
        {
            switch (connectionMode)
            {
                case ConnectionMode.Gateway:
                    return this.GatewayClient;
                case ConnectionMode.Direct:
                    return this.Client;
                default:
                    throw new ArgumentException($"Unexpected connection mode: {connectionMode}");
            }
        }

        protected CosmosClient CreateNewCosmosClient(ConnectionMode connectionMode)
        {
            switch (connectionMode)
            {
                case ConnectionMode.Gateway:
                    return TestCommon.CreateCosmosClient(true);
                case ConnectionMode.Direct:
                    return TestCommon.CreateCosmosClient(false);
                default:
                    throw new ArgumentException($"Unexpected connection mode: {connectionMode}");
            }
        }

        protected static async Task<List<T>> QueryWithCosmosElementContinuationTokenAsync<T>(
            Container container,
            string query,
            QueryRequestOptions queryRequestOptions = null)
        {
            if (queryRequestOptions == null)
            {
                queryRequestOptions = new QueryRequestOptions();
            }

            List<T> resultsFromCosmosElementContinuationToken = new List<T>();
            CosmosElement continuationToken = null;
            do
            {
                QueryRequestOptions computeRequestOptions = queryRequestOptions.Clone();
                computeRequestOptions.ExecutionEnvironment = Cosmos.Query.Core.ExecutionContext.ExecutionEnvironment.Compute;
                computeRequestOptions.CosmosElementContinuationToken = continuationToken;

                FeedIteratorInternal<T> itemQuery = (FeedIteratorInternal<T>)container.GetItemQueryIterator<T>(
                   queryText: query,
                   requestOptions: computeRequestOptions);
                try
                {
                    FeedResponse<T> cosmosQueryResponse = await itemQuery.ReadNextAsync();
                    if (queryRequestOptions.MaxItemCount.HasValue)
                    {
                        Assert.IsTrue(
                            cosmosQueryResponse.Count <= queryRequestOptions.MaxItemCount.Value,
                            "Max Item Count is not being honored");
                    }

                    resultsFromCosmosElementContinuationToken.AddRange(cosmosQueryResponse);
                    continuationToken = itemQuery.GetCosmosElementContinuationToken();
                }
                catch (CosmosException cosmosException) when (cosmosException.StatusCode == (HttpStatusCode)429)
                {
                    itemQuery = (FeedIteratorInternal<T>)container.GetItemQueryIterator<T>(
                            queryText: query,
                            requestOptions: queryRequestOptions);
                }
            } while (continuationToken != null);

            return resultsFromCosmosElementContinuationToken;
        }

        protected static async Task<List<T>> QueryWithContinuationTokensAsync<T>(
            Container container,
            string query,
            QueryRequestOptions queryRequestOptions = null)
        {
            if (queryRequestOptions == null)
            {
                queryRequestOptions = new QueryRequestOptions();
            }

            List<T> resultsFromContinuationToken = new List<T>();
            string continuationToken = null;
            do
            {
                FeedIterator<T> itemQuery = container.GetItemQueryIterator<T>(
                   queryText: query,
                   requestOptions: queryRequestOptions,
                   continuationToken: continuationToken);

                while (true)
                {
                    try
                    {
                        FeedResponse<T> cosmosQueryResponse = await itemQuery.ReadNextAsync();
                        if (queryRequestOptions.MaxItemCount.HasValue)
                        {
                            Assert.IsTrue(
                                cosmosQueryResponse.Count <= queryRequestOptions.MaxItemCount.Value,
                                "Max Item Count is not being honored");
                        }

                        resultsFromContinuationToken.AddRange(cosmosQueryResponse);
                        continuationToken = cosmosQueryResponse.ContinuationToken;
                        break;
                    }
                    catch (CosmosException cosmosException) when (cosmosException.StatusCode == (HttpStatusCode)429)
                    {
                        itemQuery = container.GetItemQueryIterator<T>(
                            queryText: query,
                            requestOptions: queryRequestOptions,
                            continuationToken: continuationToken);
                    }
                }
            } while (continuationToken != null);

            return resultsFromContinuationToken;
        }

        protected static async Task<List<T>> QueryWithoutContinuationTokensAsync<T>(
            Container container,
            string query,
            QueryRequestOptions queryRequestOptions = null)
        {
            if (queryRequestOptions == null)
            {
                queryRequestOptions = new QueryRequestOptions();
            }

            List<T> results = new List<T>();
            FeedIterator<T> itemQuery = container.GetItemQueryIterator<T>(
                queryText: query,
                requestOptions: queryRequestOptions);

            string continuationTokenForRetries = null;
            while (itemQuery.HasMoreResults)
            {
                try
                {
                    FeedResponse<T> page = await itemQuery.ReadNextAsync();
                    results.AddRange(page);

                    if (queryRequestOptions.MaxItemCount.HasValue)
                    {
                        Assert.IsTrue(
                            page.Count <= queryRequestOptions.MaxItemCount.Value,
                            "Max Item Count is not being honored");
                    }

                    try
                    {
                        continuationTokenForRetries = page.ContinuationToken;
                    }
                    catch (Exception)
                    {
                        // Grabbing a continuation token is not supported on all queries.
                    }
                }
                catch (CosmosException cosmosException) when (cosmosException.StatusCode == (HttpStatusCode)429)
                {
                    itemQuery = container.GetItemQueryIterator<T>(
                        queryText: query,
                        requestOptions: queryRequestOptions,
                        continuationToken: continuationTokenForRetries);

                    if (continuationTokenForRetries == null)
                    {
                        // The query failed and we don't have a save point, so just restart the whole thing.
                        results = new List<T>();
                    }
                }
            }

            return results;
        }

        protected static async Task NoOp()
        {
            await Task.Delay(0);
        }

        protected static async Task<List<T>> RunQueryAsync<T>(
            Container container,
            string query,
            QueryRequestOptions queryRequestOptions = null)
        {
            return await RunQueryCombinationsAsync<T>(
                container,
                query,
                queryRequestOptions,
                QueryDrainingMode.ContinuationToken | QueryDrainingMode.HoldState | QueryDrainingMode.CosmosElementContinuationToken);
        }

        [Flags]
        public enum QueryDrainingMode
        {
            None = 0,
            HoldState = 1,
            ContinuationToken = 2,
            CosmosElementContinuationToken = 4,
        }

        protected static async Task<List<T>> RunQueryCombinationsAsync<T>(
            Container container,
            string query,
            QueryRequestOptions queryRequestOptions,
            QueryDrainingMode queryDrainingMode)
        {
            if (queryDrainingMode == QueryDrainingMode.None)
            {
                throw new ArgumentOutOfRangeException(nameof(queryDrainingMode));
            }

            Dictionary<QueryDrainingMode, List<T>> queryExecutionResults = new Dictionary<QueryDrainingMode, List<T>>();

            if (queryDrainingMode.HasFlag(QueryDrainingMode.HoldState))
            {
                List<T> queryResultsWithoutContinuationToken = await QueryWithoutContinuationTokensAsync<T>(
                    container,
                    query,
                    queryRequestOptions);

                queryExecutionResults[QueryDrainingMode.HoldState] = queryResultsWithoutContinuationToken;
            }

            if (queryDrainingMode.HasFlag(QueryDrainingMode.ContinuationToken))
            {
                List<T> queryResultsWithContinuationTokens = await QueryWithContinuationTokensAsync<T>(
                    container,
                    query,
                    queryRequestOptions);

                queryExecutionResults[QueryDrainingMode.ContinuationToken] = queryResultsWithContinuationTokens;
            }

            if (queryDrainingMode.HasFlag(QueryDrainingMode.CosmosElementContinuationToken))
            {
                List<T> queryResultsWithCosmosElementContinuationToken = await QueryWithCosmosElementContinuationTokenAsync<T>(
                    container,
                    query,
                    queryRequestOptions);

                queryExecutionResults[QueryDrainingMode.CosmosElementContinuationToken] = queryResultsWithCosmosElementContinuationToken;
            }

            foreach (QueryDrainingMode queryDrainingMode1 in queryExecutionResults.Keys)
            {
                foreach (QueryDrainingMode queryDrainingMode2 in queryExecutionResults.Keys)
                {
                    if (queryDrainingMode1 != queryDrainingMode2)
                    {
                        List<JToken> queryDrainingModeAsJTokens1 = queryExecutionResults[queryDrainingMode1]
                            .Select(x => x == null ? JValue.CreateNull() : JToken.FromObject(x)).ToList();

                        List<JToken> queryDrainingModeAsJTokens2 = queryExecutionResults[queryDrainingMode2]
                            .Select(x => x == null ? JValue.CreateNull() : JToken.FromObject(x)).ToList();

                        Assert.IsTrue(
                            queryDrainingModeAsJTokens1.SequenceEqual(queryDrainingModeAsJTokens2, JsonTokenEqualityComparer.Value),
                            $"{query} returned different results.\n" +
                            $"{queryDrainingMode1}: {JsonConvert.SerializeObject(queryDrainingModeAsJTokens1)}\n" +
                            $"{queryDrainingMode2}: {JsonConvert.SerializeObject(queryDrainingModeAsJTokens2)}\n");
                    }
                }
            }

            return queryExecutionResults.Values.First();
        }

        protected async Task<List<T>> RunSinglePartitionQuery<T>(
            Container container,
            string query,
            QueryRequestOptions requestOptions = null)
        {
            FeedIterator<T> resultSetIterator = container.GetItemQueryIterator<T>(
                query,
                requestOptions: requestOptions);

            List<T> items = new List<T>();
            while (resultSetIterator.HasMoreResults)
            {
                items.AddRange(await resultSetIterator.ReadNextAsync());
            }

            return items;
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