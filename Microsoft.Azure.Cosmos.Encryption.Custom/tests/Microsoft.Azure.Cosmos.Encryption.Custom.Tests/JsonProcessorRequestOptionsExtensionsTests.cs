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
        public void TryReadOverride_NullRequestOptions_Default()
        {
            RequestOptions roNull = null;
            bool found = roNull.TryReadJsonProcessorOverride(out JsonProcessor jp);
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
        public void GetJsonProcessor_NullRequestOptions_ReturnsSuppliedDefault()
        {
            RequestOptions roNull = null;
            JsonProcessor result = roNull.GetJsonProcessor(JsonProcessor.Stream);
            Assert.AreEqual(JsonProcessor.Stream, result);
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
        public void SelectAndSanitize_UsesCurrentValueWithoutMutatingCaller()
        {
            EncryptionItemRequestOptions requestOptions = new ()
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" },
                    { "unrelated", 123 },
                },
            };

            EncryptionItemRequestOptions sanitized = requestOptions.SelectAndSanitizeJsonProcessor(
                JsonProcessor.Newtonsoft,
                out JsonProcessor firstProcessor,
                out bool firstHasOverride);

            Assert.IsTrue(firstHasOverride);
            Assert.AreEqual(JsonProcessor.Stream, firstProcessor);
            Assert.AreNotSame(requestOptions, sanitized);
            Assert.IsTrue(requestOptions.Properties.ContainsKey(JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey));
            Assert.IsFalse(sanitized.Properties.ContainsKey(JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey));
            Assert.AreEqual(123, sanitized.Properties["unrelated"]);

            requestOptions.Properties = new Dictionary<string, object>
            {
                { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Newtonsoft" },
            };
            requestOptions.SelectAndSanitizeJsonProcessor(
                JsonProcessor.Stream,
                out JsonProcessor secondProcessor,
                out _);

            Assert.AreEqual(JsonProcessor.Newtonsoft, secondProcessor);
        }

        [TestMethod]
        public void SelectAndSanitize_NoHiddenProperty_ReturnsOriginalOptions()
        {
            ItemRequestOptions requestOptions = new ()
            {
                Properties = new Dictionary<string, object>
                {
                    { "unrelated", 123 },
                },
            };

            ItemRequestOptions sanitized = requestOptions.SelectAndSanitizeJsonProcessor(
                JsonProcessor.Stream,
                out JsonProcessor processor,
                out bool hasOverride);

            Assert.AreSame(requestOptions, sanitized);
            Assert.AreEqual(JsonProcessor.Stream, processor);
            Assert.IsFalse(hasOverride);
        }
    }
}
#endif
