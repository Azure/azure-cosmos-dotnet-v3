//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class HeadersTests
    {
        private const string Key = "testKey";

        [TestMethod]
        public void HeaderResponseGenerateTest()
        {
            
            Dictionary<string, string> httpHeaders = typeof(HttpConstants.HttpHeaders).GetFields(BindingFlags.Public | BindingFlags.Static)
                .ToDictionary(x => x.Name, x => (string)x.GetValue(null));
            Dictionary<string, string> backendHeaders = typeof(WFConstants.BackendHeaders).GetFields(BindingFlags.Public | BindingFlags.Static)
                .ToDictionary(x => x.Name, x => (string)x.GetValue(null));

            string output = "List<(string name, string value)> headerNames = new List<(string name, string value)>() {" + Environment.NewLine;
            HashSet<string> responseHeaders = typeof(RntbdConstants.Response).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => !string.Equals("payloadPresent", x.Name))
                .Select(x => char.ToUpper(x.Name.First()) + x.Name.Substring(1))
                .ToHashSet();
            responseHeaders.Remove("LastStateChangeDateTime");
            responseHeaders.Add("LastStateChangeUtc");
            responseHeaders.Remove("RetryAfterMilliseconds");
            responseHeaders.Add("RetryAfterInMilliseconds");

            responseHeaders.Remove("StorageMaxResoureQuota");
            responseHeaders.Add("MaxResourceQuota");

            responseHeaders.Remove("StorageResourceQuotaUsage");
            responseHeaders.Add("CurrentResourceQuotaUsage");

            responseHeaders.Remove("CollectionUpdateProgress");
            responseHeaders.Add("CollectionIndexTransformationProgress");

            responseHeaders.Remove("CollectionLazyIndexProgress");
            responseHeaders.Add("CollectionLazyIndexingProgress");

            responseHeaders.Remove("ServerDateTimeUtc");
            responseHeaders.Add("XDate");

            responseHeaders.Remove("XpConfigurationSesssionsCount");
            responseHeaders.Add("XPConfigurationSessionsCount");

            responseHeaders.Remove("UnflushedMergeLogEntryCount");
            responseHeaders.Add("UnflushedMergLogEntryCount");

            responseHeaders.Remove("ResourceName");
            responseHeaders.Add("ResourceId");

            responseHeaders.Remove("XpRole");
            responseHeaders.Add("XPRole");

            // Is not set on transport serialization
            responseHeaders.Remove("ReadsPerformed");
            responseHeaders.Remove("WritesPerformed");
            responseHeaders.Remove("QueriesPerformed");
            responseHeaders.Remove("IndexTermsGenerated");
            responseHeaders.Remove("ScriptsExecuted");

            foreach (string name in responseHeaders)
            {
                if (httpHeaders.ContainsKey(name))
                {
                    output += $"    (\"HttpConstants.HttpHeaders.{name}\", \"{httpHeaders[name]}\"),{Environment.NewLine}";
                }
                else if (backendHeaders.ContainsKey(name))
                {
                    output += $"    (\"WFConstants.BackendHeaders.{name}\", \"{backendHeaders[name]}\"),{Environment.NewLine}";
                }
                else
                {
                    throw new Exception(name);
                }
                
            }

            output += "}; #>";

            Assert.IsNotNull(output);
        }

        [TestMethod]
        public void HeaderGenerateTest()
        {
            FieldInfo[] allHeaderConstants = typeof(HttpConstants.HttpHeaders).GetFields(BindingFlags.Public | BindingFlags.Static);
            Dictionary<string, string> allHeadersFromValueToName = allHeaderConstants.ToDictionary(x => (string)x.GetValue(null), x => "HttpConstants.HttpHeaders." + x.Name);
            FieldInfo[] allBackendHeaderConstants = typeof(WFConstants.BackendHeaders).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach(var backendHeader in allBackendHeaderConstants)
            {
                allHeadersFromValueToName[(string)backendHeader.GetValue(null)] = "WFConstants.BackendHeaders." + backendHeader.Name;
            }

            FieldInfo[] allRequestHeaders = typeof(RntbdConstants.Request).GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(x => x.Name.Length).ToArray();
            StringBuilder requestBuilder = new StringBuilder();

            requestBuilder.Append("<# List<(string name, string value)> headerNames = new List<(string name, string value)>()");
            requestBuilder.Append(Environment.NewLine + "{" + Environment.NewLine);
            List<(string name, string value)> headerNames = new List<(string name, string value)>();
            foreach (KeyValuePair<string, Action<object, DocumentServiceRequest, RntbdConstants.Request>> fieldInfo in TransportSerialization.AddHeaders)
            {
                string name = allHeadersFromValueToName[fieldInfo.Key];
                headerNames.Add((name, fieldInfo.Key));
            }

            headerNames = headerNames.OrderBy(x => x.name).ToList();
            foreach ((string name, string value) in headerNames)
            {
                requestBuilder.Append($"    (\"{name}\", \"{value}\"),{Environment.NewLine}");
            }

            requestBuilder.Append(Environment.NewLine + "}" + Environment.NewLine);
            string headers = requestBuilder.ToString();

            List<string> sortedHeaderPropertyNames = headerNames.Select(x => x.name.Split('.').Last()).OrderBy(x => x).ToList();

            IEnumerable <IGrouping<int, (string, string)>> groupByLength = headerNames.GroupBy(x => x.value.Length).OrderBy(x => x.Key);
            foreach(IGrouping<int, string> group in groupByLength)
            {
                foreach(string name in group)
                {
                    
                    //if(object.ReferenceEquals(name, HttpConstants.HttpHeaders.Continuation))
                    //{
                    //    return this.TestGetToNameValueCollection;
                    //}
                }
            }


            HashSet<string> mapRntbdFieldToHeaderKey = allHeaderConstants.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // Match custom rntbd field names to the http headers.
            //mapRntbdFieldToHeaderKey["AuthorizationToken"] = HttpConstants.HttpHeaders.Authorization;
            //mapRntbdFieldToHeaderKey["Date"] = HttpConstants.HttpHeaders.XDate;
            //mapRntbdFieldToHeaderKey["ContinuationToken"] = HttpConstants.HttpHeaders.Continuation;
            //mapRntbdFieldToHeaderKey["Match"] = HttpConstants.HttpHeaders.IfNoneMatch;
            //mapRntbdFieldToHeaderKey["IsFanout"] = WFConstants.BackendHeaders.IsFanoutRequest;
            //mapRntbdFieldToHeaderKey["ClientVersion"] = HttpConstants.HttpHeaders.Version;
            //mapRntbdFieldToHeaderKey["filterBySchemaRid"] = HttpConstants.HttpHeaders.FilterBySchemaResourceId;
            //mapRntbdFieldToHeaderKey["collectionChildResourceContentLengthLimitInKB"] = WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB;
            //mapRntbdFieldToHeaderKey["returnPreference"] = HttpConstants.HttpHeaders.Prefer;

            HashSet<string> mapBackend = allBackendHeaderConstants.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            StringBuilder builder = new StringBuilder();
            var propertyNames = allRequestHeaders.Select(x => x.Name);
            foreach (string name in propertyNames)
            {
                builder.Append("if (object.ReferenceEquals(key, ");
                if (mapRntbdFieldToHeaderKey.Contains(name))
                {
                    builder.Append($"HttpConstants.HttpHeaders.{name})){{");

                }
                else
                {
                    builder.Append($"WFConstants.BackendHeaders.{name})){{");
                }

                builder.Append($" this.{name} = value; return; }} {Environment.NewLine}");
            }

            string properties2 = builder.ToString();
            Assert.IsNotNull(properties2);

            {
                StringBuilder keysbuilder = new StringBuilder();
                foreach (string name in propertyNames)
                {
                    keysbuilder.Append($"if (this.{name} != null) {{ yield return ");
                    if (mapRntbdFieldToHeaderKey.Contains(name))
                    {
                        keysbuilder.Append($"HttpConstants.HttpHeaders.{name}; }}");

                    }
                    else
                    {
                        keysbuilder.Append($"WFConstants.BackendHeaders.{name}; }}");
                    }

                    keysbuilder.Append(Environment.NewLine);
                }

                string getIfStatements = keysbuilder.ToString();
                Assert.IsNotNull(getIfStatements);
            }

            {
                StringBuilder getbuilder = new StringBuilder();
                foreach (string name in propertyNames)
                {
                    getbuilder.Append("if (object.ReferenceEquals(key, ");
                    if (mapRntbdFieldToHeaderKey.Contains(name))
                    {
                        getbuilder.Append($"HttpConstants.HttpHeaders.{name})){{");

                    }
                    else
                    {
                        getbuilder.Append($"WFConstants.BackendHeaders.{name})){{");
                    }

                    getbuilder.Append($" return this.{name}; }} {Environment.NewLine}");
                }

                string getIfStatements = getbuilder.ToString();
                Assert.IsNotNull(getIfStatements);
            }


            {
                StringBuilder allKeys = new StringBuilder();
                foreach (string name in propertyNames)
                {
                    allKeys.Append($"if (this.{name} != null) {{ yield return this.{name}; }} {Environment.NewLine} ");
                }

                string getIfStatements = allKeys.ToString();
                Assert.IsNotNull(getIfStatements);
            }
        }


        [TestMethod]
        public void TestAddAndGetAndSet()
        {
            string value1 = Guid.NewGuid().ToString();
            string value2 = Guid.NewGuid().ToString();
            Headers Headers = new Headers();
            Headers.Add(Key, value1);
            Assert.AreEqual(value1, Headers.Get(Key));
            Headers.Set(Key, value2);
            Assert.AreEqual(value2, Headers.Get(Key));
        }

        [TestMethod]
        public void TestIndexer()
        {
            Headers Headers = new Headers();
            string value = Guid.NewGuid().ToString();
            Headers.CosmosMessageHeaders[Key] = value;
            Assert.AreEqual(value, Headers[Key]);
        }

        [TestMethod]
        public void TestRemove()
        {
            Headers Headers = new Headers();
            string value = Guid.NewGuid().ToString();
            Headers.CosmosMessageHeaders[Key] = value;
            Assert.AreEqual(value, Headers[Key]);
            Headers.Remove(Key);
            Assert.IsNull(Headers[Key]);
        }

        [TestMethod]
        public void TestClear()
        {
            Headers Headers = new Headers();
            Headers.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            Headers.CosmosMessageHeaders.Clear();
            Assert.IsNull(Headers[Key]);
        }

        [TestMethod]
        public void TestCount()
        {
            Headers Headers = new Headers();
            Headers.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            Assert.AreEqual(1, Headers.CosmosMessageHeaders.Count());
        }

        [TestMethod]
        public void TestGetValues()
        {
            Headers Headers = new Headers();
            string value1 = Guid.NewGuid().ToString();
            Headers.Add(Key, value1);
            IEnumerable<string> values = Headers.GetValues(Key);
            Assert.AreEqual(1, values.Count());
        }

        [TestMethod]
        public void TestAllKeys()
        {
            Headers Headers = new Headers();
            Headers.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            Assert.AreEqual(Key, Headers.AllKeys().First());
        }

        [TestMethod]
        public void TestGetIEnumerableKeys()
        {
            Headers Headers = new Headers();
            string value = Guid.NewGuid().ToString();
            Headers.CosmosMessageHeaders[Key] = value;
            foreach (string header in Headers)
            {
                Assert.AreEqual(value, Headers[header]);
                return;
            }

            IEnumerator<string> keys = Headers.GetEnumerator();
            Assert.IsNull(keys.Current);
            Assert.IsTrue(keys.MoveNext());
            Assert.AreEqual(Key, keys.Current);
            Assert.IsFalse(keys.MoveNext());
        }

        [TestMethod]
        [ExpectedException(typeof(NotImplementedException))]
        public void TestGetToNameValueCollection()
        {
            Headers Headers = new Headers();
            string value = Guid.NewGuid().ToString();
            Headers.CosmosMessageHeaders[Key] = value;
            NameValueCollection anotherCollection = Headers.CosmosMessageHeaders.ToNameValueCollection();
            Assert.AreEqual(value, anotherCollection[Key]);
        }

        [TestMethod]
        public void TestSetAndGetKnownProperties()
        {
            // Most commonly used in the Request
            {
                string value1 = Guid.NewGuid().ToString();
                string value2 = Guid.NewGuid().ToString();
                string value3 = Guid.NewGuid().ToString();
                string value4 = Guid.NewGuid().ToString();
                Headers Headers = new Headers();
                Headers.ContinuationToken = value1;
                Headers.PartitionKey = value2;
                Headers.PartitionKeyRangeId = value3;
                Assert.AreEqual(value1, Headers.ContinuationToken);
                Assert.AreEqual(value2, Headers.PartitionKey);
                Assert.AreEqual(value3, Headers.PartitionKeyRangeId);
                Assert.AreEqual(value1, Headers[HttpConstants.HttpHeaders.Continuation]);
                Assert.AreEqual(value2, Headers[HttpConstants.HttpHeaders.PartitionKey]);
                Assert.AreEqual(value3, Headers[WFConstants.BackendHeaders.PartitionKeyRangeId]);
                value1 = Guid.NewGuid().ToString();
                value2 = Guid.NewGuid().ToString();
                value3 = Guid.NewGuid().ToString();
                Headers.CosmosMessageHeaders[HttpConstants.HttpHeaders.Continuation] = value1;
                Headers.CosmosMessageHeaders[HttpConstants.HttpHeaders.PartitionKey] = value2;
                Headers.CosmosMessageHeaders[WFConstants.BackendHeaders.PartitionKeyRangeId] = value3;
                Assert.AreEqual(value1, Headers.ContinuationToken);
                Assert.AreEqual(value2, Headers.PartitionKey);
                Assert.AreEqual(value3, Headers.PartitionKeyRangeId);
                Assert.AreEqual(value1, Headers[HttpConstants.HttpHeaders.Continuation]);
                Assert.AreEqual(value2, Headers[HttpConstants.HttpHeaders.PartitionKey]);
                Assert.AreEqual(value3, Headers[WFConstants.BackendHeaders.PartitionKeyRangeId]);
            }

            // Most commonly used in the Response
            {
                string value1 = Guid.NewGuid().ToString();
                string value2 = "1002";
                string value3 = "20";
                string value4 = "someSession";
                Headers requestHeaders = new Headers();
                requestHeaders.CosmosMessageHeaders[HttpConstants.HttpHeaders.Continuation] = value1;
                requestHeaders.CosmosMessageHeaders[WFConstants.BackendHeaders.SubStatus] = value2;
                requestHeaders.CosmosMessageHeaders[HttpConstants.HttpHeaders.RetryAfterInMilliseconds] = value3;
                requestHeaders.CosmosMessageHeaders[HttpConstants.HttpHeaders.SessionToken] = value4;
                Assert.AreEqual(value1, requestHeaders.ContinuationToken);
                Assert.AreEqual(int.Parse(value2), (int)requestHeaders.SubStatusCode);
                Assert.AreEqual(TimeSpan.FromMilliseconds(20), requestHeaders.RetryAfter);
                Assert.AreEqual(value4, requestHeaders.Session);
                Assert.AreEqual(value1, requestHeaders[HttpConstants.HttpHeaders.Continuation]);
                Assert.AreEqual(value2, requestHeaders[WFConstants.BackendHeaders.SubStatus]);
                Assert.AreEqual(value3, requestHeaders[HttpConstants.HttpHeaders.RetryAfterInMilliseconds]);
                Assert.AreEqual(value4, requestHeaders[HttpConstants.HttpHeaders.SessionToken]);
            }
        }

        [TestMethod]
        public void TestClearWithKnownProperties()
        {
            Headers Headers = new Headers();
            Headers.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            Headers.PartitionKey = Guid.NewGuid().ToString();
            Headers.ContinuationToken = Guid.NewGuid().ToString();
            Headers.CosmosMessageHeaders[HttpConstants.HttpHeaders.RetryAfterInMilliseconds] = "20";
            Headers.CosmosMessageHeaders.Clear();
            Assert.IsNull(Headers[Key]);
            Assert.IsNull(Headers.PartitionKey);
            Assert.IsNull(Headers.ContinuationToken);
            Assert.IsNull(Headers.RetryAfter);
        }

        [TestMethod]
        public void TestCountWithKnownProperties()
        {
            Headers Headers = new Headers();
            Headers.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            Headers.PartitionKey = Guid.NewGuid().ToString();
            Headers.ContinuationToken = Guid.NewGuid().ToString();
            Headers.CosmosMessageHeaders[HttpConstants.HttpHeaders.RetryAfterInMilliseconds] = "20";
            Assert.AreEqual(4, Headers.CosmosMessageHeaders.Count());
        }

        [TestMethod]
        public void TestAllKeysWithKnownProperties()
        {
            Headers Headers = new Headers();
            Headers.CosmosMessageHeaders[Key] = Guid.NewGuid().ToString();
            Headers.ContinuationToken = Guid.NewGuid().ToString();
            Headers.CosmosMessageHeaders[HttpConstants.HttpHeaders.RetryAfterInMilliseconds] = "20";
            Headers.Add(WFConstants.BackendHeaders.SubStatus, "1002");
            Headers.PartitionKey = Guid.NewGuid().ToString();
            string[] allKeys = Headers.AllKeys();
            Assert.IsTrue(allKeys.Contains(Key));
            Assert.IsTrue(allKeys.Contains(HttpConstants.HttpHeaders.PartitionKey));
            Assert.IsTrue(allKeys.Contains(HttpConstants.HttpHeaders.RetryAfterInMilliseconds));
            Assert.IsTrue(allKeys.Contains(HttpConstants.HttpHeaders.Continuation));
            Assert.IsTrue(allKeys.Contains(WFConstants.BackendHeaders.SubStatus));
        }
    }
}
