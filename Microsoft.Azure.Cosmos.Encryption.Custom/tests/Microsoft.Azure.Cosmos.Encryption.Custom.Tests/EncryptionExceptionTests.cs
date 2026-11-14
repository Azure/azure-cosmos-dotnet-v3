//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EncryptionExceptionTests
    {
        [TestMethod]
        public void Constructor_WithNullDataEncryptionKeyId_CoalescesToEmptyAndPreservesInner()
        {
            NotSupportedException inner = new("Encryption format version 4 is not supported.");

            EncryptionException ex = new(dataEncryptionKeyId: null, encryptedContent: "cipher", innerException: inner);

            Assert.AreEqual(string.Empty, ex.DataEncryptionKeyId);
            Assert.AreEqual("cipher", ex.EncryptedContent);
            Assert.AreSame(inner, ex.InnerException);
            Assert.AreEqual(inner.Message, ex.Message);
        }

        [TestMethod]
        public void Constructor_WithNullEncryptedContent_CoalescesToEmptyAndPreservesInner()
        {
            InvalidOperationException inner = new("decrypt failed");

            EncryptionException ex = new(dataEncryptionKeyId: "dek-1", encryptedContent: null, innerException: inner);

            Assert.AreEqual("dek-1", ex.DataEncryptionKeyId);
            Assert.AreEqual(string.Empty, ex.EncryptedContent);
            Assert.AreSame(inner, ex.InnerException);
        }

        [TestMethod]
        public void Constructor_WithBothNull_DoesNotThrowAndPreservesInner()
        {
            NotSupportedException inner = new("both null");

            EncryptionException ex = new(dataEncryptionKeyId: null, encryptedContent: null, innerException: inner);

            Assert.AreEqual(string.Empty, ex.DataEncryptionKeyId);
            Assert.AreEqual(string.Empty, ex.EncryptedContent);
            Assert.AreSame(inner, ex.InnerException);
        }

        [TestMethod]
        public void Constructor_WithNonNullValues_RetainsThem()
        {
            InvalidOperationException inner = new("boom");

            EncryptionException ex = new("dek-42", "encrypted-blob", inner);

            Assert.AreEqual("dek-42", ex.DataEncryptionKeyId);
            Assert.AreEqual("encrypted-blob", ex.EncryptedContent);
            Assert.AreSame(inner, ex.InnerException);
        }
    }
}
