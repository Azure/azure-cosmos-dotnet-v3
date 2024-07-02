﻿//-----------------------------------------------------------------------
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
    using System.Runtime.CompilerServices;
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
        internal RequestChargeTrackingHandler GatewayRequestChargeHandler = new RequestChargeTrackingHandler();
        internal RequestChargeTrackingHandler DirectRequestChargeHandler = new RequestChargeTrackingHandler();
        internal CosmosClient GatewayClient;
        internal CosmosClient Client;
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

        [TestInitialize]
        public async Task Initialize()
        {
            this.GatewayClient = TestCommon.CreateCosmosClient(true, builder => builder.AddCustomHandlers(this.GatewayRequestChargeHandler));
            this.Client = TestCommon.CreateCosmosClient(false, builder => builder.AddCustomHandlers(this.DirectRequestChargeHandler));
            this.database = await this.Client.CreateDatabaseAsync(Guid.NewGuid().ToString() + "db");
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await this.database.DeleteStreamAsync();
            this.Client.Dispose();
            this.GatewayClient.Dispose();
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
            IRoutingMapProvider routingMapProvider = await this.Client.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            Assert.IsNotNull(routingMapProvider);

            IReadOnlyList<PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(
                container.ResourceId, 
                fullRange,
                NoOpTrace.Singleton);
            return ranges;
        }

        private async Task<Container> CreateMultiPartitionContainer(
            string partitionKey = "/id",
            Microsoft.Azure.Cosmos.IndexingPolicy indexingPolicy = null,
            Cosmos.GeospatialType geospatialType = Cosmos.GeospatialType.Geography)
        {
            ContainerResponse containerResponse = await this.CreatePartitionedContainer(
                throughput: 25000,
                partitionKey: partitionKey,
                indexingPolicy: indexingPolicy,
                geospatialType);

            IReadOnlyList<PartitionKeyRange> ranges = await this.GetPartitionKeyRanges(containerResponse);
            Assert.IsTrue(
                ranges.Count() > 1,
                $"{nameof(CreateMultiPartitionContainer)} failed to create a container with more than 1 physical partition.");

            return containerResponse;
        }

        private async Task<Container> CreateSinglePartitionContainer(
            string partitionKey = "/id",
            Microsoft.Azure.Cosmos.IndexingPolicy indexingPolicy = null,
            Cosmos.GeospatialType geospatialType = Cosmos.GeospatialType.Geography)
        {
            ContainerResponse containerResponse = await this.CreatePartitionedContainer(
                throughput: 4000,
                partitionKey: partitionKey,
                indexingPolicy: indexingPolicy,
                geospatialType: geospatialType);

            Assert.IsNotNull(containerResponse);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.IsNotNull(containerResponse.Resource);
            Assert.IsNotNull(containerResponse.Resource.ResourceId);

            IReadOnlyList<PartitionKeyRange> ranges = await this.GetPartitionKeyRanges(containerResponse);
            Assert.AreEqual(1, ranges.Count());

            return containerResponse;
        }

        private async Task<Container> CreateNonPartitionedContainerAsync(
            Microsoft.Azure.Cosmos.IndexingPolicy indexingPolicy = null,
            Cosmos.GeospatialType geospatialType = Cosmos.GeospatialType.Geography)
        {
            string containerName = Guid.NewGuid().ToString() + "container";
            await NonPartitionedContainerHelper.CreateNonPartitionedContainer(
                this.database,
                containerName,
                indexingPolicy == null ? null : JsonConvert.SerializeObject(indexingPolicy),
                geospatialType);

            return this.database.GetContainer(containerName);
        }

        private async Task<ContainerResponse> CreatePartitionedContainer(
            int throughput,
            string partitionKey = "/id",
            Microsoft.Azure.Cosmos.IndexingPolicy indexingPolicy = null,
            Cosmos.GeospatialType geospatialType = Cosmos.GeospatialType.Geography)
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
                    },
                    GeospatialConfig = new Cosmos.GeospatialConfig(geospatialType)
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
            Cosmos.IndexingPolicy indexingPolicy = null,
            Cosmos.GeospatialType geospatialType = Cosmos.GeospatialType.Geography)
        {
            return this.CreateContainerAndIngestDocumentsAsync(
                CollectionTypes.NonPartitioned,
                documents,
                partitionKey: null,
                indexingPolicy: indexingPolicy,
                geospatialType: geospatialType);
        }

        private Task<(Container, IReadOnlyList<CosmosObject>)> CreateSinglePartitionContainerAndIngestDocumentsAsync(
            IEnumerable<string> documents,
            string partitionKey = "/id",
            Cosmos.IndexingPolicy indexingPolicy = null,
            Cosmos.GeospatialType geospatialType = Cosmos.GeospatialType.Geography)
        {
            return this.CreateContainerAndIngestDocumentsAsync(
                CollectionTypes.SinglePartition,
                documents,
                partitionKey,
                indexingPolicy,
                geospatialType);
        }

        private Task<(Container, IReadOnlyList<CosmosObject>)> CreateMultiPartitionContainerAndIngestDocumentsAsync(
            IEnumerable<string> documents,
            string partitionKey = "/id",
            Cosmos.IndexingPolicy indexingPolicy = null,
            Cosmos.GeospatialType geospatialType = Cosmos.GeospatialType.Geography)
        {
            return this.CreateContainerAndIngestDocumentsAsync(
                CollectionTypes.MultiPartition,
                documents,
                partitionKey,
                indexingPolicy,
                geospatialType);
        }

        private async Task<(Container, IReadOnlyList<CosmosObject>)> CreateContainerAndIngestDocumentsAsync(
            CollectionTypes collectionType,
            IEnumerable<string> documents,
            string partitionKey = "/id",
            Cosmos.IndexingPolicy indexingPolicy = null,
            Cosmos.GeospatialType geospatialType = Cosmos.GeospatialType.Geography)
        {
            Container container = collectionType switch
            {
                CollectionTypes.NonPartitioned => await this.CreateNonPartitionedContainerAsync(indexingPolicy, geospatialType),
                CollectionTypes.SinglePartition => await this.CreateSinglePartitionContainer(partitionKey, indexingPolicy, geospatialType),
                CollectionTypes.MultiPartition => await this.CreateMultiPartitionContainer(partitionKey, indexingPolicy, geospatialType),
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
                this.Client.Dispose();
                this.GatewayClient.Dispose();
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
            CosmosClientFactory cosmosClientFactory = null,
            Cosmos.GeospatialType geospatialType = Cosmos.GeospatialType.Geography)
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
                cosmosClientFactory,
                geospatialType);
        }

        internal Task CreateIngestQueryDeleteAsync<T>(
            ConnectionModes connectionModes,
            CollectionTypes collectionTypes,
            IEnumerable<string> documents,
            Query<T> query,
            T testArgs,
            string partitionKey = "/id",
            Cosmos.IndexingPolicy indexingPolicy = null,
            CosmosClientFactory cosmosClientFactory = null,
            Cosmos.GeospatialType geospatialType = Cosmos.GeospatialType.Geography)
        {
            return this.CreateIngestQueryDeleteAsync(
                connectionModes,
                collectionTypes,
                documents,
                query,
                cosmosClientFactory ?? this.CreateDefaultCosmosClient,
                testArgs,
                partitionKey,
                indexingPolicy,
                geospatialType);
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
            Cosmos.IndexingPolicy indexingPolicy = null,
            Cosmos.GeospatialType geospatialType = Cosmos.GeospatialType.Geography)
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
                            indexingPolicy,
                            geospatialType),
                        CollectionTypes.SinglePartition => this.CreateSinglePartitionContainerAndIngestDocumentsAsync(
                            documents,
                            partitionKey,
                            indexingPolicy,
                            geospatialType),
                        CollectionTypes.MultiPartition => this.CreateMultiPartitionContainerAndIngestDocumentsAsync(
                            documents,
                            partitionKey,
                            indexingPolicy,
                            geospatialType),
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
            int resultCount = 0;
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
                            resultCount += cosmosQueryResponse.Count;
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

            Assert.AreEqual(resultsFromContinuationToken.Count, resultCount);
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

            int resultCount = 0;
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
                        resultCount += page.Count;

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
                            resultCount = 0;
                        }
                    }
                }
            }
            finally
            {
                itemQuery.Dispose();
            }

            Assert.AreEqual(results.Count, resultCount);
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
            return RunQueryAsync<CosmosElement>(container, query, queryRequestOptions);
        }

        internal static Task<List<T>> RunQueryAsync<T>(
            Container container,
            string query,
            QueryRequestOptions queryRequestOptions = null)
        {
            return RunQueryCombinationsAsync<T>(
                container,
                query,
                queryRequestOptions,
                QueryDrainingMode.ContinuationToken | QueryDrainingMode.HoldState);
        }

        [Flags]
        public enum QueryDrainingMode
        {
            None = 0,
            HoldState = 1,
            ContinuationToken = 2,
        }

        internal static Task<List<CosmosElement>> RunQueryCombinationsAsync(
            Container container,
            string query,
            QueryRequestOptions queryRequestOptions,
            QueryDrainingMode queryDrainingMode)
        {
            return RunQueryCombinationsAsync<CosmosElement>(container, query, queryRequestOptions, queryDrainingMode);
        }

            internal static async Task<List<T>> RunQueryCombinationsAsync<T>(
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

            foreach (QueryDrainingMode queryDrainingMode1 in queryExecutionResults.Keys)
            {
                foreach (QueryDrainingMode queryDrainingMode2 in queryExecutionResults.Keys)
                {
                    if (queryDrainingMode1 != queryDrainingMode2)
                    {
                        List<T> first = queryExecutionResults[queryDrainingMode1];
                        List<T> second = queryExecutionResults[queryDrainingMode2];
                        Assert.IsTrue(first.SequenceEqual(second));
                    }
                }
            }

            return queryExecutionResults.Values.First();
        }

        internal static async IAsyncEnumerable<FeedResponse<T>> RunSimpleQueryAsync<T>(
            Container container,
            string query,
            QueryRequestOptions requestOptions = null)
        {
            using (FeedIterator<T> resultSetIterator = container.GetItemQueryIterator<T>(
                query,
                requestOptions: requestOptions))
            {
                while (resultSetIterator.HasMoreResults)
                {
                    FeedResponse<T> response = await resultSetIterator.ReadNextAsync();
                    yield return response;
                }
            }
        }

        internal static async IAsyncEnumerable<FeedResponse<T>> RunSimpleQueryWithNewIteratorAsync<T>(
           Container container,
           string query,
           QueryRequestOptions requestOptions = null)
        {
            string continuationToken = null;
            while (true)
            {
                using (FeedIterator<T> resultSetIterator = container.GetItemQueryIterator<T>(
                query,
                continuationToken,
                requestOptions: requestOptions))
                {
                    while (resultSetIterator.HasMoreResults)
                    {
                        FeedResponse<T> response = await resultSetIterator.ReadNextAsync();

                        continuationToken = response.ContinuationToken;

                        yield return response;

                        break;
                    }

                    if (!resultSetIterator.HasMoreResults)
                        break;
                }
            }
        }

        internal static async IAsyncEnumerable<ResponseMessage> RunSimpleQueryAsync(
           Container container,
           string query,
           QueryRequestOptions requestOptions = null)
        {
            using FeedIterator resultSetIterator = container.GetItemQueryStreamIterator(
                query,
                null,
                requestOptions: requestOptions);

            while (resultSetIterator.HasMoreResults)
            {
                ResponseMessage response = await resultSetIterator.ReadNextAsync();
                yield return response;
            }
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

        internal class RequestChargeTrackingHandler : RequestHandler
        {
            private double totalRequestCharge;
            private bool isEnabled;

            public void StartTracking()
            {
                this.isEnabled = true;
            }

            public double StopTracking()
            {
                double requestCharge = this.totalRequestCharge;
                this.isEnabled = false;
                this.totalRequestCharge = 0;
                return requestCharge;
            }

            public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                ResponseMessage response = await base.SendAsync(request, cancellationToken);

                if (this.isEnabled)
                {
                    this.AddRequestCharge(response.Headers.RequestCharge);
                }

                return response;
            }

            private void AddRequestCharge(double requestCharge)
            {
                if (requestCharge == 0)
                {
                    return;
                }

                double startValue;
                double currentValue = this.totalRequestCharge;

                do
                {
                    startValue = currentValue;
                    double targetValue = currentValue + requestCharge;
                    currentValue = Interlocked.CompareExchange(ref this.totalRequestCharge, targetValue, startValue);
                } while (currentValue != startValue);
            }
        }
    }
}