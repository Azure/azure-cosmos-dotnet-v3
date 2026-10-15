//------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using H = EncryptionScenarioTestHelpers;

    [TestClass]
    public class EncryptionReaderRegressionTests
    {
        private const string DateString = "2024-01-01T10:00:00.000Z";

        [DataTestMethod]
        [DataRow(DateParseHandling.DateTime)]
        [DataRow(DateParseHandling.DateTimeOffset)]
        public void Reader_IgnoresHostDateParsing(DateParseHandling dateParseHandling)
        {
            Func<JsonSerializerSettings> original = JsonConvert.DefaultSettings;
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings { DateParseHandling = dateParseHandling };
            try
            {
                using MemoryStream input = H.Stream("{\"date\":\"" + DateString + "\"}");
                JObject actual = NewtonsoftJsonObjectReader.Read(input);
                Assert.AreEqual(JTokenType.String, actual["date"].Type);
                Assert.AreEqual(DateString, actual["date"].Value<string>());
                Assert.AreEqual(0, input.Position);
                Assert.IsTrue(input.CanRead);
            }
            finally
            {
                JsonConvert.DefaultSettings = original;
            }
        }

        [TestMethod]
        public void Reader_IgnoresHostConverters()
        {
            Func<JsonSerializerSettings> original = JsonConvert.DefaultSettings;
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new RejectingTokenConverter() },
            };
            try
            {
                using MemoryStream input = H.Stream("{\"id\":\"i\"}");
                Assert.AreEqual("i", NewtonsoftJsonObjectReader.Read(input)["id"].Value<string>());
            }
            finally
            {
                JsonConvert.DefaultSettings = original;
            }
        }

        [TestMethod]
        public async Task Reader_HostUnlimitedDepthCannotRemoveSdkLimit()
        {
            Func<JsonSerializerSettings> original = JsonConvert.DefaultSettings;
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings { MaxDepth = null };
            try
            {
                using MemoryStream input = H.Stream("{\"nested\":" + new string('[', 70) + "0" + new string(']', 70) + "}");
                InvalidOperationException failure = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                {
                    NewtonsoftJsonObjectReader.Read(input);
                    return Task.CompletedTask;
                });
                Assert.AreEqual(NewtonsoftJsonObjectReader.InvalidBodyMessage, failure.Message);
                Assert.IsInstanceOfType(failure.InnerException, typeof(JsonException));
                Assert.AreEqual(0, input.Position);
                Assert.IsTrue(input.CanRead);
            }
            finally
            {
                JsonConvert.DefaultSettings = original;
            }
        }

        [TestMethod]
        public async Task PublicMdeWrite_HostDateParsingCannotChangeEncryptedOrPlainStrings()
        {
            Func<JsonSerializerSettings> original = JsonConvert.DefaultSettings;
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings { DateParseHandling = DateParseHandling.DateTimeOffset };
            try
            {
                JObject item = H.PlainItem();
                item["Sensitive"] = DateString;
                item["Plain"] = DateString;
                H.PublicEncryptor encryptor = new();
                Container container = H.Container(encryptor, out Mock<Container> inner, out _);
                SetupWrites(inner, _ => { });
                ItemResponse<JObject> response = await container.CreateItemAsync(item, new PartitionKey("p"), H.Options(0));
                Assert.IsTrue(JToken.DeepEquals(item, response.Resource));
                Assert.AreEqual(JTokenType.String, response.Resource["Sensitive"].Type);
                Assert.AreEqual(JTokenType.String, response.Resource["Plain"].Type);
                Assert.AreEqual(1, encryptor.EncryptCalls);
                Assert.AreEqual(1, encryptor.DecryptCalls);
            }
            finally
            {
                JsonConvert.DefaultSettings = original;
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(TypedWriteCases), DynamicDataSourceType.Method)]
        public async Task PublicTypedMigration_AcceptsForwardOnlySerializer(string operation, int processor)
        {
            ForwardOnlySerializer serializer = new();
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _, serializer);
            JObject submitted = null;
            SetupWrites(inner, document => submitted = document);
            JObject item = H.PlainItem();
            item["_ei"] = JValue.CreateNull();
            EncryptionItemRequestOptions options = H.Options(processor);
            ItemResponse<JObject> response = operation switch
            {
                "Create" => await container.CreateItemAsync(item, new PartitionKey("p"), options),
                "Replace" => await container.ReplaceItemAsync(item, "i", new PartitionKey("p"), options),
                _ => await container.UpsertItemAsync(item, new PartitionKey("p"), options),
            };

            Assert.IsTrue(JToken.DeepEquals(H.PlainItem(), response.Resource));
            Assert.AreEqual(H.Mde, submitted["_ei"]["_ea"].Value<string>());
            Assert.AreEqual(JTokenType.Null, item["_ei"].Type);
            Assert.IsFalse(serializer.LastStream.CanRead);
            Assert.AreEqual(1, encryptor.EncryptCalls);
            Assert.AreEqual(1, encryptor.DecryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
        }

        public static IEnumerable<object[]> TypedWriteCases()
        {
            foreach (int processor in H.Processors)
            {
                foreach (string operation in new[] { "Create", "Replace", "Upsert" })
                {
                    yield return new object[] { operation, processor };
                }
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(CurrentPositionProcessors), DynamicDataSourceType.Method)]
        public async Task PublicTypedMigration_ConsumesStreamFromCurrentPosition(int processor)
        {
            const string prefix = "not part of the JSON:";
            Mock<CosmosSerializer> serializer = new(MockBehavior.Strict);
            serializer.Setup(instance => instance.ToStream(It.IsAny<JObject>()))
                .Returns((JObject item) =>
                {
                    MemoryStream stream = H.Stream(prefix + item.ToString(Formatting.None));
                    stream.Position = Encoding.UTF8.GetByteCount(prefix);
                    return stream;
                });
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _, serializer.Object);
            SetupWrites(inner, _ => { });
            JObject item = H.PlainItem();
            item["_ei"] = JValue.CreateNull();
            ItemResponse<JObject> response = await container.CreateItemAsync(item, new PartitionKey("p"), H.Options(processor));
            Assert.IsTrue(JToken.DeepEquals(H.PlainItem(), response.Resource));
            Assert.AreEqual(1, encryptor.EncryptCalls);
        }

        public static IEnumerable<object[]> CurrentPositionProcessors()
        {
            foreach (int processor in H.Processors)
            {
                yield return new object[] { processor };
            }
        }

        [TestMethod]
        public async Task PublicRawMdeWrite_AcceptsForwardOnlyInput()
        {
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            SetupWrites(inner, _ => { });
            using ForwardOnlyStream input = new(Encoding.UTF8.GetBytes(H.PlainItem().ToString(Formatting.None)));
            using ResponseMessage response = await container.CreateItemStreamAsync(input, new PartitionKey("p"), H.Options(0));
            Assert.IsTrue(JToken.DeepEquals(H.PlainItem(), EncryptionProcessor.BaseSerializer.FromStream<JObject>(response.Content)));
            Assert.IsFalse(input.CanRead);
            Assert.AreEqual(1, encryptor.EncryptCalls);
        }

        [DataTestMethod]
        [DataRow("Create")]
        [DataRow("Replace")]
        [DataRow("Upsert")]
        public void PublicBatchMdeWrite_AcceptsForwardOnlyInput(string operation)
        {
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            Mock<TransactionalBatch> innerBatch = new();
            JObject submitted = null;
            Action<Stream> capture = stream =>
            {
                using (stream)
                {
                    submitted = EncryptionProcessor.BaseSerializer.FromStream<JObject>(stream);
                }
            };
            innerBatch.Setup(innerOperation => innerOperation.CreateItemStream(It.IsAny<Stream>(), It.IsAny<TransactionalBatchItemRequestOptions>()))
                .Callback((Stream stream, TransactionalBatchItemRequestOptions _) => capture(stream)).Returns(innerBatch.Object);
            innerBatch.Setup(innerOperation => innerOperation.ReplaceItemStream(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<TransactionalBatchItemRequestOptions>()))
                .Callback((string _, Stream stream, TransactionalBatchItemRequestOptions ignoredOptions) => capture(stream)).Returns(innerBatch.Object);
            innerBatch.Setup(innerOperation => innerOperation.UpsertItemStream(It.IsAny<Stream>(), It.IsAny<TransactionalBatchItemRequestOptions>()))
                .Callback((Stream stream, TransactionalBatchItemRequestOptions _) => capture(stream)).Returns(innerBatch.Object);
            inner.Setup(instance => instance.CreateTransactionalBatch(new PartitionKey("p"))).Returns(innerBatch.Object);
            TransactionalBatch batch = container.CreateTransactionalBatch(new PartitionKey("p"));
            EncryptionTransactionalBatchItemRequestOptions options = new() { EncryptionOptions = H.Options(0).EncryptionOptions };
            using ForwardOnlyStream input = new(Encoding.UTF8.GetBytes(H.PlainItem().ToString(Formatting.None)));
            switch (operation)
            {
                case "Create": batch.CreateItemStream(input, options); break;
                case "Replace": batch.ReplaceItemStream("i", input, options); break;
                default: batch.UpsertItemStream(input, options); break;
            }

            Assert.AreEqual(H.Mde, submitted["_ei"]["_ea"].Value<string>());
            Assert.IsFalse(input.CanRead);
            Assert.AreEqual(1, encryptor.EncryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
        }

        [TestMethod]
        public async Task PublicUnknownMetadataUpsert_UsesStableGatewayExceptionContract()
        {
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            JObject item = H.PlainItem();
            item["_ei"] = H.Metadata("future-point-migration-algorithm");
            InvalidOperationException failure = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => container.UpsertItemAsync(item, new PartitionKey("p"), H.Options(0)));
            Assert.AreEqual(EncryptionMetadataClassifier.InvalidMetadataMessage, failure.Message);
            Assert.AreEqual(0, encryptor.EncryptCalls);
            inner.Verify(instance => instance.UpsertItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

#if NET8_0_OR_GREATER
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task TypedStreamPreScan_ForwardOnlyCopyFailurePreservesCallerInput(bool cancel)
        {
            using CancellationTokenSource cancellation = new();
            IOException expected = new("forward-only read failed");
            using ForwardOnlyStream input = new(Encoding.UTF8.GetBytes(H.PlainItem().ToString(Formatting.None)))
            {
                BeforeRead = cancel ? cancellation.Cancel : () => throw expected,
            };
            H.PublicEncryptor encryptor = new();
            Exception failure = await H.CaptureExceptionAsync(async () =>
            {
                using Stream result = await EncryptionProcessor.EncryptAsync(
                    input, encryptor, H.Options((int)JsonProcessor.Stream),
                    new CosmosDiagnosticsContext(), cancellation.Token, replacePlaintextEncryptionMetadata: true);
            });
            if (cancel)
            {
                Assert.IsInstanceOfType(failure, typeof(OperationCanceledException));
                Assert.AreEqual(cancellation.Token, ((OperationCanceledException)failure).CancellationToken);
            }
            else
            {
                Assert.AreSame(expected, failure);
            }

            Assert.IsTrue(input.CanRead);
            Assert.AreEqual(0, encryptor.EncryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
        }
#endif

        private static void SetupWrites(Mock<Container> inner, Action<JObject> capture)
        {
            ResponseMessage Accept(Stream stream)
            {
                using (stream)
                {
                    JObject document = EncryptionProcessor.BaseSerializer.FromStream<JObject>(stream);
                    capture(document);
                    return H.Response(HttpStatusCode.Created, document.ToString(Formatting.None));
                }
            }

            inner.Setup(instance => instance.CreateItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, PartitionKey _, ItemRequestOptions options, CancellationToken token) => Accept(stream));
            inner.Setup(instance => instance.ReplaceItemStreamAsync(It.IsAny<Stream>(), "i", It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, string _, PartitionKey pk, ItemRequestOptions options, CancellationToken token) => Accept(stream));
            inner.Setup(instance => instance.UpsertItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, PartitionKey _, ItemRequestOptions options, CancellationToken token) => Accept(stream));
        }

        private sealed class RejectingTokenConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => typeof(JToken).IsAssignableFrom(objectType);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
                => throw new InvalidOperationException("Host JToken converter must not run.");

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                => throw new InvalidOperationException("Host JToken converter must not run.");
        }

        private sealed class ForwardOnlySerializer : CosmosSerializer
        {
            public ForwardOnlyStream LastStream { get; private set; }

            public override T FromStream<T>(Stream stream) => EncryptionProcessor.BaseSerializer.FromStream<T>(stream);

            public override Stream ToStream<T>(T input)
            {
                using MemoryStream serialized = EncryptionProcessor.BaseSerializer.ToStream(input);
                this.LastStream = new ForwardOnlyStream(serialized.ToArray());
                return this.LastStream;
            }
        }

        private sealed class ForwardOnlyStream : MemoryStream
        {
            public ForwardOnlyStream(byte[] bytes) : base(bytes) { }

            public Action BeforeRead { get; set; }

            public override bool CanSeek => false;

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                this.BeforeRead?.Invoke();
                cancellationToken.ThrowIfCancellationRequested();
                return base.ReadAsync(buffer, offset, count, cancellationToken);
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                this.BeforeRead?.Invoke();
                cancellationToken.ThrowIfCancellationRequested();
                return base.ReadAsync(buffer, cancellationToken);
            }
        }
    }
}
