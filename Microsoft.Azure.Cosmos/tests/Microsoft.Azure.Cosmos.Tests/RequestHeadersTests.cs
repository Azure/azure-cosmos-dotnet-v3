//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class RequestHeadersTests
    {
        private const string Key = "testKey";

        [TestMethod]
        public void TestAddAndGetAndSet()
        {
            string value1 = Guid.NewGuid().ToString();
            string value2 = Guid.NewGuid().ToString();
            var requestHeaders = new RequestHeaders();
            requestHeaders.Add(Key, value1);
            Assert.AreEqual(value1, requestHeaders.Get(Key));
            requestHeaders.Set(Key, value2);
            Assert.AreEqual(value2, requestHeaders.Get(Key));
        }

        [TestMethod]
        public void TestIndexer()
        {
            var requestHeaders = new RequestHeaders();
            string value = Guid.NewGuid().ToString();
            requestHeaders.CosmosMessageHeaders[Key] = value;
            Assert.AreEqual(value, requestHeaders[Key]);
        }

        [TestMethod]
        public void TestRemove()
        {
            var requestHeaders = new RequestHeaders();
            string value = Guid.NewGuid().ToString();
            requestHeaders.CosmosMessageHeaders[Key] = value;
            Assert.AreEqual(value, requestHeaders[Key]);
            requestHeaders.Remove(Key);
            Assert.IsNull(requestHeaders[Key]);
        }

        [TestMethod]
        public void TestClear()
        {
            var requestHeaders = new RequestHeaders();
            requestHeaders.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            requestHeaders.CosmosMessageHeaders.Clear();
            Assert.IsNull(requestHeaders[Key]);
        }

        [TestMethod]
        public void TestCount()
        {
            var requestHeaders = new RequestHeaders();
            requestHeaders.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            Assert.AreEqual(1, requestHeaders.CosmosMessageHeaders.Count());
        }

        [TestMethod]
        public void TestGetValues()
        {
            var requestHeaders = new RequestHeaders();
            string value1 = Guid.NewGuid().ToString();
            requestHeaders.Add(Key, value1);
            IEnumerable<string> values = requestHeaders.GetValues(Key);
            Assert.AreEqual(1, values.Count());
        }

        [TestMethod]
        public void TestAllKeys()
        {
            var requestHeaders = new RequestHeaders();
            requestHeaders.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            Assert.AreEqual(Key, requestHeaders.AllKeys().First());
        }

        [TestMethod]
        public void TestGetIEnumerableKeys()
        {
            var requestHeaders = new RequestHeaders();
            string value = Guid.NewGuid().ToString();
            requestHeaders.CosmosMessageHeaders[Key] = value;
            foreach (var header in requestHeaders)
            {
                Assert.AreEqual(value, requestHeaders[header]);
                return;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(NotImplementedException))]
        public void TestGetToNameValueCollection()
        {
            var requestHeaders = new RequestHeaders();
            string value = Guid.NewGuid().ToString();
            requestHeaders.CosmosMessageHeaders[Key] = value;
            NameValueCollection anotherCollection = requestHeaders.CosmosMessageHeaders.ToNameValueCollection();
            Assert.AreEqual(value, anotherCollection[Key]);
        }

        [TestMethod]
        public void TestSetAndGetKnownProperties()
        {
            string value1 = Guid.NewGuid().ToString();
            string value2 = Guid.NewGuid().ToString();
            string value3 = Guid.NewGuid().ToString();
            var requestHeaders = new RequestHeaders();
            requestHeaders.Continuation = value1;
            requestHeaders.PartitionKey = value2;
            requestHeaders.PartitionKeyRangeId = value3;
            Assert.AreEqual(value1, requestHeaders.Continuation);
            Assert.AreEqual(value2, requestHeaders.PartitionKey);
            Assert.AreEqual(value3, requestHeaders.PartitionKeyRangeId);
            Assert.AreEqual(value1, requestHeaders[HttpConstants.HttpHeaders.Continuation]);
            Assert.AreEqual(value2, requestHeaders[HttpConstants.HttpHeaders.PartitionKey]);
            Assert.AreEqual(value3, requestHeaders[WFConstants.BackendHeaders.PartitionKeyRangeId]);
            value1 = Guid.NewGuid().ToString();
            value2 = Guid.NewGuid().ToString();
            value3 = Guid.NewGuid().ToString();
            requestHeaders.CosmosMessageHeaders[HttpConstants.HttpHeaders.Continuation] = value1;
            requestHeaders.CosmosMessageHeaders[HttpConstants.HttpHeaders.PartitionKey] = value2;
            requestHeaders.CosmosMessageHeaders[WFConstants.BackendHeaders.PartitionKeyRangeId] = value3;
            Assert.AreEqual(value1, requestHeaders.Continuation);
            Assert.AreEqual(value2, requestHeaders.PartitionKey);
            Assert.AreEqual(value3, requestHeaders.PartitionKeyRangeId);
            Assert.AreEqual(value1, requestHeaders[HttpConstants.HttpHeaders.Continuation]);
            Assert.AreEqual(value2, requestHeaders[HttpConstants.HttpHeaders.PartitionKey]);
            Assert.AreEqual(value3, requestHeaders[WFConstants.BackendHeaders.PartitionKeyRangeId]);
        }

        [TestMethod]
        public void TestClearWithKnownProperties()
        {
            var requestHeaders = new RequestHeaders();
            requestHeaders.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            requestHeaders.PartitionKey = Guid.NewGuid().ToString();
            requestHeaders.CosmosMessageHeaders.Clear();
            Assert.IsNull(requestHeaders[Key]);
            Assert.IsNull(requestHeaders.PartitionKey);
        }

        [TestMethod]
        public void TestCountWithKnownProperties()
        {
            var requestHeaders = new RequestHeaders();
            requestHeaders.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            requestHeaders.PartitionKey = Guid.NewGuid().ToString();
            Assert.AreEqual(2, requestHeaders.CosmosMessageHeaders.Count());
        }

        [TestMethod]
        public void TestAllKeysWithKnownProperties()
        {
            var requestHeaders = new RequestHeaders();
            requestHeaders.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            requestHeaders.PartitionKey = Guid.NewGuid().ToString();
            var allKeys = requestHeaders.AllKeys();
            Assert.IsTrue(allKeys.Contains(Key));
            Assert.IsTrue(allKeys.Contains(HttpConstants.HttpHeaders.PartitionKey));
        }

        [TestMethod]
        public void AllKnownPropertiesHaveGetAndSetAndIndexed()
        {
            var requestHeaders = new RequestHeaders();
            var knownHeaderProperties = typeof(RequestHeaders)
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttributes(typeof(CosmosKnownHeaderAttribute), false).Any());

            foreach (var knownHeaderProperty in knownHeaderProperties)
            {
                string value = Guid.NewGuid().ToString();
                string header = ((CosmosKnownHeaderAttribute)knownHeaderProperty.GetCustomAttributes(typeof(CosmosKnownHeaderAttribute), false).First()).HeaderName;
                requestHeaders.CosmosMessageHeaders[header] = value; // Using indexer

                Assert.AreEqual(value, (string)knownHeaderProperty.GetValue(requestHeaders)); // Verify getter

                value = Guid.NewGuid().ToString();
                knownHeaderProperty.SetValue(requestHeaders, value);
                Assert.AreEqual(value, requestHeaders[header]);
            }
        }
    }
}
