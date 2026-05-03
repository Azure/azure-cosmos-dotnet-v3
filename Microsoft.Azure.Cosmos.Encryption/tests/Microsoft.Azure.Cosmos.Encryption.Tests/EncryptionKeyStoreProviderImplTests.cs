//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using global::Azure.Core.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class EncryptionKeyStoreProviderImplTests
    {
        [TestMethod]
        public void Constructor_DekByteCacheEnabled_TwoHours()
        {
            Mock<IKeyEncryptionKeyResolver> mockResolver = new Mock<IKeyEncryptionKeyResolver>();
            EncryptionKeyStoreProviderImpl provider = new EncryptionKeyStoreProviderImpl(mockResolver.Object, "testProvider");

            Assert.AreEqual(TimeSpan.FromHours(2), provider.DataEncryptionKeyCacheTimeToLive);
        }
    }
}
