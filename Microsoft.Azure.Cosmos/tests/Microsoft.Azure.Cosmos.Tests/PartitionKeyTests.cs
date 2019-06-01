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
        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void NullValue()
        {
            new PartitionKey(null);
        }

        [TestMethod]
        public void WithDocumentPartitionKey()
        {
            const string somePK = "somePK";
            Documents.PartitionKey v2PK = new Documents.PartitionKey(somePK);
            PartitionKey pk = new PartitionKey(v2PK);
            Assert.AreEqual(v2PK.InternalKey.ToJsonString(), pk.ToString());
        }

        [TestMethod]
        public void ValueContainsOriginalValue()
        {
            const string somePK = "somePK";
            PartitionKey pk = new PartitionKey(somePK);
            Assert.AreEqual(somePK, pk.Value);
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
        public void WithCosmosPartitionKey()
        {
            const string somePK = "somePK";
            PartitionKey v3PK = new PartitionKey(somePK);
            PartitionKey pk = new PartitionKey(v3PK);
            Assert.AreEqual(v3PK.ToString(), pk.ToString());
        }

        [TestMethod]
        public void TestPartitionKeyValues()
        {
            Tuple<object, string>[] testcases =
            {
                Tuple.Create<object, string>(Documents.Undefined.Value, "[{}]"),
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
    }
}