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

        private Task<MdeCryptoResult> EncryptWithPublicEncryptorAsync(
            TypeMarker typeMarker,
            byte[] plainText,
            int plainTextLength)
        {
            byte[] exactPlainText = new byte[plainTextLength];
            Buffer.BlockCopy(plainText, 0, exactPlainText, 0, plainTextLength);

            Task<byte[]> encryptTask;
            try
            {
                encryptTask = this.encryptor.EncryptAsync(
                    exactPlainText,
                    this.dataEncryptionKeyId,
                    this.encryptionAlgorithm,
                    this.cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                return Task.FromException<MdeCryptoResult>(exception);
            }

            return CompleteEncryptionAsync(encryptTask, typeMarker);
        }

        private Task<MdeCryptoResult> DecryptWithPublicEncryptorAsync(
            byte[] cipherTextWithTypeMarker,
            int cipherTextLength)
        {
            byte[] exactCipherText = new byte[cipherTextLength - 1];
            Buffer.BlockCopy(cipherTextWithTypeMarker, 1, exactCipherText, 0, exactCipherText.Length);

            Task<byte[]> decryptTask;
            try
            {
                decryptTask = this.encryptor.DecryptAsync(
                    exactCipherText,
                    this.dataEncryptionKeyId,
                    this.encryptionAlgorithm,
                    this.cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                return Task.FromException<MdeCryptoResult>(exception);
            }

            return CompleteDecryptionAsync(decryptTask);
        }

        private static async Task<MdeCryptoResult> CompleteEncryptionAsync(
            Task<byte[]> encryptTask,
            TypeMarker typeMarker)
        {
#pragma warning disable VSTHRD003 // The task is supplied by the caller's Encryptor implementation and is always awaited asynchronously.
            byte[] cipherText = await encryptTask.ConfigureAwait(false) ?? throw new InvalidOperationException(
                $"{nameof(Encryptor)} returned null cipherText from {nameof(Encryptor.EncryptAsync)}.");
#pragma warning restore VSTHRD003

            byte[] cipherTextWithTypeMarker = new byte[checked(cipherText.Length + 1)];
            cipherTextWithTypeMarker[0] = (byte)typeMarker;
            Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);
            return new MdeCryptoResult(cipherTextWithTypeMarker, cipherTextWithTypeMarker.Length);
        }

        private static async Task<MdeCryptoResult> CompleteDecryptionAsync(Task<byte[]> decryptTask)
        {
#pragma warning disable VSTHRD003 // The task is supplied by the caller's Encryptor implementation and is always awaited asynchronously.
            byte[] plainText = await decryptTask.ConfigureAwait(false) ?? throw new InvalidOperationException(
                $"{nameof(Encryptor)} returned null plainText from {nameof(Encryptor.DecryptAsync)}.");
#pragma warning restore VSTHRD003

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
