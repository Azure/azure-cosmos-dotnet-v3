namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Linq;

    [TestClass]
    public class EmulatorIssueRepro
    {
        [TestMethod]
        public void Main()
        {
            this.MyAsyncMethod().GetAwaiter().GetResult();
        }

        public class Model
        {
            public string id { get; set; }
            public string name { get; set; }
        }

        public async Task MyAsyncMethod()
        {
            CosmosClientOptions options = new()
            {
                HttpClientFactory = () => new HttpClient(new HttpClientHandler()
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                }),
                ConnectionMode = ConnectionMode.Gateway,
            };

            using CosmosClient client = new(
                accountEndpoint: "https://localhost:8081/",
                authKeyOrResourceToken: "<<AuthKeyHere>>",
                clientOptions: options
            );


            Database database = await client.CreateDatabaseIfNotExistsAsync(
                id: "cosmicworks",
                throughput: 400
            );

            ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync(
                id: "products",
                partitionKeyPath: "/id"
            );

            Container container = containerResponse.Container;

            if (containerResponse.StatusCode == System.Net.HttpStatusCode.Created)
            {
                var i1 = new
                {
                    id = "68719518371",
                    name = "Kiama classic surfboard"
                };

                await container.UpsertItemAsync(i1);
            }

            Console.WriteLine($@"Endpoint : {client.Endpoint}");

            string query = "SELECT * FROM c";
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("-------------------------------");
                Console.WriteLine("RAW Querying without partition key");
                Console.WriteLine("-------------------------------");
                await this.RawQueryItemsAsyc(container, query, new QueryRequestOptions());
            }

            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("-------------------------------");
                Console.WriteLine("RAW Querying with partition key");
                Console.WriteLine("-------------------------------");
                await this.RawQueryItemsAsyc(container, query, new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey("68719518371")
                });
            }

            query = null;
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("-------------------------------");
                Console.WriteLine("RAW Querying without partition key");
                Console.WriteLine("-------------------------------");
                await this.RawQueryItemsAsyc(container, query, new QueryRequestOptions());
            }

            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("-------------------------------");
                Console.WriteLine("RAW Querying with partition key");
                Console.WriteLine("-------------------------------");
                await this.RawQueryItemsAsyc(container, query, new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey("68719518371")
                });
            }

            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("-------------------------------");
                Console.WriteLine("LINQ Querying without partition key");
                Console.WriteLine("-------------------------------");
                await this.LinqQueryItemsAsyc(container, new QueryRequestOptions());
            }

            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("-------------------------------");
                Console.WriteLine("LINQ Querying with partition key");
                Console.WriteLine("-------------------------------");
                await this.LinqQueryItemsAsyc(container, new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey("68719518371")
                });
            }
        }

        private async Task RawQueryItemsAsyc(Container container, string query, QueryRequestOptions requestOptions)
        {
            Console.WriteLine($"Query: {query}");

            try
            {
                var feedIterator = container
                    .GetItemQueryIterator<Model>
                    (
                        queryText: query,
                        requestOptions: requestOptions
                    );
                while (feedIterator.HasMoreResults)
                {
                    var feedResponse = await feedIterator.ReadNextAsync();
                    Console.Write("Item.id : " + feedResponse.FirstOrDefault()?.id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception : {ex}");
            }
        }

        private async Task LinqQueryItemsAsyc(Container container, QueryRequestOptions requestOptions)
        {
            try
            {
                var query = container
                .GetItemLinqQueryable<Model>
                (
                    requestOptions: requestOptions,
                    linqSerializerOptions: new CosmosLinqSerializerOptions()
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                    }
                );
                var feedIterator = query.ToFeedIterator();
                while (feedIterator.HasMoreResults)
                {
                    var feedResponse = await feedIterator.ReadNextAsync();
                    Console.Write("Item.id : " + feedResponse.FirstOrDefault()?.id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception : {ex}");
            }
        }
    }
}
