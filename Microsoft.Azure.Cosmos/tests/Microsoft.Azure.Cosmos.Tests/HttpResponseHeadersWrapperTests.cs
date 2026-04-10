//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Reflection;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class HttpResponseHeadersWrapperTests
    {
        private const string Key = "testKey";

        public enum HeaderType
        {
            Headers,
            HttpResponseHeadersNameValueCollectionWrapperWithContentHeaders,
            HttpResponseHeadersNameValueCollectionWrapperNoContentHeaders,
            DictionaryNameValueCollection,
        }

        [TestMethod]
        [DataRow(HeaderType.Headers)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperWithContentHeaders)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperNoContentHeaders)]
        [DataRow(HeaderType.DictionaryNameValueCollection)]
        public void TestAddAndGetAndSet(HeaderType headerType)
        {
            string value1 = Guid.NewGuid().ToString();
            string value2 = Guid.NewGuid().ToString();
            INameValueCollection headers = this.CreateHeaders(headerType);
            headers.Add(Key, value1);

            Assert.AreEqual(value1, headers.Get(Key));
            headers.Set(Key, value2);
            Assert.AreEqual(value2, headers.Get(Key));
        }

        [TestMethod]
        [DataRow(HeaderType.Headers)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperWithContentHeaders)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperNoContentHeaders)]
        [DataRow(HeaderType.DictionaryNameValueCollection)]
        public void TestIndexer(HeaderType headerType)
        {
            INameValueCollection headers = this.CreateHeaders(headerType);
            string value = Guid.NewGuid().ToString();
            headers[Key] = value;
            Assert.AreEqual(value, headers[Key]);
        }

        [TestMethod]
        [DataRow(HeaderType.Headers)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperWithContentHeaders)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperNoContentHeaders)]
        [DataRow(HeaderType.DictionaryNameValueCollection)]
        public void TestRemove(HeaderType headerType)
        {
            INameValueCollection headers = this.CreateHeaders(headerType);
            string value = Guid.NewGuid().ToString();
            headers[Key] = value;
            Assert.AreEqual(value, headers[Key]);
            headers.Remove(Key);
            Assert.IsNull(headers[Key]);
        }

        [TestMethod]
        [DataRow(HeaderType.Headers)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperWithContentHeaders)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperNoContentHeaders)]
        [DataRow(HeaderType.DictionaryNameValueCollection)]
        public void TestClear(HeaderType headerType)
        {
            INameValueCollection headers = this.CreateHeaders(headerType);
            headers[Key] = Guid.NewGuid().ToString();
            headers.Clear();
            Assert.IsNull(headers[Key]);
        }

        [TestMethod]
        [DataRow(HeaderType.Headers)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperNoContentHeaders)]
        [DataRow(HeaderType.DictionaryNameValueCollection)]
        public void TestCount(HeaderType headerType)
        {
            INameValueCollection headers = this.CreateHeaders(headerType);
            headers[Key] = Guid.NewGuid().ToString();
            Assert.AreEqual(1, headers.Count());
        }

        [TestMethod]
        public void TestCountWithContentHeaders()
        {
            HttpResponseMessage responseMessage = new HttpResponseMessage()
            {
                Content = new StringContent("Test")
            };

            HttpResponseHeadersWrapper headers = new HttpResponseHeadersWrapper(
                responseMessage.Headers,
                responseMessage.Content?.Headers);

            Assert.AreEqual(1, headers.Count());
            Assert.AreEqual(1, headers.Keys().Count());
            string contentHeaderKey = headers.Keys().FirstOrDefault();
            string initialValue = headers[contentHeaderKey];
            Assert.IsNotNull(initialValue);
            headers[contentHeaderKey] = "newValue";

            Assert.AreEqual(1, headers.Count());
            Assert.AreEqual("newValue", headers[contentHeaderKey]);

            string randomValue = Guid.NewGuid().ToString();
            headers[Key] = randomValue;
            Assert.AreEqual(2, headers.Count());
            Assert.AreEqual(randomValue, headers[Key]);
            Assert.AreEqual("newValue", headers[contentHeaderKey]);

            headers.Remove(contentHeaderKey);
            headers.Remove(Key);
            Assert.AreEqual(0, headers.Count());
        }

        [TestMethod]
        [DataRow(HeaderType.Headers)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperWithContentHeaders)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperNoContentHeaders)]
        [DataRow(HeaderType.DictionaryNameValueCollection)]
        public void TestGetValues(HeaderType headerType)
        {
            INameValueCollection headers = this.CreateHeaders(headerType);
            string value1 = Guid.NewGuid().ToString();
            headers.Add(Key, value1);
            IEnumerable<string> values = headers.GetValues(Key);
            Assert.AreEqual(1, values.Count());
        }

        [TestMethod]
        [DataRow(HeaderType.Headers)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperWithContentHeaders)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperNoContentHeaders)]
        [DataRow(HeaderType.DictionaryNameValueCollection)]
        public void TestAllKeys(HeaderType headerType)
        {
            INameValueCollection headers = this.CreateHeaders(headerType);
            headers[Key] = Guid.NewGuid().ToString();
            Assert.AreEqual(Key, headers.AllKeys().First());
        }

        [TestMethod]
        [DataRow(HeaderType.Headers)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperWithContentHeaders)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperNoContentHeaders)]
        [DataRow(HeaderType.DictionaryNameValueCollection)]
        public void TestGetIEnumerableKeys(HeaderType headerType)
        {
            INameValueCollection headers = this.CreateHeaders(headerType);
            string value = Guid.NewGuid().ToString();
            headers[Key] = value;
            foreach (string header in headers)
            {
                Assert.AreEqual(value, headers[header]);
                return;
            }

            IEnumerator<string> keys = (IEnumerator<string>)headers.GetEnumerator();
            Assert.IsNull(keys.Current);
            Assert.IsTrue(keys.MoveNext());
            Assert.AreEqual(Key, keys.Current);
            Assert.IsFalse(keys.MoveNext());
        }

        [TestMethod]
        [DataRow(HeaderType.Headers)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperWithContentHeaders)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperNoContentHeaders)]
        [DataRow(HeaderType.DictionaryNameValueCollection)]
        public void TestSetAndGetKnownProperties(HeaderType headerType)
        {
            string value1 = Guid.NewGuid().ToString();
            string value2 = Guid.NewGuid().ToString();
            string value3 = Guid.NewGuid().ToString();
            INameValueCollection headers = this.CreateHeaders(headerType);

            headers.Add(HttpConstants.HttpHeaders.Continuation, value1);
            headers.Add(HttpConstants.HttpHeaders.PartitionKey, value2);
            headers.Add(WFConstants.BackendHeaders.PartitionKeyRangeId, value3);

            Assert.AreEqual(value1, headers[HttpConstants.HttpHeaders.Continuation]);
            Assert.AreEqual(value2, headers[HttpConstants.HttpHeaders.PartitionKey]);
            Assert.AreEqual(value3, headers[WFConstants.BackendHeaders.PartitionKeyRangeId]);
            value1 = Guid.NewGuid().ToString();
            value2 = Guid.NewGuid().ToString();
            value3 = Guid.NewGuid().ToString();
            headers[HttpConstants.HttpHeaders.Continuation] = value1;
            headers[HttpConstants.HttpHeaders.PartitionKey] = value2;
            headers[WFConstants.BackendHeaders.PartitionKeyRangeId] = value3;
            Assert.AreEqual(value1, headers[HttpConstants.HttpHeaders.Continuation]);
            Assert.AreEqual(value2, headers[HttpConstants.HttpHeaders.PartitionKey]);
            Assert.AreEqual(value3, headers[WFConstants.BackendHeaders.PartitionKeyRangeId]);
        }

        [TestMethod]
        [DataRow(HeaderType.Headers)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperWithContentHeaders)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperNoContentHeaders)]
        [DataRow(HeaderType.DictionaryNameValueCollection)]
        public void TestClearWithKnownProperties(HeaderType headerType)
        {
            INameValueCollection headers = this.CreateHeaders(headerType);
            headers[Key] = Guid.NewGuid().ToString();
            headers.Add(HttpConstants.HttpHeaders.PartitionKey, Guid.NewGuid().ToString());
            headers.Add(HttpConstants.HttpHeaders.Continuation, Guid.NewGuid().ToString());
            headers[HttpConstants.HttpHeaders.RetryAfterInMilliseconds] = "20";
            headers.Clear();
            Assert.IsNull(headers[Key]);
            Assert.IsNull(headers[HttpConstants.HttpHeaders.PartitionKey]);
            Assert.IsNull(headers[HttpConstants.HttpHeaders.Continuation]);
            Assert.IsNull(headers[HttpConstants.HttpHeaders.RetryAfterInMilliseconds]);
        }

        [TestMethod]
        [DataRow(HeaderType.Headers)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperWithContentHeaders)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperNoContentHeaders)]
        [DataRow(HeaderType.DictionaryNameValueCollection)]
        public void TestAllKeysWithKnownProperties(HeaderType headerType)
        {
            INameValueCollection headers = this.CreateHeaders(headerType);
            headers[Key] = Guid.NewGuid().ToString();
            headers.Add(HttpConstants.HttpHeaders.Continuation, Guid.NewGuid().ToString());
            headers[HttpConstants.HttpHeaders.RetryAfterInMilliseconds] = "20";
            headers.Add(WFConstants.BackendHeaders.SubStatus, "1002");
            headers.Add(HttpConstants.HttpHeaders.PartitionKey, Guid.NewGuid().ToString());
            string[] allKeys = headers.AllKeys();
            Assert.IsTrue(allKeys.Contains(Key));
            Assert.IsTrue(allKeys.Contains(HttpConstants.HttpHeaders.PartitionKey));
            Assert.IsTrue(allKeys.Contains(HttpConstants.HttpHeaders.RetryAfterInMilliseconds));
            Assert.IsTrue(allKeys.Contains(HttpConstants.HttpHeaders.Continuation));
            Assert.IsTrue(allKeys.Contains(WFConstants.BackendHeaders.SubStatus));
        }


        [TestMethod]
        [DataRow(HeaderType.Headers)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperWithContentHeaders)]
        [DataRow(HeaderType.HttpResponseHeadersNameValueCollectionWrapperNoContentHeaders)]
        [DataRow(HeaderType.DictionaryNameValueCollection)]
        public void VerifyUnKnownHeader(HeaderType headerType)
        {
            INameValueCollection headers = this.CreateHeaders(headerType);
            string key = Guid.NewGuid().ToString();
            string value = Guid.NewGuid().ToString();
            headers[key] = value;
            Assert.AreEqual(value, headers[key]);
            Assert.AreEqual(value, headers[key.ToLower()]);
            Assert.AreEqual(value, headers[key.ToUpper()]);
            Assert.AreEqual(value, headers.Get(key));
            Assert.AreEqual(value, headers.Get(key.ToLower()));
            Assert.AreEqual(value, headers.Get(key.ToUpper()));
            Assert.AreEqual(key, headers.Keys().First());

            headers.Remove(key);
            Assert.IsNull(headers[key]);
        }

        private INameValueCollection CreateHeaders(HeaderType headerType)
        {
            switch (headerType)
            {
                case HeaderType.Headers:
                    return new Headers().CosmosMessageHeaders.INameValueCollection;
                case HeaderType.HttpResponseHeadersNameValueCollectionWrapperNoContentHeaders:
                    HttpResponseMessage responseMessage = new HttpResponseMessage();
                    return new HttpResponseHeadersWrapper(
                        responseMessage.Headers,
                        null);
                case HeaderType.HttpResponseHeadersNameValueCollectionWrapperWithContentHeaders:
                    HttpResponseMessage responseMessageWithContent = new HttpResponseMessage()
                    {
                        Content = new StringContent("testscontent")
                    };

                    return new HttpResponseHeadersWrapper(
                        responseMessageWithContent.Headers,
                        responseMessageWithContent.Content?.Headers);
                case HeaderType.DictionaryNameValueCollection:
                    return new Headers(new DictionaryNameValueCollection()).CosmosMessageHeaders.INameValueCollection;
                default:
                    throw new ArgumentException("Invalid header type");
            }
        }
    }
}