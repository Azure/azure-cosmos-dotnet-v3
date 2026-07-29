# Spec: Migrate from `RegionProximityUtil` Static Table to Server-Provided Region Proximity

## 1. Background and Motivation

### 1.1 Current State

All Cosmos DB SDKs implement a `CosmosClientOptions.ApplicationRegion` (or equivalent) feature that automatically orders `PreferredLocations` by geographic proximity to the application's region. Today, this ordering is provided by a **hardcoded static lookup table** called `RegionProximityUtil` (variants: `RegionProximityUtil.cs`, `RegionProximityUtilProxy.cs`), which maps every known Azure region to estimated round-trip times (RTTs) to all other regions.

| Property | Value |
|---|---|
| Location (.NET server-side) | [CosmosDB/Product/Microsoft.Azure.Documents/SharedFiles/RegionProximityUtil.cs](https://msdata.visualstudio.com/CosmosDB/_git/CosmosDB?path=/Product/Microsoft.Azure.Documents/SharedFiles/RegionProximityUtil.cs) |
| Location (.NET SDK direct layer) | `Microsoft.Azure.Cosmos/src/direct/RegionProximityUtil.cs` (`msdata/direct` branch) |
| Namespace | `Microsoft.Azure.Documents` |
| Type | `internal static class` |
| Key data | `SourceRegionToTargetRegionsRTTInMs: Dictionary<string, Dictionary<string, long>>` |
| Key methods | `GeneratePreferredRegionList(string)`, `GetRegionsForLinkType(GeoLinkTypes, string)` |

**Problems with this approach:**

1. **Staleness**: The lookup table is baked into each SDK binary. Adding a new Azure region or updating network topology requires an SDK release.
2. **Approximation**: RTT values are estimates — not measured, not account-aware, not path-adaptive.
3. **No account context**: The table includes all Azure regions regardless of what's actually provisioned in the customer's account.
4. **Maintenance burden**: The same table must be kept in sync across the server codebase and every SDK's `direct` layer.
5. **Region validation coupling**: `SetCurrentLocation` rejects any region not in the static table, causing errors when new regions are added before the SDK is updated.

### 1.2 Proposed Solution

The Cosmos DB gateway (`DatabaseAccountHandler`) now has the ability to compute and return a **server-side, account-filtered, proximity-ordered list** of regions as part of the `DatabaseAccount` (`GET /`) response. SDKs should:

1. **Send** a source-region query parameter with the `GET /` (GetDatabaseAccount) request so the server can tailor the ordering.
2. **Parse** the `"regionProximity"` field from the response.
3. **Use** the server-provided list to order `PreferredLocations` instead of calling `RegionProximityUtil.GeneratePreferredRegionList()`.
4. **Fall back** to the static table during initial client bootstrap (before the first account read).
5. **Phase out** the static table once server-provided data is verified and widely deployed.

---

## 2. Server-Side API Contract

### 2.1 Feature Flag

The gateway only populates `regionProximity` when the feature flag is enabled:

```
ConfigurationProperties.EnableRegionProximityData = "enableRegionProximityData"  (bool, default: false)
```

> **Source**: [CosmosDB\Product\Cosmos\RoutingGateway\Runtime\RequestHandlers\DatabaseAccountHandler.cs](https://msdata.visualstudio.com/CosmosDB/_git/CosmosDB?path=/Product/Cosmos/RoutingGateway/Runtime/RequestHandlers/DatabaseAccountHandler.cs&version=GBmaster&line=221&lineEnd=222&lineStartColumn=1&lineEndColumn=1&lineStyle=plain&_a=contents),

SDKs **must** handle the case where `regionProximity` is absent from the response (older gateway versions or when the flag is off).

### 2.2 Request: Query Parameter

To receive a proximity-ordered list, the SDK **must** include a query parameter in the `GET /` (GetDatabaseAccount) request:

| Parameter | Value |
|---|---|
| Name | `regionproximitysourceregion` |
| Constant | `Constants.RegionProximity.SourceRegionQueryParam = "regionproximitysourceregion"` |
| Value | The sanitized name of the region where the application/SDK client is running |
| Source | [CosmosDB\Product\Microsoft.Azure.Documents\SharedFiles\Constants.cs](https://msdata.visualstudio.com/CosmosDB/_git/CosmosDB?path=/Product/Microsoft.Azure.Documents/SharedFiles/Constants.cs&version=GBmaster&line=3189&lineEnd=3190&lineStartColumn=1&lineEndColumn=1&lineStyle=plain&_a=contents) |

**Example request URL:**
```
GET https://myaccount.documents.azure.com/?regionproximitysourceregion=eastus
```

**Sanitization rule**: region names must be lowercase with spaces/hyphens removed (e.g., `"East US"` → `"eastus"`). The server uses `StringUtil.SanitizeString()` for this normalization.

> **Note**: If the SDK does not send this parameter (or sends an empty/unrecognized value), the server will return the account's regions in unordered form (write regions + read regions, deduplicated, sanitized). This is a safe degraded-mode fallback.

### 2.3 Response: `regionProximity` Field

The server returns the proximity-ordered list as a new top-level field in the `DatabaseAccount` JSON:

```json
{
  "id": "myaccount",
  "writableLocations": [...],
  "readableLocations": [...],
  "regionProximity": ["East US", "West US", "North Europe"],
  ...
}
```

| Property | Value |
|---|---|
| JSON key | `"regionProximity"` |
| Constant | `Constants.Properties.RegionProximity = "regionProximity"` |
| Type | JSON array of strings (region names) |
| Ordering | Ascending by RTT from the source region passed in the query parameter |
| Filtering | Includes **only** regions provisioned in the account (intersection of all write + read locations) |
| Source | [CosmosDB\Product\Microsoft.Azure.Documents\SharedFiles\DatabaseAccount.cs](https://msdata.visualstudio.com/CosmosDB/_git/CosmosDB?path=/Product/Microsoft.Azure.Documents/SharedFiles/DatabaseAccount.cs&version=GBmaster&line=368&lineEnd=369&lineStartColumn=1&lineEndColumn=1&lineStyle=plain&_a=contents) |

**Server-side data flow:**

```
GET /?regionproximitysourceregion=eastus
    │
    ▼
DatabaseAccountHandler.GetRegionProximityDataForDatabaseAccountAsync()
    │
    ├─ 1. Extract sourceRegion from query ("eastus")
    ├─ 2. GetRankedRegionProximityDataAsync(sourceRegion)
    │       └─ Read IGatewayRegionalConfigurationProvider
    │              key = "regionproximity_" + sourceRegion  (e.g. "regionproximity_eastus")
    │              Sorted by RTT value → List<string> of ALL Azure regions, ordered by proximity
    │       └─ Cache: AsyncTimeCache, refresh interval = RegionProximityRankingRefreshIntervalInSeconds
    │
    ├─ 3. Intersect ranked list with account's write+read regions
    │       (keeps only regions provisioned in the account)
    │
    └─ 4. Return Collection<string> — account regions ordered by proximity
```

**Fallback when no source region provided (or error):**
```
Returns: account write + read regions, deduplicated, sanitized — but NOT proximity-ordered
```

### 2.4 Server-Side Caching

The server caches the global proximity ranking per source region:

- Cache type: `AsyncTimeCache<string, List<string>>` (keyed by source region)
- Cache TTL: configurable via `ConfigurationProperties.RegionProximityRankingRefreshIntervalInSeconds`
- Region name normalization: cached via `ConcurrentDictionary<string, string> sanitizedRegionProximityNames`

> **Source**: `DatabaseAccountHandler.cs` lines 49–50, 315–344

---

## 3. Cross-SDK Requirements

Every Cosmos DB SDK implementing `ApplicationRegion` (or equivalent) **must** satisfy the following behavioral contract.

### 3.1 Sending the Source Region Query Parameter

| Requirement | Detail |
|---|---|
| **R-1** | When `ApplicationRegion` (or equivalent) is configured by the user, the SDK MUST append `?regionproximitysourceregion=<sanitized-region>` to all `GET /` (GetDatabaseAccount) requests. |
| **R-2** | The region name MUST be sanitized to lowercase with spaces stripped before sending (e.g., `"East US"` → `"eastus"`). |
| **R-3** | If `ApplicationRegion` is not configured, the SDK SHOULD NOT send the query parameter (the server returns an unordered list by default). |
| **R-4** | The query parameter MUST be sent on both the **initial** account read and all **background refresh** reads. |

### 3.2 Parsing the `regionProximity` Field

| Requirement | Detail |
|---|---|
| **R-5** | SDKs MUST parse the `"regionProximity"` field from the `DatabaseAccount` response. |
| **R-6** | The field is a JSON array of strings. Each string is a region name (e.g., `"East US"`). |
| **R-7** | If the field is absent, null, or empty, the SDK MUST treat it as "no server data available" and retain the existing fallback behavior. |
| **R-8** | The parsed list MUST be stored and made available for use by the region preference resolution logic. |

### 3.3 Using the Server-Provided List

| Requirement | Detail |
|---|---|
| **R-9** | When the server provides a non-empty `regionProximity` list, the SDK MUST use it instead of the static `RegionProximityUtil.GeneratePreferredRegionList()` to determine the ordered `PreferredLocations`. |
| **R-10** | The server-provided list is already filtered to account regions — the SDK MUST NOT additionally filter it against account regions. |
| **R-11** | If the user has configured explicit `PreferredLocations` (not `ApplicationRegion`), the server-provided proximity list MUST NOT override those explicit preferences. |
| **R-12** | The proximity list MUST be refreshed on background account refresh (typically every 5 minutes). |

### 3.4 Fallback Behavior

| Requirement | Detail |
|---|---|
| **R-13** | During initial client startup, before the first account read completes, the SDK MAY use `RegionProximityUtil.GeneratePreferredRegionList()` as a bootstrap approximation for `PreferredLocations`. |
| **R-14** | Once the first account read succeeds and returns a non-empty `regionProximity`, the SDK MUST override the bootstrap list with the server-provided list. |
| **R-15** | If the server never returns a non-empty `regionProximity` (older gateway, feature flag off), the SDK MUST continue using `RegionProximityUtil` for the entire session. |
| **R-16** | Region validation (rejecting unrecognized region names in `SetCurrentLocation`) MUST NOT fail for regions absent from the static table if the server later returns them. SDKs should soften or defer this validation once server data is available. |

### 3.5 Error Handling

| Requirement | Detail |
|---|---|
| **R-17** | Failure to parse `regionProximity` (malformed JSON, unexpected type) MUST NOT throw or surface an exception to the caller. The SDK MUST log a warning and fall back to the static table. |
| **R-18** | If the query parameter causes a gateway error (non-2xx response), the SDK MUST retry the request without the query parameter as a fallback. |

---

## 4. .NET SDK Implementation Details

### 4.1 .NET SDK Implementation Details

#### 4.1.1 `GatewayAccountReader.cs` — Send the Query Parameter

File: `Microsoft.Azure.Cosmos/src/GatewayAccountReader.cs`

The `GetDatabaseAccountAsync(Uri serviceEndpoint)` method must append the `regionproximitysourceregion` query parameter to the request URI when `ApplicationRegion` is configured:

```csharp
// Before the HttpClient.GetAsync call:
if (!string.IsNullOrEmpty(this.connectionPolicy.CurrentLocation))
{
    string sanitizedRegion = RegionNameMapper.SanitizeRegionName(this.connectionPolicy.CurrentLocation);
    // Append ?regionproximitysourceregion=<sanitizedRegion> to serviceEndpoint
    serviceEndpoint = AppendQueryParameter(
        serviceEndpoint,
        HttpConstants.QueryStrings.RegionProximitySourceRegion,  // "regionproximitysourceregion"
        sanitizedRegion);
}
```

**Notes**:
- The `currentLocation` is set when `SetCurrentLocation()` is called (triggered by `ApplicationRegion`).
- Add `HttpConstants.QueryStrings.RegionProximitySourceRegion = "regionproximitysourceregion"` to the SDK constants.
- This must also be sent during background refresh calls in `GlobalEndpointManager`.

#### 4.1.2 `AccountProperties.cs` — Move from `AdditionalProperties` to First-Class Field

Currently `regionProximity` is parsed from `AdditionalProperties` (the catch-all `[JsonExtensionData]` dict). Once the service graduates this field to a permanent, first-class JSON property, the `[JsonProperty]` attribute should be added directly:

```csharp
// Future (when field is stable):
[JsonProperty(PropertyName = "regionProximity", NullValueHandling = NullValueHandling.Ignore)]
internal Collection<string> RegionProximityInternal { get; set; }
```

Until then, the `AdditionalProperties` parsing approach in `GlobalEndpointManager` is correct.

#### 4.1.3 `ConnectionPolicy.cs` — Update `SetCurrentLocation` Logic

File: `Microsoft.Azure.Cosmos/src/ConnectionPolicy.cs`

**Current behavior** (both master and topic branch):
```csharp
public void SetCurrentLocation(string location)
{
    // Validates against static table — rejects unknown regions
    if (!RegionProximityUtil.SourceRegionToTargetRegionsRTTInMs.ContainsKey(location))
        throw new ArgumentException(...);

    // Generates preferred list from static RTT table
    List<string> proximityBasedPreferredLocations = RegionProximityUtil.GeneratePreferredRegionList(location);
    // ... sets this.preferredLocations
}
```

**Required changes**:

1. **Store `currentLocation`** as a field on `ConnectionPolicy` so `GatewayAccountReader` can read it to construct the query parameter:
   ```csharp
   internal string CurrentLocation { get; private set; }
   ```

2. **Soften region validation**: Accept regions not in the static table with a warning (rather than throwing) to avoid blocking new regions:
   ```csharp
   if (!RegionProximityUtil.SourceRegionToTargetRegionsRTTInMs.ContainsKey(location))
   {
       // Log warning: region not in static table, will use server-provided proximity if available
       DefaultTrace.TraceWarning("Region '{0}' not in static RegionProximityUtil table.", location);
   }
   ```

3. **Bootstrap from static table** (unchanged for Phase 2 — still needed until server data arrives):
   - Continue using `RegionProximityUtil.GeneratePreferredRegionList(location)` for the initial `PreferredLocations`.
   - If the region is not in the table, fall back to an empty preferred list (let the SDK use the account's default ordering).

4. **Apply server-provided list on account init** (`GlobalEndpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh`):
   ```csharp
   // After SetRegionProximity() is called and RegionProximity is non-empty:
   if (!string.IsNullOrEmpty(this.connectionPolicy.CurrentLocation)
       && this.connectionPolicy.RegionProximity.Count > 0)
   {
       // Re-apply preferred locations using server data
       this.connectionPolicy.SetPreferredLocations(this.connectionPolicy.RegionProximity);
   }
   ```

#### 4.1.4 `RegionProximityUtilProxy.cs` — Update Interop API

File: [CosmosDB\Product\SDK\.net\Microsoft.Azure.Cosmos.Friends\src\RegionProximityUtilProxy.cs](https://msdata.visualstudio.com/CosmosDB/_git/CosmosDB?path=/Product/SDK/.net/Microsoft.Azure.Cosmos.Friends/src/RegionProximityUtilProxy.cs)

This proxy is used by Interop APIs. It exposes `TryGetPreferredRegionList` which calls `RegionProximityUtil.GeneratePreferredRegionList()` directly. Long-term, this should accept a server-provided list as an override. Short-term, no change needed.

#### 4.1.5 `GlobalEndpointManager.cs` — Background Refresh

The background refresh loop already calls `ParseRegionProximityFromAdditionalProperties` and `SetRegionProximity()` (from the topic branch). It needs to be extended to also re-apply `PreferredLocations` when proximity data changes:

```csharp
GlobalEndpointManager.ParseRegionProximityFromAdditionalProperties(accountProperties);
this.connectionPolicy.SetRegionProximity(accountProperties.RegionProximityInternal);

// Phase 2: re-apply PreferredLocations if ApplicationRegion is configured
if (!string.IsNullOrEmpty(this.connectionPolicy.CurrentLocation)
    && this.connectionPolicy.RegionProximity.Count > 0)
{
    this.connectionPolicy.SetPreferredLocations(this.connectionPolicy.RegionProximity);
}
```

### 4.2 .NET SDK File Summary

| File | Phase | Change Required |
|---|---|---|
| `GatewayAccountReader.cs` | Phase 2 | Send `?regionproximitysourceregion=<region>` on all GetDatabaseAccount calls |
| `ConnectionPolicy.cs` | Phase 2 | Add `CurrentLocation` field; soften validation; apply server list post-init |
| `GlobalEndpointManager.cs` | Phase 2 | Re-apply `PreferredLocations` from `RegionProximity` after account refresh |
| `AccountProperties.cs` | Phase 1 | `RegionProximityInternal` added (parse from AdditionalProperties) |
| `ConnectionPolicy.cs` | Phase 1 | `RegionProximity` property + `SetRegionProximity()` added |
| `HttpConstants.cs` | Phase 2 | Add `QueryStrings.RegionProximitySourceRegion = "regionproximitysourceregion"` |
| `RegionProximityUtil.cs` | Phase 3 | Remove `GeneratePreferredRegionList` usage from `SetCurrentLocation`; deprecate file |
| `RegionProximityUtilProxy.cs` | Phase 3 | Remove or reroute to server data |

---

## 5. Phased Deprecation Plan for `RegionProximityUtil`

### Phase 1 — Server Data Infrastructure

**Goal**: Parse and store server-provided proximity data. No behavior change yet.

- Server: `DatabaseAccountHandler` populates `regionProximity` when `EnableRegionProximityData = true`
- .NET SDK: Parse `regionProximity` from `AdditionalProperties` into `AccountProperties.RegionProximityInternal`
- .NET SDK: Store on `ConnectionPolicy.RegionProximity` via `SetRegionProximity()`
- No change to `SetCurrentLocation` or `PreferredLocations` logic

---

### Phase 2 — Active Use of Server Data (To Do)

**Goal**: Use server-provided proximity to order `PreferredLocations` when available.

**Server changes**:
- Enable `EnableRegionProximityData` in production (roll out feature flag)
- Monitor `LogRegionProximityFailureMetric` for errors

**SDK changes (all SDKs)**:
1. Send `?regionproximitysourceregion=<region>` on all GetDatabaseAccount requests
2. After account initialization, if `RegionProximity` is non-empty, apply it to `PreferredLocations`
3. On background refresh, re-apply `PreferredLocations` from updated `RegionProximity`
4. Store `currentLocation` (from `SetCurrentLocation`) as an SDK field for use in query param
5. Soften region name validation in `SetCurrentLocation` — warn instead of throw for unknown regions

**Backward compatibility**:
- If server does not return `regionProximity` (empty or absent): continue using static table
- If `RegionProximity` is populated: override bootstrap list from static table

---

### Phase 3 — Remove Static Table Dependency (Future)

**Goal**: Fully remove `RegionProximityUtil.GeneratePreferredRegionList()` from the `SetCurrentLocation` path.

**Prerequisites**:
- Phase 2 has been live for at least 2 SDK release cycles
- Server-side `EnableRegionProximityData` is enabled in all environments
- SDK telemetry confirms `RegionProximity` is consistently non-empty on all account reads

**Changes**:
1. Remove `RegionProximityUtil.GeneratePreferredRegionList()` from `SetCurrentLocation`
2. `SetCurrentLocation` stores the location name for query param construction only
3. `GetRegionsForLinkType` (used by N-region direct transport) remains in `RegionProximityUtil` — this is a **separate use case** requiring separate server-side data (see §6.4)
4. Update `RegionProximityUtilProxy` for Interop APIs

---

## 6. Open Questions and Scope Boundaries

### 6.1 Region Name Normalization / Sanitization

The server uses `StringUtil.SanitizeString()` to normalize region names (e.g., `"East US"` → `"eastus"`). SDKs must implement the same normalization. The exact transformation should be documented as a shared constant/utility:

- Rule: lowercase, remove all non-alphanumeric characters (spaces, hyphens, etc.)
- Example: `"East US"` → `"eastus"`, `"West Europe"` → `"westeurope"`, `"Australia Central 2"` → `"australiacentral2"`

The .NET SDK already has `RegionNameMapper.GetCosmosDBRegionName()` which handles public ↔ internal region name mapping. Sanitization for the query param is a separate, simpler transformation.

### 6.2 Initial Bootstrap Timing

There is an inherent race condition: `SetCurrentLocation` is called before the first account read, so server-provided proximity is not yet available. SDKs must:

1. Accept the static-table bootstrap as a temporary best-effort ordering.
2. Override it with server data immediately after the first successful account read.
3. NOT treat the bootstrap list as authoritative or cache it beyond the first account read.

### 6.3 `ApplicationPreferredRegions` Interaction

When users configure `ApplicationPreferredRegions` (explicit list) instead of `ApplicationRegion`, the server-provided proximity list MUST NOT be applied. The explicit list always takes precedence.

### 6.4 `GetRegionsForLinkType` — Separate Scope

`RegionProximityUtil.GetRegionsForLinkType()` is used by the direct transport layer (N-region synchronous commit) to determine which regions are within latency thresholds (Strong <100ms, Medium <200ms). This is **out of scope for this spec**. Deprecating this use case requires a separate server API to provide link-type classification per region.

### 6.5 Sovereign Clouds (Fairfax, Mooncake, etc.)

The server's `regionProximity` configuration is per-environment (see backfill scripts: `RegionProximityBackfill0.json` in Prod, Fairfax, Mooncake, etc.). SDKs must be tested in all cloud environments before Phase 3 removal.

---

## 7. Test Requirements

### 7.1 Unit Tests

| Test | SDK location (example — .NET) |
|---|---|
| `regionProximity` field parsed from JSON response | `AccountPropertiesTest.cs` |
| `regionProximity` absent from response → empty collection, no error | `AccountPropertiesTest.cs` |
| `ParseRegionProximityFromAdditionalProperties` populates `AccountProperties.RegionProximityInternal` | `GlobalEndpointManagerTest.cs` |
| `SetRegionProximity` updates `ConnectionPolicy.RegionProximity` | `ConnectionPolicyTest.cs` |
| `GetDatabaseAccount` request includes `?regionproximitysourceregion=` when `ApplicationRegion` set | `GatewayAccountReaderTest.cs` (Phase 2) |
| `PreferredLocations` updated from server `RegionProximity` post-init | `GlobalEndpointManagerTest.cs` (Phase 2) |
| `PreferredLocations` NOT changed when `ApplicationPreferredRegions` is used | `GlobalEndpointManagerTest.cs` (Phase 2) |
| Background refresh updates `RegionProximity` and re-applies `PreferredLocations` | `GlobalEndpointManagerTest.cs` (Phase 2) |
| Unknown region in `SetCurrentLocation` warns but does not throw (Phase 2) | `ConnectionPolicyTest.cs` |

### 7.2 Integration / Emulator Tests

- Emulator must be updated to support returning `regionProximity` in `DatabaseAccount` response (or tests must mock it)
- Test that client routed to correct region when `ApplicationRegion` is set and server returns proximity data

---

## 8. Wire Format Reference

### GetDatabaseAccount Request (with proximity)

```http
GET https://myaccount.documents.azure.com/?regionproximitysourceregion=eastus HTTP/1.1
Authorization: <auth-header>
x-ms-version: 2020-07-15
...
```

### GetDatabaseAccount Response (with `regionProximity`)

```json
{
  "id": "myaccount",
  "_self": "dbs/",
  "writableLocations": [
    { "name": "East US", "databaseAccountEndpoint": "https://myaccount-eastus.documents.azure.com:443/" }
  ],
  "readableLocations": [
    { "name": "East US", "databaseAccountEndpoint": "https://myaccount-eastus.documents.azure.com:443/" },
    { "name": "West US", "databaseAccountEndpoint": "https://myaccount-westus.documents.azure.com:443/" },
    { "name": "North Europe", "databaseAccountEndpoint": "https://myaccount-northeurope.documents.azure.com:443/" }
  ],
  "regionProximity": [
    "East US",
    "West US",
    "North Europe"
  ],
  ...
}
```

**Note**: `regionProximity` is scoped to the account's provisioned regions. It will never include regions not in `writableLocations` or `readableLocations`.

---

## 9. Key File Reference

### Server Side (https://msdata.visualstudio.com/CosmosDB/_git/CosmosDB)

| File | Role |
|---|---|
| `Product\Cosmos\RoutingGateway\Runtime\RequestHandlers\DatabaseAccountHandler.cs` | Computes and injects `regionProximity` into account response |
| `Product\Microsoft.Azure.Documents\SharedFiles\DatabaseAccount.cs` | `RegionProximity` property definition (line 368) |
| `Product\Microsoft.Azure.Documents\SharedFiles\Constants.cs` | `Constants.RegionProximity.SourceRegionQueryParam`, `Constants.Properties.RegionProximity` (line 3186–3189) |
| `Product\Microsoft.Azure.Documents\SharedFiles\RegionProximityUtil.cs` | Same static table that SDKs copy to their direct layer |
| `Product\Microsoft.Azure.Documents\Services\Roles\ManagementFrontend\Admin\UpsertRegionProximityRequestHandler.cs` | Admin endpoint to update proximity data for a region |
| `Product\Cosmos\Management\Runtime\Operations\AdminOperations\UpsertRegionProximityWorkflow.cs` | Workflow triggered by UpsertRegionProximityRequestHandler |
| `Product\SDK\.net\Microsoft.Azure.Cosmos.Friends\src\RegionProximityUtilProxy.cs` | Interop proxy for `RegionProximityUtil` |

### .NET SDK (https://github.com/Azure/azure-cosmos-dotnet-v3)

| File | Role |
|---|---|
| `Microsoft.Azure.Cosmos/src/GatewayAccountReader.cs` | Makes GetDatabaseAccount HTTP request — **needs query param added** (Phase 2) |
| `Microsoft.Azure.Cosmos/src/ConnectionPolicy.cs` | Stores `RegionProximity`; uses `RegionProximityUtil` in `SetCurrentLocation` |
| `Microsoft.Azure.Cosmos/src/Resource/Settings/AccountProperties.cs` | `RegionProximityInternal: Collection<string>` added |
| `Microsoft.Azure.Cosmos/src/Routing/GlobalEndpointManager.cs`| Parses & propagates `regionProximity` on init + background refresh |
| `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs` | Entry point for `ApplicationRegion` → calls `SetCurrentLocation()` |
| `Microsoft.Azure.Cosmos/src/direct/RegionProximityUtil.cs` | Static table being deprecated (for `SetCurrentLocation` use case) |

---

## 10. Appendix: Mapping of `RegionProximityUtil` Methods to Their Deprecation Strategy

| Method | Current Usage | Deprecation Strategy |
|---|---|---|
| `GeneratePreferredRegionList(string)` | `ConnectionPolicy.SetCurrentLocation()` — builds initial `PreferredLocations` | **Phase 2/3**: Replace with server-provided `regionProximity` list. Keep as bootstrap fallback until Phase 3. |
| `SourceRegionToTargetRegionsRTTInMs` dict | Validation in `SetCurrentLocation()` (region lookup) | **Phase 2**: Soften validation to warning. **Phase 3**: Remove. |
| `GetRegionsForLinkType(GeoLinkTypes, string)` | Direct transport layer — N-region synchronous commit | **Out of scope**: Requires separate server-side data source. Keep until further spec. |
| `TryGetPreferredRegionList()` (proxy) | Interop APIs (`RegionProximityUtilProxy`) | **Phase 3**: Update to accept server data or remove if callers are migrated. |
