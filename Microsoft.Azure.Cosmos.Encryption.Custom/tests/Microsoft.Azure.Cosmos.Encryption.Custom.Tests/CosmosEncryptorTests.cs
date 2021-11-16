//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Microsoft.Azure.Cosmos.Encryption.Custom;

    [TestClass]
    public class CosmosEncryptorTests
    {
        private static Mock<DataEncryptionKey> mockDataEncryptionKey;
        private static Mock<DataEncryptionKeyProvider> mockDataEncryptionKeyProvider;
        private static CosmosEncryptor cosmosEncryptor;
        private const string dekId = "dekId";

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _ = testContext;
            CosmosEncryptorTests.mockDataEncryptionKey = new Mock<DataEncryptionKey>();
            CosmosEncryptorTests.mockDataEncryptionKey
                .Setup(m => m.EncryptData(It.IsAny<byte[]>()))
                .Returns((byte[] plainText) => TestCommon.EncryptData(plainText));
            CosmosEncryptorTests.mockDataEncryptionKey
                .Setup(m => m.DecryptData(It.IsAny<byte[]>()))
                .Returns((byte[] cipherText) => TestCommon.DecryptData(cipherText));

            CosmosEncryptorTests.mockDataEncryptionKeyProvider = new Mock<DataEncryptionKeyProvider>();
            CosmosEncryptorTests.mockDataEncryptionKeyProvider
                .Setup(m => m.FetchDataEncryptionKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string dekId, string algo, CancellationToken cancellationToken) =>
                    dekId == CosmosEncryptorTests.dekId ? CosmosEncryptorTests.mockDataEncryptionKey.Object : null);

            CosmosEncryptorTests.cosmosEncryptor = new CosmosEncryptor(CosmosEncryptorTests.mockDataEncryptionKeyProvider.Object);
        }

        [TestMethod]
        public async Task EncryptWithUnknownDek()
        {
            try
            {
                await CosmosEncryptorTests.cosmosEncryptor.EncryptAsync(
                    TestCommon.GenerateRandomByteArray(),
                    "unknownDek",
                    CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);

                Assert.Fail("Encryption shoudn't have succeeded with uninitialized DEK.");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("Null DataEncryptionKey returned from FetchDataEncryptionKeyAsync.", ex.Message);
            }
        }

        [TestMethod]
        public async Task ValidateEncryptDecrypt()
        {
            byte[] plainText = TestCommon.GenerateRandomByteArray();
            byte[] cipherText = await CosmosEncryptorTests.cosmosEncryptor.EncryptAsync(
                plainText,
                CosmosEncryptorTests.dekId,
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);

            CosmosEncryptorTests.mockDataEncryptionKey.Verify(
                m => m.EncryptData(plainText),
                Times.Once);
            
            byte[] decryptedText = await CosmosEncryptorTests.cosmosEncryptor.DecryptAsync(
                cipherText,
                CosmosEncryptorTests.dekId,
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);

            CosmosEncryptorTests.mockDataEncryptionKey.Verify(
                m => m.DecryptData(cipherText),
                Times.Once);

            CosmosEncryptorTests.mockDataEncryptionKeyProvider.Verify(
                m => m.FetchDataEncryptionKeyAsync(
                    CosmosEncryptorTests.dekId,
                    CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    It.IsAny<CancellationToken>()), Times.Exactly(2));

            Assert.IsTrue(plainText.SequenceEqual(decryptedText));
        }
    }
}
