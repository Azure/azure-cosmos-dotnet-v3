//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using global::Azure.Core.Cryptography;

    /// <summary>
    /// Extension methods for <see cref="CosmosClient"/> to support client-side encryption.
    /// </summary>
    [CLSCompliant(false)]
    public static class EncryptionCosmosClientExtensions
    {
        /// <summary>
        /// Provides an instance of CosmosClient with support for performing operations involving client-side encryption.
        /// </summary>
        /// <param name="cosmosClient">CosmosClient instance on which encryption support is needed.</param>
        /// <param name="keyEncryptionKeyResolver">Resolver that allows interaction with key encryption keys.</param>
        /// <param name="keyEncryptionKeyResolverName">Name of the resolver, for example <see cref="KeyEncryptionKeyResolverName.AzureKeyVault" />.</param>
        /// <param name="keyCacheTimeToLive">Time for which raw data encryption keys are cached in-memory. Defaults to 1 hour.</param>
        /// <returns>CosmosClient instance with support for performing operations involving client-side encryption.</returns>
        /// <example>
        /// This example shows how to get instance of CosmosClient with support for performing operations involving client-side encryption.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// Azure.Core.TokenCredential tokenCredential = new Azure.Identity.DefaultAzureCredential();
        /// Azure.Core.Cryptography.IKeyEncryptionKeyResolver keyResolver = new Azure.Security.KeyVault.Keys.Cryptography.KeyResolver(tokenCredential);
        /// CosmosClient client = (new CosmosClient(endpoint, authKey)).WithEncryption(keyResolver, KeyEncryptionKeyResolverName.AzureKeyVault);
        /// Container container = client.GetDatabase("databaseId").GetContainer("containerId");
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// See <see href="https://aka.ms/CosmosClientEncryption">client-side encryption documentation</see> for more details.
        /// </remarks>
        public static CosmosClient WithEncryption(
            this CosmosClient cosmosClient,
            IKeyEncryptionKeyResolver keyEncryptionKeyResolver,
            string keyEncryptionKeyResolverName,
            TimeSpan? keyCacheTimeToLive = null)
        {
            if (keyEncryptionKeyResolver == null)
            {
                throw new ArgumentNullException(nameof(keyEncryptionKeyResolver));
            }

            if (keyEncryptionKeyResolverName == null)
            {
                throw new ArgumentNullException(nameof(keyEncryptionKeyResolverName));
            }

            if (cosmosClient == null)
            {
                throw new ArgumentNullException(nameof(cosmosClient));
            }

            return new EncryptionCosmosClient(cosmosClient, keyEncryptionKeyResolver, keyEncryptionKeyResolverName, keyCacheTimeToLive);
        }
    }
}
