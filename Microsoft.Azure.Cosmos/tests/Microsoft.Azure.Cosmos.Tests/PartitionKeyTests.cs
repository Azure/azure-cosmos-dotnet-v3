//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PartitionKeyTests
    {
        [TestMethod]
        public void NullValue()
        {
            new PartitionKey(null);
        }

        [TestMethod]
        public void ToStringGetsJsonString()
        {
            const string somePK = "somePK";
            string expected = $"[\"{somePK}\"]";
            PartitionKey pk = new PartitionKey(somePK);
            Assert.AreEqual(expected, pk.ToString());
        }

        [TestMethod]
        public void TestPartitionKeyValues()
        {
            Tuple<dynamic, string>[] testcases =
            {
                Tuple.Create<dynamic, string>(Documents.Undefined.Value, "[{}]"),
                Tuple.Create<dynamic, string>(Documents.Undefined.Value, "[{}]"),
                Tuple.Create<dynamic, string>(false, "[false]"),
                Tuple.Create<dynamic, string>(true, "[true]"),
                Tuple.Create<dynamic, string>(123.456, "[123.456]"),
                Tuple.Create<dynamic, string>("PartitionKeyValue", "[\"PartitionKeyValue\"]"),
            };

            foreach (Tuple<object, string> testcase in testcases)
            {
                Assert.AreEqual(testcase.Item2, new PartitionKey(testcase.Item1).ToString());
            }
        }
    }
}