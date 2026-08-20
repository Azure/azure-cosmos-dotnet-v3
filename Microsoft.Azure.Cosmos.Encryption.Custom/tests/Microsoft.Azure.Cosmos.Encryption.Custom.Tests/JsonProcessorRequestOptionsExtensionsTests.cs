// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonProcessorRequestOptionsExtensionsTests
    {
        [TestMethod]
        public void TryReadOverride_EnumValue_Succeeds()
        {
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" }
                }
            };

            bool found = ro.TryReadJsonProcessorOverride(out JsonProcessor jp);
            Assert.IsTrue(found);
            Assert.AreEqual(JsonProcessor.Stream, jp);
        }

        [TestMethod]
        public void TryReadOverride_StringValue_CaseInsensitive_Succeeds()
        {
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "sTrEaM" }
                }
            };

            bool found = ro.TryReadJsonProcessorOverride(out JsonProcessor jp);
            Assert.IsTrue(found);
            Assert.AreEqual(JsonProcessor.Stream, jp);
        }

        [TestMethod]
        public void TryReadOverride_StringValue_ExactMatch_Succeeds()
        {
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" }
                }
            };

            bool found = ro.TryReadJsonProcessorOverride(out JsonProcessor jp);
            Assert.IsTrue(found);
            Assert.AreEqual(JsonProcessor.Stream, jp);
        }

        [TestMethod]
        public void TryReadOverride_InvalidString_Ignored()
        {
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "invalid" }
                }
            };

            bool found = ro.TryReadJsonProcessorOverride(out JsonProcessor jp);
            Assert.IsFalse(found);
            Assert.AreEqual(JsonProcessor.Newtonsoft, jp);
        }

        [TestMethod]
        public void TryReadOverride_UndefinedNumericString_Ignored()
        {
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "999" }
                }
            };

            bool found = ro.TryReadJsonProcessorOverride(out JsonProcessor jp);
            Assert.IsFalse(found);
            Assert.AreEqual(JsonProcessor.Newtonsoft, jp);
        }

        [TestMethod]
        public void TryReadOverride_NullRequestOptions_Default()
        {
            RequestOptions roNull = null;
            bool found = roNull.TryReadJsonProcessorOverride(out JsonProcessor jp);
            Assert.IsFalse(found);
            Assert.AreEqual(JsonProcessor.Newtonsoft, jp);
        }

        [TestMethod]
        public void TryReadOverride_NoPropertiesDictionary_Default()
        {
            RequestOptions ro = new ItemRequestOptions(); // Properties remains null
            bool found = ro.TryReadJsonProcessorOverride(out JsonProcessor jp);
            Assert.IsFalse(found);
            Assert.AreEqual(JsonProcessor.Newtonsoft, jp);
        }

        [TestMethod]
        public void TryReadOverride_MixedCaseKey_NotRecognized()
        {
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    // Intentionally different casing pattern; should not match for perf (no ToUpper/ToLower)
                    { "Encryption-Json-Processor", "Stream" }
                }
            };

            bool found = ro.TryReadJsonProcessorOverride(out JsonProcessor jp);
            Assert.IsFalse(found);
            Assert.AreEqual(JsonProcessor.Newtonsoft, jp);
        }

        [TestMethod]
        public void TryReadOverride_MismatchedKey_NotRecognized()
        {
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey + "-extra", "Stream" }
                }
            };

            bool found = ro.TryReadJsonProcessorOverride(out JsonProcessor jp);
            Assert.IsFalse(found);
            Assert.AreEqual(JsonProcessor.Newtonsoft, jp);
        }

        [TestMethod]
        public void GetJsonProcessor_NullRequestOptions_ReturnsSuppliedDefault()
        {
            RequestOptions roNull = null;
            JsonProcessor result = roNull.GetJsonProcessor(JsonProcessor.Stream);
            Assert.AreEqual(JsonProcessor.Stream, result);
        }

        [TestMethod]
        public void GetJsonProcessor_NullRequestOptions_NoArg_ReturnsNewtonsoft()
        {
            RequestOptions roNull = null;
            JsonProcessor result = roNull.GetJsonProcessor();
            Assert.AreEqual(JsonProcessor.Newtonsoft, result);
        }

        [TestMethod]
        public void GetJsonProcessor_OverridePresent_ReturnsOverride_IgnoresDefault()
        {
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream }
                }
            };

            JsonProcessor result = ro.GetJsonProcessor(JsonProcessor.Newtonsoft);
            Assert.AreEqual(JsonProcessor.Stream, result);
        }

        [TestMethod]
        public void GetJsonProcessor_NoOverride_ReturnsSuppliedDefault()
        {
            RequestOptions ro = new ItemRequestOptions(); // Properties remains null
            JsonProcessor result = ro.GetJsonProcessor(JsonProcessor.Stream);
            Assert.AreEqual(JsonProcessor.Stream, result);
        }

        [TestMethod]
        public void GetJsonProcessor_EncryptionItemOptions_CachesOverrideAndRemovesProperty()
        {
            EncryptionItemRequestOptions requestOptions = new ()
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" },
                    { "unrelated", 123 },
                },
            };

            Assert.AreEqual(JsonProcessor.Stream, requestOptions.GetJsonProcessor(JsonProcessor.Newtonsoft));
            Assert.AreEqual(JsonProcessor.Stream, requestOptions.GetJsonProcessor(JsonProcessor.Newtonsoft));
            Assert.IsFalse(requestOptions.Properties.ContainsKey(JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey));
            Assert.AreEqual(123, requestOptions.Properties["unrelated"]);
        }

        [TestMethod]
        public void GetJsonProcessor_EncryptionBatchItemOptions_CachesOverrideAndRemovesProperty()
        {
            EncryptionTransactionalBatchItemRequestOptions requestOptions = new ()
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" },
                },
            };

            Assert.AreEqual(JsonProcessor.Stream, requestOptions.GetJsonProcessor(JsonProcessor.Newtonsoft));
            Assert.AreEqual(JsonProcessor.Stream, requestOptions.GetJsonProcessor(JsonProcessor.Newtonsoft));
            Assert.IsNull(requestOptions.Properties);
        }
    }
}
#endif
