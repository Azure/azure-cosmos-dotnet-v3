//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SqlParameterEqualityTests
    {
        [TestMethod]
        public void SqlParameterEqualityTest()
        {
            SqlParameter param1 = new SqlParameter("name", null);
            SqlParameter param2 = new SqlParameter("name", null);

            Assert.IsTrue(param1.Equals(param2));

            SqlParameter param3 = new SqlParameter("name", "value");

            Assert.IsFalse(param1.Equals(param3));

            SqlParameter param4 = new SqlParameter("name", new string(new[] { 'v', 'a', 'l', 'u', 'e' }));
            Assert.IsTrue(param3.Equals(param4));
        }
    }
}
