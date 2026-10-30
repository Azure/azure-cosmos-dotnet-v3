// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class MdeCryptoOperationAdapter
    {
        private readonly string dataEncryptionKeyId;
        private readonly DataEncryptionKey encryptionKey;
        private readonly string encryptionAlgorithm;
        private readonly Encryptor encryptor;
        private readonly MdeEncryptor mdeEncryptor;
        private readonly CancellationToken cancellationToken;

        internal bool UsesPublicEncryptor => this.encryptionKey == null;

        private MdeCryptoOperationAdapter(
            Encryptor encryptor,
            DataEncryptionKey encryptionKey,
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            MdeEncryptor mdeEncryptor,
            CancellationToken cancellationToken)
        {
            this.encryptor = encryptor;
            this.encryptionKey = encryptionKey;
            this.dataEncryptionKeyId = dataEncryptionKeyId;
            this.encryptionAlgorithm = encryptionAlgorithm;
            this.mdeEncryptor = mdeEncryptor;
            this.cancellationToken = cancellationToken;
        }

        internal static async Task<MdeCryptoOperationAdapter> CreateAsync(
            Encryptor encryptor,
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            MdeEncryptor mdeEncryptor,
            CancellationToken cancellationToken)
        {
            DataEncryptionKey encryptionKey = null;
            if (encryptor is IDataEncryptionKeyAccessor keyAccessor)
            {
                encryptionKey = await keyAccessor.GetEncryptionKeyAsync(
                    dataEncryptionKeyId,
                    encryptionAlgorithm,
                    cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException(
                        $"{nameof(IDataEncryptionKeyAccessor)} returned null {nameof(DataEncryptionKey)}.");
            }

            return new MdeCryptoOperationAdapter(
                encryptor,
                encryptionKey,
                dataEncryptionKeyId,
                encryptionAlgorithm,
                mdeEncryptor,
                cancellationToken);
        }

        internal bool TryEncrypt(
            TypeMarker typeMarker,
            byte[] plainText,
            int plainTextLength,
            ArrayPoolManager arrayPoolManager,
            out MdeCryptoResult result,
            out Task<MdeCryptoResult> pendingOperation)
        {
            if (this.encryptionKey != null)
            {
                (byte[] buffer, int length) = this.mdeEncryptor.Encrypt(
                    this.encryptionKey,
                    typeMarker,
                    plainText,
                    plainTextLength,
                    arrayPoolManager);
                result = new MdeCryptoResult(buffer, length);
                pendingOperation = null;
                return true;
            }

            result = default;
            pendingOperation = this.EncryptWithPublicEncryptorAsync(typeMarker, plainText, plainTextLength);
            return false;
        }

        internal bool TryDecrypt(
            byte[] cipherTextWithTypeMarker,
            int cipherTextLength,
            ArrayPoolManager arrayPoolManager,
            out MdeCryptoResult result,
            out Task<MdeCryptoResult> pendingOperation)
        {
            if (this.encryptionKey != null)
            {
                (byte[] buffer, int length) = this.mdeEncryptor.Decrypt(
                    this.encryptionKey,
                    cipherTextWithTypeMarker,
                    cipherTextLength,
                    arrayPoolManager);
                result = new MdeCryptoResult(buffer, length);
                pendingOperation = null;
                return true;
            }

            result = default;
            pendingOperation = this.DecryptWithPublicEncryptorAsync(cipherTextWithTypeMarker, cipherTextLength);
            return false;
        }

        private async Task<MdeCryptoResult> EncryptWithPublicEncryptorAsync(
            TypeMarker typeMarker,
            byte[] plainText,
            int plainTextLength)
        {
            byte[] cipherText = await MdeCryptoOperations.EncryptAsync(
                this.encryptor,
                this.dataEncryptionKeyId,
                this.encryptionAlgorithm,
                typeMarker,
                plainText,
                plainTextLength,
                this.cancellationToken).ConfigureAwait(false);
            return new MdeCryptoResult(cipherText, cipherText.Length);
        }

        private async Task<MdeCryptoResult> DecryptWithPublicEncryptorAsync(
            byte[] cipherTextWithTypeMarker,
            int cipherTextLength)
        {
            byte[] plainText = await MdeCryptoOperations.DecryptAsync(
                this.encryptor,
                this.dataEncryptionKeyId,
                this.encryptionAlgorithm,
                cipherTextWithTypeMarker,
                cipherTextLength,
                this.cancellationToken).ConfigureAwait(false);
            return new MdeCryptoResult(plainText, plainText.Length);
        }
    }

    internal readonly struct MdeCryptoResult
    {
        internal MdeCryptoResult(byte[] buffer, int length)
        {
            this.Buffer = buffer;
            this.Length = length;
        }

        internal byte[] Buffer { get; }

        internal int Length { get; }
    }
}
#endif