// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EncryptionRequestOptionsExperimentalTests
    {
        [TestMethod]
        public void ConfigureJsonProcessor_StreamSetsOverride()
        {
            ItemRequestOptions options = new ();

            options.ConfigureJsonProcessor(JsonProcessor.Stream);

            Assert.IsNotNull(options.Properties);
            Assert.AreEqual(JsonProcessor.Stream, options.Properties["encryption-json-processor"]);
        }

        [TestMethod]
        public void ConfigureJsonProcessor_NewtonsoftRemovesOverrideButKeepsOtherValues()
        {
            ItemRequestOptions options = new ()
            {
                Properties = new Dictionary<string, object>
                {
                    { "custom", 42 },
                    { "encryption-json-processor", JsonProcessor.Stream },
                }
            };

            options.ConfigureJsonProcessor(JsonProcessor.Newtonsoft);

            Assert.IsNotNull(options.Properties);
            Assert.IsFalse(options.Properties.ContainsKey("encryption-json-processor"));
            Assert.AreEqual(42, options.Properties["custom"]);
        }

        [TestMethod]
        public void ConfigureJsonProcessor_ReturnsSameInstance()
        {
            ItemRequestOptions options = new ();

            ItemRequestOptions result = options.ConfigureJsonProcessor(JsonProcessor.Stream);

            Assert.AreSame(options, result);
        }

        [TestMethod]
        public void CreateRequestOptions_StreamConfiguresOverride()
        {
            RequestOptions options = EncryptionRequestOptionsExperimental.CreateRequestOptions(JsonProcessor.Stream);

            Assert.IsNotNull(options.Properties);
            Assert.AreEqual(JsonProcessor.Stream, options.Properties["encryption-json-processor"]);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConfigureJsonProcessor_NullOptionsThrows()
        {
            EncryptionRequestOptionsExperimental.ConfigureJsonProcessor<ItemRequestOptions>(null, JsonProcessor.Stream);
        }
    }
}
#endif
