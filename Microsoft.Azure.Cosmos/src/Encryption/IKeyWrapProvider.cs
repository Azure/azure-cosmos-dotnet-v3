//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for interacting with a provider that can be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption.
    /// See <see href="tbd"/> for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public interface IKeyWrapProvider
    {
        /// <summary>
        /// Wraps (i.e. encrypts) the provided data encryption key.
        /// </summary>
        /// <param name="key">Data encryption key that needs to be wrapped.</param>
        /// <param name="metadata">Metadata for the wrap provider that should be used to wrap / unwrap the key.</param>
        /// <returns>Awaitable wrapped (i.e. encrypted) version of data encryption key passed in.</returns>
        Task<byte[]> WrapKeyAsync(byte[] key, KeyWrapMetadata metadata);

        /// <summary>
        /// Unwraps (i.e. decrypts) the provided wrapped data encryption key.
        /// </summary>
        /// <param name="wrappedKey">Wrapped form of data encryption key that needs to be unwrapped.</param>
        /// <param name="metadata">Metadata for the wrap provider that should be used to wrap / unwrap the key.</param>
        /// <returns>Awaitable unwrapped (i.e. unencrypted) version of data encryption key passed in.</returns>
        Task<byte[]> UnwrapKeyAsync(byte[] wrappedKey, KeyWrapMetadata metadata);
    }
}
