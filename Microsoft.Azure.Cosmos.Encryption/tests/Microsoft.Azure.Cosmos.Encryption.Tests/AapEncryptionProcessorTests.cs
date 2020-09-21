//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using TestDoc = TestCommon.TestDoc;

    [TestClass]
    public class AapEncryptionProcessorTests
    {
        private static Mock<Encryptor> mockEncryptor;
        private static EncryptionOptions encryptionOptions;
        private const string dekId = "dekId";

        [ClassInitialize]
        public static void ClassInitilize(TestContext testContext)
        {
            AapEncryptionProcessorTests.encryptionOptions = new EncryptionOptions()
            {
                DataEncryptionKeyId = AapEncryptionProcessorTests.dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = TestDoc.PathsToEncrypt
            };

            AapEncryptionProcessorTests.mockEncryptor = new Mock<Encryptor>();
            AapEncryptionProcessorTests.mockEncryptor.Setup(m => m.EncryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] plainText, string dekId, string algo, CancellationToken t) =>
                    dekId == AapEncryptionProcessorTests.dekId ? TestCommon.EncryptData(plainText) : throw new InvalidOperationException("DEK not found."));
            AapEncryptionProcessorTests.mockEncryptor.Setup(m => m.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] cipherText, string dekId, string algo, CancellationToken t) => 
                    dekId == AapEncryptionProcessorTests.dekId ? TestCommon.DecryptData(cipherText) : throw new InvalidOperationException("Null DEK was returned."));
        }

        [TestMethod]
        public async Task InvalidPathToEncrypt()
        {
            TestDoc testDoc = TestDoc.Create();
            EncryptionOptions encryptionOptionsWithInvalidPathToEncrypt = new EncryptionOptions()
            {
                DataEncryptionKeyId = AapEncryptionProcessorTests.dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = new List<string>() { "/SensitiveStr", "/Invalid" }
            };

            try
            {
                await AapEncryptionProcessor.EncryptAsync(
                    testDoc.ToStream(),
                    AapEncryptionProcessorTests.mockEncryptor.Object,
                    encryptionOptionsWithInvalidPathToEncrypt,
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
            //Utf8JsonWriter outputWriter = null;
            JObject encryptedDoc = await AapEncryptionProcessorTests.VerifyEncryptionSucceeded(testDoc);
            

            EncryptionProperties encryptionProperties = new EncryptionProperties(
                   encryptionFormatVersion: 3,
                   AapEncryptionProcessorTests.encryptionOptions.EncryptionAlgorithm,
                   AapEncryptionProcessorTests.encryptionOptions.DataEncryptionKeyId,
                   encryptedData: null,
                   AapEncryptionProcessorTests.encryptionOptions.PathsToEncrypt);

            MemoryStream output = new MemoryStream();
            using (Utf8JsonWriter writer = new Utf8JsonWriter(output))
            using (JsonDocument doc = JsonDocument.Parse(TestCommon.ToStream(encryptedDoc)))
            {

                await AapEncryptionProcessor.DecryptAndWriteAsync(
                doc.RootElement,
                AapEncryptionProcessorTests.mockEncryptor.Object,
                writer,
                encryptionProperties,
                CancellationToken.None);
            }

            output.Seek(0, SeekOrigin.Begin);
            JObject itemJObj = TestCommon.FromStream<JObject>(output);
            AapEncryptionProcessorTests.VerifyDecryptionSucceeded(
                itemJObj,
                testDoc);
        }

        [TestMethod]
        public async Task ValidateEncryptDecryptDocument()
        {
            TestDoc testDoc = TestDoc.Create();

            JObject encryptedDoc = await AapEncryptionProcessorTests.VerifyEncryptionSucceeded(testDoc);

            EncryptionProperties encryptionProperties = new EncryptionProperties(
                   encryptionFormatVersion: 3,
                   AapEncryptionProcessorTests.encryptionOptions.EncryptionAlgorithm,
                   AapEncryptionProcessorTests.encryptionOptions.DataEncryptionKeyId,
                   encryptedData: null,
                   AapEncryptionProcessorTests.encryptionOptions.PathsToEncrypt);

            MemoryStream output = new MemoryStream();
            using (Utf8JsonWriter writer = new Utf8JsonWriter(output))
            using (JsonDocument doc = JsonDocument.Parse(TestCommon.ToStream(encryptedDoc)))
            {
                await AapEncryptionProcessor.DecryptAndWriteAsync(
                doc.RootElement,
                AapEncryptionProcessorTests.mockEncryptor.Object,
                writer,
                encryptionProperties,
                CancellationToken.None);
            }

            output.Seek(0, SeekOrigin.Begin);
            JObject decryptedDoc = TestCommon.FromStream<JObject>(output);
            AapEncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc);
        }

        [TestMethod]
        public async Task ValidateDecryptStream()
        {
            TestDoc testDoc = TestDoc.Create();

            Stream encryptedStream = await AapEncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                AapEncryptionProcessorTests.mockEncryptor.Object,
                AapEncryptionProcessorTests.encryptionOptions,
                CancellationToken.None);

            EncryptionProperties encryptionProperties = new EncryptionProperties(
                   encryptionFormatVersion: 3,
                   AapEncryptionProcessorTests.encryptionOptions.EncryptionAlgorithm,
                   AapEncryptionProcessorTests.encryptionOptions.DataEncryptionKeyId,
                   encryptedData: null,
                   AapEncryptionProcessorTests.encryptionOptions.PathsToEncrypt);

            MemoryStream output = new MemoryStream();
            using (Utf8JsonWriter writer = new Utf8JsonWriter(output))
            using (JsonDocument doc = JsonDocument.Parse(encryptedStream))
            {
                await AapEncryptionProcessor.DecryptAndWriteAsync(
                doc.RootElement,
                AapEncryptionProcessorTests.mockEncryptor.Object,
                writer,
                encryptionProperties,
                CancellationToken.None);
            }

            output.Seek(0, SeekOrigin.Begin);

            JObject decryptedDoc = AapEncryptionProcessor.BaseSerializer.FromStream<JObject>(output);
            AapEncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc);
        }

        [TestMethod]
        public async Task DecryptStreamWithoutEncryptedProperty()
        {
            TestDoc testDoc = TestDoc.Create();
            Stream docStream = testDoc.ToStream();

            MemoryStream output = new MemoryStream();
            using (Utf8JsonWriter writer = new Utf8JsonWriter(output))
            using (JsonDocument doc = JsonDocument.Parse(docStream))
            {
                await AapEncryptionProcessor.DecryptAndWriteAsync(
                doc.RootElement,
                AapEncryptionProcessorTests.mockEncryptor.Object,
                writer,
                null,
                CancellationToken.None);
            }

            output.Seek(0, SeekOrigin.Begin);            

            Assert.IsTrue(output.CanSeek);
            Assert.AreEqual(docStream.Length, output.Length);
        }

        private static async Task<JObject> VerifyEncryptionSucceeded(TestDoc testDoc)
        {
            Stream encryptedStream = await AapEncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                AapEncryptionProcessorTests.mockEncryptor.Object,
                AapEncryptionProcessorTests.encryptionOptions,
                CancellationToken.None);

            JObject encryptedDoc = AapEncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStream);
            
            Assert.AreEqual(testDoc.Id, encryptedDoc.Property("id").Value.Value<string>());
            Assert.AreEqual(testDoc.PK, encryptedDoc.Property(nameof(TestDoc.PK)).Value.Value<string>());
            Assert.AreEqual(testDoc.NonSensitive, encryptedDoc.Property(nameof(TestDoc.NonSensitive)).Value.Value<string>());

            if (testDoc.SensitiveStr != null)
            {
                Assert.AreNotEqual(testDoc.SensitiveStr, encryptedDoc.Property(nameof(TestDoc.SensitiveStr)).Value.Value<string>());
            }
            else
            {
                Assert.AreEqual(testDoc.SensitiveStr, encryptedDoc.Property(nameof(TestDoc.SensitiveStr)).Value.Value<string>());
            }

            Assert.AreNotEqual(testDoc.SensitiveInt, encryptedDoc.Property(nameof(TestDoc.SensitiveInt)).Value.Value<string>());

            JProperty eiJProp = encryptedDoc.Property(Constants.EncryptedInfo);
            Assert.IsNotNull(eiJProp);
            Assert.IsNotNull(eiJProp.Value);
            Assert.AreEqual(JTokenType.Object, eiJProp.Value.Type);
            EncryptionProperties encryptionProperties = ((JObject)eiJProp.Value).ToObject<EncryptionProperties>();

            Assert.IsNotNull(encryptionProperties);
            Assert.AreEqual(AapEncryptionProcessorTests.dekId, encryptionProperties.DataEncryptionKeyId);
            Assert.AreEqual(3, encryptionProperties.EncryptionFormatVersion);

            return encryptedDoc;
        }

        private static void VerifyDecryptionSucceeded(
            JObject decryptedDoc,
            TestDoc expectedDoc)
        {
            Assert.AreEqual(expectedDoc.SensitiveStr, decryptedDoc.Property(nameof(TestDoc.SensitiveStr)).Value.Value<string>());
            Assert.AreEqual(expectedDoc.SensitiveInt, decryptedDoc.Property(nameof(TestDoc.SensitiveInt)).Value.Value<int>());
            //We keep the EI for further use.
            //Assert.IsNull(decryptedDoc.Property(Constants.EncryptedInfo));
        }
    }
}
