namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using TestCommon = Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestCommon;

    [TestClass]
    [TestCategory("Query")]
    public sealed class SupportedSerializationFormatsTest : QueryTestsBase
    {
        [TestMethod]
        public async Task TestSupportedSerializationFormats()
        {
            string[] inputDocuments = new[]
            {
                @"{""id"":""0"",""name"":""document_0""}",
                @"{""id"":""1"",""name"":""document_1""}",
                @"{""id"":""2"",""name"":""document_2""}",
                @"{""id"":""3"",""name"":""document_3""}",
                @"{""id"":""4"",""name"":""document_4""}",
                @"{""id"":""5"",""name"":""document_5""}",
                @"{""id"":""6"",""name"":""document_6""}",
                @"{""id"":""7"",""name"":""document_7""}",
                @"{""id"":""8"",""name"":""document_8""}",
                @"{""id"":""9"",""name"":""document_9""}",
            };

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                this.TestSupportedSerializationFormatsHelper,
                "/id");
        }

        [TestMethod]
        [DoNotParallelize]
        [DataRow(ConnectionMode.Direct, null)]
        [DataRow(ConnectionMode.Gateway, null)]
        [DataRow(ConnectionMode.Direct, "CosmosBinary")]
        [DataRow(ConnectionMode.Gateway, "CosmosBinary")]
        [DataRow(ConnectionMode.Direct, "JsonText")]
        [DataRow(ConnectionMode.Gateway, "JsonText")]
        public async Task TestSupportedSerializationFeatures(ConnectionMode connectionMode, string formatsOverride)
        {
            string previousBinaryEncoding = Environment.GetEnvironmentVariable(Cosmos.ConfigurationManager.BinaryEncodingEnabled);
            try
            {
                Environment.SetEnvironmentVariable(Cosmos.ConfigurationManager.BinaryEncodingEnabled, bool.FalseString);
                byte[] binary = Enumerable.Range(0, 193).Select(index => (byte)((index * 73 + 19) % 256)).ToArray();
                JObject document = new JObject
                {
                    ["id"] = "binary-features",
                    ["base64"] = Convert.ToBase64String(binary),
                    ["numbers"] = new JArray(Enumerable.Range(1000, 128)),
                    ["uint64"] = ulong.MaxValue,
                };
                SerializationFeaturesHandler handler = new SerializationFeaturesHandler(
                    formatsOverride,
                    (content, operation) => AssertSerializationFeatures(content, operation, formatsOverride, document));
                CosmosClientOptions clientOptions = new CosmosClientOptions
                {
                    ConnectionMode = connectionMode,
                    EnableStreamPassThrough = true,
                };
                clientOptions.CustomHandlers.Add(handler);
                using CosmosClient client = TestCommon.CreateCosmosClient(clientOptions);
                Container createdContainer = await this.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id");
                Container container = client.GetContainer(this.database.Id, createdContainer.Id);
                using (MemoryStream payload = new MemoryStream(Encoding.UTF8.GetBytes(document.ToString())))
                using (ResponseMessage created = await container.CreateItemStreamAsync(payload, new Cosmos.PartitionKey("binary-features")))
                {
                    created.EnsureSuccessStatusCode();
                }

                Environment.SetEnvironmentVariable(Cosmos.ConfigurationManager.BinaryEncodingEnabled, bool.TrueString);
                using (ResponseMessage read = await container.ReadItemStreamAsync(
                    "binary-features",
                    new Cosmos.PartitionKey("binary-features"),
                    new ItemRequestOptions { EnableBinaryResponseOnPointOperations = true }))
                {
                    read.EnsureSuccessStatusCode();
                }

                using (FeedIterator iterator = container.GetItemQueryStreamIterator(
                    "SELECT * FROM c",
                    requestOptions: new QueryRequestOptions { PartitionKey = new Cosmos.PartitionKey("binary-features") }))
                {
                    while (iterator.HasMoreResults)
                    {
                        using ResponseMessage page = await iterator.ReadNextAsync();
                        page.EnsureSuccessStatusCode();
                    }
                }

                Assert.IsTrue(handler.ReadResponses > 0);
                Assert.IsTrue(handler.QueryResponses > 0);
            }
            finally
            {
                Environment.SetEnvironmentVariable(Cosmos.ConfigurationManager.BinaryEncodingEnabled, previousBinaryEncoding);
            }
        }

        private static void AssertSerializationFeatures(
            ReadOnlyMemory<byte> content,
            OperationType operation,
            string formatsOverride,
            JObject expected)
        {
            Assert.IsFalse(content.IsEmpty);
            bool expectBinary = formatsOverride != "JsonText";
            bool expectFeatures = formatsOverride == null;
            Assert.AreEqual(expectBinary, content.Span[0] == (byte)JsonSerializationFormat.Binary);
            string text;
            if (expectBinary)
            {
                int documentOffset = 1;
                if (operation == OperationType.Query)
                {
                    int documentsOffset = GetPropertyOffset(content, documentOffset, "Documents");
                    documentOffset = JsonBinaryEncoding.Enumerator.GetArrayItems(content, documentsOffset, null).Single().Offset;
                }

                byte base64Marker = content.Span[GetPropertyOffset(content, documentOffset, "base64")];
                byte numbersMarker = content.Span[GetPropertyOffset(content, documentOffset, "numbers")];
                byte uint64Marker = content.Span[GetPropertyOffset(content, documentOffset, "uint64")];
                Assert.AreEqual(expectFeatures, JsonBinaryEncoding.TypeMarker.IsBase64String(base64Marker), $"Base64 marker: 0x{base64Marker:X2}");
                Assert.AreEqual(expectFeatures, JsonBinaryEncoding.IsUniformArrayTypeMarker(numbersMarker), $"Array marker: 0x{numbersMarker:X2}");
                Assert.AreEqual(expectFeatures, uint64Marker == JsonBinaryEncoding.TypeMarker.NumberUInt64, $"Number marker: 0x{uint64Marker:X2}");

                IJsonWriter writer = JsonWriter.Create(JsonSerializationFormat.Text);
                JsonReader.Create(content).WriteAll(writer);
                text = Encoding.UTF8.GetString(writer.GetResult().Span);
            }
            else
            {
                text = Encoding.UTF8.GetString(content.Span);
            }

            JObject root = JObject.Parse(text);
            JObject actual = operation == OperationType.Query ? (JObject)root["Documents"].Single() : root;
            Assert.AreEqual((string)expected["base64"], (string)actual["base64"]);
            CollectionAssert.AreEqual(expected["numbers"].Values<int>().ToArray(), actual["numbers"].Values<int>().ToArray());
            if (expectFeatures || !expectBinary)
            {
                Assert.AreEqual(expected["uint64"].ToString(), actual["uint64"].ToString());
            }
        }

        private static int GetPropertyOffset(ReadOnlyMemory<byte> content, int objectOffset, string name)
        {
            return JsonBinaryEncoding.Enumerator.GetObjectProperties(content, objectOffset)
                .Single(property => JsonBinaryEncoding.GetStringValue(content, content.Slice(property.NameOffset), null) == name)
                .ValueOffset;
        }

        private sealed class SerializationFeaturesHandler : RequestHandler
        {
            private readonly string formatsOverride;
            private readonly Action<ReadOnlyMemory<byte>, OperationType> assertResponse;

            public SerializationFeaturesHandler(string formatsOverride, Action<ReadOnlyMemory<byte>, OperationType> assertResponse)
            {
                this.formatsOverride = formatsOverride;
                this.assertResponse = assertResponse;
            }

            public int ReadResponses { get; private set; }

            public int QueryResponses { get; private set; }

            public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                bool inspect = request.ResourceType == ResourceType.Document
                    && (request.OperationType == OperationType.Read || request.OperationType == OperationType.Query);
                if (inspect)
                {
                    StringAssert.Contains(request.Headers.CosmosMessageHeaders.SupportedSerializationFormats, CosmosSerializationUtil.SupportedBinarySerializationFormat);
                    if (this.formatsOverride != null)
                    {
                        request.Headers.CosmosMessageHeaders.SupportedSerializationFormats = this.formatsOverride;
                    }
                }

                ResponseMessage response = await base.SendAsync(request, cancellationToken);
                if (inspect && response.IsSuccessStatusCode)
                {
                    // Inspect the service payload before the query pipeline or point-operation conversion rewrites it.
                    using MemoryStream copy = new MemoryStream();
                    await response.Content.CopyToAsync(copy, cancellationToken);
                    byte[] content = copy.ToArray();
                    response.Content.Dispose();
                    response.Content = new MemoryStream(content);
                    this.assertResponse(content, request.OperationType);
                    if (request.OperationType == OperationType.Read)
                    {
                        this.ReadResponses++;
                    }
                    else
                    {
                        this.QueryResponses++;
                    }
                }

                return response;
            }
        }

        private async Task TestSupportedSerializationFormatsHelper(Container container, IReadOnlyList<CosmosObject> documents)
        {
            List<(Cosmos.PartitionKey?, string[]) > partitionKeyAndExpectedResults = new List<(Cosmos.PartitionKey? partitionKey, string[] documents)>
                {
                    (
                        partitionKey: null,
                        documents: new[] { "document_0", "document_1", "document_2", "document_3", "document_4","document_5", "document_6", "document_7", "document_8", "document_9" }
                    ),
                    (
                        partitionKey: new Cosmos.PartitionKey("0"),
                        documents: new[] { "document_0" }
                    )
                };

            foreach (string query in new string[]
                {
                    null,   // With null query, this turns into a ReadFeed API, for which SupportedSerializationFormats should be ignored.
                    "SELECT * FROM c"
                })
            {
                List<QueryRequestOptions> queryRequestOptionsList = new List<QueryRequestOptions>()
                {
                    new QueryRequestOptions()
                    {
                        SupportedSerializationFormats = SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow
                    },
                    new QueryRequestOptions()
                    {
                        SupportedSerializationFormats = SupportedSerializationFormats.JsonText | SupportedSerializationFormats.CosmosBinary
                    },
                    new QueryRequestOptions()
                    {
                        SupportedSerializationFormats = SupportedSerializationFormats.JsonText | SupportedSerializationFormats.HybridRow
                    },
                    new QueryRequestOptions()
                    {
                        SupportedSerializationFormats = SupportedSerializationFormats.JsonText | SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow
                    },
                    new QueryRequestOptions()
                    {
                        SupportedSerializationFormats = SupportedSerializationFormats.JsonText
                    },
                    new QueryRequestOptions()
                    {
                        SupportedSerializationFormats = SupportedSerializationFormats.CosmosBinary
                    },
                    new QueryRequestOptions()
                    {
                        SupportedSerializationFormats = SupportedSerializationFormats.CosmosBinary
                    },
                    new QueryRequestOptions()
                    {
                        SupportedSerializationFormats = SupportedSerializationFormats.JsonText
                    }
                };

                // GetItemQueryIterator
                foreach (QueryRequestOptions requestOptions in queryRequestOptionsList)
                {
                    QueryDefinition queryDefinition = query != null ? new QueryDefinition(query) : null;
                    foreach ((Cosmos.PartitionKey? partitionKey, string[] expectedResults) in partitionKeyAndExpectedResults)
                    {
                        requestOptions.PartitionKey = partitionKey;

                        List<JObject> queryResults = new List<JObject>();
                        using (FeedIterator<JObject> feedIterator = container.GetItemQueryIterator<JObject>(queryDefinition, requestOptions: requestOptions))
                        {
                            while (feedIterator.HasMoreResults)
                            {
                                FeedResponse<JObject> response = await feedIterator.ReadNextAsync();
                                queryResults.AddRange(response.ToList());
                            }
                        }

                        string[] actualResults = queryResults
                            .Select(doc => doc["name"].ToString())
                            .ToArray();

                        CollectionAssert.AreEquivalent(expectedResults, actualResults);
                    }
                }

                // GetItemQueryStreamIterator
                foreach (QueryRequestOptions requestOptions in queryRequestOptionsList)
                {
                    QueryDefinition queryDefinition = query != null ? new QueryDefinition(query) : null;
                    foreach ((Cosmos.PartitionKey? partitionKey, string[] expectedResults) in partitionKeyAndExpectedResults)
                    {
                        requestOptions.PartitionKey = partitionKey;

                        List<CosmosElement> queryResults = new List<CosmosElement>();
                        using (FeedIterator feedIterator = container.GetItemQueryStreamIterator(queryDefinition, requestOptions: requestOptions))
                        {
                            while (feedIterator.HasMoreResults)
                            {
                                ResponseMessage response = await feedIterator.ReadNextAsync();
                                queryResults.AddRange(Deserialize(response.Content));
                            }
                        }

                        string[] actualResults = queryResults
                            .Select(doc => ((CosmosString)((CosmosObject)doc)["name"]).Value.ToString())
                            .ToArray();

                        CollectionAssert.AreEquivalent(expectedResults, actualResults);
                    }
                }
            }
        }

        private static IEnumerable<CosmosElement> Deserialize(Stream content)
        {
            string contentAsString = new StreamReader(content).ReadToEnd();
            CosmosObject obj = CosmosObject.Parse(contentAsString);
            foreach (CosmosElement element in (CosmosArray)obj["Documents"])
            {
                yield return element;
            }
        }
    }
}