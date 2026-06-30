//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using TestDoc = TestCommon.TestDoc;

#pragma warning disable CS0618 // Type or member is obsolete

    [TestClass]
    public class LegacyEncryptionProcessorTests
    {
        private static Mock<Encryptor> mockEncryptor;
        private static EncryptionOptions encryptionOptions;
        private const string dekId = "dekId";

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _ = testContext;
            LegacyEncryptionProcessorTests.encryptionOptions = new EncryptionOptions()
            {
                DataEncryptionKeyId = LegacyEncryptionProcessorTests.dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt
            };
            LegacyEncryptionProcessorTests.mockEncryptor = TestEncryptorFactory.CreateLegacy(dekId);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]

        public async Task InvalidPathToEncrypt(JsonProcessor jsonProcessor)
        {
            TestDoc testDoc = TestDoc.Create();
            EncryptionOptions encryptionOptionsWithInvalidPathToEncrypt = new ()
            {
                DataEncryptionKeyId = LegacyEncryptionProcessorTests.dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = new List<string>() { "/SensitiveStr", "/Invalid" }
            };

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                    testDoc.ToStream(),
                    LegacyEncryptionProcessorTests.mockEncryptor.Object,
                    encryptionOptionsWithInvalidPathToEncrypt,
                    jsonProcessor,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None);

            JObject encryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStream);

            (JObject decryptedDoc, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedDoc,
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            LegacyEncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                1,
                decryptionContext,
                invalidPathsConfigured: true);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task EncryptDecryptPropertyWithNullValue(JsonProcessor jsonProcessor)
        {
            TestDoc testDoc = TestDoc.Create();
            testDoc.SensitiveStr = null;

            JObject encryptedDoc = await LegacyEncryptionProcessorTests.VerifyEncryptionSucceeded(testDoc, jsonProcessor);

            (JObject decryptedDoc, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedDoc,
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            LegacyEncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task ValidateEncryptDecryptDocument(JsonProcessor jsonProcessor)
        {
            TestDoc testDoc = TestDoc.Create();

            JObject encryptedDoc = await LegacyEncryptionProcessorTests.VerifyEncryptionSucceeded(testDoc, jsonProcessor);

            (JObject decryptedDoc, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedDoc,
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            LegacyEncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task ValidateDecryptStream(JsonProcessor jsonProcessor)
        {
            TestDoc testDoc = TestDoc.Create();

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                LegacyEncryptionProcessorTests.encryptionOptions,
                jsonProcessor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedStream,
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            JObject decryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decryptedStream);
            LegacyEncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }

        [TestMethod]
        public async Task DecryptStreamWithoutEncryptedProperty()
        {
            TestDoc testDoc = TestDoc.Create();
            Stream docStream = testDoc.ToStream();

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                docStream,
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.IsTrue(decryptedStream.CanSeek);
            Assert.AreEqual(0, decryptedStream.Position);
            Assert.AreEqual(docStream.Length, decryptedStream.Length);
            Assert.IsNull(decryptionContext);
        }

#if NET8_0_OR_GREATER
        // Locks the contract that justifies removing JsonProcessor.Stream from JsonProcessors above: the legacy
        // AEAD algorithm does not support the Stream processor, so EncryptAsync rejects it at Validate. Without this
        // the unsupported combination would be silently uncovered once the dead data row is gone.
        [TestMethod]
        public async Task EncryptAsync_AeadStream_Throws_NotSupported()
        {
            TestDoc testDoc = TestDoc.Create();

            await Assert.ThrowsExceptionAsync<NotSupportedException>(() => EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                LegacyEncryptionProcessorTests.encryptionOptions,
                JsonProcessor.Stream,
                new CosmosDiagnosticsContext(),
                CancellationToken.None));
        }
#endif

        private static async Task<JObject> VerifyEncryptionSucceeded(TestDoc testDoc, JsonProcessor jsonProcessor)
        {
            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                LegacyEncryptionProcessorTests.encryptionOptions,
                jsonProcessor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            JObject encryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStream);
            
            Assert.AreEqual(testDoc.Id, encryptedDoc.Property("id").Value.Value<string>());
            Assert.AreEqual(testDoc.PK, encryptedDoc.Property(nameof(TestDoc.PK)).Value.Value<string>());
            Assert.AreEqual(testDoc.NonSensitive, encryptedDoc.Property(nameof(TestDoc.NonSensitive)).Value.Value<string>());
            Assert.IsNull(encryptedDoc.Property(nameof(TestDoc.SensitiveStr)));
            Assert.IsNull(encryptedDoc.Property(nameof(TestDoc.SensitiveInt)));

            JProperty eiJProp = encryptedDoc.Property(Constants.EncryptedInfo);
            Assert.IsNotNull(eiJProp);
            Assert.IsNotNull(eiJProp.Value);
            Assert.AreEqual(JTokenType.Object, eiJProp.Value.Type);
            EncryptionProperties encryptionProperties = ((JObject)eiJProp.Value).ToObject<EncryptionProperties>();

            Assert.IsNotNull(encryptionProperties);
            Assert.AreEqual(LegacyEncryptionProcessorTests.dekId, encryptionProperties.DataEncryptionKeyId);
            Assert.AreEqual(2, encryptionProperties.EncryptionFormatVersion);
            Assert.IsNotNull(encryptionProperties.EncryptedData);

            return encryptedDoc;
        }

        private static void VerifyDecryptionSucceeded(
            JObject decryptedDoc,
            TestDoc expectedDoc,
            int pathCount,
            DecryptionContext decryptionContext,
            bool invalidPathsConfigured = false)
        {
            Assert.AreEqual(expectedDoc.SensitiveStr, decryptedDoc.Property(nameof(TestDoc.SensitiveStr)).Value.Value<string>());
            Assert.AreEqual(expectedDoc.SensitiveInt, decryptedDoc.Property(nameof(TestDoc.SensitiveInt)).Value.Value<int>());
            Assert.IsNull(decryptedDoc.Property(Constants.EncryptedInfo));
            
            Assert.IsNotNull(decryptionContext);
            Assert.IsNotNull(decryptionContext.DecryptionInfoList);
            DecryptionInfo decryptionInfo = decryptionContext.DecryptionInfoList[0];
            Assert.AreEqual(LegacyEncryptionProcessorTests.dekId, decryptionInfo.DataEncryptionKeyId);
            Assert.AreEqual(pathCount, decryptionInfo.PathsDecrypted.Count);

            if (!invalidPathsConfigured)
            {
                Assert.IsFalse(TestDoc.PathsToEncrypt.Exists(path => !decryptionInfo.PathsDecrypted.Contains(path)));
            }
            else
            {
                Assert.IsTrue(TestDoc.PathsToEncrypt.Exists(path => !decryptionInfo.PathsDecrypted.Contains(path)));
            }
        }

        // AEAD (AEAes256CbcHmacSha256Randomized) is Newtonsoft-only by design: the Stream processor is rejected at
        // EncryptionOptions.Validate with NotSupportedException (locked by EncryptAsync_AeadStream_Throws_NotSupported
        // below). A JsonProcessor.Stream row here only ever threw - it was dead/misleading data once these
        // [TestMethod]s actually run (they were 'internal' before, so MSTest silently skipped them, UTA007) - so it
        // has been removed. The parameterized AEAD round-trip tests run under Newtonsoft only.
        public static IEnumerable<object[]> JsonProcessors
        {
            get
            {
                yield return new object[] { JsonProcessor.Newtonsoft };
            }
        }
    }

#pragma warning restore CS0618 // Type or member is obsolete
}
