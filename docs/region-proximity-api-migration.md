# Spec: Migrate from `RegionProximityUtil` Static Table to Server-Provided Region Proximity

| Section | Contents |
| --- | --- |
| [1. Overview](#1-overview) | Why the static table is being replaced, and current status |
| [2. Server Contract](#2-server-contract) | Request, response, degraded modes |
| [3. .NET Implementation](#3-net-implementation) | Proposed code changes |
| [4. Rollout](#4-rollout) | Three phases and their exit criteria |
| [5. Open Decisions](#5-open-decisions) | **Blocking** — read before starting Phase 2 |
| [6. Tests](#6-tests) | Unit coverage |
| [7. Wire Format](#7-wire-format) | Example account response |

---

## 1. Overview

### 1.1 Current state

`CosmosClientOptions.ApplicationRegion` orders `PreferredLocations` by geographic proximity using **`RegionProximityUtil`** — a static region-to-region RTT table compiled into the SDK binary ([server copy](https://msdata.visualstudio.com/CosmosDB/_git/CosmosDB?path=/Product/Microsoft.Azure.Documents/SharedFiles/RegionProximityUtil.cs); SDK copy at `Microsoft.Azure.Cosmos/src/direct/RegionProximityUtil.cs` on `msdata/direct`).

Shipping the table in the binary makes it stale by construction: a new Azure region needs an SDK release, `SetCurrentLocation` throws for any region the table predates, and the same data must stay in sync across the server and .NET `direct` layer. The table is also account-blind — it ranks all Azure regions regardless of what the account actually provisions.

### 1.2 Proposed change

Move steady-state ordering server-side: the gateway returns an account-filtered, proximity-ordered `regionProximity` list on `GET /`, and the .NET SDK consumes it after account metadata arrives. Pre-metadata regional discovery remains client-side so a client can initialize when the global endpoint is unavailable; Phase 3 removes RTT-based ordering, not regional bootstrap. The checked-in Prod backfill reuses the static table's RTT values verbatim, so the gain is updates without an SDK release and account filtering, not accuracy.

### 1.3 Status

Server support exists behind a feature flag. **No .NET SDK code has landed.** A server-side defect makes the ordered path return an empty list, and a second one mis-sorts the ranking once that is fixed, so Phase 2 is blocked until both land — see §5.1 and §5.2.

### 1.4 Out of scope

`RegionProximityUtil.GetRegionsForLinkType()` — used by the direct transport layer for N-region synchronous commit (Strong <100 ms, Medium <200 ms) — requires a separate server API for link-type classification and is unaffected by this spec.

---

## 2. Server Contract

### 2.1 Request

The .NET SDK opts in per request by adding a query parameter to `GET /` (GetDatabaseAccount):

```
GET https://myaccount.documents.azure.com/?regionproximitysourceregion=eastus
```

|  |  |
| --- | --- |
| Name | `regionproximitysourceregion` (`Constants.RegionProximity.SourceRegionQueryParam`) |
| Value | The region the application is running in, **sanitized** |
| Sanitization | `StringUtil.SanitizeString(s)` is exactly `s.Replace(" ", "").ToLowerInvariant()` — spaces removed, then lowercased, nothing else stripped. `"East US"` → `"eastus"`, `"Australia Central 2"` → `"australiacentral2"` |

Omitting the parameter does **not** suppress the field — the server returns a non-empty but *unordered* list (§2.3).

### 2.2 Response

The gateway populates `regionProximity` only when `ConfigurationProperties.EnableRegionProximityData` (bool, default `false`) is enabled for the environment. The flag is **never echoed on the wire**. An absent field or empty array means no usable ranking, not proof that the flag is disabled. Retain the prior ordering (initially the bootstrap order) while reconciling current account destinations (§3.5).

|  |  |
| --- | --- |
| JSON key | `regionProximity` (`Constants.Properties.RegionProximity`) |
| Type | JSON array of **sanitized** region names — `"eastus"`, never `"East US"` |
| Ordering | Ascending by RTT from the source region, **only** when a recognized source region was supplied |
| Filtering | Ranked global list intersected with the **union** of the account's writable and readable locations; completeness is not guaranteed |

> ⚠️ **Response values are sanitized; account location names are not.** `writableLocations[].name` is `"East US"` while the matching proximity entry is `"eastus"`. The .NET SDK must map entries back to account display names before updating `PreferredLocations` (§3.5).

Computed by `DatabaseAccountHandler.GetRegionProximityDataForDatabaseAccountAsync`, which reads a per-source-region configuration key (`regionproximity_<sanitized-source>`) through an `AsyncTimeCache` and intersects the ranked result with the account's regions. `Enumerable.Intersect` preserves the order of the ranked list.

### 2.3 Degraded modes

`GetRegionProximityDataForDatabaseAccountAsync` wraps its whole body in `try/catch` and never fails the request, producing four distinct outcomes:

| Condition | Response | Proximity-ordered? |
| --- | --- | --- |
| Recognized source region | ranked ∩ account regions | Yes — but empty in practice, see §5.1 |
| Parameter missing, empty, or whitespace-only | all account regions, deduped, sanitized | No |
| Lookup throws | all account regions, deduped, sanitized (catch block) | No |
| Unknown region or empty config, no throw | `[]` | n/a |

> ⚠️ **The wire format carries no discriminator.** Rows 1 and 3 are indistinguishable in shape — a non-empty array of sanitized regions. If the .NET SDK applies "non-empty ⇒ trust the ordering," it will silently substitute an arbitrary ordering for a good one whenever the server-side lookup fails, and emptiness — the only signal the fallback rules rely on — does not fire. Open decision: §5.3.

---

## 3. .NET Implementation

> **Status: proposed.** `RegionProximityInternal`, `ReplacePreferredLocations`, and `ParseRegionProximityFromAdditionalProperties` are additions, not existing SDK APIs.

### 3.1 Parse the field

*Files: `Resource/Settings/AccountProperties.cs`, `Routing/GlobalEndpointManager.cs`*

`regionProximity` arrives as a top-level key on the wire, but the SDK must **not** bind it to a declared property. Leave it in the `[JsonExtensionData]` `AdditionalProperties` bag and parse it explicitly.

```csharp
// AccountProperties.cs - mirrors ThinClientWritableLocationsInternal.
// Initialized in the constructor so an absent field reads as empty, never null.
[JsonIgnore]
internal Collection<string> RegionProximityInternal { get; set; }
```

```csharp
// GlobalEndpointManager.cs - mirrors ParseThinClientLocationsFromAdditionalProperties.
private static void ParseRegionProximityFromAdditionalProperties(AccountProperties databaseAccount)
{
    if (databaseAccount?.AdditionalProperties != null
        && databaseAccount.AdditionalProperties.TryGetValue("regionProximity", out JToken token)
        && token is JArray array)
    {
        Collection<string> result = new Collection<string>();
        foreach (JToken entry in array)
        {
            if (entry.Type == JTokenType.String && !string.IsNullOrEmpty(entry.ToString()))
            {
                result.Add(entry.ToString());
            }
        }

        databaseAccount.RegionProximityInternal = result;
    }
}
```

An absent or wrong-typed value leaves the constructor-initialized empty collection in place, so the presence gate resolves to off with no extra branch. A malformed field must not fail the account read; non-string or empty array entries are skipped.

### 3.2 Send the query parameter

*Files: `GatewayAccountReader.cs`, `DocumentClient.cs`*

```csharp
internal static Uri AddRegionProximitySourceRegion(Uri serviceEndpoint, string currentLocation)
{
    if (string.IsNullOrEmpty(currentLocation))
    {
        return serviceEndpoint;
    }

    UriBuilder builder = new UriBuilder(serviceEndpoint);
    NameValueCollection query = HttpUtility.ParseQueryString(builder.Query);
    query["regionproximitysourceregion"] = RegionNameMapper.SanitizeRegionName(currentLocation);
    builder.Query = query.ToString();
    return builder.Uri;
}
```

Put this shared helper on `GatewayAccountReader` and call it before authorization and request construction in both account-read paths:

1. `GatewayAccountReader.GetDatabaseAccountAsync` — initialization, including regional recovery attempts.
2. `DocumentClient.GetDatabaseAccountPrivateAsync` — forced and background refreshes.

Authorization and HTTP dispatch MUST use the same augmented URI. The helper preserves existing query parameters and returns the original URI when `CurrentLocation` is absent. `RegionNameMapper.SanitizeRegionName` is new; mirror `StringUtil.SanitizeString` exactly (§2.1). Keep the query key local to avoid changing the separately versioned Direct package solely for a constant.

Do not retry without the parameter on account-read failure: the gateway swallows proximity errors, and older gateways ignore unknown parameters. A second request would mask genuine auth or throttling failures.

### 3.3 Track the current location

*File: `ConnectionPolicy.cs`*

Add `internal string CurrentLocation { get; private set; }`, set by `SetCurrentLocation`, so both account-read paths can build the query parameter (§3.2) and `GlobalEndpointManager` can tell that the user chose `ApplicationRegion` (§3.5).

**Unchanged through Phase 2**: the static table validates the source and supplies bootstrap candidates before the first account read.

### 3.4 Define source validation

*File: `ConnectionPolicy.cs`*

`SetCurrentLocation` currently throws `ArgumentException` for any region missing from `RegionProximityUtil.SourceRegionToTargetRegionsRTTInMs`. Preserve that behavior through Phase 2.

In Phase 3, accept any non-whitespace source string and treat validation as best effort. Do **not** validate against `regionProximity`: it contains account destinations, while `ApplicationRegion` explicitly may be outside the account's provisioned regions. Do not switch the check to `Regions` either; that list is also compiled into the SDK and preserves the same new-region staleness.

Removing the exception for an unknown non-whitespace source is a customer-visible behavior change. Document it in `changelog.md`; absence of a usable ranking means "proximity unavailable," not "invalid source."

### 3.5 Apply and refresh

*File: `Routing/GlobalEndpointManager.cs`*

Use one helper for init and refresh. The current preferences retain the last resolved order (initially the bootstrap order), so no duplicate ordering cache is needed:

```csharp
private readonly bool isRegionProximityEnabled =
    ConfigurationManager.IsRegionProximityEnabled(defaultValue: true);

private void TryApplyServerProximity(AccountProperties accountProperties)
{
    if (string.IsNullOrEmpty(this.connectionPolicy.CurrentLocation))
    {
        return;
    }

    List<string> destinations = accountProperties.WritableRegions
        .Concat(accountProperties.ReadableRegions)
        .Select(region => region.Name)
        .Where(name => !string.IsNullOrEmpty(name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (destinations.Count == 0)
    {
        return;
    }

    Dictionary<string, string> displayNameBySanitized = destinations
        .ToDictionary(
            name => RegionNameMapper.SanitizeRegionName(name),
            StringComparer.OrdinalIgnoreCase);

    bool canAdoptRanking = this.isRegionProximityEnabled
        && (accountProperties.RegionProximityInternal?.Count ?? 0) > 0;

    List<string> ranked = canAdoptRanking
        ? accountProperties.RegionProximityInternal
            .Where(displayNameBySanitized.ContainsKey)
            .Select(sanitized => displayNameBySanitized[sanitized])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        : new List<string>();

    IEnumerable<string> orderedPrefix;
    if (ranked.Count > 0)
    {
        orderedPrefix = ranked;
    }
    else
    {
        orderedPrefix = this.connectionPolicy.PreferredLocations
            .Where(region => destinations.Contains(region, StringComparer.OrdinalIgnoreCase));
    }

    List<string> resolved = orderedPrefix
        .Concat(destinations)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (!resolved.SequenceEqual(
        this.connectionPolicy.PreferredLocations,
        StringComparer.OrdinalIgnoreCase))
    {
        this.connectionPolicy.ReplacePreferredLocations(resolved);
    }
}
```

Use only a ranking requested with `ApplicationRegion`: without the source parameter, the server list is unordered (§2.3). Map entries using writable/readable names from the same account response, not the compiled-in region list; `RegionNameMapper.GetCosmosDBRegionName` cannot recover display names for unknown regions. The response supplies both display names and the complete destination set. A partial ranking cannot remove destinations; a missing, disabled, or zero-mapped ranking preserves the prior order only for surviving regions and appends new regions in writable-then-readable account order. This applies from the first account response, not only after a ranking has succeeded.

Reconciliation reduces `PreferredLocationCount` from the bootstrap list to account regions, even with ranking adoption disabled. `ClientRetryPolicy` allows at most one unavailable-endpoint retry for eligible requests, and none when the count is at most one; `MetadataRequestThrottleRetryPolicy` allows `Math.Max(1, count)` such retries. Document the resulting retry reductions in `changelog.md`.

On **both** init and refresh, parse the field (§3.1), update account topology through `LocationCache.OnDatabaseAccountRead`, then run this helper. Background refresh runs every five minutes by default (`DefaultBackgroundRefreshLocationTimeIntervalInMS`). Add `ConfigurationManager.IsRegionProximityEnabled` following `IsThinClientEnabled`; initialize it as shown and trace unavailable ranking adoption once. Absence of the field does not prove the server flag is disabled (§2.2).

`CosmosClientOptions.ValidateLimitToEndpointSettings` already rejects combining `ApplicationRegion` with a non-empty `ApplicationPreferredRegions`, so the `CurrentLocation` guard leaves explicit preferences untouched.

`ReplacePreferredLocations` must publish a complete snapshot with one `PreferenceChanged` notification. Make that existing event policy-owned rather than forwarding subscriptions to the replaceable collection; preserve notifications for direct list edits and unsubscribe correctly on disposal. Holders of the old instance, such as client telemetry's `preferredRegions`, must read the current snapshot.

`LocationCache.UpdateLocationCache` must also compute endpoints and effective preferences from **`nextLocationInfo`**, including the replacement list. `GetPreferredAvailableEndpoints` currently reads the old `this.locationInfo`; a collection swap alone would leave routing one update behind. Cover normal and thin-client endpoints without changing write eligibility.

### 3.6 File summary

| File (under `Microsoft.Azure.Cosmos/src/`) | Phase | Change |
| --- | --- | --- |
| `Resource/Settings/AccountProperties.cs` | 1 | Add `RegionProximityInternal` as `[JsonIgnore]` (§3.1) |
| `Routing/GlobalEndpointManager.cs` | 1, 2 | Parse on init and refresh; apply a complete ordering and reconcile topology (§3.5) |
| `Routing/LocationCache.cs` | 2 | Compute endpoints from the incoming preference snapshot, not the previous one (§3.5) |
| `GatewayAccountReader.cs` | 2 | Add shared query construction and use it during initialization (§3.2) |
| `DocumentClient.cs` | 2 | Use the shared query construction for forced and background refreshes (§3.2) |
| `ConnectionPolicy.cs` | 2, 3 | Track `CurrentLocation`; replace preferences atomically; seed RTT-free bootstrap and change validation in Phase 3 (§3.3–§4.3) |
| `CosmosClientOptions.cs` | 3 | Update the `ApplicationRegion` remarks, which promise proximity-ordered recovery (§4.3) |
| `RegionNameMapper.cs` | 2 | Add the forward (display → sanitized) transform |
| `Util/ConfigurationManager.cs` | 2 | Add `IsRegionProximityEnabled`, defaulting to on (§3.5) |
| `direct/RegionProximityUtil.cs`, `RegionProximityUtilProxy.cs` (Friends repo) | 3 | Audit remaining consumers; retain shared helpers/data required by `GetRegionsForLinkType` (§1.4) |

---

## 4. Rollout

### 4.1 Phase 1 — parse only

Add `RegionProximityInternal` and `ParseRegionProximityFromAdditionalProperties` (§3.1). Store the server data; change no behavior. Server-side support already exists behind `EnableRegionProximityData`.

### 4.2 Phase 2 — consume

**Blocked on the server fixes in §5.1 and §5.2.**

**Server**, in order:

1. Sanitize destination keys so the ranked list can intersect account regions at all (§5.1)
2. Sort proximity values numerically rather than lexicographically (§5.2)
3. Enable `EnableRegionProximityData` in production
4. Monitor `LogRegionProximityFailureMetric`

**.NET SDK**:

1. Store `CurrentLocation` from `SetCurrentLocation` (§3.3)
2. Send the query parameter through shared construction on initial, forced, and background reads (§3.2)
3. Map sanitized names back to account display names (§3.5)
4. Complete partial rankings with every account destination and reconcile topology on refresh (§3.5)
5. Apply atomically on init and refresh, skipping no-op rewrites (§3.5)

**Backward compatibility**: without a source region, leave preferences unchanged. With one but no usable ranking, preserve the prior relative order while completing current topology (§3.5). Non-empty alone does not prove proximity ordering — see §5.3.

### 4.3 Phase 3 — remove RTT-based client ordering

Stop calling `GeneratePreferredRegionList` from `SetCurrentLocation`. Instead, seed `PreferredLocations` with the source region first, followed by SDK-known `Regions` sorted ordinally by display name and de-duplicated case-insensitively. `GatewayAccountReader.InitializeReaderAsync` continues passing these candidates to `GetDatabaseAccountFromAnyLocationsAsync`; preserve the existing global/custom-endpoint attempts. Update the `ApplicationRegion` remarks and `changelog.md`: recovery candidates are no longer proximity-ordered, and steady-state ordering is proximity-based only when a ranking arrives.

Accept unknown non-whitespace sources for the query (§3.4). Before hostname construction, require the sanitized source candidate to be a valid DNS label; trace and skip invalid or unconstructible candidates without rejecting the query source. Source text must not alter the URI structure. After the first account read, reconcile all account destinations whether or not a ranking arrives (§3.5).

This removes the client's RTT-based ordering, **not the shared RTT data**: `GetRegionsForLinkType` still indexes `SourceRegionToTargetRegionsRTTInMs`. Deleting shared helpers or tables requires a separate migration of remaining consumers (§1.4).

**Exit criteria** — required before retiring RTT-based client fallback:

- §5.1 and §5.2 resolved: the ordered path returns a non-empty, numerically ordered list
- §5.3 resolved: a degraded response is distinguishable from a proximity-ordered one
- With the global endpoint unavailable and no custom initialization endpoints, an `ApplicationRegion` client can obtain account metadata through a generated regional candidate
- Unknown non-whitespace source regions no longer throw; this customer-visible validation change is documented in `changelog.md`
- `EnableRegionProximityData` enabled in every environment, **including sovereign clouds** (Fairfax, Mooncake) — the configuration is per-environment, with per-cloud backfill data
- A `regionproximity_<region>` configuration exists for every source region supported by the service, including regions newer than the SDK's `Regions`; a missing backfill silently degrades clients in that region
- `LogRegionProximityFailureMetric` at roughly zero across all regions over a sustained window — a service-side measurement, since the SDK has no equivalent telemetry today
- A canary account per cloud returns an ordering matching the static table for known-good region pairs, proving the ordering is *correct* rather than merely present

---

## 5. Open Decisions

§5.1 and §5.2 are pre-existing server defects and block Phase 2. §5.3 is a contract gap: Phase 2 can ship without it, but only as a best-effort ordering, and it must be closed before Phase 3 retires RTT-based client fallback. None of the three is fixable in the .NET SDK.

### 5.1 Blocking Phase 2 — destination keys are never sanitized

`UpsertRegionProximityWorkflow` standardizes only the *source* region (`this.sourceRegion = this.StandardizeRegionString(sourceRegion)`); destination keys are stored verbatim (`regionProximityValues.ToList().ForEach(kvp => regionProximityDataCollection.Add(kvp.Key, kvp.Value.ToString()))`). The checked-in backfill data uses display names — `"East US"`, `"Australia Central 2"` — and `RegionalConfiguration`, a `NameValueCollection`, preserves them.

`GetRankedRegionProximityDataAsync` builds the ranked list from those keys, and the handler then evaluates `rankedRegions.Intersect(accountRegions.Select(SanitizeString))`: `"East US"` against `"eastus"`, under `Enumerable.Intersect`'s default ordinal comparer. **Nothing matches**, so the recognized-source path returns an empty collection.

Consequence: row 1 of the §2.3 table collapses into row 4, and the wire format in §7 is never produced by the ordered path. With the flag on, the feature silently no-ops. This is the blocking item — the .NET SDK work cannot be validated against a path that always returns nothing.

### 5.2 Blocking Phase 2 — RTT values are sorted as strings

Values are stored via `kvp.Value.ToString()` and ranked with `.OrderBy(item => item.Value)`, a lexicographic sort. The backfill values `0, 2, 6, 18, 205, 314` yield `"0", "18", "2", "205", "314", "6"`.

Fixing §5.1 exposes this one, so both must land before Phase 2 rollout.

### 5.3 Blocking Phase 3 — no discriminator for degraded responses

Per §2.3 the server returns an unordered full region list when the proximity lookup is skipped or throws, and it is indistinguishable on the wire from a genuinely ordered one. The .NET SDK therefore risks adopting an arbitrary ordering as if it were proximity-ranked, and the failure surfaces only as latency. Options:

1. Return an explicit flag (e.g. `regionProximityOrdered: true`) alongside the array — unambiguous, but needs a wire change.
2. Omit the field entirely when no ranking was computed, so absence means "no data" — reuses the existing absent-field path, but changes the current fallback contract.
3. Leave as-is and infer ordering from whether the .NET SDK sent a source region — cheapest, but still wrong for the exception path.

Option 2 is the smallest correct change and needs no new field. Until this is resolved, treat Phase 2's ordering guarantee as best-effort. The ambiguity is latent while §5.1 stands, since row 1 returns an empty array today.

---

## 6. Tests

The .NET SDK needs the following coverage.

| Test | Location |
| --- | --- |
| `regionProximity` parsed out of `AdditionalProperties` into `RegionProximityInternal` | `GlobalEndpointManagerTest.cs` |
| Absent field yields an empty collection and no error | `GlobalEndpointManagerTest.cs` |
| Malformed value (wrong JSON type, or an array with non-string entries) does not fail the account read; bad entries are dropped, the rest kept | `GlobalEndpointManagerTest.cs` |
| Sanitized entries map to account display names before use, including a region absent from the SDK's `Regions` class | `GlobalEndpointManagerTest.cs` |
| The switch defaults on; when disabled it prevents ranking adoption but not topology reconciliation | `GlobalEndpointManagerTest.cs` (Phase 2) |
| Initial and refresh account reads carry `?regionproximitysourceregion=eastus` when `ApplicationRegion = "East US"`, omit it when unset, and preserve existing query parameters | `GatewayAccountReaderTests.cs`, `CosmosClientTests.cs` (Phase 2) |
| A partial or duplicate ranking is de-duplicated and completed with all writable/readable account regions | `GlobalEndpointManagerTest.cs` (Phase 2) |
| First response with absent, disabled, or zero-mapped ranking preserves bootstrap-relative order and includes destinations newer than the SDK | `GlobalEndpointManagerTest.cs` (Phases 2, 3) |
| A refresh without usable proximity retains surviving ranked regions, removes deprovisioned regions, and appends new regions | `GlobalEndpointManagerTest.cs` (Phase 2) |
| After a partial ranking, an eligible read retries a 503 in a healthy unranked region with partition failover disabled | `ClientRetryPolicyTests.cs`, `GlobalEndpointManagerTest.cs` (Phase 2) |
| After reconciliation, the count equals the account's region count: eligible `ClientRetryPolicy` requests get zero retries for one region and one retry for multiple regions; metadata requests get `Math.Max(1, count)` unavailable-endpoint retries | `ClientRetryPolicyTests.cs`, `MetadataRequestThrottleRetryPolicyTests.cs` (Phase 2) |
| Each replacement notifies once, preserves event subscriptions, and immediately updates normal/thin-client endpoint order without a second cache refresh | `GlobalEndpointManagerTest.cs`, `LocationCacheTests.cs` (Phase 2) |
| Refresh returning an unchanged ordering does not rewrite `PreferredLocations` | `GlobalEndpointManagerTest.cs` (Phase 2) |
| A non-empty response is NOT applied when no source region was sent (§2.3) | `GlobalEndpointManagerTest.cs` (Phase 2) |
| `PreferredLocations` NOT changed when `ApplicationPreferredRegions` is used | `GlobalEndpointManagerTest.cs` (Phase 2) |
| An application source outside the account's provisioned regions still applies the returned destination ranking | `GlobalEndpointManagerTest.cs` (Phase 2) |
| With the global endpoint unavailable, Phase 3 bootstrap reaches account metadata through a generated regional candidate | `GlobalEndpointManagerTest.cs` (Phase 3) |
| An invalid source-derived hostname neither changes URI structure nor aborts discovery through remaining candidates | `GlobalEndpointManagerTest.cs` (Phase 3) |
| A non-whitespace source absent from the static RTT table and `Regions` is accepted | `CosmosClientOptionsUnitTests.cs` (Phase 3) |

---

## 7. Wire Format

```json
{
  "id": "myaccount",
  "writableLocations": [
    { "name": "East US", "databaseAccountEndpoint": "https://myaccount-eastus.documents.azure.com:443/" }
  ],
  "readableLocations": [
    { "name": "East US", "databaseAccountEndpoint": "https://myaccount-eastus.documents.azure.com:443/" },
    { "name": "West US", "databaseAccountEndpoint": "https://myaccount-westus.documents.azure.com:443/" },
    { "name": "North Europe", "databaseAccountEndpoint": "https://myaccount-northeurope.documents.azure.com:443/" }
  ],
  "regionProximity": [ "eastus", "westus", "northeurope" ]
}
```

`regionProximity` is always a subset of `writableLocations` ∪ `readableLocations`.

> ⚠️ This shows the **intended** contract. The ordered path does not produce it today — see §5.1.
