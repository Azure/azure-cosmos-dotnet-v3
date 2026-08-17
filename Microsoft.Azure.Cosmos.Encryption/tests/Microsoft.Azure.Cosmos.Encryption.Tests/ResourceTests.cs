//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using Microsoft.Data.Encryption.Resources;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ResourceTests
    {
        [TestMethod]
        public void InvalidAuthenticationTagResourceIsAvailable()
        {
            Assert.AreEqual(
                "Specified ciphertext has an invalid authentication tag.",
                Strings.InvalidAuthenticationTag);
        }
    }
}
