// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class MdeCryptoOperations
    {
        internal static byte[] WrapCipherText(TypeMarker typeMarker, byte[] cipherText)
        {
            byte[] result = new byte[checked(cipherText.Length + 1)];
            result[0] = (byte)typeMarker;
            Buffer.BlockCopy(cipherText, 0, result, 1, cipherText.Length);
            return result;
        }

        internal static byte[] UnwrapCipherText(byte[] cipherTextWithTypeMarker, int length)
        {
            byte[] result = new byte[length - 1];
            Buffer.BlockCopy(cipherTextWithTypeMarker, 1, result, 0, result.Length);
            return result;
        }

        internal static async Task<byte[]> EncryptAsync(
            Encryptor encryptor,
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            TypeMarker typeMarker,
            byte[] plainText,
            int plainTextLength,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Public encryptors can retain their input after the SDK stops waiting.
            byte[] exactPlainText = new byte[plainTextLength];
            Buffer.BlockCopy(plainText, 0, exactPlainText, 0, plainTextLength);
            Task<byte[]> operation = encryptor.EncryptAsync(
                exactPlainText,
                dataEncryptionKeyId,
                encryptionAlgorithm,
                cancellationToken);
            byte[] cipherText = await AwaitResultAsync(
                operation,
                nameof(Encryptor.EncryptAsync),
                "cipherText",
                cancellationToken).ConfigureAwait(false);
            return WrapCipherText(typeMarker, cipherText);
        }

        internal static async Task<byte[]> DecryptAsync(
            Encryptor encryptor,
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            byte[] cipherTextWithTypeMarker,
            int cipherTextLength,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] exactCipherText = UnwrapCipherText(cipherTextWithTypeMarker, cipherTextLength);
            Task<byte[]> operation = encryptor.DecryptAsync(
                exactCipherText,
                dataEncryptionKeyId,
                encryptionAlgorithm,
                cancellationToken);
            return await AwaitResultAsync(
                operation,
                nameof(Encryptor.DecryptAsync),
                "plainText",
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task<byte[]> AwaitResultAsync(
            Task<byte[]> operation,
            string operationName,
            string resultName,
            CancellationToken cancellationToken)
        {
            if (operation == null)
            {
                throw new InvalidOperationException($"{nameof(Encryptor)} returned a null task from {operationName}.");
            }

            byte[] result;
            try
            {
#pragma warning disable VSTHRD003 // The task belongs to the caller's Encryptor and is awaited asynchronously.
#if NET8_0_OR_GREATER
                result = await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
#else
                if (!operation.IsCompleted && cancellationToken.CanBeCanceled)
                {
                    TaskCompletionSource<bool> canceled = new (TaskCreationOptions.RunContinuationsAsynchronously);
                    using CancellationTokenRegistration registration = cancellationToken.Register(
                        static state => ((TaskCompletionSource<bool>)state).TrySetResult(true),
                        canceled);

                    if (await Task.WhenAny(operation, canceled.Task).ConfigureAwait(false) != operation)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }

                result = await operation.ConfigureAwait(false);
#endif
#pragma warning restore VSTHRD003
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation ends our wait, not the custom operation; observe any later fault.
                _ = operation.ContinueWith(
                    static completed => { _ = completed.Exception; },
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                throw;
            }

            if (result == null)
            {
                throw new InvalidOperationException($"{nameof(Encryptor)} returned null {resultName} from {operationName}.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
    }
}
