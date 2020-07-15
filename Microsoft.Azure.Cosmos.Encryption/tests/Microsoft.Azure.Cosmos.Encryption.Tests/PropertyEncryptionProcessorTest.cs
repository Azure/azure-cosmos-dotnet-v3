//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class PropertyEncryptionProcessorTests
    {
        private static Mock<Encryptor> mockEncryptor;
        private const string pdekId = "pdekId";
        private static List<EncryptionOptions> propertyEncryptionOptions;
        private static Dictionary<List<string>, string> PathsToEncrypt = new Dictionary<List<string>, string>();

        [ClassInitialize]
        public static void ClassInitilize(TestContext testContext)
        {
            propertyEncryptionOptions = new List<EncryptionOptions> {
            {
                new EncryptionOptions()
                {
                    DataEncryptionKeyId = PropertyEncryptionProcessorTests.pdekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256,
                    PathsToEncrypt = TestDoc.PropertyPathsToEncrypt
                }
            } };
            PathsToEncrypt.Add(TestDoc.PropertyPathsToEncrypt, pdekId);
            PropertyEncryptionProcessorTests.mockEncryptor = new Mock<Encryptor>();
            PropertyEncryptionProcessorTests.mockEncryptor.Setup(m => m.EncryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] plainText, string dekId, string algo, CancellationToken t) =>
                    dekId == PropertyEncryptionProcessorTests.pdekId ? TestCommon.EncryptData(plainText) : throw new InvalidOperationException("DEK not found."));
            PropertyEncryptionProcessorTests.mockEncryptor.Setup(m => m.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] cipherText, string dekId, string algo, CancellationToken t) =>
                    dekId == PropertyEncryptionProcessorTests.pdekId ? TestCommon.DecryptData(cipherText) : throw new InvalidOperationException("Null DEK was returned."));
        }

        [TestMethod]
        public async Task InvalidPathToEncrypt()
        {
            TestDoc testDoc = TestDoc.Create();
            List<EncryptionOptions> propertyEncryptionOptionsWithInvalidPath = new List<EncryptionOptions>();
            propertyEncryptionOptionsWithInvalidPath.Add(
                new EncryptionOptions()
                {
                    DataEncryptionKeyId = PropertyEncryptionProcessorTests.pdekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256,
                    PathsToEncrypt = new List<string>() { "/Name", "/Invalid" }
                });

            try
            {
                await PropertyEncryptionProcessor.EncryptAsync(
                    testDoc.ToStream(),
                    PropertyEncryptionProcessorTests.mockEncryptor.Object,
                    propertyEncryptionOptionsWithInvalidPath,
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

            JObject encryptedDoc = await PropertyEncryptionProcessorTests.VerifyEncryptionSucceeded(testDoc);

            JObject decryptedDoc = await PropertyEncryptionProcessor.DecryptAsync(
                encryptedDoc,
                PropertyEncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                PathsToEncrypt,
                CancellationToken.None);

            PropertyEncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc);
        }

        [TestMethod]
        public async Task ValidateEncryptDecryptDocument()
        {
            TestDoc testDoc = TestDoc.Create();

            JObject encryptedDoc = await PropertyEncryptionProcessorTests.VerifyEncryptionSucceeded(testDoc);

            JObject decryptedDoc = await PropertyEncryptionProcessor.DecryptAsync(
                encryptedDoc,
                PropertyEncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                PathsToEncrypt,
                CancellationToken.None);

            PropertyEncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc);
        }

        [TestMethod]
        public async Task ValidateDecryptStream()
        {
            TestDoc testDoc = TestDoc.Create();

            Stream encryptedStream = await PropertyEncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                PropertyEncryptionProcessorTests.mockEncryptor.Object,
                PropertyEncryptionProcessorTests.propertyEncryptionOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            Stream decryptedStream = await PropertyEncryptionProcessor.DecryptAsync(
                encryptedStream,
                PropertyEncryptionProcessorTests.mockEncryptor.Object,
                new CosmosDiagnosticsContext(),
                PropertyEncryptionProcessorTests.PathsToEncrypt,
                CancellationToken.None);

            JObject decryptedDoc = PropertyEncryptionProcessor.BaseSerializer.FromStream<JObject>(decryptedStream);
            PropertyEncryptionProcessorTests.VerifyDecryptionSucceeded(
                decryptedDoc,
                testDoc);
        }

        private static async Task<JObject> VerifyEncryptionSucceeded(TestDoc testDoc)
        {
            Stream encryptedStream = await PropertyEncryptionProcessor.EncryptAsync(
                testDoc.ToStream(),
                PropertyEncryptionProcessorTests.mockEncryptor.Object,
                PropertyEncryptionProcessorTests.propertyEncryptionOptions,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);

            JObject encryptedDoc = PropertyEncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStream);

            Assert.AreEqual(testDoc.Id, encryptedDoc.Property("id").Value.Value<string>());
            Assert.AreEqual(testDoc.PK, encryptedDoc.Property(nameof(TestDoc.PK)).Value.Value<string>());
            Assert.AreEqual(testDoc.SSN, encryptedDoc.Property(nameof(TestDoc.SSN)).Value.Value<int>());
            Assert.AreNotEqual(testDoc.Name, encryptedDoc.Property(nameof(TestDoc.Name)).Value.Value<string>());

            JProperty encrProp = encryptedDoc.Property(nameof(TestDoc.Name));//.Value.Value<string>();
            Assert.IsNotNull(encrProp);
            Assert.IsNotNull(encrProp.Value.Value<string>());

            return encryptedDoc;
        }

        private static void VerifyDecryptionSucceeded(
            JObject decryptedDoc,
            TestDoc expectedDoc)
        {
            Assert.AreEqual(expectedDoc.Name, decryptedDoc.Property(nameof(TestDoc.Name)).Value.Value<string>());
            Assert.IsNull(decryptedDoc.Property(Constants.EncryptedInfo));
        }
        internal class TestDoc
        {
            public static List<string> PropertyPathsToEncrypt { get; } = new List<string>() { "/Name" };

            [JsonProperty("id")]
            public string Id { get; set; }

            public string PK { get; set; }

            public string Name { get; set; }

            public int SSN { get; set; }

            public string Sensitive { get; set; }

            public TestDoc()
            {
            }

            public TestDoc(TestDoc other)
            {
                this.Id = other.Id;
                this.PK = other.PK;
                this.Name = other.Name;
                this.SSN = other.SSN;
                this.Sensitive = other.Sensitive;
            }

            public override bool Equals(object obj)
            {
                return obj is TestDoc doc
                       && this.Id == doc.Id
                       && this.PK == doc.PK
                       && this.Name == doc.Name
                       && this.SSN == doc.SSN
                       && this.Sensitive == this.Sensitive;
            }

            public override int GetHashCode()
            {
                int hashCode = 1652434776;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Id);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.PK);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Name);
                hashCode = (hashCode * -1521134295) + EqualityComparer<int>.Default.GetHashCode(this.SSN);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Sensitive);
                return hashCode;
            }

            public static TestDoc Create(string partitionKey = null)
            {
                return new TestDoc()
                {
                    Id = Guid.NewGuid().ToString(),
                    PK = partitionKey ?? Guid.NewGuid().ToString(),
                    Name = "myName",
                    SSN = new Random().Next(),
                    Sensitive = Guid.NewGuid().ToString()
                };
            }

            public Stream ToStream()
            {
                return TestCommon.ToStream(this);
            }
        }
    }
}