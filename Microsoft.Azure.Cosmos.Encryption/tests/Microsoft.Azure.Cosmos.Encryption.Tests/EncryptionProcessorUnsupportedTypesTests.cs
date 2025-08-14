//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionProcessorUnsupportedTypesTests
    {
        [TestMethod]
        public void Serialize_UnsupportedTypes_ShouldThrow_InvalidOperationException()
        {
            // Guid
            Exception ex = null;
            try { _ = EncryptionProcessor.Serialize(new JValue(Guid.NewGuid())); }
            catch (Exception e) { ex = e; }
            Assert.IsNotNull(ex);
            Assert.IsInstanceOfType(ex, typeof(InvalidOperationException));

            // Bytes
            ex = null;
            try { _ = EncryptionProcessor.Serialize(new JValue(new byte[] { 1, 2, 3, 4 })); }
            catch (Exception e) { ex = e; }
            Assert.IsNotNull(ex);
            Assert.IsInstanceOfType(ex, typeof(InvalidOperationException));

            // TimeSpan
            ex = null;
            try { _ = EncryptionProcessor.Serialize(new JValue(TimeSpan.FromMinutes(5))); }
            catch (Exception e) { ex = e; }
            Assert.IsNotNull(ex);
            Assert.IsInstanceOfType(ex, typeof(InvalidOperationException));

            // Uri (additional unsupported type)
            ex = null;
            try { _ = EncryptionProcessor.Serialize(new JValue(new Uri("https://example.com"))); }
            catch (Exception e) { ex = e; }
            Assert.IsNotNull(ex);
            Assert.IsInstanceOfType(ex, typeof(InvalidOperationException));
        }

        [TestMethod]
        public void Serialize_ShouldEscape_NonString_ShouldThrow_ArgumentException()
        {
            // shouldEscape path is enforced in SerializeAndEncryptValueAsync; use the public EncryptAsync with 'id' configured and non-string id.
            var settings = new EncryptionSettings("rid", new System.Collections.Generic.List<string> { "/id" });
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            var forProperty = new EncryptionSettingForProperty(
                clientEncryptionKeyId: "cek1",
                encryptionType: Microsoft.Data.Encryption.Cryptography.EncryptionType.Deterministic,
                encryptionContainer: container,
                databaseRid: "dbRid");
            settings.SetEncryptionSettingForProperty("id", forProperty);

            using var s = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"id\":42}"));
            var ex = Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            {
                await EncryptionProcessor.EncryptAsync(s, settings, operationDiagnostics: null, cancellationToken: default);
            }).GetAwaiter().GetResult();
            StringAssert.Contains(ex.Message, "value to escape has to be string type");
        }
    }
}
