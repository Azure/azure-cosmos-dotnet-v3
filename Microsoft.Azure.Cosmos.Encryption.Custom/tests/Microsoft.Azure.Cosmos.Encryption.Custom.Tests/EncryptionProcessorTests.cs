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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using TestDoc = TestCommon.TestDoc;
#if NET8_0_OR_GREATER
    using Microsoft.Azure.Cosmos;
#endif

    [TestClass]
    public class EncryptionProcessorTests
    {
        private static Mock<Encryptor> mockEncryptor;
        private const string DekId = "dekId";

        [ClassInitialize]
        public static void ClassInitialize(TestContext ctx)
        {
            _ = ctx;
            mockEncryptor = TestEncryptorFactory.CreateMde(DekId, out _);
        }

#if NET8_0_OR_GREATER
        private static EncryptionOptions CreateMdeOptions()
        {
            return new()
            {
                DataEncryptionKeyId = DekId,
#pragma warning disable CS0618
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };
        }

        [TestMethod]
        public async Task EncryptDecrypt_StreamProcessor_WithProvidedOutput()
        {
            TestDoc doc = TestDoc.Create();
            EncryptionOptions opts = CreateMdeOptions();
            
            // Capture activities to validate scopes are created
            List<Activity> capturedActivities = new List<Activity>();
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => { lock (capturedActivities) { capturedActivities.Add(activity); } }
            };
            ActivitySource.AddActivityListener(listener);
            
            CosmosDiagnosticsContext diagEncrypt = CosmosDiagnosticsContext.Create(null);
            MemoryStream encrypted = new();
            await EncryptionProcessor.EncryptAsync(doc.ToStream(), encrypted, mockEncryptor.Object, opts, JsonProcessor.Stream, diagEncrypt, CancellationToken.None);
            encrypted.Position = 0;

            CosmosDiagnosticsContext diagDecrypt = CosmosDiagnosticsContext.Create(null);
            MemoryStream decryptedOut = new();
            ItemRequestOptions requestOptions = new() { Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream } } };
            DecryptionContext ctx = await EncryptionProcessor.DecryptAsync(encrypted, decryptedOut, mockEncryptor.Object, diagDecrypt, requestOptions, CancellationToken.None);

            decryptedOut.Position = 0;
            JObject decryptedObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decryptedOut);
            Assert.AreEqual(doc.SensitiveStr, decryptedObj.Property(nameof(TestDoc.SensitiveStr)).Value.Value<string>());
            Assert.IsNull(decryptedObj.Property(Constants.EncryptedInfo));
            Assert.IsNotNull(ctx);
            Assert.IsTrue(ctx.DecryptionInfoList.First().PathsDecrypted.All(p => TestDoc.PathsToEncrypt.Contains(p)));
            
            // Validate diagnostic scopes were created
            string expectedEncryptScope = CosmosDiagnosticsContext.ScopeEncryptModeSelectionPrefix + JsonProcessor.Stream;
            string expectedDecryptScope = CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream;
            lock (capturedActivities)
            {
                Assert.IsTrue(capturedActivities.Any(a => a.DisplayName == expectedEncryptScope),
                    $"Expected encrypt scope '{expectedEncryptScope}' not found. Activities: {string.Join(", ", capturedActivities.Select(a => a.DisplayName))}");
                Assert.IsTrue(capturedActivities.Any(a => a.DisplayName == expectedDecryptScope),
                    $"Expected decrypt scope '{expectedDecryptScope}' not found. Activities: {string.Join(", ", capturedActivities.Select(a => a.DisplayName))}");
            }
        }

        [TestMethod]
        public async Task Encrypt_NewtonsoftProcessor_Works()
        {
            TestDoc doc = TestDoc.Create();
            EncryptionOptions opts = CreateMdeOptions();
        
            // Capture activities to validate scopes are created
            List<Activity> capturedActivities = new List<Activity>();
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => { lock (capturedActivities) { capturedActivities.Add(activity); } }
            };
            ActivitySource.AddActivityListener(listener);
        
            CosmosDiagnosticsContext diagEncrypt = CosmosDiagnosticsContext.Create(null);
            EncryptionItemRequestOptions encryptRequest = RequestOptionsOverrideHelper.Create(opts, JsonProcessor.Newtonsoft);
            Stream encrypted = await EncryptionProcessor.EncryptAsync(doc.ToStream(), mockEncryptor.Object, encryptRequest, diagEncrypt, CancellationToken.None);

            Assert.IsNotNull(encrypted);
            encrypted.Dispose();
        
            // Validate Newtonsoft encrypt scope was created
            string expectedEncryptScope = CosmosDiagnosticsContext.ScopeEncryptModeSelectionPrefix + JsonProcessor.Newtonsoft;
            lock (capturedActivities)
            {
                Assert.IsTrue(capturedActivities.Any(a => a.DisplayName == expectedEncryptScope),
                    $"Expected Newtonsoft encrypt scope '{expectedEncryptScope}' not found. Activities: {string.Join(", ", capturedActivities.Select(a => a.DisplayName))}");
            }
        }

        [TestMethod]
        public async Task Decrypt_StreamSelection_FallbackWhenUnencrypted()
        {
            string json = "{\"id\":\"id1\",\"pk\":\"pk1\",\"NonSensitive\":\"v\"}"; // no _ei
            MemoryStream input = new(System.Text.Encoding.UTF8.GetBytes(json));
            CosmosDiagnosticsContext ctxDiag = CosmosDiagnosticsContext.Create(null);
            ItemRequestOptions opts = new() { Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream } } };
            (Stream result, DecryptionContext ctxDec) = await EncryptionProcessor.DecryptAsync(input, mockEncryptor.Object, ctxDiag, opts, CancellationToken.None);
            Assert.IsNull(ctxDec);
            Assert.AreEqual(0, result.Position);
        }
