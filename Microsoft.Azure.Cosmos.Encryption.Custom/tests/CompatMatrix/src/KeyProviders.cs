// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
// Shared test key providers for the compat-matrix subprocesses.
// Compiled into BOTH the OLD (1.0.0-preview07) and NEW (1.1.0-preview01) builds,
// against whichever Microsoft.Data.Encryption.Cryptography each package pins.

namespace CompatMatrix
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Data.Encryption.Cryptography;

    // MDE algorithm (format v3) key store provider.
    internal sealed class MatrixKeyStoreProvider : EncryptionKeyStoreProvider
    {
        private readonly byte[] derived = Enumerable.Range(0, 32).Select(i => (byte)(255 - i)).ToArray();

        public override string ProviderName => "matrix-store";

        public override byte[] UnwrapKey(string id, KeyEncryptionKeyAlgorithm a, byte[] enc) => this.derived;

        public override byte[] WrapKey(string id, KeyEncryptionKeyAlgorithm a, byte[] key) => key;

        public override byte[] Sign(string id, bool enclave) => new byte[] { 0x01 };

        public override bool Verify(string id, bool enclave, byte[] sig) => sig?.Length == 1 && sig[0] == 0x01;
    }

    // AEAD (legacy, format v2) wrap provider.
    internal sealed class MatrixWrapProvider : EncryptionKeyWrapProvider
    {
        public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(byte[] wrapped, EncryptionKeyWrapMetadata md, CancellationToken ct)
            => Task.FromResult(new EncryptionKeyUnwrapResult(wrapped.Select(b => (byte)(b - 2)).ToArray(), TimeSpan.FromDays(1)));

        public override Task<EncryptionKeyWrapResult> WrapKeyAsync(byte[] key, EncryptionKeyWrapMetadata md, CancellationToken ct)
            => Task.FromResult(new EncryptionKeyWrapResult(key.Select(b => (byte)(b + 2)).ToArray(), md));
    }

    // Encryptor bridging both DEK families to the underlying CosmosEncryptor.
    internal sealed class MatrixEncryptor : Encryptor
    {
        private readonly CosmosEncryptor inner;

        public MatrixEncryptor(DataEncryptionKeyProvider provider) => this.inner = new CosmosEncryptor(provider);

        public override Task<byte[]> DecryptAsync(byte[] cipherText, string dekId, string algo, CancellationToken ct = default)
            => this.inner.DecryptAsync(cipherText, dekId, algo, ct);

        public override Task<byte[]> EncryptAsync(byte[] plainText, string dekId, string algo, CancellationToken ct = default)
            => this.inner.EncryptAsync(plainText, dekId, algo, ct);
#if CEC_NEW
        public override Task<Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKey> GetEncryptionKeyAsync(string dekId, string algo, CancellationToken ct = default)
            => this.inner.GetEncryptionKeyAsync(dekId, algo, ct);
#endif
    }
}
