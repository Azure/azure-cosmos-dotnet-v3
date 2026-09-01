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
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Tests;
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
        public async Task InvalidPathToEncrypt()
        {
            TestDoc testDoc = TestDoc.Create();
            EncryptionOptions encryptionOptionsWithInvalidPathToEncrypt = new ()
            {
                DataEncryptionKeyId = LegacyEncryptionProcessorTests.dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = new List<string>() { "/SensitiveStr", "/Invalid" }
            };

            EncryptionItemRequestOptions requestOptions = RequestOptionsOverrideHelper.Create(
                encryptionOptionsWithInvalidPathToEncrypt,
                JsonProcessor.Newtonsoft);

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                requestOptions,
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
        public async Task EncryptDecryptPropertyWithNullValue()
        {
            TestDoc testDoc = TestDoc.Create();
            testDoc.SensitiveStr = null;

            JObject encryptedDoc = await LegacyEncryptionProcessorTests.VerifyEncryptionSucceeded(testDoc);

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
        public async Task ValidateEncryptDecryptDocument()
        {
            TestDoc testDoc = TestDoc.Create();

            JObject encryptedDoc = await LegacyEncryptionProcessorTests.VerifyEncryptionSucceeded(testDoc);

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
        public async Task ValidateDecryptStream(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = ResolveJsonProcessor(jsonProcessorValue);
            TestDoc testDoc = TestDoc.Create();

            EncryptionItemRequestOptions requestOptions = RequestOptionsOverrideHelper.Create(LegacyEncryptionProcessorTests.encryptionOptions, JsonProcessor.Newtonsoft);

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                requestOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedStream,
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                RequestOptionsOverrideHelper.Create(jsonProcessor),
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

        [TestMethod]
        public async Task DecryptableItemCore_CorruptLegacyMetadataWithoutDekId_PreservesFailure()
        {
            TestDoc testDoc = TestDoc.Create();
            JObject encryptedDoc = await LegacyEncryptionProcessorTests.VerifyEncryptionSucceeded(testDoc);
            ((JObject)encryptedDoc[Constants.EncryptedInfo]).Remove(Constants.EncryptionDekId);
            string encryptedContent = encryptedDoc.ToString();
            DecryptableItemCore decryptableItem = new (
                encryptedDoc,
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                new Mock<CosmosSerializer>().Object);

            EncryptionException exception = await Assert.ThrowsExceptionAsync<EncryptionException>(
                async () => await decryptableItem.GetItemAsync<TestDoc>());

            Assert.AreEqual(string.Empty, exception.DataEncryptionKeyId);
            Assert.AreEqual(encryptedContent, exception.EncryptedContent);
            Assert.IsInstanceOfType(exception.InnerException, typeof(InvalidOperationException));
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task DecryptStream_TamperedLegacyCiphertext_PreservesFailure(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = ResolveJsonProcessor(jsonProcessorValue);
            using Stream encryptedStream = await TestCommon.CreateLegacyEncryptedStreamAsync(
                TestDoc.Create(),
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                LegacyEncryptionProcessorTests.dekId);
            JObject encryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStream);
            ((JObject)encryptedDoc[Constants.EncryptedInfo])[Constants.EncryptedData] =
                Convert.ToBase64String(new byte[] { 1 });
            using Stream tamperedStream = EncryptionProcessor.BaseSerializer.ToStream(encryptedDoc);

            await Assert.ThrowsExceptionAsync<JsonReaderException>(
                async () => await EncryptionProcessor.DecryptAsync(
                    tamperedStream,
                    LegacyEncryptionProcessorTests.mockEncryptor.Object,
                    new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create(jsonProcessor),
                    CancellationToken.None));

            Assert.IsTrue(tamperedStream.CanRead);
            Assert.AreEqual(
                jsonProcessor == JsonProcessor.Newtonsoft ? tamperedStream.Length : 0,
                tamperedStream.Position);
        }

        [TestMethod]
        [DynamicData(nameof(JsonProcessors))]
        public async Task DecryptStream_LegacyDecryptorNotSupported_PreservesFailure(int jsonProcessorValue)
        {
            JsonProcessor jsonProcessor = ResolveJsonProcessor(jsonProcessorValue);
            using Stream encryptedStream = await TestCommon.CreateLegacyEncryptedStreamAsync(
                TestDoc.Create(),
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                LegacyEncryptionProcessorTests.dekId);
            NotSupportedException decryptorFailure = new ("Legacy decryptor is unavailable.");
            Mock<Encryptor> failingEncryptor = new ();
            failingEncryptor
                .Setup(e => e.DecryptAsync(
                    It.IsAny<byte[]>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(decryptorFailure);

            NotSupportedException exception = await Assert.ThrowsExceptionAsync<NotSupportedException>(
                async () => await EncryptionProcessor.DecryptAsync(
                    encryptedStream,
                    failingEncryptor.Object,
                    new CosmosDiagnosticsContext(),
                    RequestOptionsOverrideHelper.Create(jsonProcessor),
                    CancellationToken.None));

            Assert.AreSame(decryptorFailure, exception);
            Assert.IsTrue(encryptedStream.CanRead);
            Assert.AreEqual(
                jsonProcessor == JsonProcessor.Newtonsoft ? encryptedStream.Length : 0,
                encryptedStream.Position);
        }

        private static async Task<JObject> VerifyEncryptionSucceeded(TestDoc testDoc)
        {
            EncryptionItemRequestOptions requestOptions = RequestOptionsOverrideHelper.Create(LegacyEncryptionProcessorTests.encryptionOptions, JsonProcessor.Newtonsoft);

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                LegacyEncryptionProcessorTests.mockEncryptor.Object,
                requestOptions,
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

        public static IEnumerable<object[]> JsonProcessors
        {
            get
            {
                yield return new object[] { (int)JsonProcessor.Newtonsoft };
#if NET8_0_OR_GREATER
                yield return new object[] { (int)JsonProcessor.Stream };
#endif
            }
        }

        private static JsonProcessor ResolveJsonProcessor(int value)
        {
            if (!Enum.IsDefined(typeof(JsonProcessor), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Invalid JsonProcessor value supplied to test.");
            }

            return (JsonProcessor)value;
        }
    }

#pragma warning restore CS0618 // Type or member is obsolete
}
