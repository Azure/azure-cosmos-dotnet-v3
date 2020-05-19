namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tests.Json;

    [MemoryDiagnoser]
    public class EndToEnd
    {
        private readonly CosmosClient client;
        private readonly Container container;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemBenchmark"/> class.
        /// </summary>
        public EndToEnd()
        {
            CosmosClientBuilder clientBuilder = new CosmosClientBuilder(
                accountEndpoint: "<YourEndPointHere>",
                authKeyOrResourceToken: "<YourAccountKeyHere>");

            this.client = clientBuilder.Build();
            Database db = this.client.CreateDatabaseIfNotExistsAsync("BenchmarkDB").Result;
            ContainerResponse containerResponse = db.CreateContainerIfNotExistsAsync(
               id: "BenchmarkContainer",
               partitionKeyPath: "/id",
               throughput: 10000).Result;

            this.container = containerResponse;

            if (containerResponse.StatusCode == HttpStatusCode.Created)
            {
                string path = $"TestJsons/NutritionData.json";
                string json = TextFileConcatenation.ReadMultipartFile(path);
                json = JsonTestUtils.RandomSampleJson(json, seed: 42, maxNumberOfItems: 1000);

                CosmosArray cosmosArray = CosmosArray.Parse(json);
                foreach (CosmosElement document in cosmosArray)
                {
                    ItemResponse<CosmosElement> itemResponse = this.container.CreateItemAsync(document).Result;
                }
            }
        }

        [Benchmark]
        public Task Parallel_Text_CosmosElement()
        {
            return QueryWithCosmosElements(
                this.container,
                "SELECT * FROM c",
                JsonSerializationFormat.Text);
        }

        [Benchmark]
        public Task Parallel_Binary_CosmosElement()
        {
            return QueryWithCosmosElements(
                this.container,
                "SELECT * FROM c",
                JsonSerializationFormat.Binary);
        }

        [Benchmark]
        public Task Parallel_Text_TextStream()
        {
            return QueryWithTextStream(
                this.container,
                "SELECT * FROM c",
                JsonSerializationFormat.Text);
        }

        [Benchmark]
        public Task Parallel_Binary_TextStream()
        {
            return QueryWithTextStream(
                this.container,
                "SELECT * FROM c",
                JsonSerializationFormat.Binary);
        }

        [Benchmark]
        public Task OrderBy_Text_CosmosElement()
        {
            return QueryWithCosmosElements(
                this.container,
                "SELECT * FROM c ORDER BY c._ts",
                JsonSerializationFormat.Text);
        }

        [Benchmark]
        public Task OrderBy_Binary_CosmosElement()
        {
            return QueryWithCosmosElements(
                this.container,
                "SELECT * FROM c ORDER BY c._ts",
                JsonSerializationFormat.Binary);
        }

        [Benchmark]
        public Task OrderBy_Text_TextStream()
        {
            return QueryWithTextStream(
                this.container,
                "SELECT * FROM c ORDER BY c._ts",
                JsonSerializationFormat.Text);
        }

        [Benchmark]
        public Task OrderBy_Binary_TextStream()
        {
            return QueryWithTextStream(
                this.container,
                "SELECT * FROM c ORDER BY c._ts",
                JsonSerializationFormat.Binary);
        }

        [Benchmark]
        public Task GroupBy_Text_CosmosElement()
        {
            return QueryWithCosmosElements(
                this.container,
                "SELECT MAX(c.version) FROM c GROUP BY c.id",
                JsonSerializationFormat.Text);
        }

        [Benchmark]
        public Task GroupBy_Binary_CosmosElement()
        {
            return QueryWithCosmosElements(
                this.container,
                "SELECT MAX(c.version) FROM c GROUP BY c.id",
                JsonSerializationFormat.Binary);
        }

        [Benchmark]
        public Task GroupBy_Text_TextStream()
        {
            return QueryWithTextStream(
                this.container,
                "SELECT MAX(c.version) FROM c GROUP BY c.id",
                JsonSerializationFormat.Text);
        }

        [Benchmark]
        public Task GroupBy_Binary_TextStream()
        {
            return QueryWithTextStream(
                this.container,
                "SELECT MAX(c.version) FROM c GROUP BY c.id",
                JsonSerializationFormat.Binary);
        }

        [Benchmark]
        public Task Distinct_Text_CosmosElement()
        {
            return QueryWithCosmosElements(
                this.container,
                "SELECT DISTINCT * FROM c",
                JsonSerializationFormat.Text);
        }

        [Benchmark]
        public Task Distinct_Binary_CosmosElement()
        {
            return QueryWithCosmosElements(
                this.container,
                "SELECT DISTINCT * FROM c",
                JsonSerializationFormat.Binary);
        }

        [Benchmark]
        public Task Distinct_Text_TextStream()
        {
            return QueryWithTextStream(
                this.container,
                "SELECT DISTINCT * FROM c",
                JsonSerializationFormat.Text);
        }

        [Benchmark]
        public Task Distinct_Binary_TextStream()
        {
            return QueryWithTextStream(
                this.container,
                "SELECT DISTINCT * FROM c",
                JsonSerializationFormat.Binary);
        }

        private static async Task QueryWithTextStream(
            Container container,
            string query,
            JsonSerializationFormat jsonSerializationFormat)
        {
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions()
            {
                MaxItemCount = -1,
                MaxBufferedItemCount = -1,
                MaxConcurrency = -1,
            };
            SetSerializationFormat(queryRequestOptions, jsonSerializationFormat);

            FeedIterator feedIterator = container.GetItemQueryStreamIterator(
                queryText: query,
                requestOptions: queryRequestOptions);

            List<Stream> streams = new List<Stream>();

            while (feedIterator.HasMoreResults)
            {
                ResponseMessage responseMessage = await feedIterator.ReadNextAsync();
                streams.Add(responseMessage.Content);
            }
        }

        private static async Task QueryWithCosmosElements(
            Container container,
            string query,
            JsonSerializationFormat jsonSerializationFormat)
        {
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions()
            {
                MaxItemCount = -1,
                MaxBufferedItemCount = -1,
                MaxConcurrency = -1,
            };
            SetSerializationFormat(queryRequestOptions, jsonSerializationFormat);

            FeedIterator feedIterator = container.GetItemQueryStreamIterator(
                queryText: query,
                requestOptions: queryRequestOptions);

            List<CosmosElement> documents = new List<CosmosElement>();

            while (feedIterator.HasMoreResults)
            {
                QueryResponse queryResponse = (QueryResponse)await feedIterator.ReadNextAsync();
                documents.AddRange(queryResponse.CosmosElements);
            }
        }

        private static void SetSerializationFormat(
            QueryRequestOptions queryRequestOptions,
            JsonSerializationFormat jsonSerializationFormat)
        {
            string contentSerializationFormat = jsonSerializationFormat switch
            {
                JsonSerializationFormat.Text => "JsonText",
                JsonSerializationFormat.Binary => "CosmosBinary",
                JsonSerializationFormat.HybridRow => "HybridRow",
                _ => throw new Exception(),
            };

            CosmosSerializationFormatOptions formatOptions = new CosmosSerializationFormatOptions(
                contentSerializationFormat,
                (content) => JsonNavigator.Create(content),
                () => JsonWriter.Create(JsonSerializationFormat.Text));

            queryRequestOptions.CosmosSerializationFormatOptions = formatOptions;
        }
    }
}
