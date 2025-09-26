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
    public class RequestOptionsPropertiesExtensionsTests
    {
        [TestMethod]
    public void TryReadOverride_EnumValue_Succeeds()
        {
            RequestOptions ro = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { RequestOptionsPropertiesExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream }
                }
            };

            bool found = RequestOptionsPropertiesExtensions.TryReadJsonProcessorOverride(ro, out JsonProcessor jp);
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
                    { RequestOptionsPropertiesExtensions.JsonProcessorPropertyBagKey, "sTrEaM" }
                }
            };

            bool found = RequestOptionsPropertiesExtensions.TryReadJsonProcessorOverride(ro, out JsonProcessor jp);
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
                    { RequestOptionsPropertiesExtensions.JsonProcessorPropertyBagKey, "Stream" }
                }
            };

            bool found = RequestOptionsPropertiesExtensions.TryReadJsonProcessorOverride(ro, out JsonProcessor jp);
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
                    { RequestOptionsPropertiesExtensions.JsonProcessorPropertyBagKey, "invalid" }
                }
            };

            bool found = RequestOptionsPropertiesExtensions.TryReadJsonProcessorOverride(ro, out JsonProcessor jp);
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

            Assert.ThrowsException<NotSupportedException>(() => RequestOptionsPropertiesExtensions.ResolveJsonProcessorSelection(ro, opts));
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
                    { RequestOptionsPropertiesExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream }
                }
            };

            RequestOptionsPropertiesExtensions.ResolveJsonProcessorSelection(ro, opts);
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

            RequestOptionsPropertiesExtensions.ResolveJsonProcessorSelection(ro, opts);
            Assert.AreEqual(JsonProcessor.Newtonsoft, opts.JsonProcessor);
        }

        [TestMethod]
        public void TryReadOverride_NullRequestOptions_Default()
        {
            bool found = RequestOptionsPropertiesExtensions.TryReadJsonProcessorOverride(null, out JsonProcessor jp);
            Assert.IsFalse(found);
            Assert.AreEqual(JsonProcessor.Newtonsoft, jp);
        }

        [TestMethod]
        public void TryReadOverride_NoPropertiesDictionary_Default()
        {
            RequestOptions ro = new ItemRequestOptions(); // Properties remains null
            bool found = RequestOptionsPropertiesExtensions.TryReadJsonProcessorOverride(ro, out JsonProcessor jp);
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

            bool found = RequestOptionsPropertiesExtensions.TryReadJsonProcessorOverride(ro, out JsonProcessor jp);
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
                    { RequestOptionsPropertiesExtensions.JsonProcessorPropertyBagKey + "-extra", JsonProcessor.Stream }
                }
            };

            bool found = RequestOptionsPropertiesExtensions.TryReadJsonProcessorOverride(ro, out JsonProcessor jp);
            Assert.IsFalse(found);
            Assert.AreEqual(JsonProcessor.Newtonsoft, jp);
        }
    }
}
#endif
