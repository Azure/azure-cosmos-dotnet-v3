//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using TestDoc = TestCommon.TestDoc;

#pragma warning disable CS0618 // Type or member is obsolete

    [TestClass]
    public class StreamDecryptableItemTests
    {
        private static Mock<Encryptor> mockEncryptor;
        private static CosmosSerializer cosmosSerializer;
        private const string dekId = "dekId";

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _ = testContext;

            mockEncryptor = CreateEncryptor();

            cosmosSerializer = CreateSerializer();
        }

        [TestMethod]
        public void Constructor_WithValidParameters_Succeeds()
        {
            TestDoc testDoc = TestDoc.Create();
            Stream testStream = testDoc.ToStream();

            StreamDecryptableItem item = new (testStream, mockEncryptor.Object, cosmosSerializer);

            Assert.IsNotNull(item);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullStream_ThrowsArgumentNullException()
        {
            _ = new StreamDecryptableItem(null, mockEncryptor.Object, cosmosSerializer);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullEncryptor_ThrowsArgumentNullException()
        {
            TestDoc testDoc = TestDoc.Create();
            Stream testStream = testDoc.ToStream();

            _ = new StreamDecryptableItem(testStream, null, cosmosSerializer);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullSerializer_ThrowsArgumentNullException()
        {
            TestDoc testDoc = TestDoc.Create();
            Stream testStream = testDoc.ToStream();

            _ = new StreamDecryptableItem(testStream, mockEncryptor.Object, null);
        }

        [TestMethod]
        public async Task GetItemAsync_WithEncryptedStream_DecryptsSuccessfully()
        {
            TestDoc originalDoc = TestDoc.Create();
            EncryptionItemRequestOptions requestOptions = new ()
            {
                EncryptionOptions = new ()
                {
                    DataEncryptionKeyId = dekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                    PathsToEncrypt = TestDoc.PathsToEncrypt
                }
            };

            Stream encryptedStreamForCore = await EncryptionProcessor.EncryptAsync(
                originalDoc.ToStream(),
                mockEncryptor.Object,
                requestOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Stream encryptedStreamForStream = await EncryptionProcessor.EncryptAsync(
                originalDoc.ToStream(),
                mockEncryptor.Object,
                requestOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            JObject encryptedJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStreamForCore);
            DecryptableItemCore coreItem = new (encryptedJObj, mockEncryptor.Object, cosmosSerializer);

            StreamDecryptableItem streamItem = new (encryptedStreamForStream, mockEncryptor.Object, cosmosSerializer);

            (TestDoc coreResult, DecryptionContext coreContext) = await coreItem.GetItemAsync<TestDoc>();
            (TestDoc streamResult, DecryptionContext streamContext) = await streamItem.GetItemAsync<TestDoc>();

            Assert.IsNotNull(coreResult);
            Assert.IsNotNull(streamResult);
            Assert.AreEqual(originalDoc.Id, coreResult.Id);
            Assert.AreEqual(originalDoc.Id, streamResult.Id);
            Assert.AreEqual(originalDoc.SensitiveStr, coreResult.SensitiveStr);
            Assert.AreEqual(originalDoc.SensitiveStr, streamResult.SensitiveStr);
            Assert.AreEqual(originalDoc.SensitiveInt, coreResult.SensitiveInt);
            Assert.AreEqual(originalDoc.SensitiveInt, streamResult.SensitiveInt);

            Assert.IsNotNull(coreContext);
            Assert.IsNotNull(streamContext);
            Assert.AreEqual(coreContext.DecryptionInfoList.Count, streamContext.DecryptionInfoList.Count);
        }

        [TestMethod]
        public async Task GetItemAsync_BehaviorEquivalentToDecryptableItemCore_WithUnencryptedContent()
        {
            TestDoc originalDoc = TestDoc.Create();
            
            Stream unencryptedStreamForCore = originalDoc.ToStream();
            Stream unencryptedStreamForStream = originalDoc.ToStream();

            JObject unencryptedJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(unencryptedStreamForCore);
            DecryptableItemCore coreItem = new (unencryptedJObj, mockEncryptor.Object, cosmosSerializer);

            StreamDecryptableItem streamItem = new (unencryptedStreamForStream, mockEncryptor.Object, cosmosSerializer);

            (TestDoc coreResult, DecryptionContext coreContext) = await coreItem.GetItemAsync<TestDoc>();
            (TestDoc streamResult, DecryptionContext streamContext) = await streamItem.GetItemAsync<TestDoc>();

            Assert.IsNotNull(coreResult);
            Assert.IsNotNull(streamResult);
            Assert.AreEqual(originalDoc.Id, coreResult.Id);
            Assert.AreEqual(originalDoc.Id, streamResult.Id);
            Assert.AreEqual(originalDoc.NonSensitive, coreResult.NonSensitive);
            Assert.AreEqual(originalDoc.NonSensitive, streamResult.NonSensitive);

            Assert.IsNull(coreContext);
            Assert.IsNull(streamContext);
        }

        [TestMethod]
        public async Task GetItemAsync_VerifyDecryptionContextEquivalence()
        {
            TestDoc originalDoc = TestDoc.Create();
            EncryptionItemRequestOptions requestOptions = new ()
            {
                EncryptionOptions = new ()
                {
                    DataEncryptionKeyId = dekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                    PathsToEncrypt = TestDoc.PathsToEncrypt
                }
            };

            Stream encryptedStreamForCore = await EncryptionProcessor.EncryptAsync(
                originalDoc.ToStream(),
                mockEncryptor.Object,
                requestOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Stream encryptedStreamForStream = await EncryptionProcessor.EncryptAsync(
                originalDoc.ToStream(),
                mockEncryptor.Object,
                requestOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            JObject encryptedJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStreamForCore);
            DecryptableItemCore coreItem = new (encryptedJObj, mockEncryptor.Object, cosmosSerializer);

            StreamDecryptableItem streamItem = new (encryptedStreamForStream, mockEncryptor.Object, cosmosSerializer);

            (_, DecryptionContext coreContext) = await coreItem.GetItemAsync<TestDoc>();
            (_, DecryptionContext streamContext) = await streamItem.GetItemAsync<TestDoc>();

            Assert.IsNotNull(coreContext);
            Assert.IsNotNull(streamContext);
            Assert.AreEqual(coreContext.DecryptionInfoList.Count, streamContext.DecryptionInfoList.Count);

            for (int i = 0; i < coreContext.DecryptionInfoList.Count; i++)
            {
                DecryptionInfo coreInfo = coreContext.DecryptionInfoList[i];
                DecryptionInfo streamInfo = streamContext.DecryptionInfoList[i];

                Assert.AreEqual(coreInfo.DataEncryptionKeyId, streamInfo.DataEncryptionKeyId);
                Assert.AreEqual(coreInfo.PathsDecrypted.Count, streamInfo.PathsDecrypted.Count);
                
                foreach (string path in coreInfo.PathsDecrypted)
                {
                    Assert.IsTrue(streamInfo.PathsDecrypted.Any(p => p == path), 
                        $"StreamDecryptableItem should have decrypted path: {path}");
                }
            }
        }

        [TestMethod]
        [ExpectedException(typeof(EncryptionException))]
        public async Task GetItemAsync_WithInvalidDek_ThrowsEncryptionException()
        {
            TestDoc originalDoc = TestDoc.Create();
            EncryptionItemRequestOptions requestOptions = new ()
            {
                EncryptionOptions = new ()
                {
                    DataEncryptionKeyId = dekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                    PathsToEncrypt = TestDoc.PathsToEncrypt
                }
            };

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                originalDoc.ToStream(),
                mockEncryptor.Object,
                requestOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Mock<Encryptor> failingMockEncryptor = new Mock<Encryptor>();
            failingMockEncryptor.Setup(m => m.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Null DEK was returned."));

            StreamDecryptableItem streamItem = new StreamDecryptableItem(
                encryptedStream,
                failingMockEncryptor.Object,
                cosmosSerializer);

            await streamItem.GetItemAsync<TestDoc>();
        }

        [TestMethod]
        public async Task GetItemAsync_ExceptionHandling_EquivalentToDecryptableItemCore()
        {
            TestDoc originalDoc = TestDoc.Create();
            EncryptionItemRequestOptions requestOptions = new ()
            { 
                EncryptionOptions = new ()
                {
                    DataEncryptionKeyId = dekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                    PathsToEncrypt = TestDoc.PathsToEncrypt
                }
            };

            Stream encryptedStreamForCore = await EncryptionProcessor.EncryptAsync(
                originalDoc.ToStream(),
                mockEncryptor.Object,
                requestOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Stream encryptedStreamForStream = await EncryptionProcessor.EncryptAsync(
                originalDoc.ToStream(),
                mockEncryptor.Object,
                requestOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Mock<Encryptor> failingMockEncryptor = new Mock<Encryptor>();
            failingMockEncryptor.Setup(m => m.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Null DEK was returned."));

            JObject encryptedJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStreamForCore);
            DecryptableItemCore coreItem = new DecryptableItemCore(
                encryptedJObj,
                failingMockEncryptor.Object,
                cosmosSerializer);

            StreamDecryptableItem streamItem = new StreamDecryptableItem(
                encryptedStreamForStream,
                failingMockEncryptor.Object,
                cosmosSerializer);

            EncryptionException coreException = null;
            EncryptionException streamException = null;

            try
            {
                await coreItem.GetItemAsync<TestDoc>();
            }
            catch (EncryptionException ex)
            {
                coreException = ex;
            }

            try
            {
                await streamItem.GetItemAsync<TestDoc>();
            }
            catch (EncryptionException ex)
            {
                streamException = ex;
            }

            Assert.IsNotNull(coreException);
            Assert.IsNotNull(streamException);
            Assert.IsNotNull(coreException.InnerException);
            Assert.IsNotNull(streamException.InnerException);
            Assert.IsTrue(coreException.EncryptedContent.Length > 0);
            Assert.IsTrue(streamException.EncryptedContent.Length > 0);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetDecryptableItemVariants), DynamicDataSourceType.Method)]
        public async Task GetItemAsync_EncryptedPayload_ReturnsOriginalDocument(DecryptableItemVariant variant)
        {
            TestDoc originalDoc = TestDoc.Create();

            await using DecryptableItem decryptableItem = await CreateDecryptableItemAsync(
                variant,
                originalDoc,
                encrypt: true,
                decryptor: mockEncryptor.Object,
                serializer: cosmosSerializer);

            (TestDoc result, DecryptionContext context) = await decryptableItem.GetItemAsync<TestDoc>();

            Assert.IsNotNull(result);
            Assert.AreEqual(originalDoc.Id, result.Id);
            Assert.AreEqual(originalDoc.SensitiveStr, result.SensitiveStr);
            Assert.AreEqual(originalDoc.SensitiveInt, result.SensitiveInt);
            Assert.IsNotNull(context);
            Assert.IsTrue(context.DecryptionInfoList?.Count > 0);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetDecryptableItemVariants), DynamicDataSourceType.Method)]
        public async Task GetItemAsync_UnencryptedPayload_ReturnsDocumentWithNullContext(DecryptableItemVariant variant)
        {
            TestDoc originalDoc = TestDoc.Create();

            await using DecryptableItem decryptableItem = await CreateDecryptableItemAsync(
                variant,
                originalDoc,
                encrypt: false,
                decryptor: mockEncryptor.Object,
                serializer: cosmosSerializer);

            (TestDoc result, DecryptionContext context) = await decryptableItem.GetItemAsync<TestDoc>();

            Assert.IsNotNull(result);
            Assert.AreEqual(originalDoc.Id, result.Id);
            Assert.AreEqual(originalDoc.NonSensitive, result.NonSensitive);
            Assert.IsNull(context);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetDecryptableItemVariants), DynamicDataSourceType.Method)]
        public async Task GetItemAsync_WithInvalidDek_ThrowsEncryptionException_ForAllVariants(DecryptableItemVariant variant)
        {
            TestDoc originalDoc = TestDoc.Create();

            Mock<Encryptor> failingEncryptor = new Mock<Encryptor>();
            failingEncryptor.Setup(m => m.EncryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] plainText, string dekId, string algo, CancellationToken t) => TestCommon.EncryptData(plainText));
            failingEncryptor.Setup(m => m.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Null DEK was returned."));

            await using DecryptableItem decryptableItem = await CreateDecryptableItemAsync(
                variant,
                originalDoc,
                encrypt: true,
                decryptor: failingEncryptor.Object,
                serializer: cosmosSerializer,
                encryptionEncryptor: mockEncryptor.Object);

            await Assert.ThrowsExceptionAsync<EncryptionException>(async () => await decryptableItem.GetItemAsync<TestDoc>());
        }

        [TestMethod]
        public async Task GetItemAsync_StreamDecryptableItem_CachesResultAndDisposesStream()
        {
            TestDoc originalDoc = TestDoc.Create();
            int decryptAsyncCalls = 0;
            Mock<Encryptor> encryptor = CreateEncryptor(() => Interlocked.Increment(ref decryptAsyncCalls));

            int serializerCalls = 0;
            Mock<CosmosSerializer> serializerMock = new Mock<CosmosSerializer>();
            serializerMock.Setup(s => s.FromStream<TestDoc>(It.IsAny<Stream>()))
                .Returns<Stream>(stream =>
                {
                    Interlocked.Increment(ref serializerCalls);
                    return EncryptionProcessor.BaseSerializer.FromStream<TestDoc>(stream);
                });
            serializerMock.Setup(s => s.FromStream<JObject>(It.IsAny<Stream>()))
                .Returns<Stream>(stream => EncryptionProcessor.BaseSerializer.FromStream<JObject>(stream));

            TrackingStream trackingStream = await CreateTrackingEncryptedStreamAsync(originalDoc, encryptor.Object);

            StreamDecryptableItem decryptableItem = new StreamDecryptableItem(
                trackingStream,
                encryptor.Object,
                serializerMock.Object);

            try
            {
                (TestDoc firstResult, DecryptionContext firstContext) = await decryptableItem.GetItemAsync<TestDoc>();

                Assert.AreEqual(1, decryptAsyncCalls, "DecryptAsync should be invoked once for the initial call.");
                Assert.AreEqual(1, serializerCalls, "Serializer should be invoked once for the initial call.");
                Assert.AreEqual(2, trackingStream.AsyncDisposeCallCount, "Content stream should be disposed by both the processor and the decryptable item exactly once each.");
                Assert.AreEqual(2, trackingStream.SyncDisposeCallCount, "Content stream should observe the paired synchronous dispose callbacks.");

                (TestDoc secondResult, DecryptionContext secondContext) = await decryptableItem.GetItemAsync<TestDoc>();

                Assert.AreSame(firstResult, secondResult, "Cached item should be returned on subsequent calls.");
                Assert.AreSame(firstContext, secondContext, "Cached decryption context should be returned on subsequent calls.");
                Assert.AreEqual(1, decryptAsyncCalls, "DecryptAsync should not be invoked again for cached data.");
                Assert.AreEqual(1, serializerCalls, "Serializer should not be invoked again for cached data.");
                Assert.AreEqual(2, trackingStream.AsyncDisposeCallCount, "Underlying stream should not be disposed again beyond the initial decryption path.");

                await decryptableItem.DisposeAsync();

                Assert.AreEqual(2, trackingStream.AsyncDisposeCallCount, "DisposeAsync should be idempotent once the item has already disposed the stream.");
                Assert.AreEqual(2, trackingStream.SyncDisposeCallCount);

                await decryptableItem.DisposeAsync();

                Assert.AreEqual(2, trackingStream.AsyncDisposeCallCount, "Subsequent DisposeAsync calls should have no effect.");
                Assert.AreEqual(2, trackingStream.SyncDisposeCallCount);
            }
            finally
            {
                await decryptableItem.DisposeAsync();
            }
        }

        private static Mock<Encryptor> CreateEncryptor(Action onDecryptInvoked = null)
        {
            Mock<Encryptor> encryptorMock = new Mock<Encryptor>();
            encryptorMock.Setup(m => m.EncryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] plainText, string dekId, string algo, CancellationToken token) =>
                    dekId == StreamDecryptableItemTests.dekId ? TestCommon.EncryptData(plainText) : throw new InvalidOperationException("DEK not found."));

            encryptorMock.Setup(m => m.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] cipherText, string dekId, string algo, CancellationToken token) =>
                {
                    if (dekId != StreamDecryptableItemTests.dekId)
                    {
                        throw new InvalidOperationException("Null DEK was returned.");
                    }

                    onDecryptInvoked?.Invoke();
                    return TestCommon.DecryptData(cipherText);
                });

            return encryptorMock;
        }

        private static CosmosSerializer CreateSerializer()
        {
            Mock<CosmosSerializer> serializerMock = new Mock<CosmosSerializer>();
            serializerMock.Setup(s => s.FromStream<TestDoc>(It.IsAny<Stream>()))
                .Returns<Stream>(stream => EncryptionProcessor.BaseSerializer.FromStream<TestDoc>(stream));
            serializerMock.Setup(s => s.FromStream<JObject>(It.IsAny<Stream>()))
                .Returns<Stream>(stream => EncryptionProcessor.BaseSerializer.FromStream<JObject>(stream));

            return serializerMock.Object;
        }

        public static IEnumerable<object[]> GetDecryptableItemVariants()
        {
            yield return new object[] { DecryptableItemVariant.Stream };
            yield return new object[] { DecryptableItemVariant.Newtonsoft };
        }

        private static async Task<DecryptableItem> CreateDecryptableItemAsync(
            DecryptableItemVariant variant,
            TestDoc document,
            bool encrypt,
            Encryptor decryptor,
            CosmosSerializer serializer,
            Encryptor encryptionEncryptor = null)
        {
            Stream contentStream = encrypt
                ? await CreateEncryptedStreamAsync(document, encryptionEncryptor ?? decryptor).ConfigureAwait(false)
                : document.ToStream();

            if (variant == DecryptableItemVariant.Stream)
            {
                return new StreamDecryptableItem(contentStream, decryptor, serializer);
            }

            JObject jObject = EncryptionProcessor.BaseSerializer.FromStream<JObject>(contentStream);
            return new DecryptableItemCore(jObject, decryptor, serializer);
        }

        private static async Task<Stream> CreateEncryptedStreamAsync(TestDoc document, Encryptor encryptor)
        {
            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                document.ToStream(),
                encryptor,
                CreateEncryptionOptions(),
                JsonProcessor.Newtonsoft,
                new CosmosDiagnosticsContext(),
                CancellationToken.None).ConfigureAwait(false);

            if (encryptedStream.CanSeek)
            {
                encryptedStream.Position = 0;
            }

            return encryptedStream;
        }

        private static EncryptionOptions CreateEncryptionOptions()
        {
            return new EncryptionOptions()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt
            };
        }

        private static async Task<TrackingStream> CreateTrackingEncryptedStreamAsync(TestDoc document, Encryptor encryptor)
        {
            Stream encryptedStream = await CreateEncryptedStreamAsync(document, encryptor).ConfigureAwait(false);

            try
            {
                MemoryStream buffer = new MemoryStream();

                if (encryptedStream.CanSeek)
                {
                    encryptedStream.Position = 0;
                }

                await encryptedStream.CopyToAsync(buffer).ConfigureAwait(false);
                return new TrackingStream(buffer.ToArray());
            }
            finally
            {
                await encryptedStream.DisposeAsync().ConfigureAwait(false);
            }
        }

        public enum DecryptableItemVariant
        {
            Stream,
            Newtonsoft,
        }

        private sealed class TrackingStream : MemoryStream
        {
            public TrackingStream(byte[] buffer)
                : base(buffer, writable: true)
            {
            }

            public int AsyncDisposeCallCount { get; private set; }

            public int SyncDisposeCallCount { get; private set; }

            public override ValueTask DisposeAsync()
            {
                this.AsyncDisposeCallCount++;
                return base.DisposeAsync();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.SyncDisposeCallCount++;
                }

                base.Dispose(disposing);
            }
        }
    }
}
#endif
