//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Microsoft.Data.Encryption.Cryptography;

    [TestClass]
    public class CosmosDataEncryptionKeyProviderTests
    {
        private const string ContainerId = "dekContainer";

        [TestMethod]
        public async Task InitializeAsync_WithValidContainer_CreatesAndSetsContainer()
        {
            Mock<Container> mockContainer = new(MockBehavior.Strict);
            Mock<ContainerResponse> mockContainerResponse = new(MockBehavior.Strict);
            mockContainerResponse.Setup(r => r.Container).Returns(mockContainer.Object);
            mockContainerResponse.Setup(r => r.Resource).Returns(new ContainerProperties(ContainerId, partitionKeyPath: "/id"));

            Mock<Database> mockDatabase = new(MockBehavior.Strict);
            mockDatabase
                .Setup(db => db.CreateContainerIfNotExistsAsync(
                    It.Is<string>(s => s == ContainerId),
                    It.Is<string>(pk => pk == "/id"),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);

            CosmosDataEncryptionKeyProvider provider = CreateProvider();

            await provider.InitializeAsync(mockDatabase.Object, ContainerId);

            Assert.AreSame(mockContainer.Object, provider.Container);

            mockDatabase.VerifyAll();
            mockContainerResponse.VerifyAll();
        }

        [TestMethod]
        public async Task InitializeAsync_WithWrongPartitionKey_Throws()
        {
            Mock<Container> mockContainer = new(MockBehavior.Strict);
            Mock<ContainerResponse> mockContainerResponse = new(MockBehavior.Strict);
            mockContainerResponse.Setup(r => r.Container).Returns(mockContainer.Object);
            mockContainerResponse.Setup(r => r.Resource).Returns(new ContainerProperties("dekBad", partitionKeyPath: "/different-id"));

            Mock<Database> mockDatabase = new(MockBehavior.Strict);
            mockDatabase
                .Setup(db => db.CreateContainerIfNotExistsAsync(
                    It.Is<string>(s => s == "dekBad"),
                    It.Is<string>(pk => pk == "/id"),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContainerResponse.Object);

            CosmosDataEncryptionKeyProvider provider = CreateProvider();

            ArgumentException ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => provider.InitializeAsync(mockDatabase.Object, "dekBad"));

            StringAssert.Contains(ex.Message, "partition key definition");
            Assert.AreEqual("containerId", ex.ParamName);

            mockDatabase.VerifyAll();
        }

        [TestMethod]
        public void Initialize_WithContainer_Succeeds()
        {
            Mock<Container> mockContainer = new(MockBehavior.Strict);

            CosmosDataEncryptionKeyProvider provider = CreateProvider();
            provider.Initialize(mockContainer.Object);

            Assert.AreSame(mockContainer.Object, provider.Container);
        }

        [TestMethod]
        public void Initialize_WithNullContainer_Throws()
        {
            CosmosDataEncryptionKeyProvider provider = CreateProvider();

            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() => provider.Initialize(null));

            Assert.AreEqual("container", ex.ParamName);
        }

        [TestMethod]
        public void Initialize_Twice_Throws()
        {
            Mock<Container> mockContainer = new(MockBehavior.Strict);
            CosmosDataEncryptionKeyProvider provider = CreateProvider();
            provider.Initialize(mockContainer.Object);

            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(() => provider.Initialize(mockContainer.Object));

            StringAssert.Contains(ex.Message, nameof(CosmosDataEncryptionKeyProvider));
        }

        [TestMethod]
        public async Task InitializeAsync_AfterInitializeContainer_Throws()
        {
            Mock<Container> mockContainer = new(MockBehavior.Strict);
            Mock<Database> mockDatabase = new(MockBehavior.Strict);
            CosmosDataEncryptionKeyProvider provider = CreateProvider();
            
            provider.Initialize(mockContainer.Object);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => provider.InitializeAsync(mockDatabase.Object, "ignored"));
        }

        [TestMethod]
        public void AccessContainer_BeforeInitialization_Throws()
        {
            CosmosDataEncryptionKeyProvider provider = CreateProvider();

            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(() => _ = provider.Container);

            StringAssert.Contains(ex.Message, nameof(CosmosDataEncryptionKeyProvider));
        }

        [TestMethod]
        public void Constructor_EncryptionKeyWrapProvider_SetsProperties()
        {
#pragma warning disable CS0618
            Mock<EncryptionKeyWrapProvider> wrapProviderMock = new(MockBehavior.Strict);
            
            CosmosDataEncryptionKeyProvider provider = new(wrapProviderMock.Object);
            
            Assert.AreSame(wrapProviderMock.Object, provider.EncryptionKeyWrapProvider);
            Assert.IsNotNull(provider.DataEncryptionKeyContainer);
            Assert.IsNotNull(provider.DekCache);
#pragma warning restore CS0618
        }

        [TestMethod]
        public void Constructor_EncryptionKeyStoreProvider_SetsMdePropertiesAndTtl_DefaultInfinite()
        {
            TestEncryptionKeyStoreProvider keyStoreProvider = new();

            CosmosDataEncryptionKeyProvider provider = new(keyStoreProvider);

            Assert.AreSame(keyStoreProvider, provider.EncryptionKeyStoreProvider);
            Assert.IsNotNull(provider.DekCache);
            Assert.IsNotNull(provider.DataEncryptionKeyContainer);
            Assert.IsTrue(provider.PdekCacheTimeToLive.HasValue);
            Assert.IsTrue(provider.PdekCacheTimeToLive.Value > TimeSpan.Zero);
        }

        [TestMethod]
        public void Constructor_KeyStoreProvider_SetsMdePropertiesAndTtl_Custom()
        {
            TimeSpan ttl = TimeSpan.FromMinutes(15);
            TestEncryptionKeyStoreProvider keyStoreProvider = new()
            {
                DataEncryptionKeyCacheTimeToLive = ttl
            };

            CosmosDataEncryptionKeyProvider provider = new(keyStoreProvider);

            Assert.AreSame(keyStoreProvider, provider.EncryptionKeyStoreProvider);
            Assert.AreEqual(ttl, provider.PdekCacheTimeToLive);
        }

        private static CosmosDataEncryptionKeyProvider CreateProvider()
        {
            return new CosmosDataEncryptionKeyProvider(new TestEncryptionKeyStoreProvider());
        }
    }
}
