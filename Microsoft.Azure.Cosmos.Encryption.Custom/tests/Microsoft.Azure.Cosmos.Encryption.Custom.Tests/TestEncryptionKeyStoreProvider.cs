//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System.Linq;
    using Microsoft.Data.Encryption.Cryptography;

    internal sealed class TestEncryptionKeyStoreProvider : EncryptionKeyStoreProvider
    {
        public int UnwrapCalls { get; private set; }
        public int WrapCalls { get; private set; }

        public byte[] DerivedRawKey { get; } = Enumerable.Range(0, 32).Select(static i => (byte)(255 - i)).ToArray();

        public override string ProviderName => "test-store";

        public override byte[] UnwrapKey(string encryptionKeyId, KeyEncryptionKeyAlgorithm algorithm, byte[] encryptedKey)
        {
            this.UnwrapCalls++;
            
            return this.DerivedRawKey;
        }

        public override byte[] WrapKey(string encryptionKeyId, KeyEncryptionKeyAlgorithm algorithm, byte[] key)
        {
            this.WrapCalls++;

            return key;
        }

        public override byte[] Sign(string encryptionKeyId, bool allowEnclaveComputations)
        {
            return new byte[] { 0x01 };
        }

        public override bool Verify(string encryptionKeyId, bool allowEnclaveComputations, byte[] signature)
        {
            return signature?.Length == 1 && signature[0] == 0x01;
        }
    }
}
