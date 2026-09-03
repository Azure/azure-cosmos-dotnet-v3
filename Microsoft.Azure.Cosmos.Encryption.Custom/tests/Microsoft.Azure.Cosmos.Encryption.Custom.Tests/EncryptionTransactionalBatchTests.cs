//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections;
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
    using Moq.Protected;

    [TestClass]
    public class EncryptionTransactionalBatchTests
    {
        [TestMethod]
        public async Task ExecuteAsync_SnapshotsReusedOptionsForMixedOperationsByIndex()
        {
            Dictionary<string, object> firstProperties = new ()
            {
                { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" },
                { "custom-property", "first" },
            };
            TransactionalBatchItemRequestOptions itemOptions = new ()
            {
                Properties = firstProperties,
            };
            List<TransactionalBatchItemRequestOptions> forwardedOptions = new ();
            EncryptionTransactionalBatch batch = CreateBatch(
                setupOperation: inner =>
                {
                    inner.Setup(b => b.CreateItemStream(
                            It.IsAny<Stream>(),
                            It.IsAny<TransactionalBatchItemRequestOptions>()))
                        .Callback<Stream, TransactionalBatchItemRequestOptions>((_, options) => forwardedOptions.Add(options))
                        .Returns(inner.Object);
                    inner.Setup(b => b.ReadItem(
                            It.IsAny<string>(),
                            It.IsAny<TransactionalBatchItemRequestOptions>()))
                        .Callback<string, TransactionalBatchItemRequestOptions>((_, options) => forwardedOptions.Add(options))
                        .Returns(inner.Object);
                },
                resultCount: 2);
            batch.CreateItemStream(
                new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":\"stream\"}")),
                itemOptions);
            Dictionary<string, object> secondProperties = new ()
            {
                { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Newtonsoft" },
                { "custom-property", "second" },
            };
            itemOptions.Properties = secondProperties;
            batch.ReadItem("newtonsoft", itemOptions);

            List<Activity> activities = await ExecuteAndCaptureActivitiesAsync(() => batch.ExecuteAsync());

            Assert.AreEqual(1, activities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream));
            Assert.AreEqual(1, activities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft));
            Assert.AreEqual(2, forwardedOptions.Count);
            Assert.IsTrue(forwardedOptions.All(options =>
                !ReferenceEquals(options, itemOptions) &&
                !options.Properties.ContainsKey(JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey)));
            Assert.AreEqual("first", forwardedOptions[0].Properties["custom-property"]);
            Assert.AreEqual("second", forwardedOptions[1].Properties["custom-property"]);
            Assert.AreEqual("Stream", firstProperties[JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey]);
            Assert.AreSame(secondProperties, itemOptions.Properties);
            Assert.AreEqual("Newtonsoft", itemOptions.Properties[JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey]);
        }

        [TestMethod]
        public async Task ExecuteAsync_UsesBatchJsonProcessorOverrideAsFallback()
        {
            TransactionalBatchRequestOptions forwardedRequestOptions = null;
            EncryptionTransactionalBatch batch = CreateBatch(
                setupOperation: inner => inner
                    .Setup(b => b.ReadItem("id", null))
                    .Returns(inner.Object),
                onExecuteWithOptions: options => forwardedRequestOptions = options);
            batch.ReadItem("id");
            TransactionalBatchRequestOptions requestOptions = new ()
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" },
                    { "custom-property", "preserved" },
                },
            };

            List<Activity> activities = await ExecuteAndCaptureActivitiesAsync(
                () => batch.ExecuteAsync(requestOptions));

            Assert.IsTrue(activities.Any(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream));
            Assert.AreNotSame(requestOptions, forwardedRequestOptions);
            Assert.AreEqual("preserved", forwardedRequestOptions.Properties["custom-property"]);
            Assert.IsFalse(forwardedRequestOptions.Properties.ContainsKey(
                JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey));
            Assert.AreEqual("Stream", requestOptions.Properties[
                JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey]);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ExecuteAsync_ReusedBatchUsesOnlyCurrentExecutionMetadata(bool useRequestOptions)
        {
            Mock<TransactionalBatchResponse> firstResponse = CreateResponse();
            Mock<TransactionalBatchResponse> secondResponse = CreateResponse();
            Mock<TransactionalBatch> inner = new ();
            inner.Setup(b => b.ReadItem(
                    It.IsAny<string>(),
                    It.IsAny<TransactionalBatchItemRequestOptions>()))
                .Returns(inner.Object);
            if (useRequestOptions)
            {
                inner.SetupSequence(b => b.ExecuteAsync(
                        It.IsAny<TransactionalBatchRequestOptions>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(firstResponse.Object)
                    .ReturnsAsync(secondResponse.Object);
            }
            else
            {
                inner.SetupSequence(b => b.ExecuteAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(firstResponse.Object)
                    .ReturnsAsync(secondResponse.Object);
            }

            EncryptionTransactionalBatch batch = CreateBatch(inner);
            batch.ReadItem("stream", CreateItemOptions(JsonProcessor.Stream));
            List<Activity> firstActivities = await ExecuteAndCaptureActivitiesAsync(
                () => ExecuteAsync(batch, useRequestOptions));

            batch.ReadItem("newtonsoft", CreateItemOptions(JsonProcessor.Newtonsoft));
            List<Activity> secondActivities = await ExecuteAndCaptureActivitiesAsync(
                () => ExecuteAsync(batch, useRequestOptions));

            Assert.AreEqual(1, firstActivities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream));
            Assert.AreEqual(1, secondActivities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft));
        }

        [TestMethod]
        public async Task ExecuteAsync_FailureBeforeInnerDelegationRetainsMetadata()
        {
            Mock<TransactionalBatchResponse> response = CreateResponse();
            Mock<TransactionalBatch> inner = new ();
            inner.Setup(b => b.ReadItem(
                    "stream",
                    It.IsAny<TransactionalBatchItemRequestOptions>()))
                .Returns(inner.Object);
            inner.Setup(b => b.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(response.Object);
            EncryptionTransactionalBatch batch = CreateBatch(inner);
            batch.ReadItem("stream", CreateItemOptions(JsonProcessor.Stream));
            TransactionalBatchRequestOptions requestOptions = new ()
            {
                Properties = new ThrowingReadOnlyDictionary(
                    JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey,
                    JsonProcessor.Newtonsoft),
            };

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => batch.ExecuteAsync(requestOptions));

            Assert.AreEqual("Enumeration failed.", exception.Message);
            inner.Verify(
                b => b.ExecuteAsync(
                    It.IsAny<TransactionalBatchRequestOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Never());
            List<Activity> activities = await ExecuteAndCaptureActivitiesAsync(() => batch.ExecuteAsync());
            Assert.AreEqual(1, activities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream));
        }

        [TestMethod]
        public async Task ExecuteAsync_CancellationAfterDelegationConsumesMetadata()
        {
            Mock<TransactionalBatchResponse> response = CreateResponse();
            Mock<TransactionalBatch> inner = new ();
            inner.Setup(b => b.ReadItem(
                    It.IsAny<string>(),
                    It.IsAny<TransactionalBatchItemRequestOptions>()))
                .Returns(inner.Object);
            inner.SetupSequence(b => b.ExecuteAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromCanceled<TransactionalBatchResponse>(new CancellationToken(canceled: true)))
                .ReturnsAsync(response.Object);
            EncryptionTransactionalBatch batch = CreateBatch(inner);
            batch.ReadItem("cancelled", CreateItemOptions(JsonProcessor.Stream));

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => batch.ExecuteAsync());

            batch.ReadItem("next", CreateItemOptions(JsonProcessor.Newtonsoft));
            List<Activity> activities = await ExecuteAndCaptureActivitiesAsync(() => batch.ExecuteAsync());
            Assert.AreEqual(1, activities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft));
        }

        [TestMethod]
        public async Task ExecuteAsync_ThrownExceptionAfterDelegationConsumesMetadata()
        {
            Mock<TransactionalBatchResponse> response = CreateResponse();
            Mock<TransactionalBatch> inner = new ();
            inner.Setup(b => b.ReadItem(
                    It.IsAny<string>(),
                    It.IsAny<TransactionalBatchItemRequestOptions>()))
                .Returns(inner.Object);
            inner.SetupSequence(b => b.ExecuteAsync(It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Inner execution failed."))
                .ReturnsAsync(response.Object);
            EncryptionTransactionalBatch batch = CreateBatch(inner);
            batch.ReadItem("failed", CreateItemOptions(JsonProcessor.Stream));

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => batch.ExecuteAsync());

            Assert.AreEqual("Inner execution failed.", exception.Message);
            batch.ReadItem("next", CreateItemOptions(JsonProcessor.Newtonsoft));
            List<Activity> activities = await ExecuteAndCaptureActivitiesAsync(() => batch.ExecuteAsync());
            Assert.AreEqual(1, activities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft));
        }

        [TestMethod]
        public async Task ExecuteAsync_FailedServiceResponseConsumesMetadata()
        {
            Mock<TransactionalBatchResponse> failedResponse = CreateResponse(isSuccessStatusCode: false);
            Mock<TransactionalBatchResponse> successfulResponse = CreateResponse();
            Mock<TransactionalBatch> inner = new ();
            inner.Setup(b => b.ReadItem(
                    It.IsAny<string>(),
                    It.IsAny<TransactionalBatchItemRequestOptions>()))
                .Returns(inner.Object);
            inner.SetupSequence(b => b.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(failedResponse.Object)
                .ReturnsAsync(successfulResponse.Object);
            EncryptionTransactionalBatch batch = CreateBatch(inner);
            batch.ReadItem("failed", CreateItemOptions(JsonProcessor.Stream));

            using (TransactionalBatchResponse response = await batch.ExecuteAsync())
            {
                Assert.IsFalse(response.IsSuccessStatusCode);
            }

            batch.ReadItem("next", CreateItemOptions(JsonProcessor.Newtonsoft));
            List<Activity> activities = await ExecuteAndCaptureActivitiesAsync(() => batch.ExecuteAsync());
            Assert.AreEqual(1, activities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft));
        }

        [TestMethod]
        public async Task ExecuteAsync_OverlappingAdditionBelongsToNextExecution()
        {
            Mock<TransactionalBatchResponse> firstResponse = CreateResponse();
            Mock<TransactionalBatchResponse> secondResponse = CreateResponse();
            TaskCompletionSource<TransactionalBatchResponse> firstExecution = new (
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> delegated = new (
                TaskCreationOptions.RunContinuationsAsynchronously);
            int executionCount = 0;
            Mock<TransactionalBatch> inner = new ();
            inner.Setup(b => b.ReadItem(
                    It.IsAny<string>(),
                    It.IsAny<TransactionalBatchItemRequestOptions>()))
                .Returns(inner.Object);
            inner.Setup(b => b.ExecuteAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (Interlocked.Increment(ref executionCount) == 1)
                    {
                        delegated.SetResult(true);
                        return firstExecution.Task;
                    }

                    return Task.FromResult(secondResponse.Object);
                });
            EncryptionTransactionalBatch batch = CreateBatch(inner);
            List<Activity> activities = new ();
            using ActivityListener listener = CreateActivityListener(activities);

            batch.ReadItem("first", CreateItemOptions(JsonProcessor.Stream));
            Task<TransactionalBatchResponse> firstTask = batch.ExecuteAsync();
            await delegated.Task;
            batch.ReadItem("second", CreateItemOptions(JsonProcessor.Newtonsoft));
            firstExecution.SetResult(firstResponse.Object);
            using (await firstTask)
            {
            }

            using (await batch.ExecuteAsync())
            {
            }

            Assert.AreEqual(1, activities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream));
            Assert.AreEqual(1, activities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft));
        }

        [TestMethod]
        public void CreateItemStream_Unencrypted_PreservesCallerStreamAndSanitizesCopiedOptions()
        {
            Stream forwardedStream = null;
            TransactionalBatchItemRequestOptions forwardedRequestOptions = null;
            EncryptionTransactionalBatch batch = CreateBatch(
                setupOperation: inner => inner
                    .Setup(b => b.CreateItemStream(
                        It.IsAny<Stream>(),
                        It.IsAny<TransactionalBatchItemRequestOptions>()))
                    .Callback<Stream, TransactionalBatchItemRequestOptions>((stream, options) =>
                    {
                        forwardedStream = stream;
                        forwardedRequestOptions = options;
                    })
                    .Returns(inner.Object));
            TrackingStream payload = new (Encoding.UTF8.GetBytes("{\"id\":\"doc1\"}"));
            TransactionalBatchItemRequestOptions requestOptions = new ()
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" },
                    { "custom-property", "preserved" },
                },
            };

            batch.CreateItemStream(payload, requestOptions);

            Assert.AreSame(payload, forwardedStream);
            Assert.AreEqual(0, payload.DisposeCount);
            Assert.AreNotSame(requestOptions, forwardedRequestOptions);
            Assert.AreEqual("preserved", forwardedRequestOptions.Properties["custom-property"]);
            Assert.IsFalse(forwardedRequestOptions.Properties.ContainsKey(
                JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey));
            Assert.AreEqual("Stream", requestOptions.Properties[
                JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey]);
        }

        [DataTestMethod]
        [DataRow("Create")]
        [DataRow("Replace")]
        [DataRow("Upsert")]
        public async Task EncryptedWrite_ContainerStreamDefault_UsesNewtonsoftForWriteAndResponse(string operation)
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor("dekId");
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

            List<Activity> activities = await ExecuteAndCaptureActivitiesAsync(async () =>
            {
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

                return await batch.ExecuteAsync();
            });

            Assert.AreEqual(1, activities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeEncryptModeSelectionPrefix + JsonProcessor.Newtonsoft));
            Assert.AreEqual(1, activities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft));
        }

        [TestMethod]
        public async Task EncryptedWrite_ExplicitStream_UsesStreamForWriteAndResponse()
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor("dekId");
            Stream encryptedPayload = null;
            TransactionalBatchItemRequestOptions forwardedRequestOptions = null;
            EncryptionTransactionalBatch batch = CreateBatch(
                setupOperation: inner => inner
                    .Setup(b => b.CreateItemStream(
                        It.IsAny<Stream>(),
                        It.IsAny<TransactionalBatchItemRequestOptions>()))
                    .Callback<Stream, TransactionalBatchItemRequestOptions>((payload, options) =>
                    {
                        encryptedPayload = Copy(payload);
                        forwardedRequestOptions = options;
                    })
                    .Returns(inner.Object),
                encryptor: encryptor.Object,
                resultStreamFactory: _ => Copy(encryptedPayload));
            EncryptionTransactionalBatchItemRequestOptions requestOptions = new ()
            {
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKeyId = "dekId",
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    PathsToEncrypt = new[] { "/Sensitive" },
                },
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" },
                    { "custom-property", "preserved" },
                },
            };
            MemoryStream payload = new (Encoding.UTF8.GetBytes("{\"id\":\"doc1\",\"Sensitive\":\"secret\"}"));

            List<Activity> activities = await ExecuteAndCaptureActivitiesAsync(async () =>
            {
                batch.CreateItemStream(payload, requestOptions);
                return await batch.ExecuteAsync();
            });

            Assert.AreEqual(1, activities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeEncryptModeSelectionPrefix + JsonProcessor.Stream));
            Assert.AreEqual(1, activities.Count(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream));
            Assert.AreEqual("preserved", forwardedRequestOptions.Properties["custom-property"]);
            Assert.IsFalse(forwardedRequestOptions.Properties.ContainsKey(
                JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey));
            Assert.AreEqual("Stream", requestOptions.Properties[
                JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey]);
        }

        [TestMethod]
        public async Task LegacyEncryptedWrite_ContainerStreamDefault_UsesNewtonsoftAndDecryptsResponse()
        {
            const string dekId = "dekId";
            Mock<Encryptor> encryptor = TestEncryptorFactory.CreateLegacy(dekId);
            TestCommon.TestDoc expected = TestCommon.TestDoc.Create();
            Stream encryptedPayload = null;
            EncryptionTransactionalBatch batch = CreateBatch(
                setupOperation: inner => inner
                    .Setup(b => b.CreateItemStream(
                        It.IsAny<Stream>(),
                        It.IsAny<TransactionalBatchItemRequestOptions>()))
                    .Callback<Stream, TransactionalBatchItemRequestOptions>((payload, _) =>
                        encryptedPayload = Copy(payload))
                    .Returns(inner.Object),
                encryptor: encryptor.Object,
                defaultJsonProcessor: JsonProcessor.Stream,
                resultStreamFactory: _ => Copy(encryptedPayload));
            EncryptionTransactionalBatchItemRequestOptions requestOptions = new ()
            {
                EncryptionOptions = CreateLegacyEncryptionOptions(dekId),
            };

            batch.CreateItemStream(expected.ToStream(), requestOptions);
            using TransactionalBatchResponse response = await batch.ExecuteAsync();
            TestCommon.TestDoc actual = TestCommon.FromStream<TestCommon.TestDoc>(
                response[0].ResourceStream);

            Assert.AreEqual(expected, actual);
#pragma warning disable CS0618
            encryptor.Verify(instance => instance.DecryptAsync(
                It.IsAny<byte[]>(),
                dekId,
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                It.IsAny<CancellationToken>()),
                Times.Once);
#pragma warning restore CS0618
        }

        [TestMethod]
        public async Task ReadItem_StreamSelection_DecryptsLegacyCiphertext()
        {
            const string dekId = "dekId";
            Mock<Encryptor> legacyEncryptor = TestEncryptorFactory.CreateLegacy(dekId);
            TestCommon.TestDoc expected = TestCommon.TestDoc.Create();
            Stream encryptedContent = await CreateLegacyEncryptedStreamAsync(
                expected,
                legacyEncryptor.Object,
                dekId);
            EncryptionTransactionalBatch batch = CreateBatch(
                setupOperation: inner => inner
                    .Setup(b => b.ReadItem(
                        expected.Id,
                        It.IsAny<TransactionalBatchItemRequestOptions>()))
                    .Returns(inner.Object),
                encryptor: legacyEncryptor.Object,
                resultStreamFactory: _ => encryptedContent);
            batch.ReadItem(
                expected.Id,
                new TransactionalBatchItemRequestOptions
                {
                    Properties = new Dictionary<string, object>
                    {
                        { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" },
                    },
                });

            using TransactionalBatchResponse response = await batch.ExecuteAsync();

            TestCommon.TestDoc actual = TestCommon.FromStream<TestCommon.TestDoc>(
                response[0].ResourceStream);
            Assert.AreEqual(expected, actual);
#pragma warning disable CS0618
            legacyEncryptor.Verify(instance => instance.DecryptAsync(
                It.IsAny<byte[]>(),
                dekId,
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                It.IsAny<CancellationToken>()),
                Times.Once);
#pragma warning restore CS0618
        }

        [DataTestMethod]
        [DataRow("Create")]
        [DataRow("Replace")]
        [DataRow("Upsert")]
        public void LegacyWrite_ExplicitStream_FailsBeforeBatchOperation(string operation)
        {
            Mock<TransactionalBatch> innerBatch = null;
            EncryptionTransactionalBatch batch = CreateBatch(
                setupOperation: inner => innerBatch = inner,
                encryptor: TestEncryptorFactory.CreateLegacy("dekId").Object);
            EncryptionTransactionalBatchItemRequestOptions requestOptions = new ()
            {
                EncryptionOptions = CreateLegacyEncryptionOptions("dekId"),
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" },
                },
            };
            MemoryStream payload = new (Encoding.UTF8.GetBytes("{\"id\":\"doc1\",\"SensitiveStr\":\"secret\"}"));

            AggregateException exception = Assert.ThrowsException<AggregateException>(() =>
            {
                switch (operation)
                {
                    case "Create":
                        batch.CreateItemStream(payload, requestOptions);
                        break;
                    case "Replace":
                        batch.ReplaceItemStream("doc1", payload, requestOptions);
                        break;
                    case "Upsert":
                        batch.UpsertItemStream(payload, requestOptions);
                        break;
                    default:
                        Assert.Fail($"Unknown operation: {operation}");
                        break;
                }
            });

            Assert.IsInstanceOfType(exception.InnerException, typeof(NotSupportedException));
            NotSupportedException notSupportedException = (NotSupportedException)exception.InnerException;
            StringAssert.Contains(notSupportedException.Message, "AE AES encryption algorithm");
            innerBatch.Verify(
                inner => inner.CreateItemStream(
                    It.IsAny<Stream>(),
                    It.IsAny<TransactionalBatchItemRequestOptions>()),
                Times.Never());
            innerBatch.Verify(
                inner => inner.ReplaceItemStream(
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<TransactionalBatchItemRequestOptions>()),
                Times.Never());
            innerBatch.Verify(
                inner => inner.UpsertItemStream(
                    It.IsAny<Stream>(),
                    It.IsAny<TransactionalBatchItemRequestOptions>()),
                Times.Never());
        }

        [TestMethod]
        public void Dispose_DisposesReplacementStreamAndInnerResponseOnce()
        {
            TrackingStream originalStream = new (Encoding.UTF8.GetBytes("{\"id\":\"doc1\"}"));
            TrackingStream decryptedStream = new (Encoding.UTF8.GetBytes("{\"id\":\"doc1\",\"Sensitive\":\"secret\"}"));
            Mock<TransactionalBatchOperationResult> originalResult = new ();
            originalResult.SetupGet(result => result.ResourceStream).Returns(originalStream);
            EncryptionTransactionalBatchOperationResult decryptedResult = new (originalResult.Object, decryptedStream);
            int innerDisposeCount = 0;
            Mock<TransactionalBatchResponse> innerResponse = new ();
            innerResponse.Protected()
                .Setup("Dispose", ItExpr.IsAny<bool>())
                .Callback(() =>
                {
                    innerDisposeCount++;
                    originalStream.Dispose();
                });
            EncryptionTransactionalBatchResponse response = new (
                new[] { decryptedResult },
                innerResponse.Object,
                Mock.Of<CosmosSerializer>());

            response.Dispose();
            response.Dispose();

            Assert.AreEqual(1, decryptedStream.DisposeCount);
            Assert.AreEqual(1, originalStream.DisposeCount);
            Assert.AreEqual(1, innerDisposeCount);
            Assert.IsNull(decryptedResult.ResourceStream);
        }

        [TestMethod]
        public void Dispose_WhenReplacementCleanupThrows_ContinuesAndIsIdempotent()
        {
            TrackingStream throwingStream = new (
                Encoding.UTF8.GetBytes("{\"id\":\"doc1\"}"),
                new IOException("replacement cleanup failed"));
            TrackingStream secondStream = new (Encoding.UTF8.GetBytes("{\"id\":\"doc2\"}"));
            Mock<TransactionalBatchOperationResult> firstOriginalResult = new ();
            Mock<TransactionalBatchOperationResult> secondOriginalResult = new ();
            int innerDisposeCount = 0;
            Mock<TransactionalBatchResponse> innerResponse = new ();
            innerResponse.Protected()
                .Setup("Dispose", ItExpr.IsAny<bool>())
                .Callback(() => innerDisposeCount++);
            EncryptionTransactionalBatchResponse response = new (
                new TransactionalBatchOperationResult[]
                {
                    new EncryptionTransactionalBatchOperationResult(firstOriginalResult.Object, throwingStream),
                    new EncryptionTransactionalBatchOperationResult(secondOriginalResult.Object, secondStream),
                },
                innerResponse.Object,
                Mock.Of<CosmosSerializer>());

            IOException exception = Assert.ThrowsException<IOException>(() => response.Dispose());
            response.Dispose();

            Assert.AreEqual("replacement cleanup failed", exception.Message);
            Assert.AreEqual(1, throwingStream.DisposeCount);
            Assert.AreEqual(1, secondStream.DisposeCount);
            Assert.AreEqual(1, innerDisposeCount);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(2)]
        public async Task ExecuteAsync_WhenResultCountDoesNotMatchOperations_DisposesInnerResponse(
            int resultCount)
        {
            int innerDisposeCount = 0;
            Mock<TransactionalBatchResponse> mismatchedResponse = CreateResponse(
                resultCount: resultCount,
                setupResponse: response => response.Protected()
                    .Setup("Dispose", ItExpr.IsAny<bool>())
                    .Callback(() => innerDisposeCount++));
            Mock<TransactionalBatchResponse> successfulResponse = CreateResponse();
            Mock<TransactionalBatch> inner = new ();
            inner.Setup(b => b.ReadItem(
                    It.IsAny<string>(),
                    It.IsAny<TransactionalBatchItemRequestOptions>()))
                .Returns(inner.Object);
            inner.SetupSequence(b => b.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mismatchedResponse.Object)
                .ReturnsAsync(successfulResponse.Object);
            EncryptionTransactionalBatch batch = CreateBatch(inner);
            batch.ReadItem("id");

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => batch.ExecuteAsync());

            StringAssert.Contains(exception.Message, $"{resultCount} operation results for 1 operations");
            Assert.AreEqual(1, innerDisposeCount);

            batch.ReadItem("next");
            using TransactionalBatchResponse response = await batch.ExecuteAsync();
            Assert.AreEqual(1, response.Count);
        }

        [TestMethod]
        public async Task ExecuteAsync_WhenLaterResultDecryptionFails_ConsumesMetadataAndDisposesInnerResponse()
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor("dekId");
            TrackingStream encryptedStream = await CreateTrackingEncryptedPayloadAsync(encryptor.Object);
            MemoryStream malformedStream = new (Encoding.UTF8.GetBytes("{not-json"));
            int innerDisposeCount = 0;
            Mock<TransactionalBatchResponse> failedResponse = CreateResponse(
                resultCount: 2,
                resultStreamFactory: index => index == 0 ? encryptedStream : malformedStream,
                setupResponse: response => response.Protected()
                    .Setup("Dispose", ItExpr.IsAny<bool>())
                    .Callback(() =>
                    {
                        innerDisposeCount++;
                        throw new IOException("cleanup failure");
                    }));
            Mock<TransactionalBatchResponse> successfulResponse = CreateResponse();
            Mock<TransactionalBatch> inner = new ();
            inner.Setup(b => b.ReadItem(
                    It.IsAny<string>(),
                    It.IsAny<TransactionalBatchItemRequestOptions>()))
                .Returns(inner.Object);
            inner.SetupSequence(b => b.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(failedResponse.Object)
                .ReturnsAsync(successfulResponse.Object);
            EncryptionTransactionalBatch batch = CreateBatch(inner, encryptor.Object);
            batch.ReadItem("valid");
            batch.ReadItem("invalid");

            Newtonsoft.Json.JsonReaderException exception =
                await Assert.ThrowsExceptionAsync<Newtonsoft.Json.JsonReaderException>(
                () => batch.ExecuteAsync());

            StringAssert.Contains(exception.Message, "Invalid JavaScript");
            Assert.AreEqual(1, innerDisposeCount);
            Assert.AreEqual(1, encryptedStream.DisposeCount);

            batch.ReadItem("next", CreateItemOptions(JsonProcessor.Newtonsoft));
            using TransactionalBatchResponse response = await batch.ExecuteAsync();
            Assert.AreEqual(1, response.Count);
        }

        private static EncryptionTransactionalBatch CreateBatch(
            Mock<TransactionalBatch> inner,
            Encryptor encryptor = null,
            JsonProcessor defaultJsonProcessor = JsonProcessor.Newtonsoft)
        {
            return new EncryptionTransactionalBatch(
                inner.Object,
                encryptor ?? Mock.Of<Encryptor>(),
                Mock.Of<CosmosSerializer>(),
                defaultJsonProcessor);
        }

        private static Mock<Encryptor> CreateMdeEncryptor(string dekId)
        {
            Mock<DataEncryptionKey> dataEncryptionKey = new ();
            dataEncryptionKey.SetupGet(key => key.EncryptionAlgorithm)
                .Returns(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);
            dataEncryptionKey.Setup(key => key.GetEncryptByteCount(It.IsAny<int>()))
                .Returns<int>(length => length);
            dataEncryptionKey.Setup(key => key.GetDecryptByteCount(It.IsAny<int>()))
                .Returns<int>(length => length);
            dataEncryptionKey.Setup(key => key.EncryptData(It.IsAny<byte[]>()))
                .Returns<byte[]>(TestCommon.EncryptData);
            dataEncryptionKey.Setup(key => key.EncryptData(
                    It.IsAny<byte[]>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<int>()))
                .Returns((byte[] input, int offset, int length, byte[] output, int outputOffset) =>
                    TestCommon.EncryptData(input, offset, length, output, outputOffset));
            dataEncryptionKey.Setup(key => key.DecryptData(It.IsAny<byte[]>()))
                .Returns<byte[]>(TestCommon.DecryptData);
            dataEncryptionKey.Setup(key => key.DecryptData(
                    It.IsAny<byte[]>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<int>()))
                .Returns((byte[] input, int offset, int length, byte[] output, int outputOffset) =>
                    TestCommon.DecryptData(input, offset, length, output, outputOffset));

            Mock<Encryptor> encryptor = new ();
            encryptor.Setup(instance => instance.GetEncryptionKeyAsync(
                    dekId,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(dataEncryptionKey.Object);
            encryptor.Setup(instance => instance.EncryptAsync(
                    It.IsAny<byte[]>(),
                    dekId,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] plaintext, string _, string _, CancellationToken _) =>
                    TestCommon.EncryptData(plaintext));
            encryptor.Setup(instance => instance.DecryptAsync(
                    It.IsAny<byte[]>(),
                    dekId,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] ciphertext, string _, string _, CancellationToken _) =>
                    TestCommon.DecryptData(ciphertext));
            return encryptor;
        }

        private static EncryptionTransactionalBatch CreateBatch(
            System.Action<Mock<TransactionalBatch>> setupOperation,
            int resultCount = 1,
            Encryptor encryptor = null,
            JsonProcessor defaultJsonProcessor = JsonProcessor.Newtonsoft,
            System.Func<int, Stream> resultStreamFactory = null,
            System.Action<Mock<TransactionalBatchResponse>> setupResponse = null,
            System.Action<TransactionalBatchRequestOptions> onExecuteWithOptions = null)
        {
            Mock<TransactionalBatchResponse> response = CreateResponse(
                resultCount,
                resultStreamFactory: resultStreamFactory,
                setupResponse: setupResponse);

            Mock<TransactionalBatch> inner = new ();
            setupOperation(inner);
            inner.Setup(b => b.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(response.Object);
            inner.Setup(b => b.ExecuteAsync(It.IsAny<TransactionalBatchRequestOptions>(), It.IsAny<CancellationToken>()))
                .Callback<TransactionalBatchRequestOptions, CancellationToken>(
                    (options, _) => onExecuteWithOptions?.Invoke(options))
                .ReturnsAsync(response.Object);

            return new EncryptionTransactionalBatch(
                inner.Object,
                encryptor ?? Mock.Of<Encryptor>(),
                Mock.Of<CosmosSerializer>(),
                defaultJsonProcessor);
        }

        private static Mock<TransactionalBatchResponse> CreateResponse(
            int resultCount = 1,
            bool isSuccessStatusCode = true,
            System.Func<int, Stream> resultStreamFactory = null,
            System.Action<Mock<TransactionalBatchResponse>> setupResponse = null)
        {
            List<TransactionalBatchOperationResult> results = new ();
            for (int index = 0; index < resultCount; index++)
            {
                int resultIndex = index;
                Mock<TransactionalBatchOperationResult> result = new ();
                result.SetupGet(r => r.ResourceStream)
                    .Returns(() => resultStreamFactory?.Invoke(resultIndex)
                        ?? new MemoryStream(Encoding.UTF8.GetBytes($"{{\"id\":\"doc{resultIndex}\"}}")));
                result.SetupGet(r => r.StatusCode)
                    .Returns(isSuccessStatusCode ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
                results.Add(result.Object);
            }

            Mock<TransactionalBatchResponse> response = new ();
            response.SetupGet(r => r.IsSuccessStatusCode).Returns(isSuccessStatusCode);
            response.SetupGet(r => r.StatusCode)
                .Returns(isSuccessStatusCode ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
            response.SetupGet(r => r.Count).Returns(results.Count);
            response.Setup(r => r.GetEnumerator())
                .Returns(() => results.GetEnumerator());
            setupResponse?.Invoke(response);
            return response;
        }

        private static TransactionalBatchItemRequestOptions CreateItemOptions(JsonProcessor jsonProcessor)
        {
            return new TransactionalBatchItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, jsonProcessor },
                },
            };
        }

        private static Task<TransactionalBatchResponse> ExecuteAsync(
            EncryptionTransactionalBatch batch,
            bool useRequestOptions)
        {
            return useRequestOptions
                ? batch.ExecuteAsync(new TransactionalBatchRequestOptions())
                : batch.ExecuteAsync();
        }

        private static EncryptionOptions CreateLegacyEncryptionOptions(string dekId)
        {
#pragma warning disable CS0618
            return new EncryptionOptions
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = TestCommon.TestDoc.PathsToEncrypt,
            };
