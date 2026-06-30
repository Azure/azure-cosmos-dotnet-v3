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
        public void GetJsonProcessor_NoOverride_ReturnsContainerDefault()
        {
            RequestOptions ro = new ItemRequestOptions(); // no per-request override
            Assert.AreEqual(JsonProcessor.Stream, ro.GetJsonProcessor(JsonProcessor.Stream));
        }

        [TestMethod]
        public void GetJsonProcessor_NoOverride_NoDefault_ReturnsNewtonsoft()
        {
            RequestOptions ro = new ItemRequestOptions(); // no per-request override, no explicit default
            Assert.AreEqual(JsonProcessor.Newtonsoft, ro.GetJsonProcessor());
        }

        [TestMethod]
        public void GetJsonProcessor_PerRequestOverride_WinsOverContainerDefault()
        {
            RequestOptions streamOverride = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Stream" }
                }
            };

            // Per-request Stream override wins even though the container default is Newtonsoft.
            Assert.AreEqual(JsonProcessor.Stream, streamOverride.GetJsonProcessor(JsonProcessor.Newtonsoft));

            RequestOptions newtonsoftOverride = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, "Newtonsoft" }
                }
            };

            // Per-request Newtonsoft override wins even though the container default is Stream.
            Assert.AreEqual(JsonProcessor.Newtonsoft, newtonsoftOverride.GetJsonProcessor(JsonProcessor.Stream));
        }
    }
}
#endif
