// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonProcessorPropertyBagTests
    {
        [TestMethod]
        public void TryGetOverride_EnumValue_Succeeds()
        {
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorPropertyBag.JsonProcessorPropertyBagKey, JsonProcessor.Stream }
                }
            };

            bool found = JsonProcessorPropertyBag.TryGetJsonProcessorOverride(ro, out JsonProcessor jp);
            Assert.IsTrue(found);
            Assert.AreEqual(JsonProcessor.Stream, jp);
        }

        [TestMethod]
        public void TryGetOverride_StringValue_CaseInsensitive_Succeeds()
        {
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorPropertyBag.JsonProcessorPropertyBagKey, "sTrEaM" }
                }
            };

            bool found = JsonProcessorPropertyBag.TryGetJsonProcessorOverride(ro, out JsonProcessor jp);
            Assert.IsTrue(found);
            Assert.AreEqual(JsonProcessor.Stream, jp);
        }

        [TestMethod]
        public void TryGetOverride_InvalidString_Ignored()
        {
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorPropertyBag.JsonProcessorPropertyBagKey, "invalid" }
                }
            };

            bool found = JsonProcessorPropertyBag.TryGetJsonProcessorOverride(ro, out JsonProcessor jp);
            Assert.IsFalse(found);
            Assert.AreEqual(JsonProcessor.Newtonsoft, jp);
        }

        [TestMethod]
        public void DetermineAndNormalize_UnsupportedCombination_Throws()
        {
#pragma warning disable CS0618 // testing legacy path rejection for Stream processor
            EncryptionOptions opts = new()
            {
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                JsonProcessor = JsonProcessor.Stream
            };
#pragma warning restore CS0618
            RequestOptions ro = new ItemRequestOptions();

            Assert.ThrowsException<NotSupportedException>(() => JsonProcessorPropertyBag.DetermineAndNormalizeJsonProcessor(opts, ro));
        }

        [TestMethod]
        public void DetermineAndNormalize_OverrideApplied()
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
                    { JsonProcessorPropertyBag.JsonProcessorPropertyBagKey, JsonProcessor.Stream }
                }
            };

            JsonProcessorPropertyBag.DetermineAndNormalizeJsonProcessor(opts, ro);
            Assert.AreEqual(JsonProcessor.Stream, opts.JsonProcessor);
        }

        [TestMethod]
        public void DetermineAndNormalize_NoOverride_NoChange()
        {
            EncryptionOptions opts = new()
            {
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                JsonProcessor = JsonProcessor.Newtonsoft
            };
            RequestOptions ro = new ItemRequestOptions();

            JsonProcessorPropertyBag.DetermineAndNormalizeJsonProcessor(opts, ro);
            Assert.AreEqual(JsonProcessor.Newtonsoft, opts.JsonProcessor);
        }

        [TestMethod]
        public void TryGetOverride_NullRequestOptions_Default()
        {
            bool found = JsonProcessorPropertyBag.TryGetJsonProcessorOverride(null, out JsonProcessor jp);
            Assert.IsFalse(found);
            Assert.AreEqual(JsonProcessor.Newtonsoft, jp);
        }

        [TestMethod]
        public void TryGetOverride_NoPropertiesDictionary_Default()
        {
            RequestOptions ro = new ItemRequestOptions(); // Properties remains null
            bool found = JsonProcessorPropertyBag.TryGetJsonProcessorOverride(ro, out JsonProcessor jp);
            Assert.IsFalse(found);
            Assert.AreEqual(JsonProcessor.Newtonsoft, jp);
        }

        [TestMethod]
        public void TryGetOverride_MixedCaseKey_NotRecognized()
        {
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    // Intentionally different casing pattern; should not match for perf (no ToUpper/ToLower)
                    { "Encryption-Json-Processor", JsonProcessor.Stream }
                }
            };

            bool found = JsonProcessorPropertyBag.TryGetJsonProcessorOverride(ro, out JsonProcessor jp);
            Assert.IsFalse(found);
            Assert.AreEqual(JsonProcessor.Newtonsoft, jp);
        }

        [TestMethod]
        public void TryGetOverride_MismatchedKey_NotRecognized()
        {
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorPropertyBag.JsonProcessorPropertyBagKey + "-extra", JsonProcessor.Stream }
                }
            };

            bool found = JsonProcessorPropertyBag.TryGetJsonProcessorOverride(ro, out JsonProcessor jp);
            Assert.IsFalse(found);
            Assert.AreEqual(JsonProcessor.Newtonsoft, jp);
        }
    }
}
#endif
