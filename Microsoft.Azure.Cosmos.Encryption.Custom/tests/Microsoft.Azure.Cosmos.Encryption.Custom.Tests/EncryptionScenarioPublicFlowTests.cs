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
    public class EncryptionScenarioPublicFlowTests
    {
        [DataTestMethod]
        [DynamicData(nameof(TypedWriteRows), DynamicDataSourceType.Method)]
        public async Task TypedPointWrite_NullMetadataAndTargetPartitions_PreserveCallerAndIdentity(string operation, string target, int processor)
        {
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            JObject item = H.PlainItem();
            item["_ei"] = JValue.CreateNull();
            if (target == "missing")
            {
                item.Remove("Sensitive");
            }
            else if (target == "null")
            {
                item["Sensitive"] = JValue.CreateNull();
            }

            JObject callerBefore = (JObject)item.DeepClone();
            JObject stored = null;
            PartitionKey forwardedKey = default;
            string forwardedId = null;
            EncryptionItemRequestOptions options = H.Options(processor);
            ResponseMessage Accept(Stream stream, PartitionKey pk)
            {
                forwardedKey = pk;
                using StreamReader reader = new(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                stored = JObject.Parse(reader.ReadToEnd());
                stream.Dispose();
                return H.Response(operation == "Create" ? HttpStatusCode.Created : HttpStatusCode.OK, stored.ToString(Formatting.None));
            }

            inner.Setup(c => c.CreateItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(), options, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, PartitionKey pk, ItemRequestOptions requestOptions, CancellationToken token) => Accept(stream, pk));
            inner.Setup(c => c.ReplaceItemStreamAsync(It.IsAny<Stream>(), "i", It.IsAny<PartitionKey>(), options, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, string id, PartitionKey pk, ItemRequestOptions requestOptions, CancellationToken token) =>
                {
                    forwardedId = id;
                    return Accept(stream, pk);
                });
            inner.Setup(c => c.UpsertItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(), options, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, PartitionKey pk, ItemRequestOptions requestOptions, CancellationToken token) => Accept(stream, pk));

            ItemResponse<JObject> response = operation switch
            {
                "Create" => await container.CreateItemAsync(item, new PartitionKey("p"), options),
                "Replace" => await container.ReplaceItemAsync(item, "i", new PartitionKey("p"), options),
                _ => await container.UpsertItemAsync(item, new PartitionKey("p"), options),
            };

            Assert.IsTrue(JToken.DeepEquals(callerBefore, item), "Serialization and encryption must not mutate the caller's item.");
            Assert.AreEqual(new PartitionKey("p"), forwardedKey);
            if (operation == "Replace")
            {
                Assert.AreEqual("i", forwardedId);
            }

            Assert.AreEqual("i", (string)stored["id"]);
            Assert.AreEqual("p", (string)stored["PK"]);
            Assert.AreEqual(1, stored.Properties().Count(p => p.Name == "_ei"));
            Assert.AreEqual(H.Mde, (string)stored["_ei"]["_ea"]);
            Assert.AreEqual(3, (int)stored["_ei"]["_ef"]);
            Assert.AreEqual(target == "value" ? 1 : 0, ((JArray)stored["_ei"]["_ep"]).Count);
            JObject expected = (JObject)callerBefore.DeepClone();
            expected.Remove("_ei");
            Assert.IsTrue(JToken.DeepEquals(expected, response.Resource));
            Assert.AreEqual(target == "value" ? 1 : 0, encryptor.EncryptCalls);
            Assert.AreEqual(target == "value" ? 1 : 0, encryptor.DecryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
        }

        public static IEnumerable<object[]> TypedWriteRows()
        {
            foreach (int processor in H.Processors)
            {
                foreach (string operation in new[] { "Create", "Replace", "Upsert" })
                {
                    foreach (string target in new[] { "value", "missing", "null" })
                    {
                        yield return new object[] { operation, target, processor };
                    }
                }
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(LateFailureRows), DynamicDataSourceType.Method)]
        public async Task ProvidedOutput_LateSecondPropertyFailure_PreservesNonemptyDestination(bool cancel, int processor)
        {
            JObject item = H.MdeItem();
            item["Other"] = Convert.ToBase64String(new byte[] { 2 }.Concat(Encoding.UTF8.GetBytes("other")).ToArray());
            item["_ei"]["_ep"] = new JArray("/Sensitive", "/Other");
            using CancellationTokenSource cancellation = new();
            InvalidOperationException expectedFailure = new("second custom decrypt failed");
            H.PublicEncryptor encryptor = new();
            encryptor.OnDecrypt = (bytes, algorithm, token) =>
            {
                if (encryptor.DecryptCalls == 2)
                {
                    if (cancel)
                    {
                        cancellation.Cancel();
                        return Task.FromResult(bytes);
                    }

                    return Task.FromException<byte[]>(expectedFailure);
                }

                return Task.FromResult(bytes);
            };
            using MemoryStream input = H.Stream(item.ToString(Formatting.None));
            byte[] inputBefore = input.ToArray();
            using MemoryStream output = H.Output();
            byte[] outputBefore = output.ToArray();

            Exception failure = await H.CaptureExceptionAsync(async () =>
                await EncryptionProcessor.DecryptAsync(input, output, encryptor, new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create((JsonProcessor)processor), cancellation.Token));

            if (cancel)
            {
                Assert.IsInstanceOfType(failure, typeof(OperationCanceledException));
                Assert.AreEqual(cancellation.Token, ((OperationCanceledException)failure).CancellationToken);
            }
            else
            {
                Assert.AreSame(expectedFailure, failure);
            }

            Assert.AreEqual(2, encryptor.DecryptCalls);
            Assert.IsTrue(input.CanRead);
            CollectionAssert.AreEqual(inputBefore, input.ToArray());
            CollectionAssert.AreEqual(outputBefore, output.ToArray());
            Assert.AreEqual(2L, output.Position);
        }

        public static IEnumerable<object[]> LateFailureRows()
        {
            foreach (int processor in H.Processors)
            {
                yield return new object[] { false, processor };
                yield return new object[] { true, processor };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(MixedFeedRows), DynamicDataSourceType.Method)]
        public async Task QueryStream_MixedPlaintextMdeLegacy_PreservesPage(bool writable, int processor)
        {
            JObject feed = H.Feed(H.PlainItem("plain"), H.MdeItem(), H.LegacyItem());
            byte[] original = Encoding.UTF8.GetBytes(feed.ToString(Formatting.None));
            using MemoryStream content = new(original.ToArray(), writable);
            ResponseMessage raw = new(HttpStatusCode.OK) { Content = content };
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            Mock<FeedIterator> iterator = new();
            iterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(raw);
            QueryDefinition query = new("SELECT * FROM c");
            QueryRequestOptions options = QueryOptions(processor);
            inner.Setup(c => c.GetItemQueryStreamIterator(query, null, options)).Returns(iterator.Object);

            using ResponseMessage response = await container.GetItemQueryStreamIterator(query, requestOptions: options).ReadNextAsync();
            Assert.AreSame(raw.Headers, response.Headers);
            JObject actual = EncryptionProcessor.BaseSerializer.FromStream<JObject>(response.Content);
            Assert.AreEqual("scenario-feed", (string)actual["_rid"]);
            Assert.AreEqual(3, (int)actual["_count"]);
            JArray documents = (JArray)actual["Documents"];
            CollectionAssert.AreEqual(new[] { "plain", "mde", "legacy" }, documents.Select(d => (string)d["id"]).ToArray());
            foreach (JObject document in documents)
            {
                Assert.AreEqual("secret!", (string)document["Sensitive"]);
                Assert.AreEqual("p", (string)document["PK"]);
                Assert.AreEqual("preserved", (string)document["Plain"]);
                Assert.IsNull(document["_ei"]);
            }

            Assert.AreEqual(0, encryptor.KeyCalls);
            CollectionAssert.AreEqual(original, content.ToArray(), "Whole-page Legacy fallback must reread the original ciphertext.");
        }

        public static IEnumerable<object[]> MixedFeedRows()
        {
            foreach (int processor in H.Processors)
            {
                yield return new object[] { true, processor };
                yield return new object[] { false, processor };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(FeedFailureRows), DynamicDataSourceType.Method)]
        public async Task QueryStream_LateFailureOrCancellation_DoesNotRewriteOriginalPage(string fault, int processor)
        {
            JObject second = H.MdeItem("second");
            if (fault == "metadata")
            {
                second["_ei"]["_ep"] = new JArray(JValue.CreateNull());
            }

            using CancellationTokenSource cancellation = new();
            InvalidOperationException expected = new("late feed crypto failure");
            H.PublicEncryptor encryptor = new();
            encryptor.OnDecrypt = (bytes, algorithm, token) =>
            {
                if (encryptor.DecryptCalls == 2)
                {
                    if (fault == "cancel")
                    {
                        cancellation.Cancel();
                        return Task.FromResult(bytes);
                    }

                    if (fault == "crypto")
                    {
                        return Task.FromException<byte[]>(expected);
                    }
                }

                return Task.FromResult(bytes);
            };
            using MemoryStream content = H.Stream(H.Feed(H.MdeItem("first"), second).ToString(Formatting.None));
            byte[] original = content.ToArray();
            using ResponseMessage raw = new(HttpStatusCode.OK) { Content = content };
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            Mock<FeedIterator> iterator = new();
            iterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(raw);
            QueryDefinition query = new("SELECT * FROM c");
            QueryRequestOptions options = QueryOptions(processor);
            inner.Setup(c => c.GetItemQueryStreamIterator(query, null, options)).Returns(iterator.Object);

            Exception failure = await H.CaptureExceptionAsync(async () =>
            {
                using ResponseMessage response = await container.GetItemQueryStreamIterator(query, requestOptions: options).ReadNextAsync(cancellation.Token);
            });

            if (fault == "cancel")
            {
                Assert.IsInstanceOfType(failure, typeof(OperationCanceledException));
                Assert.AreEqual(cancellation.Token, ((OperationCanceledException)failure).CancellationToken);
            }
            else if (fault == "crypto")
            {
                Assert.AreSame(expected, failure);
            }
            else
            {
                Assert.IsInstanceOfType(failure, typeof(InvalidOperationException));
                Assert.AreEqual(EncryptionMetadataClassifier.InvalidMetadataMessage, failure.Message);
            }

            Assert.IsTrue(content.CanRead);
            CollectionAssert.AreEqual(original, content.ToArray());
            Assert.AreEqual(fault == "metadata" ? 1 : 2, encryptor.DecryptCalls);
        }

        public static IEnumerable<object[]> FeedFailureRows()
        {
            foreach (int processor in H.Processors)
            {
                foreach (string fault in new[] { "metadata", "crypto", "cancel" })
                {
                    yield return new object[] { fault, processor };
                }
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ChangeFeed_ManualCheckpoint_EagerFailureAndLazyDeliveryHaveSeparateBoundaries(bool lazy)
        {
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            Container.ChangeFeedHandlerWithManualCheckpoint<JObject> captured = null;
            inner.Setup(c => c.GetChangeFeedProcessorBuilderWithManualCheckpoint("scenario",
                    It.IsAny<Container.ChangeFeedHandlerWithManualCheckpoint<JObject>>()))
                .Callback<string, Container.ChangeFeedHandlerWithManualCheckpoint<JObject>>((name, handler) => captured = handler)
                .Returns((ChangeFeedProcessorBuilder)null);
            int handlerCalls = 0;
            int checkpointCalls = 0;
            Func<Task> checkpoint = () =>
            {
                checkpointCalls++;
                return Task.CompletedTask;
            };
            IReadOnlyCollection<DecryptableItem> delivered = null;
            if (lazy)
            {
                container.GetChangeFeedProcessorBuilderWithManualCheckpoint<DecryptableItem>("scenario",
                    (context, items, forwardedCheckpoint, token) =>
                    {
                        handlerCalls++;
                        delivered = items;
                        Assert.AreSame(checkpoint, forwardedCheckpoint);
                        Assert.AreEqual(0, encryptor.DecryptCalls);
                        return Task.CompletedTask;
                    });
            }
            else
            {
                container.GetChangeFeedProcessorBuilderWithManualCheckpoint<JObject>("scenario",
                    (context, items, forwardedCheckpoint, token) =>
                    {
                        handlerCalls++;
                        return Task.CompletedTask;
                    });
            }

            JObject invalid = H.MdeItem("bad");
            invalid["_ei"]["_ep"] = new JArray(JValue.CreateNull());
            IReadOnlyCollection<JObject> documents = new[] { H.MdeItem("good"), invalid };
            Exception failure = await H.CaptureExceptionAsync(() => captured(new Mock<ChangeFeedProcessorContext>().Object,
                documents, checkpoint, CancellationToken.None));
            Assert.AreEqual(0, checkpointCalls);
            if (lazy)
            {
                Assert.IsNull(failure);
                Assert.AreEqual(1, handlerCalls);
                Assert.AreEqual(2, delivered.Count);
                (JObject first, _) = await delivered.First().GetItemAsync<JObject>();
                Assert.AreEqual("secret!", (string)first["Sensitive"]);
                EncryptionException lazyFailure = await Assert.ThrowsExceptionAsync<EncryptionException>(
                    () => delivered.Last().GetItemAsync<JObject>());
                Assert.AreEqual(EncryptionMetadataClassifier.InvalidMetadataMessage, lazyFailure.InnerException.Message);
                Assert.AreEqual(1, encryptor.DecryptCalls);
                foreach (DecryptableItem item in delivered)
                {
                    await item.DisposeAsync();
                }
            }
            else
            {
                Assert.IsInstanceOfType(failure, typeof(InvalidOperationException));
                Assert.AreEqual(EncryptionMetadataClassifier.InvalidMetadataMessage, failure.Message);
                Assert.AreEqual(0, handlerCalls);
                Assert.AreEqual(1, encryptor.DecryptCalls);
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Batch_ResponseStatusGate_PreservesFailedResourcesAndDecryptsSuccessfulMixedResults(bool success)
        {
            H.PublicEncryptor encryptor = new();
            Container container = H.Container(encryptor, out Mock<Container> inner, out _);
            Mock<TransactionalBatch> batch = new();
            List<TransactionalBatchOperationResult> results = new();
            List<InspectionStream> resources = new();
            foreach (JObject item in new[] { H.MdeItem(), H.LegacyItem() })
            {
                InspectionStream resource = new(Encoding.UTF8.GetBytes(item.ToString(Formatting.None)), throwOnRead: !success);
                resources.Add(resource);
                Mock<TransactionalBatchOperationResult> result = new();
                result.SetupGet(r => r.ResourceStream).Returns(resource);
                result.SetupGet(r => r.StatusCode).Returns(success ? HttpStatusCode.OK : HttpStatusCode.FailedDependency);
                results.Add(result.Object);
            }

            Mock<TransactionalBatchOperationResult> nullResult = new();
            nullResult.SetupGet(r => r.ResourceStream).Returns((Stream)null);
            results.Add(nullResult.Object);
            Mock<TransactionalBatchResponse> raw = new();
            raw.SetupGet(r => r.StatusCode).Returns(success ? HttpStatusCode.OK : HttpStatusCode.Conflict);
            raw.SetupGet(r => r.IsSuccessStatusCode).Returns(success);
            raw.SetupGet(r => r.Count).Returns(results.Count);
            raw.Setup(r => r.GetEnumerator()).Returns(() => results.GetEnumerator());
            batch.Setup(b => b.ExecuteAsync(It.IsAny<CancellationToken>())).ReturnsAsync(raw.Object);
            inner.Setup(c => c.CreateTransactionalBatch(new PartitionKey("p"))).Returns(batch.Object);

            using TransactionalBatchResponse response = await container.CreateTransactionalBatch(new PartitionKey("p")).ExecuteAsync();
            Assert.AreEqual(raw.Object.StatusCode, response.StatusCode);
            Assert.AreEqual(3, response.Count);
            Assert.IsNull(response[2].ResourceStream);
            for (int index = 0; index < 2; index++)
            {
                if (success)
                {
                    JObject actual = EncryptionProcessor.BaseSerializer.FromStream<JObject>(response[index].ResourceStream);
                    Assert.AreEqual("secret!", (string)actual["Sensitive"]);
                    Assert.IsNull(actual["_ei"]);
                    Assert.IsFalse(resources[index].CanRead);
                }
                else
                {
                    Assert.AreSame(results[index], response[index]);
                    Assert.AreSame(resources[index], response[index].ResourceStream);
                    Assert.AreEqual(0, resources[index].Reads);
                    Assert.IsTrue(resources[index].CanRead);
                }

                resources[index].Dispose();
            }

            Assert.AreEqual(success ? 2 : 0, encryptor.DecryptCalls);
            Assert.AreEqual(0, encryptor.KeyCalls);
        }

        private static QueryRequestOptions QueryOptions(int processor) => new()
        {
            Properties = new Dictionary<string, object> { [JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey] = (JsonProcessor)processor },
        };

        private sealed class InspectionStream : MemoryStream
        {
            private readonly bool throwOnRead;

            internal InspectionStream(byte[] bytes, bool throwOnRead)
                : base(bytes)
            {
                this.throwOnRead = throwOnRead;
            }

            internal int Reads { get; private set; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                this.Reads++;
                if (this.throwOnRead)
                {
                    throw new IOException("A failed batch must not inspect resource content.");
                }

                return base.Read(buffer, offset, count);
            }
        }
    }
}