#endif

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task Decrypt_StreamSelection_LegacyAlgorithm_FallsBackToNewtonsoft()
        {
            List<Activity> capturedActivities = new ();
            using ActivityListener listener = new ()
            {
                ShouldListenTo = source => source.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => capturedActivities.Add(activity),
            };
            ActivitySource.AddActivityListener(listener);
            TestDoc doc = TestDoc.Create();
            using Stream legacyEncrypted = await TestCommon.CreateLegacyEncryptedStreamAsync(
                doc,
                mockEncryptor.Object,
                DekId);

            ItemRequestOptions opts = new() { Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" } } };
            CosmosDiagnosticsContext diag = CosmosDiagnosticsContext.Create(null);

            // Legacy algorithm should decrypt successfully by falling back to the legacy decryption path
            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(legacyEncrypted, mockEncryptor.Object, diag, opts, CancellationToken.None);

            Assert.IsNotNull(decrypted);
            Assert.IsNotNull(context);
            Assert.AreEqual(0, decrypted.Position);
            Assert.IsFalse(legacyEncrypted.CanRead);
            TestDoc result = TestCommon.FromStream<TestDoc>(decrypted);
            Assert.AreEqual(doc, result);
            Assert.IsTrue(capturedActivities.Any(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream));
            Assert.IsTrue(capturedActivities.Any(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft));
        }

        [TestMethod]
        public async Task DecryptProvidedOutput_StreamSelection_LegacyAlgorithm_FallsBackToNewtonsoft()
        {
            List<Activity> capturedActivities = new ();
            using ActivityListener listener = new ()
            {
                ShouldListenTo = source => source.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => capturedActivities.Add(activity),
            };
            ActivitySource.AddActivityListener(listener);
            TestDoc doc = TestDoc.Create();
            using Stream legacyEncrypted = await TestCommon.CreateLegacyEncryptedStreamAsync(
                doc,
                mockEncryptor.Object,
                DekId);

            ItemRequestOptions opts = new() { Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" } } };
            CosmosDiagnosticsContext diag = CosmosDiagnosticsContext.Create(null);
            MemoryStream output = new();

            DecryptionContext context = await EncryptionProcessor.DecryptAsync(
                legacyEncrypted,
                output,
                mockEncryptor.Object,
                diag,
                opts,
                CancellationToken.None);

            Assert.IsNotNull(context);
            AssertLegacyDecryptionContext(context);
            Assert.AreEqual(0, output.Position);
            Assert.IsFalse(legacyEncrypted.CanRead);
            TestDoc actual = TestCommon.FromStream<TestDoc>(output);
            Assert.AreEqual(doc, actual);
            Assert.IsTrue(capturedActivities.Any(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream));
            Assert.IsTrue(capturedActivities.Any(activity =>
                activity.DisplayName == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft));
        }

        [TestMethod]
        public async Task DecryptCore_StreamSelection_LegacyAlgorithm_ParsesNewtonsoftOnce()
        {
            TestDoc expected = TestDoc.Create();
            using Stream encrypted = await TestCommon.CreateLegacyEncryptedStreamAsync(
                expected,
                mockEncryptor.Object,
                DekId);
            using SingleSynchronousReadPassStream input = new (((MemoryStream)encrypted).ToArray());

            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(
                input,
                mockEncryptor.Object,
                JsonProcessor.Stream,
                legacyFallback: true,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            using (decrypted)
            {
                Assert.IsNotNull(context);
                AssertLegacyDecryptionContext(context);
                Assert.AreEqual(0, decrypted.Position);
                Assert.AreEqual(expected, TestCommon.FromStream<TestDoc>(decrypted));
            }

            Assert.IsFalse(input.CanRead);
        }

        [TestMethod]
        public async Task DecryptCore_StreamSelection_PlaintextDoesNotUseNewtonsoftProbe()
        {
            byte[] plaintext = System.Text.Encoding.UTF8.GetBytes(
                "{\"id\":\"id1\",\"pk\":\"pk1\",\"NonSensitive\":\"value\"}");
            using SynchronousReadTrackingStream input = new (plaintext);

            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(
                input,
                mockEncryptor.Object,
                JsonProcessor.Stream,
                legacyFallback: true,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.AreSame(input, decrypted);
            Assert.IsNull(context);
            Assert.AreEqual(0, decrypted.Position);
            Assert.AreEqual(0, input.SynchronousReadCount);
            CollectionAssert.AreEqual(plaintext, input.ToArray());
        }

        [TestMethod]
        public async Task DecryptProvidedOutput_StreamSelection_LegacyAlgorithm_RejectsNonSeekableOutputBeforeDecrypt()
        {
            Mock<Encryptor> encryptor = TestEncryptorFactory.CreateLegacy(DekId);
            using Stream encrypted = await TestCommon.CreateLegacyEncryptedStreamAsync(
                TestDoc.Create(),
                encryptor.Object,
                DekId);
            encryptor.ResetCalls();
            using NonSeekableWriteTrackingStream output = new ();

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                async () => await EncryptionProcessor.DecryptAsync(
                    encrypted,
                    output,
                    encryptor.Object,
                    new CosmosDiagnosticsContext(),
                    new ItemRequestOptions
                    {
                        Properties = new Dictionary<string, object>
                        {
                            { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream },
                        },
                    },
                    CancellationToken.None));

            Assert.AreEqual("output", exception.ParamName);
            Assert.AreEqual(0, output.BytesWritten);
            encryptor.Verify(
                e => e.DecryptAsync(
                    It.IsAny<byte[]>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            Assert.IsTrue(encrypted.CanRead);
            Assert.AreEqual(0, encrypted.Position);
        }

        [TestMethod]
        public async Task Encrypt_LegacyAlgorithm_StreamProcessor_Throws()
        {
            TestDoc doc = TestDoc.Create();
            Mock<Encryptor> encryptor = new ();
            EncryptionItemRequestOptions ro = new() 
            { 
                EncryptionOptions = TestCommon.CreateLegacyEncryptionOptions(DekId),
                Properties = new Dictionary<string, object> { { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" } } 
            };

            CosmosDiagnosticsContext diag = CosmosDiagnosticsContext.Create(null);

            try
            {
                await EncryptionProcessor.EncryptAsync(doc.ToStream(), encryptor.Object, ro, diag, CancellationToken.None);
                Assert.Fail("Expected NotSupportedException for legacy algorithm with Stream processor override.");
            }
            catch (NotSupportedException ex)
            {
                Assert.IsTrue(ex.Message.IndexOf("not supported", StringComparison.OrdinalIgnoreCase) >= 0, $"Unexpected message: {ex.Message}");
            }

            encryptor.Verify(
                e => e.EncryptAsync(
                    It.IsAny<byte[]>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestMethod]
        public async Task EncryptProvidedOutput_LegacyAlgorithm_StreamProcessor_ThrowsBeforeDispatch()
        {
            using Stream input = TestDoc.Create().ToStream();
            using MemoryStream output = new ();
            Mock<Encryptor> encryptor = new ();

            await Assert.ThrowsExceptionAsync<NotSupportedException>(
                async () => await EncryptionProcessor.EncryptAsync(
                    input,
                    output,
                    encryptor.Object,
                    TestCommon.CreateLegacyEncryptionOptions(DekId),
                    JsonProcessor.Stream,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None));

            encryptor.Verify(
                e => e.EncryptAsync(
                    It.IsAny<byte[]>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            Assert.AreEqual(0, input.Position);
            Assert.AreEqual(0, output.Length);
            Assert.IsTrue(input.CanRead);
        }

        [TestMethod]
        public async Task ConvertResponseToDecryptableItemsAsync_Stream_WhenSplitterThrowsMidFeed_PreservesOriginalException()
        {
            // Regression: if the splitter throws after yielding one or more documents (here, a partial
            // feed: one complete document then a transport error), the StreamDecryptableItems held in the
            // method's local list must be drained and the original exception rethrown unchanged - rather
            // than abandoning the list (and its pooled buffers) or wrapping/swallowing the failure.
            byte[] partialFeed = System.Text.Encoding.UTF8.GetBytes("{\"_count\":2,\"Documents\":[{\"id\":\"doc1\",\"pk\":\"pk\"},");
            IOException sentinel = new ("simulated mid-feed transport error");

            using ThrowAfterPrefixStream stream = new (partialFeed, sentinel);

            Mock<CosmosSerializer> serializerMock = new ();

            IOException thrown = await Assert.ThrowsExceptionAsync<IOException>(async () =>
            {
                _ = await EncryptionProcessor.ConvertResponseToDecryptableItemsAsync(
                    stream,
                    mockEncryptor.Object,
                    serializerMock.Object,
                    JsonProcessor.Stream,
                    CancellationToken.None);
            });

            Assert.AreSame(sentinel, thrown, "Original exception identity must be preserved through the orphan-cleanup catch path.");
        }

        private sealed class ThrowAfterPrefixStream : Stream
        {
            private readonly byte[] prefix;
            private readonly Exception toThrow;
            private int position;

            public ThrowAfterPrefixStream(byte[] prefix, Exception toThrow)
            {
                this.prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
                this.toThrow = toThrow ?? throw new ArgumentNullException(nameof(toThrow));
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => this.position;
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (this.position >= this.prefix.Length)
                {
                    throw this.toThrow;
                }

                int available = this.prefix.Length - this.position;
                int toCopy = Math.Min(available, count);
                Buffer.BlockCopy(this.prefix, this.position, buffer, offset, toCopy);
                this.position += toCopy;
                return toCopy;
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (this.position >= this.prefix.Length)
                {
                    return Task.FromException<int>(this.toThrow);
                }

                return Task.FromResult(this.Read(buffer, offset, count));
            }

#if NET8_0_OR_GREATER
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (this.position >= this.prefix.Length)
                {
                    return ValueTask.FromException<int>(this.toThrow);
                }

                int available = this.prefix.Length - this.position;
                int toCopy = Math.Min(available, buffer.Length);
                this.prefix.AsSpan(this.position, toCopy).CopyTo(buffer.Span);
                this.position += toCopy;
                return new ValueTask<int>(toCopy);
            }
#endif

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private sealed class SingleSynchronousReadPassStream : MemoryStream
        {
            private readonly byte[] content;
            private bool synchronousPassCompleted;
            private bool synchronousReadReachedEnd;

            public SingleSynchronousReadPassStream(byte[] buffer)
                : base(buffer)
            {
                this.content = buffer;
            }

            public override long Position
            {
                get => base.Position;
                set
                {
                    if (value == 0 && base.Position != 0 && this.synchronousReadReachedEnd)
                    {
                        this.synchronousPassCompleted = true;
                    }

                    base.Position = value;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                this.ThrowIfSynchronousPassCompleted();
                int read = base.Read(buffer, offset, count);
                this.synchronousReadReachedEnd |= base.Position == base.Length;
                return read;
            }

            public override int Read(Span<byte> buffer)
            {
                this.ThrowIfSynchronousPassCompleted();
                int read = base.Read(buffer);
                this.synchronousReadReachedEnd |= base.Position == base.Length;
                return read;
            }

            public override Task<int> ReadAsync(
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken)
            {
                int read = this.ReadForAsync(buffer.AsSpan(offset, count));
                return Task.FromResult(read);
            }

            public override ValueTask<int> ReadAsync(
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                return new ValueTask<int>(this.ReadForAsync(buffer.Span));
            }

            private int ReadForAsync(Span<byte> buffer)
            {
                int read = Math.Min(buffer.Length, checked((int)(base.Length - base.Position)));
                this.content.AsSpan(checked((int)base.Position), read).CopyTo(buffer);
                base.Position += read;
                return read;
            }

            private void ThrowIfSynchronousPassCompleted()
            {
                if (this.synchronousPassCompleted)
                {
                    throw new InvalidOperationException("Legacy fallback parsed the input stream more than once.");
                }
            }
        }

        private sealed class SynchronousReadTrackingStream : MemoryStream
        {
            private readonly byte[] content;

            public SynchronousReadTrackingStream(byte[] buffer)
                : base(buffer)
            {
                this.content = buffer;
            }

            public int SynchronousReadCount { get; private set; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                this.SynchronousReadCount++;
                return base.Read(buffer, offset, count);
            }

            public override int Read(Span<byte> buffer)
            {
                this.SynchronousReadCount++;
                return base.Read(buffer);
            }

            public override Task<int> ReadAsync(
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(this.ReadForAsync(buffer.AsSpan(offset, count)));
            }

            public override ValueTask<int> ReadAsync(
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                return new ValueTask<int>(this.ReadForAsync(buffer.Span));
            }

            private int ReadForAsync(Span<byte> buffer)
            {
                int read = Math.Min(buffer.Length, checked((int)(base.Length - base.Position)));
                this.content.AsSpan(checked((int)base.Position), read).CopyTo(buffer);
                base.Position += read;
                return read;
            }
        }

        private sealed class NonSeekableWriteTrackingStream : Stream
        {
            private readonly MemoryStream inner = new ();

            public int BytesWritten => checked((int)this.inner.Length);

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => this.inner.Length;

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
                this.inner.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                this.inner.Write(buffer, offset, count);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }
#endif

        [TestMethod]
        public async Task Decrypt_UnsupportedJsonProcessorWithLegacyCiphertext_Throws()
        {
            using Stream encrypted = await TestCommon.CreateLegacyEncryptedStreamAsync(
                TestDoc.Create(),
                mockEncryptor.Object,
                DekId);
            ItemRequestOptions requestOptions = new ()
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, (JsonProcessor)99 },
                },
            };

            NotSupportedException exception = await Assert.ThrowsExceptionAsync<NotSupportedException>(
                async () => await EncryptionProcessor.DecryptAsync(
                    encrypted,
                    mockEncryptor.Object,
                    new CosmosDiagnosticsContext(),
                    requestOptions,
                    CancellationToken.None));

            StringAssert.Contains(exception.Message, "JsonProcessor");
            Assert.IsTrue(encrypted.CanRead);
            Assert.AreEqual(0, encrypted.Position);
        }

        [TestMethod]
        public async Task DecryptProvidedOutput_UnsupportedJsonProcessorWithLegacyCiphertext_ThrowsWithoutWriting()
        {
            using Stream encrypted = await TestCommon.CreateLegacyEncryptedStreamAsync(
                TestDoc.Create(),
                mockEncryptor.Object,
                DekId);
            using MemoryStream output = new ();
            ItemRequestOptions requestOptions = new ()
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, (JsonProcessor)99 },
                },
            };

            NotSupportedException exception = await Assert.ThrowsExceptionAsync<NotSupportedException>(
                async () => await EncryptionProcessor.DecryptAsync(
                    encrypted,
                    output,
                    mockEncryptor.Object,
                    new CosmosDiagnosticsContext(),
                    requestOptions,
                    CancellationToken.None));

            StringAssert.Contains(exception.Message, "JsonProcessor");
            Assert.AreEqual(0, output.Length);
            Assert.IsTrue(encrypted.CanRead);
            Assert.AreEqual(0, encrypted.Position);
        }

        [TestMethod]
        public async Task DecryptProvidedOutput_Newtonsoft_RewindsInputBeforeParsing()
        {
            TestDoc expected = TestDoc.Create();
            using Stream encrypted = await TestCommon.CreateLegacyEncryptedStreamAsync(
                expected,
                mockEncryptor.Object,
                DekId);
            encrypted.Position = encrypted.Length;
            using MemoryStream output = new ();

            DecryptionContext context = await EncryptionProcessor.DecryptAsync(
                encrypted,
                output,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                requestOptions: null,
                CancellationToken.None);

            Assert.IsNotNull(context);
            AssertLegacyDecryptionContext(context);
            Assert.AreEqual(0, output.Position);
            Assert.AreEqual(expected, TestCommon.FromStream<TestDoc>(output));
        }

        [TestMethod]
        [DynamicData(nameof(SupportedJsonProcessors))]
        public async Task Decrypt_MissingAlgorithmMetadata_ReturnsInputUnchanged(int jsonProcessorValue)
        {
            byte[] plaintext = System.Text.Encoding.UTF8.GetBytes(
                "{\"id\":\"id1\",\"Sensitive\":\"plaintext\",\"_ei\":{\"_ef\":3,\"_en\":\"dekId\",\"_ep\":[\"/Sensitive\"]}}");
            using MemoryStream input = new (plaintext);

            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(
                input,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                RequestOptionsOverrideHelper.Create((JsonProcessor)jsonProcessorValue),
                CancellationToken.None);

            Assert.AreSame(input, decrypted);
            Assert.IsNull(context);
            Assert.AreEqual(0, decrypted.Position);
            CollectionAssert.AreEqual(plaintext, input.ToArray());
        }

        [TestMethod]
        [DynamicData(nameof(SupportedJsonProcessors))]
        public async Task Decrypt_PresentUnknownAlgorithmMetadata_FailsClosed(int jsonProcessorValue)
        {
            byte[] payload = System.Text.Encoding.UTF8.GetBytes(
                "{\"id\":\"id1\",\"Sensitive\":\"ciphertext\",\"_ei\":{\"_ef\":3,\"_ea\":\"future-algorithm\",\"_en\":\"dekId\",\"_ep\":[\"/Sensitive\"]}}");
            using MemoryStream input = new (payload);

            NotSupportedException exception = await Assert.ThrowsExceptionAsync<NotSupportedException>(
                async () => await EncryptionProcessor.DecryptAsync(
                    input,
                    mockEncryptor.Object,
                    new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create((JsonProcessor)jsonProcessorValue),
                    CancellationToken.None));

            StringAssert.Contains(exception.Message, "future-algorithm");
            Assert.IsTrue(input.CanRead);
        }

        [TestMethod]
        public async Task DecryptProvidedOutput_Newtonsoft_LegacyAlgorithm_Succeeds()
        {
            TestDoc expected = TestDoc.Create();
            using Stream encrypted = await TestCommon.CreateLegacyEncryptedStreamAsync(
                expected,
                mockEncryptor.Object,
                DekId);
            using MemoryStream output = new ();

            DecryptionContext context = await EncryptionProcessor.DecryptAsync(
                encrypted,
                output,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                requestOptions: null,
                CancellationToken.None);

            Assert.IsNotNull(context);
            AssertLegacyDecryptionContext(context);
            Assert.AreEqual(0, output.Position);
            Assert.IsFalse(encrypted.CanRead);
            TestDoc actual = TestCommon.FromStream<TestDoc>(output);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public async Task DecryptProvidedOutput_NullInput_ReturnsNullWithoutChangingOutput()
        {
            using MemoryStream output = new (new byte[] { 1, 2, 3 });
            output.Position = 1;

            DecryptionContext context = await EncryptionProcessor.DecryptAsync(
                input: null,
                output,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                requestOptions: null,
                CancellationToken.None);

            Assert.IsNull(context);
            Assert.AreEqual(1, output.Position);
            Assert.AreEqual(3, output.Length);
        }

        public static IEnumerable<object[]> SupportedJsonProcessors
        {
            get
            {
                yield return new object[] { (int)JsonProcessor.Newtonsoft };
#if NET8_0_OR_GREATER
                yield return new object[] { (int)JsonProcessor.Stream };
#endif
            }
        }

        private static void AssertLegacyDecryptionContext(DecryptionContext context)
        {
            Assert.AreEqual(1, context.DecryptionInfoList.Count);
            DecryptionInfo decryptionInfo = context.DecryptionInfoList[0];
            Assert.AreEqual(DekId, decryptionInfo.DataEncryptionKeyId);
            CollectionAssert.AreEquivalent(
                TestDoc.PathsToEncrypt,
                decryptionInfo.PathsDecrypted.ToList());
        }
    }
}
