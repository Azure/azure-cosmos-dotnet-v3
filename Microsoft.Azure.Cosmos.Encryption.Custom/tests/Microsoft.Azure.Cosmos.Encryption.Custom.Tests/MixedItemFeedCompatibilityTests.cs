//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class MixedItemFeedCompatibilityTests
    {
        private const string DekId = "mixedFixtureDek";
        private const string PartitionKeyValue = "mixed-pk";
        private const string PlaintextFixture =
            "{\"id\":\"mixed-1\",\"PK\":\"mixed-pk\",\"Sensitive\":\"exact secret\",\"HighPrecision\":9007199254740993,\"TrailingZero\":123.0,\"Exponent\":6.02E+23}";

        // These documents were produced once by the production encryption processors with the
        // fixed 01..20 key below. Keeping their bytes pinned prevents a writer and reader from
        // drifting together and hiding a wire-compatibility regression.
        private const string LegacyAeadFixtureBase64 =
            "eyJpZCI6Im1peGVkLTEiLCJQSyI6Im1peGVkLXBrIiwiSGlnaFByZWNpc2lvbiI6OTAwNzE5OTI1NDc0MDk5MywiVHJhaWxpbmdaZXJvIjoxMjMuMCwiRXhwb25lbnQiOjYuMDJFKzIzLCJfZWkiOnsiX2VmIjoyLCJfZW4iOiJtaXhlZEZpeHR1cmVEZWsiLCJfZWEiOiJBRUFlczI1NkNiY0htYWNTaGEyNTZSYW5kb21pemVkIiwiX2VkIjoiQVY3TVNFd2RuSE1SQS92bzVocEtpWjM2L3o0cExRN3RuQ2tqdDlIUzRFMzZwQm40Y0NLZm1MVzRWNUhkMFJBc2NBRFFUV1NueC9HRlBHOFdjMTVqZ1RoODNKUTJ4emMxbDBrSW05OW56MUlMIiwiX2VwIjpbIi9TZW5zaXRpdmUiXX19";
        private const string MdeNewtonsoftFixtureBase64 =
            "eyJpZCI6Im1peGVkLTEiLCJQSyI6Im1peGVkLXBrIiwiU2Vuc2l0aXZlIjoiQWdFeDBZS2ZoemZ0eW9vSFpNR0pZWDgwRXpJTkRpVWRiR3NnanpsbHRvd1JHU2lRTFpLQzlGN01hMDJCaDlsYm5UZ1hVOTUyMWJxNlZmWjRlVmFqeE56bCIsIkhpZ2hQcmVjaXNpb24iOjkwMDcxOTkyNTQ3NDA5OTMsIlRyYWlsaW5nWmVybyI6MTIzLjAsIkV4cG9uZW50Ijo2LjAyRSsyMywiX2VpIjp7Il9lZiI6MywiX2VuIjoibWl4ZWRGaXh0dXJlRGVrIiwiX2VhIjoiTWRlQWVhZEFlczI1NkNiY0htYWMyNTZSYW5kb21pemVkIiwiX2VkIjpudWxsLCJfZXAiOlsiL1NlbnNpdGl2ZSJdfX0=";
        private const string MdeStreamFixtureBase64 =
            "eyJpZCI6Im1peGVkLTEiLCJQSyI6Im1peGVkLXBrIiwiU2Vuc2l0aXZlIjoiQWdGWTZCQlVvVjdzRHlLY0l4UUJzS1dnSmdodk81OHdLUy9VajRXY0VFdXlyT2dRMXZwbmh2V1RZdXFxTUdYREQ1MmdSL0h4ZzdlaTBTVW1QNS9WTWlEaCIsIkhpZ2hQcmVjaXNpb24iOjkwMDcxOTkyNTQ3NDA5OTMsIlRyYWlsaW5nWmVybyI6MTIzLjAsIkV4cG9uZW50Ijo2LjAyRSsyMywiX2VpIjp7Il9lZiI6MywiX2VuIjoibWl4ZWRGaXh0dXJlRGVrIiwiX2VhIjoiTWRlQWVhZEFlczI1NkNiY0htYWMyNTZSYW5kb21pemVkIiwiX2VkIjpudWxsLCJfZXAiOlsiL1NlbnNpdGl2ZSJdfX0=";

        [DataTestMethod]
        [DynamicData(nameof(GetReaderProcessors), DynamicDataSourceType.Method)]
        public async Task QueryTypedAndStreamPages_DecryptHeterogeneousContainer(string processorName)
        {
            CompatibilityFixture[] fixtures = CreateFixtures();
            ContainerHarness harness = CreateHarness(fixtures);
            QueryRequestOptions options = CreateQueryOptions(processorName);

            FeedResponse<FixtureDocument> typedPage = await harness.Container
                .GetItemQueryIterator<FixtureDocument>(
                    new QueryDefinition("SELECT * FROM c ORDER BY c.id"),
                    requestOptions: options)
                .ReadNextAsync();

            AssertMatrix(typedPage.Resource, fixtures);

            using ResponseMessage streamPage = await harness.Container
                .GetItemQueryStreamIterator(
                    new QueryDefinition("SELECT * FROM c ORDER BY c.id"),
                    requestOptions: CreateQueryOptions(processorName))
                .ReadNextAsync();

            AssertMatrix(ReadFeedDocuments(streamPage.Content), fixtures);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetReaderProcessors), DynamicDataSourceType.Method)]
        public async Task ReadManyTypedAndStreamResponses_DecryptHeterogeneousContainer(string processorName)
        {
            CompatibilityFixture[] fixtures = CreateFixtures();
            ContainerHarness harness = CreateHarness(fixtures);
            IReadOnlyList<(string id, PartitionKey partitionKey)> items = fixtures
                .Select(fixture => (fixture.Id, new PartitionKey(PartitionKeyValue)))
                .ToArray();

            FeedResponse<FixtureDocument> typedResponse = await harness.Container.ReadManyItemsAsync<FixtureDocument>(
                items,
                CreateReadManyOptions(processorName));
            AssertMatrix(typedResponse.Resource, fixtures);

            using ResponseMessage streamResponse = await harness.Container.ReadManyItemsStreamAsync(
                items,
                CreateReadManyOptions(processorName));
            AssertMatrix(ReadFeedDocuments(streamResponse.Content), fixtures);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetReaderProcessors), DynamicDataSourceType.Method)]
        public async Task QueryLazyDecryptableItems_DecryptHeterogeneousContainer(string processorName)
        {
            CompatibilityFixture[] fixtures = CreateFixtures();
            ContainerHarness harness = CreateHarness(fixtures);
            FeedResponse<DecryptableItem> page = await harness.Container
                .GetItemQueryIterator<DecryptableItem>(
                    new QueryDefinition("SELECT * FROM c ORDER BY c.id"),
                    requestOptions: CreateQueryOptions(processorName))
                .ReadNextAsync();

            try
            {
                List<FixtureDocument> materialized = new (fixtures.Length);
                foreach (DecryptableItem item in page.Resource)
                {
                    (FixtureDocument document, DecryptionContext _) = await item.GetItemAsync<FixtureDocument>();
                    materialized.Add(document);
                }

                AssertMatrix(materialized, fixtures);
            }
            finally
            {
                if (page is IAsyncDisposable disposable)
                {
                    await disposable.DisposeAsync();
                }
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(GetReaderProcessors), DynamicDataSourceType.Method)]
        public async Task ChangeFeedStreamProcessor_DecryptsHeterogeneousContainer(string processorName)
        {
            CompatibilityFixture[] fixtures = CreateFixtures();
            ContainerHarness harness = CreateHarness(fixtures);
#if NET8_0_OR_GREATER
            if (string.Equals(processorName, JsonProcessor.Stream.ToString(), StringComparison.Ordinal))
            {
                harness.Container.UseStreamingJsonProcessingByDefault();
            }
#endif

            Container.ChangeFeedStreamHandler capturedHandler = null;
            harness.Inner
                .Setup(container => container.GetChangeFeedProcessorBuilder(
                    "mixed-processor",
                    It.IsAny<Container.ChangeFeedStreamHandler>()))
                .Callback<string, Container.ChangeFeedStreamHandler>((_, handler) => capturedHandler = handler)
                .Returns((ChangeFeedProcessorBuilder)null);

            IReadOnlyList<FixtureDocument> delivered = null;
            harness.Container.GetChangeFeedProcessorBuilder(
                "mixed-processor",
                (context, changes, cancellationToken) =>
                {
                    delivered = ReadFeedDocuments(changes);
                    return Task.CompletedTask;
                });

            Assert.IsNotNull(capturedHandler);
            using MemoryStream changeFeed = CreateFeedStream(fixtures);
            await capturedHandler(Mock.Of<ChangeFeedProcessorContext>(), changeFeed, CancellationToken.None);

            AssertMatrix(delivered, fixtures);
        }

