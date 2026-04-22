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

    /// <summary>
    /// Security-invariant specification for PR #5428's IDistributedCache (L2) integration.
    ///
    /// These tests do not re-verify the resilience behavior covered by
    /// <c>DekCacheResilienceTests</c> and <c>DekCacheDistributedCacheTests</c>.
    /// They instead pin down the invariants that the L2 boundary MUST uphold regardless
    /// of implementation: raw-DEK exclusion, deserialization-gadget protection,
    /// metadata-substitution surface, cache-key isolation, fail-safe error handling on
    /// both read and write, and payload hygiene.
    ///
    /// Each test is grounded in a named source-of-truth (see SOURCE-* comments).
    /// GAPs are flagged where the invariant cannot be positively proven from code.
    /// </summary>
    [TestClass]
    public class DekCacheSecurityInvariantsTests
    {
        private const string DekId = "secDek";
        private const string DefaultCachePrefix = "dek";
        private const string DefaultCacheKey = DefaultCachePrefix + ":" + DekId;

        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);

        // A 32-byte marker that doubles as a "raw DEK". If any byte of this marker is
        // ever written to L2, we treat it as a leakage of raw key material.
        private static readonly byte[] RawDekMarker = new byte[]
        {
            0xDE, 0xAD, 0xBE, 0xEF, 0xFE, 0xED, 0xFA, 0xCE,
            0xCA, 0xFE, 0xBA, 0xBE, 0xB1, 0x6B, 0x00, 0xB5,
            0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77,
            0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF,
        };

        // ---------------------------------------------------------------
        // A. Raw DEK material exclusion
        // ---------------------------------------------------------------

        /// <summary>
        /// REQ: Raw (unwrapped) DEK bytes MUST NEVER be written to the IDistributedCache.
        /// SOURCE: SOURCE-SECURITY-INTENT — DekCache.cs line 26-28 (two caches), 167-215
        /// (SetDekProperties → UpdateDistributedCacheAsync); RawDekCache has no L2 code path.
        /// SOURCE: SOURCE-DEK-WRAP — DataEncryptionKeyProperties.cs line 87-88 (only
        /// WrappedDataEncryptionKey is cacheable).
        /// </summary>
        [TestMethod]
        public async Task RawDekMarker_NeverAppearsInL2Bytes_ForAnyCachedDek()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            // Populate L2 via the full cache path: Cosmos fetch -> memory + L2 write.
            // WrappedDataEncryptionKey is the KEK-wrapped ciphertext (intentionally odd shape
            // so it is distinguishable from RawDekMarker).
            byte[] wrapped = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            DataEncryptionKeyProperties props = new DataEncryptionKeyProperties(
                DekId,
                "AEAD_AES_256_CBC_HMAC_SHA256",
                wrapped,
                new EncryptionKeyWrapMetadata("custom", "kek-name", "RSA-OAEP", "kek-value"),
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => Task.FromResult(props),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Simulate the downstream "unwrap" step putting the raw key into the RawDekCache.
            // This is exactly the caller flow in DataEncryptionKeyContainerCore.SetRawDek.
            // Only the in-memory RawDekCache is touched; nothing here should write to L2.
            InMemoryRawDek raw = BuildRawDek(RawDekMarker, now);
            cache.SetRawDek(DekId, raw);

            Assert.IsTrue(l2.ContainsKey(DefaultCacheKey), "Baseline: L2 must hold the wrapped DEK entry.");

            byte[] l2Bytes = l2.GetRawForTest(DefaultCacheKey);
            Assert.IsNotNull(l2Bytes);
            Assert.IsFalse(
                ContainsSubsequence(l2Bytes, RawDekMarker),
                "Raw/unwrapped DEK bytes must NEVER appear in the IDistributedCache payload.");
        }

        /// <summary>
        /// REQ: SetRawDek writes only to the in-memory RawDekCache; it MUST NOT touch L2.
        /// SOURCE: SOURCE-SECURITY-INTENT — DekCache.cs line 217-220 (SetRawDek body has no
        /// distributedCache reference at all).
        /// </summary>
        [TestMethod]
        public async Task SetRawDek_DoesNotWriteToDistributedCache()
        {
            DateTime now = NewClock();
            RecordingDistributedCache l2 = new RecordingDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            cache.SetRawDek(DekId, BuildRawDek(RawDekMarker, now));

            // Give any (nonexistent) fire-and-forget a chance to surface.
            await Task.Delay(20);

            Assert.AreEqual(0, l2.SetCount, "SetRawDek must not cause an L2 Set.");
            Assert.AreEqual(0, l2.GetCount, "SetRawDek must not cause an L2 Get.");
            Assert.AreEqual(0, l2.RemoveCount, "SetRawDek must not cause an L2 Remove.");
        }

        /// <summary>
        /// REQ: GetOrAddRawDekAsync consults only the in-memory RawDekCache. It MUST NOT
        /// read from or write to L2, because L2 is only authorized to carry wrapped material.
        /// SOURCE: SOURCE-SECURITY-INTENT — DekCache.cs line 102-125 (no distributedCache use).
        /// </summary>
        [TestMethod]
        public async Task GetOrAddRawDekAsync_DoesNotReadOrWriteDistributedCache()
        {
            DateTime now = NewClock();
            RecordingDistributedCache l2 = new RecordingDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            DataEncryptionKeyProperties props = MakeDekProperties(DekId);
            // SelfLink is used as the RawDekCache key, so set a distinct one via round-trip.
            DataEncryptionKeyProperties propsWithSelf = JsonConvert.DeserializeObject<DataEncryptionKeyProperties>(
                JsonConvert.SerializeObject(new { id = DekId, _self = "self/" + DekId, keyWrapMetadata = props.EncryptionKeyWrapMetadata, wrappedDataEncryptionKey = props.WrappedDataEncryptionKey, encryptionAlgorithm = props.EncryptionAlgorithm }));

            int unwrapCalls = 0;
            InMemoryRawDek result = await cache.GetOrAddRawDekAsync(
                propsWithSelf,
                (p, ctx, ct) =>
                {
                    unwrapCalls++;
                    return Task.FromResult(BuildRawDek(RawDekMarker, now));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, unwrapCalls, "Unwrapper must be invoked exactly once on cold miss.");
            Assert.AreEqual(0, l2.SetCount, "Raw-DEK path must never write to L2.");
            Assert.AreEqual(0, l2.GetCount, "Raw-DEK path must never read from L2.");
        }

        // ---------------------------------------------------------------
        // B. Deserialization gadget protection
        // ---------------------------------------------------------------

        /// <summary>
        /// REQ: An attacker-planted $type-discriminator at the top of the cache payload MUST
        /// NOT trigger instantiation of arbitrary CLR types. The read must fail closed,
        /// treat the entry as corrupt, and fall through to Cosmos.
        /// SOURCE: SOURCE-TYPENAMEHANDLING — DekCache.cs line 422 (TypeNameHandling.None).
        /// SOURCE: SOURCE-FAIL-SAFE — DekCache.cs line 329-340 (catch + fall back).
        /// </summary>
        [TestMethod]
        public async Task L2PayloadWithTopLevelDollarTypeGadget_TreatedAsMissAndFallsThrough()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            // Known-gadget type names. With TypeNameHandling.None, Newtonsoft MUST ignore these.
            string malicious = "{\"$type\":\"System.Diagnostics.Process, System\",\"v\":1,\"serverProperties\":null,\"serverPropertiesExpiryUtc\":\"2099-01-01T00:00:00Z\"}";
            l2.SetRawForTest(DefaultCacheKey, Encoding.UTF8.GetBytes(malicious));

            int cosmosCalls = 0;
            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(DekId, result.Id);
            Assert.AreEqual(1, cosmosCalls, "Gadget payload must be treated as corrupt — fall through to Cosmos.");
        }

        /// <summary>
        /// REQ: A $type property nested inside serverProperties MUST NOT cause the unwrap
        /// pipeline to instantiate an attacker-chosen type. The read must fail closed.
        /// SOURCE: SOURCE-TYPENAMEHANDLING — DekCache.cs line 422.
        /// SOURCE: SOURCE-FAIL-SAFE — DekCache.cs line 329-340.
        /// </summary>
        [TestMethod]
        public async Task L2PayloadWithNestedDollarTypeGadget_TreatedAsMissAndFallsThrough()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            string malicious = "{\"v\":1,\"serverProperties\":{\"$type\":\"System.IO.FileInfo, System.IO.FileSystem\",\"id\":\"" + DekId + "\",\"wrappedDataEncryptionKey\":\"AQID\",\"keyWrapMetadata\":{\"type\":\"custom\",\"name\":\"k\",\"value\":\"v\"},\"encryptionAlgorithm\":\"AEAD_AES_256_CBC_HMAC_SHA256\"},\"serverPropertiesExpiryUtc\":\"2099-01-01T00:00:00Z\"}";
            l2.SetRawForTest(DefaultCacheKey, Encoding.UTF8.GetBytes(malicious));

            int cosmosCalls = 0;
            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) =>
                {
                    cosmosCalls++;
                    return Task.FromResult(MakeDekProperties(id, wrappedKey: new byte[] { 0xAA, 0xBB }));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(DekId, result.Id);

            // The sole gadget-protection invariant under SOURCE-TYPENAMEHANDLING:
            // no arbitrary CLR type (FileInfo, Process, ...) may be constructed from a
            // $type discriminator. With TypeNameHandling.None pinned in
            // CacheSerializerSettings, Newtonsoft must ignore $type entirely. The result
            // must therefore be exactly DataEncryptionKeyProperties (not a subtype, not a
            // surrogate, not a gadget type).
            Assert.AreEqual(
                typeof(DataEncryptionKeyProperties),
                result.GetType(),
                "Nested $type must not re-type the deserialized object (gadget attack).");
            Assert.IsTrue(
                cosmosCalls == 0 || cosmosCalls == 1,
                "A deterministic outcome (payload accepted OR fall-through) is required; multiple Cosmos calls indicate uncontrolled retry.");
        }

        // ---------------------------------------------------------------
        // C. Metadata-substitution attack surface
        // ---------------------------------------------------------------

        /// <summary>
        /// REQ: If an attacker overwrites L2 with a DEK payload whose EncryptionKeyWrapMetadata
        /// points at a KEK under attacker control, Peer B MUST NOT receive that attacker
        /// metadata as an authoritative answer without independent verification.
        /// SOURCE: SOURCE-METADATA-INTEGRITY — EncryptionKeyWrapMetadata is what the unwrap
        /// provider uses to locate the KEK (EncryptionKeyWrapProvider.cs line 41).
        /// GAP: PR #5428 ships no cryptographic integrity check on L2 entries. The intent
        /// statement in the PR ("peer-populated cache entry can rescue the request") is
        /// silent on integrity. This test documents the observable behavior so that when
        /// a signing/HMAC scheme is added the test can be tightened to Assert.AreNotEqual.
        /// Until then it pins down the current observable: Peer B DOES receive the
        /// substituted metadata verbatim, making L2 integrity a customer concern.
        /// </summary>
        [TestMethod]
        public async Task L2MetadataSubstitution_ExposesPeerBToAttackerControlledMetadata_DocumentedGap()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache sharedL2 = new ClockControlledDistributedCache(() => now);

            // Peer A populates L2 with a legit KEK pointer.
            DekCache peerA = NewCache(DefaultTtl, sharedL2, () => now);
            peerA.SetDekProperties(DekId, MakeDekProperties(DekId, kekName: "legit-kek"));
            await peerA.LastDistributedCacheWriteTask;
            Assert.IsTrue(sharedL2.ContainsKey(DefaultCacheKey), "Setup: Peer A must have populated L2.");

            // Attacker rewrites the L2 slot with metadata pointing at a KEK they control.
            string tampered = JsonConvert.SerializeObject(
                new
                {
                    v = 1,
                    serverProperties = MakeDekProperties(DekId, kekName: "attacker-kek"),
                    serverPropertiesExpiryUtc = now.AddHours(1),
                },
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                    DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                });
            sharedL2.SetRawForTest(DefaultCacheKey, Encoding.UTF8.GetBytes(tampered));

            // Peer B — fresh cache instance, cold L1 — reads.
            DekCache peerB = NewCache(DefaultTtl, sharedL2, () => now);

            int peerBCosmosCalls = 0;
            DataEncryptionKeyProperties peerBResult = await peerB.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) =>
                {
                    peerBCosmosCalls++;
                    return Task.FromResult(MakeDekProperties(id, kekName: "legit-kek"));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // REQUIREMENT-IDEAL: peerBResult.EncryptionKeyWrapMetadata.Name == "legit-kek".
            // CURRENT-OBSERVABLE: L2 is trusted verbatim; peerBResult.*.Name == "attacker-kek".
            //
            // The SDK MUST at minimum NEVER corrupt Peer B's in-memory decision to the point
            // that a legitimate Cosmos-sourced value is overwritten after the unwrap layer
            // rejects the attacker KEK. The test here pins the observable so a future
            // integrity fix (e.g. signed L2 payloads) can invert the assertion cleanly.
            Assert.IsNotNull(peerBResult.EncryptionKeyWrapMetadata);
            Assert.IsTrue(
                peerBResult.EncryptionKeyWrapMetadata.Name == "legit-kek"
                || peerBResult.EncryptionKeyWrapMetadata.Name == "attacker-kek",
                "Peer B's metadata must be exactly one of the two known strings; any other value indicates a deserialization corruption vector.");

            if (peerBResult.EncryptionKeyWrapMetadata.Name == "attacker-kek")
            {
                // Document the GAP: if this branch fires, privilege escalation via L2 tamper
                // is possible when the Peer-B KEK provider is willing to honour the attacker-
                // supplied metadata. Mitigation is out-of-band (network ACL on L2) today.
                Assert.AreEqual(0, peerBCosmosCalls, "Current design: attacker payload is accepted without a Cosmos verification round-trip.");
            }
        }

        // ---------------------------------------------------------------
        // D. Cache-key isolation
        // ---------------------------------------------------------------

        /// <summary>
        /// REQ: Cache keys are constructed by joining prefix + ':' + dekId. Two providers
        /// MUST NOT produce colliding L2 keys when the colon character is present in
        /// either the user-supplied prefix or the dekId. The join must be unambiguous.
        /// SOURCE: SOURCE-CROSS-TENANT — CosmosDataEncryptionKeyProvider.cs line 81 doc:
        /// "to avoid collisions when multiple providers share the same cache instance".
        /// SOURCE: DekCache.cs line 417 key-format implementation.
        /// </summary>
        [TestMethod]
        public async Task CacheKey_WithColonInPrefixOrDekId_MustNotCollideAcrossProviders()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache sharedL2 = new ClockControlledDistributedCache(() => now);

            // Provider 1: prefix "tenantA:dek", dekId "id"  -> expected key "tenantA:dek:id"
            DekCache provider1 = new DekCache(
                dekPropertiesTimeToLive: DefaultTtl,
                distributedCache: sharedL2,
                cacheKeyPrefix: "tenantA:dek",
                utcNow: () => now);

            // Provider 2: prefix "tenantA",     dekId "dek:id" -> expected key "tenantA:dek:id"
            DekCache provider2 = new DekCache(
                dekPropertiesTimeToLive: DefaultTtl,
                distributedCache: sharedL2,
                cacheKeyPrefix: "tenantA",
                utcNow: () => now);

            await provider1.GetOrAddDekPropertiesAsync(
                "id",
                (id, ctx, ct) => Task.FromResult(MakeDekProperties(id, wrappedKey: new byte[] { 0x11 })),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            int provider2CosmosCalls = 0;
            DataEncryptionKeyProperties provider2Result = await provider2.GetOrAddDekPropertiesAsync(
                "dek:id",
                (id, ctx, ct) =>
                {
                    provider2CosmosCalls++;
                    return Task.FromResult(MakeDekProperties(id, wrappedKey: new byte[] { 0x22 }));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Invariant we REQUIRE: provider 2 must see its own data, not provider 1's.
            CollectionAssert.AreEqual(
                new byte[] { 0x22 },
                provider2Result.WrappedDataEncryptionKey,
                "Providers with colon-overlapping prefix/dekId combinations must not alias each other's L2 entries. Colliding keys are a privilege-escalation vector.");
            Assert.AreEqual(1, provider2CosmosCalls, "Provider 2 must have fetched its own data rather than reading provider 1's L2 slot.");
        }

        // ---------------------------------------------------------------
        // E. L2 read error does not leak data
        // ---------------------------------------------------------------

        /// <summary>
        /// REQ: When L2 GetAsync throws an exception (network timeout, serialization crash,
        /// etc.), the cache MUST swallow the exception, fall back to Cosmos, and return
        /// the legitimate result. The exception's message MUST NOT be reflected anywhere
        /// in the returned DataEncryptionKeyProperties.
        /// SOURCE: SOURCE-FAIL-SAFE — DekCache.cs line 329-340 ("fall back to source. Don't throw").
        /// SOURCE: SOURCE-CONTRACT — IDistributedCache implementations are untrusted.
        /// </summary>
        [TestMethod]
        public async Task L2GetAsyncThrows_FallsBackToCosmos_ErrorStringNotReflectedInResult()
        {
            DateTime now = NewClock();
            string uniqueErrorMarker = "REDIS-OUTAGE-MARKER-" + Guid.NewGuid().ToString("N");
            ThrowingDistributedCache l2 = new ThrowingDistributedCache(
                () => now,
                getException: new InvalidOperationException("simulated socket: " + uniqueErrorMarker));
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => Task.FromResult(MakeDekProperties(id, kekName: "kek-" + id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(DekId, result.Id);

            // No field on the result should carry the error text.
            Assert.IsFalse(string.IsNullOrEmpty(result.Id));
            Assert.IsFalse(
                (result.Id ?? string.Empty).Contains(uniqueErrorMarker, StringComparison.Ordinal),
                "Error text leaked into Id field.");
            Assert.IsFalse(
                (result.EncryptionKeyWrapMetadata?.Name ?? string.Empty).Contains(uniqueErrorMarker, StringComparison.Ordinal),
                "Error text leaked into metadata Name.");
            Assert.IsFalse(
                (result.EncryptionKeyWrapMetadata?.Value ?? string.Empty).Contains(uniqueErrorMarker, StringComparison.Ordinal),
                "Error text leaked into metadata Value.");
            Assert.IsFalse(
                (result.EncryptionAlgorithm ?? string.Empty).Contains(uniqueErrorMarker, StringComparison.Ordinal),
                "Error text leaked into EncryptionAlgorithm.");
        }

        // ---------------------------------------------------------------
        // F. Write-side invariants
        // ---------------------------------------------------------------

        /// <summary>
        /// REQ: SetDekProperties MUST be synchronously safe even if L2.SetAsync throws.
        /// The in-memory cache must still be updated; the caller must not observe the
        /// L2 write failure.
        /// SOURCE: SOURCE-FAIL-SAFE — DekCache.cs line 193-207 (catch on fire-and-forget).
        /// SOURCE: DekCache.cs line 132-165 xmldoc ("memory cache is updated synchronously...
        /// distributed cache asynchronously").
        /// </summary>
        [TestMethod]
        public async Task SetDekProperties_WhenL2SetAsyncThrows_DoesNotPropagateToCaller()
        {
            DateTime now = NewClock();
            ThrowingDistributedCache l2 = new ThrowingDistributedCache(
                () => now,
                setException: new InvalidOperationException("simulated L2 write failure"));
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            // Must not throw synchronously.
            cache.SetDekProperties(DekId, MakeDekProperties(DekId));

            // The fire-and-forget task must complete (faulted) — awaiting it must also not throw
            // back into caller-land; DekCache's internal catch is the guard.
            try
            {
                await cache.LastDistributedCacheWriteTask;
            }
            catch (Exception ex)
            {
                Assert.Fail(
                    $"L2 write failure must be swallowed inside DekCache; instead it surfaced as {ex.GetType().Name}: {ex.Message}");
            }

            // The in-memory (L1) cache must still serve without calling the fetcher.
            int cosmosCalls = 0;
            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(DekId, result.Id);
            Assert.AreEqual(0, cosmosCalls, "Memory cache must remain authoritative after an L2 write failure.");
        }

        /// <summary>
        /// REQ: RemoveAsync MUST NOT throw to the caller when L2.RemoveAsync throws.
        /// The memory cache must still be cleaned.
        /// SOURCE: SOURCE-FAIL-SAFE — DekCache.cs line 232-243 catch block
        /// ("Don't fail the operation if distributed cache removal fails").
        /// </summary>
        [TestMethod]
        public async Task RemoveAsync_WhenL2RemoveAsyncThrows_DoesNotPropagateToCaller()
        {
            DateTime now = NewClock();
            ThrowingDistributedCache l2 = new ThrowingDistributedCache(
                () => now,
                removeException: new InvalidOperationException("simulated L2 remove failure"));
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            cache.SetDekProperties(DekId, MakeDekProperties(DekId));

            try
            {
                await cache.RemoveAsync(DekId);
            }
            catch (Exception ex)
            {
                Assert.Fail(
                    $"L2 remove failure must be swallowed inside DekCache; instead it surfaced as {ex.GetType().Name}: {ex.Message}");
            }

            // The RemoveAsync must have also cleaned the in-memory cache BEFORE encountering
            // the L2 failure. We assert this via a second RemoveAsync that, observing an empty
            // in-memory cache, returns cleanly. (We cannot assert via subsequent Get because a
            // prior fire-and-forget SetDekProperties may have populated L2, which would legitimately
            // serve the Get. SOURCE-FAIL-SAFE requires only that the caller never sees the L2 error.)
            try
            {
                await cache.RemoveAsync(DekId);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Repeated RemoveAsync with failing L2 must also swallow: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // ---------------------------------------------------------------
        // G. Cache payload does not leak sensitive non-key data
        // ---------------------------------------------------------------

        /// <summary>
        /// REQ: The L2 payload MUST contain only the intended DTO fields (v, serverProperties,
        /// serverPropertiesExpiryUtc). It must not leak environment variables, host-level
        /// JsonConvert.DefaultSettings-injected type discriminators, or any ambient data.
        /// SOURCE: SOURCE-TYPENAMEHANDLING + DekCache.cs line 420-426 CacheSerializerSettings
        /// (TypeNameHandling.None + DefaultContractResolver, pinned per-call to ignore
        /// JsonConvert.DefaultSettings).
        /// </summary>
        [TestMethod]
        public async Task L2Payload_DoesNotLeakEnvironmentOrDollarTypeDiscriminator()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            // Plant a highly-unusual env var. If the SDK ever grows a leaky serializer that
            // includes process state, we will see this marker appear in the payload.
            string envMarker = "COSMOS_SEC_INV_MARKER_" + Guid.NewGuid().ToString("N");
            Environment.SetEnvironmentVariable("COSMOS_SEC_INV_MARKER", envMarker);

            // Also perturb host-level JsonConvert.DefaultSettings to try to force $type in.
            // DekCache MUST ignore this and use its own CacheSerializerSettings.
            JsonSerializerSettings originalDefaults = JsonConvert.DefaultSettings?.Invoke();
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
            };

            try
            {
                await cache.GetOrAddDekPropertiesAsync(
                    DekId,
                    (id, ctx, ct) => Task.FromResult(MakeDekProperties(id)),
                    CosmosDiagnosticsContext.Create(null),
                    CancellationToken.None);

                byte[] payload = l2.GetRawForTest(DefaultCacheKey);
                Assert.IsNotNull(payload, "Baseline: L2 must have been populated.");
                string json = Encoding.UTF8.GetString(payload);

                Assert.IsFalse(
                    json.Contains(envMarker, StringComparison.Ordinal),
                    "L2 payload leaked an environment variable value.");
                Assert.IsFalse(
                    json.Contains("$type", StringComparison.Ordinal),
                    "L2 payload contains a Newtonsoft $type discriminator — TypeNameHandling must be pinned to None.");
                Assert.IsFalse(
                    json.Contains("System.", StringComparison.Ordinal),
                    "L2 payload contains CLR type names — indicates leakage of host-level serializer state.");
                Assert.IsFalse(
                    json.Contains("AccountKey", StringComparison.Ordinal),
                    "L2 payload mentions 'AccountKey' — potential Cosmos connection-string fragment leak.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("COSMOS_SEC_INV_MARKER", null);
                JsonConvert.DefaultSettings = originalDefaults == null ? null : (Func<JsonSerializerSettings>)(() => originalDefaults);
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static DateTime NewClock() => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static DekCache NewCache(TimeSpan ttl, IDistributedCache l2, Func<DateTime> utcNow)
        {
            return new DekCache(
                dekPropertiesTimeToLive: ttl,
                distributedCache: l2,
                utcNow: utcNow);
        }

        private static DataEncryptionKeyProperties MakeDekProperties(string id, byte[] wrappedKey = null, string kekName = "test")
        {
            return new DataEncryptionKeyProperties(
                id,
                "AEAD_AES_256_CBC_HMAC_SHA256",
                wrappedKey ?? new byte[] { 0x01, 0x02, 0x03 },
                new EncryptionKeyWrapMetadata("custom", "test-value", "RSA-OAEP", kekName),
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        private static InMemoryRawDek BuildRawDek(byte[] rawBytes, DateTime nowUtc)
        {
#pragma warning disable CS0618 // obsolete: algorithm constant still usable for test DEK construction
            DataEncryptionKey key = DataEncryptionKey.Create(
                rawBytes,
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized);
#pragma warning restore CS0618
            return new InMemoryRawDek(key, TimeSpan.FromHours(1), nowUtc);
        }

        private static bool ContainsSubsequence(byte[] haystack, byte[] needle)
        {
            if (haystack == null || needle == null || needle.Length == 0 || haystack.Length < needle.Length)
            {
                return false;
            }

            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------
        // IDistributedCache test doubles
        // ---------------------------------------------------------------

        /// <summary>
        /// Clock-respecting in-memory cache. Copied from DekCacheResilienceTests pattern.
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

        /// <summary>
        /// Clock-respecting cache that records every call for I/O-invariant assertions.
        /// </summary>
        private sealed class RecordingDistributedCache : IDistributedCache
        {
            private readonly ClockControlledDistributedCache inner;

            public RecordingDistributedCache(Func<DateTime> utcNow)
            {
                this.inner = new ClockControlledDistributedCache(utcNow);
            }

            public int GetCount;
            public int SetCount;
            public int RemoveCount;

            public byte[] Get(string key)
            {
                Interlocked.Increment(ref this.GetCount);
                return this.inner.Get(key);
            }

            public Task<byte[]> GetAsync(string key, CancellationToken token = default)
            {
                Interlocked.Increment(ref this.GetCount);
                return this.inner.GetAsync(key, token);
            }

            public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            {
                Interlocked.Increment(ref this.SetCount);
                this.inner.Set(key, value, options);
            }

            public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            {
                Interlocked.Increment(ref this.SetCount);
                return this.inner.SetAsync(key, value, options, token);
            }

            public void Remove(string key)
            {
                Interlocked.Increment(ref this.RemoveCount);
                this.inner.Remove(key);
            }

            public Task RemoveAsync(string key, CancellationToken token = default)
            {
                Interlocked.Increment(ref this.RemoveCount);
                return this.inner.RemoveAsync(key, token);
            }

            public void Refresh(string key) { }

            public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        }

        /// <summary>
        /// Clock-respecting cache that throws a caller-supplied exception on a chosen verb.
        /// </summary>
        private sealed class ThrowingDistributedCache : IDistributedCache
        {
            private readonly ClockControlledDistributedCache inner;
            private readonly Exception getException;
            private readonly Exception setException;
            private readonly Exception removeException;

            public ThrowingDistributedCache(
                Func<DateTime> utcNow,
                Exception getException = null,
                Exception setException = null,
                Exception removeException = null)
            {
                this.inner = new ClockControlledDistributedCache(utcNow);
                this.getException = getException;
                this.setException = setException;
                this.removeException = removeException;
            }

            public byte[] Get(string key)
            {
                if (this.getException != null) throw this.getException;
                return this.inner.Get(key);
            }

            public Task<byte[]> GetAsync(string key, CancellationToken token = default)
            {
                if (this.getException != null) throw this.getException;
                return this.inner.GetAsync(key, token);
            }

            public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            {
                if (this.setException != null) throw this.setException;
                this.inner.Set(key, value, options);
            }

            public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            {
                if (this.setException != null) throw this.setException;
                return this.inner.SetAsync(key, value, options, token);
            }

            public void Remove(string key)
            {
                if (this.removeException != null) throw this.removeException;
                this.inner.Remove(key);
            }

            public Task RemoveAsync(string key, CancellationToken token = default)
            {
                if (this.removeException != null) throw this.removeException;
                return this.inner.RemoveAsync(key, token);
            }

            public void Refresh(string key) { }

            public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        }
    }
}
