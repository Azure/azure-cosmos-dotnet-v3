# HTTP retry response disposal ordering

**Contract:** HTTP-DROP-ORDER/v1.
**Scope:** Unit-only protection of existing behavior, not a completed paired
unit/service increment. No production change or new public guarantee.

## Approval and spec-before-code record

On 2026-11-02 the user selected **"Implement the unit tests and SDK spec
(Recommended)"** after being asked:

> Should we implement the deterministic retry-disposal unit tests and promote
> their spec into the SDK now? This would be a unit-only increment—not completed
> paired coverage—and would not add the HTTP/2 fixture, access live accounts,
> change CI settings or activate protected enforcement.

This specification is committed and pushed before executable test changes.
The commit history records that ordering. The approved session specification
`Retry response disposal before the next dispatch`, v1.0, remains unchanged with
SHA-256 `1f43686be77d102436bd7f727dfdbeac7b3e354551dfa0f752e0b2e10af0924d`.
This SDK document promotes its bounded unit contract and preserves the missing
real-transport pair as explicit debt, not as implementation authority.

At `2026-11-02T18:48:40Z`, freshly resolved SDK `main` remained
`52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8`, tree
`2c89abbd9680af78d5072b523df277d8c46b755d`. The relevant source, actual assertion
bodies and policies were re-read before edits. The original source diagnosis
and contract are unchanged.

## Existing behavior and evidence

The A-01 W-11/G-05 lead is an assertion gap, not a newly reproduced production
defect. In [`SendHttpHelperAsync`][core], the SDK records a returned response,
returns it if nonretryable or out of attempts, and otherwise disposes it before
continuing the loop. The original
[`ThinClientRetriableResponsesAreDisposedBeforeRetryAsync`][original-test]
returns two 408s followed by 200, but only checks disposal after awaiting the
whole operation. Cleanup deferred until loop completion could pass those
post-call assertions.

The original test also observes real production diagnostics and metrics
consumers: both dropped 408s remain recorded, trace serialization remains safe,
unknown content length forces the disposed-content fallback, content-header
access throws, and guarded metrics report zero response-body bytes. All those
assertions must remain.

