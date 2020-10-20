//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
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
    using TestDoc = TestCommon.TestDoc;

    [TestClass]
    public class EncryptionProcessorTests
    {
        private static Mock<Encryptor> mockEncryptor;
        private static EncryptionOptions encryptionOptions;
        private const string dekId = "dekId";

        [ClassInitialize]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The ClassInitialize method takes a single parameter of type TestContext.")]
        public static void ClassInitialize(TestContext testContext)
        {
            EncryptionProcessorTests.encryptionOptions = new EncryptionOptions()
            {
                DataEncryptionKeyId = EncryptionProcessorTests.dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt
            };

            EncryptionProcessorTests.mockEncryptor = new Mock<Encryptor>();
            EncryptionProcessorTests.mockEncryptor.Setup(m => m.EncryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] plainText, string dekId, string algo, CancellationToken t) =>
                    dekId == EncryptionProcessorTests.dekId ? TestCommon.EncryptData(plainText) : throw new InvalidOperationException("DEK not found."));
            EncryptionProcessorTests.mockEncryptor.Setup(m => m.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] cipherText, string dekId, string algo, CancellationToken t) => 
                    dekId == EncryptionProcessorTests.dekId ? TestCommon.DecryptData(cipherText) : throw new InvalidOperationException("Null DEK was returned."));
        }

        [TestMethod]
        public async Task InvalidPathToEncrypt()
        {
            TestDoc testDoc = TestDoc.Create();
            EncryptionOptions encryptionOptionsWithInvalidPathToEncrypt = new EncryptionOptions()
            {
                DataEncryptionKeyId = EncryptionProcessorTests.dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = new List<string>() { "/SensitiveStr", "/Invalid" }
            };

            try
            {
                await EncryptionProcessor.EncryptAsync(
                    testDoc.ToStream(),
                    EncryptionProcessorTests.mockEncryptor.Object,
                    encryptionOptionsWithInvalidPathToEncrypt,
                    new CosmosDiagnosticsContext(),
                    CancellationToken.None);

                Assert.Fail("Invalid path to encrypt didn't result in exception.");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("PathsToEncrypt includes a path: '/Invalid' which was not found.", ex.Message);
            }
        }

        [TestMethod]
        public async Task EncryptDecryptPropertyWithNullValue()
        {
            TestDoc testDoc = TestDoc.Create();
            testDoc.SensitiveStr = null;

            JObject encryptedDoc = await EncryptionProcessorTests.VerifyEncryptionSucceeded(testDoc);

            (JObject decryptedDoc, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedDoc,
                EncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            EncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                decryptionContext);
        }

        [TestMethod]
        public async Task ValidateEncryptDecryptDocument()
        {
            TestDoc testDoc = TestDoc.Create();

            JObject encryptedDoc = await EncryptionProcessorTests.VerifyEncryptionSucceeded(testDoc);

            (JObject decryptedDoc, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedDoc,
                EncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            EncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                decryptionContext);
        }

        [TestMethod]
        public async Task ValidateDecryptStream()
        {
            TestDoc testDoc = TestDoc.Create();

            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                EncryptionProcessorTests.mockEncryptor.Object,
                EncryptionProcessorTests.encryptionOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                encryptedStream,
                EncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            JObject decryptedDoc = EncryptionProcessor.BaseSerializer.FromStream<JObject>(decryptedStream);
            EncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc,
                decryptionContext);
        }

        [TestMethod]
        public async Task DecryptStreamWithoutEncryptedProperty()
        {
            TestDoc testDoc = TestDoc.Create();
            Stream docStream = testDoc.ToStream();

            (Stream decryptedStream, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                docStream,
                EncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Assert.IsTrue(decryptedStream.CanSeek);
            Assert.AreEqual(0, decryptedStream.Position);
            Assert.AreEqual(docStream.Length, decryptedStream.Length);
            Assert.IsNull(decryptionContext);
        }

        private static async Task<JObject> VerifyEncryptionSucceeded(TestDoc testDoc)
        {
            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                EncryptionProcessorTests.mockEncryptor.Object,
                EncryptionProcessorTests.encryptionOptions,
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
            Assert.AreEqual(EncryptionProcessorTests.dekId, encryptionProperties.DataEncryptionKeyId);
            Assert.AreEqual(2, encryptionProperties.EncryptionFormatVersion);
            Assert.IsNotNull(encryptionProperties.EncryptedData);

            return encryptedDoc;
        }

        private static void VerifyDecryptionSucceeded(
            JObject decryptedDoc,
            TestDoc expectedDoc,
            DecryptionContext decryptionContext)
        {
            Assert.AreEqual(expectedDoc.SensitiveStr, decryptedDoc.Property(nameof(TestDoc.SensitiveStr)).Value.Value<string>());
            Assert.AreEqual(expectedDoc.SensitiveInt, decryptedDoc.Property(nameof(TestDoc.SensitiveInt)).Value.Value<int>());
            Assert.IsNull(decryptedDoc.Property(Constants.EncryptedInfo));
            
            Assert.IsNotNull(decryptionContext);
            Assert.IsNotNull(decryptionContext.DecryptionInfoList);
            DecryptionInfo decryptionInfo = decryptionContext.DecryptionInfoList.First();
            Assert.AreEqual(EncryptionProcessorTests.dekId, decryptionInfo.DataEncryptionKeyId);
            Assert.AreEqual(TestDoc.PathsToEncrypt.Count, decryptionInfo.PathsDecrypted.Count);
            Assert.IsFalse(TestDoc.PathsToEncrypt.Exists(path => !decryptionInfo.PathsDecrypted.Contains(path)));
        }
    }
}
