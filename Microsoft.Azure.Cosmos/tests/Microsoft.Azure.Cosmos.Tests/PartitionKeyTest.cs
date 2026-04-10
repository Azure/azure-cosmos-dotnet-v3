//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;
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
                Assert.AreEqual(testcase.Item2, new Documents.PartitionKey(testcase.Item1).ToString());
            }
        }

        /// <summary>
        /// Test Equals override for <see cref="PartitionKey"/>.
        /// </summary>
        [TestMethod]
        public void TestPartitionKeyCompare()
        {
            Assert.IsTrue(new Documents.PartitionKey(Undefined.Value).Equals(Documents.PartitionKey.FromJsonString("[{}]")));
            Assert.IsTrue(new Documents.PartitionKey(null).Equals(Documents.PartitionKey.FromJsonString("[null]")));
            Assert.IsTrue(new Documents.PartitionKey(false).Equals(Documents.PartitionKey.FromJsonString("[false]")));
            Assert.IsTrue(new Documents.PartitionKey(true).Equals(Documents.PartitionKey.FromJsonString("[true]")));
            Assert.IsTrue(new Documents.PartitionKey(123.456).Equals(Documents.PartitionKey.FromJsonString("[123.456]")));
            Assert.IsTrue(new Documents.PartitionKey("PartitionKey Value").Equals(Documents.PartitionKey.FromJsonString("[\"PartitionKey Value\"]")));
            Assert.IsTrue(new Documents.PartitionKey("null").Equals(Documents.PartitionKey.FromJsonString("[\"null\"]")));
        }
    }
}