The expected ordering is independently stated in the existing
[#5987 fix and changelog][fix]; a separate next-dispatch observer tests it.
Do not derive the expected sequence by calling the retry predicate in an
assertion. Related response-classification, timeout-selector and mocked
store-routing tests do not supply this missing next-dispatch observation.

### Entry, policies and ownership

Public item reads normally traverse `ContainerInlineCore` / `ContainerCore`,
the request handler and `TransportHandler`, `DocumentClient.GetStoreProxy`,
then the selected store model. Thin routing requires Gateway mode, the thin
feature flag, non-resource-token authorization, advertised endpoints and
healthy regional probe state. `ThinClientStoreClient` creates an outer POST
with requested HTTP version 2.0 and uses the ordinary HTTP retry core.

For a non-DTX document read, the actual [policy selector][selector] chooses
[`HttpTimeoutPolicyForThinClient`][thin-policy]: three attempt slots with
6/6/10-second timeouts and zero inter-attempt delays. A returned HTTP 408 is
retryable for a thin read but not a thin write. Three slots mean at most three
total sends, not three retries after the first send. No substatus is assigned
by the in-memory fixture; it is not Direct barrier 408/21013.

| Resource | Required ownership boundary |
|---|---|
| Dropped retryable HTTP response, content and unread stream | Disposed by the retry helper before the next send |
| Successful, nonretryable or exhausted final HTTP response | Returned intact to the helper's caller, which owns disposal |
| Per-attempt request and linked timeout CTS | Created/disposed by the helper for each attempt |
| Original service request, caller CTS and statistics | Borrowed from their owning operation/test, not disposed by response cleanup |
| HTTP client and handler | Client-scoped; retry cleanup must not destroy them or prevent a following operation |

`SendAsync` uses `ResponseHeadersRead`. The downstream thin store later owns
the terminal outer HTTP response with `using` and parses a binary proxy payload;
that is a different boundary from retry-loop disposal. A raw HTTP unit terminal
response is not a fabricated public thin-service response.

## Atomic contract and preserved neighbors

Preconditions are a non-DTX document read, the actual selected policy, a returned
408 response owned by that attempt and remaining slots. Caller cancellation,
terminal ownership and a subsequent send failure are separate explicit controls.
No chaos interceptor, hedging, concurrent measured operation or throwing
disposal callback is assumed.

| Scenario | Required outcome |
|---|---|
| 408, 408, 200 | Both earlier contents have completed disposal at the beginning of each next send; exactly three sends; final success stays readable until caller cleanup |
| Three returned 408s | First two disposed before their successors; third returned intact with original status/body; no fourth send or manufactured 503 |
| Nonretryable response | Exactly one send; the same response and body remain caller-owned |
| Caller already cancelled | Caller-token `OperationCanceledException`, zero request factories and sends |
| Caller cancelled by completed dropped-content disposal | One send; first content disposed; caller-token cancellation, no next send or timeout-to-503 conversion |
| Next send fails with a nonretryable sentinel | Earlier response already disposed; original sentinel propagates; earlier diagnostics record remains usable |
| Following operation on the same HTTP client | Succeeds with a fresh request and live token after normal recovery, caller cancellation or the sentinel failure |

Preserve ordinary Gateway document-read policy, thin-write nonretry behavior,
non-thin metadata retries, final-response ownership, existing diagnostics/metrics
assertions and existing timeout/503 classification. Nonzero-delay policies do
not acquire a new prompt-cancellation guarantee from this zero-delay thin test.

Not contracted: server receipt of RST_STREAM before the next request, exact TCP
reuse, absence of runtime-internal unobserved exceptions, GC scheduling, every
status/substatus, write replay, DTX, bulk, hedged-loser cleanup or new consistency
semantics.

## Unit scenarios and expected leaves

All changes are confined to `CosmosHttpClientCoreTests.cs` and its private
content/journal support. Use the existing `MockMessageHandler`,
`MockCosmosUtil.CreateCosmosHttpClient`, actual policy selector and
`DisposeTrackingContent`. Record disposal only after base content disposal
completes. Assertions run before client/final-response teardown; teardown is not
the ordering witness.

| ID / method | Rows and independent witnesses |
|---|---|
| U-01 `ThinClientRetriableResponsesAreDisposedBeforeRetryAsync` | GET/requested-1.1 and POST/requested-2.0; 408,408,200; next-send completed-disposal assertions, exact causal journal/send count, readable final success, same-client following success; retain all original diagnostics and metrics assertions |
| U-02 `RetryDisposal_ExhaustedFinalResponseRemainsCallerOwnedAsync` | Three immediate 408s; first two disposed at next send, exact terminal response identity/status/body, final content live before explicit caller disposal, exactly three sends |
| U-03 `RetryDisposal_NonRetriableResponseRemainsCallerOwnedAsync` | Gateway document read/408; thin document write/408; thin document read/500; actual selector with partition failover disabled, one send and unchanged terminal identity/body |
| U-04 `RetryDisposal_PreCancelledCallerDoesNotDispatchAsync` | Pre-cancel caller CTS; require caller-token cancellation and zero factory/send invocations |
| U-05 `RetryDisposal_CancellationAfterDropDoesNotSendAgainAsync` | Cancel synchronously from the completed-disposal callback; exact one-send journal, disposed first response and caller-token cancellation; fresh-token following operation still succeeds |
| U-06 `RetryDisposal_NextSendFailurePreservesEarlierDisposalAsync` | 408 then exact `InvalidOperationException`; observe prior disposal at second send, require original exception and recorded first response; same-client following operation succeeds |
| U-07 `RetryDisposal_MetadataResponseIsDisposedBeforeNextSendAsync` | Actual non-thin metadata-read hot-path policy, GET, 408 then 200; observe ordering and terminal readability; only its first zero delay is traversed |

The expected new/strengthened cohort is **10 parameter leaves**: two U-01,
three U-03 and one for each other method. This is a discovery reconciliation
expectation, not a coverage target. No new sleeps, clock-ordering races, GC
oracle, production-timeout changes, public test hooks or fixture dependencies
are needed.

## Targeted validation and falsification

Use the existing net6.0 unit project, not a new runner. The planned cohort is
W-11 plus `RetryDisposal_`, with these directly relevant existing neighbors:

- `VerifyShouldRetryOnResponseTest`
- `RetryTransient408sTestAsync`
- `DoesNotRetryTransient408sOnDefaultPolicyTestAsync`
- `HttpTimeoutThrow503TestAsync`
- `HttpTimeoutPolicyForThinClientOn503TestAsync`
- `HttpTimeoutPolicyWhenThinClientEnabledForPointReads`
- `HttpTimeoutPolicyWhenThinClientEnabledForNonPointReads`
- `HttpTimeoutPolicyForReadDistributedTransaction_ReturnsDefault`
- `HttpTimeoutPolicyForWriteDistributedTransaction_ReturnsDefault`

Those nine neighbors yield 19 expected leaves with the new cohort. Do not select
the whole SDK or unrelated long-running/flaky tests. Preserve any pre-existing
delays in selected neighbors rather than rewriting them.

Required configuration identities are GA net6.0 Debug and preview net6.0 Release
with `IsPreview=true`. Native YAML's ordinary unit command does not forward the
caller's Release label; the preview command does. Existing project selection
makes nonquarantined core tests eligible but does not prove execution,
first-attempt success, effective host/runtime or protected admission.

The initial local runner receipt is macOS arm64, SDK 9.0.304 and runtime 9.0.8;
it has no PATH-visible .NET 6 runtime. This is not the native Windows/.NET 6 cell.
Do not relabel a newer-runtime roll-forward or a compiled net6.0 target as that
cell's execution. Use an existing supported runner if available; otherwise
retain the concrete runner failure and unexecuted variants in the implementation
receipt.

After both actual testhost aborts established the missing dependency, the
coordinator authorized only a matching official .NET 6 runtime inside this
session's own directory, using supported command-scoped testhost selection.
Inspect and pin the official distribution/version/architecture and verify its
hash before use. Preserve the initial failures and every correction outcome.
No global installation, symlink/profile/SDK modification, TFM retargeting,
roll-forward-to-9 substitution, ASP.NET dependency or HTTP/2 fixture is authorized.
Execution on the actual macOS/arm64 host, if possible without unsupported hacks,
is still distinct from native Windows qualification.

On 2026-11-02, the official osx-arm64 .NET 6.0.36 runtime executed the cohort
through VSTest's command-scoped `RunConfiguration.DotNetHostPath`. The host
diagnostic records the session-private 6.0.36 runtime location. All 19 expected
leaves passed in GA Debug and preview Release, with no skipped/inconclusive
results. The original missing-assets failure, existing-manifest restore and
missing-runtime aborts remain recorded rather than replaced by the passes.
This required legacy runtime is end-of-support; using it for the scoped tests
is not a production-runtime recommendation or broader platform-support claim.

A temporary cleanup-at-loop-return negative control failed both U-01 rows at
the new next-dispatch disposal assertion. The production file was then restored
byte-for-byte, and all 19 leaves passed again in each variant. No negative-control
product change is committed. These are local unit results on macOS/arm64, not
native Windows/CI execution, protected admission or real HTTP/2/service proof.

A minimal targeted negative control may defer dropped-response disposal until
loop completion in an isolated temporary check. The new next-send assertion
must reject that ordering while legitimate recovery/terminal ownership and
original diagnostics/metrics assertions remain unchanged. Restore the original
production source before any final commit, and retain the failing assertion
and restored-candidate result. Do not launch mutation/property/fuzz tooling.
Suppressing retries, swallowing cancellation or disposing terminal responses
must also be rejected by the normal control assertions.

Record source/tree, spec-first and implementation commits, actual runner/runtime,
flags/TFM, produced test/core artifact identities, discovered/executed leaves,
all first failures/corrections and final disposition. Missing, failed, skipped or
inconclusive evidence remains visible; aggregate pass counts are not paired proof.

## Still-missing real HTTP/2 pair

No HTTP/2 server, emulator/live run, certificate, account, dependency or CI change
is part of this approval. The existing `CosmosItemThinClientTests` fixture needs
an externally configured thin-capable account. Its real happy paths and synthetic
probe/pre-send failure helpers cannot independently produce a real data-plane
408 and observe its response disposal.

The preserved v1.0 paired design remains conditional on all of:

- A controlled real HTTP/2 fault endpoint returning flushed 408 headers with an
  incomplete unread body, plus a transparent real-content completed-disposal
  witness at the next send. In-memory failed responses are not a substitute.
- Public item-read routing through the normal thin store/policy, actual negotiated
  HTTP/2 receipts, two controlled failed exchanges and unchanged real service
  success with independently seeded item state. Cancellation and subsequent
  same-client recovery remain meaningful paired controls.
- Compatible host/TLS/ALPN and exact .NET/ASP.NET runtime admission. Kestrel would
  need a separately approved test-only framework reference; runtime 6.x plus
  SDK 8.x in YAML does not establish that capability.
- Explicit named isolated-account, namespace/create-delete, healthy topology,
  consistency/session, signed loopback-envelope handling and finite run authority.
  No production/customer resource access or permission changes.
- Probe/metadata/hedge isolation, process-flag restoration, owned-resource teardown
  and separately bounded eventual server-release evidence. Physical reset need
  not be observed before the next server request.

Universal unobserved-task/GC behavior remains open under [#5996][follow-up].
Direct backend RNTBD is not the client-to-thin-proxy HTTP/2 surface. The canonical
[transport chapter, Two Connectivity Modes][transport-concept] and
[status/retry chapter, Overview][retry-concept], pinned at
`bcfbc786fa5296deebcccd11369efb583e63e48c`, guide those boundaries; the simplified
408 overview does not override this SDK's explicit thin-policy branch.

This increment is independent of M-01.5 parser/producer implementation. Trusted
proof qualification and paired-fixture admission remain separate. Existing
parent-first PR integration and CI/protected-enforcement settings are unchanged.
Test/docs-only scope has no shipped behavior change and requires no new
customer changelog entry; preserve the existing #5987 guidance.

[core]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/src/HttpClient/CosmosHttpClientCore.cs#L405-L549
[original-test]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/CosmosHttpClientCoreTests.cs#L947-L1052
[selector]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/src/HttpClient/HttpTimeoutPolicy.cs#L38-L124
[thin-policy]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/src/HttpClient/HttpTimeoutPolicyForThinClient.cs#L31-L75
[fix]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/changelog.md#L107-L119
[follow-up]: https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5996
[transport-concept]: https://github.com/tvaron3/cosmosdb-design-docs/blob/bcfbc786fa5296deebcccd11369efb583e63e48c/06-transport-layer-rntbd.md#two-connectivity-modes
[retry-concept]: https://github.com/tvaron3/cosmosdb-design-docs/blob/bcfbc786fa5296deebcccd11369efb583e63e48c/17-status-codes-and-sdk-retries.md#1-overview
