namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EncryptionPropertiesTests
    {
        [TestMethod]
        public void Ctor_AssignsAllMandatoryProperties()
        {
            EncryptionProperties properties = new (
                11,
                "algorithm",
                "dek-id",
                new byte[] { 1, 2, 3, 4, 5 },
                new List<string> { "a", "b" });

            Assert.AreEqual(11, properties.EncryptionFormatVersion);
            Assert.AreEqual("algorithm", properties.EncryptionAlgorithm);
            Assert.AreEqual("dek-id", properties.DataEncryptionKeyId);
            Assert.IsTrue(new byte[] { 1, 2, 3, 4, 5}.SequenceEqual(properties.EncryptedData));
            Assert.IsTrue(new List<string> { "a", "b"}.SequenceEqual(properties.EncryptedPaths));
            Assert.AreEqual(CompressionOptions.CompressionAlgorithm.None, properties.CompressionAlgorithm);
            Assert.IsNull(properties.CompressedEncryptedPaths);
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public void Ctor_AssignsAllProperties()
        {
            EncryptionProperties properties = new (
                11,
                "algorithm",
                "dek-id",
                new byte[] { 1, 2, 3, 4, 5 },
                new List<string> { "a", "b" },
                CompressionOptions.CompressionAlgorithm.Brotli,
                new Dictionary<string, int> { { "a", 246 } });

            Assert.AreEqual(11, properties.EncryptionFormatVersion);
            Assert.AreEqual("algorithm", properties.EncryptionAlgorithm);
            Assert.AreEqual("dek-id", properties.DataEncryptionKeyId);
            Assert.IsTrue(new byte[] { 1, 2, 3, 4, 5 }.SequenceEqual(properties.EncryptedData));
            Assert.IsTrue(new List<string> { "a", "b" }.SequenceEqual(properties.EncryptedPaths));
            Assert.AreEqual(CompressionOptions.CompressionAlgorithm.Brotli, properties.CompressionAlgorithm);
            Assert.IsTrue(new Dictionary<string, int> { { "a", 246 } }.SequenceEqual(properties.CompressedEncryptedPaths));
        }
#endif
    }
}
