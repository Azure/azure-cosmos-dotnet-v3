namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// This is an end to end test that requires connecting to azure cosmos db accounts.
    /// </summary>
    [TestClass]
    public class ConflictsE2ETest
    {
        private const int MaxRetries = 10;

        private const string Database = "Microsoft.Azure.Cosmos.EmulatorTests.Conflicts";
        private const string Collection = "ConflictsTest";
        private const string Key = "";
        private static readonly Endpoint Endpoint1 = new Endpoint("", ConnectionMode.Direct);
        private static readonly Endpoint Endpoint2 = new Endpoint("", ConnectionMode.Direct);

        private class Endpoint
        {
            public Endpoint(string url, ConnectionMode connectionMode)
            {
                this.Url = url;
                this.ConnectionMode = connectionMode;
            }

            public ConnectionMode ConnectionMode { get; }

            public string Url { get; }
        }

        /// <summary>
        /// Tests querying conflicts in a cosmosdb collection.
        /// </summary>
        /// <remarks>
        /// This test uses ConflictsTestSettings.json for test configuration.
        /// 1. An actual cosmosdb account in Azure is required for this test to run since none of the emulators do not allow for required test setup.
        /// 2. Test setup will create a well known database (drop if it exists) and collection.
        /// 3. The conditions for generating a conflict are subject to backend non-determinism. For increasing chances of generating a conflict:
        ///    - Ensure that the account is set to use eventual consistency
        ///    - Use more than 2 regions in the configuration.
        /// </remarks>
        /// <returns></returns>
        [Ignore]
        [TestMethod]
        public async Task TestConflicts()
        {
            Assert.IsTrue(!string.IsNullOrWhiteSpace(Key), "Please specify a valid key");

            IReadOnlyList<(CosmosClient Client, Container Container)> cosmosContainers = await this.CreateDatabaseAndContainer(
                Database,
                Collection,
                Key,
                Endpoint1,
                Endpoint2);

            await this.InsertWithoutConflict(cosmosContainers);
            await this.InsertWithConflict(cosmosContainers);
            await this.VerifyConflict(cosmosContainers);
        }

        private async Task VerifyConflict(IReadOnlyList<(CosmosClient Client, Container Container)> cosmosContainers)
        {
            List<List<CosmosObject>> conflictsUsingDefaultIterator = await this.GetConflictsUsingDefaultIterator(cosmosContainers);
            List<List<CosmosObject>> conflictsUsingQueryWithoutOptions = await this.GetConflictsUsingQueryWithoutOptions(cosmosContainers);

            Assert.AreEqual(conflictsUsingDefaultIterator.Count, conflictsUsingQueryWithoutOptions.Count, "Conflict count should be identical");
            for (int i = 0; i < conflictsUsingDefaultIterator.Count; i++)
            {
                Assert.AreEqual(string.Join(",", conflictsUsingDefaultIterator[i].ToString()), string.Join(",", conflictsUsingQueryWithoutOptions[i].ToString()));
            }
        }

        private async Task<List<List<CosmosObject>>> GetConflictsUsingQueryWithoutOptions(IReadOnlyList<(CosmosClient Client, Container Container)> cosmosContainers)
            => await this.GetConflicts(
                cosmosContainers,
                query: @"SELECT * FROM c",
                options: null);

        private async Task<List<List<CosmosObject>>> GetConflictsUsingDefaultIterator(IReadOnlyList<(CosmosClient Client, Container Container)> cosmosContainers)
            => await this.GetConflicts(cosmosContainers, query: null, options: null);

        private async Task<List<List<CosmosObject>>> GetConflicts(
            IReadOnlyList<(CosmosClient Client, Container Container)> cosmosContainers,
            string query,
            QueryRequestOptions options)
        {
            List<List<CosmosObject>> allConflicts = new List<List<CosmosObject>>();
            foreach ((CosmosClient client, Container container) pair in cosmosContainers)
            {
                List<CosmosObject> clientReportedConflicts = new List<CosmosObject>();
                FeedIterator<CosmosObject> iterator = pair.container.Conflicts.GetConflictQueryIterator<CosmosObject>(queryText: query, requestOptions: options);
                while (iterator.HasMoreResults)
                {
                    FeedResponse<CosmosObject> page = await iterator.ReadNextAsync();
                    clientReportedConflicts.AddRange(page);
                }

                allConflicts.Add(clientReportedConflicts);
            }

            // Ideally each client will observe exactly 1 conflict. However this is dependent upon regional (eventual) consistency and underlying race condition with this test.
            Assert.IsTrue(allConflicts.Any(list => list.Count == 1), "Exactly 1 conflict is expected!");

            return allConflicts;
        }

        /// <summary>
        /// Inserts a document that is guaranteed to not conflict with any other.
        /// </summary>
        private async Task InsertWithoutConflict(IReadOnlyList<(CosmosClient Client, Container Container)> cosmosContainers)
            => await this.InsertFromMultipleClients(
                cosmosContainers,
                payloadFormat: @"{{""id"" : ""NoConflict_{0}"", ""type"":""noconflict"", ""pk"":""1""}}",
                clientFilter: clientIndex => clientIndex == 0);

        /// <summary>
        /// Insert documents until exactly one conflict is generated.
        /// This is a non-deterministic operation (in terms of both duration and outcome) due to backend's behavior.
        /// It will terminate the test based on simple heuristic if desired outcome cannot be achieved.
        /// </summary>
        private async Task InsertWithConflict(IReadOnlyList<(CosmosClient Client, Container Container)> cosmosContainers)
            => await this.InsertFromMultipleClients(
                cosmosContainers,
                payloadFormat: @"{{""id"" : ""Conflict_{0}"", ""type"":""conflict"", ""pk"":""1"", ""index"":{1}}}");

        /// <summary>
        /// Inserts items from multiple clients.
        /// </summary>
        /// <param name="cosmosContainers">Containers to insert documents to.</param>
        /// <param name="payloadFormat">Format of the document with placeholders for insertion iteration (one round across all clients) and optional client index.</param>
        /// <param name="clientFilter">Optional filter that determines whether a client should be used for insertion.</param>
        /// <returns></returns>
        private async Task InsertFromMultipleClients(
            IReadOnlyList<(CosmosClient Client, Container Container)> cosmosContainers,
            string payloadFormat,
            Func<int, bool> clientFilter = null)
        {
            PartitionKey partitionKey = new PartitionKey("1");
            bool retry = true;
            int i = 0;

            // To offset the backend specific non-determinism, we change the order in which we use containers to create the items.
            // Other mitigations to explore include having more than 2 regions and ordering those randomly while creating items.
            IEnumerable<Container> containersInOrder = cosmosContainers.Select(pair => pair.Container);
            IEnumerable<Container> containersInReverseOrder = cosmosContainers.Reverse().Select(pair => pair.Container);
            while (retry)
            {
                int clientIndex = 0;
                List<ResponseMessage> responses = new List<ResponseMessage>();
                IEnumerable<Container> containers =
                    i % 2 == 1 ?
                    containersInOrder :
                    containersInReverseOrder;
                foreach (Container container in containers)
                {
                    bool useClient = clientFilter == null || clientFilter(clientIndex);
                    if (useClient)
                    {
                        ResponseMessage response = await this.CreateItem(
                            container,
                            string.Format(payloadFormat, i, clientIndex),
                            partitionKey);
                        responses.Add(response);
                    }

                    clientIndex++;
                }

                Assert.IsTrue(responses.Count > 0, "At least one client should attempt document creation!");

                // Sometimes the conflicts may be detected (and rejected) by the backend with status code = Conflict synchronously with the request.
                // We keep retrying until all clients are able to "successfully" create item in the backend which will be later detected as conflict.
                retry = responses.Any(response => response.StatusCode != HttpStatusCode.Created);
                i++;

                // Even with the measures above, the conflicts may continue to get detected (and rejected) by the backend perpetually in a synchronous manner.
                // After 3000 tries (which can take upto 5 minutes for 2 regions), we determine that the test is inconclusive, since the setup failed.
                if (i > 3000)
                {
                    string expectedResponses = string.Join(",", Enumerable.Repeat("Created", cosmosContainers.Count));
                    string actualResponses = string.Join(",", responses.Select(response => response.StatusCode.ToString()));
                    Assert.Fail($@"Document insertion failed after 3000 tries. Please rerun the test. Expected responses : ""{expectedResponses}"". Actual responses : ""{actualResponses}"".");
                }
            }
        }

        private async Task<ResponseMessage> CreateItem(Container container, string payload, PartitionKey partitionKey)
        {
            return await this.ExecuteOperationWithRetry(
                MaxRetries,
                () => container.CreateItemStreamAsync(
                    this.ToStream(payload),
                    partitionKey),
                // Since the test also creates the database and document collection, first few read/write operations on the collection can return NotFound.
                responseMessage => responseMessage.StatusCode == HttpStatusCode.NotFound);
        }

        private Stream ToStream(string stringValue)
        {
            MemoryStream stream = new();
            StreamWriter writer = new(stream);
            writer.Write(stringValue);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        /// <summary>
        /// Instantiates client and container pointing to each region. Creates (drops if exists) database, container using one region's connections.
        /// </summary>
        /// <returns>Returns the CosmosClient and Container pointing to each region.</returns>
        private async Task<IReadOnlyList<(CosmosClient Client, Container Container)>> CreateDatabaseAndContainer(
            string database,
            string collection,
            string key,
            params Endpoint[] endpoints)
        {
            Assert.IsTrue(endpoints?.Length > 1, "At least one endpoint must be specified");

            HashSet<string> endpointSet = new HashSet<string>(endpoints.Select(endpoint => endpoint.Url));
            Assert.AreEqual(endpoints.Length, endpointSet.Count, "Please specify unique endpoints!");

            int endpointIndex = 0;
            List<(CosmosClient Client, Container Container)> clients = new();
            foreach (Endpoint endpoint in endpoints)
            {
                CosmosClient client = new CosmosClient(endpoint.Url, key, new CosmosClientOptions { ConnectionMode = endpoint.ConnectionMode });

                if (endpointIndex == 0)
                {
                    ConsistencyLevel consistencyLevel = await client.GetAccountConsistencyLevelAsync();
                    Assert.AreEqual(ConsistencyLevel.Eventual, consistencyLevel, "Only account with eventual consistency is supported by this test.");
                }

                DatabaseResponse databaseResponse = await this.ExecuteOperationWithRetry(
                    MaxRetries,
                    () => client.CreateDatabaseIfNotExistsAsync(database));
                if (endpointIndex == 0 && databaseResponse.StatusCode == HttpStatusCode.OK)
                {
                    await databaseResponse.Database.DeleteAsync();
                    databaseResponse = await this.ExecuteOperationWithRetry(
                        MaxRetries,
                        () => client.CreateDatabaseIfNotExistsAsync(database));
                }

                HttpStatusCode expectedStatus = endpointIndex == 0 ? HttpStatusCode.Created : HttpStatusCode.OK;
                Assert.AreEqual(expectedStatus, databaseResponse.StatusCode,
                    $"Endpoint#: {endpointIndex}, Endpoint : {endpoint.Url}. CreateDatabaseIfNotExistsAsync received unexpected response.");

                ContainerResponse containerResponse = await this.ExecuteOperationWithRetry(
                    MaxRetries,
                    () => databaseResponse.Database.CreateContainerIfNotExistsAsync(
                            new ContainerProperties(collection, "/pk")
                            {
                                ConflictResolutionPolicy = new ConflictResolutionPolicy() { Mode = ConflictResolutionMode.Custom }
                            }));
                Assert.AreEqual(expectedStatus, databaseResponse.StatusCode,
                    $"Endpoint#: {endpointIndex}, Endpoint : {endpoint.Url}. CreateContainerIfNotExistsAsync received unexpected response.");

                clients.Add((client, containerResponse.Container));
                endpointIndex++;
            }

            return clients;
        }

        private async Task<T> ExecuteOperationWithRetry<T>(int maxRetryCount, Func<Task<T>> operation, Func<T, bool> shouldRetryWithoutException = null)
        {
            for (int i = 0; i < maxRetryCount; i++)
            {
                try
                {
                    T result = await operation();
                    if (shouldRetryWithoutException != null && shouldRetryWithoutException(result))
                    {
                        if (i + 1 < maxRetryCount)
                        {
                            await Task.Delay(i * 1000);
                            continue;
                        }

                        break;
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Attempt {i + 1}. Max Retries {maxRetryCount}. Exception: {ex}.");
                    if (i + 1 < maxRetryCount)
                    {
                        Thread.Sleep(i * 1000);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            throw new InvalidOperationException($"Operation failed after retries!");
        }
    }
}
