// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.DistributedTransaction
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

    /// <summary>
    /// Verifies that document bodies embedded in text JSON requests stay valid text JSON
    /// when the client serializer is configured with SerializerOptions and binary encoding is enabled.
    /// These tests verify SDK serialization and mocked transport, not the Cosmos DB service.
    /// </summary>
    [TestClass]
    [DoNotParallelize] // Mutates a process wide environment variable.
    public class DistributedTransactionBinaryEncodingTests
    {
        private const byte BinaryFormatSignature = 0x80;

        private static readonly CosmosSerializationOptions CamelCaseOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
        };

        [TestMethod]
        [DataRow("Create", false, false)]
        [DataRow("Create", false, true)]
        [DataRow("Create", true, false)]
        [DataRow("Create", true, true)]
        [DataRow("Replace", false, false)]
        [DataRow("Replace", false, true)]
        [DataRow("Replace", true, false)]
        [DataRow("Replace", true, true)]
        [DataRow("Upsert", false, false)]
        [DataRow("Upsert", false, true)]
        [DataRow("Upsert", true, false)]
        [DataRow("Upsert", true, true)]
        public async Task WriteOperation_ResourceBodyIsValidTextJson(
            string operationName,
            bool binaryEncodingEnabled,
            bool useSerializerOptions)
        {
            OperationType operationType = (OperationType)Enum.Parse(typeof(OperationType), operationName);
            TestDocument document = new TestDocument
            {
                Id = "doc1",
                Pk = "pk1",
                DisplayName = "name",
                Details = new TestDetails { InnerValue = 7 },
            };

            JsonElement operation = await WithBinaryEncodingAsync(binaryEncodingEnabled, async () =>
            {
                CosmosSerializerCore serializerCore = CosmosSerializerCore.Create(
                    customSerializer: null,
                    serializationOptions: useSerializerOptions ? CamelCaseOptions : null);

                return await SerializeSingleOperationAsync(
                    serializerCore,
                    new DistributedTransactionOperation<TestDocument>(
                        operationType,
                        operationIndex: 0,
                        database: "db",
                        container: "container",
                        partitionKey: new PartitionKey("pk1"),
                        id: "doc1",
                        resource: document));
            });

            JsonElement body = operation.GetProperty("resourceBody");
            string idName = "id";
            string displayName = useSerializerOptions ? "displayName" : "DisplayName";
            string detailsName = useSerializerOptions ? "details" : "Details";
            string innerName = useSerializerOptions ? "innerValue" : "InnerValue";

            Assert.AreEqual("doc1", body.GetProperty(idName).GetString());
            Assert.AreEqual("pk1", body.GetProperty(useSerializerOptions ? "pk" : "Pk").GetString());
            Assert.AreEqual("name", body.GetProperty(displayName).GetString());
            Assert.AreEqual(7, body.GetProperty(detailsName).GetProperty(innerName).GetInt32());
            Assert.IsFalse(body.TryGetProperty(useSerializerOptions ? "DisplayName" : "displayName", out _));
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task StreamResourceBody_BinaryOrTextInput_IsValidTextJson(
            bool binaryInput,
            bool useSerializerOptions)
        {
            byte[] payload = binaryInput
                ? CreateBinaryPayload("{\"id\":\"doc1\",\"nested\":{\"value\":3}}")
                : Encoding.UTF8.GetBytes("{\"id\":\"doc1\",\"nested\":{\"value\":3}}");

            foreach (OperationType operationType in new[] { OperationType.Create, OperationType.Replace, OperationType.Upsert })
            {
                foreach (bool seekable in new[] { false, true })
                {
                    using Stream resourceStream = seekable ? new MemoryStream(payload) : new TrackingNonSeekableStream(payload);
                    CosmosSerializerCore serializerCore = CosmosSerializerCore.Create(
                        customSerializer: null,
                        serializationOptions: useSerializerOptions ? CamelCaseOptions : null);
                    DistributedTransactionOperation streamOperation = new DistributedTransactionOperation(
                        operationType,
                        operationIndex: 0,
                        database: "db",
                        container: "container",
                        partitionKey: new PartitionKey("pk1"),
                        id: "doc1")
                    {
                        ResourceStream = resourceStream,
                    };
                    await streamOperation.MaterializeResourceAsync(serializerCore, CancellationToken.None);
                    Assert.IsTrue(resourceStream.CanRead, "Caller-supplied streams must remain open after materialization.");
                    JsonElement operation = await SerializeSingleOperationAsync(
                        serializerCore,
                        streamOperation);

                    JsonElement body = operation.GetProperty("resourceBody");
                    Assert.AreEqual("doc1", body.GetProperty("id").GetString());
                    Assert.AreEqual(3, body.GetProperty("nested").GetProperty("value").GetInt32());
                }
            }
        }

        [TestMethod]
        public async Task DistributedTransactionSerialization_DoesNotChangeOtherSerializationModes()
        {
            WriterTrackingConverter converter = new WriterTrackingConverter();
            CosmosSerializerCore serializerCore = new CosmosSerializerCore(new CosmosJsonDotNetSerializer(
                new Newtonsoft.Json.JsonSerializerSettings { Converters = { converter } },
                binaryEncodingEnabled: true));
            ConvertedValue value = new ConvertedValue();
            PatchSpec patchSpec = new PatchSpec(
                new List<PatchOperation> { PatchOperation.Set("/value", value) },
                new PatchItemRequestOptions());
            QueryDefinition query = new QueryDefinition("SELECT @Value").WithParameter("@Value", value);

            for (int pass = 0; pass < 2; pass++)
            {
                using (Stream stream = serializerCore.ToStream(patchSpec))
                {
                    Assert.AreEqual(false, converter.UsedTextWriter, "Default Patch serialization must retain its configured mode.");
                }

                using (Stream stream = serializerCore.ToStreamSqlQuerySpec(query.ToSqlQuerySpec(), ResourceType.Document))
                {
                    Assert.AreEqual(false, converter.UsedTextWriter, "Query serialization must retain its configured mode.");
                }

                using (ItemBatchOperation batchOperation = new ItemBatchOperation<PatchSpec>(
                    OperationType.Patch, 0, new PartitionKey("pk1"), patchSpec, id: "doc1"))
                {
                    await SerializeBatchOperationAsync(serializerCore, batchOperation);
                    Assert.AreEqual(false, converter.UsedTextWriter, "Batch Patch serialization must retain its configured mode.");
                }

                await SerializeSingleOperationAsync(
                    serializerCore,
                    new DistributedTransactionOperation<ConvertedValue>(
                        OperationType.Create, 0, "db", "container", new PartitionKey("pk1"), "doc1", value));
                Assert.AreEqual(true, converter.UsedTextWriter, "DTx documents should select the text-only mode.");

                JsonElement dtxPatch = await SerializeSingleOperationAsync(
                    serializerCore,
                    new DistributedTransactionOperation<PatchSpec>(
                        OperationType.Patch, 0, "db", "container", new PartitionKey("pk1"), "doc1", patchSpec));
                Assert.AreEqual(true, converter.UsedTextWriter, "DTx Patch must select text without changing other callers.");
                Assert.AreEqual("value", dtxPatch.GetProperty("resourceBody").GetProperty("operations")[0].GetProperty("value").GetString());
            }
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task Patch_ValuesAreValidTextJson(bool binaryEncodingEnabled, bool useSerializerOptions)
        {
            JsonElement operation = await WithBinaryEncodingAsync(binaryEncodingEnabled, async () =>
            {
                CosmosSerializerCore serializerCore = CosmosSerializerCore.Create(
                    null, useSerializerOptions ? CamelCaseOptions : null);
                PatchSpec patchSpec = new PatchSpec(
                    new List<PatchOperation>
                    {
                        PatchOperation.Add("/details", new TestDetails { InnerValue = 7 }),
                        PatchOperation.Set("/name", "text"),
                        PatchOperation.Replace("/count", 42),
                        PatchOperation.Set("/enabled", true),
                        PatchOperation.Set<object>("/nullable", null),
                        PatchOperation.Increment("/increment", 1.5),
                        PatchOperation.Remove("/old"),
                        PatchOperation.Move("/source", "/destination"),
                    },
                    new PatchItemRequestOptions());
                return await SerializeSingleOperationAsync(
                    serializerCore,
                    new DistributedTransactionOperation<PatchSpec>(
                        OperationType.Patch, 0, "db", "container", new PartitionKey("pk1"), "doc1", patchSpec,
                        new DistributedTransactionPatchItemRequestOptions { FilterPredicate = "FROM c WHERE c.count < 10" }));
            });

            JsonElement body = operation.GetProperty("resourceBody");
            Assert.AreEqual("FROM c WHERE c.count < 10", body.GetProperty("condition").GetString());
            JsonElement values = body.GetProperty("operations");
            Assert.AreEqual(8, values.GetArrayLength());
            JsonElement details = values[0].GetProperty("value");
            Assert.AreEqual(7, details.GetProperty(useSerializerOptions ? "innerValue" : "InnerValue").GetInt32());
            Assert.IsFalse(details.TryGetProperty(useSerializerOptions ? "InnerValue" : "innerValue", out _));
            Assert.AreEqual("text", values[1].GetProperty("value").GetString());
            Assert.AreEqual(42, values[2].GetProperty("value").GetInt32());
            Assert.IsTrue(values[3].GetProperty("value").GetBoolean());
            Assert.AreEqual(JsonValueKind.Null, values[4].GetProperty("value").ValueKind);
            Assert.AreEqual(1.5, values[5].GetProperty("value").GetDouble());
            Assert.AreEqual("incr", values[5].GetProperty("op").GetString());
            Assert.AreEqual("remove", values[6].GetProperty("op").GetString());
            Assert.IsFalse(values[6].TryGetProperty("value", out _));
            Assert.AreEqual("move", values[7].GetProperty("op").GetString());
            Assert.AreEqual("/source", values[7].GetProperty("from").GetString());
            Assert.AreEqual("/destination", values[7].GetProperty("path").GetString());
            Assert.IsFalse(values[7].TryGetProperty("value", out _));
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task Patch_ValueStream_RespectsPositionAndDisposesInput(bool binaryInput, bool seekable)
        {
            byte[] payload = binaryInput ? CreateBinaryPayload("\"value\"") : Encoding.UTF8.GetBytes("\"value\"");
            byte[] padded = new byte[payload.Length + 1];
            Array.Copy(payload, 0, padded, 1, payload.Length);
            using Stream valueStream = seekable ? new MemoryStream(padded) : new TrackingNonSeekableStream(payload);
            if (seekable)
            {
                valueStream.Position = 1;
            }

            JsonElement operation = await SerializeSingleOperationAsync(
                CosmosSerializerCore.Create(null, CamelCaseOptions),
                new DistributedTransactionOperation<PatchSpec>(
                    OperationType.Patch, 0, "db", "container", new PartitionKey("pk1"), "doc1",
                    new PatchSpec(new List<PatchOperation> { PatchOperation.Set("/s", valueStream) }, new PatchItemRequestOptions())));

            Assert.AreEqual("value", operation.GetProperty("resourceBody").GetProperty("operations")[0].GetProperty("value").GetString());
            if (valueStream is TrackingNonSeekableStream tracked)
            {
                Assert.IsTrue(tracked.IsDisposed, "Patch owns value streams and must dispose them.");
            }
            else
            {
                Assert.IsFalse(valueStream.CanRead, "Patch owns value streams and must dispose them.");
            }
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task Patch_ResourceStream_ProducesTextWithoutTakingOwnership(bool binaryInput, bool seekable)
        {
            const string json = "{\"condition\":\"FROM c WHERE c.count < 10\",\"operations\":[{\"op\":\"set\",\"path\":\"/count\",\"value\":42}]}";
            byte[] payload = binaryInput ? CreateBinaryPayload(json) : Encoding.UTF8.GetBytes(json);
            using Stream stream = seekable ? new MemoryStream(payload) : new TrackingNonSeekableStream(payload);
            DistributedTransactionOperation operation = new DistributedTransactionOperation(
                OperationType.Patch, 0, "db", "container", new PartitionKey("pk1"), "doc1")
            {
                ResourceStream = stream,
            };
            CosmosSerializerCore serializerCore = CosmosSerializerCore.Create(null, CamelCaseOptions);
            await operation.MaterializeResourceAsync(serializerCore, CancellationToken.None);
            Assert.IsTrue(stream.CanRead, "Raw request streams remain caller-owned.");
            if (stream is TrackingNonSeekableStream tracked)
            {
                Assert.IsFalse(tracked.IsDisposed);
            }

            JsonElement serialized = await SerializeSingleOperationAsync(serializerCore, operation);
            JsonElement body = serialized.GetProperty("resourceBody");
            Assert.AreEqual("FROM c WHERE c.count < 10", body.GetProperty("condition").GetString());
            Assert.AreEqual(42, body.GetProperty("operations")[0].GetProperty("value").GetInt32());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task DistributedTransaction_UsesConfiguredTextWriterWithoutBinaryRoundTrip(bool useCamelCase)
        {
            await WithBinaryEncodingAsync(true, async () =>
            {
                CosmosSerializerCore serializerCore = CosmosSerializerCore.Create(
                    customSerializer: null,
                    serializationOptions: new CosmosSerializationOptions
                    {
                        PropertyNamingPolicy = useCamelCase ? CosmosPropertyNamingPolicy.CamelCase : CosmosPropertyNamingPolicy.Default,
                    });
                TextWriterProbe value = new TextWriterProbe();
                string propertyName = useCamelCase ? "innerValue" : "InnerValue";

                foreach (OperationType operationType in new[] { OperationType.Create, OperationType.Replace, OperationType.Upsert })
                {
                    DistributedTransactionOperation operation = new DistributedTransactionOperation<TextWriterProbe>(
                        operationType, 0, "db", "container", new PartitionKey("pk1"), "doc1", value);
                    JsonElement serialized = await SerializeSingleOperationAsync(serializerCore, operation);
                    Assert.AreEqual(7, serialized.GetProperty("resourceBody").GetProperty(propertyName).GetInt32());
                    Assert.AreEqual((byte)'{', operation.ResourceBody.Span[0]);
                }

                JsonElement patch = await SerializeSingleOperationAsync(
                    serializerCore,
                    new DistributedTransactionOperation<PatchSpec>(
                        OperationType.Patch, 0, "db", "container", new PartitionKey("pk1"), "doc1",
                        new PatchSpec(new List<PatchOperation> { PatchOperation.Set("/probe", value) }, new PatchItemRequestOptions())));
                Assert.AreEqual(7, patch.GetProperty("resourceBody").GetProperty("operations")[0].GetProperty("value").GetProperty(propertyName).GetInt32());
                return true;
            });
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task DistributedTransaction_PreservesCustomSerializerAndConverter(bool binaryOutput)
        {
            TrackingSerializer serializer = new TrackingSerializer(new CosmosJsonDotNetSerializer(
                new Newtonsoft.Json.JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
                    Converters = { new ConvertedValueConverter() },
                },
                binaryEncodingEnabled: binaryOutput));
            CosmosSerializerCore serializerCore = new CosmosSerializerCore(serializer);
            ConvertedValue value = new ConvertedValue();

            foreach (OperationType operationType in new[] { OperationType.Create, OperationType.Replace, OperationType.Upsert })
            {
                DistributedTransactionOperation operation = new DistributedTransactionOperation<ConvertedValue>(
                    operationType, 0, "db", "container", new PartitionKey("pk1"), "doc1", value);
                try
                {
                    await operation.MaterializeResourceAsync(serializerCore, CancellationToken.None);
                    Assert.IsTrue(serializer.Streams.TrueForAll(stream => stream.IsDisposed), "The SDK must dispose owned serializer streams before test cleanup.");
                    JsonElement serialized = await SerializeSingleOperationAsync(serializerCore, operation);
                    AssertConvertedValue(serialized.GetProperty("resourceBody"));
                }
                finally
                {
                    operation.ResourceStream?.Dispose();
                }
            }

            DistributedTransactionOperation patchOperation = new DistributedTransactionOperation<PatchSpec>(
                OperationType.Patch, 0, "db", "container", new PartitionKey("pk1"), "doc1",
                new PatchSpec(new List<PatchOperation> { PatchOperation.Set("/value", value) }, new PatchItemRequestOptions()));
            try
            {
                await patchOperation.MaterializeResourceAsync(serializerCore, CancellationToken.None);
                Assert.IsFalse(patchOperation.ResourceStream.CanRead, "The SDK must dispose the materialized PatchSpec stream.");
                Assert.IsTrue(serializer.Streams.TrueForAll(stream => stream.IsDisposed), "Patch value streams must be disposed before test cleanup.");
                JsonElement patch = await SerializeSingleOperationAsync(serializerCore, patchOperation);
                AssertConvertedValue(patch.GetProperty("resourceBody").GetProperty("operations")[0].GetProperty("value"));
            }
            finally
            {
                patchOperation.ResourceStream?.Dispose();
            }

            Assert.AreEqual(4, serializer.Streams.Count);
            Assert.IsTrue(serializer.Streams.TrueForAll(stream => stream.IsDisposed), "Owned serializer streams must be disposed.");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task DistributedTransaction_InvalidBinary_DisposesOwnedSerializerStream(bool patch)
        {
            byte[] payload = CreateBinaryPayload("{\"id\":\"doc1\",\"value\":42}");
            Array.Resize(ref payload, payload.Length - 1);
            using TrackingNonSeekableStream stream = new TrackingNonSeekableStream(payload);
            Mock<CosmosSerializer> serializer = new Mock<CosmosSerializer>(MockBehavior.Strict);
            serializer.Setup(instance => instance.ToStream(It.IsAny<ConvertedValue>())).Returns(stream);
            CosmosSerializerCore serializerCore = new CosmosSerializerCore(serializer.Object);
            DistributedTransactionOperation operation = patch
                ? new DistributedTransactionOperation<PatchSpec>(
                    OperationType.Patch, 0, "db", "container", new PartitionKey("pk1"), "doc1",
                    new PatchSpec(new List<PatchOperation> { PatchOperation.Set("/value", new ConvertedValue()) }, new PatchItemRequestOptions()))
                : new DistributedTransactionOperation<ConvertedValue>(
                    OperationType.Create, 0, "db", "container", new PartitionKey("pk1"), "doc1", new ConvertedValue());

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => DistributedTransactionServerRequest.CreateAsync(
                new List<DistributedTransactionOperation> { operation },
                serializerCore,
                CancellationToken.None,
                tracksDispatch: false));

            Assert.IsTrue(stream.IsDisposed, "Conversion failures must also release the SDK-owned serializer stream.");
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task DeleteAndPatchRemoveMove_RemainValidTextJson(
            bool binaryEncodingEnabled,
            bool useSerializerOptions)
        {
            JsonElement deleteOperation = await WithBinaryEncodingAsync(binaryEncodingEnabled, async () =>
                await SerializeSingleOperationAsync(
                    CosmosSerializerCore.Create(customSerializer: null, serializationOptions: useSerializerOptions ? CamelCaseOptions : null),
                    new DistributedTransactionOperation(
                        OperationType.Delete,
                        operationIndex: 0,
                        database: "db",
                        container: "container",
                        partitionKey: new PartitionKey("pk1"),
                        id: "doc1")));

            Assert.IsFalse(deleteOperation.TryGetProperty("resourceBody", out _));

            JsonElement removeOperation = await WithBinaryEncodingAsync(binaryEncodingEnabled, async () =>
                await SerializeSingleOperationAsync(
                    CosmosSerializerCore.Create(customSerializer: null, serializationOptions: useSerializerOptions ? CamelCaseOptions : null),
                    new DistributedTransactionOperation<PatchSpec>(
                        OperationType.Patch,
                        operationIndex: 0,
                        database: "db",
                        container: "container",
                        partitionKey: new PartitionKey("pk1"),
                        id: "doc1",
                        resource: new PatchSpec(
                            new List<PatchOperation> { PatchOperation.Remove("/old"), PatchOperation.Move("/source", "/destination") },
                            new PatchItemRequestOptions()))));

            JsonElement patchOperation = removeOperation.GetProperty("resourceBody").GetProperty("operations")[0];
            Assert.AreEqual("remove", patchOperation.GetProperty("op").GetString());
            Assert.AreEqual("/old", patchOperation.GetProperty("path").GetString());
            Assert.IsFalse(patchOperation.TryGetProperty("value", out _));
            JsonElement move = removeOperation.GetProperty("resourceBody").GetProperty("operations")[1];
            Assert.AreEqual("move", move.GetProperty("op").GetString());
            Assert.AreEqual("/source", move.GetProperty("from").GetString());
            Assert.AreEqual("/destination", move.GetProperty("path").GetString());
            Assert.IsFalse(move.TryGetProperty("value", out _));
        }

        [TestMethod]
        public void PointOperationSerialization_WithSerializerOptionsAndBinaryEncoding_RemainsBinary()
        {
            byte[] bytes = WithBinaryEncoding(true, () =>
            {
                CosmosSerializerCore serializerCore = CosmosSerializerCore.Create(
                    customSerializer: null,
                    serializationOptions: CamelCaseOptions);

                using Stream stream = serializerCore.ToStream(
                    new TestDocument { Id = "doc1", Pk = "pk1", DisplayName = "name" },
                    canUseBinaryEncodingForPointOperations: true);
                return ReadAll(stream);
            });

            Assert.AreEqual(BinaryFormatSignature, bytes[0]);
        }

        [TestMethod]
        [DataRow("Create")]
        [DataRow("Replace")]
        [DataRow("Upsert")]
        public async Task PointDocuments_WithSerializerOptions_KeepBinaryTransport(string operationName)
        {
            await WithBinaryEncodingAsync(true, async () =>
            {
                OperationType operationType = (OperationType)Enum.Parse(typeof(OperationType), operationName);
                int requests = 0;
                TestHandler handler = new TestHandler((request, cancellationToken) =>
                {
                    Assert.AreEqual(operationType, request.OperationType);
                    byte[] body = ReadAll(request.Content);
                    Assert.AreEqual(BinaryFormatSignature, body[0]);
                    Assert.AreEqual(SupportedSerializationFormats.CosmosBinary.ToString(),
                        request.Headers[HttpConstants.HttpHeaders.SupportedSerializationFormats]);
                    Assert.IsTrue(CosmosSerializationUtil.TryConvertBinaryToText(body, out byte[] text));
                    using JsonDocument document = JsonDocument.Parse(text);
                    Assert.AreEqual("name", document.RootElement.GetProperty("displayName").GetString());
                    requests++;
                    return Task.FromResult(new ResponseMessage(HttpStatusCode.OK));
                });
                using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(builder =>
                    builder.AddCustomHandlers(handler).WithSerializerOptions(CamelCaseOptions));
                Container container = client.GetContainer("db", "container");
                TestDocument item = new TestDocument { Id = "doc1", Pk = "pk1", DisplayName = "name" };
                ItemRequestOptions options = new ItemRequestOptions { EnableContentResponseOnWrite = false };
                ItemResponse<TestDocument> response;
                switch (operationType)
                {
                    case OperationType.Create:
                        response = await container.CreateItemAsync(item, new PartitionKey("pk1"), options);
                        break;
                    case OperationType.Replace:
                        response = await container.ReplaceItemAsync(item, "doc1", new PartitionKey("pk1"), options);
                        break;
                    case OperationType.Upsert:
                        response = await container.UpsertItemAsync(item, new PartitionKey("pk1"), options);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(operationName));
                }

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(1, requests);
                return true;
            });
        }

        [TestMethod]
        [DataRow("Create")]
        [DataRow("Replace")]
        [DataRow("Upsert")]
        public async Task TransactionalBatch_DocumentWithSerializerOptions_RemainsBinary(string operationName)
        {
            await WithBinaryEncodingAsync(true, async () =>
            {
                CosmosSerializerCore serializerCore = CosmosSerializerCore.Create(null, CamelCaseOptions);
                using ItemBatchOperation operation = new ItemBatchOperation<TestDocument>(
                    (OperationType)Enum.Parse(typeof(OperationType), operationName), 0, new PartitionKey("pk1"),
                    new TestDocument { Id = "doc1", DisplayName = "name" }, id: "doc1");
                byte[] body = await SerializeBatchOperationAsync(serializerCore, operation);
                Assert.AreEqual(BinaryFormatSignature, body[0]);
                Assert.IsTrue(CosmosSerializationUtil.TryConvertBinaryToText(body, out byte[] text));
                using JsonDocument document = JsonDocument.Parse(text);
                Assert.AreEqual("name", document.RootElement.GetProperty("displayName").GetString());
                return true;
            });
        }

        [TestMethod]
        public void TryConvertBinaryToText_TextEmptyAndBinaryContent()
        {
            byte[] text = Encoding.UTF8.GetBytes("{\"a\":1}");
            Assert.IsFalse(CosmosSerializationUtil.TryConvertBinaryToText(text, out byte[] unchanged));
            Assert.IsNull(unchanged);
            Assert.AreEqual(0x7B, text[0], "Input must not be modified.");

            Assert.IsFalse(CosmosSerializationUtil.TryConvertBinaryToText(ReadOnlyMemory<byte>.Empty, out _));

            byte[] binary = CreateBinaryPayload("{\"a\":1}");
            byte[] binaryCopy = (byte[])binary.Clone();
            Assert.IsTrue(CosmosSerializationUtil.TryConvertBinaryToText(binary, out byte[] converted));
            CollectionAssert.AreEqual(binaryCopy, binary, "Input must not be modified.");
            Assert.AreEqual("{\"a\":1}", Encoding.UTF8.GetString(converted));

            // A binary payload located inside a larger buffer must be handled by its offset and length.
            byte[] padded = new byte[binary.Length + 10];
            Array.Copy(binary, 0, padded, 5, binary.Length);
            Assert.IsTrue(CosmosSerializationUtil.TryConvertBinaryToText(new ReadOnlyMemory<byte>(padded, 5, binary.Length), out byte[] convertedFromSlice));
            Assert.AreEqual("{\"a\":1}", Encoding.UTF8.GetString(convertedFromSlice));
        }

        private static void AssertConvertedValue(JsonElement value)
        {
            Assert.AreEqual("custom", value.GetProperty("converterOutput").GetString());
            Assert.IsFalse(value.TryGetProperty("OriginalValue", out _));
        }

        private static async Task<byte[]> SerializeBatchOperationAsync(CosmosSerializerCore serializerCore, ItemBatchOperation operation)
        {
            SinglePartitionKeyServerBatchRequest request = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                new PartitionKey("pk1"), new ArraySegment<ItemBatchOperation>(new[] { operation }),
                serializerCore, NoOpTrace.Singleton, CancellationToken.None);
            using MemoryStream stream = request.TransferBodyStream();
            List<ItemBatchOperation> operations = await new BatchRequestPayloadReader().ReadPayloadAsync(stream);
            Assert.AreEqual(1, operations.Count);
            using ItemBatchOperation parsed = operations[0];
            Assert.AreEqual(operation.OperationType, parsed.OperationType);
            return parsed.ResourceBody.ToArray();
        }

        private static async Task<JsonElement> SerializeSingleOperationAsync(
            CosmosSerializerCore serializerCore,
            DistributedTransactionOperation operation)
        {
            try
            {
                DistributedTransactionServerRequest request = await DistributedTransactionServerRequest.CreateAsync(
                    new List<DistributedTransactionOperation> { operation },
                    serializerCore,
                    CancellationToken.None,
                    tracksDispatch: false);

                using MemoryStream bodyStream = request.CreateBodyStream();
                using MemoryStream replayStream = request.CreateBodyStream();
                CollectionAssert.AreEqual(bodyStream.ToArray(), replayStream.ToArray(), "Retries must reuse identical request bytes.");

                // Parse the complete request, not just an independently materialized resource.
                using JsonDocument document = JsonDocument.Parse(bodyStream.ToArray());
                return document.RootElement.GetProperty("operations")[0].Clone();
            }
            finally
            {
                operation.ResourceStream?.Dispose();
            }
        }

        private static byte[] CreateBinaryPayload(string json)
        {
            using Stream stream = CosmosSerializationUtil.ConvertToStreamUsingJsonSerializationFormat(
                Encoding.UTF8.GetBytes(json),
                JsonSerializationFormat.Binary);
            byte[] bytes = ReadAll(stream);
            Assert.AreEqual(BinaryFormatSignature, bytes[0]);
            return bytes;
        }

        private static byte[] ReadAll(Stream stream)
        {
            using MemoryStream buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        private static T WithBinaryEncoding<T>(bool enabled, Func<T> action)
        {
            string original = Environment.GetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled);
            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, enabled ? "True" : "False");
                return action();
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, original);
            }
        }

        private static async Task<T> WithBinaryEncodingAsync<T>(bool enabled, Func<Task<T>> action)
        {
            string original = Environment.GetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled);
            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, enabled ? "True" : "False");
                return await action();
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, original);
            }
        }

        private sealed class TestDocument
        {
            [Newtonsoft.Json.JsonProperty("id")]
            public string Id { get; set; }

            public string Pk { get; set; }

            public string DisplayName { get; set; }

            public TestDetails Details { get; set; }
        }

        private sealed class TestDetails
        {
            public int InnerValue { get; set; }
        }

        private sealed class ConvertedValue
        {
            public string OriginalValue { get; set; } = "must not appear";
        }

        [Newtonsoft.Json.JsonConverter(typeof(TextWriterProbeConverter))]
        private sealed class TextWriterProbe
        {
        }

        private sealed class TextWriterProbeConverter : Newtonsoft.Json.JsonConverter<TextWriterProbe>
        {
            public override void WriteJson(Newtonsoft.Json.JsonWriter writer, TextWriterProbe value, Newtonsoft.Json.JsonSerializer serializer)
            {
                Assert.IsInstanceOfType(writer, typeof(Newtonsoft.Json.JsonTextWriter), "Text-only requests must not first serialize to binary.");
                serializer.Serialize(writer, new TestDetails { InnerValue = 7 });
            }

            public override TextWriterProbe ReadJson(
                Newtonsoft.Json.JsonReader reader,
                Type objectType,
                TextWriterProbe existingValue,
                bool hasExistingValue,
                Newtonsoft.Json.JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class TrackingSerializer : CosmosSerializer
        {
            private readonly CosmosSerializer inner;

            public TrackingSerializer(CosmosSerializer inner)
            {
                this.inner = inner;
            }

            public List<TrackingNonSeekableStream> Streams { get; } = new List<TrackingNonSeekableStream>();

            public override T FromStream<T>(Stream stream)
            {
                throw new NotSupportedException();
            }

            public override Stream ToStream<T>(T input)
            {
                using Stream serialized = this.inner.ToStream(input);
                TrackingNonSeekableStream stream = new TrackingNonSeekableStream(ReadAll(serialized));
                this.Streams.Add(stream);
                return stream;
            }
        }

        private sealed class TrackingNonSeekableStream : CosmosBufferedStreamWrapperTests.NonSeekableMemoryStream
        {
            public TrackingNonSeekableStream(byte[] content)
                : base(content)
            {
            }

            public bool IsDisposed { get; private set; }

            protected override void Dispose(bool disposing)
            {
                this.IsDisposed = true;
                base.Dispose(disposing);
            }
        }

        private sealed class ConvertedValueConverter : Newtonsoft.Json.JsonConverter<ConvertedValue>
        {
            public override void WriteJson(Newtonsoft.Json.JsonWriter writer, ConvertedValue value, Newtonsoft.Json.JsonSerializer serializer)
            {
                serializer.Serialize(writer, new { ConverterOutput = "custom" });
            }

            public override ConvertedValue ReadJson(
                Newtonsoft.Json.JsonReader reader,
                Type objectType,
                ConvertedValue existingValue,
                bool hasExistingValue,
                Newtonsoft.Json.JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class WriterTrackingConverter : Newtonsoft.Json.JsonConverter<ConvertedValue>
        {
            public bool? UsedTextWriter { get; private set; }

            public override void WriteJson(Newtonsoft.Json.JsonWriter writer, ConvertedValue value, Newtonsoft.Json.JsonSerializer serializer)
            {
                this.UsedTextWriter = writer is Newtonsoft.Json.JsonTextWriter;
                writer.WriteValue("value");
            }

            public override ConvertedValue ReadJson(
                Newtonsoft.Json.JsonReader reader,
                Type objectType,
                ConvertedValue existingValue,
                bool hasExistingValue,
                Newtonsoft.Json.JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }
        }
    }
}
