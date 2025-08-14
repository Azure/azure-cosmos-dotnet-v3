//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EncryptionProcessorNullCryptoPathTests
    {
        // These tests require a seam to inject an algorithm that returns null from Encrypt/Decrypt.
        // Marking as Ignored for now; when a test hook is added, implement them to assert
        // InvalidOperationException is thrown with the expected messages.

        [TestMethod]
        [Ignore("Pending test seam to inject null-returning algorithm")]
        public void SerializeAndEncryptValueAsync_ReturnsNullCipher_Throws()
        {
        }

        [TestMethod]
        [Ignore("Pending test seam to inject null-returning algorithm")]
        public void DecryptAndDeserializeValueAsync_ReturnsNullPlain_Throws()
        {
        }
    }
}
