//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Cross-process / cross-instance interop contract for the distributed-cache feature
    /// introduced by PR #5428.
    ///
    /// The feature's stated raison d'etre is "peer A writes L2 -> peer B reads L2". Every
    /// test in this class simulates that explicitly: two independent <see cref="DekCache"/>
    /// instances share a single <see cref="IDistributedCache"/>. One is exercised to write;
    /// a distinct one is exercised to read. L1 in-process state is therefore never shared
    /// between writer and reader, forcing the assertion to go through the L2 byte payload
    /// -- i.e. the interop contract.
    ///
    /// Each test is grounded in a named SOURCE to avoid codifying accidental behavior.
    /// </summary>
    [TestClass]
    public class DekCacheInteropTests
    {
        private const string DekId = "interopDek";
        private const string DefaultCachePrefix = "dek";
        private const string DefaultCacheKey = DefaultCachePrefix + ":" + DekId;

        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);

        // ---------------------------------------------------------------
        // A. Round-trip fidelity across independent DekCache instances
        // ---------------------------------------------------------------

        // REQ: CreatedTime is preserved exactly (UTC, second-precision per UnixDateTimeConverter)
        //      when a payload written by one peer is read by another peer via L2.
        // SOURCE: DekCache.cs:420-426 (DateTimeZoneHandling.Utc, IsoDateFormat) +
        //         DataEncryptionKeyProperties.cs:99-101 (CreatedTime has UnixDateTimeConverter).
        [TestMethod]
        public async Task PeerA_Writes_PeerB_Reads_CreatedTime_PreservedUtc()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);

            DekCache peerA = NewCache(l2, () => now);
            DekCache peerB = NewCache(l2, () => now);

            // Second-precision UTC DateTime (UnixDateTimeConverter truncates to seconds).
            DateTime created = new DateTime(2024, 3, 15, 10, 30, 45, DateTimeKind.Utc);

            DataEncryptionKeyProperties written = MakeDekProperties(DekId, createdTime: created);
            peerA.SetDekProperties(DekId, written);
            await peerA.LastDistributedCacheWriteTask;

            DataEncryptionKeyProperties read = await peerB.GetOrAddDekPropertiesAsync(
                DekId, FailingFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            Assert.IsNotNull(read.CreatedTime, "CreatedTime must survive the L2 round-trip.");
            Assert.AreEqual(created.Ticks, read.CreatedTime.Value.Ticks, "CreatedTime ticks must match exactly.");
            Assert.AreEqual(DateTimeKind.Utc, read.CreatedTime.Value.Kind, "CreatedTime must be UTC after the round-trip.");
        }

        // REQ: A complex EncryptionKeyWrapMetadata (all four fields: type/algorithm/name/value)
        //      survives the L2 round-trip without field loss.
        // SOURCE: DataEncryptionKeyProperties.cs:168-169 (EncryptionKeyWrapMetadata equality
        //         is part of DataEncryptionKeyProperties.Equals) +
        //         EncryptionKeyWrapMetadata.cs:109-116 (equality spans Type, Algorithm, Value, Name).
        [TestMethod]
        public async Task PeerA_Writes_PeerB_Reads_EncryptionKeyWrapMetadata_AllFieldsPreserved()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);

            DekCache peerA = NewCache(l2, () => now);
            DekCache peerB = NewCache(l2, () => now);

            EncryptionKeyWrapMetadata meta = new EncryptionKeyWrapMetadata(
                type: "akv",
                value: "https://vault.example/keys/kek/v1",
                algorithm: "RSA-OAEP-256",
                name: "kekName");

            DataEncryptionKeyProperties written = new DataEncryptionKeyProperties(
                DekId,
                "AEAD_AES_256_CBC_HMAC_SHA256",
                new byte[] { 1, 2, 3, 4, 5 },
                meta,
                new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            peerA.SetDekProperties(DekId, written);
            await peerA.LastDistributedCacheWriteTask;

            DataEncryptionKeyProperties read = await peerB.GetOrAddDekPropertiesAsync(
                DekId, FailingFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            Assert.IsTrue(
                meta.Equals(read.EncryptionKeyWrapMetadata),
                "All four EncryptionKeyWrapMetadata fields (Type, Algorithm, Name, Value) must round-trip through L2.");
        }

        // REQ: A large WrappedDataEncryptionKey survives the L2 round-trip byte-for-byte.
        //      Mutation of wrapped key material across the cache boundary would make
        //      the feature not merely wrong but dangerous (decrypt would fail or, worse, succeed with corruption).
        // SOURCE: PR description (SOURCE-PR-INTENT: "cross-process/cross-instance caching of
        //         Data Encryption Key (DEK) properties") + DataEncryptionKeyProperties.cs:87-88.
        [TestMethod]
        public async Task PeerA_Writes_PeerB_Reads_LargeWrappedDataEncryptionKey_ByteForByte()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);

            DekCache peerA = NewCache(l2, () => now);
            DekCache peerB = NewCache(l2, () => now);

            byte[] wrapped = new byte[1024];
            for (int i = 0; i < wrapped.Length; i++)
            {
                wrapped[i] = (byte)((i * 31) ^ 0xA5);
            }

            DataEncryptionKeyProperties written = MakeDekProperties(DekId, wrappedKey: wrapped);
            peerA.SetDekProperties(DekId, written);
            await peerA.LastDistributedCacheWriteTask;

            DataEncryptionKeyProperties read = await peerB.GetOrAddDekPropertiesAsync(
                DekId, FailingFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            CollectionAssert.AreEqual(
                wrapped,
                read.WrappedDataEncryptionKey,
                "WrappedDataEncryptionKey bytes must be identical after an L2 round-trip.");
        }

        // REQ: A freshly-constructed third peer (never warmed, never shared state in-memory)
        //      can read an entry written by another peer through the L2 byte payload alone.
        //      This is the cross-process correctness claim of the PR.
        // SOURCE: SOURCE-PR-INTENT + CosmosDataEncryptionKeyProvider.cs:79 XML doc
        //         ("cross-process/cross-instance caching").
        [TestMethod]
        public async Task FreshThirdPeer_ReadsEntryWrittenByFirstPeer()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);

            DekCache peerA = NewCache(l2, () => now);
            peerA.SetDekProperties(DekId, MakeDekProperties(DekId, wrappedKey: new byte[] { 0x77, 0x88 }));
            await peerA.LastDistributedCacheWriteTask;

            // Peer C is constructed AFTER peer A wrote. Nothing in L1 is shared.
            DekCache peerC = NewCache(l2, () => now);

            int cosmosCalls = 0;
            DataEncryptionKeyProperties read = await peerC.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(0, cosmosCalls, "Peer C must read from L2 alone; no Cosmos call.");
            CollectionAssert.AreEqual(new byte[] { 0x77, 0x88 }, read.WrappedDataEncryptionKey);
            Assert.AreEqual(DekId, read.Id);
        }

        // ---------------------------------------------------------------
        // B. Field stability / format pinning
        // ---------------------------------------------------------------

        // REQ: The L2 JSON wire format uses the pinned property names "v", "serverProperties",
        //      "serverPropertiesExpiryUtc". These names are the interop contract; renaming them
        //      without a version bump silently breaks peers on older SDKs.
        // SOURCE: DekCache.cs:464-474 (JsonProperty attributes on CachedDekPropertiesDto).
        [TestMethod]
        public async Task RawL2Payload_ContainsPinnedJsonPropertyNames()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache peerA = NewCache(l2, () => now);

            peerA.SetDekProperties(DekId, MakeDekProperties(DekId));
            await peerA.LastDistributedCacheWriteTask;

            byte[] raw = l2.GetRawForTest(DefaultCacheKey);
            Assert.IsNotNull(raw, "L2 must be populated after SetDekProperties.");

            JObject parsed = JObject.Parse(Encoding.UTF8.GetString(raw));

            Assert.IsNotNull(parsed.Property("v"), "JsonProperty 'v' is part of the pinned wire format.");
            Assert.IsNotNull(parsed.Property("serverProperties"), "JsonProperty 'serverProperties' is part of the pinned wire format.");
            Assert.IsNotNull(parsed.Property("serverPropertiesExpiryUtc"), "JsonProperty 'serverPropertiesExpiryUtc' is part of the pinned wire format.");

            Assert.AreEqual(1, (int)parsed["v"], "Current wire version must be v:1.");
        }

        // REQ: The L2 JSON payload must not contain a "$type" polymorphic discriminator.
        //      Allowing polymorphic deserialization exposes the process to well-known Newtonsoft
        //      deserialization-gadget vulnerabilities via a compromised IDistributedCache peer.
        // SOURCE: DekCache.cs:420-426 (TypeNameHandling.None).
        [TestMethod]
        public async Task RawL2Payload_DoesNotContainTypeDiscriminator()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache peerA = NewCache(l2, () => now);

            peerA.SetDekProperties(DekId, MakeDekProperties(DekId));
            await peerA.LastDistributedCacheWriteTask;

            string json = Encoding.UTF8.GetString(l2.GetRawForTest(DefaultCacheKey));

            Assert.IsFalse(
                json.Contains("$type", StringComparison.Ordinal),
                $"L2 JSON must not contain a '$type' discriminator (TypeNameHandling.None). Payload was: {json}");
        }

        // REQ: The L2 JSON encodes serverPropertiesExpiryUtc in a form peer B can
        //      unambiguously interpret as an instant in UTC -- ISO 8601 with a UTC offset/Z,
        //      equal to write-time + TTL.
        // SOURCE: DekCache.cs:420-426 (IsoDateFormat + DateTimeZoneHandling.Utc) +
        //         DekCache.cs:357-359 (expiry = utcNow + TTL).
        [TestMethod]
        public async Task RawL2Payload_ExpiryStampIsIsoUtc_AndEqualsWriteTimePlusTtl()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache peerA = NewCache(l2, () => now);

            peerA.SetDekProperties(DekId, MakeDekProperties(DekId));
            await peerA.LastDistributedCacheWriteTask;

            JObject parsed = JObject.Parse(Encoding.UTF8.GetString(l2.GetRawForTest(DefaultCacheKey)));
            JToken stamp = parsed["serverPropertiesExpiryUtc"];
            Assert.IsNotNull(stamp, "Expiry stamp must be present in the payload.");

            // Newtonsoft in IsoDateFormat renders DateTime as a JSON string, not a raw integer/object.
            Assert.AreEqual(JTokenType.Date, stamp.Type, "Expiry must be encoded as a JSON date/ISO string, not a numeric or arbitrary value.");

            DateTime parsedExpiry = stamp.Value<DateTime>().ToUniversalTime();
            DateTime expected = now + DefaultTtl;

            Assert.AreEqual(
                expected.Ticks,
                parsedExpiry.Ticks,
                $"Expiry stamp must equal write-clock + TTL. expected={expected:o} actual={parsedExpiry:o}");
        }

        // ---------------------------------------------------------------
        // C. Forward-compat: tolerating unknown extra fields
        // ---------------------------------------------------------------

        // REQ: A future SDK may add new JSON fields to the payload. An older peer must still
        //      deserialize such a payload correctly (additive change is forward-compatible).
        // SOURCE: SOURCE-PR-INTENT interpretation (cross-process interop requires forward-
        //         compatibility for the cache to be rolled out heterogeneously) +
        //         DekCache.cs:420-426 serializer settings do NOT set MissingMemberHandling.Error,
        //         so unknown members are ignored by design.
        [TestMethod]
        public async Task L2PayloadWithUnknownExtraField_PeerStillDeserializes()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache peerB = NewCache(l2, () => now);

            // Simulate a future peer's write containing an extra "futureField" alongside
            // the pinned fields. Keep "v" = 1 so version gating does not reject the entry.
            DataEncryptionKeyProperties dek = MakeDekProperties(DekId);
            JObject payload = new JObject
            {
                ["v"] = 1,
                ["serverProperties"] = JObject.FromObject(dek),
                ["serverPropertiesExpiryUtc"] = now.AddMinutes(30),
                ["futureField"] = "some-future-value",
                ["anotherFutureObject"] = new JObject { ["nested"] = 42 },
            };

            l2.SetRawForTest(DefaultCacheKey, Encoding.UTF8.GetBytes(payload.ToString(Formatting.None)));

            int cosmosCalls = 0;
            DataEncryptionKeyProperties read = await peerB.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(DekId, read.Id, "Unknown extra fields must not cause a deserialization failure.");
            Assert.AreEqual(0, cosmosCalls, "A forward-compatible payload must be served from L2, not fall through to Cosmos.");
        }

        // ---------------------------------------------------------------
        // D. Version handling: missing "v" field -> treated as v1
        // ---------------------------------------------------------------

        // REQ: A payload with no "v" field at all (a hypothetical pre-versioning write) must
        //      be treated as version 1 rather than rejected as "unsupported".
        // SOURCE: DekCache.cs:467 (`public int Version { get; set; } = 1;`) -- the class-field
        //         initializer runs in the parameterless ctor that Newtonsoft invokes before
        //         property population, so an absent "v" property leaves Version at 1.
        //         DekCache.cs:451-454 gates on Version != 1 throwing, so missing "v" must NOT throw.
        [TestMethod]
        public async Task L2PayloadMissingVersionField_IsTreatedAsV1_AndServed()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache peerB = NewCache(l2, () => now);

            DataEncryptionKeyProperties dek = MakeDekProperties(DekId);
            JObject payload = new JObject
            {
                // Deliberately no "v" field.
                ["serverProperties"] = JObject.FromObject(dek),
                ["serverPropertiesExpiryUtc"] = now.AddMinutes(30),
            };

            l2.SetRawForTest(DefaultCacheKey, Encoding.UTF8.GetBytes(payload.ToString(Formatting.None)));

            int cosmosCalls = 0;
            DataEncryptionKeyProperties read = await peerB.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(DekId, read.Id, "Payload with missing 'v' must deserialize (default Version = 1).");
            Assert.AreEqual(0, cosmosCalls, "Missing-'v' payload must be served from L2, not rejected as unsupported.");
        }

        // ---------------------------------------------------------------
        // E. Concurrent writers
        // ---------------------------------------------------------------

        // REQ: When two peers race to write the same dekId, a third peer must read ONE of
        //      the two payloads intact -- never a byte-level merge or a deserialization error.
        // SOURCE: IDistributedCache contract is last-write-wins at the byte level; the
        //         DekCache serializer must not introduce any interleaving that would corrupt
        //         the payload. Grounded in SOURCE-PR-INTENT (interop) and the fact that
        //         DekCache.cs:382-390 writes with a single SetAsync call per write.
        [TestMethod]
        public async Task ConcurrentWrites_ByTwoPeers_PeerCReadsOneValidValue()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);

            DekCache peerA = NewCache(l2, () => now);
            DekCache peerB = NewCache(l2, () => now);
            DekCache peerC = NewCache(l2, () => now);

            byte[] valueA = new byte[] { 0xAA, 0xAA };
            byte[] valueB = new byte[] { 0xBB, 0xBB };

            // Kick off both writes concurrently (each SetDekProperties fires a background L2 write).
            peerA.SetDekProperties(DekId, MakeDekProperties(DekId, wrappedKey: valueA));
            peerB.SetDekProperties(DekId, MakeDekProperties(DekId, wrappedKey: valueB));

            await Task.WhenAll(peerA.LastDistributedCacheWriteTask, peerB.LastDistributedCacheWriteTask);

            DataEncryptionKeyProperties read = await peerC.GetOrAddDekPropertiesAsync(
                DekId, FailingFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            bool isA = read.WrappedDataEncryptionKey.Length == valueA.Length
                && read.WrappedDataEncryptionKey[0] == valueA[0]
                && read.WrappedDataEncryptionKey[1] == valueA[1];
            bool isB = read.WrappedDataEncryptionKey.Length == valueB.Length
                && read.WrappedDataEncryptionKey[0] == valueB[0]
                && read.WrappedDataEncryptionKey[1] == valueB[1];

            Assert.IsTrue(
                isA ^ isB,
                $"Peer C must observe exactly one of the two concurrently-written values; got {BitConverter.ToString(read.WrappedDataEncryptionKey)}.");
        }

        // ---------------------------------------------------------------
        // F. Remove propagation across instances
        // ---------------------------------------------------------------

        // REQ: RemoveAsync(dekId) invalidates the L2 entry. A different peer that has never
        //      seen the DEK in-memory must, on the next GetOrAddDekPropertiesAsync, re-fetch
        //      from the source -- and, if the source is down, must surface that error, proving
        //      it was not served stale data from L2.
        // SOURCE: DekCache.cs:222-244 (RemoveAsync always removes the distributed-cache entry,
        //         per the XML doc "Always remove from distributed cache regardless of memory
        //         cache state, since another instance may have populated it").
        [TestMethod]
        public async Task RemoveAsync_ByPeerA_InvalidatesL2_ForPeerB()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);

            DekCache peerA = NewCache(l2, () => now);
            peerA.SetDekProperties(DekId, MakeDekProperties(DekId));
            await peerA.LastDistributedCacheWriteTask;
            Assert.IsTrue(l2.ContainsKey(DefaultCacheKey), "Pre-condition: L2 populated by peer A.");

            await peerA.RemoveAsync(DekId);
            Assert.IsFalse(l2.ContainsKey(DefaultCacheKey), "Peer A's RemoveAsync must invalidate the shared L2 entry.");

            // Peer B is a fresh DekCache with empty L1; with L2 cleared and Cosmos failing,
            // the cross-instance invalidation must surface as a fresh-fetch failure, not a
            // silent hit on an un-invalidated L2.
            DekCache peerB = NewCache(l2, () => now);

            InvalidOperationException thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => peerB.GetOrAddDekPropertiesAsync(
                    DekId, FailingFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None));

            StringAssert.Contains(thrown.Message, "simulated cosmos outage");
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static DateTime NewClock()
        {
            return new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        private static DekCache NewCache(IDistributedCache l2, Func<DateTime> utcNow)
        {
            return new DekCache(
                dekPropertiesTimeToLive: DefaultTtl,
                distributedCache: l2,
                utcNow: utcNow);
        }

        private static DataEncryptionKeyProperties MakeDekProperties(
            string id,
            byte[] wrappedKey = null,
            DateTime? createdTime = null)
        {
            return new DataEncryptionKeyProperties(
                id,
                "AEAD_AES_256_CBC_HMAC_SHA256",
                wrappedKey ?? new byte[] { 1, 2, 3 },
                new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                createdTime ?? new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        private static Task<DataEncryptionKeyProperties> FailingFetcher(string id, CosmosDiagnosticsContext ctx, CancellationToken ct)
        {
            throw new InvalidOperationException("simulated cosmos outage");
        }

        /// <summary>
        /// IDistributedCache test double that honours an injected clock and exposes the raw
        /// byte payload for interop-contract inspection. Mirrors the pattern from
        /// DekCacheResilienceTests so the two test files stay behaviourally aligned.
        /// </summary>
        private sealed class ClockControlledDistributedCache : IDistributedCache
        {
            private readonly ConcurrentDictionary<string, Entry> store = new ConcurrentDictionary<string, Entry>();
            private readonly Func<DateTime> utcNow;

            public ClockControlledDistributedCache(Func<DateTime> utcNow)
            {
                this.utcNow = utcNow;
            }

            public byte[] Get(string key) => this.GetAsync(key).GetAwaiter().GetResult();

            public Task<byte[]> GetAsync(string key, CancellationToken token = default)
            {
                if (this.store.TryGetValue(key, out Entry entry))
                {
                    if (entry.AbsoluteExpiration.HasValue
                        && entry.AbsoluteExpiration.Value.UtcDateTime <= this.utcNow())
                    {
                        this.store.TryRemove(key, out _);
                        return Task.FromResult<byte[]>(null);
                    }

                    return Task.FromResult(entry.Value);
                }

                return Task.FromResult<byte[]>(null);
            }

            public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
                => this.SetAsync(key, value, options).GetAwaiter().GetResult();

            public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            {
                this.store[key] = new Entry
                {
                    Value = value,
                    AbsoluteExpiration = options?.AbsoluteExpiration,
                };

                return Task.CompletedTask;
            }

            public void Remove(string key) => this.store.TryRemove(key, out _);

            public Task RemoveAsync(string key, CancellationToken token = default)
            {
                this.Remove(key);
                return Task.CompletedTask;
            }

            public void Refresh(string key) { }

            public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

            public bool ContainsKey(string key) => this.store.ContainsKey(key);

            public void SetRawForTest(string key, byte[] bytes)
            {
                this.store[key] = new Entry { Value = bytes, AbsoluteExpiration = null };
            }

            public byte[] GetRawForTest(string key)
            {
                return this.store.TryGetValue(key, out Entry entry) ? entry.Value : null;
            }

            private sealed class Entry
            {
                public byte[] Value { get; set; }

                public DateTimeOffset? AbsoluteExpiration { get; set; }
            }
        }
    }
}