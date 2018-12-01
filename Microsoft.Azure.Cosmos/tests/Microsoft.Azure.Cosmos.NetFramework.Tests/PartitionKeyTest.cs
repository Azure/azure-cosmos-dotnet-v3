//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="PartitionKey"/> class.
    /// </summary>
    [TestClass]
    public class PartitionKeyTest
    {
        /// <summary>
        /// Simple test for <see cref="PartitionKey"/> class.
        /// </summary>
        [TestMethod]
        public void TestPartitionKey()
        {
            Tuple<object, string>[] testcases =
            {
                Tuple.Create<object, string>(Undefined.Value, "[{}]"),
                Tuple.Create<object, string>(null, "[null]"),
                Tuple.Create<object, string>(false, "[false]"),
                Tuple.Create<object, string>(true, "[true]"),
                Tuple.Create<object, string>(123.456, "[123.456]"),
                Tuple.Create<object, string>("PartitionKeyValue", "[\"PartitionKeyValue\"]"),
            };

            foreach (Tuple<object, string> testcase in testcases)
            {
                Assert.AreEqual(testcase.Item2, new PartitionKey(testcase.Item1).ToString());
            }
        }

        /// <summary>
        /// Test Equals override for <see cref="PartitionKey"/>.
        /// </summary>
        [TestMethod]
        public void TestPartitionKeyCompare()
        {
            Assert.IsTrue(new PartitionKey(Undefined.Value).Equals(PartitionKey.FromJsonString("[{}]")));
            Assert.IsTrue(new PartitionKey(null).Equals(PartitionKey.FromJsonString("[null]")));
            Assert.IsTrue(new PartitionKey(false).Equals(PartitionKey.FromJsonString("[false]")));
            Assert.IsTrue(new PartitionKey(true).Equals(PartitionKey.FromJsonString("[true]")));
            Assert.IsTrue(new PartitionKey(123.456).Equals(PartitionKey.FromJsonString("[123.456]")));
            Assert.IsTrue(new PartitionKey("PartitionKey Value").Equals(PartitionKey.FromJsonString("[\"PartitionKey Value\"]")));
            Assert.IsTrue(new PartitionKey("null").Equals(PartitionKey.FromJsonString("[\"null\"]")));
        }
    }
}
