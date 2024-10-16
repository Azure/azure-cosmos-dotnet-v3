#if NET8_0_OR_GREATER

namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.SystemTextJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonBytesTests
    {
        [TestMethod]
        public void Ctor_ThrowsForInvalidInputs()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new JsonBytes(null, 1, 1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new JsonBytes(new byte[10], -1, 1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new JsonBytes(new byte[10], 0, -1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new JsonBytes(new byte[10], 8, 8));
        }

        [TestMethod]
        public void Properties_AreSetCorrectly()
        {
            byte[] bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
            JsonBytes jsonBytes = new (bytes, 1, 5);

            Assert.AreEqual(1, jsonBytes.Offset);
            Assert.AreEqual(5, jsonBytes.Length);
            Assert.AreSame(bytes, jsonBytes.Bytes);
        }
    }
}
#endif
