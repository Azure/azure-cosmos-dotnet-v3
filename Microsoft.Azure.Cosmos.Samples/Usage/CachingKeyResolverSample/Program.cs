// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace CachingKeyResolverSample
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Azure.Identity;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption;

    /// <summary>
    /// Sample demonstrating how to wrap Azure Key Vault's <c>KeyResolver</c> with an
    /// in-memory cache to eliminate synchronous AKV I/O during concurrent
    /// encryption / decryption operations.
    /// </summary>
    /// <remarks>
    /// Prerequisites:
    /// <list type="bullet">
    ///   <item>An Azure Cosmos DB account with client-side encryption support.</item>
    ///   <item>An Azure Key Vault with a key created for encryption.</item>
    ///   <item>Azure.Identity credentials configured (DefaultAzureCredential).</item>
    /// </list>
    ///
    /// Set the following environment variables before running:
    /// <list type="bullet">
    ///   <item><c>COSMOS_ENDPOINT</c> — Cosmos DB account URI</item>
    ///   <item><c>COSMOS_KEY</c> — Cosmos DB account key</item>
    ///   <item><c>KEY_VAULT_URL</c> — Azure Key Vault URI (e.g. https://my-vault.vault.azure.net/)</item>
    ///   <item><c>ENCRYPTION_KEY_NAME</c> — Name of the key in Key Vault</item>
    /// </list>
    /// </remarks>
    public class Program
    {
        private const string DatabaseId = "CachingKeyResolverSampleDb";
        private const string ContainerId = "EncryptedItems";
        private const string DekName = "sampleDek";

        public static async Task Main(string[] args)
        {
            string cosmosEndpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT");
            string cosmosKey = Environment.GetEnvironmentVariable("COSMOS_KEY");
            string keyVaultUrl = Environment.GetEnvironmentVariable("KEY_VAULT_URL");
            string encryptionKeyName = Environment.GetEnvironmentVariable("ENCRYPTION_KEY_NAME");

            if (string.IsNullOrEmpty(cosmosEndpoint)
                || string.IsNullOrEmpty(cosmosKey)
                || string.IsNullOrEmpty(keyVaultUrl)
                || string.IsNullOrEmpty(encryptionKeyName))
            {
                Console.WriteLine("ERROR: Set COSMOS_ENDPOINT, COSMOS_KEY, KEY_VAULT_URL, and ENCRYPTION_KEY_NAME environment variables.");
                return;
            }

            string keyId = $"{keyVaultUrl.TrimEnd('/')}/keys/{encryptionKeyName}";

            int cacheHits = 0;
            int cacheMisses = 0;

            // ── Create CachingKeyResolver ───────────────────────────────────
            CachingKeyResolverOptions options = new CachingKeyResolverOptions
            {
                KeyCacheTimeToLive = TimeSpan.FromHours(2),
                ProactiveRefreshThreshold = TimeSpan.FromMinutes(5),
                RefreshTimerInterval = TimeSpan.FromMinutes(1),
            };

            using CachingKeyResolver cachingResolver = new CachingKeyResolver(
                new DefaultAzureCredential(),
                options);

            cachingResolver.OnCacheAccess += (id, isHit) =>
            {
                if (isHit)
                {
                    System.Threading.Interlocked.Increment(ref cacheHits);
                    Console.WriteLine($"  [CACHE HIT]  {id}");
                }
                else
                {
                    System.Threading.Interlocked.Increment(ref cacheMisses);
                    Console.WriteLine($"  [CACHE MISS] {id}");
                }
            };

            // ── Create Cosmos client with encryption ────────────────────────
            CosmosClient baseClient = new CosmosClient(cosmosEndpoint, cosmosKey);
            CosmosClient encryptionClient = baseClient.WithEncryption(
                cachingResolver,
                KeyEncryptionKeyResolverName.AzureKeyVault);

            try
            {
                Console.WriteLine("Setting up database and container...");

                Database database = await encryptionClient.CreateDatabaseIfNotExistsAsync(DatabaseId);

                // Create client encryption key (DEK) wrapped by the AKV key (KEK).
                EncryptionKeyWrapMetadata wrapMetadata = new EncryptionKeyWrapMetadata(
                    KeyEncryptionKeyResolverName.AzureKeyVault,
                    encryptionKeyName,
                    keyId,
                    "RSA-OAEP");

                try
                {
                    await database.CreateClientEncryptionKeyAsync(
                        DekName,
                        DataEncryptionAlgorithm.AeadAes256CbcHmacSha256,
                        wrapMetadata);
                    Console.WriteLine($"Created client encryption key '{DekName}'.");
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    Console.WriteLine($"Client encryption key '{DekName}' already exists.");
                }

                // Create container with encryption policy.
                Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>
                {
                    new ClientEncryptionIncludedPath
                    {
                        Path = "/secret",
                        ClientEncryptionKeyId = DekName,
                        EncryptionType = "Deterministic",
                        EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                    },
                };

                ClientEncryptionPolicy policy = new ClientEncryptionPolicy(paths, policyFormatVersion: 2);
                ContainerProperties containerProps = new ContainerProperties(ContainerId, "/pk")
                {
                    ClientEncryptionPolicy = policy,
                };

                Container container;
                try
                {
                    container = await database.CreateContainerAsync(containerProps);
                    Console.WriteLine($"Created container '{ContainerId}' with encryption policy.");
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    container = database.GetContainer(ContainerId);
                    Console.WriteLine($"Container '{ContainerId}' already exists.");
                }

                container = await container.InitializeEncryptionAsync();

                // ── Insert an encrypted document ────────────────────────────
                var document = new
                {
                    id = Guid.NewGuid().ToString(),
                    pk = "sample",
                    secret = "This value is encrypted at rest.",
                    visible = "This value is NOT encrypted.",
                };

                Console.WriteLine("\nInserting encrypted document...");
                await container.CreateItemAsync(document, new PartitionKey(document.pk));
                Console.WriteLine("Document inserted.");

                // ── Read back the document ──────────────────────────────────
                Console.WriteLine("Reading document back...");
                var response = await container.ReadItemAsync<dynamic>(document.id, new PartitionKey(document.pk));
                Console.WriteLine($"Read document: {response.Resource}");

                // ── Summary ─────────────────────────────────────────────────
                Console.WriteLine($"\n══════════════════════════════════════");
                Console.WriteLine($"  Cache hits:   {cacheHits}");
                Console.WriteLine($"  Cache misses: {cacheMisses}");
                Console.WriteLine($"══════════════════════════════════════");
            }
            finally
            {
                // Cleanup
                try
                {
                    await encryptionClient.GetDatabase(DatabaseId).DeleteAsync();
                    Console.WriteLine($"\nCleaned up database '{DatabaseId}'.");
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }
        }
    }
}
