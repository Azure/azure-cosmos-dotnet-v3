namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EncryptionOptionsExtensionsTests
    {
        [TestMethod]
        public void Validate_EncryptionOptions_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionOptions()
            {
                DataEncryptionKeyId = null,
                EncryptionAlgorithm = "something",
                PathsToEncrypt = new List<string>()
            }.Validate());

            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionOptions()
            {
                DataEncryptionKeyId = "something",
                EncryptionAlgorithm = null,
                PathsToEncrypt = new List<string>()
            }.Validate());

            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionOptions()
            {
                DataEncryptionKeyId = "something",
                EncryptionAlgorithm = "something",
                PathsToEncrypt = null
            }.Validate());
        }
    }
}
