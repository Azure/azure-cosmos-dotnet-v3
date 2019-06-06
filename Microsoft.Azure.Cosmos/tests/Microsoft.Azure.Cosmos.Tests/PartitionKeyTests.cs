//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PartitionKeyTests
    {
        [TestMethod]
        public void ValidatePartitionKeySupportedTypes()
        {
            Dictionary<dynamic, string> pkValuesToJsonStrings = new Dictionary<dynamic, string>()
            {
                {"testString", "[\"testString\"]" },
                {1234, "[1234.0]" },
                {42.42, "[42.42]" },
                {true, "[true]" },
                {false, "[false]" },
            };

            foreach(var pkValueToJsonString in pkValuesToJsonStrings)
            {
                Documents.PartitionKey v2PK = new Documents.PartitionKey(pkValueToJsonString.Key);
                PartitionKey pk = new PartitionKey(pkValueToJsonString.Key);
                Assert.AreEqual(pkValueToJsonString.Value, v2PK.InternalKey.ToJsonString());
                Assert.AreEqual(pkValueToJsonString.Value, pk.ToString());
            }

            Guid testGuid = new Guid("228326CF-6B43-46B1-BC86-11701FB06E51");
            Documents.PartitionKey v2PKGuid = new Documents.PartitionKey(testGuid.ToString());
            PartitionKey pkGuid = new PartitionKey(testGuid);
            Assert.AreEqual(v2PKGuid.InternalKey.ToJsonString(), pkGuid.ToString());
            Assert.AreEqual("[\"228326cf-6b43-46b1-bc86-11701fb06e51\"]", pkGuid.ToString());
        }

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