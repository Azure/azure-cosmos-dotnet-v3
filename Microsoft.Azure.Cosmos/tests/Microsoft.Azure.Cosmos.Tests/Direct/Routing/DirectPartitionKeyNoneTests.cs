// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DirectPartitionKeyNoneTests
    {
        [Owner("kirankk")]
        [TestMethod]
        public void TestNoneHashCodeToNotThrow()
        {
            // Should succeed and not fail/throw
            PartitionKey.None.GetHashCode();
        }
    }
}