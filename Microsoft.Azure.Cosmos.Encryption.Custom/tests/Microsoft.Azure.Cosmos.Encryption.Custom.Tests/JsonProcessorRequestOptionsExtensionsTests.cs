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

        [TestMethod]
        public void WithEncryptionJsonProcessor_WritesSelection_ReadBackViaGetJsonProcessor()
        {
            QueryRequestOptions ro = new QueryRequestOptions()
                .WithEncryptionJsonProcessor(JsonProcessor.Stream);

            // The per-call selection must win over whatever container default is supplied.
            Assert.AreEqual(JsonProcessor.Stream, ro.GetJsonProcessor(JsonProcessor.Newtonsoft));

            ro.WithEncryptionJsonProcessor(JsonProcessor.Newtonsoft);
            Assert.AreEqual(JsonProcessor.Newtonsoft, ro.GetJsonProcessor(JsonProcessor.Stream));
        }

        [TestMethod]
        public void WithEncryptionJsonProcessor_ReturnsSameInstance_AndPreservesConcreteType()
        {
            QueryRequestOptions ro = new ();
            QueryRequestOptions result = ro.WithEncryptionJsonProcessor(JsonProcessor.Stream);

            // Generic TRequestOptions keeps the concrete type for fluent chaining, and the same
            // instance is returned (the call configures, it does not clone the options object).
            Assert.AreSame(ro, result);
        }

        [TestMethod]
        public void WithEncryptionJsonProcessor_DoesNotMutateCallerSuppliedPropertiesDictionary()
        {
            // A properties dictionary may be shared with other request-options instances; the extension
            // must copy into a fresh dictionary rather than mutating the caller's original.
            Dictionary<string, object> shared = new () { { "unrelated", 123 } };
            QueryRequestOptions ro = new () { Properties = shared };

            ro.WithEncryptionJsonProcessor(JsonProcessor.Stream);

            Assert.IsFalse(shared.ContainsKey(JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey), "Original dictionary must not be mutated.");
            Assert.IsFalse(ReferenceEquals(shared, ro.Properties), "A new dictionary should have been assigned.");
            Assert.IsTrue(ro.Properties.ContainsKey(JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey));
            Assert.AreEqual(123, ro.Properties["unrelated"], "Pre-existing properties must be preserved in the copy.");
        }

        [TestMethod]
        public void WithEncryptionJsonProcessor_NullRequestOptions_Throws()
        {
            QueryRequestOptions ro = null;
            Assert.ThrowsException<ArgumentNullException>(() => ro.WithEncryptionJsonProcessor(JsonProcessor.Stream));
        }
    }
}
#endif
