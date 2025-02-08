//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
#if NET8_0_OR_GREATER
    using System.Text.Json.Nodes;
#endif
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
#if NET8_0_OR_GREATER
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
#endif
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using TestDoc = TestCommon.TestDoc;

    [TestClass]
    public class MdeEncryptionProcessorTests
    {
        private static Mock<Encryptor> mockEncryptor;
        private const string dekId = "dekId";

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _ = testContext;

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
            StreamProcessor.InitialBufferSize = 16; //we force smallest possible initial buffer to make sure both secondary reads and resize paths are executed
#endif

            Mock<DataEncryptionKey> DekMock = new();
            DekMock.Setup(m => m.EncryptData(It.IsAny<byte[]>()))
                .Returns((byte[] plainText) => TestCommon.EncryptData(plainText));
            DekMock.Setup(m => m.GetEncryptByteCount(It.IsAny<int>()))
                .Returns((int plainTextLength) => plainTextLength);
            DekMock.Setup(m => m.EncryptData(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<int>()))
                .Returns((byte[] plainText, int plainTextOffset, int plainTextLength, byte[] output, int outputOffset) => TestCommon.EncryptData(plainText, plainTextOffset, plainTextLength, output, outputOffset));
            DekMock.Setup(m => m.DecryptData(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<int>()))
                .Returns((byte[] plainText, int plainTextOffset, int plainTextLength, byte[] output, int outputOffset) => TestCommon.DecryptData(plainText, plainTextOffset, plainTextLength, output, outputOffset));
            DekMock.Setup(m => m.GetDecryptByteCount(It.IsAny<int>()))
                .Returns((int cipherTextLength) => cipherTextLength);


            mockEncryptor = new Mock<Encryptor>();
            mockEncryptor.Setup(m => m.GetEncryptionKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string dekId, string algorithm, CancellationToken token) =>
                    dekId == MdeEncryptionProcessorTests.dekId ? DekMock.Object : throw new InvalidOperationException("DEK not found."));

            mockEncryptor.Setup(m => m.EncryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] plainText, string dekId, string algo, CancellationToken t) =>
                    dekId == MdeEncryptionProcessorTests.dekId ? TestCommon.EncryptData(plainText) : throw new InvalidOperationException("DEK not found."));

            mockEncryptor.Setup(m => m.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] cipherText, string dekId, string algo, CancellationToken t) =>
                    dekId == MdeEncryptionProcessorTests.dekId ? TestCommon.DecryptData(cipherText) : throw new InvalidOperationException("Null DEK was returned."));
        }

        [TestMethod]
        [DataRow(JsonProcessor.Newtonsoft)]
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        [DataRow(JsonProcessor.Stream)]
#endif
        public async Task InvalidPathToEncrypt(JsonProcessor jsonProcessor)
        {
            TestDoc testDoc = TestDoc.Create();
            EncryptionOptions encryptionOptionsWithInvalidPathToEncrypt = new()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = new List<string>() { "/SensitiveStr", "/Invalid" },
                JsonProcessor = jsonProcessor,
            };

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                   testDoc.ToStream(),
                   mockEncryptor.Object,
                   encryptionOptionsWithInvalidPathToEncrypt,
                   new CosmosDiagnosticsContext(),
                   CancellationToken.None);


            JObject encryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStream);

            (JObject decryptedDoc, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
               encryptedDoc,
               mockEncryptor.Object,
               new CosmosDiagnosticsContext(),
               CancellationToken.None);

            VerifyDecryptionSucceeded(
                 decryptedDoc,
                 testDoc,
                 1,
                 decryptionContext,
                 invalidPathsConfigured: true);
        }

        [TestMethod]
        [DataRow(JsonProcessor.Newtonsoft)]
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        [DataRow(JsonProcessor.Stream)]
#endif
        public async Task DuplicatePathToEncrypt(JsonProcessor jsonProcessor)
        {
            TestDoc testDoc = TestDoc.Create();
            EncryptionOptions encryptionOptionsWithDuplicatePathToEncrypt = new()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = new List<string>() { "/SensitiveStr", "/SensitiveStr" },
                JsonProcessor = jsonProcessor,
            };

            try
            {
                await EncryptionProcessor.EncryptAsync(
                    testDoc.ToStream(),
                    mockEncryptor.Object,
                    encryptionOptionsWithDuplicatePathToEncrypt,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None);

                Assert.Fail("Duplicate paths in PathToEncrypt didn't result in exception.");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("Duplicate paths in PathsToEncrypt passed via EncryptionOptions.", ex.Message);
            }
        }

        [TestMethod]
        [DynamicData(nameof(EncryptionOptionsCombinations))]
        public async Task EncryptDecryptPropertyWithNullValue_VerifyByNewtonsoft(EncryptionOptions encryptionOptions)
        {
            TestDoc testDoc = TestDoc.Create();
            testDoc.SensitiveStr = null;

            JObject encryptedDoc = await VerifyEncryptionSucceededNewtonsoft(testDoc, encryptionOptions);

            (JObject decryptedDoc, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
               encryptedDoc,
               mockEncryptor.Object,
               new CosmosDiagnosticsContext(),
               CancellationToken.None);

            VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }

        [TestMethod]
        [DynamicData(nameof(EncryptionOptionsCombinations))]
        public async Task ValidateEncryptDecryptDocument_VerifyByNewtonsoft(EncryptionOptions encryptionOptions)
        {
            TestDoc testDoc = TestDoc.Create();

            JObject encryptedDoc = await VerifyEncryptionSucceededNewtonsoft(testDoc, encryptionOptions);

            (JObject decryptedDoc, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedDoc,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }

        [TestMethod]
        [DynamicData(nameof(EncryptionOptionsCombinations))]
        public async Task ValidateDecryptByNewtonsoftStream_VerifyByNewtonsoft(EncryptionOptions encryptionOptions)
        {
            TestDoc testDoc = TestDoc.Create();

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                mockEncryptor.Object,
                encryptionOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedStream,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                JsonProcessor.Newtonsoft,
                CancellationToken.None);

            JObject decryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decryptedStream);
            VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }

        [TestMethod]
        [DynamicData(nameof(EncryptionOptionsStreamTestCombinations))]
        public async Task ValidateDecryptBySystemTextStream_VerifyByNewtonsoft(EncryptionOptions encryptionOptions, JsonProcessor decryptionJsonProcessor)
        {
            TestDoc testDoc = TestDoc.Create();

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                mockEncryptor.Object,
                encryptionOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedStream,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                decryptionJsonProcessor,
                CancellationToken.None);

            JObject decryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decryptedStream);
            VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        [TestMethod]
        [DynamicData(nameof(EncryptionOptionsStreamTestCombinations))]
        public async Task ValidateDecryptBySystemTextStream_VerifyBySystemText(EncryptionOptions encryptionOptions, JsonProcessor decryptionJsonProcessor)
        {
            TestDoc testDoc = TestDoc.Create();

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                mockEncryptor.Object,
                encryptionOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedStream,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                decryptionJsonProcessor,
                CancellationToken.None);

            JsonNode decryptedDoc = JsonNode.Parse(decryptedStream);
            VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                TestDoc.PathsToEncrypt.Count,
                decryptionContext);
        }
