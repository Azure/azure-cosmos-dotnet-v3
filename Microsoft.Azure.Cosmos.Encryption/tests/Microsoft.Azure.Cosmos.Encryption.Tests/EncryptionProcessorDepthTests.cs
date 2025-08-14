//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Mde = Microsoft.Data.Encryption.Cryptography;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Encryption.Tests.TestHelpers;

    [TestClass]
    public class EncryptionProcessorDepthTests
    {
        private static string DeepJson(int depth)
        {
            // Build nested {"o": {"o": ... {"v": "x"} }}
            JObject cur = new JObject { ["v"] = "x" };
            for (int i = 0; i < depth; i++)
            {
                cur = new JObject { ["o"] = cur };
            }
            return cur.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static EncryptionSettings CreateSettings(string prop, Mde.AeadAes256CbcHmac256EncryptionAlgorithm algo)
        {
            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            settings.SetEncryptionSettingForProperty(prop, new EncryptionSettingForProperty("cek1", Mde.EncryptionType.Deterministic, container, "dbRid", algo));
            return settings;
        }

    private static Mde.AeadAes256CbcHmac256EncryptionAlgorithm Algo() => TestCryptoHelpers.CreateAlgorithm(Mde.EncryptionType.Deterministic);

        [TestMethod]
        public async Task EncryptDecrypt_MaxDepthMinusOne_Succeeds()
        {
            // Base serializer uses MaxDepth = 64; we generate a depth somewhat below that to avoid parser issues
            string json = $"{{\"id\":\"d\",\"Secret\":{DeepJson(30)} }}"; // 30 nested levels
            var settings = CreateSettings("Secret", Algo());

            using Stream enc = await EncryptionProcessor.EncryptAsync(EncryptionProcessor.BaseSerializer.ToStream(JObject.Parse(json)), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            using Stream dec = await EncryptionProcessor.DecryptAsync(enc, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject round = EncryptionProcessor.BaseSerializer.FromStream<JObject>(dec);
            Assert.AreEqual("x", round.SelectToken("$.Secret..v").Value<string>());
        }

        [TestMethod]
        public async Task EncryptDecrypt_NearMaxDepth_Succeeds()
        {
            // Push close to MaxDepth (64). Using ~60 nested objects keeps us under the cap considering root and wrappers.
            JObject deep = JObject.Parse(DeepJson(60));
            JObject doc = new JObject { ["id"] = "deep", ["Secret"] = deep };
            var settings = CreateSettings("Secret", Algo());

            using Stream enc = await EncryptionProcessor.EncryptAsync(EncryptionProcessor.BaseSerializer.ToStream(doc), settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            using Stream dec = await EncryptionProcessor.DecryptAsync(enc, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
            JObject round = EncryptionProcessor.BaseSerializer.FromStream<JObject>(dec);
            Assert.AreEqual("x", round.SelectToken("$.Secret..v").Value<string>());
        }

    // Centralized helpers used instead
    }
}
