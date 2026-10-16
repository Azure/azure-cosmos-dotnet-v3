//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    // Public APIs only; shared with standalone compatibility consumers.
    [TestClass]
    public class EncryptionPublicConsumerCompatibilityTests
    {
#pragma warning disable CS0618
        private const string Legacy = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized;
#pragma warning restore CS0618
        private const string Mde = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized;
        private const string KeyId = "consumer-key";
        private const string ProcessorKey = "encryption-json-processor";
        private const string InvalidMetadata = "The document contains invalid encryption metadata.";
        private const string PlainJson = "{\"id\":\"i\",\"PK\":\"p\",\"Sensitive\":\"secret!\"}";
        private const string FixturePlainJson = "{\"id\":\"released-legacy\",\"PK\":\"p\",\"Sensitive\":\"legacy fixture\",\"Nested\":{\"values\":[\"plain\",7,null]}}";
        // Produced by preview07's public CreateItemStreamAsync on 2026-10-12 with raw key bytes 1..32.
        private const string ReleasedLegacyJson = "{\"id\":\"released-legacy\",\"PK\":\"p\",\"_ei\":{\"_ef\":2,\"_en\":\"consumer-key\",\"_ea\":\"AEAes256CbcHmacSha256Randomized\",\"_ed\":\"AY8fcyCFzsXbYEkdT/k4wy78WQjSdnpPZodytF/WsKjD0aqC5IEQsA2mSF8N5czQwACSFvFpXn1oFVTdUe+Q+SPD4X8MPH7wbRWK+VdWTzQtG6ifyTsl3bR8aIjqzzu5xyMQCIzH9MInaWg6Fr7lmtzz+88ZNFIS0nx2fvTdVMg3\",\"_ep\":[\"/Sensitive\",\"/Nested\"]}}";
        private static readonly ConsumerSerializer Serializer = new ();

        public TestContext TestContext { get; set; }

        [DataTestMethod]
        [DynamicData(nameof(ServiceErrorCases), DynamicDataSourceType.Method)]
        public async Task ReadMany_PublicStatusHandler_CanInspectErrorThenReadEncryptedSuccess(int status, bool noBody, string processor)
        {
            const string body = "{\"code\":\"service-error\",\"message\":\"consumer sentinel\"}";
            ConsumerEncryptor encryptor = new (true) { ExpectedAlgorithm = Legacy };
            string encrypted = ReleasedLegacyJson;
            Container container = CreateContainer(encryptor, out Mock<Container> inner);
            ReadManyRequestOptions options = new ()
            {
                Properties = new Dictionary<string, object> { [ProcessorKey] = processor, ["caller-tag"] = "unchanged" },
            };
            IReadOnlyList<(string id, PartitionKey partitionKey)> ids = new[] { ("released-legacy", new PartitionKey("p")) };
            using ResponseMessage error = Response((HttpStatusCode)status, noBody ? null : body);
            error.Headers["x-ms-activity-id"] = "error-activity";
            error.Headers["x-ms-request-charge"] = "12.5";
            error.Headers["x-ms-retry-after-ms"] = "23";
            inner.SetupSequence(c => c.ReadManyItemsStreamAsync(ids, options, It.IsAny<CancellationToken>()))
                .ReturnsAsync(error)
                .ReturnsAsync(Response(HttpStatusCode.OK, "{\"Documents\":[" + encrypted + "],\"_count\":1}"));

            ResponseMessage first = null;
            Exception failure = await CaptureAsync(async () => first = await container.ReadManyItemsStreamAsync(ids, options));
            this.Observe("ReadMany", new { status, noBody, processor, error = failure?.GetType().FullName, message = failure?.Message });
            Assert.IsNull(failure, "An HTTP error must reach the caller's status handler, not fail feed parsing.");
            using (first)
            {
                Assert.AreEqual((HttpStatusCode)status, first.StatusCode);
                Assert.AreEqual("error-activity", first.Headers.ActivityId);
                Assert.AreEqual(12.5, first.Headers.RequestCharge);
                Assert.AreEqual("23", first.Headers["x-ms-retry-after-ms"]);
                string action = first.StatusCode == HttpStatusCode.TooManyRequests
                    ? "retry-after:" + first.Headers["x-ms-retry-after-ms"]
                    : first.StatusCode == HttpStatusCode.NotFound ? "handle-missing" : "unexpected";
                Assert.AreEqual(status == 429 ? "retry-after:23" : "handle-missing", action);
                if (noBody)
                {
                    Assert.IsNull(first.Content);
                }
                else
                {
                    Assert.AreEqual(body, await ReadTextAsync(first.Content));
                }

                Assert.AreEqual(0, encryptor.DecryptCalls);
            }

            // Application retry, not the SDK's HTTP retry policy.
            using ResponseMessage success = await container.ReadManyItemsStreamAsync(ids, options);
            Assert.AreEqual(HttpStatusCode.OK, success.StatusCode);
            JObject page = Parse(await ReadTextAsync(success.Content));
            Assert.IsTrue(JToken.DeepEquals(Parse(FixturePlainJson), page["Documents"][0]));
            Assert.AreEqual(1, encryptor.DecryptCalls);
            Assert.AreEqual("unchanged", options.Properties["caller-tag"]);
            Assert.AreEqual(processor, options.Properties[ProcessorKey]);
            inner.Verify(c => c.ReadManyItemsStreamAsync(ids, options, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        public static IEnumerable<object[]> ServiceErrorCases()
        {
            foreach (string processor in Processors())
            {
                foreach (int status in new[] { 404, 429 })
                {
                    yield return new object[] { status, false, processor };
                    yield return new object[] { status, true, processor };
                }
            }
        }

        [DataTestMethod]
        [DataRow("Create", "fault")]
        [DataRow("Create", "null")]
        [DataRow("Create", "empty")]
        [DataRow("Create", "cancel")]
        [DataRow("Replace", "fault")]
        [DataRow("Replace", "null")]
        [DataRow("Replace", "empty")]
        [DataRow("Replace", "cancel")]
        [DataRow("Upsert", "fault")]
        [DataRow("Upsert", "null")]
        [DataRow("Upsert", "empty")]
        [DataRow("Upsert", "cancel")]
        public async Task Legacy_PublicWriteFailure_AllowsCallerToReadAndRetrySameInput(string operation, string fault)
        {
            using CancellationTokenSource cancellation = new ();
            InvalidOperationException sentinel = new ("consumer encryptor failure");
            ConsumerEncryptor encryptor = new ()
            {
                EncryptOverride = (bytes, token) =>
                {
                    if (fault == "fault")
                    {
                        return Task.FromException<byte[]>(sentinel);
                    }

                    if (fault == "null")
                    {
                        return Task.FromResult<byte[]>(null);
                    }

                    if (fault == "empty")
                    {
                        return Task.FromResult(Array.Empty<byte>());
                    }

                    cancellation.Cancel();
                    return Task.FromCanceled<byte[]>(cancellation.Token);
                },
            };
            Container container = CreateContainer(encryptor, out Mock<Container> inner);
            EncryptionItemRequestOptions options = WriteOptions(Legacy);
            using MemoryStream input = Utf8(PlainJson);
            int dispatched = 0;
            Stream dispatchedStream = null;
            SetupWrites(inner, options, (stream, pk) =>
            {
                dispatched++;
                dispatchedStream = stream;
                Assert.AreEqual(new PartitionKey("p"), pk);
                Assert.AreNotSame(input, stream);
                string stored = ReadText(stream);
                stream.Dispose();
                return Response(HttpStatusCode.Created, stored);
            });
            Exception failure = await CaptureAsync(async () =>
            {
                using ResponseMessage response = await WriteAsync(container, operation, input, options, cancellation.Token);
            });
            this.Observe("LegacyInput", new { operation, fault, readable = input.CanRead, error = failure?.GetType().FullName, message = failure?.Message, dispatched });
            Assert.IsNotNull(failure);
            if (fault == "fault")
            {
                Assert.AreSame(sentinel, failure);
            }
            else if (fault == "cancel")
            {
                Assert.IsInstanceOfType(failure, typeof(OperationCanceledException));
                Assert.AreEqual(cancellation.Token, ((OperationCanceledException)failure).CancellationToken);
            }
            else
            {
                Assert.IsInstanceOfType(failure, typeof(InvalidOperationException));
                if (fault == "empty")
                {
                    Assert.AreEqual("Encryptor returned empty cipherText from EncryptAsync.", failure.Message);
                }
            }

            Assert.AreEqual(0, dispatched, "Encryption failure must not send a write to the inner container.");
            Assert.IsTrue(input.CanRead, "The caller must be able to recover its input after encryption fails.");
            input.Position = 0; // Rewinding is the caller's choice; the SDK need not rewind a borrowed input.
            Assert.AreEqual(PlainJson, await ReadTextAsync(input));
            input.Position = 0;
            encryptor.EncryptOverride = null;
            using ResponseMessage retry = await WriteAsync(container, operation, input, options, CancellationToken.None);
            Assert.IsTrue(JToken.DeepEquals(Parse(PlainJson), Parse(await ReadTextAsync(retry.Content))));
            Assert.AreEqual(1, dispatched);
            Assert.IsFalse(dispatchedStream.CanRead, "The simulated SDK consumes/disposes its own encrypted request.");
        }

        [DataTestMethod]
        [DynamicData(nameof(StringCases), DynamicDataSourceType.Method)]
        public async Task Legacy_PublicWriterReader_PreservesActualStringsAndIdentity(bool realCipher, string processor)
        {
            const string original = "{\"id\":\"i\",\"PK\":\"p\",\"Sensitive\":\"2024-01-01T10:00:00.000Z\",\"Nested\":{\"when\":\"2024-01-01T10:00:00.000Z\",\"offset\":\"2025-01-02T03:04:05.1234567+03:00\",\"values\":[\"2024-01-01T10:00:00.000Z\"]}}";
            ConsumerEncryptor encryptor = new (realCipher);
            string encrypted = await WriteDocumentAsync(original, Legacy, encryptor, "/Sensitive", "/Nested");
            Container container = CreateContainer(encryptor, out Mock<Container> inner);
            ItemRequestOptions options = ReadOptions(processor);
            inner.Setup(c => c.ReadItemStreamAsync("i", new PartitionKey("p"), options, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Response(HttpStatusCode.OK, encrypted));
            using ResponseMessage response = await container.ReadItemStreamAsync("i", new PartitionKey("p"), options);
            string actual = await ReadTextAsync(response.Content);
            this.Observe("LegacyStrings", new
            {
                realCipher,
                processor,
                original,
                actual,
                writerPlaintext = Encoding.UTF8.GetString(encryptor.LastPlaintext),
                readerPlaintext = Encoding.UTF8.GetString(encryptor.LastDecryptedPlaintext),
                producer = "public CreateItemStreamAsync with a byte-copy or real AES Encryptor; no crypto byte normalization",
            });
            Assert.IsTrue(JToken.DeepEquals(Parse(original), Parse(actual)), "A JSON string is data, not an instruction to parse a date.");
            Assert.AreEqual(JTokenType.String, Parse(actual)["Sensitive"].Type);
            Assert.AreEqual(JTokenType.String, Parse(actual)["Nested"]["offset"].Type);
        }

        public static IEnumerable<object[]> StringCases()
        {
            foreach (string processor in Processors())
            {
                yield return new object[] { false, processor };
                yield return new object[] { true, processor };
            }
        }

        [TestMethod]
        public async Task Legacy_PublicPocoConsumer_PreservesDateTimeIntent()
        {
            ConsumerDates original = new ()
            {
                id = "i",
                PK = "p",
                Sensitive = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                Offset = new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.FromHours(3)),
            };
            ConsumerEncryptor encryptor = new (true);
            string encrypted;
            using (Stream serialized = Serializer.ToStream(original))
            {
                encrypted = await WriteDocumentAsync(await ReadTextAsync(serialized), Legacy, encryptor, "/Sensitive", "/Offset");
            }

            Container container = CreateContainer(encryptor, out Mock<Container> inner);
            inner.Setup(c => c.ReadItemStreamAsync("i", new PartitionKey("p"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Response(HttpStatusCode.OK, encrypted));
            ItemResponse<ConsumerDates> response = await container.ReadItemAsync<ConsumerDates>("i", new PartitionKey("p"));
            Assert.AreEqual(original.Sensitive, response.Resource.Sensitive);
            Assert.AreEqual(original.Offset, response.Resource.Offset);
            Assert.AreEqual(original.id, response.Resource.id);
            Assert.AreEqual(original.PK, response.Resource.PK);
            this.Observe("PocoDates", new { response.Resource.Sensitive, response.Resource.Offset, interpretation = "Typed date value/instant, not an immutable serialized spelling." });
        }

        [TestMethod]
        public async Task Legacy_PublicWriter_ProducesRealCipherFixture()
        {
            ConsumerEncryptor encryptor = new (true);
            string encrypted = await WriteDocumentAsync(FixturePlainJson, Legacy, encryptor, "/Sensitive", "/Nested");
            JObject document = Parse(encrypted);
            Assert.AreEqual(2, (int)document["_ei"]["_ef"]);
            Assert.AreEqual(KeyId, (string)document["_ei"]["_en"]);
            Assert.AreEqual(Legacy, (string)document["_ei"]["_ea"]);
            this.Observe("FixtureProducer", new
            {
                encrypted,
                original = FixturePlainJson,
                rawKeyBase64 = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()),
                producer = "Container.CreateItemStreamAsync / DataEncryptionKey.Create / public Encryptor",
            });
        }

        [DataTestMethod]
        [DynamicData(nameof(ReleasedFixtureCases), DynamicDataSourceType.Method)]
        public async Task ReleasedLegacy_PublicRead_PreservesDocumentWithOptionalPaths(bool containerStream, string processor, bool omitPaths)
        {
            ConsumerEncryptor encryptor = new (true) { ExpectedAlgorithm = Legacy };
            JObject document = Parse(ReleasedLegacyJson);
            if (omitPaths)
            {
                ((JObject)document["_ei"]).Remove("_ep");
            }

            Container container = CreateContainer(encryptor, out Mock<Container> inner);
            Configure(container, containerStream);
            ItemRequestOptions options = processor == null ? null : ReadOptions(processor);
            inner.Setup(c => c.ReadItemStreamAsync("released-legacy", new PartitionKey("p"), options, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Response(HttpStatusCode.OK, document.ToString(Formatting.None)));
            using ResponseMessage response = await container.ReadItemStreamAsync("released-legacy", new PartitionKey("p"), options);
            string actual = await ReadTextAsync(response.Content);
            this.Observe("ReleasedLegacyRead", new { containerStream, processor, omitPaths, actual, encryptor.DecryptCalls });
            Assert.IsTrue(JToken.DeepEquals(Parse(FixturePlainJson), Parse(actual)));
            Assert.AreEqual(1, encryptor.DecryptCalls, "Legacy fallback must not perform duplicate crypto.");
            Assert.AreEqual(0, encryptor.KeyCalls);
            if (options != null)
            {
                Assert.AreEqual(processor, options.Properties[ProcessorKey]);
                Assert.AreEqual("untouched", options.Properties["caller-tag"]);
            }
        }

        public static IEnumerable<object[]> ReleasedFixtureCases()
        {
            foreach (bool omitPaths in new[] { false, true })
            {
                yield return new object[] { false, null, omitPaths };
                foreach (string processor in Processors())
                {
                    yield return new object[] { false, processor, omitPaths };
                }

#if NET8_0_OR_GREATER && !RELEASED_CUSTOM_PACKAGE
                yield return new object[] { true, null, omitPaths };
#endif
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(LegacyCancellationCases), DynamicDataSourceType.Method)]
        public async Task ReleasedLegacy_PublicRead_CancellationRemainsCancellation(bool containerStream, string processor)
        {
            using CancellationTokenSource cancellation = new ();
            ConsumerEncryptor encryptor = new (true)
            {
                ExpectedAlgorithm = Legacy,
                DecryptOverride = (bytes, token) =>
                {
                    Assert.AreEqual(cancellation.Token, token);
                    cancellation.Cancel();
                    return Task.FromCanceled<byte[]>(cancellation.Token);
                },
            };
            Container container = CreateContainer(encryptor, out Mock<Container> inner);
            Configure(container, containerStream);
            ItemRequestOptions options = processor == null ? null : ReadOptions(processor);
            using ResponseMessage raw = Response(HttpStatusCode.OK, ReleasedLegacyJson);
            inner.Setup(c => c.ReadItemStreamAsync("released-legacy", new PartitionKey("p"), options, cancellation.Token))
                .ReturnsAsync(raw);
            Exception failure = await CaptureAsync(async () =>
            {
                using ResponseMessage response = await container.ReadItemStreamAsync("released-legacy", new PartitionKey("p"), options, cancellation.Token);
            });
            this.Observe("LegacyReadCancellation", new { containerStream, processor, error = failure?.GetType().FullName, encryptor.DecryptCalls });
            Assert.IsInstanceOfType(failure, typeof(OperationCanceledException));
            Assert.AreEqual(cancellation.Token, ((OperationCanceledException)failure).CancellationToken);
            Assert.AreEqual(1, encryptor.DecryptCalls);
        }

        public static IEnumerable<object[]> LegacyCancellationCases()
        {
            yield return new object[] { false, null };
            foreach (string processor in Processors())
            {
                yield return new object[] { false, processor };
            }

#if NET8_0_OR_GREATER && !RELEASED_CUSTOM_PACKAGE
            yield return new object[] { true, null };
#endif
        }

        [DataTestMethod]
        [DataRow("released-legacy")]
        [DataRow("plaintext")]
        [DataRow("plaintext-null-metadata")]
        public async Task PublicTypedReplace_ExplicitMdeRewrite_PreservesCallerValuesAndIdentity(string source)
        {
            ConsumerEncryptor encryptor = new (true, allowKeyAccess: true) { ExpectedAlgorithm = Legacy };
            Container container = CreateContainer(encryptor, out Mock<Container> inner);
            JObject item;
            if (source == "released-legacy")
            {
                inner.Setup(c => c.ReadItemStreamAsync("released-legacy", new PartitionKey("p"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => Response(HttpStatusCode.OK, ReleasedLegacyJson));
                using ResponseMessage read = await container.ReadItemStreamAsync("released-legacy", new PartitionKey("p"));
                item = Parse(await ReadTextAsync(read.Content));
            }
            else
            {
                item = Parse("{\"id\":\"i\",\"PK\":\"p\",\"Sensitive\":\"2024-01-01T10:00:00.000Z\",\"Nested\":{\"offset\":\"2025-01-02T03:04:05.1234567+03:00\"}}");
                if (source == "plaintext-null-metadata")
                {
                    item["_ei"] = JValue.CreateNull();
                }
            }

            JObject callerBefore = (JObject)item.DeepClone();
            JObject expected = (JObject)item.DeepClone();
            expected.Remove("_ei");
            EncryptionItemRequestOptions options = WriteOptions(Mde, "/Sensitive", "/Nested");
            encryptor.ExpectedAlgorithm = Mde;
            JObject stored = null;
            string id = (string)item["id"];
            inner.Setup(c => c.ReplaceItemStreamAsync(It.IsAny<Stream>(), id, new PartitionKey("p"), options, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, string forwardedId, PartitionKey pk, ItemRequestOptions ignored, CancellationToken token) =>
                {
                    stored = Parse(ReadText(stream));
                    stream.Dispose();
                    return Response(HttpStatusCode.OK, stored.ToString(Formatting.None));
                });
            ItemResponse<JObject> result = null;
            Exception failure = await CaptureAsync(async () => result = await container.ReplaceItemAsync(item, id, new PartitionKey("p"), options));
            this.Observe("ExplicitMdeRewrite", new { source, error = failure?.GetType().FullName, message = failure?.Message, stored, actual = result?.Resource });
            Assert.IsNull(failure);
            Assert.IsTrue(JToken.DeepEquals(callerBefore, item), "Opting in to rewrite must not mutate the caller's object.");
            Assert.IsTrue(JToken.DeepEquals(expected, result.Resource));
            Assert.AreEqual(id, (string)stored["id"]);
            Assert.AreEqual("p", (string)stored["PK"]);
            Assert.AreEqual(Mde, (string)stored["_ei"]["_ea"]);
            Assert.AreEqual(3, (int)stored["_ei"]["_ef"]);
            Assert.AreEqual(1, stored.Properties().Count(p => p.Name == "_ei"));
        }

        [DataTestMethod]
        [DynamicData(nameof(OptionalMdeDataCases), DynamicDataSourceType.Method)]
        public async Task PublicMdeReader_OptionalEnvelopeData_DoesNotGuessCustomCipherFormat(string processor)
        {
            ConsumerEncryptor encryptor = new ();
            JObject document = Parse(await WriteDocumentAsync(PlainJson, Mde, encryptor));
            ((JObject)document["_ei"]).Remove("_ed");
            encryptor.ResetCounts();
            Container container = CreateContainer(encryptor, out Mock<Container> inner);
            ItemRequestOptions options = ReadOptions(processor);
            inner.Setup(c => c.ReadItemStreamAsync("i", new PartitionKey("p"), options, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Response(HttpStatusCode.OK, document.ToString(Formatting.None)));
            using ResponseMessage response = await container.ReadItemStreamAsync("i", new PartitionKey("p"), options);
            Assert.IsTrue(JToken.DeepEquals(Parse(PlainJson), Parse(await ReadTextAsync(response.Content))));
            Assert.AreEqual(1, encryptor.DecryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
        }

        public static IEnumerable<object[]> OptionalMdeDataCases()
        {
            foreach (string processor in Processors())
            {
                yield return new object[] { processor };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(LazyCases), DynamicDataSourceType.Method)]
        public async Task Lazy_PublicConsumer_CanQuarantineMalformedKeyWithoutLosingPrimaryFailure(string algorithm, string badKey)
        {
            ConsumerEncryptor encryptor = new (true, allowKeyAccess: true);
            JObject normal = Parse(await WriteDocumentAsync(PlainJson, algorithm, encryptor));
            Assert.AreEqual(JTokenType.String, normal["_ei"]["_en"].Type, "A normal public writer does not produce this malformed key.");
            JObject malformed = (JObject)normal.DeepClone();
            malformed["_ei"]["_en"] = JToken.Parse(badKey);
            encryptor.ResetCounts();
            Container container = CreateContainer(encryptor, out Mock<Container> inner);
            inner.SetupSequence(c => c.ReadItemStreamAsync("i", new PartitionKey("p"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response(HttpStatusCode.OK, malformed.ToString(Formatting.None)))
                .ReturnsAsync(Response(HttpStatusCode.OK, normal.ToString(Formatting.None)));
            ItemResponse<DecryptableItem> response = await container.ReadItemAsync<DecryptableItem>("i", new PartitionKey("p"));
            try
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    Exception error = await CaptureAsync(async () => await response.Resource.GetItemAsync<JObject>());
                    this.Observe("LazyMalformedKey", new { algorithm, badKey, attempt, error = error?.GetType().FullName, message = error?.Message, content = (error as EncryptionException)?.EncryptedContent });
                    Assert.IsInstanceOfType(error, typeof(EncryptionException), "A consumer's EncryptionException handler must remain reachable.");
                    EncryptionException failure = (EncryptionException)error;
                    Assert.IsInstanceOfType(failure.InnerException, typeof(InvalidOperationException));
                    Assert.AreEqual(InvalidMetadata, failure.InnerException.Message);
                    Assert.IsTrue(JToken.DeepEquals(malformed, Parse(failure.EncryptedContent)), "The consumer needs the original document for quarantine/diagnostics.");
                }

                Assert.AreEqual(0, encryptor.DecryptCalls);
            }
            finally
            {
                await DisposeItemAsync(response.Resource);
            }

            ItemResponse<DecryptableItem> control = await container.ReadItemAsync<DecryptableItem>("i", new PartitionKey("p"));
            try
            {
                (JObject value, DecryptionContext context) = await control.Resource.GetItemAsync<JObject>();
                Assert.IsTrue(JToken.DeepEquals(Parse(PlainJson), value));
                Assert.IsNotNull(context);
            }
            finally
            {
                await DisposeItemAsync(control.Resource);
            }
        }

        public static IEnumerable<object[]> LazyCases()
        {
            foreach (string algorithm in new[] { Legacy, Mde })
            {
                yield return new object[] { algorithm, "{}" };
                yield return new object[] { algorithm, "[]" };
            }
        }

#if NET8_0_OR_GREATER && !RELEASED_CUSTOM_PACKAGE
        [DataTestMethod]
        [DataRow(false, null, false)]
        [DataRow(true, null, false)]
        [DataRow(true, "Newtonsoft", false)]
        [DataRow(true, "Stream", false)]
        [DataRow(false, null, true)]
        [DataRow(true, null, true)]
        [DataRow(true, "Newtonsoft", true)]
        [DataRow(true, "Stream", true)]
        public async Task PointRead_PublicConsumer_SeparatesCorrectValueFromReaderSelection(bool containerStream, string processor, bool realCipher)
        {
            ConsumerEncryptor encryptor = new (realCipher, allowKeyAccess: realCipher);
            string encrypted = await WriteDocumentAsync(PlainJson, Mde, encryptor);
            encryptor.ResetCounts();
            List<string> activities = new ();
            using ActivityListener listener = new ()
            {
                ShouldListenTo = source => source.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> creation) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => activities.Add(activity.OperationName),
            };
            ActivitySource.AddActivityListener(listener);
            Container container = CreateContainer(encryptor, out Mock<Container> inner);
            if (containerStream)
            {
                container.UseStreamingJsonProcessingByDefault();
            }

            ItemRequestOptions options = processor == null ? null : ReadOptions(processor);
            inner.Setup(c => c.ReadItemStreamAsync("i", new PartitionKey("p"), options, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Response(HttpStatusCode.OK, encrypted));
            using ResponseMessage response = await container.ReadItemStreamAsync("i", new PartitionKey("p"), options);
            string actual = await ReadTextAsync(response.Content);
            this.Observe("PointSelection", new { containerStream, processor, realCipher, actual, activities, encryptor.DecryptCalls, encryptor.KeyCalls });
            Assert.IsTrue(JToken.DeepEquals(Parse(PlainJson), Parse(actual)), "Reader selection alone must not be reported as broken normal data.");
            if (!realCipher || encryptor.KeyCalls == 0)
            {
                Assert.AreEqual(1, encryptor.DecryptCalls);
                Assert.AreEqual(0, encryptor.KeyCalls);
            }
            string expected = processor ?? (containerStream ? "Stream" : "Newtonsoft");
            CollectionAssert.Contains(activities, "EncryptionProcessor.Decrypt.Mde." + expected);
            if (options != null)
            {
                Assert.AreEqual(processor, options.Properties[ProcessorKey]);
                Assert.AreEqual("untouched", options.Properties["caller-tag"]);
            }
        }
#endif

        [DataTestMethod]
        [DynamicData(nameof(BomCases), DynamicDataSourceType.Method)]
        public async Task PublicPointRead_CustomProducerBom_RecordsPolicyBoundaryWithNormalControl(bool containerStream, string processor, bool lazy)
        {
            ConsumerEncryptor encryptor = new ();
            Container container = CreateContainer(encryptor, out Mock<Container> inner);
#if NET8_0_OR_GREATER && !RELEASED_CUSTOM_PACKAGE
            if (containerStream)
            {
                container.UseStreamingJsonProcessingByDefault();
            }
#endif
            ItemRequestOptions options = processor == null ? null : ReadOptions(processor);
            byte[] ordinary = Encoding.UTF8.GetBytes("{\"id\":\"i\"}");
            byte[] custom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(ordinary).ToArray();
            inner.SetupSequence(c => c.ReadItemStreamAsync("i", new PartitionKey("p"), options, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.OK) { Content = new MemoryStream(custom) })
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.OK) { Content = new MemoryStream(ordinary) });
            JObject value = null;
            Exception failure = await CaptureAsync(async () => value = await PublicReadAsync(container, options, lazy));
            this.Observe("CustomProducerBom", new
            {
                containerStream,
                processor,
                lazy,
                inputBase64 = Convert.ToBase64String(custom),
                error = failure?.GetType().FullName,
                message = failure?.Message,
                value,
                producer = "SDK-injected custom HTTP response; stock Cosmos service BOM production not established",
                policy = "Observation, not a requirement that every reader accept BOM.",
            });
            if (failure == null)
            {
                Assert.AreEqual("i", (string)value["id"]);
            }

            Assert.AreEqual(0, encryptor.DecryptCalls);
            JObject control = await PublicReadAsync(container, options, lazy);
            Assert.AreEqual("i", (string)control["id"], "The same public path must still handle ordinary service JSON.");
        }

        public static IEnumerable<object[]> BomCases()
        {
            foreach (bool lazy in new[] { false, true })
            {
                yield return new object[] { false, null, lazy };
                yield return new object[] { false, "Newtonsoft", lazy };
#if NET8_0_OR_GREATER && !RELEASED_CUSTOM_PACKAGE
                yield return new object[] { true, null, lazy };
                yield return new object[] { true, "Newtonsoft", lazy };
                yield return new object[] { true, "Stream", lazy };
#endif
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(MetadataCases), DynamicDataSourceType.Method)]
        public async Task Legacy_PublicRead_RejectsMissingNullEmptyCipherBeforeCrypto(string fieldState, string processor)
        {
            ConsumerEncryptor encryptor = new (true);
            JObject document = Parse(await WriteDocumentAsync(PlainJson, Legacy, encryptor));
            if (fieldState == "missing")
            {
                ((JObject)document["_ei"]).Remove("_ed");
            }
            else
            {
                document["_ei"]["_ed"] = fieldState == "null" ? JValue.CreateNull() : JValue.CreateString(string.Empty);
            }

            encryptor.ResetCounts();
            Container container = CreateContainer(encryptor, out Mock<Container> inner);
            ItemRequestOptions options = ReadOptions(processor);
            inner.Setup(c => c.ReadItemStreamAsync("i", new PartitionKey("p"), options, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Response(HttpStatusCode.OK, document.ToString(Formatting.None)));
            Exception failure = await CaptureAsync(async () =>
            {
                using ResponseMessage response = await container.ReadItemStreamAsync("i", new PartitionKey("p"), options);
            });
            this.Observe("LegacyMissingCipher", new { fieldState, processor, error = failure?.GetType().FullName, message = failure?.Message, encryptor.DecryptCalls });
            Assert.IsInstanceOfType(failure, typeof(InvalidOperationException));
            Assert.AreEqual(InvalidMetadata, failure.Message);
            Assert.AreEqual(0, encryptor.DecryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
        }

        public static IEnumerable<object[]> MetadataCases()
        {
            foreach (string processor in Processors())
            {
                foreach (string state in new[] { "missing", "null", "empty" })
                {
                    yield return new object[] { state, processor };
                }
            }
        }

        private static IEnumerable<string> Processors()
        {
            yield return "Newtonsoft";
#if NET8_0_OR_GREATER && !RELEASED_CUSTOM_PACKAGE
            yield return "Stream";
#endif
        }

        private void Observe(string family, object evidence)
            => this.TestContext.WriteLine("PUBLIC_PROOF " + JsonConvert.SerializeObject(new { family, evidence }));

        private static JObject Parse(string json)
        {
            using StringReader text = new (json);
            using JsonTextReader reader = new (text) { DateParseHandling = DateParseHandling.None };
            return JObject.Load(reader);
        }

        private static MemoryStream Utf8(string json) => new (Encoding.UTF8.GetBytes(json));

        private static ResponseMessage Response(HttpStatusCode status, string json)
            => new (status) { Content = json == null ? null : Utf8(json) };

        private static string ReadText(Stream stream)
        {
            using StreamReader reader = new (stream, Encoding.UTF8, true, 1024, leaveOpen: true);
            return reader.ReadToEnd();
        }

        private static async Task<string> ReadTextAsync(Stream stream)
        {
            using StreamReader reader = new (stream, Encoding.UTF8, true, 1024, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }

        private static ItemRequestOptions ReadOptions(string processor) => new ()
        {
            Properties = new Dictionary<string, object> { [ProcessorKey] = processor, ["caller-tag"] = "untouched" },
        };

        private static void Configure(Container container, bool containerStream)
        {
#if NET8_0_OR_GREATER && !RELEASED_CUSTOM_PACKAGE
            if (containerStream)
            {
                container.UseStreamingJsonProcessingByDefault();
            }
#endif
        }

        private static EncryptionItemRequestOptions WriteOptions(string algorithm, params string[] paths) => new ()
        {
            EncryptionOptions = new EncryptionOptions
            {
                DataEncryptionKeyId = KeyId,
                EncryptionAlgorithm = algorithm,
                PathsToEncrypt = paths.Length == 0 ? new[] { "/Sensitive" } : paths,
            },
        };

        private static Container CreateContainer(Encryptor encryptor, out Mock<Container> inner)
        {
            Mock<CosmosResponseFactory> factory = new ();
            factory.Setup(f => f.CreateItemResponse<JObject>(It.IsAny<ResponseMessage>()))
                .Returns((ResponseMessage response) => ItemResponse(Parse(ReadText(response.Content)), response));
            factory.Setup(f => f.CreateItemResponse<ConsumerDates>(It.IsAny<ResponseMessage>()))
                .Returns((ResponseMessage response) => ItemResponse(Serializer.FromStream<ConsumerDates>(response.Content), response));
            Mock<CosmosClient> client = new ();
            client.SetupGet(c => c.ClientOptions).Returns(new CosmosClientOptions { Serializer = Serializer });
            client.SetupGet(c => c.ResponseFactory).Returns(factory.Object);
            Mock<Database> database = new ();
            database.SetupGet(d => d.Client).Returns(client.Object);
            inner = new Mock<Container>();
            inner.SetupGet(c => c.Database).Returns(database.Object);
            return inner.Object.WithEncryptor(encryptor);
        }

        private static ItemResponse<T> ItemResponse<T>(T item, ResponseMessage response)
        {
            Mock<ItemResponse<T>> result = new ();
            result.SetupGet(r => r.Resource).Returns(item);
            result.SetupGet(r => r.StatusCode).Returns(response.StatusCode);
            response.Dispose();
            return result.Object;
        }

        private static void SetupWrites(Mock<Container> inner, EncryptionItemRequestOptions options, Func<Stream, PartitionKey, ResponseMessage> accept)
        {
            inner.Setup(c => c.CreateItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(), options, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, PartitionKey pk, ItemRequestOptions ignored, CancellationToken token) => accept(stream, pk));
            inner.Setup(c => c.ReplaceItemStreamAsync(It.IsAny<Stream>(), "i", It.IsAny<PartitionKey>(), options, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, string id, PartitionKey pk, ItemRequestOptions ignored, CancellationToken token) => accept(stream, pk));
            inner.Setup(c => c.UpsertItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(), options, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, PartitionKey pk, ItemRequestOptions ignored, CancellationToken token) => accept(stream, pk));
        }

        private static Task<ResponseMessage> WriteAsync(Container container, string operation, Stream input, EncryptionItemRequestOptions options, CancellationToken token)
            => operation switch
            {
                "Create" => container.CreateItemStreamAsync(input, new PartitionKey("p"), options, token),
                "Replace" => container.ReplaceItemStreamAsync(input, "i", new PartitionKey("p"), options, token),
                _ => container.UpsertItemStreamAsync(input, new PartitionKey("p"), options, token),
            };

        private static async Task<string> WriteDocumentAsync(string json, string algorithm, ConsumerEncryptor encryptor, params string[] paths)
        {
            encryptor.ExpectedAlgorithm = algorithm;
            Container writer = CreateContainer(encryptor, out Mock<Container> inner);
            EncryptionItemRequestOptions options = WriteOptions(algorithm, paths);
            string stored = null;
            SetupWrites(inner, options, (stream, pk) =>
            {
                stored = ReadText(stream);
                stream.Dispose();
                return Response(HttpStatusCode.Created, "{}");
            });
            using MemoryStream input = Utf8(json);
            using ResponseMessage response = await writer.CreateItemStreamAsync(input, new PartitionKey("p"), options);
            return stored;
        }

        private static async Task<JObject> PublicReadAsync(Container container, ItemRequestOptions options, bool lazy)
        {
            if (!lazy)
            {
                using ResponseMessage response = await container.ReadItemStreamAsync("i", new PartitionKey("p"), options);
                return Parse(await ReadTextAsync(response.Content));
            }

            ItemResponse<DecryptableItem> result = await container.ReadItemAsync<DecryptableItem>("i", new PartitionKey("p"), options);
            try
            {
                (JObject value, _) = await result.Resource.GetItemAsync<JObject>();
                return value;
            }
            finally
            {
                await DisposeItemAsync(result.Resource);
            }
        }

        private static async Task DisposeItemAsync(DecryptableItem item)
        {
            if ((object)item is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }
        }

        private static async Task<Exception> CaptureAsync(Func<Task> operation)
        {
            try
            {
                await operation();
                return null;
            }
            catch (Exception failure)
            {
                return failure;
            }
        }

        public sealed class ConsumerDates
        {
            public string id { get; set; }

            public string PK { get; set; }

            public DateTime Sensitive { get; set; }

            public DateTimeOffset Offset { get; set; }
        }

        private sealed class ConsumerSerializer : CosmosSerializer
        {
            public override T FromStream<T>(Stream stream)
            {
                using (stream)
                using (StreamReader text = new (stream))
                using (JsonTextReader reader = new (text) { DateParseHandling = DateParseHandling.None })
                {
                    return JsonSerializer.CreateDefault().Deserialize<T>(reader);
                }
            }

            public override Stream ToStream<T>(T input) => Utf8(JsonConvert.SerializeObject(input));
        }

        private sealed class ConsumerEncryptor : Encryptor
        {
            private readonly DataEncryptionKey realKey;
            private readonly bool allowKeyAccess;
            private readonly Dictionary<string, byte[]> opaqueCiphertext = new ();

            public ConsumerEncryptor(bool realCipher = false, bool allowKeyAccess = false)
            {
                this.allowKeyAccess = allowKeyAccess;
                if (realCipher)
                {
                    this.realKey = DataEncryptionKey.Create(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray(), Legacy);
                }
            }

            public Func<byte[], CancellationToken, Task<byte[]>> EncryptOverride { get; set; }

            public Func<byte[], CancellationToken, Task<byte[]>> DecryptOverride { get; set; }

            public string ExpectedAlgorithm { get; set; }

            public int EncryptCalls { get; private set; }

            public int DecryptCalls { get; private set; }

            public int KeyCalls { get; private set; }

            public byte[] LastPlaintext { get; private set; }

            public byte[] LastDecryptedPlaintext { get; private set; }

            public void ResetCounts()
            {
                this.EncryptCalls = 0;
                this.DecryptCalls = 0;
                this.KeyCalls = 0;
            }

#if !RELEASED_CUSTOM_PACKAGE
            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                Assert.AreEqual(KeyId, dataEncryptionKeyId);
                Assert.AreEqual(this.ExpectedAlgorithm ?? Legacy, encryptionAlgorithm);
                this.KeyCalls++;
                if (this.allowKeyAccess)
                {
                    return Task.FromResult(this.realKey);
                }

                throw new NotSupportedException("This consumer implements only public byte-array encryption, not direct key access.");
            }
#endif

            public override Task<byte[]> EncryptAsync(byte[] plainText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                Assert.AreEqual(KeyId, dataEncryptionKeyId);
                Assert.AreEqual(this.ExpectedAlgorithm ?? Legacy, encryptionAlgorithm);
                this.EncryptCalls++;
                this.LastPlaintext = plainText.ToArray();
                if (this.EncryptOverride != null)
                {
                    return this.EncryptOverride(plainText, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (this.realKey != null)
                {
                    return Task.FromResult(this.realKey.EncryptData(plainText));
                }

                if (encryptionAlgorithm == Legacy)
                {
                    return Task.FromResult(plainText.ToArray());
                }

                // Opaque one-byte cipher; the SDK owns only its type marker.
                byte[] cipher = new[] { checked((byte)(this.opaqueCiphertext.Count + 0xD0)) };
                this.opaqueCiphertext.Add(Convert.ToBase64String(cipher), plainText.ToArray());
                return Task.FromResult(cipher);
            }

            public override Task<byte[]> DecryptAsync(byte[] cipherText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                Assert.AreEqual(KeyId, dataEncryptionKeyId);
                Assert.AreEqual(this.ExpectedAlgorithm ?? Legacy, encryptionAlgorithm);
                this.DecryptCalls++;
                if (this.DecryptOverride != null)
                {
                    return this.DecryptOverride(cipherText, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                byte[] plaintext = this.realKey != null
                    ? this.realKey.DecryptData(cipherText)
                    : encryptionAlgorithm == Legacy ? cipherText.ToArray() : this.opaqueCiphertext[Convert.ToBase64String(cipherText)].ToArray();
                this.LastDecryptedPlaintext = plaintext.ToArray();
                return Task.FromResult(plaintext);
            }
        }
    }
}
