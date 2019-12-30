//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for interacting with a provider that can be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption.
    /// See https://tbd for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public abstract class KeyWrapProvider
    {
        /// <summary>
        /// Wraps (i.e. encrypts) the provided data encryption key.
        /// </summary>
        /// <param name="key">Data encryption key that needs to be wrapped.</param>
        /// <param name="metadata">Metadata for the wrap provider that should be used to wrap / unwrap the key.</param>
        /// <param name="cancellationToken">Cancellation token allowing for cancellation of this operation.</param>
        /// <returns>Awaitable wrapped (i.e. encrypted) version of data encryption key passed in possibly with updated metadata.</returns>
        public abstract Task<KeyWrapResponse> WrapKeyAsync(byte[] key, KeyWrapMetadata metadata, CancellationToken cancellationToken);

        /// <summary>
        /// Unwraps (i.e. decrypts) the provided wrapped data encryption key.
        /// </summary>
        /// <param name="wrappedKey">Wrapped form of data encryption key that needs to be unwrapped.</param>
        /// <param name="metadata">Metadata for the wrap provider that should be used to wrap / unwrap the key.</param>
        /// <param name="cancellationToken">Cancellation token allowing for cancellation of this operation.</param>
        /// <returns>Awaitable unwrapped (i.e. unencrypted) version of data encryption key passed in and how long the raw data encryption key can be cached on the client.</returns>
        public abstract Task<KeyUnwrapResponse> UnwrapKeyAsync(byte[] wrappedKey, KeyWrapMetadata metadata, CancellationToken cancellationToken);
    }
}
