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
    
    [TestClass]
    public class AapCosmosEncryptorTests
    {
        private static Mock<DataEncryptionKey> mockDataEncryptionKey;
        private static Mock<DataEncryptionKeyProvider> mockDataEncryptionKeyProvider;
        private static AapCosmosEncryptor aapcosmosEncryptor;
        private const string dekId = "dekId";

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            AapCosmosEncryptorTests.mockDataEncryptionKey = new Mock<DataEncryptionKey>();
            AapCosmosEncryptorTests.mockDataEncryptionKey
                .Setup(m => m.EncryptData(It.IsAny<byte[]>()))
                .Returns((byte[] plainText) => TestCommon.EncryptData(plainText));
            AapCosmosEncryptorTests.mockDataEncryptionKey
                .Setup(m => m.DecryptData(It.IsAny<byte[]>()))
                .Returns((byte[] cipherText) => TestCommon.DecryptData(cipherText));

            AapCosmosEncryptorTests.mockDataEncryptionKeyProvider = new Mock<DataEncryptionKeyProvider>();
            AapCosmosEncryptorTests.mockDataEncryptionKeyProvider
                .Setup(m => m.FetchDataEncryptionKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string dekId, string algo, CancellationToken cancellationToken) =>
                    dekId == AapCosmosEncryptorTests.dekId ? AapCosmosEncryptorTests.mockDataEncryptionKey.Object : null);

            AapCosmosEncryptorTests.aapcosmosEncryptor = new AapCosmosEncryptor(AapCosmosEncryptorTests.mockDataEncryptionKeyProvider.Object);
        }

        [TestMethod]
        public async Task EncryptWithUnknownDek()
        {
            try
            {
                await AapCosmosEncryptorTests.aapcosmosEncryptor.EncryptAsync(
                    TestCommon.GenerateRandomByteArray(),
                    "unknownDek",
                    CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized);

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
            byte[] cipherText = await AapCosmosEncryptorTests.aapcosmosEncryptor.EncryptAsync(
                plainText,
                AapCosmosEncryptorTests.dekId,
                CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized);

            AapCosmosEncryptorTests.mockDataEncryptionKey.Verify(
                m => m.EncryptData(plainText),
                Times.Once);
            
            byte[] decryptedText = await AapCosmosEncryptorTests.aapcosmosEncryptor.DecryptAsync(
                cipherText,
                AapCosmosEncryptorTests.dekId,
                CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized);

            AapCosmosEncryptorTests.mockDataEncryptionKey.Verify(
                m => m.DecryptData(cipherText),
                Times.Once);

            AapCosmosEncryptorTests.mockDataEncryptionKeyProvider.Verify(
                m => m.FetchDataEncryptionKeyAsync(
                    AapCosmosEncryptorTests.dekId,
                    CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized,
                    It.IsAny<CancellationToken>()), Times.Exactly(2));

            Assert.IsTrue(plainText.SequenceEqual(decryptedText));
        }
    }
}
