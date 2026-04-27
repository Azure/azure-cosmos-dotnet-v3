## 1. Setup

- [ ] 1.1 Create a git worktree for the feature branch (e.g., `feature/prefer-writes-in-hub`) to work in isolation from the main working directory

## 2. Public API Surface

- [ ] 2.1 Add `public bool PreferWritesInHub { get; set; }` property to `CosmosClientOptions` with XML doc comments (default `false`)
- [ ] 2.2 Add `public CosmosClientBuilder WithPreferWritesInHub()` fluent method to `CosmosClientBuilder`
- [ ] 2.3 Add validation in `CosmosClientOptions.ValidateDirectTCPSettings` to throw `ArgumentException` when both `PreferWritesInHub` and `LimitToEndpoint` are `true`

## 3. Internal Plumbing

- [ ] 3.1 Add `internal bool PreferWritesInHub` property to `ConnectionPolicy` and wire it from `CosmosClientOptions.GetConnectionPolicy()`
- [ ] 3.2 Thread the flag through `GlobalEndpointManager` constructor into `LocationCache` constructor as a new `bool preferWritesInHub` parameter; store as a readonly field

## 4. Routing Logic

- [ ] 4.1 Modify `LocationCache.ResolveServiceEndpoint` to check `preferWritesInHub` for write operations: resolve to hub endpoint first, fall back to preferred-region write endpoints if the hub is unavailable
- [ ] 4.2 Verify that read operations are unaffected by the new flag (no code change needed, but confirm via code review)

## 5. Diagnostics & Tracing

- [ ] 5.1 Add `PreferWritesInHub` to `ConsistencyConfig` in `ClientConfigurationTraceDatum` so the setting is visible in diagnostics
- [ ] 5.2 Update serialization in `GetSerializedDatum()` / `TraceDatumJsonWriter` to include the new field in diagnostics output

## 6. Tests

- [ ] 6.1 Add unit tests for `CosmosClientOptions` validation: default value is `false`, setting to `true` works, conflict with `LimitToEndpoint` throws
- [ ] 6.2 Add unit tests for `CosmosClientBuilder.WithPreferWritesInHub()` sets the option correctly
- [ ] 6.3 Add unit tests for `LocationCache.ResolveServiceEndpoint` with `preferWritesInHub = true`: writes go to hub, reads follow preferred regions
- [ ] 6.4 Add unit tests for hub-unavailable fallback: when hub endpoint is marked unavailable, writes fall back to preferred-region ordering
- [ ] 6.5 Add unit tests confirming no behavior change when `preferWritesInHub = false`
- [ ] 6.6 Add unit tests for ExcludeRegions interaction: when hub region is in `ExcludeRegions`, writes fall back to preferred-region ordering
- [ ] 6.7 Add unit tests for ApplicationRegion compatibility: when `ApplicationRegion` is set with `PreferWritesInHub`, writes go to hub and proximity-generated list is used as fallback
- [ ] 6.8 Add unit tests for diagnostics: verify `PreferWritesInHub` appears in `ClientConfigurationTraceDatum` output for both `true` and `false` values
