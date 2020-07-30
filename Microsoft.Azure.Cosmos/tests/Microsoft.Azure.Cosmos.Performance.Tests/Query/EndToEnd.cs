namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Reflection;
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
        private static class Queries
        {
            public static readonly Query Parallel = new Query("Parallel", "SELECT * FROM c");
            public static readonly Query OrderBy = new Query("ORDER BY", "SELECT * FROM c ORDER BY c._ts");
            public static readonly Query GroupBy = new Query("GROUP BY", "SELECT MAX(c.version) FROM c GROUP BY c.id");
            public static readonly Query Distinct = new Query("Distinct", "SELECT DISTINCT * FROM c");
        }

        public sealed class Query
        {
            public Query(string description, string text)
            {
                this.Description = description;
                this.Text = text;
            }

            public string Text { get; }
            public string Description { get; }

            public override string ToString()
            {
                return this.Description;
            }
        }

        public enum SerializationFormat
        {
            Text,
            Binary,
        }

        public enum QueryType
        {
            CosmosElement,
            TextStream,
        }

        private readonly string accountEndpoint = string.Empty; // insert your endpoint here.
        private readonly string accountKey = string.Empty; // insert your key here.
        private readonly CosmosClient client;
        private readonly Container container;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemBenchmark"/> class.
        /// </summary>
        public EndToEnd()
        {
            if (string.IsNullOrEmpty(this.accountEndpoint) && string.IsNullOrEmpty(this.accountKey))
            {
                return;
            }

            CosmosClientBuilder clientBuilder = new CosmosClientBuilder(
                accountEndpoint: this.accountEndpoint,
                authKeyOrResourceToken: this.accountKey);

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
        public async Task ReadFeedBaselineAsync()
        {
            FeedIterator resultIterator = this.container.GetItemQueryStreamIterator(
                requestOptions: new QueryRequestOptions()
                {
                    MaxItemCount = -1,
                    MaxConcurrency = -1,
                    MaxBufferedItemCount = -1
                });

            while (resultIterator.HasMoreResults)
            {
                using (ResponseMessage response = await resultIterator.ReadNextAsync())
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        [Benchmark]
        public async Task ChangeFeedBaselineAsync()
        {
            ChangeFeedIteratorCore feedIterator = ((ContainerCore)this.container)
                .GetChangeFeedStreamIterator(
                    changeFeedRequestOptions: new ChangeFeedRequestOptions()
                    {
                        From = ChangeFeedRequestOptions.StartFrom.CreateFromBeginning(),
                    }) as ChangeFeedIteratorCore;

            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage = await feedIterator.ReadNextAsync())
                {
                }
            }
        }

        [Benchmark]
        [ArgumentsSource(nameof(Data))]
        public Task RunBenchmark(Query query, QueryType queryType, SerializationFormat serializationFormat)
        {
            JsonSerializationFormat jsonSerializationFormat = serializationFormat switch
            {
                SerializationFormat.Text => JsonSerializationFormat.Text,
                SerializationFormat.Binary => JsonSerializationFormat.Binary,
                _ => throw new ArgumentOutOfRangeException(nameof(serializationFormat)),
            };

            Func<Container, string, JsonSerializationFormat, Task> func = queryType switch
            {
                QueryType.CosmosElement => QueryWithCosmosElements,
                QueryType.TextStream => QueryWithTextStream,
                _ => throw new ArgumentOutOfRangeException(nameof(queryType)),
            };

            return func(this.container, query.Text, jsonSerializationFormat);
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

        public IEnumerable<object[]> Data()
        {
            foreach (FieldInfo fieldInfo in typeof(Queries).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Query query = (Query)fieldInfo.GetValue(null);
                foreach (QueryType queryType in Enum.GetValues(typeof(QueryType)))
                {
                    foreach (SerializationFormat serializationFormat in Enum.GetValues(typeof(SerializationFormat)))
                    {
                        yield return new object[] { query, queryType, serializationFormat };
                    }
                }
            }
        }
    }
}
