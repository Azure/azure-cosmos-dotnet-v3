//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CacheIsolationOperationCountTests
    {
        private const string DekId = "cacheIsolationDek";
        private static readonly string[] PathsToEncrypt =
        {
            "/Sensitive1", "/Sensitive2", "/Sensitive3", "/Sensitive4",
            "/Sensitive5", "/Sensitive6", "/Sensitive7", "/Sensitive8",
        };

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task InterleavedProviders_HonorSeparateCacheLifetimes(bool usePublicFallback)
        {
            DataEncryptionKeyProperties dekProperties = new (
                DekId,
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(),
                new EncryptionKeyWrapMetadata("name", "value"),
                DateTime.UtcNow);

            TestEncryptionKeyStoreProvider cachingStore = new ()
            {
                DataEncryptionKeyCacheTimeToLive = TimeSpan.FromMinutes(30),
            };
            TestEncryptionKeyStoreProvider noRetentionStore = new ()
            {
                DataEncryptionKeyCacheTimeToLive = TimeSpan.Zero,
            };

            using CosmosDataEncryptionKeyProvider cachingProvider = new (cachingStore);
            using CosmosDataEncryptionKeyProvider noRetentionProvider = new (noRetentionStore);
            // Seed wrapped DEK metadata without a remote container; key acquisition and encryption remain real.
            cachingProvider.DekCache.SetDekProperties(DekId, dekProperties);
            noRetentionProvider.DekCache.SetDekProperties(DekId, dekProperties);

            Encryptor cachingEncryptor = usePublicFallback
                ? new PublicEncryptor(new CosmosEncryptor(cachingProvider))
                : new CosmosEncryptor(cachingProvider);
            Encryptor noRetentionEncryptor = usePublicFallback
                ? new PublicEncryptor(new CosmosEncryptor(noRetentionProvider))
                : new CosmosEncryptor(noRetentionProvider);

            for (int i = 0; i < 2; i++)
            {
                await RoundTripAsync(cachingEncryptor);
                Assert.AreEqual(1, cachingStore.UnwrapCalls, "The caching provider must reuse its own unwrapped key.");

                await RoundTripAsync(noRetentionEncryptor);
                // The built-in accessor fetches once per direction; the public fallback fetches once per populated path.
                int unwrapsPerRoundTrip = usePublicFallback ? 2 * PathsToEncrypt.Length : 2;
                Assert.AreEqual((i + 1) * unwrapsPerRoundTrip, noRetentionStore.UnwrapCalls, "The zero-TTL provider must unwrap independently for each required key fetch.");
                Assert.AreEqual(1, cachingStore.UnwrapCalls, "The zero-TTL provider must not evict the other provider's key.");
                Console.WriteLine($"PublicFallback={usePublicFallback}, roundTrips={i + 1}, cached={cachingStore.UnwrapCalls}, zeroTtl={noRetentionStore.UnwrapCalls}");
            }
        }

        private static async Task RoundTripAsync(Encryptor encryptor)
        {
            JObject document = new ();
            foreach (string path in PathsToEncrypt)
            {
                document[path.Substring(1)] = path;
            }

            EncryptionItemRequestOptions options = new ()
            {
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKeyId = DekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                    PathsToEncrypt = PathsToEncrypt,
                },
            };

            using Stream encrypted = await EncryptionProcessor.EncryptAsync(
                EncryptionProcessor.BaseSerializer.ToStream(document),
                encryptor,
                options,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);
            (Stream decrypted, DecryptionContext context) = await EncryptionProcessor.DecryptAsync(
                encrypted,
                encryptor,
                new CosmosDiagnosticsContext(),
                CancellationToken.None);
            using (decrypted)
            {
                Assert.IsNotNull(context);
                Assert.AreEqual(PathsToEncrypt.Length, context.DecryptionInfoList.Single().PathsDecrypted.Count());
                Assert.IsTrue(JToken.DeepEquals(document, EncryptionProcessor.BaseSerializer.FromStream<JObject>(decrypted)));
            }
        }

        private sealed class PublicEncryptor : Encryptor
        {
            private readonly CosmosEncryptor inner;

            public PublicEncryptor(CosmosEncryptor inner)
            {
                this.inner = inner;
            }

            public override Task<byte[]> EncryptAsync(byte[] plainText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                return this.inner.EncryptAsync(plainText, dataEncryptionKeyId, encryptionAlgorithm, cancellationToken);
            }

            public override Task<byte[]> DecryptAsync(byte[] cipherText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                return this.inner.DecryptAsync(cipherText, dataEncryptionKeyId, encryptionAlgorithm, cancellationToken);
            }

            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default)
            {
                return this.inner.GetEncryptionKeyAsync(dataEncryptionKeyId, encryptionAlgorithm, cancellationToken);
            }
        }
    }
}
