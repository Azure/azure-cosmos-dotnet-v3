//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionProcessorEncryptValueStreamAsyncTests
    {
    // Internals are visible to this test assembly; call the internal method directly.

        private static MemoryStream ToStream(string json)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);
        }

        private static object UninitializedEncryptionSettingForProperty()
        {
            return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionSettingForProperty));
        }

        [TestMethod]
        public async Task EncryptValueStreamAsync_NullStream_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
            {
                await EncryptionProcessor.EncryptValueStreamAsync(valueStreamToEncrypt: null, encryptionSettingForProperty: (EncryptionSettingForProperty)UninitializedEncryptionSettingForProperty(), shouldEscape: false, cancellationToken: CancellationToken.None);
            }, "valueStreamToEncrypt");
        }

        [TestMethod]
        public async Task EncryptValueStreamAsync_NullSettings_Throws()
        {
            using var s = ToStream("{}");
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
            {
                await EncryptionProcessor.EncryptValueStreamAsync(valueStreamToEncrypt: s, encryptionSettingForProperty: null, shouldEscape: false, cancellationToken: CancellationToken.None);
            }, "encryptionSettingForProperty");
        }

        [TestMethod]
        public async Task EncryptValueStreamAsync_ObjectArray_AllNullLeaves_NoCrypto_ReturnsSameShape()
        {
            using var s = ToStream("{ \"a\": null, \"b\": [ null, null, { \"c\": null } ] }");
            using Stream result = await EncryptionProcessor.EncryptValueStreamAsync(valueStreamToEncrypt: s, encryptionSettingForProperty: (EncryptionSettingForProperty)UninitializedEncryptionSettingForProperty(), shouldEscape: false, cancellationToken: CancellationToken.None);

            JToken original = JToken.Parse("{ \"a\": null, \"b\": [ null, null, { \"c\": null } ] }");
            JToken roundtrip = new CosmosJsonDotNetSerializer().FromStream<JToken>(result);

            Assert.IsTrue(JToken.DeepEquals(original, roundtrip));
        }
    }
}
