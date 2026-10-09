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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using H = EncryptionScenarioTestHelpers;

    [TestClass]
    public class EncryptionScenarioOwnershipTests
    {
        [DataTestMethod]
        [DynamicData(nameof(LazyReaderRows), DynamicDataSourceType.Method)]
        public async Task Lazy_UnsupportedEnvelope_RetainsOriginalCiphertextForRecovery(int processor)
        {
            JObject document = H.MdeItem();
            document["_ei"]["_ef"] = 0;
            H.PublicEncryptor encryptor = new();
            Mock<CosmosSerializer> serializer = new(MockBehavior.Strict);
            DecryptableItem item;
#if NET8_0_OR_GREATER
            if (processor == (int)JsonProcessor.Stream)
            {
                item = new StreamDecryptableItem(H.Stream(document.ToString(Formatting.None)), encryptor, serializer.Object);
            }
            else
#endif
            {
                item = new DecryptableItemCore(document, encryptor, serializer.Object);
            }

            try
            {
                EncryptionException failure = await Assert.ThrowsExceptionAsync<EncryptionException>(() => item.GetItemAsync<JObject>());
                Assert.IsInstanceOfType(failure.InnerException, typeof(NotSupportedException));
                Assert.AreEqual(H.KeyId, failure.DataEncryptionKeyId);
                Assert.IsTrue(JToken.DeepEquals(document, JObject.Parse(failure.EncryptedContent)));
                Assert.AreEqual(0, encryptor.DecryptCalls);
                Assert.AreEqual(0, encryptor.KeyCalls);
#if NET8_0_OR_GREATER
                if (processor == (int)JsonProcessor.Stream)
                {
                    await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => item.GetItemAsync<JObject>());
                }
                else
#endif
                {
                    EncryptionException repeated = await Assert.ThrowsExceptionAsync<EncryptionException>(() => item.GetItemAsync<JObject>());
                    Assert.AreEqual(failure.EncryptedContent, repeated.EncryptedContent);
                }
            }
            finally
            {
                await item.DisposeAsync();
            }
        }

        public static IEnumerable<object[]> LazyReaderRows()
            => H.Processors.Select(processor => new object[] { processor });

        [DataTestMethod]
        [DynamicData(nameof(RawDepthRows), DynamicDataSourceType.Method)]
        public async Task Decrypt_RawObjectDepthBoundary_PreservesInputAndProvidedOutput(int depth, int processor)
        {
            string json = string.Concat(Enumerable.Repeat("{\"n\":", depth)) + "0" + new string('}', depth);
            using MemoryStream input = H.Stream(json);
            byte[] before = input.ToArray();
            using MemoryStream output = H.Output();
            byte[] outputBefore = output.ToArray();
            H.PublicEncryptor encryptor = new();
            if (depth == 64)
            {
                DecryptionContext context = await EncryptionProcessor.DecryptAsync(input, output, encryptor,
                    new CosmosDiagnosticsContext(), RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None);
                Assert.IsNull(context);
            }
            else
            {
                InvalidOperationException failure = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
                    await EncryptionProcessor.DecryptAsync(input, output, encryptor, new CosmosDiagnosticsContext(),
                        RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None));
                Assert.AreEqual("The response body must contain a JSON object.", failure.Message);
            }

            CollectionAssert.AreEqual(before, input.ToArray());
            CollectionAssert.AreEqual(outputBefore, output.ToArray());
            Assert.AreEqual(2L, output.Position);
            Assert.IsTrue(input.CanRead);
            Assert.AreEqual(0L, input.Position);
            Assert.AreEqual(0, encryptor.DecryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
        }

        public static IEnumerable<object[]> RawDepthRows()
        {
            foreach (int processor in H.Processors)
            {
                yield return new object[] { 64, processor };
                yield return new object[] { 65, processor };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(StreamAndBatchMetadataRows), DynamicDataSourceType.Method)]
        public async Task StreamOrBatchWrite_PlaintextMetadataDoesNotInheritTypedPointReplacement(bool batchWrite, int processor)
        {
            JObject item = H.PlainItem();
            item["_ei"] = JValue.CreateNull();
            string before = item.ToString(Formatting.None);
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            Mock<TransactionalBatch> batch = new();
            inner.Setup(c => c.CreateTransactionalBatch(new PartitionKey("p"))).Returns(batch.Object);
            using MemoryStream input = H.Stream(before);
            Exception failure;
            if (batchWrite)
            {
                EncryptionTransactionalBatchItemRequestOptions options = new()
                {
                    EncryptionOptions = H.Options(processor).EncryptionOptions,
                    Properties = new Dictionary<string, object> { [JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey] = (JsonProcessor)processor },
                };
                failure = await H.CaptureExceptionAsync(() =>
                {
                    container.CreateTransactionalBatch(new PartitionKey("p")).CreateItemStream(input, options);
                    return Task.CompletedTask;
                });
            }
            else
            {
                failure = await H.CaptureExceptionAsync(async () =>
                {
                    using ResponseMessage response = await container.CreateItemStreamAsync(input, new PartitionKey("p"), H.Options(processor));
                });
            }

            Assert.IsNotNull(failure, "Only typed point writes request plaintext metadata replacement.");
            inner.Verify(c => c.CreateItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
            batch.Verify(b => b.CreateItemStream(It.IsAny<Stream>(), It.IsAny<TransactionalBatchItemRequestOptions>()), Times.Never);
            Assert.IsTrue(input.CanRead);
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(before), input.ToArray());
            Assert.AreEqual(before, item.ToString(Formatting.None));
        }

        public static IEnumerable<object[]> StreamAndBatchMetadataRows()
        {
            foreach (int processor in H.Processors)
            {
                yield return new object[] { false, processor };
                yield return new object[] { true, processor };
            }
        }

        [TestMethod]
        public async Task Batch_LateResponseFailure_DoesNotReplayCommittedOperations()
        {
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            Mock<TransactionalBatch> batch = new();
            JObject invalid = H.MdeItem("bad");
            invalid["_ei"]["_ep"] = new JArray(JValue.CreateNull());
            using MemoryStream first = H.Stream(H.MdeItem("good").ToString(Formatting.None));
            using MemoryStream second = H.Stream(invalid.ToString(Formatting.None));
            Mock<TransactionalBatchOperationResult> firstResult = new();
            firstResult.SetupGet(r => r.ResourceStream).Returns(first);
            Mock<TransactionalBatchOperationResult> secondResult = new();
            secondResult.SetupGet(r => r.ResourceStream).Returns(second);
            List<TransactionalBatchOperationResult> results = new() { firstResult.Object, secondResult.Object };
            Mock<TransactionalBatchResponse> raw = new();
            raw.SetupGet(r => r.IsSuccessStatusCode).Returns(true);
            raw.Setup(r => r.GetEnumerator()).Returns(() => results.GetEnumerator());
            batch.Setup(b => b.ExecuteAsync(It.IsAny<CancellationToken>())).ReturnsAsync(raw.Object);
            inner.Setup(c => c.CreateTransactionalBatch(new PartitionKey("p"))).Returns(batch.Object);

            InvalidOperationException failure = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                using TransactionalBatchResponse response = await container.CreateTransactionalBatch(new PartitionKey("p")).ExecuteAsync();
            });

            Assert.AreEqual(EncryptionMetadataClassifier.InvalidMetadataMessage, failure.Message);
            batch.Verify(b => b.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.AreEqual(1, encryptor.DecryptCalls);
            Assert.IsFalse(first.CanRead);
            Assert.IsTrue(second.CanRead);
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task PublicLazyStreamPage_DisposesSkippedOwnedItemsAndSupportsRepeatedCleanup()
        {
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            container.UseStreamingJsonProcessingByDefault();
            using MemoryStream rawContent = H.Stream(H.Feed(H.MdeItem("first"), H.MdeItem("skipped")).ToString(Formatting.None));
            ResponseMessage raw = new(HttpStatusCode.OK) { Content = rawContent };
            Mock<FeedIterator> iterator = new();
            iterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(raw);
            QueryDefinition query = new("SELECT * FROM c");
            inner.Setup(c => c.GetItemQueryStreamIterator(query, null, (QueryRequestOptions)null)).Returns(iterator.Object);
            FeedResponse<DecryptableItem> page = await container.GetItemQueryIterator<DecryptableItem>(query).ReadNextAsync();
            Assert.IsFalse(rawContent.CanRead, "The response is owned by page construction, not the yielded items.");
            Assert.AreEqual(2, page.Count);
            Assert.AreEqual(0, encryptor.DecryptCalls);
            IAsyncDisposable disposable = (IAsyncDisposable)page;
            await disposable.DisposeAsync();
            await disposable.DisposeAsync();
            foreach (DecryptableItem item in page)
            {
                await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => item.GetItemAsync<JObject>());
                await item.DisposeAsync();
            }

            Assert.AreEqual(0, encryptor.DecryptCalls);
        }
#endif
    }
}
