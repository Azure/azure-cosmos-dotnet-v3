//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Locks in the C4-NOVEL fix: <see cref="DekCache.RemoveAsync(string,CancellationToken)"/>
    /// and the source-refresh path must invalidate <see cref="DekCache.RawDekCache"/> for both
    /// the dekId key (used by <see cref="DekCache.SetRawDek"/>) and the SelfLink key (used by
    /// <see cref="DekCache.GetOrAddRawDekAsync"/>) so a stale raw DEK is never served after a
    /// rewrap or explicit invalidation.
    /// </summary>
    [TestClass]
    public class DekCacheRawDekInvalidationTests
    {
        private const string DekId = "dek-raw-test";
        private const string SelfLink = "dbs/db/colls/coll/clientencryptionkeys/dek-raw-test";

        private static DataEncryptionKeyProperties MakeDekPropertiesWithSelfLink(string id, string selfLink, byte[] wrapped)
        {
            // SelfLink is a system-generated property with internal setter. The established
            // pattern in DekCacheSecurityInvariantsTests is to seed it via a JSON round-trip on
            // a minimal payload (no _ts to avoid the UnixDateTimeConverter integer-only path).
            return Newtonsoft.Json.JsonConvert.DeserializeObject<DataEncryptionKeyProperties>(
                Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    id,
                    _self = selfLink,
                    encryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
                    wrappedDataEncryptionKey = wrapped,
                    keyWrapMetadata = new { type = "test", name = "test", value = "test", algorithm = "RSA-OAEP" },
                }));
        }

        [TestMethod]
        public async Task RemoveAsync_EvictsRawDekCachedUnderDekId()
        {
            using DekCache cache = new(new DekCacheOptions { DekPropertiesTimeToLive = TimeSpan.FromMinutes(30) });

            // Seed properties + raw under dekId via SetDekProperties + SetRawDek.
            DataEncryptionKeyProperties props = MakeDekPropertiesWithSelfLink(DekId, SelfLink, new byte[] { 1 });
            cache.SetDekProperties(DekId, props);
            cache.SetRawDek(DekId, new InMemoryRawDek(null, TimeSpan.FromMinutes(30)));

            await cache.RemoveAsync(DekId);

            // Trigger a fresh unwrap; the unwrapper should be called because raw was evicted.
            int unwrapCalls = 0;
            await cache.GetOrAddRawDekAsync(
                MakeDekPropertiesWithSelfLink(DekId, SelfLink, new byte[] { 1 }),
                (p, ctx, ct) => { unwrapCalls++; return Task.FromResult(new InMemoryRawDek(null, TimeSpan.FromMinutes(30))); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(1, unwrapCalls, "RemoveAsync should evict the raw entry keyed by dekId; the next GetOrAdd must call the unwrapper.");
        }

        [TestMethod]
        public async Task RemoveAsync_EvictsRawDekCachedUnderSelfLink()
        {
            using DekCache cache = new(new DekCacheOptions { DekPropertiesTimeToLive = TimeSpan.FromMinutes(30) });

            // Seed properties first so RemoveAsync's local lookup yields the SelfLink for eviction.
            DataEncryptionKeyProperties props = MakeDekPropertiesWithSelfLink(DekId, SelfLink, new byte[] { 1 });
            cache.SetDekProperties(DekId, props);

            // Seed raw under SelfLink (the GetOrAddRawDekAsync key path).
            int unwrapCalls = 0;
            await cache.GetOrAddRawDekAsync(
                props,
                (p, ctx, ct) => { unwrapCalls++; return Task.FromResult(new InMemoryRawDek(null, TimeSpan.FromMinutes(30))); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);
            Assert.AreEqual(1, unwrapCalls);

            await cache.RemoveAsync(DekId);

            await cache.GetOrAddRawDekAsync(
                props,
                (p, ctx, ct) => { unwrapCalls++; return Task.FromResult(new InMemoryRawDek(null, TimeSpan.FromMinutes(30))); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);
            Assert.AreEqual(2, unwrapCalls, "RemoveAsync should also evict the raw entry keyed by SelfLink.");
        }

        [TestMethod]
        public async Task RemoveAsync_EvictsRawDek_EvenWhenLocalPropertiesAreAbsent()
        {
            using DekCache cache = new(new DekCacheOptions { DekPropertiesTimeToLive = TimeSpan.FromMinutes(30) });

            // Raw keyed by dekId only; properties cache is empty (covers the "RemoveAsync called
            // by a peer where local L1 was never populated" scenario the prior implementation
            // missed because it gated raw eviction on properties cache hit).
            cache.SetRawDek(DekId, new InMemoryRawDek(null, TimeSpan.FromMinutes(30)));

            await cache.RemoveAsync(DekId);

            int unwrapCalls = 0;
            await cache.GetOrAddRawDekAsync(
                MakeDekPropertiesWithSelfLink(DekId, SelfLink, new byte[] { 1 }),
                (p, ctx, ct) => { unwrapCalls++; return Task.FromResult(new InMemoryRawDek(null, TimeSpan.FromMinutes(30))); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(1, unwrapCalls, "RemoveAsync must evict raw unconditionally, not gated on local properties presence.");
        }

        [TestMethod]
        public async Task SourceRefresh_EvictsStaleRawDek_AfterPropertiesChange()
        {
            // Simulates rewrap: same DekId + SelfLink, different wrapped bytes. After the source
            // refresh the raw entry should be re-derived from the new wrapped bytes rather than
            // returning the previously-cached raw key.
            using DekCache cache = new(new DekCacheOptions { DekPropertiesTimeToLive = TimeSpan.FromMinutes(30) });

            DataEncryptionKeyProperties v1 = MakeDekPropertiesWithSelfLink(DekId, SelfLink, new byte[] { 1, 1 });
            DataEncryptionKeyProperties v2 = MakeDekPropertiesWithSelfLink(DekId, SelfLink, new byte[] { 2, 2 });

            // Initial: properties + raw both populated.
            int unwrapCalls = 0;
            cache.SetDekProperties(DekId, v1);
            await cache.GetOrAddRawDekAsync(
                v1,
                (p, ctx, ct) => { unwrapCalls++; return Task.FromResult(new InMemoryRawDek(null, TimeSpan.FromMinutes(30))); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);
            Assert.AreEqual(1, unwrapCalls);

            // Source refresh produces v2 (rewrap). The fetcher returns v2 from "Cosmos".
            await cache.RemoveAsync(DekId); // simulate cache eviction prior to a re-fetch
            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => Task.FromResult(v2),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Next raw lookup must re-unwrap from v2.
            await cache.GetOrAddRawDekAsync(
                v2,
                (p, ctx, ct) => { unwrapCalls++; return Task.FromResult(new InMemoryRawDek(null, TimeSpan.FromMinutes(30))); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(2, unwrapCalls, "Source refresh of properties must invalidate the raw DEK so the next lookup re-unwraps from the new wrapped bytes.");
        }
    }
}
