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
            mockDataEncryptionKey = new Mock<DataEncryptionKey>();
            mockDataEncryptionKey
                .Setup(m => m.EncryptData(It.IsAny<byte[]>()))
                .Returns((byte[] plainText) => TestCommon.EncryptData(plainText));
            mockDataEncryptionKey
                .Setup(m => m.DecryptData(It.IsAny<byte[]>()))
                .Returns((byte[] cipherText) => TestCommon.DecryptData(cipherText));

            mockDataEncryptionKeyProvider = new Mock<DataEncryptionKeyProvider>();
            mockDataEncryptionKeyProvider
                .Setup(m => m.FetchDataEncryptionKeyWithoutRawKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string dekId, string algo, CancellationToken cancellationToken) =>
                    dekId == CosmosEncryptorTests.dekId ? mockDataEncryptionKey.Object : null);

            cosmosEncryptor = new CosmosEncryptor(mockDataEncryptionKeyProvider.Object);
        }

        [TestMethod]
        public async Task EncryptWithUnknownDek()
        {
            try
            {
                await cosmosEncryptor.EncryptAsync(
                    TestCommon.GenerateRandomByteArray(),
                    "unknownDek",
                    CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);

                Assert.Fail("Encryption shoudn't have succeeded with uninitialized DEK.");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("Null DataEncryptionKey returned from FetchDataEncryptionKeyWithoutRawKeyAsync.", ex.Message);
            }
        }

        [TestMethod]
        public async Task ValidateEncryptDecrypt()
        {
            byte[] plainText = TestCommon.GenerateRandomByteArray();
            byte[] cipherText = await cosmosEncryptor.EncryptAsync(
                plainText,
                dekId,
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);

            mockDataEncryptionKey.Verify(
                m => m.EncryptData(plainText),
                Times.Once);
            
            byte[] decryptedText = await cosmosEncryptor.DecryptAsync(
                cipherText,
                dekId,
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);

            mockDataEncryptionKey.Verify(
                m => m.DecryptData(cipherText),
                Times.Once);

            mockDataEncryptionKeyProvider.Verify(
                m => m.FetchDataEncryptionKeyWithoutRawKeyAsync(
                    dekId,
                    CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    It.IsAny<CancellationToken>()), Times.Exactly(2));

            Assert.IsTrue(plainText.SequenceEqual(decryptedText));
        }
    }
}
