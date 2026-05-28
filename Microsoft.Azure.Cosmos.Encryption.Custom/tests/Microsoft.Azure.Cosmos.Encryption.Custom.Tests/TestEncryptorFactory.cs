//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Moq;

    /// <summary>
    /// Shared helper for creating mock Encryptor (and DataEncryptionKey for MDE) instances used in tests.
    /// Reduces repetitive Moq setup code across test classes.
    /// </summary>
    internal static class TestEncryptorFactory
    {
        public static Mock<Encryptor> CreateMde(string dekId, out Mock<DataEncryptionKey> dekMock)
        {
            Mock<DataEncryptionKey> localDek = new Mock<DataEncryptionKey>();
            localDek.SetupGet(d => d.EncryptionAlgorithm).Returns(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);
            localDek.Setup(d => d.GetEncryptByteCount(It.IsAny<int>())).Returns<int>(i => i);
            localDek.Setup(d => d.GetDecryptByteCount(It.IsAny<int>())).Returns<int>(i => i);
            localDek.Setup(d => d.EncryptData(It.IsAny<byte[]>())).Returns<byte[]>(b => TestCommon.EncryptData(b));
            localDek.Setup(d => d.EncryptData(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<int>()))
                .Returns((byte[] input, int offset, int length, byte[] output, int outputOffset) => TestCommon.EncryptData(input, offset, length, output, outputOffset));
            localDek.Setup(d => d.DecryptData(It.IsAny<byte[]>())).Returns<byte[]>(b => TestCommon.DecryptData(b));
            localDek.Setup(d => d.DecryptData(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<int>()))
                .Returns((byte[] input, int offset, int length, byte[] output, int outputOffset) => TestCommon.DecryptData(input, offset, length, output, outputOffset));

            Mock<Encryptor> encryptor = new Mock<Encryptor>();
            encryptor.Setup(e => e.GetEncryptionKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, string algo, CancellationToken t) => id == dekId ? localDek.Object : throw new InvalidOperationException("DEK not found"));
            encryptor.Setup(e => e.EncryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] plain, string id, string algo, CancellationToken t) => id == dekId ? TestCommon.EncryptData(plain) : throw new InvalidOperationException("DEK not found"));
            encryptor.Setup(e => e.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] cipher, string id, string algo, CancellationToken t) => id == dekId ? TestCommon.DecryptData(cipher) : throw new InvalidOperationException("DEK not found"));

            dekMock = localDek;
            return encryptor;
        }

        public static Mock<Encryptor> CreateLegacy(string dekId)
        {
            Mock<Encryptor> encryptor = new Mock<Encryptor>();
            encryptor.Setup(e => e.EncryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] plain, string id, string algo, CancellationToken t) => id == dekId ? TestCommon.EncryptData(plain) : throw new InvalidOperationException("DEK not found"));
            encryptor.Setup(e => e.DecryptAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] cipher, string id, string algo, CancellationToken t) => id == dekId ? TestCommon.DecryptData(cipher) : throw new InvalidOperationException("Null DEK was returned."));
            return encryptor;
        }
    }
}
