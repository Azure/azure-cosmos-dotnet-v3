//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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

#if NET8_0_OR_GREATER
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
        [DynamicData(nameof(JsonProcessors))]
        internal async Task InvalidPathToEncrypt(JsonProcessor jsonProcessor)
        {
            TestDoc testDoc = TestDoc.Create();
            EncryptionOptions encryptionOptionsWithInvalidPathToEncrypt = new()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = new List<string>() { "/SensitiveStr", "/Invalid" },
            };

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                   testDoc.ToStream(),
                   mockEncryptor.Object,
                   encryptionOptionsWithInvalidPathToEncrypt,
                   jsonProcessor,
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
        [DynamicData(nameof(JsonProcessors))]
        internal async Task DuplicatePathToEncrypt(JsonProcessor jsonProcessor)
        {
            TestDoc testDoc = TestDoc.Create();
            EncryptionOptions encryptionOptionsWithDuplicatePathToEncrypt = new()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = new List<string>() { "/SensitiveStr", "/SensitiveStr" },
            };

            try
            {
                await EncryptionProcessor.EncryptAsync(
                    testDoc.ToStream(),
                    mockEncryptor.Object,
                    encryptionOptionsWithDuplicatePathToEncrypt,
                    jsonProcessor,
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
        [DynamicData(nameof(JsonProcessors))]
        internal async Task EncryptDecryptPropertyWithNullValue_VerifyByNewtonsoft(JsonProcessor jsonProcessor)
        {
            EncryptionOptions encryptionOptions = this.CreateEncryptionOptions();
            TestDoc testDoc = TestDoc.Create();
            testDoc.SensitiveStr = null;

            JObject encryptedDoc = await VerifyEncryptionSucceededNewtonsoft(testDoc, encryptionOptions, jsonProcessor);

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
        [DynamicData(nameof(JsonProcessors))]
        internal async Task ValidateEncryptDecryptDocument_VerifyByNewtonsoft(JsonProcessor jsonProcessor)
        {
            EncryptionOptions encryptionOptions = this.CreateEncryptionOptions();
            TestDoc testDoc = TestDoc.Create();

            JObject encryptedDoc = await VerifyEncryptionSucceededNewtonsoft(testDoc, encryptionOptions, jsonProcessor);

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
        [DynamicData(nameof(JsonProcessors))]
        internal async Task ValidateDecryptByNewtonsoftStream_VerifyByNewtonsoft(JsonProcessor jsonProcessor)
        {
            EncryptionOptions encryptionOptions = this.CreateEncryptionOptions();
            TestDoc testDoc = TestDoc.Create();

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                mockEncryptor.Object,
                encryptionOptions,
                jsonProcessor,
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
        [DynamicData(nameof(JsonProcessorCombinations))]
        internal async Task ValidateDecryptBySystemTextStream_VerifyByNewtonsoft(JsonProcessor encryptionJsonProcessor, JsonProcessor decryptionJsonProcessor)
        {
            EncryptionOptions encryptionOptions = this.CreateEncryptionOptions();
            TestDoc testDoc = TestDoc.Create();

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                mockEncryptor.Object,
                encryptionOptions,
                encryptionJsonProcessor,
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

#if NET8_0_OR_GREATER
        [TestMethod]
        [DynamicData(nameof(JsonProcessorCombinations))]
        internal async Task ValidateDecryptBySystemTextStream_VerifyBySystemText(JsonProcessor encryptionJsonProcessor, JsonProcessor decryptionJsonProcessor)
        {
            EncryptionOptions encryptionOptions = this.CreateEncryptionOptions();
            TestDoc testDoc = TestDoc.Create();

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                mockEncryptor.Object,
                encryptionOptions,
                encryptionJsonProcessor,
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
        [DynamicData(nameof(JsonProcessors))]
        internal async Task DecryptStreamWithoutEncryptedProperty(JsonProcessor processor)
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

        private static async Task<JObject> VerifyEncryptionSucceededNewtonsoft(TestDoc testDoc, EncryptionOptions encryptionOptions, JsonProcessor jsonProcessor)
        {
            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                 testDoc.ToStream(),
                 mockEncryptor.Object,
                 encryptionOptions,
                 jsonProcessor,
                 new CosmosDiagnosticsContext(),
                 CancellationToken.None);

            JObject encryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStream);

            Assert.AreEqual(testDoc.Id, encryptedDoc.Property("id").Value.Value<string>());
            Assert.AreEqual(testDoc.PK, encryptedDoc.Property(nameof(TestDoc.PK)).Value.Value<string>());
            Assert.AreEqual(testDoc.NonSensitive, encryptedDoc.Property(nameof(TestDoc.NonSensitive)).Value.Value<string>());
            Assert.IsNotNull(encryptedDoc.Property(nameof(TestDoc.SensitiveInt)).Value.Value<string>());
            Assert.AreNotEqual(testDoc.SensitiveInt, encryptedDoc.Property(nameof(TestDoc.SensitiveInt)).Value.Value<string>()); // not equal since value is encrypted

            JProperty eiJProp = encryptedDoc.Property(Constants.EncryptedInfo);
            Assert.IsNotNull(eiJProp);
            Assert.IsNotNull(eiJProp.Value);
            Assert.AreEqual(JTokenType.Object, eiJProp.Value.Type);
            EncryptionProperties encryptionProperties = ((JObject)eiJProp.Value).ToObject<EncryptionProperties>();

            Assert.IsNotNull(encryptionProperties);
            Assert.AreEqual(dekId, encryptionProperties.DataEncryptionKeyId);

            Assert.AreEqual(EncryptionFormatVersion.Mde, encryptionProperties.EncryptionFormatVersion);

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

#if NET8_0_OR_GREATER
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

        private EncryptionOptions CreateEncryptionOptions()
        {
            return new EncryptionOptions()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };
        }

        public static IEnumerable<object[]> JsonProcessors
        {
            get
            {
                    yield return new object[] { JsonProcessor.Newtonsoft };
#if NET8_0_OR_GREATER
                    yield return new object[] { JsonProcessor.Stream };
#endif
            }
        }

        public static IEnumerable<object[]> JsonProcessorCombinations
        {
                       get
            {
                foreach (JsonProcessor encProcessor in JsonProcessors.Select(p => (JsonProcessor)p[0]))
                {
                    foreach (JsonProcessor decProcessor in JsonProcessors.Select(p => (JsonProcessor)p[0]))
                    {
                        yield return new object[] { encProcessor, decProcessor };
                    }
                }
            }
        }
    }
}