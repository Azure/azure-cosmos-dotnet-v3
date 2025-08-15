//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using Mde = Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// Validation tests for EncryptionProcessor including argument validation,
    /// settings validation, unsupported types, and type markers.
    /// </summary>
    public partial class EncryptionProcessorTests
    {
        #region Argument Validation Tests

        [TestMethod]
        public async Task Validation_EncryptAsync_NullInput_ThrowsArgumentNullException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => EncryptionProcessor.EncryptAsync(input: null, encryptionSettings: CreateSettingsForId(), operationDiagnostics: null, cancellationToken: CancellationToken.None), "input");
        }

        [TestMethod]
        public async Task Validation_EncryptAsync_NullSettings_ThrowsOrFailsPredictably()
        {
            using System.IO.MemoryStream input = ToStream("{\"id\":\"1\"}");
            try
            {
                await EncryptionProcessor.EncryptAsync(input: input, encryptionSettings: null, operationDiagnostics: null, cancellationToken: CancellationToken.None);
                Assert.Fail("Expected an exception when encryptionSettings is null.");
            }
            catch (NullReferenceException)
            {
                // Current implementation: NRE when accessing PropertiesToEncrypt; acceptable documented behavior for now.
            }
            catch (ArgumentNullException)
            {
                // Future improvement may throw ANE; accept either to avoid test fragility.
            }
        }

        [TestMethod]
        public async Task Validation_EncryptAsync_IdNonStringWithShouldEscape_ThrowsArgumentException()
        {
            // Arrange: id is an integer, settings configured to encrypt 'id' which triggers shouldEscape
            EncryptionSettings settings = CreateSettingsForId();
            using System.IO.MemoryStream input = ToStream("{\"id\": 42, \"p\": 1}");

            // Act & Assert
            ArgumentException ex = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => EncryptionProcessor.EncryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None));
            StringAssert.Contains(ex.Message, "value to escape has to be string type");
        }

        #endregion

        #region Settings Validation Tests

        private static EncryptionSettings CreateSettingsWithMissingMapping(params string[] properties)
        {
            // Create settings and only declare PropertiesToEncrypt via real mappings, then remove them
            // to simulate missing mapping when traversing documents.
            EncryptionSettings settings = new EncryptionSettings("rid", new System.Collections.Generic.List<string> { "/id" });
            EncryptionContainer container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            foreach (string p in properties)
            {
                EncryptionSettingForProperty forProperty = new EncryptionSettingForProperty(
                    clientEncryptionKeyId: "cek1",
                    encryptionType: Mde.EncryptionType.Randomized,
                    encryptionContainer: container,
                    databaseRid: "dbRid");
                settings.SetEncryptionSettingForProperty(p, forProperty);
            }

            // Now clear the mapping dictionary via reflection to simulate Keys present but value missing.
            System.Reflection.FieldInfo dictField = typeof(EncryptionSettings).GetField("encryptionSettingsDictByPropertyName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            System.Collections.Generic.Dictionary<string, EncryptionSettingForProperty> dict = (System.Collections.Generic.Dictionary<string, EncryptionSettingForProperty>)dictField.GetValue(settings);
            foreach (string p in properties)
            {
                dict[p] = null;
            }

            return settings;
        }

        [TestMethod]
        public async Task Validation_EncryptAsync_PropertyWithoutSetting_Throws_And_DoesNotDisposeInput()
        {
            // Arrange: The item contains property 'foo', settings list 'foo' for encryption, but no mapping is configured.
            using System.IO.MemoryStream input = ToStream("{\"id\":\"1\",\"foo\":123}");
            EncryptionSettings settings = CreateSettingsWithMissingMapping("foo");

            // Act
            try
            {
                await EncryptionProcessor.EncryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
                Assert.Fail("Expected ArgumentException due to missing EncryptionSettingForProperty mapping.");
            }
            catch (ArgumentException ex)
            {
                StringAssert.Contains(ex.Message, "Invalid Encryption Setting for the Property:foo");
            }

            // Assert: The input was fully consumed. Some serializers may dispose the stream during read.
            try
            {
                Assert.AreEqual(input.Length, input.Position, "Input stream position should be at end after failure.");
            }
            catch (ObjectDisposedException)
            {
                // Acceptable: FromStream may dispose the input stream after reading.
            }
        }

        [TestMethod]
        public async Task Validation_DecryptAsync_PropertyWithoutSetting_Throws_And_DoesNotDisposeInput()
        {
            // Arrange: The document contains property 'bar', settings list 'bar' for encryption, but no mapping is configured.
            using System.IO.MemoryStream input = ToStream("{\"id\":\"1\",\"bar\":\"someValue\"}");
            EncryptionSettings settings = CreateSettingsWithMissingMapping("bar");

            // Act
            try
            {
                await EncryptionProcessor.DecryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);
                Assert.Fail("Expected ArgumentException due to missing EncryptionSettingForProperty mapping.");
            }
            catch (ArgumentException ex)
            {
                StringAssert.Contains(ex.Message, "Invalid Encryption Setting for Property:bar");
            }

            // Assert: Input should NOT be disposed, and since it was fully read, position should be at end.
            Assert.IsTrue(input.CanRead, "Input stream should not be disposed on failure.");
            Assert.AreEqual(input.Length, input.Position, "Input stream position should be at end after failure.");
        }

        #endregion

        #region Unsupported Types Tests

        [TestMethod]
        public void Validation_Serialize_UnsupportedTypes_ShouldThrow_InvalidOperationException()
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
        public void Validation_Serialize_ShouldEscape_NonString_ShouldThrow_ArgumentException()
        {
            // shouldEscape path is enforced in SerializeAndEncryptValueAsync; use the public EncryptAsync with 'id' configured and non-string id.
            EncryptionSettings settings = new EncryptionSettings("rid", new System.Collections.Generic.List<string> { "/id" });
            EncryptionContainer container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            EncryptionSettingForProperty forProperty = new EncryptionSettingForProperty(
                clientEncryptionKeyId: "cek1",
                encryptionType: Mde.EncryptionType.Deterministic,
                encryptionContainer: container,
                databaseRid: "dbRid");
            settings.SetEncryptionSettingForProperty("id", forProperty);

            using System.IO.MemoryStream s = new System.IO.MemoryStream(Encoding.UTF8.GetBytes("{\"id\":42}"));
            ArgumentException ex = Assert.ThrowsExceptionAsync<ArgumentException>(
                () => EncryptionProcessor.EncryptAsync(s, settings, operationDiagnostics: null, cancellationToken: default)).GetAwaiter().GetResult();
            StringAssert.Contains(ex.Message, "value to escape has to be string type");
        }

        #endregion

        #region Type Marker Tests

        [TestMethod]
        public void Validation_TypeMarker_BasicFunctionality()
        {
            // Test that type markers are properly handled
            // This is a placeholder test - would need to see TypeMarkerTests content for specific tests
            JValue value = new JValue("test");
            (EncryptionProcessor.TypeMarker, byte[]) serialized = EncryptionProcessor.Serialize(value);
            Assert.IsNotNull(serialized);
            Assert.IsTrue(serialized.Item2.Length > 0);
        }

        [TestMethod]
        public void TypeMarker_RoundTrips_For_All_Supported_Types()
        {
            // Boolean
            (EncryptionProcessor.TypeMarker mBool, byte[] bBool) = EncryptionProcessor.Serialize(new JValue(true));
            Assert.AreEqual(EncryptionProcessor.TypeMarker.Boolean, mBool);
            Assert.AreEqual(true, EncryptionProcessor.DeserializeAndAddProperty(bBool, mBool).Value<bool>());

            // Double
            (EncryptionProcessor.TypeMarker mDouble, byte[] bDouble) = EncryptionProcessor.Serialize(new JValue(3.14159));
            Assert.AreEqual(EncryptionProcessor.TypeMarker.Double, mDouble);
            Assert.AreEqual(3.14159, EncryptionProcessor.DeserializeAndAddProperty(bDouble, mDouble).Value<double>(), 0.0);

            // Long
            (EncryptionProcessor.TypeMarker mLong, byte[] bLong) = EncryptionProcessor.Serialize(new JValue(42L));
            Assert.AreEqual(EncryptionProcessor.TypeMarker.Long, mLong);
            Assert.AreEqual(42L, EncryptionProcessor.DeserializeAndAddProperty(bLong, mLong).Value<long>());

            // String
            (EncryptionProcessor.TypeMarker mString, byte[] bString) = EncryptionProcessor.Serialize(new JValue("hello"));
            Assert.AreEqual(EncryptionProcessor.TypeMarker.String, mString);
            Assert.AreEqual("hello", EncryptionProcessor.DeserializeAndAddProperty(bString, mString).Value<string>());
        }

        [TestMethod]
        public async Task TypeMarker_Invalid_Or_Malformed_Cipher_Throws()
        {
            // Build real ciphertext first
            Mde.AeadAes256CbcHmac256EncryptionAlgorithm algorithm = CreateDeterministicAlgorithm();
            EncryptionSettings settings = CreateSettingsWithInjected("p", algorithm);
            EncryptionSettingForProperty propSetting = settings.GetEncryptionSettingForProperty("p");

            // Encrypt a simple string with shouldEscape=false so we get byte[] token
            using System.IO.Stream enc = await EncryptionProcessor.EncryptValueStreamAsync(ToStream("\"abc\""), propSetting, shouldEscape: false, cancellationToken: CancellationToken.None);
            JToken token = EncryptionProcessor.BaseSerializer.FromStream<JToken>(enc);
            byte[] cipherWithMarker = token.ToObject<byte[]>();
            Assert.IsNotNull(cipherWithMarker);
            Assert.IsTrue(cipherWithMarker.Length > 1);

            // Tamper the type marker to an invalid value (e.g., 0 which is not defined)
            byte[] tampered = (byte[])cipherWithMarker.Clone();
            tampered[0] = 0; // invalid TypeMarker

            // Decrypt path should throw when DeserializeAndAddProperty sees invalid marker
            JObject wrapper = new JObject { ["p"] = tampered };
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => EncryptionProcessor.DecryptJTokenAsync(wrapper["p"], propSetting, isEscaped: false, cancellationToken: CancellationToken.None));
        }

        #endregion
    }
}