#if NET8_0_OR_GREATER
        [DataTestMethod]
        [DataRow("mde-newtonsoft", "Newtonsoft", "Stream")]
        [DataRow("mde-stream", "Stream", "Newtonsoft")]
        public async Task MaterializedMdeDocument_RewritesWithOppositeProcessor_AndRereadsWithBoth(
            string sourceFixtureName,
            string sourceProcessorName,
            string targetProcessorName)
        {
            CompatibilityFixture sourceFixture = CreateFixtures()
                .Single(fixture => string.Equals(fixture.Name, sourceFixtureName, StringComparison.Ordinal));
            FixedKeyEncryptor encryptor = new ();
            FixtureCosmosSerializer serializer = new ();
            FixtureDocument materialized = await MaterializeAsync(
                sourceFixture.RawStoredJson,
                sourceProcessorName,
                encryptor,
                serializer);
            AssertPlaintext(materialized, sourceFixture);

            ContainerHarness harness = CreateHarness(Array.Empty<CompatibilityFixture>(), encryptor, serializer);
            string storedRewrite = null;
            harness.Inner
                .Setup(container => container.ReplaceItemStreamAsync(
                    It.IsAny<Stream>(),
                    sourceFixture.Id,
                    new PartitionKey(PartitionKeyValue),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns<Stream, string, PartitionKey, ItemRequestOptions, CancellationToken>(
                    async (content, _, _, _, _) =>
                    {
                        storedRewrite = await ReadToEndAsync(content);
                        return CreateOkResponse(storedRewrite);
                    });

            using ResponseMessage rewriteResponse = await harness.Container.ReplaceItemStreamAsync(
                CreateStream(materialized.RawJson),
                sourceFixture.Id,
                new PartitionKey(PartitionKeyValue),
                CreateEncryptedWriteOptions(targetProcessorName));

            AssertPlaintext(new FixtureDocument(await ReadToEndAsync(rewriteResponse.Content)), sourceFixture);
            AssertMdeV3Storage(storedRewrite, sourceFixture);

            harness.Inner
                .Setup(container => container.ReadItemStreamAsync(
                    sourceFixture.Id,
                    new PartitionKey(PartitionKeyValue),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => CreateOkResponse(storedRewrite));

            foreach (object[] readerData in GetReaderProcessors())
            {
                string readerProcessorName = (string)readerData[0];
                using ResponseMessage reread = await harness.Container.ReadItemStreamAsync(
                    sourceFixture.Id,
                    new PartitionKey(PartitionKeyValue),
                    CreateItemOptions(readerProcessorName));
                AssertPlaintext(new FixtureDocument(await ReadToEndAsync(reread.Content)), sourceFixture);
            }
        }

        [Ignore]
        [TestMethod]
        public async Task ChangeFeedTypedProcessor_UsesConfiguredStreamProcessor()
        {
            // Production gap: typed change-feed processor overloads accept JObject callbacks from
            // the inner container and always create DecryptableItemCore / use JObject decryption.
            // They have no request-options surface and do not consult DefaultJsonProcessor, so the
            // Stream processor cannot currently be selected without changing production routing.
            CompatibilityFixture fixture = CreateFixtures()
                .Single(candidate => candidate.Name == "mde-stream");
            ContainerHarness harness = CreateHarness(Array.Empty<CompatibilityFixture>());
            harness.Container.UseStreamingJsonProcessingByDefault();
            Container.ChangesHandler<JObject> capturedHandler = null;
            harness.Inner
                .Setup(container => container.GetChangeFeedProcessorBuilder(
                    "typed-gap",
                    It.IsAny<Container.ChangesHandler<JObject>>()))
                .Callback<string, Container.ChangesHandler<JObject>>((_, handler) => capturedHandler = handler)
                .Returns((ChangeFeedProcessorBuilder)null);

            IReadOnlyCollection<DecryptableItem> delivered = null;
            harness.Container.GetChangeFeedProcessorBuilder<DecryptableItem>(
                "typed-gap",
                (changes, cancellationToken) =>
                {
                    delivered = changes;
                    return Task.CompletedTask;
                });

            await capturedHandler(
                new[] { JObject.Parse(fixture.RawStoredJson) },
                CancellationToken.None);

            Assert.IsInstanceOfType(delivered.Single(), typeof(StreamDecryptableItem));
        }
#endif

        public static IEnumerable<object[]> GetReaderProcessors()
        {
            yield return new object[] { JsonProcessor.Newtonsoft.ToString() };
#if NET8_0_OR_GREATER
            yield return new object[] { JsonProcessor.Stream.ToString() };
#endif
        }

        private static ContainerHarness CreateHarness(
            IReadOnlyCollection<CompatibilityFixture> fixtures,
            Encryptor encryptor = null,
            CosmosSerializer serializer = null)
        {
            Mock<Container> inner = new ();
            Mock<CosmosResponseFactory> responseFactory = new ();
            serializer ??= new FixtureCosmosSerializer();
            encryptor ??= new FixedKeyEncryptor();

            Mock<CosmosClient> client = new ();
            client.SetupGet(value => value.ResponseFactory).Returns(responseFactory.Object);
            client.SetupGet(value => value.ClientOptions).Returns(new CosmosClientOptions { Serializer = serializer });
            Mock<Database> database = new ();
            database.SetupGet(value => value.Client).Returns(client.Object);
            database.SetupGet(value => value.Id).Returns("mixed-database");
            inner.SetupGet(value => value.Database).Returns(database.Object);
            inner.SetupGet(value => value.Id).Returns("mixed-container");

            responseFactory
                .Setup(factory => factory.CreateItemFeedResponse<FixtureDocument>(It.IsAny<ResponseMessage>()))
                .Returns<ResponseMessage>(response => CreateFeedResponse(ReadFeedDocuments(response.Content)));

            inner
                .Setup(container => container.GetItemQueryStreamIterator(
                    It.IsAny<QueryDefinition>(),
                    It.IsAny<string>(),
                    It.IsAny<QueryRequestOptions>()))
                .Returns(() => CreateInnerFeedIterator(fixtures));
            inner
                .Setup(container => container.ReadManyItemsStreamAsync(
                    It.IsAny<IReadOnlyList<(string id, PartitionKey partitionKey)>>(),
                    It.IsAny<ReadManyRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => CreateOkResponse(CreateFeedJson(fixtures)));

            return new ContainerHarness(
                new EncryptionContainer(inner.Object, encryptor),
                inner);
        }

        private static FeedIterator CreateInnerFeedIterator(IReadOnlyCollection<CompatibilityFixture> fixtures)
        {
            Mock<FeedIterator> iterator = new ();
            iterator
                .Setup(feed => feed.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => CreateOkResponse(CreateFeedJson(fixtures)));
            return iterator.Object;
        }

        private static FeedResponse<FixtureDocument> CreateFeedResponse(IReadOnlyList<FixtureDocument> documents)
        {
            Mock<FeedResponse<FixtureDocument>> response = new ();
            response.SetupGet(value => value.Resource).Returns(documents);
            return response.Object;
        }

        private static CompatibilityFixture[] CreateFixtures()
        {
            return new[]
            {
                CreateFixture("plaintext", "plain-1", PlaintextFixture),
                CreateFixture("legacy-aead", "legacy-1", DecodeFixture(LegacyAeadFixtureBase64)),
                CreateFixture("mde-newtonsoft", "mde-newton-1", DecodeFixture(MdeNewtonsoftFixtureBase64)),
                CreateFixture("mde-stream", "mde-stream-1", DecodeFixture(MdeStreamFixtureBase64)),
            };
        }

        private static CompatibilityFixture CreateFixture(string name, string id, string rawStoredJson)
        {
            return new CompatibilityFixture(
                name,
                id,
                rawStoredJson.Replace("mixed-1", id),
                PlaintextFixture.Replace("mixed-1", id));
        }

        private static string DecodeFixture(string base64)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }

        private static string CreateFeedJson(IEnumerable<CompatibilityFixture> fixtures)
        {
            return "{\"Documents\":[" + string.Join(",", fixtures.Select(fixture => fixture.RawStoredJson)) + "]}";
        }

        private static MemoryStream CreateFeedStream(IEnumerable<CompatibilityFixture> fixtures)
        {
            return CreateStream(CreateFeedJson(fixtures));
        }

        private static MemoryStream CreateStream(string content)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        private static ResponseMessage CreateOkResponse(string content)
        {
            return new ResponseMessage(HttpStatusCode.OK)
            {
                Content = CreateStream(content),
            };
        }

        private static QueryRequestOptions CreateQueryOptions(string processorName)
        {
            return new QueryRequestOptions
            {
                Properties = CreateProcessorProperties(processorName),
            };
        }

        private static ReadManyRequestOptions CreateReadManyOptions(string processorName)
        {
            return new ReadManyRequestOptions
            {
                Properties = CreateProcessorProperties(processorName),
            };
        }

        private static ItemRequestOptions CreateItemOptions(string processorName)
        {
            return new ItemRequestOptions
            {
                Properties = CreateProcessorProperties(processorName),
            };
        }

        private static EncryptionItemRequestOptions CreateEncryptedWriteOptions(string processorName)
        {
            return new EncryptionItemRequestOptions
            {
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKeyId = DekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    PathsToEncrypt = new[] { "/Sensitive" },
                },
                Properties = CreateProcessorProperties(processorName),
            };
        }

        private static Dictionary<string, object> CreateProcessorProperties(string processorName)
        {
            return new Dictionary<string, object>
            {
                { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, processorName },
            };
        }

        // Query pages, read-many responses, and stream change-feed callbacks all converge on
        // DeserializeAndDecryptResponseAsync. The API-specific tests above verify routing into
        // that shared array path; this one assertion matrix intentionally avoids a Cartesian copy.
        private static void AssertMatrix(
            IEnumerable<FixtureDocument> actualDocuments,
            IReadOnlyCollection<CompatibilityFixture> fixtures)
        {
            FixtureDocument[] actual = actualDocuments.ToArray();
            Assert.AreEqual(fixtures.Count, actual.Length);
            foreach (CompatibilityFixture fixture in fixtures)
            {
                FixtureDocument document = actual.Single(candidate => candidate.Id == fixture.Id);
                AssertPlaintext(document, fixture);
            }
        }

        private static void AssertPlaintext(FixtureDocument document, CompatibilityFixture fixture)
        {
            Assert.IsTrue(
                JToken.DeepEquals(
                    JToken.Parse(fixture.ExpectedPlaintext),
                    JToken.Parse(document.RawJson)),
                $"{fixture.Name}: decrypted document did not exactly match the pinned plaintext.");

            using JsonDocument json = JsonDocument.Parse(document.RawJson);
            JsonElement root = json.RootElement;
            Assert.AreEqual(fixture.Id, root.GetProperty("id").GetString(), fixture.Name);
            Assert.AreEqual(PartitionKeyValue, root.GetProperty("PK").GetString(), fixture.Name);
            Assert.AreEqual("exact secret", root.GetProperty("Sensitive").GetString(), fixture.Name);
            Assert.AreEqual(9007199254740993L, root.GetProperty("HighPrecision").GetInt64(), fixture.Name);
            Assert.AreEqual("123.0", root.GetProperty("TrailingZero").GetRawText(), fixture.Name);
            Assert.AreEqual("6.02E+23", root.GetProperty("Exponent").GetRawText(), fixture.Name);
            Assert.IsFalse(root.TryGetProperty(Constants.EncryptedInfo, out _), fixture.Name);
        }

        private static void AssertMdeV3Storage(string storedJson, CompatibilityFixture sourceFixture)
        {
            JObject stored = JObject.Parse(storedJson);
            Assert.AreEqual(sourceFixture.Id, stored["id"]?.Value<string>());
            Assert.AreEqual(PartitionKeyValue, stored["PK"]?.Value<string>());
            Assert.AreEqual(3, stored[Constants.EncryptedInfo]?["_ef"]?.Value<int>());
            CollectionAssert.AreEqual(
                new[] { "/Sensitive" },
                stored[Constants.EncryptedInfo]?["_ep"]?.Values<string>().ToArray());
            Assert.IsNotNull(stored["Sensitive"]?.Value<string>());
            Assert.AreNotEqual("exact secret", stored["Sensitive"]?.Value<string>());
            StringAssert.Contains(storedJson, "\"HighPrecision\":9007199254740993");
            StringAssert.Contains(storedJson, "\"TrailingZero\":123.0");
            StringAssert.Contains(storedJson, "\"Exponent\":6.02E+23");
        }

        private static IReadOnlyList<FixtureDocument> ReadFeedDocuments(Stream content)
        {
            string raw = ReadToEnd(content);
            using JsonDocument feed = JsonDocument.Parse(raw);
            return feed.RootElement
                .GetProperty(Constants.DocumentsResourcePropertyName)
                .EnumerateArray()
                .Select(element => new FixtureDocument(element.GetRawText()))
                .ToArray();
        }

#if NET8_0_OR_GREATER
        private static async Task<FixtureDocument> MaterializeAsync(
            string storedJson,
            string processorName,
            Encryptor encryptor,
            CosmosSerializer serializer)
        {
            DecryptableItem item;
            if (string.Equals(processorName, JsonProcessor.Stream.ToString(), StringComparison.Ordinal))
            {
                item = new StreamDecryptableItem(CreateStream(storedJson), encryptor, serializer);
            }
            else
            {
                item = new DecryptableItemCore(JObject.Parse(storedJson), encryptor, serializer);
            }

            await using (item)
            {
                (FixtureDocument document, DecryptionContext _) = await item.GetItemAsync<FixtureDocument>();
                return document;
            }
        }
#endif

        private static async Task<string> ReadToEndAsync(Stream content)
        {
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            using StreamReader reader = new (content, Encoding.UTF8, true, 1024, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }

        private static string ReadToEnd(Stream content)
        {
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            using StreamReader reader = new (content, Encoding.UTF8, true, 1024, leaveOpen: true);
            return reader.ReadToEnd();
        }

        private sealed class ContainerHarness
        {
            public ContainerHarness(EncryptionContainer container, Mock<Container> inner)
            {
                this.Container = container;
                this.Inner = inner;
            }

            public EncryptionContainer Container { get; }

            public Mock<Container> Inner { get; }
        }

        private sealed class CompatibilityFixture
        {
            public CompatibilityFixture(string name, string id, string rawStoredJson, string expectedPlaintext)
            {
                this.Name = name;
                this.Id = id;
                this.RawStoredJson = rawStoredJson;
                this.ExpectedPlaintext = expectedPlaintext;
            }

            public string Name { get; }

            public string Id { get; }

            public string RawStoredJson { get; }

            public string ExpectedPlaintext { get; }
        }

        public sealed class FixtureDocument
        {
            public FixtureDocument(string rawJson)
            {
                this.RawJson = rawJson;
                using JsonDocument json = JsonDocument.Parse(rawJson);
                this.Id = json.RootElement.GetProperty("id").GetString();
            }

            public string Id { get; }

            public string RawJson { get; }
        }

        private sealed class FixtureCosmosSerializer : CosmosSerializer
        {
            public override T FromStream<T>(Stream stream)
            {
                string raw = ReadToEnd(stream);
                if (typeof(T) == typeof(FixtureDocument))
                {
                    return (T)(object)new FixtureDocument(raw);
                }

                return JsonConvert.DeserializeObject<T>(raw);
            }

            public override Stream ToStream<T>(T input)
            {
                string raw = input is FixtureDocument document
                    ? document.RawJson
                    : JsonConvert.SerializeObject(input);
                return CreateStream(raw);
            }
        }

        private sealed class FixedKeyEncryptor : Encryptor
        {
            private readonly DataEncryptionKey legacyKey;
            private readonly DataEncryptionKey mdeKey;

            public FixedKeyEncryptor()
            {
                byte[] rawKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
                AeadAes256CbcHmac256EncryptionKey aeadKey = new (
                    rawKey,
#pragma warning disable CS0618
                    CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized);
#pragma warning restore CS0618
                this.legacyKey = new AeadAes256CbcHmac256Algorithm(
                    aeadKey,
                    EncryptionType.Randomized,
                    algorithmVersion: 1);
                Microsoft.Data.Encryption.Cryptography.PlaintextDataEncryptionKey plaintextKey = new (DekId, rawKey);
                this.mdeKey = new MdeEncryptionAlgorithm(
                    rawKey,
                    plaintextKey,
                    Data.Encryption.Cryptography.EncryptionType.Randomized);
            }

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                Assert.AreEqual(DekId, dataEncryptionKeyId);
                return Task.FromResult(this.mdeKey);
            }

            public override Task<byte[]> EncryptAsync(
                byte[] plainText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                Assert.AreEqual(DekId, dataEncryptionKeyId);
                return Task.FromResult(this.GetKey(encryptionAlgorithm).EncryptData(plainText));
            }

            public override Task<byte[]> DecryptAsync(
                byte[] cipherText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                Assert.AreEqual(DekId, dataEncryptionKeyId);
                return Task.FromResult(this.GetKey(encryptionAlgorithm).DecryptData(cipherText));
            }

            private DataEncryptionKey GetKey(string encryptionAlgorithm)
            {
                return string.Equals(
                    encryptionAlgorithm,
                    CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    StringComparison.Ordinal)
                    ? this.mdeKey
                    : this.legacyKey;
            }
        }
    }
}
