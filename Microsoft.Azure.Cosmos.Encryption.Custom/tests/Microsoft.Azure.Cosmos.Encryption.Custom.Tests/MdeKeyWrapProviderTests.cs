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
    public class MdeKeyWrapProviderTests
    {
        private TestEncryptionKeyStoreProvider provider;
        private MdeKeyWrapProvider keyWrapProvider;

        [TestInitialize]
        public void TestInitialize()
        {
            this.provider = new TestEncryptionKeyStoreProvider();
            this.keyWrapProvider = new MdeKeyWrapProvider(this.provider);
        }

        [TestMethod]
        public void Constructor_WithNullProvider_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                _ = new MdeKeyWrapProvider(null);
            });
        }

        [TestMethod]
        public async Task WrapKeyAsync_WithValidInputs_EncryptsUsingUnderlyingProvider()
        {
            byte[] keyToWrap = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
            EncryptionKeyWrapMetadata metadata = new("name", "value");

            EncryptionKeyWrapResult result = await this.keyWrapProvider.WrapKeyAsync(keyToWrap, metadata, CancellationToken.None);

            Assert.AreEqual(1, this.provider.WrapCalls);
            Assert.AreEqual(metadata, result.EncryptionKeyWrapMetadata);
        }

        [TestMethod]
        public async Task UnwrapKeyAsync_WithValidInputs_DecryptsUsingUnderlyingProvider()
        {
            byte[] wrappedKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
            EncryptionKeyWrapMetadata metadata = new("name", "value");

            EncryptionKeyUnwrapResult result = await this.keyWrapProvider.UnwrapKeyAsync(wrappedKey, metadata, CancellationToken.None);

            Assert.IsNotNull(result?.DataEncryptionKey);
            Assert.AreEqual(1, this.provider.UnwrapCalls);
        }

        [TestMethod]
        public async Task WrapKeyAsync_WithNullMetadata_ThrowsArgumentNullException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
                await this.keyWrapProvider.WrapKeyAsync(new byte[32], metadata: null, CancellationToken.None));
        }

        [TestMethod]
        public async Task UnwrapKeyAsync_WithNullMetadata_ThrowsArgumentNullException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
                await this.keyWrapProvider.UnwrapKeyAsync(new byte[32], metadata: null, CancellationToken.None));
        }
    }
}
