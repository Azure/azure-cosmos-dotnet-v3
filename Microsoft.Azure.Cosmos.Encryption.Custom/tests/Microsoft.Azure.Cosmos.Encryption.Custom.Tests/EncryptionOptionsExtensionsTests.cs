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
            }.Validate(default));

            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionOptions()
            {
                DataEncryptionKeyId = "something",
                EncryptionAlgorithm = null,
                PathsToEncrypt = new List<string>()
            }.Validate(default));

            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionOptions()
            {
                DataEncryptionKeyId = "something",
                EncryptionAlgorithm = "something",
                PathsToEncrypt = null
            }.Validate(default));

#if NET8_0_OR_GREATER
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.ThrowsException<NotSupportedException>(() => new EncryptionOptions()
            {
                DataEncryptionKeyId = "something",
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = new List<string> { "/id" },
            }.Validate(JsonProcessor.Stream));
#pragma warning restore CS0618 // Type or member is obsolete
#endif
        }
    }
}
