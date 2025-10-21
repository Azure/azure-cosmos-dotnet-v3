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
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream }
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
        public void ResolveSelection_UnsupportedCombination_Throws()
        {
#pragma warning disable CS0618 // testing legacy path rejection for Stream processor
            EncryptionOptions opts = new()
            {
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                JsonProcessor = JsonProcessor.Stream
            };
#pragma warning restore CS0618
            RequestOptions ro = new ItemRequestOptions();

            Assert.ThrowsException<NotSupportedException>(() => ro.ResolveJsonProcessorSelection(opts));
        }

        [TestMethod]
        public void ResolveSelection_OverrideApplied()
        {
            EncryptionOptions opts = new()
            {
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                JsonProcessor = JsonProcessor.Newtonsoft
            };
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream }
                }
            };

            ro.ResolveJsonProcessorSelection(opts);
            Assert.AreEqual(JsonProcessor.Stream, opts.JsonProcessor);
        }

        [TestMethod]
        public void ResolveSelection_NoOverride_NoChange()
        {
            EncryptionOptions opts = new()
            {
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                JsonProcessor = JsonProcessor.Newtonsoft
            };
            RequestOptions ro = new ItemRequestOptions();

            ro.ResolveJsonProcessorSelection(opts);
            Assert.AreEqual(JsonProcessor.Newtonsoft, opts.JsonProcessor);
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
                    { "Encryption-Json-Processor", JsonProcessor.Stream }
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
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey + "-extra", JsonProcessor.Stream }
                }
            };

            bool found = ro.TryReadJsonProcessorOverride(out JsonProcessor jp);
            Assert.IsFalse(found);
            Assert.AreEqual(JsonProcessor.Newtonsoft, jp);
        }

        [TestMethod]
        public void GetJsonProcessor_UsesOverride()
        {
            RequestOptions requestOptions = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream }
                }
            };

            JsonProcessor result = requestOptions.GetJsonProcessor();
            Assert.AreEqual(JsonProcessor.Stream, result);
        }

        [TestMethod]
        public void GetJsonProcessor_NoOverride_DefaultNewtonsoft()
        {
            RequestOptions requestOptions = new ItemRequestOptions();

            JsonProcessor result = requestOptions.GetJsonProcessor();
            Assert.AreEqual(JsonProcessor.Newtonsoft, result);
        }

        [TestMethod]
        public void GetJsonProcessor_NoOverride_CustomDefault()
        {
            RequestOptions requestOptions = new ItemRequestOptions();

            JsonProcessor result = requestOptions.GetJsonProcessor(JsonProcessor.Stream);
            Assert.AreEqual(JsonProcessor.Stream, result);
        }

        [TestMethod]
        public void GetJsonProcessor_NullRequestOptions_UsesDefault()
        {
            RequestOptions requestOptions = null;

            JsonProcessor result = requestOptions.GetJsonProcessor(JsonProcessor.Stream);
            Assert.AreEqual(JsonProcessor.Stream, result);
        }
    }
}
#endif
