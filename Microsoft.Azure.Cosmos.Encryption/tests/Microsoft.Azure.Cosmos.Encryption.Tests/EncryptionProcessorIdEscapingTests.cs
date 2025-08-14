//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EncryptionProcessorIdEscapingTests
    {
        [TestMethod]
        public void Base64_UriSafe_Roundtrip_With_Url_Problematic_Chars()
        {
            // bytes that produce '+' and '/' in standard Base64
            // Build input with a wide byte distribution
            byte[] input = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

            string uriSafe = EncryptionProcessor.ConvertToBase64UriSafeString(input);

            // Assert it contains neither '+' nor '/'
            Assert.IsFalse(uriSafe.Contains('+'));
            Assert.IsFalse(uriSafe.Contains('/'));

            byte[] roundtrip = EncryptionProcessor.ConvertFromBase64UriSafeString(uriSafe);
            CollectionAssert.AreEqual(input, roundtrip, "URI-safe Base64 conversion should be lossless.");
        }

        [TestMethod]
        public void Base64_UriSafe_Does_Not_Pad_With_Whitespace()
        {
            byte[] input = Encoding.UTF8.GetBytes("some id with / and + and ? #");
            string uriSafe = EncryptionProcessor.ConvertToBase64UriSafeString(input);

            // Sanity: No whitespace
            Assert.IsFalse(uriSafe.Any(char.IsWhiteSpace));

            // Roundtrip
            byte[] roundtrip = EncryptionProcessor.ConvertFromBase64UriSafeString(uriSafe);
            CollectionAssert.AreEqual(input, roundtrip);
        }
    }
}
