//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// This class provides extension methods for <see cref="Container"/>.
    /// </summary>
    public static class MdeContainerExtensions
    {
        /// <summary>
        /// Initializes and Caches the Client Encryption Policy configured for the container.
        /// </summary>
        /// <param name="container">MdeContainer.</param>
        /// <returns>Container to perform operations supporting client-side encryption / decryption.</returns>
        public static async Task<Container> InitializeEncryptionAsync(
            this Container container)
        {
            if (container is MdeContainer mdeContainer)
            {
                EncryptionCosmosClient encryptionCosmosClient = mdeContainer.EncryptionCosmosClient;
                await encryptionCosmosClient.GetOrAddClientEncryptionPolicyAsync(container, true);
                return await Task.FromResult(mdeContainer);
            }
            else
            {
                throw new InvalidOperationException($"Invalid {container} used for this operation");
            }
        }
    }
}
