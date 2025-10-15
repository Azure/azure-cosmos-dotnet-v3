namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
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
        [DataTestMethod]
        [DynamicData(nameof(GetSupportedJsonProcessorsData), DynamicDataSourceType.Method)]
        public void HasMoreResults_DelegatesToInnerIterator(JsonProcessor jsonProcessor)
        {
            Mock<FeedIterator> innerIterator = new Mock<FeedIterator>();
            innerIterator.SetupGet(iterator => iterator.HasMoreResults).Returns(true);

            EncryptionFeedIterator feedIterator = this.CreateFeedIterator(innerIterator.Object, jsonProcessor);

            Assert.IsTrue(feedIterator.HasMoreResults);
            innerIterator.VerifyGet(iterator => iterator.HasMoreResults, Times.Once);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetSupportedJsonProcessorsData), DynamicDataSourceType.Method)]
        public async Task ReadNextAsync_SuccessfulResponse_ReturnsDecryptedResponseMessage(JsonProcessor jsonProcessor)
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
            }, jsonProcessor);

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
        public async Task ReadNextAsync_UnsuccessfulResponse_ReturnsOriginalResponseMessage(JsonProcessor jsonProcessor)
        {
            ResponseMessage response = this.CreateResponseMessage(HttpStatusCode.NotFound, new { message = "not-found" }, jsonProcessor);

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
        public async Task ReadNextAsync_StreamAndNewtonsoftBehaveEquivalentlyAcrossUseCases()
        {
            await VerifySuccessfulScenarioAsync(
                scenarioName: "NoDocuments",
                documents: System.Array.Empty<JObject>());

            await VerifySuccessfulScenarioAsync(
                scenarioName: "MultipleDocuments",
                documents: new[]
                {
                    new JObject
                    {
                        ["id"] = "item-1",
                        ["pk"] = "pk-value",
                        ["value"] = 123,
                    },
                    new JObject
                    {
                        ["id"] = "item-2",
                        ["pk"] = "pk-value-2",
                        ["value"] = 999,
                    },
                    new JObject
                    {
                        ["id"] = "item-3",
                        ["pk"] = "pk-value-3",
                        ["value"] = 0,
                    },
                });

            await VerifyErrorScenarioAsync(HttpStatusCode.BadRequest);

            async Task VerifySuccessfulScenarioAsync(string scenarioName, IReadOnlyList<JObject> documents)
            {
                JObject payload = new()
                {
                    [Constants.DocumentsResourcePropertyName] = new JArray(documents),
                };

                using ResponseMessage newtonsoftResponse = await ExecuteAsync(
                    JsonProcessor.Newtonsoft,
                    () => this.CreateResponseMessage(HttpStatusCode.OK, payload, JsonProcessor.Newtonsoft));

                using ResponseMessage streamResponse = await ExecuteAsync(
                    JsonProcessor.Stream,
                    () => this.CreateResponseMessage(HttpStatusCode.OK, payload, JsonProcessor.Stream));

                JToken newtonsoftPayload = TestCommon.FromStream<JToken>(newtonsoftResponse.Content);
                JToken streamPayload = TestCommon.FromStream<JToken>(streamResponse.Content);

                JObject normalizedNewtonsoft = NormalizePayload(newtonsoftPayload);
                JObject normalizedStream = NormalizePayload(streamPayload);

                Assert.IsTrue(
                    JToken.DeepEquals(normalizedNewtonsoft, normalizedStream),
                    $"{scenarioName}: Stream processor payload differed from Newtonsoft payload.");

                JArray docsArray = ExtractDocuments(normalizedNewtonsoft);
                Assert.IsNotNull(docsArray, $"{scenarioName}: Documents array missing in payload.");
                Assert.AreEqual(documents.Count, docsArray.Count, $"{scenarioName}: Document count mismatch.");
            }

            async Task VerifyErrorScenarioAsync(HttpStatusCode statusCode)
            {
                ResponseMessage newtonsoftResponseMessage = this.CreateResponseMessage(statusCode, new { message = "error" }, JsonProcessor.Newtonsoft);
                ResponseMessage streamResponseMessage = this.CreateResponseMessage(statusCode, new { message = "error" }, JsonProcessor.Stream);

                ResponseMessage newtonsoftResult = await ExecuteAsync(JsonProcessor.Newtonsoft, () => newtonsoftResponseMessage);
                ResponseMessage streamResult = await ExecuteAsync(JsonProcessor.Stream, () => streamResponseMessage);

                Assert.AreSame(newtonsoftResponseMessage, newtonsoftResult, "Newtonsoft processor should return original response on errors.");
                Assert.AreSame(streamResponseMessage, streamResult, "Stream processor should return original response on errors.");

                newtonsoftResponseMessage.Dispose();
                streamResponseMessage.Dispose();
            }

            async Task<ResponseMessage> ExecuteAsync(JsonProcessor processor, System.Func<ResponseMessage> responseFactory)
            {
                Mock<FeedIterator> iterator = new Mock<FeedIterator>();
                iterator
                    .Setup(feed => feed.ReadNextAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => responseFactory());

                EncryptionFeedIterator feedIterator = this.CreateFeedIterator(iterator.Object, processor);
                return await feedIterator.ReadNextAsync();
            }
        }
#endif

        private EncryptionFeedIterator CreateFeedIterator(FeedIterator innerIterator, JsonProcessor jsonProcessor)
        {
            return new EncryptionFeedIterator(innerIterator, new NoOpEncryptor(), jsonProcessor);
        }

        private ResponseMessage CreateResponseMessage(HttpStatusCode statusCode, object payload, JsonProcessor processor)
        {
            ResponseMessage response = new ResponseMessage(statusCode);
            if (payload != null)
            {
                response.Content = this.ToStream(payload, processor);
            }

            return response;
        }

        private Stream ToStream(object payload, JsonProcessor processor)
        {
#if NET8_0_OR_GREATER
            if (processor == JsonProcessor.Stream && payload is JObject obj && obj.TryGetValue(Constants.DocumentsResourcePropertyName, out JToken docsToken) && docsToken is JArray docsArray)
            {
                payload = docsArray;
            }
#endif

            string json = JsonConvert.SerializeObject(payload);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            MemoryStream stream = new MemoryStream(buffer.Length);
            stream.Write(buffer, 0, buffer.Length);
            stream.Position = 0;
            return stream;
        }

        private static JArray ExtractDocuments(JToken payload)
        {
            if (payload is JObject obj)
            {
                JToken documentsToken = obj[Constants.DocumentsResourcePropertyName];
                Assert.IsNotNull(documentsToken, "Feed payload missing Documents array.");
                Assert.IsInstanceOfType(documentsToken, typeof(JArray), "Documents payload expected to be a JSON array.");
                return (JArray)documentsToken;
            }

            if (payload is JArray array)
            {
                return array;
            }

            Assert.Fail($"Unsupported payload type: {payload?.Type}");
            return null;
        }

        private static JObject NormalizePayload(JToken payload)
        {
            if (payload is JObject obj)
            {
                return obj;
            }

            if (payload is JArray array)
            {
                return new JObject
                {
                    [Constants.DocumentsResourcePropertyName] = array,
                };
            }

            Assert.Fail($"Unsupported payload type: {payload?.Type}");
            return null;
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

        private sealed class TestCosmosSerializer : CosmosSerializer
        {
            public override T FromStream<T>(Stream stream)
            {
                using (stream)
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                using (JsonTextReader jsonReader = new JsonTextReader(reader))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    return serializer.Deserialize<T>(jsonReader);
                }
            }

            public override Stream ToStream<T>(T input)
            {
                MemoryStream stream = new MemoryStream();
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true))
                using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(jsonWriter, input);
                    jsonWriter.Flush();
                    writer.Flush();
                }

                stream.Position = 0;
                return stream;
            }
        }

        public static IEnumerable<object[]> GetSupportedJsonProcessorsData()
        {
#if NET8_0_OR_GREATER
            yield return new object[] { JsonProcessor.Stream };
#endif
            yield return new object[] { JsonProcessor.Newtonsoft };
        }

    }
}
