//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests.TestHelpers
{
    using System.Collections.Generic;
    using Mde = Microsoft.Data.Encryption.Cryptography;

    internal static class TestCryptoHelpers
    {
        public class DummyKeyEncryptionKey : Mde.KeyEncryptionKey
        {
            public DummyKeyEncryptionKey() : base(name: "testKek", path: "test://kek", keyStoreProvider: new DummyProvider()) { }

            private class DummyProvider : Mde.EncryptionKeyStoreProvider
            {
                public override string ProviderName => "testProvider";
                public override byte[] UnwrapKey(string encryptionKeyId, Mde.KeyEncryptionKeyAlgorithm algorithm, byte[] encryptedKey) => encryptedKey;
                public override byte[] WrapKey(string encryptionKeyId, Mde.KeyEncryptionKeyAlgorithm algorithm, byte[] key) => key;
                public override byte[] Sign(string encryptionKeyId, bool allowEnclaveComputations) => new byte[] { 1, 2, 3 };
                public override bool Verify(string encryptionKeyId, bool allowEnclaveComputations, byte[] signature) => true;
            }
        }

        public static Mde.AeadAes256CbcHmac256EncryptionAlgorithm CreateAlgorithm(Mde.EncryptionType type)
        {
            var kek = new DummyKeyEncryptionKey();
            var pdek = new Mde.ProtectedDataEncryptionKey("pdek-" + type.ToString().ToLowerInvariant(), kek);
            return new Mde.AeadAes256CbcHmac256EncryptionAlgorithm(pdek, type);
        }

        public static EncryptionSettings CreateSettingsWithInjected(string propertyName, Mde.EncryptionType type)
        {
            var algo = CreateAlgorithm(type);
            return CreateSettingsWithInjected(propertyName, type, algo);
        }

        public static EncryptionSettings CreateSettingsWithInjected(string propertyName, Mde.EncryptionType type, Mde.AeadAes256CbcHmac256EncryptionAlgorithm algo)
        {
            var settings = new EncryptionSettings("rid", new List<string> { "/id" });
            var container = (EncryptionContainer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EncryptionContainer));
            var forProperty = new EncryptionSettingForProperty(
                clientEncryptionKeyId: "cek1",
                encryptionType: type,
                encryptionContainer: container,
                databaseRid: "dbRid",
                injectedAlgorithm: algo);
            settings.SetEncryptionSettingForProperty(propertyName, forProperty);
            return settings;
        }
    }
}
