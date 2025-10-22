//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DataEncryptionKeyContainerCoreTests
    {
        private TestEncryptionKeyStoreProvider keyStore;
        private CosmosDataEncryptionKeyProvider provider;
        private DataEncryptionKeyContainerCore sut;

        [TestInitialize]
        public void TestInit()
        {
            this.keyStore = new TestEncryptionKeyStoreProvider();
            this.provider = new CosmosDataEncryptionKeyProvider(this.keyStore);
            this.sut = new DataEncryptionKeyContainerCore(this.provider);
        }

        [TestMethod]
        public async Task FetchDataEncryptionKeyPropertiesAsync_WithCachedValue_ReturnsFromCache()
        {
            DataEncryptionKeyProperties dekProperties = CreateMdeDekProperties();
            this.provider.DekCache.SetDekProperties(dekProperties.Id, dekProperties);

            DataEncryptionKeyProperties fetched = await this.sut.FetchDataEncryptionKeyPropertiesAsync(
                dekProperties.Id,
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(dekProperties, fetched);
        }

        [TestMethod]
        public async Task FetchUnwrappedAsync_WithMdeDek_ReturnsInMemoryRawDek()
        {
            DataEncryptionKeyProperties dekProperties = CreateMdeDekProperties();

            InMemoryRawDek rawDek = await this.sut.FetchUnwrappedAsync(
                dekProperties,
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None,
                withRawKey: true);

            Assert.IsNotNull(rawDek?.DataEncryptionKey?.RawKey);
            Assert.AreEqual(1, this.keyStore.UnwrapCalls);
        }

        [TestMethod]
        public async Task WrapAsync_WithMdeAlgorithm_RoundTripsKey()
        {
            byte[] rawKey = this.keyStore.DerivedRawKey;

            (byte[] wrapped, _, InMemoryRawDek rawDek) = await this.sut.WrapAsync(
                id: "dekWrap",
                key: rawKey,
                encryptionAlgorithm: CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                metadata: new EncryptionKeyWrapMetadata("name", "value"),
                diagnosticsContext: CosmosDiagnosticsContext.Create(null),
                cancellationToken: CancellationToken.None);

            Assert.AreEqual(1, this.keyStore.WrapCalls);
            Assert.AreEqual(1, this.keyStore.UnwrapCalls);
            Assert.IsNotNull(wrapped);
            Assert.IsNull(rawDek);
        }

        [TestMethod]
        public async Task WrapAsync_WithUnsupportedAlgorithm_ThrowsArgumentException()
        {
            ArgumentException ex = await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
                await this.sut.WrapAsync(
                    id: "dekBad",
                    key: new byte[32],
                    encryptionAlgorithm: "BadAlgo",
                    metadata: new EncryptionKeyWrapMetadata("name", "value"),
                    diagnosticsContext: CosmosDiagnosticsContext.Create(null),
                    cancellationToken: CancellationToken.None));

            StringAssert.Contains(ex.Message, "Unsupported encryption algorithm");
        }

        private static DataEncryptionKeyProperties CreateMdeDekProperties(string id = "dek")
        {
            return new(
                id,
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                wrappedDataEncryptionKey: Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(),
                encryptionKeyWrapMetadata: new EncryptionKeyWrapMetadata("name", "value"),
                createdTime: DateTime.UtcNow)
            { ETag = "etag" };
        }
    }
}
