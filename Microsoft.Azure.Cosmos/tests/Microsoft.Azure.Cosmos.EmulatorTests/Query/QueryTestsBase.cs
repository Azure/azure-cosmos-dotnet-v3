//-----------------------------------------------------------------------
// <copyright file="CrossPartitionQueryTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
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
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Tests for CrossPartitionQueryTests.
    /// </summary>
    [Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestClass]
    [TestCategory("Query")]
    public abstract class QueryTestsBase
    {
        internal static readonly string[] NoDocuments = new string[] { };
        internal CosmosClient GatewayClient = TestCommon.CreateCosmosClient(true);
        internal CosmosClient Client = TestCommon.CreateCosmosClient(false);
        internal Cosmos.Database database;

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
            QueryTestsBase.CleanUp(client).Wait();
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

            IReadOnlyList<PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(
                container.ResourceId, 
                fullRange,
                NoOpTrace.Singleton);
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

        private async Task<Container> CreateNonPartitionedContainerAsync(
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

        private Task<(Container, IReadOnlyList<CosmosObject>)> CreateNonPartitionedContainerAndIngestDocumentsAsync(
            IEnumerable<string> documents,
            Cosmos.IndexingPolicy indexingPolicy = null)
        {
            return this.CreateContainerAndIngestDocumentsAsync(
                CollectionTypes.NonPartitioned,
                documents,
                partitionKey: null,
                indexingPolicy: indexingPolicy);
        }

        private Task<(Container, IReadOnlyList<CosmosObject>)> CreateSinglePartitionContainerAndIngestDocumentsAsync(
            IEnumerable<string> documents,
            string partitionKey = "/id",
            Cosmos.IndexingPolicy indexingPolicy = null)
        {
            return this.CreateContainerAndIngestDocumentsAsync(
                CollectionTypes.SinglePartition,
                documents,
                partitionKey,
                indexingPolicy);
        }

        private Task<(Container, IReadOnlyList<CosmosObject>)> CreateMultiPartitionContainerAndIngestDocumentsAsync(
            IEnumerable<string> documents,
            string partitionKey = "/id",
            Cosmos.IndexingPolicy indexingPolicy = null)
        {
            return this.CreateContainerAndIngestDocumentsAsync(
                CollectionTypes.MultiPartition,
                documents,
                partitionKey,
                indexingPolicy);
        }

        private async Task<(Container, IReadOnlyList<CosmosObject>)> CreateContainerAndIngestDocumentsAsync(
            CollectionTypes collectionType,
            IEnumerable<string> documents,
            string partitionKey = "/id",
            Cosmos.IndexingPolicy indexingPolicy = null)
        {
            Container container = collectionType switch
            {
                CollectionTypes.NonPartitioned => await this.CreateNonPartitionedContainerAsync(indexingPolicy),
                CollectionTypes.SinglePartition => await this.CreateSinglePartitionContainer(partitionKey, indexingPolicy),
                CollectionTypes.MultiPartition => await this.CreateMultiPartitionContainer(partitionKey, indexingPolicy),
                _ => throw new ArgumentException($"Unknown {nameof(CollectionTypes)} : {collectionType}"),
            };
            List<CosmosObject> insertedDocuments = new List<CosmosObject>();
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

                JObject createdDocument = await container.CreateItemAsync(documentObject, pkValue);
                CosmosObject insertedDocument = CosmosObject.Parse<CosmosObject>(createdDocument.ToString());
                insertedDocuments.Add(insertedDocument);
            }

            return (container, insertedDocuments);
        }

        private static async Task CleanUp(CosmosClient client)
        {
            using (FeedIterator<DatabaseProperties> allDatabases = client.GetDatabaseQueryIterator<DatabaseProperties>())
            {
                while (allDatabases.HasMoreResults)
                {
                    foreach (DatabaseProperties db in await allDatabases.ReadNextAsync())
                    {
                        await client.GetDatabase(db.Id).DeleteAsync();
                    }
                }
            }
        }

        internal async Task RunWithApiVersion(string apiVersion, Func<Task> function)
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
            IReadOnlyList<CosmosObject> documents);

        internal delegate Task Query<T>(
            Container container,
            IReadOnlyList<CosmosObject> documents,
            T testArgs);

        internal delegate CosmosClient CosmosClientFactory(ConnectionMode connectionMode);

        internal Task CreateIngestQueryDeleteAsync(
            ConnectionModes connectionModes,
            CollectionTypes collectionTypes,
            IEnumerable<string> documents,
            Query query,
            string partitionKey = "/id",
            Cosmos.IndexingPolicy indexingPolicy = null,
            CosmosClientFactory cosmosClientFactory = null)
        {
            Task queryWrapper(Container container, IReadOnlyList<CosmosObject> inputDocuments, object throwaway)
            {
                return query(container, inputDocuments);
            }

            return this.CreateIngestQueryDeleteAsync<object>(
                connectionModes,
                collectionTypes,
                documents,
                queryWrapper,
                null,
                partitionKey,
                indexingPolicy,
                cosmosClientFactory);
        }

        internal Task CreateIngestQueryDeleteAsync<T>(
            ConnectionModes connectionModes,
            CollectionTypes collectionTypes,
            IEnumerable<string> documents,
            Query<T> query,
            T testArgs,
            string partitionKey = "/id",
            Cosmos.IndexingPolicy indexingPolicy = null,
            CosmosClientFactory cosmosClientFactory = null)
        {
            return this.CreateIngestQueryDeleteAsync(
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
        internal async Task CreateIngestQueryDeleteAsync<T>(
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
                IList<(Container, IReadOnlyList<CosmosObject>)> collectionsAndDocuments = new List<(Container, IReadOnlyList<CosmosObject>)>();
                foreach (CollectionTypes collectionType in Enum.GetValues(collectionTypes.GetType()).Cast<Enum>().Where(collectionTypes.HasFlag))
                {
                    if (collectionType == CollectionTypes.None)
                    {
                        continue;
                    }

                    Task<(Container, IReadOnlyList<CosmosObject>)> createContainerTask = collectionType switch
                    {
                        CollectionTypes.NonPartitioned => this.CreateNonPartitionedContainerAndIngestDocumentsAsync(
                            documents,
                            indexingPolicy),
                        CollectionTypes.SinglePartition => this.CreateSinglePartitionContainerAndIngestDocumentsAsync(
                            documents,
                            partitionKey,
                            indexingPolicy),
                        CollectionTypes.MultiPartition => this.CreateMultiPartitionContainerAndIngestDocumentsAsync(
                            documents,
                            partitionKey,
                            indexingPolicy),
                        _ => throw new ArgumentException($"Unknown {nameof(CollectionTypes)} : {collectionType}"),
                    };
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
                    foreach ((Container container, IReadOnlyList<CosmosObject> insertedDocuments) in collectionsAndDocuments)
                    {
                        Task queryTask = Task.Run(() => query(container, insertedDocuments, testArgs));
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
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                }

                ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        private static ConnectionMode GetTargetConnectionMode(ConnectionModes connectionMode)
        {
            return connectionMode switch
            {
                ConnectionModes.Gateway => ConnectionMode.Gateway,
                ConnectionModes.Direct => ConnectionMode.Direct,
                _ => throw new ArgumentException($"Unexpected connection mode: {connectionMode}"),
            };
        }

        internal CosmosClient CreateDefaultCosmosClient(ConnectionMode connectionMode)
        {
            return connectionMode switch
            {
                ConnectionMode.Gateway => this.GatewayClient,
                ConnectionMode.Direct => this.Client,
                _ => throw new ArgumentException($"Unexpected connection mode: {connectionMode}"),
            };
        }

        internal CosmosClient CreateNewCosmosClient(ConnectionMode connectionMode)
        {
            return connectionMode switch
            {
                ConnectionMode.Gateway => TestCommon.CreateCosmosClient(true),
                ConnectionMode.Direct => TestCommon.CreateCosmosClient(false),
                _ => throw new ArgumentException($"Unexpected connection mode: {connectionMode}"),
            };
        }

        internal static async Task<List<T>> QueryWithCosmosElementContinuationTokenAsync<T>(
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
                QueryRequestOptions computeRequestOptions = new QueryRequestOptions
                {
                    IfMatchEtag = queryRequestOptions.IfMatchEtag,
                    IfNoneMatchEtag = queryRequestOptions.IfNoneMatchEtag,
                    MaxItemCount = queryRequestOptions.MaxItemCount,
                    ResponseContinuationTokenLimitInKb = queryRequestOptions.ResponseContinuationTokenLimitInKb,
                    EnableScanInQuery = queryRequestOptions.EnableScanInQuery,
                    EnableLowPrecisionOrderBy = queryRequestOptions.EnableLowPrecisionOrderBy,
                    MaxBufferedItemCount = queryRequestOptions.MaxBufferedItemCount,
                    SessionToken = queryRequestOptions.SessionToken,
                    ConsistencyLevel = queryRequestOptions.ConsistencyLevel,
                    MaxConcurrency = queryRequestOptions.MaxConcurrency,
                    PartitionKey = queryRequestOptions.PartitionKey,
                    CosmosSerializationFormatOptions = queryRequestOptions.CosmosSerializationFormatOptions,
                    Properties = queryRequestOptions.Properties,
                    IsEffectivePartitionKeyRouting = queryRequestOptions.IsEffectivePartitionKeyRouting,
                    CosmosElementContinuationToken = queryRequestOptions.CosmosElementContinuationToken,
                };

                computeRequestOptions.ExecutionEnvironment = ExecutionEnvironment.Compute;
                computeRequestOptions.CosmosElementContinuationToken = continuationToken;

                using (FeedIteratorInternal<T> itemQuery = (FeedIteratorInternal<T>)container.GetItemQueryIterator<T>(
                   queryText: query,
                   requestOptions: computeRequestOptions))
                {
                    try
                    {
                        FeedResponse<T> cosmosQueryResponse = await itemQuery.ReadNextAsync();
                        if (queryRequestOptions.MaxItemCount.HasValue)
                        {
                            Assert.IsTrue(
                                cosmosQueryResponse.Count <= queryRequestOptions.MaxItemCount.Value,
                                $"Max Item Count is not being honored. Got {cosmosQueryResponse.Count} documents when {queryRequestOptions.MaxItemCount.Value} is the max.");
                        }

                        resultsFromCosmosElementContinuationToken.AddRange(cosmosQueryResponse);

                        // Force a rewrite of the continuation token, so that we test the case where we roundtrip it over the wire.
                        // There was a bug where resuming from double.NaN lead to an exception,
                        // since we parsed the type assuming it was always a double and not a string.
                        CosmosElement originalContinuationToken = itemQuery.GetCosmosElementContinuationToken();
                        continuationToken = originalContinuationToken != null ? CosmosElement.Parse(originalContinuationToken.ToString()) : null;
                    }
                    catch (CosmosException cosmosException) when (cosmosException.StatusCode == (HttpStatusCode)429)
                    {
                    }
                }
            } while (continuationToken != null);

            return resultsFromCosmosElementContinuationToken;
        }

        internal static async Task<List<T>> QueryWithContinuationTokensAsync<T>(
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

                try
                {
                    while (true)
                    {
                        try
                        {
                            FeedResponse<T> cosmosQueryResponse = await itemQuery.ReadNextAsync();
                            if (queryRequestOptions.MaxItemCount.HasValue)
                            {
                                Assert.IsTrue(
                                    cosmosQueryResponse.Count <= queryRequestOptions.MaxItemCount.Value,
                                    $"Max Item Count is not being honored. Got {cosmosQueryResponse.Count} when {queryRequestOptions.MaxItemCount.Value} is the max.");
                            }

                            resultsFromContinuationToken.AddRange(cosmosQueryResponse);
                            continuationToken = cosmosQueryResponse.ContinuationToken;
                            break;
                        }
                        catch (CosmosException cosmosException) when (cosmosException.StatusCode == (HttpStatusCode)429)
                        {
                            itemQuery.Dispose();
                            itemQuery = container.GetItemQueryIterator<T>(
                                queryText: query,
                                requestOptions: queryRequestOptions,
                                continuationToken: continuationToken);
                        }
                    }
                }
                finally
                {
                    itemQuery.Dispose();
                }
            } while (continuationToken != null);

            return resultsFromContinuationToken;
        }

        internal static async Task<List<T>> QueryWithoutContinuationTokensAsync<T>(
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
            try
            {
                string continuationTokenForRetries = null;
                while (itemQuery.HasMoreResults)
                {
                    try
                    {
                        FeedResponse<T> page = await itemQuery.ReadNextAsync();
                        results.AddRange(page);

                        if (queryRequestOptions.MaxItemCount.HasValue)
                        {
                            if (page.Count > queryRequestOptions.MaxItemCount.Value)
                            {
                                Console.WriteLine();
                            }
                            Assert.IsTrue(
                                page.Count <= queryRequestOptions.MaxItemCount.Value,
                                $"Max Item Count is not being honored. Got {page.Count} documents when the max is {queryRequestOptions.MaxItemCount.Value}.");
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
                        itemQuery.Dispose();
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
            }
            finally
            {
                itemQuery.Dispose();
            }

            return results;
        }

        internal static async Task NoOp()
        {
            await Task.Delay(0);
        }

        internal static Task<List<CosmosElement>> RunQueryAsync(
            Container container,
            string query,
            QueryRequestOptions queryRequestOptions = null)
        {
            return RunQueryCombinationsAsync(
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

        internal static async Task<List<CosmosElement>> RunQueryCombinationsAsync(
            Container container,
            string query,
            QueryRequestOptions queryRequestOptions,
            QueryDrainingMode queryDrainingMode)
        {
            if (queryDrainingMode == QueryDrainingMode.None)
            {
                throw new ArgumentOutOfRangeException(nameof(queryDrainingMode));
            }

            Dictionary<QueryDrainingMode, List<CosmosElement>> queryExecutionResults = new Dictionary<QueryDrainingMode, List<CosmosElement>>();

            if (queryDrainingMode.HasFlag(QueryDrainingMode.HoldState))
            {
                List<CosmosElement> queryResultsWithoutContinuationToken = await QueryWithoutContinuationTokensAsync<CosmosElement>(
                    container,
                    query,
                    queryRequestOptions);

                queryExecutionResults[QueryDrainingMode.HoldState] = queryResultsWithoutContinuationToken;
            }

            if (queryDrainingMode.HasFlag(QueryDrainingMode.ContinuationToken))
            {
                List<CosmosElement> queryResultsWithContinuationTokens = await QueryWithContinuationTokensAsync<CosmosElement>(
                    container,
                    query,
                    queryRequestOptions);

                queryExecutionResults[QueryDrainingMode.ContinuationToken] = queryResultsWithContinuationTokens;
            }

            if (queryDrainingMode.HasFlag(QueryDrainingMode.CosmosElementContinuationToken))
            {
                List<CosmosElement> queryResultsWithCosmosElementContinuationToken = await QueryWithCosmosElementContinuationTokenAsync<CosmosElement>(
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
                        List<CosmosElement> first = queryExecutionResults[queryDrainingMode1];
                        List<CosmosElement> second = queryExecutionResults[queryDrainingMode2];
                        Assert.IsTrue(
                            first.SequenceEqual(second),
                            $"{query} returned different results.\n" +
                            $"{queryDrainingMode1}: {JsonConvert.SerializeObject(first)}\n" +
                            $"{queryDrainingMode2}: {JsonConvert.SerializeObject(second)}\n");
                    }
                }
            }

            return queryExecutionResults.Values.First();
        }

        internal async Task<List<T>> RunSinglePartitionQuery<T>(
            Container container,
            string query,
            QueryRequestOptions requestOptions = null)
        {
            List<T> items = new List<T>();
            using (FeedIterator<T> resultSetIterator = container.GetItemQueryIterator<T>(
                query,
                requestOptions: requestOptions))
            {
                while (resultSetIterator.HasMoreResults)
                {
                    items.AddRange(await resultSetIterator.ReadNextAsync());
                }
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