//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionProcessorOverflowTests
    {
        [TestMethod]
        public void Serialize_BigInteger_Overflow_Throws()
        {
            // JValue cannot be constructed directly with BigInteger; construct with double exceeding Int64
            // and expect serializer to throw due to unsupported type or overflow when converting to long.
            // Use a numeric token that will be treated as Integer by Newtonsoft only if within range;
            // here we force a path that should not be supported.
            JToken token = new JValue(double.MaxValue);
            bool threw = false;
            try
            {
                _ = EncryptionProcessor.Serialize(token);
            }
            catch (Exception)
            {
                threw = true;
            }

            if (!threw)
            {
                Assert.Fail("Expected an exception during serialization of oversized integer.");
            }
        }
    }
}
