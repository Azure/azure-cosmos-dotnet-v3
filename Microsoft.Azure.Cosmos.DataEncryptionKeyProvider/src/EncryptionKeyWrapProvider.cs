//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.DataEncryptionKeyProvider
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for interacting with a provider that can be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption.
    /// Implementations are expected to ensure that master keys are highly available and protected against accidental deletion.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public abstract class EncryptionKeyWrapProvider
    {
        /// <summary>
        /// Wraps (i.e. encrypts) the provided data encryption key.
        /// </summary>
        /// <param name="key">Data encryption key that needs to be wrapped.</param>
        /// <param name="metadata">Metadata for the wrap provider that should be used to wrap / unwrap the key.</param>
        /// <param name="cancellationToken">Cancellation token allowing for cancellation of this operation.</param>
        /// <returns>Awaitable wrapped (i.e. encrypted) version of data encryption key passed in possibly with updated metadata.</returns>
        public abstract Task<EncryptionKeyWrapResult> WrapKeyAsync(byte[] key, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken);

        /// <summary>
        /// Unwraps (i.e. decrypts) the provided wrapped data encryption key.
        /// </summary>
        /// <param name="wrappedKey">Wrapped form of data encryption key that needs to be unwrapped.</param>
        /// <param name="metadata">Metadata for the wrap provider that should be used to wrap / unwrap the key.</param>
        /// <param name="cancellationToken">Cancellation token allowing for cancellation of this operation.</param>
        /// <returns>Awaitable unwrapped (i.e. unencrypted) version of data encryption key passed in and how long the raw data encryption key can be cached on the client.</returns>
        public abstract Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(byte[] wrappedKey, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken);
    }
}
