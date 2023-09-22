namespace Microsoft.Azure.Cosmos.EmulatorTests.Conflicts
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Security.Principal;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class ConflictsTest
    {
        private const int MaxRetries = 10;

        [TestMethod]
        public async Task TestConflicts()
        {
            IReadOnlyList<(CosmosClient Client, Container Container)> cosmosContainers = await this.CreateCosmosClients();

            foreach((CosmosClient client, Container container) pair in cosmosContainers)
            {
                Console.WriteLine(pair.container.Id);
            }

            await this.GenerateConflict(cosmosContainers);
            await this.VerifyConflict(cosmosContainers);
        }

        private async Task VerifyConflict(IReadOnlyList<(CosmosClient Client, Container Container)> cosmosContainers)
        {
            int conflictCount = 0;
            foreach((CosmosClient client, Container container) pair in cosmosContainers)
            {
                FeedIterator<dynamic> iterator = pair.container.Conflicts.GetConflictQueryIterator<dynamic>("SELECT * FROM c");
                while(iterator.HasMoreResults)
                {
                    FeedResponse<dynamic> page = await iterator.ReadNextAsync();
                    foreach (dynamic item in page)
                    {
                        conflictCount++;
                        Console.WriteLine($"{{{item.ToString()}}},");
                    }
                }
            }

            Assert.AreNotEqual(0, conflictCount, "At least one conflict expected!");
        }

        private async Task GenerateConflict(IReadOnlyList<(CosmosClient Client, Container Container)> cosmosContainers)
        {
            string payloadFormat = @"{{""id"" : ""adf{0}"", ""pk"":""1"", ""index"":{1}}}";
            PartitionKey partitionKey = new PartitionKey("1");
            //List<Task> inserts = new();
            for (int i = 0; i < 1; i++)
            {
                //IEnumerable<Task> tasks = cosmosContainers.Select(
                //    (pair, index) => Task.Factory.StartNew(
                //async () =>
                        int index = 0;
                        foreach ((CosmosClient Client, Container Container) pair in cosmosContainers)
                        {
                            ResponseMessage response = await this.ExecuteOperationWithRetry<ResponseMessage>(
                                MaxRetries,
                                () => pair.Container.CreateItemStreamAsync(
                                    string.Format(payloadFormat, i, index).ToStream(),
                                    partitionKey),
                                responseMessage => responseMessage.StatusCode == HttpStatusCode.NotFound);
                            //Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, $"Error while creating document i={i}; index={index}");
                            index++;
                        }
                        //));
                // inserts.AddRange(tasks);
            }

            // Task.WaitAll(inserts.ToArray());
        }

        private async Task<IReadOnlyList<(CosmosClient Client, Container Container)>> CreateCosmosClients()
        {
            string content = File.ReadAllText(@"Conflicts\ConflictsTestSettings.json");
            CosmosObject root = CosmosObject.Parse(content);
            string database = "Microsoft.Azure.Cosmos.EmulatorTests.Conflicts";
            string collection = "ConflictsTest";
            string key = this.GetStringValue(root, "Key");
            CosmosArray endpoints = this.GetValue<CosmosArray>(root, "Endpoints");

            int endpointIndex = 0;
            List<(CosmosClient Client, Container Container)> clients = new();
            foreach (CosmosObject endpoint in endpoints.Cast<CosmosObject>())
            {
                string endpointUrl = this.GetStringValue(endpoint, "EndpointUrl");
                string connectionModeString = this.GetStringValue(endpoint, "ConnectionMode");

                ConnectionMode connectionMode = this.ParseConnectionMode(connectionModeString);

                CosmosClient client = new CosmosClient(endpointUrl, key, new CosmosClientOptions { ConnectionMode = connectionMode });

                DatabaseResponse databaseResponse = await this.ExecuteOperationWithRetry<DatabaseResponse>(
                    MaxRetries,
                    () => client.CreateDatabaseIfNotExistsAsync(database));
                if ((endpointIndex  == 0) && (databaseResponse.StatusCode == System.Net.HttpStatusCode.OK))
                {
                    await databaseResponse.Database.DeleteAsync();
                    databaseResponse = await this.ExecuteOperationWithRetry<DatabaseResponse>(
                        MaxRetries,
                        () => client.CreateDatabaseIfNotExistsAsync(database));
                }

                HttpStatusCode expectedStatus = endpointIndex == 0 ? HttpStatusCode.Created : HttpStatusCode.OK;
                Assert.AreEqual(expectedStatus, databaseResponse.StatusCode,
                    $"Endpoint#: {endpointIndex}, Endpoint : {endpointUrl}. CreateDatabaseIfNotExistsAsync received unexpected response.");

                ContainerResponse containerResponse = await this.ExecuteOperationWithRetry<ContainerResponse>(
                    MaxRetries,
                    () => databaseResponse.Database.CreateContainerIfNotExistsAsync(
                            new ContainerProperties(collection, "/pk")
                            { 
                                ConflictResolutionPolicy = new ConflictResolutionPolicy() { Mode = ConflictResolutionMode.Custom }
                            }));
                Assert.AreEqual(expectedStatus, databaseResponse.StatusCode,
                    $"Endpoint#: {endpointIndex}, Endpoint : {endpointUrl}. CreateContainerIfNotExistsAsync received unexpected response.");

                clients.Add((client, containerResponse.Container));
                endpointIndex++;
            }

            return clients;
        }

        private async Task<T> ExecuteOperationWithRetry<T>(int maxRetryCount, Func<Task<T>> operation, Func<T, bool> shouldRetry = null)
        {
            for (int i = 0; i < maxRetryCount; i++)
            {
                try
                {
                    T result = await operation();
                    if (shouldRetry != null && shouldRetry(result))
                    {
                        if (i + 1 < maxRetryCount)
                        {
                            Thread.Sleep(i * 1000);
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

        private ConnectionMode ParseConnectionMode(string stringValue) => stringValue.ToLowerInvariant() switch
            {
                "direct" => ConnectionMode.Direct,
                "gateway" => ConnectionMode.Gateway,
                _ => throw new InvalidOperationException($"Unsupported type {stringValue.ToLowerInvariant()}")
            };

        private string GetStringValue(CosmosObject cosmosObject, string propertyName)
        {
            CosmosString cosmosString = this.GetValue<CosmosString>(cosmosObject, propertyName);
            return this.ToString(cosmosString);
        }

        private string ToString(CosmosString cosmosString) => cosmosString.Value.ToString();

        private T GetValue<T>(CosmosObject obj, string propertyName)
            where T : CosmosElement
        {
            CosmosElement val = obj[propertyName];
            Debug.Assert(val == null || val is T, "ConflictsTest Assert!", "Unexpected type!");

            return (T)val;
        }
    }
}
