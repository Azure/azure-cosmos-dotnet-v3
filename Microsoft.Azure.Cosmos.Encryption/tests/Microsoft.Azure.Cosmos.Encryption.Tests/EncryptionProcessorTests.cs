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
    protected static MemoryStream ToStream(string json)
    {
        return StreamTestHelpers.ToStream(json);
    }
    protected static string ReadToEnd(Stream s)
    {
        return StreamTestHelpers.ReadToEnd(s);
    }

        private static EncryptionSettings CreateSettingsWithNoProperties()
        {
            // Use the internal constructor normally; leaving the mapping empty results in
            // PropertiesToEncrypt being an empty enumeration (no encryption work performed).
            return new EncryptionSettings("rid", new List<string> { "/id" });
        }

        private static EncryptionSettings CreateSettingsForId()
        {
            EncryptionSettings settings = new EncryptionSettings("rid", new List<string> { "/id" });
            // Use an uninitialized container; it won't be used in the failure paths these tests exercise.
            EncryptionContainer container = (EncryptionContainer)FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            EncryptionSettingForProperty forProperty = new EncryptionSettingForProperty(
                clientEncryptionKeyId: "cek1",
                encryptionType: Mde.EncryptionType.Deterministic,
                encryptionContainer: container,
                databaseRid: "dbRid");
            settings.SetEncryptionSettingForProperty("id", forProperty);
            return settings;
        }

        private static Mde.AeadAes256CbcHmac256EncryptionAlgorithm CreateDeterministicAlgorithm()
        {
            return TestCryptoHelpers.CreateAlgorithm(Mde.EncryptionType.Deterministic);
        }

        private static EncryptionSettings CreateSettingsWithInjected(string propertyName, Mde.AeadAes256CbcHmac256EncryptionAlgorithm algorithm)
        {
            EncryptionSettings settings = new EncryptionSettings("rid", new List<string> { "/id" });
            EncryptionContainer container = (EncryptionContainer)FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            EncryptionSettingForProperty forProperty = new EncryptionSettingForProperty(
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
            EncryptionSettings settings = new EncryptionSettings("rid", new List<string> { "/id" });
            EncryptionContainer container = (EncryptionContainer)FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            settings.SetEncryptionSettingForProperty(prop, new EncryptionSettingForProperty("cek1", Mde.EncryptionType.Deterministic, container, "dbRid", algo));
            return settings;
        }

        private static Mde.AeadAes256CbcHmac256EncryptionAlgorithm Algo()
        {
            return TestCryptoHelpers.CreateAlgorithm(Mde.EncryptionType.Deterministic);
        }

    // (Removed local KEK shim; rely on TestHelpers.TestCryptoHelpers instead.)

        #endregion

    // Documentation moved to XML comments and README. Removed no-op test.
    }
}
