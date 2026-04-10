//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
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
            byte[] bytes = BitConverter.GetBytes(374.0);
            Assert.AreEqual(3717946798U, MurmurHash3.Hash32(bytes, bytes.Length));


            Assert.AreEqual(
                MurmurHash3.Hash128(bytes, bytes.Length, seed: 0).GetHigh(),
                Cosmos.MurmurHash3.Hash128((ReadOnlySpan<byte>)bytes.AsSpan(), seed: 0).GetHigh());

            Assert.AreEqual(
                MurmurHash3.Hash128(bytes, bytes.Length, seed: 0).GetLow(),
                Cosmos.MurmurHash3.Hash128((ReadOnlySpan<byte>)bytes.AsSpan(), seed: 0).GetLow());
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

            Assert.AreEqual(
                MurmurHash3.Hash128(bytes, bytes.Length, seed: 0).GetHigh(),
                Cosmos.MurmurHash3.Hash128((ReadOnlySpan<byte>)bytes.AsSpan(), seed: 0).GetHigh());

            Assert.AreEqual(
                MurmurHash3.Hash128(bytes, bytes.Length, seed: 0).GetLow(),
                Cosmos.MurmurHash3.Hash128((ReadOnlySpan<byte>)bytes.AsSpan(), seed: 0).GetLow());
        }
    }
}