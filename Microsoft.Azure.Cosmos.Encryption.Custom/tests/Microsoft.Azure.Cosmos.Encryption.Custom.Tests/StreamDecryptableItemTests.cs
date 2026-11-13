//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
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
        public async Task GetItemAsync_WhenDecryptionFailsThenCalledAgain_SurfacesObjectDisposedException()
        {
            // Regression: after a failed decrypt the catch nulled contentStream but left isDisposed/
            // isDecrypted false, so a retry NRE'd on the null stream and produced a second EncryptionException
            // with empty fields, masking the original. A retry must now surface ObjectDisposedException.
            TestDoc originalDoc = TestDoc.Create();
            Stream encryptedStream = await CreateEncryptedStreamAsync(originalDoc, mockEncryptor.Object);

            Mock<Encryptor> failingEncryptor = new ();
            failingEncryptor.Setup(m => m.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Simulated DEK failure"));

            StreamDecryptableItem streamItem = new (encryptedStream, failingEncryptor.Object, cosmosSerializer);

            EncryptionException first = await Assert.ThrowsExceptionAsync<EncryptionException>(
                async () => await streamItem.GetItemAsync<TestDoc>());

            Assert.IsNotNull(first.InnerException, "First failure should preserve the underlying cause.");

            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
                async () => await streamItem.GetItemAsync<TestDoc>(),
                "Second call after a failed decrypt must surface ObjectDisposedException, not a second masked EncryptionException.");
        }

        [TestMethod]
        public async Task GetItemAsync_StreamMode_WithMdeAlgorithm_TakesGenuineMdeStreamPath()
        {
            // Coverage gap: the other Stream-variant tests encrypt with the legacy algorithm, which makes
            // SystemTextJsonStreamAdapter.DecryptAsync throw NotSupportedException and fall back to Newtonsoft
            // - so the MDE stream-decrypt path is never actually exercised. This encrypts with MDE and asserts
            // (via the diagnostics selection scope) that the JsonProcessor.Stream branch was taken, not the
            // Newtonsoft fallback, and that the document round-trips.
            TestDoc originalDoc = TestDoc.Create();

            EncryptionOptions encryptionOptions = new ()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };

            using MemoryStream encryptedBuffer = new ();
            Mock<Encryptor> mdeEncryptor = TestEncryptorFactory.CreateMde(dekId, out _);
            await EncryptionProcessor.EncryptAsync(
                originalDoc.ToStream(),
                encryptedBuffer,
                mdeEncryptor.Object,
                encryptionOptions,
                JsonProcessor.Stream,
                new CosmosDiagnosticsContext(),
                CancellationToken.None).ConfigureAwait(false);

            encryptedBuffer.Position = 0;
            byte[] encryptedBytes = encryptedBuffer.ToArray();

            List<Activity> capturedActivities = new ();
            using ActivityListener listener = new ()
            {
                ShouldListenTo = source => source.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = a => { lock (capturedActivities) { capturedActivities.Add(a); } },
            };
            ActivitySource.AddActivityListener(listener);

            await using StreamDecryptableItem streamItem = new (new MemoryStream(encryptedBytes), mdeEncryptor.Object, cosmosSerializer);

            (TestDoc result, DecryptionContext ctx) = await streamItem.GetItemAsync<TestDoc>();

            Assert.IsNotNull(result);
            Assert.AreEqual(originalDoc.Id, result.Id, "MDE stream-decrypt must round-trip the document.");
            Assert.AreEqual(originalDoc.SensitiveStr, result.SensitiveStr);
            Assert.AreEqual(originalDoc.SensitiveInt, result.SensitiveInt);
            Assert.IsNotNull(ctx);
            Assert.IsTrue(ctx.DecryptionInfoList?.Count > 0, "DecryptionContext must list at least one decrypted path.");
            Assert.AreEqual(dekId, ctx.DecryptionInfoList[0].DataEncryptionKeyId);

            string expectedStreamScope = CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream;
            string newtonsoftScope = CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft;
            lock (capturedActivities)
            {
                Assert.IsTrue(
                    capturedActivities.Any(a => a.DisplayName == expectedStreamScope),
                    $"Expected MDE stream-decrypt scope '{expectedStreamScope}' not seen. Captured: {string.Join(", ", capturedActivities.Select(a => a.DisplayName))}");
                Assert.IsFalse(
                    capturedActivities.Any(a => a.DisplayName == newtonsoftScope),
                    $"Newtonsoft fallback scope '{newtonsoftScope}' must not be taken when the payload is MDE-encrypted (regression: previously every existing test fell through to this path).");
            }
        }

        [TestMethod]
        public async Task GetItemAsync_WhenDecryptionFails_PopulatesDataEncryptionKeyIdOnException()
        {
            // Regression: StreamDecryptableItem used to hardcode dataEncryptionKeyId: string.Empty in the
            // EncryptionException it threw on decryption failure, dropping the diagnostic identifier
            // customers use to correlate key-store/DEK-revocation failures. The fix extracts the id from
            // the on-stream _ei.DataEncryptionKeyId (or from a successful pre-failure DecryptionContext).
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

            EncryptionException ex = await Assert.ThrowsExceptionAsync<EncryptionException>(
                async () => await streamItem.GetItemAsync<TestDoc>());

            Assert.AreEqual(dekId, ex.DataEncryptionKeyId,
                "EncryptionException must surface the DEK id from _ei so customers can diagnose key-store/DEK-revocation failures.");
            Assert.IsTrue(ex.EncryptedContent.Length > 0, "EncryptedContent should still be populated for diagnostics.");
            Assert.IsNotNull(ex.InnerException, "Inner exception should preserve the underlying failure.");
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

        [TestMethod]
        public async Task GetItemAsync_SequentialCallsWithDifferentTypes_MatchesNewtonsoftPath()
        {
            TestDoc originalDoc = TestDoc.Create();
            int streamDecryptCalls = 0;
            Mock<Encryptor> streamDecryptor = CreateEncryptor(() => Interlocked.Increment(ref streamDecryptCalls));

            await using DecryptableItem newtonsoftItem = await CreateDecryptableItemAsync(
                DecryptableItemVariant.Newtonsoft,
                originalDoc,
                encrypt: true,
                decryptor: mockEncryptor.Object,
                serializer: cosmosSerializer);
            await using DecryptableItem streamItem = await CreateDecryptableItemAsync(
                DecryptableItemVariant.Stream,
                originalDoc,
                encrypt: true,
                decryptor: streamDecryptor.Object,
                serializer: cosmosSerializer,
                encryptionEncryptor: mockEncryptor.Object);

            (TestDoc newtonsoftTyped, _) = await newtonsoftItem.GetItemAsync<TestDoc>();
            (TestDoc streamTyped, _) = await streamItem.GetItemAsync<TestDoc>();
            (JObject newtonsoftJson, _) = await newtonsoftItem.GetItemAsync<JObject>();
            (JObject streamJson, _) = await streamItem.GetItemAsync<JObject>();

            Assert.AreEqual(originalDoc.Id, newtonsoftTyped.Id);
            Assert.AreEqual(originalDoc.Id, streamTyped.Id);
            Assert.AreEqual(originalDoc.SensitiveStr, newtonsoftTyped.SensitiveStr);
            Assert.AreEqual(originalDoc.SensitiveStr, streamTyped.SensitiveStr);
            Assert.AreEqual(originalDoc.Id, (string)newtonsoftJson["id"]);
            Assert.AreEqual(originalDoc.Id, (string)streamJson["id"]);
            Assert.AreEqual(originalDoc.SensitiveInt, (int)streamJson[nameof(TestDoc.SensitiveInt)]);
            Assert.AreEqual(1, streamDecryptCalls, "Different requested types should reuse the same decrypted plaintext.");
        }

        [TestMethod]
        public async Task GetItemAsync_ConcurrentCallsWithDifferentTypes_DecryptsOnceWithoutPerTypeObjectCache()
        {
            TestDoc originalDoc = TestDoc.Create();
            TrackingStream trackingStream = await CreateTrackingEncryptedStreamAsync(originalDoc, mockEncryptor.Object);
            using SemaphoreSlim decryptionStarted = new (0, 1);
            using SemaphoreSlim allowDecryptionToComplete = new (0, 1);
            int decryptAsyncCalls = 0;

            Mock<Encryptor> slowEncryptor = new ();
            slowEncryptor.Setup(m => m.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], string, string, CancellationToken>(async (cipherText, dataEncryptionKeyId, algorithm, token) =>
                {
                    _ = dataEncryptionKeyId;
                    _ = algorithm;
                    Interlocked.Increment(ref decryptAsyncCalls);
                    decryptionStarted.Release();
                    await allowDecryptionToComplete.WaitAsync(token);
                    return TestCommon.DecryptData(cipherText);
                });

            int typedSerializerCalls = 0;
            int jsonSerializerCalls = 0;
            Mock<CosmosSerializer> serializer = new ();
            serializer.Setup(s => s.FromStream<TestDoc>(It.IsAny<Stream>()))
                .Returns<Stream>(stream =>
                {
                    Interlocked.Increment(ref typedSerializerCalls);
                    return EncryptionProcessor.BaseSerializer.FromStream<TestDoc>(stream);
                });
            serializer.Setup(s => s.FromStream<JObject>(It.IsAny<Stream>()))
                .Returns<Stream>(stream =>
                {
                    Interlocked.Increment(ref jsonSerializerCalls);
                    return EncryptionProcessor.BaseSerializer.FromStream<JObject>(stream);
                });

            StreamDecryptableItem item = new (trackingStream, slowEncryptor.Object, serializer.Object);
            try
            {
                Task<(TestDoc, DecryptionContext)> typedTask = item.GetItemAsync<TestDoc>();
                await decryptionStarted.WaitAsync();

                Task<(JObject, DecryptionContext)> jsonTask = item.GetItemAsync<JObject>();
                allowDecryptionToComplete.Release();

                await Task.WhenAll(typedTask, jsonTask);
                (TestDoc typed, DecryptionContext typedContext) = await typedTask;
                (JObject json, DecryptionContext jsonContext) = await jsonTask;

                Assert.AreEqual(originalDoc.Id, typed.Id);
                Assert.AreEqual(originalDoc.SensitiveStr, typed.SensitiveStr);
                Assert.AreEqual(originalDoc.Id, (string)json["id"]);
                Assert.AreEqual(originalDoc.SensitiveInt, (int)json[nameof(TestDoc.SensitiveInt)]);
                Assert.AreSame(typedContext, jsonContext);
                Assert.AreEqual(1, decryptAsyncCalls);
                Assert.AreEqual(1, typedSerializerCalls);
                Assert.AreEqual(1, jsonSerializerCalls);

                (TestDoc repeatedTyped, DecryptionContext repeatedTypedContext) = await item.GetItemAsync<TestDoc>();
                (JObject repeatedJson, DecryptionContext repeatedJsonContext) = await item.GetItemAsync<JObject>();

                Assert.AreNotSame(typed, repeatedTyped);
                Assert.AreNotSame(json, repeatedJson);
                Assert.AreEqual(typed.Id, repeatedTyped.Id);
                Assert.AreEqual(typed.SensitiveStr, repeatedTyped.SensitiveStr);
                Assert.IsTrue(JToken.DeepEquals(json, repeatedJson));
                Assert.AreSame(typedContext, repeatedTypedContext);
                Assert.AreSame(jsonContext, repeatedJsonContext);
                Assert.AreEqual(1, decryptAsyncCalls);
                Assert.AreEqual(2, typedSerializerCalls);
                Assert.AreEqual(2, jsonSerializerCalls);

                await item.DisposeAsync();
                await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
                    async () => await item.GetItemAsync<TestDoc>());
            }
            finally
            {
                await item.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task DisposeAsync_AfterSuccessfulDecryption_DisposesAndClearsCachedPlaintext()
        {
            TestDoc originalDoc = TestDoc.Create();
            Stream encryptedStream = await CreateEncryptedStreamAsync(originalDoc, mockEncryptor.Object);
            StreamDecryptableItem item = new (encryptedStream, mockEncryptor.Object, cosmosSerializer);

            try
            {
                _ = await item.GetItemAsync<TestDoc>();

                FieldInfo cachedContentField = typeof(StreamDecryptableItem).GetField(
                    "cachedDecryptedContent",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(cachedContentField);

                PooledMemoryStream cachedContent = (PooledMemoryStream)cachedContentField.GetValue(item);
                Assert.IsNotNull(cachedContent);
                int plaintextLength = checked((int)cachedContent.Length);
                byte[] cachedBuffer = cachedContent.GetBuffer();
                Assert.IsTrue(cachedBuffer.AsSpan(0, plaintextLength).ToArray().Any(value => value != 0));

                await item.DisposeAsync();

                Assert.IsFalse(cachedContent.CanRead);
                Assert.IsNull(cachedContentField.GetValue(item));
                Assert.IsTrue(cachedBuffer.All(value => value == 0), "Cached plaintext buffer should be cleared before it is returned to the pool.");
            }
            finally
            {
                await item.DisposeAsync();
            }
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
        public async Task GetItemAsync_AfterDisposeAsync_ThrowsObjectDisposedException()
        {
            TestDoc originalDoc = TestDoc.Create();
            Stream encryptedStream = await CreateEncryptedStreamAsync(originalDoc, mockEncryptor.Object);

            StreamDecryptableItem item = new (encryptedStream, mockEncryptor.Object, cosmosSerializer);

            await item.DisposeAsync();

            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
                async () => await item.GetItemAsync<TestDoc>());
        }

        [TestMethod]
        public async Task DisposeAsync_WhileGetItemAsyncInFlight_DoesNotCorruptState()
        {
            TestDoc originalDoc = TestDoc.Create();

            SemaphoreSlim decryptionStarted = new (0, 1);
            SemaphoreSlim allowDecryptionToComplete = new (0, 1);

            Mock<Encryptor> slowEncryptor = new Mock<Encryptor>();
            slowEncryptor.Setup(m => m.EncryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] plainText, string dekId, string algo, CancellationToken t) => TestCommon.EncryptData(plainText));
            slowEncryptor.Setup(m => m.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], string, string, CancellationToken>(async (cipherText, dekId, algo, token) =>
                {
                    decryptionStarted.Release();
                    await allowDecryptionToComplete.WaitAsync();
                    return TestCommon.DecryptData(cipherText);
                });

            Stream encryptedStream = await CreateEncryptedStreamAsync(originalDoc, mockEncryptor.Object);
            StreamDecryptableItem item = new (encryptedStream, slowEncryptor.Object, cosmosSerializer);

            Task<(TestDoc, DecryptionContext)> getItemTask = item.GetItemAsync<TestDoc>();

            await decryptionStarted.WaitAsync();

            Task disposeTask = item.DisposeAsync().AsTask();

            await Task.Delay(50);
            Assert.IsFalse(disposeTask.IsCompleted, "DisposeAsync should be blocked waiting for the lock held by GetItemAsync.");

            allowDecryptionToComplete.Release();

            (TestDoc result, DecryptionContext context) = await getItemTask;
            await disposeTask;

            Assert.IsNotNull(result);
            Assert.AreEqual(originalDoc.Id, result.Id);
        }

        [TestMethod]
        public async Task GetItemAsync_StreamDecryptableItem_RetainsPlaintextAndDeserializesEveryCall()
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

                Assert.AreNotSame(firstResult, secondResult, "Each call should deserialize an independent object from the retained canonical JSON.");
                Assert.AreEqual(firstResult.Id, secondResult.Id);
                Assert.AreEqual(firstResult.SensitiveStr, secondResult.SensitiveStr);
                Assert.AreEqual(firstResult.SensitiveInt, secondResult.SensitiveInt);
                Assert.AreSame(firstContext, secondContext, "Cached decryption context should be returned on subsequent calls.");
                Assert.AreEqual(1, decryptAsyncCalls, "DecryptAsync should not be invoked again for cached data.");
                Assert.AreEqual(2, serializerCalls, "Every call should deserialize from the retained canonical JSON.");
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

        [TestMethod]
        public async Task GetItemAsync_WhenSerializerThrowsAfterDecryption_DisposesDecryptedStream()
        {
            TestDoc originalDoc = TestDoc.Create();
            Stream encryptedStream = await CreateTrackingEncryptedStreamAsync(originalDoc, mockEncryptor.Object);

            Stream capturedDecryptedStream = null;
            Mock<CosmosSerializer> throwingSerializer = new Mock<CosmosSerializer>();
            throwingSerializer.Setup(s => s.FromStream<JObject>(It.IsAny<Stream>()))
                .Returns<Stream>(stream => EncryptionProcessor.BaseSerializer.FromStream<JObject>(stream));
            throwingSerializer.Setup(s => s.FromStream<TestDoc>(It.IsAny<Stream>()))
                .Callback<Stream>(s => capturedDecryptedStream = s)
                .Throws(new InvalidOperationException("Serializer failure for regression coverage."));

            StreamDecryptableItem item = new (encryptedStream, mockEncryptor.Object, throwingSerializer.Object);
            try
            {
                await Assert.ThrowsExceptionAsync<EncryptionException>(() => item.GetItemAsync<TestDoc>());
                Assert.IsNotNull(capturedDecryptedStream, "Serializer should have been invoked with decrypted stream.");
                Assert.IsFalse(ReferenceEquals(capturedDecryptedStream, encryptedStream), "Encrypted path should produce a distinct decrypted stream.");
                Assert.IsFalse(capturedDecryptedStream.CanRead, "Decrypted stream must be disposed by inner finally even when FromStream throws (regression: stream leak).");
            }
            finally
            {
                await item.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task GetItemAsync_WhenSerializerThrowsForSecondType_DoesNotExposeCachedPlaintext()
        {
            TestDoc originalDoc = TestDoc.Create();
            const string knownSecret = "second-type-diagnostic-must-not-expose-this-secret";
            originalDoc.SensitiveStr = knownSecret;
            Stream encryptedStream = await CreateTrackingEncryptedStreamAsync(originalDoc, mockEncryptor.Object);
            InvalidOperationException serializerFailure = new ("Serializer failure for second requested type.");

            Mock<CosmosSerializer> serializer = new ();
            serializer.Setup(s => s.FromStream<TestDoc>(It.IsAny<Stream>()))
                .Returns<Stream>(stream => EncryptionProcessor.BaseSerializer.FromStream<TestDoc>(stream));
            serializer.Setup(s => s.FromStream<JObject>(It.IsAny<Stream>()))
                .Throws(serializerFailure);

            StreamDecryptableItem item = new (encryptedStream, mockEncryptor.Object, serializer.Object);
            try
            {
                (TestDoc firstResult, DecryptionContext firstContext) = await item.GetItemAsync<TestDoc>();
                Assert.AreEqual(originalDoc.Id, firstResult.Id);
                Assert.IsNotNull(firstContext);

                EncryptionException exception = await Assert.ThrowsExceptionAsync<EncryptionException>(
                    () => item.GetItemAsync<JObject>());

                Assert.AreSame(serializerFailure, exception.InnerException);
                Assert.AreEqual(dekId, exception.DataEncryptionKeyId);
                Assert.IsTrue(
                    string.IsNullOrEmpty(exception.EncryptedContent) ||
                    !exception.EncryptedContent.Contains(knownSecret, StringComparison.Ordinal),
                    "EncryptedContent must never be populated from the cached canonical plaintext.");
            }
            finally
            {
                await item.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task GetItemAsync_WhenSerializerThrowsForSecondType_RetainsCanonicalPlaintextForSubsequentCall()
        {
            TestDoc originalDoc = TestDoc.Create();
            Stream encryptedStream = await CreateTrackingEncryptedStreamAsync(originalDoc, mockEncryptor.Object);
            InvalidOperationException serializerFailure = new ("Serializer failure for second requested type.");
            int typedSerializerCalls = 0;

            Mock<CosmosSerializer> serializer = new ();
            serializer.Setup(s => s.FromStream<TestDoc>(It.IsAny<Stream>()))
                .Returns<Stream>(stream =>
                {
                    Interlocked.Increment(ref typedSerializerCalls);
                    return EncryptionProcessor.BaseSerializer.FromStream<TestDoc>(stream);
                });
            serializer.Setup(s => s.FromStream<JObject>(It.IsAny<Stream>()))
                .Throws(serializerFailure);

            StreamDecryptableItem item = new (encryptedStream, mockEncryptor.Object, serializer.Object);
            try
            {
                (TestDoc firstResult, DecryptionContext firstContext) = await item.GetItemAsync<TestDoc>();

                EncryptionException exception = await Assert.ThrowsExceptionAsync<EncryptionException>(
                    () => item.GetItemAsync<JObject>());

                (TestDoc subsequentResult, DecryptionContext subsequentContext) = await item.GetItemAsync<TestDoc>();

                Assert.AreSame(serializerFailure, exception.InnerException);
                Assert.AreNotSame(firstResult, subsequentResult);
                Assert.AreEqual(originalDoc.Id, subsequentResult.Id);
                Assert.AreEqual(originalDoc.SensitiveStr, subsequentResult.SensitiveStr);
                Assert.AreEqual(originalDoc.SensitiveInt, subsequentResult.SensitiveInt);
                Assert.AreSame(firstContext, subsequentContext);
                Assert.AreEqual(2, typedSerializerCalls, "A failed alternate deserialization must not poison the retained canonical plaintext.");
            }
            finally
            {
                await item.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task GetItemAsync_WhenFirstSerializationFails_ReportsOriginalEncryptedContentWithoutPlaintext()
        {
            TestDoc originalDoc = TestDoc.Create();
            const string knownSecret = "first-call-diagnostic-must-not-expose-this-secret";
            originalDoc.SensitiveStr = knownSecret;
            Stream encryptedStream = await CreateEncryptedStreamAsync(originalDoc, mockEncryptor.Object);

            string originalEncryptedContent;
            using (StreamReader reader = new (encryptedStream, leaveOpen: true))
            {
                originalEncryptedContent = await reader.ReadToEndAsync();
            }

            encryptedStream.Position = 0;

            InvalidOperationException serializerFailure = new ("Serializer failure after successful decryption.");
            Mock<CosmosSerializer> serializer = new ();
            serializer.Setup(s => s.FromStream<TestDoc>(It.IsAny<Stream>()))
                .Throws(serializerFailure);

            StreamDecryptableItem item = new (encryptedStream, mockEncryptor.Object, serializer.Object);
            try
            {
                EncryptionException exception = await Assert.ThrowsExceptionAsync<EncryptionException>(
                    () => item.GetItemAsync<TestDoc>());

                Assert.AreSame(serializerFailure, exception.InnerException);
                Assert.AreEqual(dekId, exception.DataEncryptionKeyId);
                Assert.AreEqual(
                    originalEncryptedContent,
                    exception.EncryptedContent,
                    "When the original encrypted payload is available, diagnostics must preserve it rather than substituting decrypted content.");
                Assert.IsFalse(
                    exception.EncryptedContent.Contains(knownSecret, StringComparison.Ordinal),
                    "EncryptedContent must never contain decrypted plaintext.");
            }
            finally
            {
                await item.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task GetItemAsync_WhenFirstSerializationFailsWithPooledEncryptedInput_PreservesOriginalContentAndReleasesBuffers()
        {
            TestDoc originalDoc = TestDoc.Create();
            const string knownSecret = "pooled-encrypted-diagnostic-secret";
            originalDoc.SensitiveStr = knownSecret;

            EncryptionOptions encryptionOptions = new ()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };

            Mock<Encryptor> mdeEncryptor = TestEncryptorFactory.CreateMde(dekId, out _);
            PooledMemoryStream encryptedInput = new ();
            StreamDecryptableItem item = null;

            try
            {
                await using Stream plaintextInput = originalDoc.ToStream();
                await EncryptionProcessor.EncryptAsync(
                    plaintextInput,
                    encryptedInput,
                    mdeEncryptor.Object,
                    encryptionOptions,
                    JsonProcessor.Stream,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None).ConfigureAwait(false);

                encryptedInput.Position = 0;
                string originalEncryptedContent = Encoding.UTF8.GetString(encryptedInput.ToArray());
                Assert.IsFalse(originalEncryptedContent.Contains(knownSecret, StringComparison.Ordinal));
                byte[] encryptedInputBuffer = encryptedInput.GetBuffer();

                FieldInfo cachedContentField = typeof(StreamDecryptableItem).GetField(
                    "cachedDecryptedContent",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(cachedContentField);

                InvalidOperationException serializerFailure = new ("First serializer call failed.");
                int serializerCalls = 0;
                Stream serializerInput = null;
                PooledMemoryStream cachedPlaintext = null;
                byte[] cachedPlaintextBuffer = null;

                Mock<CosmosSerializer> serializer = new ();
                serializer.Setup(s => s.FromStream<TestDoc>(It.IsAny<Stream>()))
                    .Callback<Stream>(stream =>
                    {
                        serializerCalls++;
                        serializerInput = stream;
                        cachedPlaintext = (PooledMemoryStream)cachedContentField.GetValue(item);
                        cachedPlaintextBuffer = cachedPlaintext.GetBuffer();
                    })
                    .Throws(serializerFailure);

                item = new StreamDecryptableItem(encryptedInput, mdeEncryptor.Object, serializer.Object);

                EncryptionException exception = await Assert.ThrowsExceptionAsync<EncryptionException>(
                    () => item.GetItemAsync<TestDoc>());

                Assert.AreSame(serializerFailure, exception.InnerException);
                Assert.AreEqual(1, serializerCalls, "The serializer must fail on its first invocation.");
                Assert.IsFalse(encryptedInput.CanRead, "The original pooled encrypted input must be disposed after failure.");
                Assert.IsNotNull(serializerInput);
                Assert.IsFalse(serializerInput.CanRead, "The decrypted adapter stream must be disposed after failure.");
                Assert.IsNotNull(cachedPlaintext);
                Assert.IsFalse(cachedPlaintext.CanRead, "The cached pooled plaintext stream must be disposed after failure.");
                Assert.IsNull(cachedContentField.GetValue(item), "The item must not retain the cached pooled plaintext stream.");
                Assert.IsTrue(encryptedInputBuffer.All(value => value == 0), "The original pooled input buffer must be cleared before return.");
                Assert.IsTrue(cachedPlaintextBuffer.All(value => value == 0), "The cached pooled plaintext buffer must be cleared before return.");
                Assert.AreEqual(
                    originalEncryptedContent,
                    exception.EncryptedContent,
                    "The exact encrypted JSON must survive the stream decrypt adapter disposing its pooled input.");
                Assert.IsFalse(exception.EncryptedContent.Contains(knownSecret, StringComparison.Ordinal));
            }
            finally
            {
                if (item != null)
                {
                    await item.DisposeAsync();
                }
                else
                {
                    await encryptedInput.DisposeAsync();
                }
            }
        }

        [TestMethod]
        public async Task GetItemAsync_WhenFirstSerializationFailsWithPooledPlaintextInput_RedactsContentAndReleasesBuffers()
        {
            const string knownSecret = "pooled-plaintext-diagnostic-secret";
            string originalPlaintext = $"{{\"id\":\"plain-item\",\"secret\":\"{knownSecret}\"}}";
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(originalPlaintext);
            PooledMemoryStream plaintextInput = new ();
            await plaintextInput.WriteAsync(plaintextBytes.AsMemory(0, plaintextBytes.Length));
            plaintextInput.Position = 0;
            byte[] plaintextInputBuffer = plaintextInput.GetBuffer();

            FieldInfo cachedContentField = typeof(StreamDecryptableItem).GetField(
                "cachedDecryptedContent",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(cachedContentField);

            InvalidOperationException serializerFailure = new ("First serializer call failed.");
            int serializerCalls = 0;
            Stream serializerInput = null;
            PooledMemoryStream cachedPlaintext = null;
            byte[] cachedPlaintextBuffer = null;
            StreamDecryptableItem item = null;

            Mock<CosmosSerializer> serializer = new ();
            serializer.Setup(s => s.FromStream<JObject>(It.IsAny<Stream>()))
                .Callback<Stream>(stream =>
                {
                    serializerCalls++;
                    serializerInput = stream;
                    cachedPlaintext = (PooledMemoryStream)cachedContentField.GetValue(item);
                    cachedPlaintextBuffer = cachedPlaintext.GetBuffer();
                })
                .Throws(serializerFailure);

            try
            {
                item = new StreamDecryptableItem(plaintextInput, mockEncryptor.Object, serializer.Object);

                EncryptionException exception = await Assert.ThrowsExceptionAsync<EncryptionException>(
                    () => item.GetItemAsync<JObject>());

                Assert.AreSame(serializerFailure, exception.InnerException);
                Assert.AreEqual(1, serializerCalls, "The serializer must fail on its first invocation.");
                Assert.AreSame(plaintextInput, serializerInput, "The unencrypted path should deserialize the original input stream.");
                Assert.IsFalse(plaintextInput.CanRead, "The original pooled plaintext input must be disposed after failure.");
                Assert.IsNotNull(cachedPlaintext);
                Assert.IsFalse(cachedPlaintext.CanRead, "The cached pooled plaintext stream must be disposed after failure.");
                Assert.IsNull(cachedContentField.GetValue(item), "The item must not retain the cached pooled plaintext stream.");
                Assert.IsTrue(plaintextInputBuffer.All(value => value == 0), "The original pooled input buffer must be cleared before return.");
                Assert.IsTrue(cachedPlaintextBuffer.All(value => value == 0), "The cached pooled plaintext buffer must be cleared before return.");
                Assert.AreNotEqual(
                    originalPlaintext,
                    exception.EncryptedContent,
                    "EncryptedContent must not expose the original plaintext item.");
                Assert.IsFalse(
                    (exception.EncryptedContent ?? string.Empty).Contains(knownSecret, StringComparison.Ordinal),
                    "EncryptedContent must not expose plaintext secrets.");
            }
            finally
            {
                if (item != null)
                {
                    await item.DisposeAsync();
                }
                else
                {
                    await plaintextInput.DisposeAsync();
                }
            }
        }

        [TestMethod]
        public async Task GetItemAsync_WhenDecryptedStreamAliasesContent_DisposesExactlyOnce()
        {
            TestDoc originalDoc = TestDoc.Create();
            byte[] plainBytes;
            using (MemoryStream ms = (MemoryStream)originalDoc.ToStream())
            {
                plainBytes = ms.ToArray();
            }

            TrackingStream trackingStream = new TrackingStream(plainBytes);

            StreamDecryptableItem item = new (trackingStream, mockEncryptor.Object, cosmosSerializer);
            try
            {
                (TestDoc result, DecryptionContext context) = await item.GetItemAsync<TestDoc>();

                Assert.IsNotNull(result);
                Assert.AreEqual(originalDoc.Id, result.Id);
                Assert.IsNull(context, "Plaintext path should yield null decryption context.");
                Assert.AreEqual(1, trackingStream.AsyncDisposeCallCount,
                    "Aliased stream must be disposed asynchronously exactly once; inner finally must skip via aliasing guard (regression: double-dispose).");
                Assert.AreEqual(2, trackingStream.SyncDisposeCallCount,
                    "Sync dispose fires twice: once via the serializer's StreamReader, once via DisposeContentStreamAsync's DisposeAsync->Dispose(true). Pre-fix would observe an additional double-dispose pair.");
            }
            finally
            {
                await item.DisposeAsync();
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
            EncryptionItemRequestOptions requestOptions = new EncryptionItemRequestOptions
            {
                EncryptionOptions = CreateEncryptionOptions()
            };

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                document.ToStream(),
                encryptor,
                requestOptions,
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
