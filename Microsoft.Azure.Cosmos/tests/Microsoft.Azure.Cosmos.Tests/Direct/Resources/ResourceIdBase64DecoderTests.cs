//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ResourceIdBase64DecoderTests
    {
        [TestMethod]
        [Owner("cosmosdbquery")] // dev maintainer: dmmelnik
        [DataRow("dGVzdGRlY29kZXI=")]
        [DataRow("dGVzdGRlY29kZXI=  ")]
        [DataRow("not a base64 text")]
        [DataRow("JCVeJCMlJCMkISMkQVNERUZBU0RUV0VSVA==")]
        [DataRow("bGlnaHQgdw==")]
        [DataRow("bGlnaHQgd28=")]
        [DataRow("bGlnaHQgd29y")]
        [DataRow("bGlnaHQgdw")]
        [DataRow("bGlnaHQgd28")]
        [DataRow("bGlnaHQgd29y")]
        [DataRow("FSEMURh/")]
        public void DecodeTest(string base64string)
        {
            byte[] expectedBytes;
            try
            {
                expectedBytes = Convert.FromBase64String(base64string);
            }
            catch (FormatException)
            {
                expectedBytes = null;
            }

            bool actualDecodingOutcome = ResourceIdBase64Decoder.TryDecode(base64string, out byte[] actualBytes);

            // Compare contract
            Assert.AreEqual(expectedBytes != null, actualDecodingOutcome, "Unexpected test outcome");
            Assert.AreEqual(expectedBytes == null, actualBytes == null, "bytes are not set to null for a failed operation");

            // Compare actual content
            if (expectedBytes != null)
            {
                ResourceIdBase64DecoderTests.AreEqual(expectedBytes, actualBytes);
            }
        }

        [TestMethod]
        [Owner("cosmosdbquery")] // dev maintainer: dmmelnik
        public void DecodeRandomTest()
        {
            Random random = new(42);

            for (int i = 0; i < 100; i++)
            {
                int size = random.Next(1, 100);
                byte[] expectedBytes = new byte[size];
                random.NextBytes(expectedBytes);
                string base64String = Convert.ToBase64String(expectedBytes);
                Assert.IsTrue(ResourceIdBase64Decoder.TryDecode(base64String, out byte[] actualBytes), $"Failed to decode proper base64 string {base64String}");
                ResourceIdBase64DecoderTests.AreEqual(expectedBytes, actualBytes);
            }
        }

        private static void AreEqual(byte[] expectedBytes, byte[] actualBytes)
        {
            Assert.AreEqual(expectedBytes.Length, actualBytes.Length, "Unexpected decoded bytes size");
            for (int i = 0; i < expectedBytes.Length; i++)
            {
                Assert.AreEqual(expectedBytes[i], actualBytes[i], "Unexpected decoded bytes");
            }
        }
    }
}