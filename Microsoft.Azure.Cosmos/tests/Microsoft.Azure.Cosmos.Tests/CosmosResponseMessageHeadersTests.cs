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
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosResponseMessageHeadersTests
    {
        private const string Key = "testKey";

        [TestMethod]
        public void TestAddAndGetAndSet()
        {
            string value1 = Guid.NewGuid().ToString();
            string value2 = Guid.NewGuid().ToString();
            var requestHeaders = new CosmosResponseMessageHeaders();
            requestHeaders.Add(Key, value1);
            Assert.AreEqual(value1, requestHeaders.Get(Key));
            requestHeaders.Set(Key, value2);
            Assert.AreEqual(value2, requestHeaders.Get(Key));
        }

        [TestMethod]
        public void TestIndexer()
        {
            var requestHeaders = new CosmosResponseMessageHeaders();
            string value = Guid.NewGuid().ToString();
            requestHeaders.CosmosMessageHeaders[Key] = value;
            Assert.AreEqual(value, requestHeaders[Key]);
        }

        [TestMethod]
        public void TestRemove()
        {
            var requestHeaders = new CosmosResponseMessageHeaders();
            string value = Guid.NewGuid().ToString();
            requestHeaders.CosmosMessageHeaders[Key] = value;
            Assert.AreEqual(value, requestHeaders[Key]);
            requestHeaders.Remove(Key);
            Assert.IsNull(requestHeaders[Key]);
        }

        [TestMethod]
        public void TestClear()
        {
            var requestHeaders = new CosmosResponseMessageHeaders();
            requestHeaders.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            requestHeaders.CosmosMessageHeaders.Clear();
            Assert.IsNull(requestHeaders[Key]);
        }

        [TestMethod]
        public void TestCount()
        {
            var requestHeaders = new CosmosResponseMessageHeaders();
            requestHeaders.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            Assert.AreEqual(1, requestHeaders.CosmosMessageHeaders.Count());
        }

        [TestMethod]
        public void TestGetValues()
        {
            var requestHeaders = new CosmosResponseMessageHeaders();
            string value1 = Guid.NewGuid().ToString();
            requestHeaders.Add(Key, value1);
            IEnumerable<string> values = requestHeaders.GetValues(Key);
            Assert.AreEqual(1, values.Count());
        }

        [TestMethod]
        public void TestAllKeys()
        {
            var requestHeaders = new CosmosResponseMessageHeaders();
            requestHeaders.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            Assert.AreEqual(Key, requestHeaders.AllKeys().First());
        }

        [TestMethod]
        public void TestGetIEnumerableKeys()
        {
            var requestHeaders = new CosmosResponseMessageHeaders();
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
            var requestHeaders = new CosmosResponseMessageHeaders();
            string value = Guid.NewGuid().ToString();
            requestHeaders.CosmosMessageHeaders[Key] = value;
            NameValueCollection anotherCollection = requestHeaders.CosmosMessageHeaders.ToNameValueCollection();
            Assert.AreEqual(value, anotherCollection[Key]);
        }

        [TestMethod]
        public void TestSetAndGetKnownProperties()
        {
            string value1 = Guid.NewGuid().ToString();
            string value2 = "1002";
            string value3 = "20";
            string value4 = "someSession";
            var requestHeaders = new CosmosResponseMessageHeaders();
            requestHeaders.CosmosMessageHeaders[HttpConstants.HttpHeaders.Continuation] = value1;
            requestHeaders.CosmosMessageHeaders[WFConstants.BackendHeaders.SubStatus] = value2;
            requestHeaders.CosmosMessageHeaders[HttpConstants.HttpHeaders.RetryAfterInMilliseconds] = value3;
            requestHeaders.CosmosMessageHeaders[HttpConstants.HttpHeaders.SessionToken] = value4;
            Assert.AreEqual(value1, requestHeaders.Continuation);
            Assert.AreEqual(int.Parse(value2), (int)requestHeaders.SubStatusCode);
            Assert.AreEqual(TimeSpan.FromMilliseconds(20), requestHeaders.RetryAfter);
            Assert.AreEqual(value4, requestHeaders.Session);
            Assert.AreEqual(value1, requestHeaders[HttpConstants.HttpHeaders.Continuation]);
            Assert.AreEqual(value2, requestHeaders[WFConstants.BackendHeaders.SubStatus]);
            Assert.AreEqual(value3, requestHeaders[HttpConstants.HttpHeaders.RetryAfterInMilliseconds]);
            Assert.AreEqual(value4, requestHeaders[HttpConstants.HttpHeaders.SessionToken]);
        }

        [TestMethod]
        public void TestClearWithKnownProperties()
        {
            var requestHeaders = new CosmosResponseMessageHeaders();
            requestHeaders.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            requestHeaders.Continuation = Guid.NewGuid().ToString();
            requestHeaders.CosmosMessageHeaders[HttpConstants.HttpHeaders.RetryAfterInMilliseconds] = "20";
            requestHeaders.CosmosMessageHeaders.Clear();
            Assert.IsNull(requestHeaders[Key]);
            Assert.IsNull(requestHeaders.Continuation);
            Assert.IsNull(requestHeaders.RetryAfter);
        }

        [TestMethod]
        public void TestCountWithKnownProperties()
        {
            var requestHeaders = new CosmosResponseMessageHeaders();
            requestHeaders.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            requestHeaders.Continuation = Guid.NewGuid().ToString();
            requestHeaders.CosmosMessageHeaders[HttpConstants.HttpHeaders.RetryAfterInMilliseconds] = "20";
            Assert.AreEqual(3, requestHeaders.CosmosMessageHeaders.Count());
        }

        [TestMethod]
        public void TestAllKeysWithKnownProperties()
        {
            var requestHeaders = new CosmosResponseMessageHeaders();
            requestHeaders.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            requestHeaders.Continuation = Guid.NewGuid().ToString();
            requestHeaders.CosmosMessageHeaders[HttpConstants.HttpHeaders.RetryAfterInMilliseconds] = "20";
            requestHeaders.Add(WFConstants.BackendHeaders.SubStatus, "1002");
            var allKeys = requestHeaders.AllKeys();
            Assert.IsTrue(allKeys.Contains(Key));
            Assert.IsTrue(allKeys.Contains(HttpConstants.HttpHeaders.RetryAfterInMilliseconds));
            Assert.IsTrue(allKeys.Contains(HttpConstants.HttpHeaders.Continuation));
            Assert.IsTrue(allKeys.Contains(WFConstants.BackendHeaders.SubStatus));
        }

        [TestMethod]
        public void AllKnownPropertiesHaveGetAndSetAndIndexed()
        {
            var responseHeaders = new CosmosResponseMessageHeaders();
            var knownHeaderProperties = typeof(CosmosResponseMessageHeaders)
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttributes(typeof(CosmosKnownHeaderAttribute), false).Any());

            foreach(var knownHeaderProperty in knownHeaderProperties)
            {
                string value = "123456789";
                string header = ((CosmosKnownHeaderAttribute)knownHeaderProperty.GetCustomAttributes(typeof(CosmosKnownHeaderAttribute), false).First()).HeaderName;
                responseHeaders.CosmosMessageHeaders[header] = value; // Using indexer

                Assert.AreEqual(value, (string)knownHeaderProperty.GetValue(responseHeaders)); // Verify getter

                value = "9876543210";
                knownHeaderProperty.SetValue(responseHeaders, value);
                Assert.AreEqual(value, responseHeaders[header]);
            }
        }
    }
}
