//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests  <see cref="MurmurHash3"/> class and its compatibility with backend implementation.
    /// </summary>
    [TestClass]
    public class MurmurHash3Test
    {
        /// <summary>
        /// Tests hashing double value in same way PartitionKey does this.
        /// This test ensures doubles are hashed same way on backend and client.
        /// </summary>
        [TestMethod]
        public void TestDoubleHash()
        {
            using (MemoryStream ms = new MemoryStream(new byte[8]))
            {
                using (BinaryWriter writer = new BinaryWriter(ms))
                {
                    writer.Write(374.0);

                    ms.Seek(0, SeekOrigin.Begin);
                    using (BinaryReader reader = new BinaryReader(ms))
                    {
                        byte[] bytes = reader.ReadBytes(8);
                        Assert.AreEqual(3717946798U, MurmurHash3.Hash32(bytes, bytes.Length));
                    }
                }
            }
        }

        /// <summary>
        /// Tests hashing string value in same way PartitionKey does this.
        /// This test ensures strings are hashed same way on backend and client.
        /// </summary>
        [TestMethod]
        public void TestStringHash()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("afdgdd");
            Assert.AreEqual(1099701186U, MurmurHash3.Hash32(bytes, bytes.Length));
        }
    }
}
