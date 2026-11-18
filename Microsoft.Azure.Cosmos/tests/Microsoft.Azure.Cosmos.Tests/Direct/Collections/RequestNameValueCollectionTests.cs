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

    [TestClass]
    public class RequestNameValueCollectionTestsTests
    {
        [Owner("dikshibahl")]
        [TestMethod]
        public void HeaderCasingTest()
        {
            RequestNameValueCollection collection = new RequestNameValueCollection
            {
                { "key", "value" }
            };

            Assert.AreEqual("value", collection["key"]);
            Assert.AreEqual("value", collection["Key"]);
            Assert.AreEqual("value", collection["KEy"]);
            Assert.AreEqual("value", collection["kEy"]);
        }

        [Owner("dikshibahl")]
        [TestMethod]
        public void GetAndSetTest()
        {
            RequestNameValueCollection collection = new RequestNameValueCollection
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

        [Owner("dikshibahl")]
        [TestMethod]
        public void InvalidKeyValuesRejectedTest()
        {
            RequestNameValueCollection collection = new RequestNameValueCollection
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
        }

        [Owner("dikshibahl")]
        [TestMethod]
        public void TestToNameValueCollectionReturnsSameValue()
        {
            // this mirrors a buggy behavior in the BE gateway.
            RequestNameValueCollection collection = new RequestNameValueCollection();
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
            RequestNameValueCollection headers = new RequestNameValueCollection();
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
            HashSet<string> rntbdRequestHeaders = new HashSet<string>(typeof(RntbdConstants.Request).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(x => char.ToUpper(x.Name.First()) + x.Name[1..]));

            rntbdRequestHeaders.Remove("AuthorizationToken");
            rntbdRequestHeaders.Add("Authorization");

            rntbdRequestHeaders.Remove("PayloadPresent");

            rntbdRequestHeaders.Remove("Date");
            rntbdRequestHeaders.Add("XDate");
            rntbdRequestHeaders.Add("HttpDate");

            rntbdRequestHeaders.Remove("Match");
            rntbdRequestHeaders.Add("IfNoneMatch");
            rntbdRequestHeaders.Add("IfMatch");

            rntbdRequestHeaders.Remove("IsFanout");
            rntbdRequestHeaders.Add("IsFanoutRequest");

            rntbdRequestHeaders.Remove("ReplicaPath");
            rntbdRequestHeaders.Remove("DatabaseName");
            rntbdRequestHeaders.Remove("CollectionName");
            rntbdRequestHeaders.Remove("DocumentName");
            rntbdRequestHeaders.Remove("AttachmentName");
            rntbdRequestHeaders.Remove("UserName");
            rntbdRequestHeaders.Remove("PermissionName");
            rntbdRequestHeaders.Remove("StoredProcedureName");
            rntbdRequestHeaders.Remove("UserDefinedFunctionName");
            rntbdRequestHeaders.Remove("TriggerName");
            rntbdRequestHeaders.Remove("ConflictName");
            rntbdRequestHeaders.Remove("PartitionKeyRangeName");
            rntbdRequestHeaders.Remove("SchemaName");
            rntbdRequestHeaders.Remove("UserDefinedTypeName");
            rntbdRequestHeaders.Remove("SnapshotName");
            rntbdRequestHeaders.Remove("ClientEncryptionKeyName");
            rntbdRequestHeaders.Remove("SystemDocumentName");
            rntbdRequestHeaders.Remove("RoleDefinitionName");
            rntbdRequestHeaders.Remove("RoleAssignmentName");
            rntbdRequestHeaders.Remove("InteropUserName");
            rntbdRequestHeaders.Remove("AuthPolicyElementName");
            rntbdRequestHeaders.Remove("AzureRbacName");
            rntbdRequestHeaders.Remove("EncryptionScopeName");
            rntbdRequestHeaders.Remove("GlobalDatabaseAccountName");
            rntbdRequestHeaders.Remove("RegionalDatabaseAccountName");
            rntbdRequestHeaders.Remove("StartEpkHash");
            rntbdRequestHeaders.Remove("EndEpkHash");

            rntbdRequestHeaders.Remove("ClientVersion");
            rntbdRequestHeaders.Add("Version");

            rntbdRequestHeaders.Remove("FilterBySchemaRid");
            rntbdRequestHeaders.Add("FilterBySchemaResourceId");

            rntbdRequestHeaders.Remove("ResponseContinuationTokenLimitInKb");
            rntbdRequestHeaders.Add("ResponseContinuationTokenLimitInKB");

            rntbdRequestHeaders.Remove("CollectionChildResourceContentLengthLimitInKB");
            rntbdRequestHeaders.Add("CollectionChildResourceContentLimitInKB");

            rntbdRequestHeaders.Remove("MergeCheckpointGLSNKeyName");
            rntbdRequestHeaders.Add("MergeCheckPointGLSN");

            rntbdRequestHeaders.Remove("ReturnPreference");
            rntbdRequestHeaders.Add("Prefer");

            rntbdRequestHeaders.Remove("IsofferStorageRefreshRequest");
            rntbdRequestHeaders.Add("IsOfferStorageRefreshRequest");

            rntbdRequestHeaders.Remove("SdkSupportedCapabilities");
            rntbdRequestHeaders.Add("SDKSupportedCapabilities");

            rntbdRequestHeaders.Remove("PopulateBYOKEncryptionProgress");
            rntbdRequestHeaders.Add("PopulateByokEncryptionProgress");

            rntbdRequestHeaders.Remove("ContinuationToken");
            rntbdRequestHeaders.Add("Continuation");

            rntbdRequestHeaders.Remove("CollectionSchemaId");
            rntbdRequestHeaders.Add("SchemaId");

            // SupportedSerializationFeatures is derived from parsing SupportedSerializationFormats; it is not its own HTTP header.
            rntbdRequestHeaders.Remove("SupportedSerializationFeatures");

            rntbdRequestHeaders.Remove("ClientIpAddress");
            rntbdRequestHeaders.Remove("IsRequestFromComputeNotAuthorized");
            rntbdRequestHeaders.Remove("StartEpkHash");
            rntbdRequestHeaders.Remove("EndEpkHash");

            rntbdRequestHeaders.Remove("DtcVersion");
            rntbdRequestHeaders.Remove("UserAgent");
            rntbdRequestHeaders.Remove("ContentType");
            rntbdRequestHeaders.Remove("IdempotencyToken");
            rntbdRequestHeaders.Remove("HybridLogicalClockTimestamp");
            rntbdRequestHeaders.Remove("DistributedTransactionId");

            HashSet<string> optimizedResponseHeaders = new HashSet<string>(typeof(RequestNameValueCollection).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(x => x.Name));

            foreach (string name in rntbdRequestHeaders)
            {
                Assert.IsTrue(optimizedResponseHeaders.Contains(name), $"{nameof(RequestNameValueCollection)} is missing RntbdRequest property {name}");
            }
        }

        [Owner("dikshibahl")]
        [TestMethod]
        public void VerifyAllKnownProperties()
        {
            Dictionary<string, string> httpHeadersMap = typeof(Documents.HttpConstants.HttpHeaders).GetFields(BindingFlags.Public | BindingFlags.Static)
                .ToDictionary(x => x.Name, x => (string)x.GetValue(null));
            Dictionary<string, string> backendHeadersMap = typeof(WFConstants.BackendHeaders).GetFields(BindingFlags.Public | BindingFlags.Static)
                .ToDictionary(x => x.Name, x => (string)x.GetValue(null));

            PropertyInfo[] optimizedResponseHeaders = typeof(RequestNameValueCollection).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => !string.Equals("Item", x.Name)).ToArray();

            RequestNameValueCollection headers = new RequestNameValueCollection();
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