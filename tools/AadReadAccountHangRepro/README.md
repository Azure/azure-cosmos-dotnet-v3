# AAD `ReadAccountAsync` hang — live repro harness

Standalone console app that reproduces (and bisects) the AAD `ReadAccountAsync` hang described in
[`/REPRO.md`](../../REPRO.md). The **primary, zero-setup repro is the unit tests** — see `/REPRO.md`.
This harness is the optional *account-based* repro for when you want to exercise a real endpoint.

## What it does

Builds a client exactly like the customer snippet (Direct mode, AAD `TokenCredential`,
`CrossRegionHedgingStrategy`, plus the reflection-set internals incl.
`ReadConsistencyStrategy=LastCommittedSingleWriteRegion`), calls `ReadAccountAsync()` under a
watchdog, and can also drive the actually-hedged container Collection Read and force a metadata
hedge via fault injection.

## Prerequisites

- Point `Microsoft.Azure.Cosmos` (in the `.csproj`) at the **preview build that contains PR #5549**
  (the one the internal team was given). The public nuget.org preview may predate #5549 and will not
  reproduce. See the comment in `AadReadAccountHangRepro.csproj` for options (local `.nupkg` via
  `nuget.config`, or a `ProjectReference` to the in-repo `/Microsoft.Azure.Cosmos/src` built with
  `/p:IsPreview=true`).
- An AAD-authenticated Cosmos account, and an identity trusted by it. For the strongest repro, use a
  **multi-region** account and an **MSAL-backed** credential (certificate / managed identity), which
  is what exhibits the token-cache bypass. `AzureCliCredential` does NOT reproduce the hang because
  `az` caches tokens itself.

```powershell
$env:COSMOS_ACCOUNT_ENDPOINT = 'https://<account>.documents.azure.com:443/'
# Credential: DefaultAzureCredential by default. To target a specific tenant via the Azure CLI:
$env:COSMOS_AAD_TENANT_ID    = '<tenant-guid>'
# Or a certificate (mirrors the customer's CertificateFetcher / MSAL path):
$env:COSMOS_TENANT_ID        = '<tenant-guid>'
$env:COSMOS_CLIENT_ID        = '<app-guid>'
$env:COSMOS_CLIENT_CERT_PATH = 'C:\path\to\cert.pfx'
```

## Run

```powershell
# 1) Show exactly what the SDK passes to the credential on each token acquire (the fingerprint):
dotnet run -c Debug -- --auth aad --tokenprobe
#    Buggy (#5549) build => HasClaims=True, claims='{"access_token":{"xms_cc":{"values":["cp1"]}}}'
#    Fixed build         => HasClaims=False on the normal path, IsCaeEnabled=True.

# 2) ReadAccountAsync bisection matrix (metadata hedging / thin client on/off):
dotnet run -c Debug -- --auth aad --timeout 40

# 3) Exercise the actually-hedged container Collection Read:
dotnet run -c Debug -- --auth aad --containerread --timeout 40

# 4) Force a metadata hedge to fire (6s primary-region delay via fault injection):
dotnet run -c Debug -- --auth aad --faultinject --timeout 60
```

On any watchdog trip the harness prints the PID and the exact `dotnet-stack report -p <pid>` /
`dotnet-dump collect -p <pid>` commands to capture the hung stack, plus the last SDK EventSource
activity before the stall.

## Notes

- The token-probe (`--tokenprobe`) is the cleanest account-based demonstration: it shows the SDK
  attaching the `cp1` claims on every acquire on the buggy build, which is what makes MSAL bypass its
  cache. It does not require the hang to actually manifest.
- To actually observe the *stall*, use an MSAL-backed credential against a real endpoint; capture the
  stack — it will be parked inside the credential's `GetTokenAsync` under
  `TokenCredentialCache.RefreshCachedTokenWithRetryHelperAsync`.
