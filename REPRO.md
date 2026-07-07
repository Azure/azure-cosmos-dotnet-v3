# Repro: AAD `ReadAccountAsync` hang introduced by PR #5549

> **Branch:** `nalutripician/aad-readaccount-hang-repro` (this branch — based on `main`, contains the
> **bug**, no fix). **Fix:** PR **#6008** (branch `nalutripician/readaccount-hang-repro`).

## Symptom

An AAD-authenticated client hangs on its first request — e.g. `await client.ReadAccountAsync()` right
after `new CosmosClient(endpoint, tokenCredential, options)` — even against a healthy account.
Customer bisection: **`3.61.0` → no hang, latest `main` → hang.**

## Root cause — PR #5549 (CAE / AAD token revocation)

`#5549` is the **only** auth-path change between `3.61.0` and `main`. It made
`TokenCredentialCache` attach a **non-empty `claims`** parameter (the `cp1` client capability) on
**every** token acquisition — even when there is no revocation challenge — because
`MergeClaimsWithClientCapabilities(null)` returns the `cp1` JSON instead of `null`:

```
claims = {"access_token":{"xms_cc":{"values":["cp1"]}}}
```

Azure.Identity/MSAL treat **any** non-empty `claims` as "the cached token does not satisfy this
challenge", so they **bypass `AcquireTokenSilent`'s token cache and call the authority (ESTS) live on
every acquisition**. Under an MSAL-backed credential (certificate / managed identity) that live call
can stall; because all callers funnel through a single-flight refresh, the first `ReadAccountAsync`
(and everything after it) hangs. `cp1`/CAE is **already** advertised the correct, cache-friendly way
via `isCaeEnabled: true` in `CosmosScopeProvider`, so the `claims` injection is redundant.

Relevant source on this branch:
- `Microsoft.Azure.Cosmos/src/Authorization/TokenCredentialCache.cs`
  (`MergeClaimsWithClientCapabilities`, and `RefreshCachedTokenWithRetryHelperAsync` where `claims`
  is attached unconditionally).
- `Microsoft.Azure.Cosmos/src/Authorization/CosmosScopeProvider.cs` (already sets `isCaeEnabled: true`).

## Repro #1 — deterministic unit tests (no account, ~5s) ⭐

Three tests were added to `CosmosAuthorizationTests`. **On this branch (buggy) they FAIL** — that
failure is the reproduction. Test 3 literally stalls for 5s (the hang).

```powershell
dotnet build Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Microsoft.Azure.Cosmos.Tests.csproj -c Debug
dotnet test  Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Microsoft.Azure.Cosmos.Tests.csproj -c Debug --no-build `
    --filter "FullyQualifiedName~TokenCredentialCache_NormalAcquisition|FullyQualifiedName~TokenCredentialCache_RevocationChallenge_StillAttaches"
```

Expected on this branch (bug present):

```
Failed TokenCredentialCache_NormalAcquisition_DoesNotAttachClaims_SoMsalCacheIsUsable
  Actual claims sent: '{"access_token":{"xms_cc":{"values":["cp1"]}}}'
Failed TokenCredentialCache_RevocationChallenge_StillAttachesMergedClaims
  The first (normal) acquisition must not attach claims.
Failed TokenCredentialCache_NormalAcquisition_DoesNotStallOnMsalCacheBypass  [5 s]   <-- the hang
```

With the fix (PR #6008) all three pass, and the full `CosmosAuthorizationTests` suite is 44/44 green.

The tests live in:
`Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/CosmosAuthorizationTests.cs`
(search for `PR #5549`).

## Repro #2 — live harness (optional, needs an account)

See [`tools/AadReadAccountHangRepro/README.md`](tools/AadReadAccountHangRepro/README.md). The
`--tokenprobe` mode is the clearest account-based demonstration: it prints the exact
`TokenRequestContext` the SDK passes on each acquire, showing the `cp1` claims on the buggy build.
For an actual stall, use a multi-region account + an MSAL-backed (certificate / managed-identity)
credential — `AzureCliCredential` will not reproduce it because `az` caches tokens itself.

## The fix (PR #6008)

Attach `claims` **only** when responding to an actual CAE/revocation challenge
(`cachedClaimsChallenge` is non-empty). On the normal path pass the scope provider's context
unchanged (`scopes` + `isCaeEnabled: true`, no claims) so the token cache is used. CAE revocation
still works because the merged claims are attached on the challenge path, and `cp1` is advertised via
`isCaeEnabled`.
