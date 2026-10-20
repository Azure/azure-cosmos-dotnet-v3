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
        [DynamicData(nameof(PointPassThroughRows), DynamicDataSourceType.Method)]
        public async Task PointWrite_EmptyPaths_PreservesOriginalStreamIdentity(int operation, int processor, bool serviceFails)
        {
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            using MemoryStream input = H.Stream(H.PlainItem().ToString(Formatting.None));
            EncryptionItemRequestOptions options = H.Options(processor);
            options.EncryptionOptions.PathsToEncrypt = Array.Empty<string>();
            IOException primary = new("service failure");
            using ResponseMessage raw = new(HttpStatusCode.OK);
            Stream sent = null;
            SetupPointService(inner, operation, (payload, _) =>
            {
                sent = payload;
                return serviceFails ? Task.FromException<ResponseMessage>(primary) : Task.FromResult(raw);
            });

            Exception failure = await H.CaptureExceptionAsync(async () =>
            {
                ResponseMessage returned = await InvokePointAsync(container, operation, input, options, CancellationToken.None);
                Assert.AreSame(raw, returned);
            });

            Assert.AreSame(serviceFails ? primary : null, failure);
            Assert.AreSame(input, sent, "An untransformed request must retain the original SDK pass-through stream.");
            Assert.IsTrue(input.CanRead, "No transformed output was created, so input ownership must stay with the caller.");
            Assert.AreEqual(0, encryptor.EncryptCalls);
        }

        public static IEnumerable<object[]> PointPassThroughRows()
        {
            foreach (int processor in H.Processors)
            {
                for (int operation = 0; operation < 3; operation++)
                {
                    yield return new object[] { operation, processor, false };
                    yield return new object[] { operation, processor, true };
                }
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(PointWriteRows), DynamicDataSourceType.Method)]
        public async Task PointWrite_ServiceOutcome_RespectsStreamOwnership(int operation, int processor, string algorithm, int outcome)
        {
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            using CancellationTokenSource cancellation = new();
            Exception primary = outcome == 1
                ? new OperationCanceledException(cancellation.Token)
                : new IOException("service failure");
            using MemoryStream input = H.Stream(H.PlainItem().ToString(Formatting.None));
            Stream sent = null;
            bool readableDuringService = false;
            ResponseMessage raw = new(outcome == 2 ? HttpStatusCode.BadRequest : HttpStatusCode.OK);
            Task<ResponseMessage> Service(Stream payload, CancellationToken token)
            {
                sent = payload;
                readableDuringService = input.CanRead;
                Assert.IsTrue(payload.CanRead);
                Assert.AreEqual(cancellation.Token, token);
                if (outcome == 1)
                {
                    cancellation.Cancel();
                }

                return outcome < 2 ? Task.FromException<ResponseMessage>(primary) : Task.FromResult(raw);
            }

            SetupPointService(inner, operation, Service);
            ResponseMessage returned = null;
            try
            {
                Exception failure = await H.CaptureExceptionAsync(async () =>
                    returned = await InvokePointAsync(container, operation, input, H.Options(processor, algorithm), cancellation.Token));
                if (outcome < 2)
                {
                    Assert.AreSame(primary, failure);
                }
                else
                {
                    Assert.IsNull(failure);
                    Assert.AreSame(raw, returned);
                }

                Assert.IsTrue(readableDuringService, "The processor must not close borrowed input before the service outcome.");
                Assert.AreEqual(outcome != 3, input.CanRead, "Only a successful write transfers the original processor success-disposal behavior.");
                Assert.IsNotNull(sent);
                Assert.AreNotSame(input, sent);
                Assert.IsFalse(sent.CanRead, "Transformed output must be released before harness cleanup.");
            }
            finally
            {
                raw.Dispose();
                sent?.Dispose();
            }
        }

        public static IEnumerable<object[]> PointWriteRows()
        {
            foreach (int processor in H.Processors)
            {
                foreach (string algorithm in new[] { H.Mde, H.Legacy })
                {
                    if (algorithm == H.Legacy && processor != (int)JsonProcessor.Newtonsoft)
                    {
                        continue;
                    }

                    for (int operation = 0; operation < 3; operation++)
                    {
                        for (int outcome = 0; outcome < 4; outcome++)
                        {
                            yield return new object[] { operation, processor, algorithm, outcome };
                        }
                    }
                }
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(PointResponseFailureRows), DynamicDataSourceType.Method)]
        public async Task PointResponse_TransformFailure_ReleasesUnreturnedResponse(int operation, int processor, bool canceled, bool cleanupThrows)
        {
            using CancellationTokenSource cancellation = new();
            Exception primary = canceled ? new OperationCanceledException(cancellation.Token) : new IOException("decrypt failure");
            H.PublicEncryptor encryptor = new()
            {
                OnDecrypt = (_, _, token) =>
                {
                    Assert.AreEqual(cancellation.Token, token);
                    if (canceled)
                    {
                        cancellation.Cancel();
                    }

                    return Task.FromException<byte[]>(primary);
                },
            };
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            using MemoryStream input = H.Stream(H.PlainItem().ToString(Formatting.None));
            using MemoryStream content = H.Stream(H.MdeItem().ToString(Formatting.None));
            TrackingPointResponse raw = new(cleanupThrows) { Content = content };
            Stream sent = null;
            SetupPointService(inner, operation, (payload, _) =>
            {
                sent = payload;
                return Task.FromResult<ResponseMessage>(raw);
            });
            try
            {
                Exception failure = await H.CaptureExceptionAsync(async () =>
                    await InvokePointAsync(container, operation, input, H.Options(processor), cancellation.Token));
                Assert.AreSame(primary, failure, "Cleanup must preserve the processing exception and cancellation token.");
                Assert.IsTrue(raw.Disposed, "Acquired response must be released before harness cleanup.");
                Assert.IsFalse(content.CanRead);
                if (sent != null)
                {
                    Assert.IsFalse(sent.CanRead, "Transformed request must be released before harness cleanup.");
                }
            }
            finally
            {
                raw.ThrowOnDispose = false;
                raw.Dispose();
                sent?.Dispose();
            }
        }

        public static IEnumerable<object[]> PointResponseFailureRows()
        {
            foreach (int processor in H.Processors)
            {
                for (int operation = 0; operation < 4; operation++)
                {
                    foreach (bool canceled in new[] { false, true })
                    {
                        foreach (bool cleanupThrows in new[] { false, true })
                        {
                            yield return new object[] { operation, processor, canceled, cleanupThrows };
                        }
                    }
                }
            }
        }

        private static Task<ResponseMessage> InvokePointAsync(Container container, int operation, Stream input,
            ItemRequestOptions options, CancellationToken token)
        {
            return operation switch
            {
                0 => container.CreateItemStreamAsync(input, new PartitionKey("p"), options, token),
                1 => container.ReplaceItemStreamAsync(input, "i", new PartitionKey("p"), options, token),
                2 => container.UpsertItemStreamAsync(input, new PartitionKey("p"), options, token),
                _ => container.ReadItemStreamAsync("i", new PartitionKey("p"), options, token),
            };
        }

        private static void SetupPointService(Mock<Container> inner, int operation, Func<Stream, CancellationToken, Task<ResponseMessage>> service)
        {
            if (operation == 0)
            {
                inner.Setup(c => c.CreateItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .Returns((Stream input, PartitionKey _, ItemRequestOptions _, CancellationToken token) => service(input, token));
            }
            else if (operation == 1)
            {
                inner.Setup(c => c.ReplaceItemStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .Returns((Stream input, string _, PartitionKey _, ItemRequestOptions _, CancellationToken token) => service(input, token));
            }
            else if (operation == 2)
            {
                inner.Setup(c => c.UpsertItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .Returns((Stream input, PartitionKey _, ItemRequestOptions _, CancellationToken token) => service(input, token));
            }
            else
            {
                inner.Setup(c => c.ReadItemStreamAsync(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .Returns((string _, PartitionKey _, ItemRequestOptions _, CancellationToken token) => service(null, token));
            }
        }

        private sealed class TrackingPointResponse : ResponseMessage
        {
            internal TrackingPointResponse(bool throwOnDispose)
                : base(HttpStatusCode.OK)
            {
                this.ThrowOnDispose = throwOnDispose;
            }

            internal bool Disposed { get; private set; }

            internal bool ThrowOnDispose { get; set; }

            protected override void Dispose(bool disposing)
            {
                this.Disposed = true;
                base.Dispose(disposing);
                if (this.ThrowOnDispose)
                {
                    throw new IOException("response cleanup failure");
                }
            }
        }

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
        public async Task Decrypt_RawObjectDepthBoundary_PreservesInput(int depth, int processor)
        {
            string json = string.Concat(Enumerable.Repeat("{\"n\":", depth)) + "0" + new string('}', depth);
            using MemoryStream input = H.Stream(json);
            byte[] before = input.ToArray();
            H.PublicEncryptor encryptor = new();
            if (depth == 64)
            {
                (Stream result, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(input, encryptor,
                    new CosmosDiagnosticsContext(), RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None);
                Assert.AreSame(input, result);
                Assert.IsNull(context);
            }
            else
            {
                InvalidOperationException failure = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
                    await EncryptionProcessor.DecryptAsync(input, encryptor, new CosmosDiagnosticsContext(),
                        RequestOptionsOverrideHelper.Create((JsonProcessor)processor), CancellationToken.None));
                Assert.AreEqual("The response body must contain a JSON object.", failure.Message);
            }

            CollectionAssert.AreEqual(before, input.ToArray());
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