#pragma warning restore CS0618
        }

        private static async Task<Stream> CreateLegacyEncryptedStreamAsync(
            TestCommon.TestDoc document,
            Encryptor encryptor,
            string dekId)
        {
            return await EncryptionProcessor.EncryptAsync(
                document.ToStream(),
                encryptor,
                new EncryptionTransactionalBatchItemRequestOptions
                {
                    EncryptionOptions = CreateLegacyEncryptionOptions(dekId),
                },
                new CosmosDiagnosticsContext(),
                CancellationToken.None);
        }

        private static async Task<TrackingStream> CreateTrackingEncryptedPayloadAsync(Encryptor encryptor)
        {
            MemoryStream input = new (Encoding.UTF8.GetBytes("{\"id\":\"doc1\",\"Sensitive\":\"secret\"}"));
            Stream encrypted = await EncryptionProcessor.EncryptAsync(
                input,
                encryptor,
                new EncryptionTransactionalBatchItemRequestOptions
                {
                    EncryptionOptions = new EncryptionOptions
                    {
                        DataEncryptionKeyId = "dekId",
                        EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                        PathsToEncrypt = new[] { "/Sensitive" },
                    },
                },
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            try
            {
                encrypted.Position = 0;
                using MemoryStream buffer = new ();
                await encrypted.CopyToAsync(buffer);
                return new TrackingStream(buffer.ToArray());
            }
            finally
            {
                encrypted.Dispose();
            }
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
            using ActivityListener listener = CreateActivityListener(activities);

            using TransactionalBatchResponse response = await execute();
            return activities;
        }

        private static ActivityListener CreateActivityListener(List<Activity> activities)
        {
            ActivityListener listener = new ()
            {
                ShouldListenTo = source => source.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => activities.Add(activity),
            };
            ActivitySource.AddActivityListener(listener);
            return listener;
        }

        private sealed class ThrowingReadOnlyDictionary : IReadOnlyDictionary<string, object>
        {
            private readonly string key;
            private readonly object value;

            public ThrowingReadOnlyDictionary(string key, object value)
            {
                this.key = key;
                this.value = value;
            }

            public object this[string key] => key == this.key
                ? this.value
                : throw new KeyNotFoundException();

            public IEnumerable<string> Keys => new[] { this.key };

            public IEnumerable<object> Values => new[] { this.value };

            public int Count => 1;

            public bool ContainsKey(string key) => key == this.key;

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                throw new InvalidOperationException("Enumeration failed.");
            }

            public bool TryGetValue(string key, out object value)
            {
                value = key == this.key ? this.value : null;
                return key == this.key;
            }

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }

        private sealed class TrackingStream : MemoryStream
        {
            private readonly Exception disposeException;
            private bool disposed;

            public TrackingStream(
                byte[] buffer,
                Exception disposeException = null)
                : base(buffer)
            {
                this.disposeException = disposeException;
            }

            public int DisposeCount { get; private set; }

            public override ValueTask DisposeAsync()
            {
                if (!this.disposed)
                {
                    this.disposed = true;
                    this.DisposeCount++;
                    base.Dispose(true);
                    return this.disposeException == null
                        ? default
                        : ValueTask.FromException(this.disposeException);
                }

                return base.DisposeAsync();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && !this.disposed)
                {
                    this.disposed = true;
                    this.DisposeCount++;
                    base.Dispose(disposing);
                    if (this.disposeException != null)
                    {
                        throw this.disposeException;
                    }

                    return;
                }

                base.Dispose(disposing);
            }
        }
    }
}
#endif