#endif

        [TestMethod]
        [DataRow(JsonProcessor.Newtonsoft)]
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        [DataRow(JsonProcessor.Stream)]
#endif
        public async Task DecryptStreamWithoutEncryptedProperty(JsonProcessor processor)
        {
            TestDoc testDoc = TestDoc.Create();
            Stream docStream = testDoc.ToStream();

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                docStream,
                mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                processor,
                CancellationToken.None);

            Assert.IsTrue(decryptedStream.CanSeek);
            Assert.AreEqual(0, decryptedStream.Position);
            Assert.AreEqual(docStream.Length, decryptedStream.Length);
            Assert.IsNull(decryptionContext);
        }

        private static async Task<JObject> VerifyEncryptionSucceededNewtonsoft(TestDoc testDoc, EncryptionOptions encryptionOptions)
        {
            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                 testDoc.ToStream(),
                 mockEncryptor.Object,
                 encryptionOptions,
                 new CosmosDiagnosticsContext(),
                 CancellationToken.None);

            JObject encryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStream);

            Assert.AreEqual(testDoc.Id, encryptedDoc.Property("id").Value.Value<string>());
            Assert.AreEqual(testDoc.PK, encryptedDoc.Property(nameof(TestDoc.PK)).Value.Value<string>());
            Assert.AreEqual(testDoc.NonSensitive, encryptedDoc.Property(nameof(TestDoc.NonSensitive)).Value.Value<string>());
            Assert.IsNotNull(encryptedDoc.Property(nameof(TestDoc.SensitiveInt)).Value.Value<string>());
            Assert.AreNotEqual(testDoc.SensitiveInt.ToString(), encryptedDoc.Property(nameof(TestDoc.SensitiveInt)).Value.Value<string>()); // not equal since value is encrypted

            JProperty eiJProp = encryptedDoc.Property(Constants.EncryptedInfo);
            Assert.IsNotNull(eiJProp);
            Assert.IsNotNull(eiJProp.Value);
            Assert.AreEqual(JTokenType.Object, eiJProp.Value.Type);
            EncryptionProperties encryptionProperties = ((JObject)eiJProp.Value).ToObject<EncryptionProperties>();

            Assert.IsNotNull(encryptionProperties);
            Assert.AreEqual(dekId, encryptionProperties.DataEncryptionKeyId);

            int expectedVersion =
                (encryptionOptions.CompressionOptions.Algorithm != CompressionOptions.CompressionAlgorithm.None)
                ? 4 : 3;
            Assert.AreEqual(expectedVersion, encryptionProperties.EncryptionFormatVersion);

            Assert.IsNull(encryptionProperties.EncryptedData);
            Assert.IsNotNull(encryptionProperties.EncryptedPaths);

            if (testDoc.SensitiveStr == null)
            {
                Assert.IsNull(encryptedDoc.Property(nameof(TestDoc.SensitiveStr)).Value.Value<string>()); // since null value is not encrypted
                Assert.AreEqual(TestDoc.PathsToEncrypt.Count - 1, encryptionProperties.EncryptedPaths.Count());
            }
            else
            {
                Assert.IsNotNull(encryptedDoc.Property(nameof(TestDoc.SensitiveStr)).Value.Value<string>());
                Assert.AreNotEqual(testDoc.SensitiveStr, encryptedDoc.Property(nameof(TestDoc.SensitiveStr)).Value.Value<string>()); // not equal since value is encrypted
                Assert.AreEqual(TestDoc.PathsToEncrypt.Count, encryptionProperties.EncryptedPaths.Count());
            }

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
            Assert.AreEqual(dekId, decryptionInfo.DataEncryptionKeyId);
            if (expectedDoc.SensitiveStr == null)
            {
                Assert.AreEqual(pathCount - 1, decryptionInfo.PathsDecrypted.Count);
                Assert.IsTrue(TestDoc.PathsToEncrypt.Exists(path => !decryptionInfo.PathsDecrypted.Contains(path)));
            }
            else
            {
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
        }

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        private static void VerifyDecryptionSucceeded(
            JsonNode decryptedDoc,
            TestDoc expectedDoc,
            int pathCount,
            DecryptionContext decryptionContext,
            bool invalidPathsConfigured = false)
        {
            AssertNullableValueKind(expectedDoc.SensitiveStr, decryptedDoc, nameof(TestDoc.SensitiveStr));
            Assert.AreEqual(expectedDoc.SensitiveInt, decryptedDoc[nameof(TestDoc.SensitiveInt)].GetValue<long>());
            Assert.IsNull(decryptedDoc[Constants.EncryptedInfo]);

            Assert.IsNotNull(decryptionContext);
            Assert.IsNotNull(decryptionContext.DecryptionInfoList);
            DecryptionInfo decryptionInfo = decryptionContext.DecryptionInfoList[0];
            Assert.AreEqual(dekId, decryptionInfo.DataEncryptionKeyId);
            if (expectedDoc.SensitiveStr == null)
            {
                Assert.AreEqual(pathCount - 1, decryptionInfo.PathsDecrypted.Count);
                Assert.IsTrue(TestDoc.PathsToEncrypt.Exists(path => !decryptionInfo.PathsDecrypted.Contains(path)));
            }
            else
            {
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
        }

        private static void AssertNullableValueKind<T>(T expectedValue, JsonNode node, string propertyName) where T : class
        {
            if (expectedValue == null)
            {
                Assert.IsTrue(node.AsObject().ContainsKey(propertyName));
                Assert.AreEqual(null, node[propertyName]);
            }
            else
            {
                Assert.AreEqual(expectedValue, node[propertyName].GetValue<T>());
            }
        }
#endif

        private static EncryptionOptions CreateEncryptionOptions(JsonProcessor processor, CompressionOptions.CompressionAlgorithm compressionAlgorithm, CompressionLevel compressionLevel)
        {
            return new EncryptionOptions()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt,
                JsonProcessor = processor,
                CompressionOptions = new CompressionOptions()
                {
                    Algorithm = compressionAlgorithm,
                    CompressionLevel = compressionLevel
                }
            };
        }

        public static IEnumerable<object[]> EncryptionOptionsCombinations => new[] {
            new object[] { CreateEncryptionOptions(JsonProcessor.Newtonsoft, CompressionOptions.CompressionAlgorithm.None, CompressionLevel.NoCompression) },
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
            new object[] { CreateEncryptionOptions(JsonProcessor.Stream, CompressionOptions.CompressionAlgorithm.None, CompressionLevel.NoCompression) },
            new object[] { CreateEncryptionOptions(JsonProcessor.Newtonsoft, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest) },
            new object[] { CreateEncryptionOptions(JsonProcessor.Stream, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.Fastest) },
            new object[] { CreateEncryptionOptions(JsonProcessor.Newtonsoft, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.NoCompression) },
            new object[] { CreateEncryptionOptions(JsonProcessor.Stream, CompressionOptions.CompressionAlgorithm.Brotli, CompressionLevel.NoCompression) },
#endif
        };

        public static IEnumerable<object[]> EncryptionOptionsStreamTestCombinations
        {
            get
            {
                foreach (object[] encryptionOptions in EncryptionOptionsCombinations)
                {
                    yield return new object[] { encryptionOptions[0], JsonProcessor.Newtonsoft };
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
                    yield return new object[] { encryptionOptions[0], JsonProcessor.Stream };
#endif
                }
            }
        }
    }
}