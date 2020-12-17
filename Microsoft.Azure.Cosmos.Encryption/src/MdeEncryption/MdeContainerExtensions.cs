//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    /// <summary>
    /// This class provides extension methods for <see cref="MdeContainer"/>.
    /// </summary>
    public static class MdeContainerExtensions
    {
        /// <summary>
        /// Get Cosmos Client with Encryption support for performing operations using client-side encryption.
        /// </summary>
        /// <param name="container">MdeContainer.</param>
        /// <returns>CosmosClient to perform operations supporting client-side encryption / decryption.</returns>
        public static async Task<Container> InitializeEncryptionAsync(
            this Container container)
        {
            if (container is MdeContainer mdeContainer)
            {
                EncryptionCosmosClient encryptionCosmosClient = mdeContainer.EncryptionCosmosClient;
                await encryptionCosmosClient.GetOrAddClientEncryptionPolicyAsync(container, false);
                return await Task.FromResult(mdeContainer);
            }
            else
            {
                throw new InvalidOperationException($"Invalid {container} used for this operation");
            }
        }
    }
}
