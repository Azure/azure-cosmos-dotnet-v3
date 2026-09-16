namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionFeedIteratorTests
    {
        // String-based processor identifiers mirror what an external caller would set on the
        // RequestOptions property bag. Canonical key constant comes from the product code.
        private const string JsonProcessorPropertyBagKey = JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey;
        private const string NewtonsoftProcessorName = "Newtonsoft";
#if NET8_0_OR_GREATER
        private const string StreamProcessorName = "Stream";
#endif

        [DataTestMethod]
        [DynamicData(nameof(GetSupportedJsonProcessorsData), DynamicDataSourceType.Method)]
        public void HasMoreResults_DelegatesToInnerIterator(string jsonProcessor)
        {
            Mock<FeedIterator> innerIterator = new Mock<FeedIterator>();
            innerIterator.SetupGet(iterator => iterator.HasMoreResults).Returns(true);

            EncryptionFeedIterator feedIterator = this.CreateFeedIterator(innerIterator.Object, jsonProcessor);

            Assert.IsTrue(feedIterator.HasMoreResults);
            innerIterator.VerifyGet(iterator => iterator.HasMoreResults, Times.Once);
        }

        [TestMethod]
        public void Constructor_AllowsNullRequestOptions()
        {
            Mock<FeedIterator> innerIterator = new Mock<FeedIterator>();

            EncryptionFeedIterator feedIterator = new EncryptionFeedIterator(innerIterator.Object, new NoOpEncryptor(), requestOptions: null);

            Assert.IsNotNull(feedIterator);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetSupportedJsonProcessorsData), DynamicDataSourceType.Method)]
        public async Task ReadNextAsync_SuccessfulResponse_ReturnsDecryptedResponseMessage(string jsonProcessor)
        {
            JObject firstDocument = new ()
            {
                ["id"] = "test-id",
                ["pk"] = "test-pk",
                ["value"] = 42,
            };

            ResponseMessage response = this.CreateResponseMessage(HttpStatusCode.OK, new JObject
            {
                [Constants.DocumentsResourcePropertyName] = new JArray(firstDocument),
            });

            Mock<FeedIterator> innerIterator = new Mock<FeedIterator>();
            innerIterator
                .Setup(iterator => iterator.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            EncryptionFeedIterator feedIterator = this.CreateFeedIterator(innerIterator.Object, jsonProcessor);

            ResponseMessage decrypted = await feedIterator.ReadNextAsync();

            Assert.IsInstanceOfType(decrypted, typeof(DecryptedResponseMessage));

            JToken payload = TestCommon.FromStream<JToken>(decrypted.Content);
            JArray documents = ExtractDocuments(payload);

            Assert.AreEqual(1, documents.Count);
            Assert.AreEqual(firstDocument["id"], documents[0]["id"]);
            Assert.AreEqual(firstDocument["pk"], documents[0]["pk"]);
            Assert.AreEqual(firstDocument["value"], documents[0]["value"]);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetSupportedJsonProcessorsData), DynamicDataSourceType.Method)]
        public async Task ReadNextAsync_UnsuccessfulResponse_ReturnsOriginalResponseMessage(string jsonProcessor)
        {
            ResponseMessage response = this.CreateResponseMessage(HttpStatusCode.NotFound, new { message = "not-found" });

            Mock<FeedIterator> innerIterator = new Mock<FeedIterator>();
            innerIterator
                .Setup(iterator => iterator.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            EncryptionFeedIterator feedIterator = this.CreateFeedIterator(innerIterator.Object, jsonProcessor);

            ResponseMessage result = await feedIterator.ReadNextAsync();

            Assert.AreSame(response, result);
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task ReadNextAsync_EmptyDocumentsArray_StreamAndNewtonsoftAgree()
        {
            JObject payload = new()
            {
                [Constants.DocumentsResourcePropertyName] = new JArray(),
            };

            using ResponseMessage newtonsoftResponse = await ExecuteAsync(NewtonsoftProcessorName);
            using ResponseMessage streamResponse = await ExecuteAsync(StreamProcessorName);

            JToken newtonsoftPayload = TestCommon.FromStream<JToken>(newtonsoftResponse.Content);
            JToken streamPayload = TestCommon.FromStream<JToken>(streamResponse.Content);

            Assert.IsTrue(
                JToken.DeepEquals(newtonsoftPayload, streamPayload),
                "Stream processor payload differed from Newtonsoft for empty Documents array.");

            JArray docsArray = ExtractDocuments(newtonsoftPayload);
            Assert.AreEqual(0, docsArray.Count);

            async Task<ResponseMessage> ExecuteAsync(string processorName)
            {
                Mock<FeedIterator> iterator = new();
                iterator
                    .Setup(feed => feed.ReadNextAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => this.CreateResponseMessage(HttpStatusCode.OK, payload));

                EncryptionFeedIterator feedIterator = this.CreateFeedIterator(iterator.Object, processorName);
                return await feedIterator.ReadNextAsync();
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(GetDocumentShapeData), DynamicDataSourceType.Method)]
        public async Task ReadNextAsync_VariousDocumentShapes_StreamAndNewtonsoftAgree(string scenarioName, JObject document)
        {
            JObject feedPayload = new()
            {
                ["_rid"] = "rid==",
                [Constants.DocumentsResourcePropertyName] = new JArray(document),
                ["_count"] = 1,
            };

            using ResponseMessage newtonsoftResponse = await ExecuteAsync(NewtonsoftProcessorName, feedPayload);
            using ResponseMessage streamResponse = await ExecuteAsync(StreamProcessorName, feedPayload);

            JToken newtonsoftPayload = TestCommon.FromStream<JToken>(newtonsoftResponse.Content);
            JToken streamPayload = TestCommon.FromStream<JToken>(streamResponse.Content);

            Assert.IsTrue(
                JToken.DeepEquals(newtonsoftPayload, streamPayload),
                $"{scenarioName}: Stream payload differed from Newtonsoft.\nNewtonsoft:\n{newtonsoftPayload}\nStream:\n{streamPayload}");

            async Task<ResponseMessage> ExecuteAsync(string processorName, JObject payload)
            {
                Mock<FeedIterator> iterator = new();
                iterator
                    .Setup(feed => feed.ReadNextAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => this.CreateResponseMessage(HttpStatusCode.OK, payload));

                EncryptionFeedIterator feedIterator = this.CreateFeedIterator(iterator.Object, processorName);
                return await feedIterator.ReadNextAsync();
            }
        }

        [TestMethod]
        public async Task ReadNextAsync_LargeFeed_StreamAndNewtonsoftAgree()
        {
            const int documentCount = 100;
            JArray documents = new();
            for (int i = 0; i < documentCount; i++)
            {
                documents.Add(new JObject
                {
                    ["id"] = $"doc-{i}",
                    ["index"] = i,
                    ["pk"] = $"pk-{i % 10}",
                    ["payload"] = new string('x', 100),
                });
            }

            JObject feedPayload = new()
            {
                ["_rid"] = "rid==",
                [Constants.DocumentsResourcePropertyName] = documents,
                ["_count"] = documentCount,
            };

            using ResponseMessage newtonsoftResponse = await ExecuteAsync(NewtonsoftProcessorName);
            using ResponseMessage streamResponse = await ExecuteAsync(StreamProcessorName);

            JToken newtonsoftPayload = TestCommon.FromStream<JToken>(newtonsoftResponse.Content);
            JToken streamPayload = TestCommon.FromStream<JToken>(streamResponse.Content);

            JArray newtonsoftDocs = ExtractDocuments(newtonsoftPayload);
            JArray streamDocs = ExtractDocuments(streamPayload);

            Assert.AreEqual(documentCount, newtonsoftDocs.Count, "Newtonsoft document count mismatch.");
            Assert.AreEqual(documentCount, streamDocs.Count, "Stream document count mismatch.");
            Assert.IsTrue(
                JToken.DeepEquals(newtonsoftPayload, streamPayload),
                "Stream payload differed from Newtonsoft for large feed.");

            async Task<ResponseMessage> ExecuteAsync(string processorName)
            {
                Mock<FeedIterator> iterator = new();
                iterator
                    .Setup(feed => feed.ReadNextAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => this.CreateResponseMessage(HttpStatusCode.OK, feedPayload));

                EncryptionFeedIterator feedIterator = this.CreateFeedIterator(iterator.Object, processorName);
                return await feedIterator.ReadNextAsync();
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(GetMalformedPayloadData), DynamicDataSourceType.Method)]
        public async Task ReadNextAsync_MalformedPayload_BothProcessorsThrow(string scenarioName, string rawPayload)
        {
            await AssertProcessorThrowsAsync(NewtonsoftProcessorName, rawPayload, scenarioName);
            await AssertProcessorThrowsAsync(StreamProcessorName, rawPayload, scenarioName);

            async Task AssertProcessorThrowsAsync(string processorName, string raw, string name)
            {
                Mock<FeedIterator> iterator = new();
                iterator
                    .Setup(feed => feed.ReadNextAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => CreateRawResponse(HttpStatusCode.OK, raw));

                EncryptionFeedIterator feedIterator = this.CreateFeedIterator(iterator.Object, processorName);

                Exception caught = null;
                try
                {
                    using ResponseMessage _ = await feedIterator.ReadNextAsync();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }

                Assert.IsNotNull(
                    caught,
                    $"{name} ({processorName}): expected an exception for malformed payload but ReadNextAsync returned successfully.");
            }
        }

        // so the parsed JTokens are equal.
        [DataTestMethod]
        [DataRow("DocumentsContainsPrimitive", "{\"Documents\":[42]}")]
        [DataRow("DocumentsContainsString", "{\"Documents\":[\"x\"]}")]
        [DataRow("DocumentsContainsNull", "{\"Documents\":[null]}")]
        [DataRow("DocumentsContainsMixed", "{\"Documents\":[42,{\"id\":\"a\"},\"str\",{\"id\":\"b\"},null]}")]
        [DataRow("DocumentsContainsNestedArray", "{\"Documents\":[[1,2,3],{\"id\":\"a\"}]}")]
        [DataRow("DuplicateDocumentsEmptyFirst", "{\"Documents\":[],\"Documents\":[{\"id\":\"y\"}]}")]
        [DataRow("DuplicateDocumentsBothNonEmpty", "{\"Documents\":[{\"id\":\"x\"}],\"Documents\":[{\"id\":\"y\"}]}")]
        [DataRow("TripleDocuments", "{\"Documents\":[],\"Documents\":[],\"Documents\":[{\"id\":\"z\"}]}")]
        public async Task ReadNextAsync_StreamAndNewtonsoftAgreeOnRawPayload(string scenarioName, string rawPayload)
        {
            using ResponseMessage newtonsoftResponse = await ExecuteAsync(NewtonsoftProcessorName);
            using ResponseMessage streamResponse = await ExecuteAsync(StreamProcessorName);

            JToken newtonsoftPayload = TestCommon.FromStream<JToken>(newtonsoftResponse.Content);
            JToken streamPayload = TestCommon.FromStream<JToken>(streamResponse.Content);

            Assert.IsTrue(
                JToken.DeepEquals(newtonsoftPayload, streamPayload),
                $"{scenarioName}: Stream payload differed from Newtonsoft.\nNewtonsoft:\n{newtonsoftPayload}\nStream:\n{streamPayload}");

            async Task<ResponseMessage> ExecuteAsync(string processorName)
            {
                Mock<FeedIterator> iterator = new();
                iterator
                    .Setup(feed => feed.ReadNextAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => CreateRawResponse(HttpStatusCode.OK, rawPayload));

                EncryptionFeedIterator feedIterator = this.CreateFeedIterator(iterator.Object, processorName);
                return await feedIterator.ReadNextAsync();
            }
        }

        public static IEnumerable<object[]> GetDocumentShapeData()
        {
            yield return new object[]
            {
                "DeeplyNested",
                new JObject
                {
                    ["id"] = "n1",
                    ["pk"] = "pk-n",
                    ["nested"] = new JObject
                    {
                        ["a"] = 1,
                        ["b"] = new JObject
                        {
                            ["c"] = new JObject { ["d"] = "deep" },
                        },
                    },
                },
            };

            yield return new object[]
            {
                "ArrayValues",
                new JObject
                {
                    ["id"] = "a1",
                    ["pk"] = "pk-a",
                    ["tags"] = new JArray("x", "y", "z"),
                    ["mixed"] = new JArray(1, "two", true, JValue.CreateNull()),
                    ["nestedObjects"] = new JArray(
                        new JObject { ["a"] = 1 },
                        new JObject { ["b"] = 2 }),
                },
            };

            yield return new object[]
            {
                "AllPrimitives",
                new JObject
                {
                    ["id"] = "p1",
                    ["pk"] = "pk-p",
                    ["intval"] = 42,
                    ["longval"] = 9223372036854775000L,
                    ["doubleval"] = 3.14,
                    ["boolTrue"] = true,
                    ["boolFalse"] = false,
                    ["nullval"] = JValue.CreateNull(),
                    ["stringval"] = "hello",
                    ["empty"] = string.Empty,
                },
            };

            yield return new object[]
            {
                "Unicode",
                new JObject
                {
                    ["id"] = "ünïcödé-ÜÑ",
                    ["pk"] = "pk-u",
                    ["greeting"] = "café 日本語 🚀",
                },
            };

            yield return new object[]
            {
                "EmptyChildren",
                new JObject
                {
                    ["id"] = "e1",
                    ["pk"] = "pk-e",
                    ["emptyObject"] = new JObject(),
                    ["emptyArray"] = new JArray(),
                },
            };

            yield return new object[]
            {
                "EscapedStrings",
                new JObject
                {
                    ["id"] = "esc",
                    ["pk"] = "pk-esc",
                    ["with-special"] = "line1\nline2\ttab\"quote\\backslash",
                    ["with-control"] = "",
                },
            };
        }

        public static IEnumerable<object[]> GetMalformedPayloadData()
        {
            yield return new object[] { "BareArray", "[{\"id\":\"x\"}]" };
            yield return new object[] { "BareNumber", "42" };
            yield return new object[] { "BareString", "\"hi\"" };
            yield return new object[] { "BareNull", "null" };
            yield return new object[] { "BareBoolean", "true" };
            yield return new object[] { "EmptyObject", "{}" };
            yield return new object[] { "NoDocumentsProperty", "{\"_count\":0,\"_rid\":\"x\"}" };
            yield return new object[] { "DocumentsIsNull", "{\"Documents\":null}" };
            yield return new object[] { "DocumentsIsNumber", "{\"Documents\":42}" };
            yield return new object[] { "DocumentsIsString", "{\"Documents\":\"x\"}" };
            yield return new object[] { "TruncatedAfterOpen", "{\"Documents\":[" };
            yield return new object[] { "EmptyContent", string.Empty };
        }

        private static ResponseMessage CreateRawResponse(HttpStatusCode statusCode, string rawJson)
        {
            ResponseMessage response = new(statusCode);
            byte[] buffer = Encoding.UTF8.GetBytes(rawJson);
            MemoryStream stream = new(buffer.Length);
            if (buffer.Length > 0)
            {
                stream.Write(buffer, 0, buffer.Length);
                stream.Position = 0;
            }

            response.Content = stream;
            return response;
        }

#endif

        private EncryptionFeedIterator CreateFeedIterator(FeedIterator innerIterator, string jsonProcessor)
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorPropertyBagKey, jsonProcessor },
                },
            };

            return new EncryptionFeedIterator(innerIterator, new NoOpEncryptor(), requestOptions);
        }

        private ResponseMessage CreateResponseMessage(HttpStatusCode statusCode, object payload)
        {
            ResponseMessage response = new ResponseMessage(statusCode);
            if (payload != null)
            {
                response.Content = this.ToStream(payload);
            }

            return response;
        }

        private Stream ToStream(object payload)
        {
            string json = JsonConvert.SerializeObject(payload);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            MemoryStream stream = new MemoryStream(buffer.Length);
            stream.Write(buffer, 0, buffer.Length);
            stream.Position = 0;
            return stream;
        }

        private static JArray ExtractDocuments(JToken payload)
        {
            Assert.IsInstanceOfType(payload, typeof(JObject), "Feed payload expected to be a JSON object with a Documents array.");

            JObject obj = (JObject)payload;
            JToken documentsToken = obj[Constants.DocumentsResourcePropertyName];
            Assert.IsNotNull(documentsToken, "Feed payload missing Documents array.");
            Assert.IsInstanceOfType(documentsToken, typeof(JArray), "Documents payload expected to be a JSON array.");
            return (JArray)documentsToken;
        }

        private sealed class NoOpEncryptor : Encryptor
        {
            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<DataEncryptionKey>(null);
            }

            public override Task<byte[]> EncryptAsync(byte[] plainText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(plainText);
            }

            public override Task<byte[]> DecryptAsync(byte[] cipherText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(cipherText);
            }
        }

        public static IEnumerable<object[]> GetSupportedJsonProcessorsData()
        {
#if NET8_0_OR_GREATER
            yield return new object[] { StreamProcessorName };
#endif
            yield return new object[] { NewtonsoftProcessorName };
        }

    }
}
