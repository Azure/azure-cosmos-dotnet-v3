//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstraction for a provider to get data encryption keys for use in client-side encryption.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    abstract class DataEncryptionKeyProvider
    {
        /// <summary>
        /// Retrieves the data encryption key for the given id.
        /// </summary>
        /// <param name="id">Identifier of the data encryption key.</param>
        /// <param name="cancellationToken">Token for request cancellation.</param>
        /// <returns>Data encryption key bytes.</returns>
        public abstract Task<byte[]> FetchDataEncryptionKeyAsync(
            string id,
            CancellationToken cancellationToken);
    }
}