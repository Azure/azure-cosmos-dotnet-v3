//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Documents.RntbdConstants;

    [TestClass]
    public class StoreResponseNameValueCollectionTests
    {
        [Owner("visridha")]
        [TestMethod]
        public void HeaderCasingTest()
        {
            StoreResponseNameValueCollection collection = new StoreResponseNameValueCollection
            {
                { "key", "value" }
            };

            Assert.AreEqual("value", collection["key"]);
            Assert.AreEqual("value", collection["Key"]);
            Assert.AreEqual("value", collection["KEy"]);
            Assert.AreEqual("value", collection["kEy"]);
        }

        [Owner("visridha")]
        [TestMethod]
        public void GetAndSetTest()
        {
            StoreResponseNameValueCollection collection = new StoreResponseNameValueCollection
            {
                ["key"] = "value"
            };
            Assert.AreEqual("value", collection.Get("key"));
            CollectionAssert.AreEqual(new string[] { "value" }, collection.GetValues("key"));

            Assert.IsNull(collection["someKey"]);
            Assert.IsNull(collection.Get("someKey"));
            Assert.IsNull(collection.GetValues("someKey"));

            collection.Set("key", "value2");
            Assert.AreEqual("value2", collection.Get("key"));
            CollectionAssert.AreEqual(new string[] { "value2" }, collection.GetValues("key"));
        }

        [Owner("visridha")]
        [TestMethod]
        public void InvalidKeyValuesRejectedTest()
        {
            StoreResponseNameValueCollection collection = new StoreResponseNameValueCollection
            {
                { "key", "value" }
            };
            try
            {
                collection.Add("key", "value2");
                Assert.Fail("Adding duplicate value should throw");
            }
            catch (ArgumentException)
            {
            }

            try
            {
                collection.Add(null, "value");
                Assert.Fail("SHould not be able to add null key");
            }
            catch (ArgumentNullException)
            {
            }

            try
            {
                collection.Add("somekey", null);
                Assert.Fail("SHould not be able to add null value");
            }
            catch (ArgumentNullException)
            {
            }

            try
            {
                collection.Set(null, "value");
                Assert.Fail("SHould not be able to add null key");
            }
            catch (ArgumentNullException)
            {
            }

            try
            {
                collection.Set("somekey", null);
                Assert.Fail("SHould not be able to add null value");
            }
            catch (ArgumentNullException)
            {
            }
        }

        [Owner("visridha")]
        [TestMethod]
        public void TestToNameValueCollectionReturnsSameValue()
        {
            // this mirrors a buggy behavior in the BE gateway.
            StoreResponseNameValueCollection collection = new StoreResponseNameValueCollection();
            collection.Set("Key", "Value");

            NameValueCollection nvc = collection.ToNameValueCollection();
            nvc.Add("someKey", "newValue");

            // now ask again.
            NameValueCollection nvc2 = collection.ToNameValueCollection();
            Assert.AreSame(nvc, nvc2);
            Assert.AreEqual("newValue", nvc.Get("someKey"));
        }

        [Owner("dikshibahl")]
        [TestMethod]
        public void VerifyUnKnownHeader()
        {
            StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
            Assert.AreEqual(0, headers.Keys().Count());
            string key = Guid.NewGuid().ToString();
            string value = Guid.NewGuid().ToString();
            headers[key] = value;
            Assert.AreEqual(value, headers[key]);
            Assert.AreEqual(value, headers.Get(key));
            Assert.AreEqual(key, headers.Keys().First());
            NameValueCollection nameValue = headers.ToNameValueCollection();
            Assert.AreEqual(value, nameValue.Get(key));

            headers.Remove(key);
            Assert.AreEqual(0, headers.Keys().Count());
            Assert.IsNull(headers[key]);
        }

        [Owner("dikshibahl")]
        [TestMethod]
        public void VerifyAllRntbdHeadersHaveKnownProperty()
        {
            HashSet<string> rntbdResponseHeaders = new();
            foreach (ResponseIdentifiers responseIdentifier in Enum.GetValues(typeof(ResponseIdentifiers)))
            {
                rntbdResponseHeaders.Add(responseIdentifier.ToString());
            }

            // Is not set on transport serialization
            rntbdResponseHeaders.Remove("ReadsPerformed");
            rntbdResponseHeaders.Remove("WritesPerformed");
            rntbdResponseHeaders.Remove("QueriesPerformed");
            rntbdResponseHeaders.Remove("IndexTermsGenerated");
            rntbdResponseHeaders.Remove("ScriptsExecuted");
            rntbdResponseHeaders.Remove("PayloadPresent");

            // The Rntbd names don't always match StoreResponseNameValueCollection properties
            // This changes the Rntbd name to match the expected name.
            rntbdResponseHeaders.Remove("ContinuationToken");
            rntbdResponseHeaders.Add("Continuation");

            rntbdResponseHeaders.Remove("LastStateChangeDateTime");
            rntbdResponseHeaders.Add("LastStateChangeUtc");

            rntbdResponseHeaders.Remove("RetryAfterMilliseconds");
            rntbdResponseHeaders.Add("RetryAfterInMilliseconds");

            rntbdResponseHeaders.Remove("StorageMaxResoureQuota");
            rntbdResponseHeaders.Add("MaxResourceQuota");

            rntbdResponseHeaders.Remove("StorageResourceQuotaUsage");
            rntbdResponseHeaders.Add("CurrentResourceQuotaUsage");

            rntbdResponseHeaders.Remove("CollectionUpdateProgress");
            rntbdResponseHeaders.Add("CollectionIndexTransformationProgress");

            rntbdResponseHeaders.Remove("CollectionLazyIndexProgress");
            rntbdResponseHeaders.Add("CollectionLazyIndexingProgress");

            rntbdResponseHeaders.Remove("ServerDateTimeUtc");
            rntbdResponseHeaders.Add("XDate");

            rntbdResponseHeaders.Remove("XpConfigurationSesssionsCount");
            rntbdResponseHeaders.Add("XPConfigurationSessionsCount");

            rntbdResponseHeaders.Remove("UnflushedMergeLogEntryCount");
            rntbdResponseHeaders.Add("UnflushedMergLogEntryCount");

            rntbdResponseHeaders.Remove("ResourceName");
            rntbdResponseHeaders.Add("ResourceId");

            rntbdResponseHeaders.Remove("XpRole");
            rntbdResponseHeaders.Add("XPRole");

            rntbdResponseHeaders.Remove("ReindexerProgress");
            rntbdResponseHeaders.Add("ReIndexerProgress");

            rntbdResponseHeaders.Remove("BYOKEncryptionProgress");
            rntbdResponseHeaders.Add("ByokEncryptionProgress");

            rntbdResponseHeaders.Remove("OfferRestorePending");
            rntbdResponseHeaders.Add("IsOfferRestorePending");

            HashSet<string> optimizedResponseHeaders = new HashSet<string>(typeof(StoreResponseNameValueCollection).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(x => x.Name));

            foreach (string name in rntbdResponseHeaders)
            {
                Assert.IsTrue(optimizedResponseHeaders.Contains(name), $"{nameof(StoreResponseNameValueCollection)} is missing RntbdResponse property {name}");
            }
        }

        [Owner("dikshibahl")]
        [TestMethod]
        public void VerifyAllKnownProperties()
        {
            Dictionary<string, string> httpHeadersMap = typeof(HttpConstants.HttpHeaders).GetFields(BindingFlags.Public | BindingFlags.Static)
                .ToDictionary(x => x.Name, x => (string)x.GetValue(null));
            Dictionary<string, string> backendHeadersMap = typeof(WFConstants.BackendHeaders).GetFields(BindingFlags.Public | BindingFlags.Static)
                .ToDictionary(x => x.Name, x => (string)x.GetValue(null));

            PropertyInfo[] optimizedResponseHeaders = typeof(StoreResponseNameValueCollection).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => !string.Equals("Item", x.Name)).ToArray();

            StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
            foreach (PropertyInfo propertyInfo in optimizedResponseHeaders)
            {
                Assert.AreEqual(0, headers.Count());
                Assert.AreEqual(0, headers.Keys().Count());

                // Test property first
                string value = Guid.NewGuid().ToString();
                propertyInfo.SetValue(headers, value);
                Assert.AreEqual(value, propertyInfo.GetValue(headers));

                if (!httpHeadersMap.TryGetValue(propertyInfo.Name, out string key))
                {
                    if (!backendHeadersMap.TryGetValue(propertyInfo.Name, out key))
                    {
                        Assert.Fail($"The property name {propertyInfo.Name} should match a header constant name");
                    }
                }

                Assert.AreEqual(1, headers.Count());
                Assert.AreEqual(1, headers.Keys().Count());
                Assert.AreEqual(key, headers.Keys().First());
                Assert.AreEqual(value, headers.Get(key));

                // Reset the value back to null
                propertyInfo.SetValue(headers, null);

                Assert.AreEqual(0, headers.Count());
                Assert.AreEqual(0, headers.Keys().Count());

                // Check adding via the interface sets the property correctly
                headers.Add(key, value);
                Assert.AreEqual(value, propertyInfo.GetValue(headers));
                Assert.AreEqual(value, headers.Get(key));

                Assert.AreEqual(1, headers.Count());
                Assert.AreEqual(1, headers.Keys().Count());
                Assert.AreEqual(key, headers.Keys().First());
                Assert.AreEqual(value, headers.Get(key));

                // Check setting via the interface sets the property correctly
                value = Guid.NewGuid().ToString();
                headers.Set(key, value);
                Assert.AreEqual(value, propertyInfo.GetValue(headers));
                Assert.AreEqual(value, headers.Get(key));

                Assert.AreEqual(1, headers.Count());
                Assert.AreEqual(1, headers.Keys().Count());
                Assert.AreEqual(key, headers.Keys().First());
                Assert.AreEqual(value, headers.Get(key));

                // Check setting via the interface sets the property correctly
                headers.Remove(key);
                Assert.AreEqual(null, propertyInfo.GetValue(headers));
                Assert.AreEqual(null, headers.Get(key));

                Assert.AreEqual(0, headers.Count());
                Assert.AreEqual(0, headers.Keys().Count());
            }
        }
    }
}