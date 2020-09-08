//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Threading;
    using global::Azure;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class UnwrappedDekLifecycleManagerTests
    {
        private static Mock<EncryptionKeyWrapProvider> mockEncryptionKeyWrapProvider;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider = new Mock<EncryptionKeyWrapProvider>();
            UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider
                .Setup(m => m.UnwrapKeyAsync(It.IsAny<byte[]>(), It.IsAny<EncryptionKeyWrapMetadata>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] wrappedKey, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken) =>
                    metadata.Value == "metadata1" ?
                    throw new UnauthorizedAccessException() :
                    metadata.Value == "accessRevoked" ?
                    throw new RequestFailedException(403, "Operation unwrapKey is not permitted on this key.") :
                    new EncryptionKeyUnwrapResult(wrappedKey, TimeSpan.FromSeconds(1)));
        }

        private UnwrappedDekLifecycleManager CreateUnwrappedDekLifecycleManager()
        {
            CosmosDataEncryptionKeyProvider cosmosDataEncryptionKeyProvider = new CosmosDataEncryptionKeyProvider(
                UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider.Object,
                backgroundRefreshInterval: TimeSpan.FromSeconds(0.25));

            return cosmosDataEncryptionKeyProvider.UnwrappedDekLifecycleManager;
        }

        [TestMethod]
        public void ValidateWithMultipleDeks()
        {
            UnwrappedDekLifecycleManager unwrappedDekLifecycleManager = this.CreateUnwrappedDekLifecycleManager();

            // refresh is not allowed, should expire
            Mock<DataEncryptionKey> dek1 = new Mock<DataEncryptionKey>();
            InMemoryRawDek rawDek1 = this.CreateInMemoryRawDek(
                dek1.Object,
                "dekId1",
                "metadata1",
                TimeSpan.FromSeconds(1),
                25);

            // refresh is allowed, shouldn't expire
            Mock<DataEncryptionKey> dek2 = new Mock<DataEncryptionKey>();
            InMemoryRawDek rawDek2 = this.CreateInMemoryRawDek(
                dek2.Object,
                "dekId2",
                "metadata2",
                TimeSpan.FromSeconds(1),
                25);

            // to ensure refresh is attempted
            rawDek1.UpdateLastUsageTime();
            rawDek2.UpdateLastUsageTime();

            unwrappedDekLifecycleManager.Add(rawDek1);
            unwrappedDekLifecycleManager.Add(rawDek2);

            Thread.Sleep(300);

            UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider.Verify(m => m.UnwrapKeyAsync(
                    rawDek1.DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                    rawDek1.DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                    It.IsAny<CancellationToken>()),
                Times.Once);
            dek1.Verify(m => m.Dispose(), Times.Never);

            UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider.Verify(m => m.UnwrapKeyAsync(
                    rawDek2.DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                    rawDek2.DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                    It.IsAny<CancellationToken>()),
                Times.Once);
            dek2.Verify(m => m.Dispose(), Times.Never);

            // to ensure refresh is attempted again
            rawDek1.UpdateLastUsageTime();
            rawDek2.UpdateLastUsageTime();

            Thread.Sleep(800);

            UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider.Verify(m => m.UnwrapKeyAsync(
                    rawDek1.DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                    rawDek1.DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
            dek1.Verify(m => m.Dispose(), Times.Once);

            UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider.Verify(m => m.UnwrapKeyAsync(
                    rawDek2.DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                    rawDek2.DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
            dek2.Verify(m => m.Dispose(), Times.Never);
        }

        [TestMethod]
        public void DekIsDisposedIfAccessIsRevoked()
        {
            UnwrappedDekLifecycleManager unwrappedDekLifecycleManager = this.CreateUnwrappedDekLifecycleManager();

            Mock<DataEncryptionKey> dek1 = new Mock<DataEncryptionKey>();
            InMemoryRawDek rawDek1 = this.CreateInMemoryRawDek(
                dek1.Object,
                "dekId1",
                "accessRevoked",
                TimeSpan.FromSeconds(0.5),
                25);

            unwrappedDekLifecycleManager.Add(rawDek1);
            rawDek1.UpdateLastUsageTime(); // to ensure refresh is attempted

            Thread.Sleep(300);

            UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider.Verify(m => m.UnwrapKeyAsync(
                    rawDek1.DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                    rawDek1.DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            dek1.Verify(m => m.Dispose(), Times.Once);
        }

        [TestMethod]
        public void DekIsDisposedAfterExpiry()
        {
            UnwrappedDekLifecycleManager unwrappedDekLifecycleManager = this.CreateUnwrappedDekLifecycleManager();

            // Refresh is blocked, should expire
            Mock<DataEncryptionKey> dek1 = new Mock<DataEncryptionKey>();
            InMemoryRawDek rawDek1 = this.CreateInMemoryRawDek(
                dek1.Object,
                "dekId1",
                "metadata1",
                TimeSpan.FromSeconds(0.5),
                25);

            unwrappedDekLifecycleManager.Add(rawDek1);
            rawDek1.UpdateLastUsageTime(); // to ensure refresh is attempted

            Thread.Sleep(750);

            UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider.Verify(m => m.UnwrapKeyAsync(
                    rawDek1.DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                    rawDek1.DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            dek1.Verify(m => m.Dispose(), Times.Once);
        }

        [TestMethod]
        public void RawDekIsRefreshedAndExpiryTimeUpdated()
        {
            UnwrappedDekLifecycleManager unwrappedDekLifecycleManager = this.CreateUnwrappedDekLifecycleManager();

            // Refresh is allowed, so shouldn't expire
            Mock<DataEncryptionKey> dek1 = new Mock<DataEncryptionKey>();
            InMemoryRawDek rawDek1 = this.CreateInMemoryRawDek(
                dek1.Object,
                "dekId1",
                "metadata2",
                TimeSpan.FromSeconds(0.5),
                25);

            unwrappedDekLifecycleManager.Add(rawDek1);

            for (int i = 0; i < 3; i++)
            {
                rawDek1.UpdateLastUsageTime(); // to ensure refresh is attempted
                Thread.Sleep(300);

                UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider.Verify(m => m.UnwrapKeyAsync(
                        rawDek1.DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                        rawDek1.DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                        It.IsAny<CancellationToken>()),
                    Times.AtLeast(i+1));

                dek1.Verify(m => m.Dispose(), Times.Never);
            }
        }

        [TestMethod]
        public void DekIsRefreshedBasedOnLastUsageTime()
        {
            UnwrappedDekLifecycleManager unwrappedDekLifecycleManager = this.CreateUnwrappedDekLifecycleManager();

            Mock<DataEncryptionKey> dek1 = new Mock<DataEncryptionKey>();
            InMemoryRawDek rawDek1 = this.CreateInMemoryRawDek(
                dek1.Object,
                "dekId1",
                "metadata2",
                TimeSpan.FromSeconds(1),
                25);

            Mock<DataEncryptionKey> dek2 = new Mock<DataEncryptionKey>();
            InMemoryRawDek rawDek2 = this.CreateInMemoryRawDek(
                dek2.Object,
                "dekId2",
                "metadata2",
                TimeSpan.FromSeconds(1),
                25);

            rawDek1.UpdateLastUsageTime(); // dekId1 refresh should be attempted

            unwrappedDekLifecycleManager.Add(rawDek1);
            unwrappedDekLifecycleManager.Add(rawDek2);

            Thread.Sleep(500);

            UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider.Verify(m => m.UnwrapKeyAsync(
                    rawDek1.DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                    rawDek1.DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider.Verify(m => m.UnwrapKeyAsync(
                    rawDek2.DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                    rawDek2.DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                    It.IsAny<CancellationToken>()),
                Times.Never);

            rawDek2.UpdateLastUsageTime(); // dekId2 refresh should be attempted

            Thread.Sleep(250);

            UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider.Verify(m => m.UnwrapKeyAsync(
                    rawDek1.DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                    rawDek1.DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                    It.IsAny<CancellationToken>()),
                Times.Once); // dekId1 isn't refreshed again because LastUsageTime

            UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider.Verify(m => m.UnwrapKeyAsync(
                    rawDek2.DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                    rawDek2.DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        [TestMethod]
        public void ValidateDekRefreshFrequency()
        {
            UnwrappedDekLifecycleManager unwrappedDekLifecycleManager = this.CreateUnwrappedDekLifecycleManager();

            Mock<DataEncryptionKey> dek1 = new Mock<DataEncryptionKey>();
            InMemoryRawDek rawDek1 = this.CreateInMemoryRawDek(
                dek1.Object,
                "dekId1",
                "metadata2",
                TimeSpan.FromSeconds(1),
                25);

            Mock<DataEncryptionKey> dek2 = new Mock<DataEncryptionKey>();
            InMemoryRawDek rawDek2 = this.CreateInMemoryRawDek(
                dek2.Object,
                "dekId2",
                "metadata2",
                TimeSpan.FromSeconds(1),
                75);

            rawDek1.UpdateLastUsageTime();
            rawDek2.UpdateLastUsageTime();

            unwrappedDekLifecycleManager.Add(rawDek1);
            unwrappedDekLifecycleManager.Add(rawDek2);

            Thread.Sleep(500);

            UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider.Verify(m => m.UnwrapKeyAsync(
                    rawDek1.DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                    rawDek1.DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            UnwrappedDekLifecycleManagerTests.mockEncryptionKeyWrapProvider.Verify(m => m.UnwrapKeyAsync(
                    rawDek2.DataEncryptionKeyProperties.WrappedDataEncryptionKey,
                    rawDek2.DataEncryptionKeyProperties.EncryptionKeyWrapMetadata,
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private InMemoryRawDek CreateInMemoryRawDek(
            DataEncryptionKey dek,
            string dekId,
            string metadata,
            TimeSpan timeSpan,
            ushort dekRefreshFrequency)
        {
            return new InMemoryRawDek(
                dek,
                new DataEncryptionKeyProperties(
                    dekId,
                    CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                    TestCommon.GenerateRandomByteArray(),
                    new EncryptionKeyWrapMetadata(metadata),
                    DateTime.UtcNow),
                timeSpan,
                dekRefreshFrequency);
        }
    }
}
