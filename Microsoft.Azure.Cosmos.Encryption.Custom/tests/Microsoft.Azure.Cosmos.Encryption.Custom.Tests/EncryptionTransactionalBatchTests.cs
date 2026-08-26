//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class EncryptionTransactionalBatchTests
    {
        [TestMethod]
        public async Task ExecuteAsync_SnapshotsReusedPerOperationOverridesByIndex()
        {
            TransactionalBatchItemRequestOptions itemOptions = new ()
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" },
                },
            };
            List<TransactionalBatchItemRequestOptions> forwardedOptions = new ();
            EncryptionTransactionalBatch batch = CreateBatch(
                setupOperation: inner => inner
                    .Setup(b => b.ReadItem(It.IsAny<string>(), It.IsAny<TransactionalBatchItemRequestOptions>()))
                    .Callback<string, TransactionalBatchItemRequestOptions>((_, options) => forwardedOptions.Add(options))
                    .Returns(inner.Object),
                resultCount: 2);
            batch.ReadItem("stream", itemOptions);
            itemOptions.Properties = new Dictionary<string, object>
            {
                { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Newtonsoft" },
            };
            batch.ReadItem("newtonsoft", itemOptions);

            List<Activity> activities = await ExecuteAndCaptureActivitiesAsync(() => batch.ExecuteAsync());

            Assert.AreEqual(1, activities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream));
            Assert.AreEqual(1, activities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft));
            Assert.AreEqual(2, forwardedOptions.Count);
            Assert.IsTrue(forwardedOptions.All(options => options.Properties == null));
            Assert.AreEqual("Newtonsoft", itemOptions.Properties[JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey]);
        }

        [TestMethod]
        public async Task ExecuteAsync_UsesBatchJsonProcessorOverrideAsFallback()
        {
            EncryptionTransactionalBatch batch = CreateBatch(
                setupOperation: inner => inner
                    .Setup(b => b.ReadItem("id", null))
                    .Returns(inner.Object));
            batch.ReadItem("id");
            TransactionalBatchRequestOptions requestOptions = new ()
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" },
                },
            };

            List<Activity> activities = await ExecuteAndCaptureActivitiesAsync(
                () => batch.ExecuteAsync(requestOptions));

            Assert.IsTrue(activities.Any(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream));
        }

        [DataTestMethod]
        [DataRow("Create")]
        [DataRow("Replace")]
        [DataRow("Upsert")]
        public async Task EncryptedWrite_ContainerStreamDefault_UsesNewtonsoftForWriteAndResponse(string operation)
        {
            Mock<Encryptor> encryptor = TestEncryptorFactory.CreateLegacy("dekId");
            Stream encryptedPayload = null;
            EncryptionTransactionalBatch batch = CreateBatch(
                setupOperation: inner =>
                {
                    switch (operation)
                    {
                        case "Create":
                            inner.Setup(b => b.CreateItemStream(It.IsAny<Stream>(), It.IsAny<TransactionalBatchItemRequestOptions>()))
                                .Callback<Stream, TransactionalBatchItemRequestOptions>((payload, _) => encryptedPayload = Copy(payload))
                                .Returns(inner.Object);
                            break;
                        case "Replace":
                            inner.Setup(b => b.ReplaceItemStream("id", It.IsAny<Stream>(), It.IsAny<TransactionalBatchItemRequestOptions>()))
                                .Callback<string, Stream, TransactionalBatchItemRequestOptions>((_, payload, _) => encryptedPayload = Copy(payload))
                                .Returns(inner.Object);
                            break;
                        case "Upsert":
                            inner.Setup(b => b.UpsertItemStream(It.IsAny<Stream>(), It.IsAny<TransactionalBatchItemRequestOptions>()))
                                .Callback<Stream, TransactionalBatchItemRequestOptions>((payload, _) => encryptedPayload = Copy(payload))
                                .Returns(inner.Object);
                            break;
                    }
                },
                encryptor: encryptor.Object,
                defaultJsonProcessor: JsonProcessor.Stream,
                resultStreamFactory: _ => Copy(encryptedPayload));
            EncryptionTransactionalBatchItemRequestOptions requestOptions = new ()
            {
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKeyId = "dekId",
#pragma warning disable CS0618
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618
                    PathsToEncrypt = new[] { "/Sensitive" },
                },
            };
            MemoryStream payload = new (Encoding.UTF8.GetBytes("{\"id\":\"doc1\",\"Sensitive\":\"secret\"}"));

            switch (operation)
            {
                case "Create":
                    batch.CreateItemStream(payload, requestOptions);
                    break;
                case "Replace":
                    batch.ReplaceItemStream("id", payload, requestOptions);
                    break;
                case "Upsert":
                    batch.UpsertItemStream(payload, requestOptions);
                    break;
            }

            using TransactionalBatchResponse response = await batch.ExecuteAsync();
            Assert.IsTrue(response.IsSuccessStatusCode);
            encryptor.Verify(instance => instance.DecryptAsync(
                It.IsAny<byte[]>(),
                "dekId",
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private static EncryptionTransactionalBatch CreateBatch(
            System.Action<Mock<TransactionalBatch>> setupOperation,
            int resultCount = 1,
            Encryptor encryptor = null,
            JsonProcessor defaultJsonProcessor = JsonProcessor.Newtonsoft,
            System.Func<int, Stream> resultStreamFactory = null)
        {
            List<TransactionalBatchOperationResult> results = new ();
            for (int index = 0; index < resultCount; index++)
            {
                Mock<TransactionalBatchOperationResult> result = new ();
                result.SetupGet(r => r.ResourceStream)
                    .Returns(() => resultStreamFactory?.Invoke(index)
                        ?? new MemoryStream(Encoding.UTF8.GetBytes($"{{\"id\":\"doc{index}\"}}")));
                result.SetupGet(r => r.StatusCode).Returns(HttpStatusCode.OK);
                results.Add(result.Object);
            }

            Mock<TransactionalBatchResponse> response = new ();
            response.SetupGet(r => r.IsSuccessStatusCode).Returns(true);
            response.Setup(r => r.GetEnumerator())
                .Returns(() => results.GetEnumerator());

            Mock<TransactionalBatch> inner = new ();
            setupOperation(inner);
            inner.Setup(b => b.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(response.Object);
            inner.Setup(b => b.ExecuteAsync(It.IsAny<TransactionalBatchRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response.Object);

            return new EncryptionTransactionalBatch(
                inner.Object,
                encryptor ?? Mock.Of<Encryptor>(),
                Mock.Of<CosmosSerializer>(),
                defaultJsonProcessor);
        }

        private static MemoryStream Copy(Stream source)
        {
            long position = source.Position;
            source.Position = 0;
            MemoryStream copy = new ();
            source.CopyTo(copy);
            source.Position = position;
            copy.Position = 0;
            return copy;
        }

        private static async Task<List<Activity>> ExecuteAndCaptureActivitiesAsync(
            System.Func<Task<TransactionalBatchResponse>> execute)
        {
            List<Activity> activities = new ();
            using ActivityListener listener = new ()
            {
                ShouldListenTo = source => source.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => activities.Add(activity),
            };
            ActivitySource.AddActivityListener(listener);

            using TransactionalBatchResponse response = await execute();
            return activities;
        }
    }
}
#endif
