//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.IO;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests  <see cref="MurmurHash3"/> class and its compatibility with backend implementation.
    /// </summary>
    [TestClass]
    public class DirectMurmurHash3Tests
    {


        /// <summary>
        /// Tests hashing string value using MurmurHash3.Hash64().
        /// </summary>
        [TestMethod]
        [Owner("adrianmi")]
        public void TestStringWithHash64()
        {
            string key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
            byte[] bytes = Encoding.UTF8.GetBytes(key);

            ulong hashedResult = MurmurHash3.Hash64(bytes, bytes.Length);
            string hashedStringValue = String.Format("{0:X}", hashedResult);

            Assert.AreEqual("8DB3453680634B7D", hashedStringValue);
        }

#if !NET8_0 //NET 8 does not support hashedResult.GetHigh(), hashedResult.GetLow() and UInt128
        [TestMethod]
        [Owner("sqlx")]
        public void TestStringWithHash128()
        {
            string key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
            byte[] bytes = Encoding.UTF8.GetBytes(key);

            UInt128 hashedResult = MurmurHash3.Hash128(bytes, bytes.Length, 0);

            Assert.AreEqual(3839973173908400816UL, hashedResult.GetHigh());
            Assert.AreEqual(1711003452693763388UL, hashedResult.GetLow());
        }

        [TestMethod]
        [Owner("sqlx")]
        public void TestDoubleHash128()
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
                        UInt128 hashedResult = MurmurHash3.Hash128(bytes, bytes.Length, 0);

                        Assert.AreEqual(12953474369317462UL, hashedResult.GetHigh());
                        Assert.AreEqual(16628891264555680919UL, hashedResult.GetLow());
                    }
                }
            }
        }
#endif
    }
}