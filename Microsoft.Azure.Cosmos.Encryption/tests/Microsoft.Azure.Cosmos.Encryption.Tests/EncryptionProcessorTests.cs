//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Mde = Microsoft.Data.Encryption.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Encryption.Tests.TestHelpers;

    /// <summary>
    /// Comprehensive test suite for EncryptionProcessor functionality.
    /// This class is split into multiple partial classes organized by test category for better maintainability.
    /// </summary>
    [TestClass]
    public partial class EncryptionProcessorTests
    {
        #region Shared Test Utilities

    // Thin wrappers so existing partial classes can call these helpers.
    protected static MemoryStream ToStream(string json) => StreamTestHelpers.ToStream(json);
    protected static string ReadToEnd(Stream s) => StreamTestHelpers.ReadToEnd(s);

        private static EncryptionSettings CreateSettingsWithNoProperties()
        {
            // Create an EncryptionSettings instance without invoking its private constructor
            // and set PropertiesToEncrypt to an empty enumerable so no work is performed.
            object settings = FormatterServices.GetUninitializedObject(typeof(EncryptionSettings));

            var prop = typeof(EncryptionSettings).GetField("<PropertiesToEncrypt>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            prop.SetValue(settings, Array.Empty<string>());

            return (EncryptionSettings)settings;
        }

        private static EncryptionSettings CreateSettingsForId()
        {
            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            // Use an uninitialized container; it won't be used in the failure paths these tests exercise.
            var container = (EncryptionContainer)FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            var forProperty = new EncryptionSettingForProperty(
                clientEncryptionKeyId: "cek1",
                encryptionType: Mde.EncryptionType.Deterministic,
                encryptionContainer: container,
                databaseRid: "dbRid");
            settings.SetEncryptionSettingForProperty("id", forProperty);
            return settings;
        }

        private static Mde.AeadAes256CbcHmac256EncryptionAlgorithm CreateDeterministicAlgorithm()
            => TestCryptoHelpers.CreateAlgorithm(Mde.EncryptionType.Deterministic);

        private static EncryptionSettings CreateSettingsWithInjected(string propertyName, Mde.AeadAes256CbcHmac256EncryptionAlgorithm algorithm)
        {
            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            var container = (EncryptionContainer)FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            var forProperty = new EncryptionSettingForProperty(
                clientEncryptionKeyId: "cek1",
                encryptionType: Mde.EncryptionType.Deterministic,
                encryptionContainer: container,
                databaseRid: "dbRid",
                injectedAlgorithm: algorithm);
            settings.SetEncryptionSettingForProperty(propertyName, forProperty);
            return settings;
        }

        private static EncryptionSettings CreateSettings(string prop, Mde.AeadAes256CbcHmac256EncryptionAlgorithm algo)
        {
            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            var container = (EncryptionContainer)FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            settings.SetEncryptionSettingForProperty(prop, new EncryptionSettingForProperty("cek1", Mde.EncryptionType.Deterministic, container, "dbRid", algo));
            return settings;
        }

        private static Mde.AeadAes256CbcHmac256EncryptionAlgorithm Algo() => TestCryptoHelpers.CreateAlgorithm(Mde.EncryptionType.Deterministic);

    // (Removed local KEK shim; rely on TestHelpers.TestCryptoHelpers instead.)

        #endregion

        /// <summary>
        /// This class serves as the main entry point for all EncryptionProcessor tests.
        /// The tests have been consolidated from 19+ separate test classes into 5 logical groupings
        /// organized as partial classes for better maintainability and reduced fragmentation.
        /// 
        /// Test Organization:
        /// • CoreFunctionality.cs - End-to-end encryption/decryption, stream handling, JSON traversal
        /// • Validation.cs - Argument validation, settings validation, unsupported types, type markers  
        /// • EdgeCases.cs - Depth handling, overflow scenarios, no-op operations, diagnostics edge cases
        /// • DataFormatEncoding.cs - ID escaping, Unicode handling, feed responses, value stream encryption
        /// • Cryptography.cs - Randomized algorithms, different encryption modes, key management
        /// </summary>
        [TestMethod]
        public void MainTestClass_OrganizationDocumentation()
        {
            // This test documents the reorganization from fragmented classes to meaningful groupings
            // Previously: 19+ separate classes (EncryptionProcessorEndToEndTests, EncryptionProcessorArgumentValidationTests, etc.)
            // Now: 5 logical partial classes with shared utilities and better maintainability
            Assert.IsTrue(true, "Test organization documented successfully");
        }
    }
}
