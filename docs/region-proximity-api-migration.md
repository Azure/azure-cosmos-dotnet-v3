# Spec: Migrate from `RegionProximityUtil` Static Table to Server-Provided Region Proximity

| Section | Contents |
| --- | --- |
| [1. Overview](#1-overview) | Why the static table is being replaced, and current status |
| [2. Server Contract](#2-server-contract) | Request, response, degraded modes |
| [3. Cross-SDK Requirements](#3-cross-sdk-requirements) | Normative rules for every SDK |
| [4. .NET Implementation](#4-net-implementation) | Proposed code changes |
| [5. Rollout](#5-rollout) | Three phases and their exit criteria |
| [6. Open Decisions](#6-open-decisions) | **Blocking** — read before starting Phase 2 |
| [7. Tests](#7-tests) | Unit coverage |
| [8. Wire Format](#8-wire-format) | Example account response |

---

## 1. Overview

### 1.1 Current state

`CosmosClientOptions.ApplicationRegion` (and its equivalent in every other SDK) orders `PreferredLocations` by geographic proximity using **`RegionProximityUtil`** — a static region-to-region RTT table compiled into each SDK binary ([server copy](https://msdata.visualstudio.com/CosmosDB/_git/CosmosDB?path=/Product/Microsoft.Azure.Documents/SharedFiles/RegionProximityUtil.cs); SDK copy at `Microsoft.Azure.Cosmos/src/direct/RegionProximityUtil.cs` on `msdata/direct`).

Shipping the table in the binary makes it stale by construction: a new Azure region needs an SDK release, `SetCurrentLocation` throws for any region the table predates, and the same data must stay in sync across the server and every SDK's `direct` layer. The table is also account-blind — it ranks all Azure regions regardless of what the account actually provisions — and its RTTs are estimates rather than measurements.

### 1.2 Proposed change

Move the ordering server-side: the gateway returns an account-filtered, proximity-ordered `regionProximity` list on `GET /`, SDKs consume it, and the static table is demoted to a bootstrap fallback and eventually removed.

### 1.3 Status

Server support exists behind a feature flag. **No SDK code has landed.** A server-side defect makes the ordered path return an empty list, and a second one mis-sorts the ranking once that is fixed, so Phase 2 is blocked until both land — see §6.1 and §6.2.

### 1.4 Out of scope

`RegionProximityUtil.GetRegionsForLinkType()` — used by the direct transport layer for N-region synchronous commit (Strong <100 ms, Medium <200 ms) — requires a separate server API for link-type classification and is unaffected by this spec.

---

## 2. Server Contract

### 2.1 Request

An SDK opts in per request by adding a query parameter to `GET /` (GetDatabaseAccount):

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

The gateway populates `regionProximity` only when `ConfigurationProperties.EnableRegionProximityData` (bool, default `false`) is enabled for the environment. The flag is evaluated server-side and **never echoed on the wire**, so an SDK cannot read it. Absence of the field is the only available signal, and it is ambiguous — an old gateway, the flag being off, and an empty lookup all look identical. All three are handled the same way: keep the static-table ordering (§3.4).

|  |  |
| --- | --- |
| JSON key | `regionProximity` (`Constants.Properties.RegionProximity`) |
| Type | JSON array of **sanitized** region names — `"eastus"`, never `"East US"` |
| Ordering | Ascending by RTT from the source region, **only** when a recognized source region was supplied |
| Filtering | Ranked global list intersected with the **union** of the account's writable and readable locations |

> ⚠️ **Response values are sanitized; account location names are not.** `writableLocations[].name` is `"East US"` while the matching proximity entry is `"eastus"`. Any SDK feeding this list into a preferred-location list keyed by account names must map back to display form first — see §3.2.

Computed by `DatabaseAccountHandler.GetRegionProximityDataForDatabaseAccountAsync`, which reads a per-source-region configuration key (`regionproximity_<sanitized-source>`) through an `AsyncTimeCache` and intersects the ranked result with the account's regions. `Enumerable.Intersect` preserves the order of the ranked list.

### 2.3 Degraded modes

`GetRegionProximityDataForDatabaseAccountAsync` wraps its whole body in `try/catch` and never fails the request, producing four distinct outcomes:

| Condition | Response | Proximity-ordered? |
| --- | --- | --- |
| Recognized source region | ranked ∩ account regions | Yes — but empty in practice, see §6.1 |
| Parameter missing, empty, or whitespace-only | all account regions, deduped, sanitized | No |
| Lookup throws | all account regions, deduped, sanitized (catch block) | No |
| Unknown region or empty config, no throw | `[]` | n/a |

> ⚠️ **The wire format carries no discriminator.** Rows 1 and 3 are byte-identical in shape — a non-empty array of sanitized regions. An SDK that applies "non-empty ⇒ trust the ordering" will silently substitute an arbitrary ordering for a good one whenever the server-side lookup fails, and emptiness — the only signal the fallback rules rely on — does not fire. Open decision: §6.3.

---

## 3. Cross-SDK Requirements

Normative for every SDK that implements `ApplicationRegion` or an equivalent.

### 3.1 Send the source region

- When the user configures `ApplicationRegion`, the SDK MUST append `?regionproximitysourceregion=<sanitized>` to **every** GetDatabaseAccount call — the initial read and all background refreshes.
- When it is not configured, the SDK SHOULD NOT send the parameter.

### 3.2 Parse the response

- Absent, null, or empty MUST be treated as "no server data available" and fall through to §3.4. It is a normal degraded case, not an error.
- A *malformed* value MUST be contained at field granularity: type-check it (ignore the field unless it is a JSON array), skip elements that are not non-empty strings, keep the rest, and never fail the surrounding account read. A client that never opted into proximity must not be broken by a bad `regionProximity`.
- **Each entry MUST be mapped back to the account's display form before use.** Build the map from the *same response's* `writableLocations` / `readableLocations` (`sanitize(name) → name`); the proximity list is always a subset of them. SDKs MUST NOT map through a region table compiled into the binary — .NET's `RegionNameMapper.GetCosmosDBRegionName` returns unknown input unchanged, so it silently fails for exactly the new regions this migration exists to serve.

### 3.3 Apply the ordering

- Use the list only when the SDK sent a source region **and** the response is non-empty. Without a source region the response is not proximity-ordered (§2.3) and MUST NOT be used for ordering.
- The list is already scoped to the account — the SDK MUST NOT filter it against account regions again.
- Explicitly configured `PreferredLocations` / `ApplicationPreferredRegions` always win.
- The ordering MUST be re-resolved on background account refresh (.NET default: 5 minutes, `DefaultBackgroundRefreshLocationTimeIntervalInMS`), and re-application MUST be skipped when the resolved ordering is unchanged — rewriting the collection fires change notifications and forces needless endpoint recomputation.

### 3.4 Fallback and gating

- Bootstrap `PreferredLocations` from `RegionProximityUtil` before the first account read, then override once a usable list arrives. The bootstrap ordering MUST NOT be treated as authoritative or cached beyond that point.
- If a usable list never arrives, keep the static table for the entire session and trace the reason once. Absence is never a failure.
- Gate consumption on **both** a client-side switch and server-side presence — `clientSwitch && (regionProximity?.Count ?? 0) > 0`, defaulting to on client-side. The server half is how the SDK infers the backend flag is disabled, since the flag is never on the wire (§2.2); the client half is the only lever to disable a bad ordering without a service change.
- SDKs MUST NOT add a retry-without-the-parameter path. The gateway swallows all proximity errors internally and gateways predating the feature ignore unknown parameters, so such a path would double account reads and mask genuine auth and throttling failures.
- Region validation MUST move, not disappear — once `regionProximity` is available it becomes the authoritative region set. Dropping the check outright trades a loud startup error for a silent latency regression (§4.4).

---

## 4. .NET Implementation

> **Status: proposed.** None of this exists on `main` today — `RegionProximityInternal`, `SetRegionProximity`, and `ParseRegionProximityFromAdditionalProperties` all return zero matches under `Microsoft.Azure.Cosmos/**/*.cs`.

### 4.1 Parse the field

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

An absent or wrong-typed value leaves the constructor-initialized empty collection in place, so the presence gate resolves to off with no extra branch.

### 4.2 Send the query parameter

*File: `GatewayAccountReader.cs`*

```csharp
// In GetDatabaseAccountAsync, before the HttpClient.GetAsync call:
if (!string.IsNullOrEmpty(this.connectionPolicy.CurrentLocation))
{
    UriBuilder builder = new UriBuilder(serviceEndpoint);
    NameValueCollection query = HttpUtility.ParseQueryString(builder.Query);
    query[HttpConstants.QueryStrings.RegionProximitySourceRegion] =
        RegionNameMapper.SanitizeRegionName(this.connectionPolicy.CurrentLocation);
    builder.Query = query.ToString();
    serviceEndpoint = builder.Uri;
}
```

The SDK has no sanitization helper today — `RegionNameMapper` holds only the inverse transform (`"westus2"` → `"West US 2"`), so `SanitizeRegionName` is new and belongs beside it rather than in a new type. Mirror `StringUtil.SanitizeString` exactly (§2.1) so the two implementations cannot drift. Also add `HttpConstants.QueryStrings.RegionProximitySourceRegion`.

### 4.3 Track the current location

*File: `ConnectionPolicy.cs`*

Add `internal string CurrentLocation { get; private set; }`, set by `SetCurrentLocation`, so `GatewayAccountReader` can build the query parameter (§4.2) and `GlobalEndpointManager` can tell that the user chose `ApplicationRegion` (§4.5).

**Unchanged through Phase 2**: bootstrapping from the static table. If the region is absent from the table, fall back to an empty preferred list and let the account's default ordering apply.

### 4.4 Relocate region validation

*File: `ConnectionPolicy.cs`*

`SetCurrentLocation` currently throws `ArgumentException` for any region missing from `RegionProximityUtil.SourceRegionToTargetRegionsRTTInMs`. That check is the only guard against a typo'd `ApplicationRegion`, so it must be relocated rather than deleted. **Decision required:**

- **(a) Validate late, against server data.** Accept unknown regions at construction; once `regionProximity` arrives, warn if the configured region is absent from it. Fixes staleness, but `SetCurrentLocation` is synchronous, so a typo can no longer be reported to the caller — it becomes a log line.
- **(b) Validate against `Regions` instead of the RTT table.** Keeps the check synchronous and fail-fast, but `Regions` is equally baked into the binary, so this decouples validation from the RTT table without fixing staleness itself.

Neither is strictly better. (b) at construction plus (a) after the first account read is the only combination that covers both, at the cost of two validation sites.

> ⚠️ If the `ArgumentException` stops being thrown for input that throws today, that is a **customer-visible behavior change** — a typo degrades silently to default routing, a latency regression with no error. (`ConnectionPolicy` is `internal sealed`, so it is a behavior break, not a public-API break.) Record it in `changelog.md` under `Breaking Changes` or `Other Changes`.

### 4.5 Apply and refresh

*File: `Routing/GlobalEndpointManager.cs`*

Account init and the background refresh loop need identical logic, so define it once rather than duplicating the block:

```csharp
private void TryApplyServerProximity(AccountProperties accountProperties)
{
    // Only a response to a request that carried a source region is ordered (§2.3).
    // ValidateLimits already rejects ApplicationRegion + ApplicationPreferredRegions
    // together, so this also keeps an explicit preferred-region list from being overridden.
    if (string.IsNullOrEmpty(this.connectionPolicy.CurrentLocation))
    {
        return;
    }

    Collection<string> proximity = accountProperties.RegionProximityInternal;
    if (proximity == null || proximity.Count == 0)
    {
        return;
    }

    // The response itself is the only non-stale source for sanitized → display (§3.2):
    // a table compiled into the SDK cannot contain regions newer than the SDK.
    Dictionary<string, string> displayNameBySanitized = accountProperties.WritableRegions
        .Concat(accountProperties.ReadableRegions)
        .Select(region => region.Name)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            name => name.Replace(" ", string.Empty),
            StringComparer.OrdinalIgnoreCase);

    // Unmapped entries are dropped rather than passed through: they would never match a
    // LocationCache key. This is not the re-filtering §3.3 forbids - the server already
    // scoped the list to the account.
    List<string> mapped = proximity
        .Where(displayNameBySanitized.ContainsKey)
        .Select(sanitized => displayNameBySanitized[sanitized])
        .ToList();

    if (mapped.SequenceEqual(this.connectionPolicy.PreferredLocations, StringComparer.OrdinalIgnoreCase))
    {
        return;
    }

    this.connectionPolicy.SetPreferredLocations(mapped);
}
```

`ParseRegionProximityFromAdditionalProperties` (§4.1) must run before this helper on **both** paths — account init and the background refresh handler — which is exactly where `ParseThinClientLocationsFromAdditionalProperties` is called today. Skipping the refresh call would freeze the ordering at whatever the first account read returned. Combine the `Count == 0` early return above with the client-side switch (§3.4), and trace once when the capability resolves to off, mirroring `DocumentClient`'s thin-client gate.

No new configuration flag is needed for the explicit-preferred-regions rule: `CosmosClientOptions.ValidateLimits` already throws when both `ApplicationRegion` and `ApplicationPreferredRegions` are set, so a non-empty `CurrentLocation` proves the user chose `ApplicationRegion`.

> ⚠️ **`SetPreferredLocations` is not atomic.** It clears the `ObservableCollection` and re-adds one region at a time; `OnPreferenceChanged` fires on each mutation and pushes the *partial* list into `LocationCache.OnLocationPreferenceChanged`. Today that happens once at construction, but re-applying every 5 minutes would make it recurring — which is why the no-op guard is a correctness concern, not just an optimization. If the ordering does legitimately change, prefer an atomic replace over clear-then-fill.

### 4.6 File summary

| File (under `Microsoft.Azure.Cosmos/src/`) | Phase | Change |
| --- | --- | --- |
| `Resource/Settings/AccountProperties.cs` | 1 | Add `RegionProximityInternal` as `[JsonIgnore]` (§4.1) |
| `Routing/GlobalEndpointManager.cs` | 1, 2 | Phase 1: add the parse helper (§4.1). Phase 2: apply on init and refresh via one shared helper (§4.5) |
| `GatewayAccountReader.cs` | 2 | Send the source-region query parameter on every GetDatabaseAccount call (§4.2) |
| `ConnectionPolicy.cs` | 2 | Add `CurrentLocation` (§4.3); relocate region validation (§4.4) |
| `RegionNameMapper.cs` | 2 | Add the forward (display → sanitized) transform |
| `HttpConstants.cs` | 2 | Add `QueryStrings.RegionProximitySourceRegion` |
| `Util/ConfigurationManager.cs` | 2 | Add the client-side kill switch (§3.4) |
| `direct/RegionProximityUtil.cs` | 3 | Drop `GeneratePreferredRegionList` from the `SetCurrentLocation` path |
| `RegionProximityUtilProxy.cs` (Friends repo) | 3 | Remove or reroute to server data |

---

## 5. Rollout

### 5.1 Phase 1 — parse only

Add `RegionProximityInternal` and `ParseRegionProximityFromAdditionalProperties` (§4.1). Store the server data; change no behavior. Server-side support already exists behind `EnableRegionProximityData`.

### 5.2 Phase 2 — consume

**Blocked on the server fixes in §6.1 and §6.2.**

**Server**, in order:

1. Sanitize destination keys so the ranked list can intersect account regions at all (§6.1)
2. Sort proximity values numerically rather than lexicographically (§6.2)
3. Enable `EnableRegionProximityData` in production
4. Monitor `LogRegionProximityFailureMetric`

**SDK** (all SDKs):

1. Store `currentLocation` from `SetCurrentLocation` (§4.3)
2. Send the query parameter on initial and background reads (§4.2)
3. Map sanitized names back to account display names (§3.2)
4. Apply to `PreferredLocations` on init and refresh, skipping no-op rewrites (§4.5)
5. Relocate `SetCurrentLocation` validation per the §4.4 decision

**Backward compatibility**: no source region sent, or the field absent or empty, keeps the static-table ordering. Non-empty alone is not sufficient — see §6.3.

### 5.3 Phase 3 — remove the static table

Remove `GeneratePreferredRegionList` from the `SetCurrentLocation` path, leaving `SetCurrentLocation` to store only the location name for query-parameter construction. `GetRegionsForLinkType` stays in `RegionProximityUtil` (out of scope, §1.4). Update `RegionProximityUtilProxy` for Interop APIs.

**Exit criteria** — each independently checkable, since this phase deletes the fallback:

- §6.1 and §6.2 resolved: the ordered path returns a non-empty, numerically ordered list
- §6.3 resolved: a degraded response is distinguishable from a proximity-ordered one
- `EnableRegionProximityData` enabled in every environment, **including sovereign clouds** (Fairfax, Mooncake) — the configuration is per-environment, with its own backfill scripts per cloud
- A `regionproximity_<region>` configuration exists for **every** region in `Regions`; a missing backfill silently degrades every client in that region, so presence must be asserted, not assumed
- `LogRegionProximityFailureMetric` at roughly zero across all regions over a sustained window — a service-side measurement, since the SDK has no equivalent telemetry today
- A canary account per cloud returns an ordering matching the static table for known-good region pairs, proving the ordering is *correct* rather than merely present

---

## 6. Open Decisions

§6.1 and §6.2 are pre-existing server defects, verified against `master`, and block Phase 2. §6.3 is a contract gap: Phase 2 can ship without it, but only as a best-effort ordering, and it must be closed before Phase 3 deletes the fallback. None of the three is fixable from any SDK.

### 6.1 Blocking Phase 2 — destination keys are never sanitized

`UpsertRegionProximityWorkflow` standardizes only the *source* region (`this.sourceRegion = this.StandardizeRegionString(sourceRegion)`); destination keys are stored verbatim (`regionProximityValues.ToList().ForEach(kvp => regionProximityDataCollection.Add(kvp.Key, kvp.Value.ToString()))`). The checked-in backfill data uses display names — `"East US"`, `"Australia Central 2"` — and `RegionalConfiguration`, a `NameValueCollection`, preserves them.

`GetRankedRegionProximityDataAsync` builds the ranked list from those keys, and the handler then evaluates `rankedRegions.Intersect(accountRegions.Select(SanitizeString))`: `"East US"` against `"eastus"`, under `Enumerable.Intersect`'s default ordinal comparer. **Nothing matches**, so the recognized-source path returns an empty collection.

Consequence: row 1 of the §2.3 table collapses into row 4, and the wire format in §8 is never produced by the ordered path. With the flag on, the feature silently no-ops. This is the blocking item — the SDK work cannot be validated against a path that always returns nothing.

### 6.2 Blocking Phase 2 — RTT values are sorted as strings

Values are stored via `kvp.Value.ToString()` and ranked with `.OrderBy(item => item.Value)`, a lexicographic sort. The backfill values `0, 2, 6, 18, 205, 314` yield `"0", "18", "2", "205", "314", "6"`.

Fixing §6.1 exposes this one, so both must land before Phase 2 rollout.

### 6.3 Blocking Phase 3 — no discriminator for degraded responses

Per §2.3 the server returns an unordered full region list when the proximity lookup is skipped or throws, and it is indistinguishable on the wire from a genuinely ordered one. Every SDK therefore risks adopting an arbitrary ordering as if it were proximity-ranked, and the failure surfaces only as latency. Options:

1. Return an explicit flag (e.g. `regionProximityOrdered: true`) alongside the array — unambiguous, but needs a wire change.
2. Omit the field entirely when no ranking was computed, so absence means "no data" — reuses the existing absent-field path, but changes the current fallback contract.
3. Leave as-is and have each SDK infer ordering from whether it sent a source region — cheapest, but still wrong for the exception path, and repeats the inference in every SDK.

Option 2 is the smallest correct change and needs no new field. Until this is resolved, treat Phase 2's ordering guarantee as best-effort. The ambiguity is latent while §6.1 stands, since row 1 returns an empty array today.

---

## 7. Tests

Locations below are .NET; every SDK needs the equivalent coverage.

| Test | Location |
| --- | --- |
| `regionProximity` parsed out of `AdditionalProperties` into `RegionProximityInternal` | `GlobalEndpointManagerTest.cs` |
| Absent field yields an empty collection and no error | `GlobalEndpointManagerTest.cs` |
| Malformed value (wrong JSON type, or an array with non-string entries) does not fail the account read; bad entries are dropped, the rest kept | `GlobalEndpointManagerTest.cs` |
| Sanitized entries map to account display names before use, including a region absent from the SDK's `Regions` class | `GlobalEndpointManagerTest.cs` |
| Capability stays off when the client-side switch is disabled even though the field is present | `GlobalEndpointManagerTest.cs` (Phase 2) |
| Request carries `?regionproximitysourceregion=eastus` when `ApplicationRegion = "East US"`, and omits it when unset | `GatewayAccountReaderTest.cs` (Phase 2) |
| `PreferredLocations` updated from the server list after init, and re-applied on refresh when the ordering changes | `GlobalEndpointManagerTest.cs` (Phase 2) |
| Refresh returning an unchanged ordering does not rewrite `PreferredLocations` | `GlobalEndpointManagerTest.cs` (Phase 2) |
| A non-empty response is NOT applied when no source region was sent (§2.3) | `GlobalEndpointManagerTest.cs` (Phase 2) |
| `PreferredLocations` NOT changed when `ApplicationPreferredRegions` is used | `GlobalEndpointManagerTest.cs` (Phase 2) |
| A region absent from the static RTT table is accepted by `SetCurrentLocation` | `ConnectionPolicyTest.cs` (Phase 2) |

---

## 8. Wire Format

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

> ⚠️ This shows the **intended** contract. The ordered path does not produce it today — see §6.1.
