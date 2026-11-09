# Local HTTP/2 disposal fixture

## Status, authority and source

This specification precedes executable changes. It specifies
`LOCAL-HTTP2-DISPOSAL/v1`: a test-only loopback fault server and completed-disposal
observer using synthetic traffic. It does **not** implement or qualify the
Cosmos SDK/public-service retry-disposal pair.

The user approved starting the local fixture prerequisite, then directed adding
net6.0 tests and opening an independent PR for ordinary repository CI rather
than blocking implementation on the local macOS runtime limitation. The
coordinator clarified that the smallest necessary test-framework/runtime setup
belongs in this PR. No new TFM, service/account access, signed-envelope rerouting,
pipeline registration/settings, manual CI action, or change to PRs #6182, #6184,
#6185 or #6187 is authorized.

| Input | Frozen identity |
|---|---|
| SDK default branch, resolved 2026-11-02T20:05:37Z | `main` |
| SDK commit | `52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8` |
| SDK tree | `2c89abbd9680af78d5072b523df277d8c46b755d` |
| GitHub authenticated read identity | `kushagraThapar` |
| Remote routing | `origin`, fetch/push `https://github.com/Azure/azure-cosmos-dotnet-v3.git`; no push URL or URL rewrite observed |
| Preceding paired-test specification | `retry-disposal-paired-test-spec.md`, SHA-256 `1f43686be77d102436bd7f727dfdbeac7b3e354551dfa0f752e0b2e10af0924d`, sections 6.1-6.4 and 7-10 |
| Unit-only implementation receipt | SHA-256 `9fac3cd524fff415536685f468cb8a23dd61755ef5c4d5459fb27511481a5f1e`; PR #6187, head `650db04e6d43c25e14c67b2df44ad743a8c2e557` |
| Project continuation as read | SHA-256 `93e5f62021e3a5399667138eaccf25abc2baa773700c79425449bf60fc5bacbc` |
| Canonical concepts, after fast-forward pull on main | `tvaron3/cosmosdb-design-docs@bcfbc786fa5296deebcccd11369efb583e63e48c`, tree `ed5a8fb7b1956dbe5818a72902b8961bd8122d40` |

The original specification/receipts are immutable. Production already disposes
dropped retryable responses before the next dispatch; this work neither changes
that code nor repeats the unit implementation.

## Boundaries and prior art

The canonical [transport chapter, Two Connectivity Modes][transport] distinguishes
Gateway HTTP from Direct RNTBD. The [status/retry chapter, Overview][retry]
distinguishes SDK and gateway retry surfaces. This fixture implements neither
RNTBD nor a service retry policy. Its synthetic HTTP 408 has no Cosmos substatus
and does not promise any new Cosmos service behavior.

At the frozen SDK source:

- The core unit project targets net6.0, uses MSTest 3.7.3 and
  Microsoft.NET.Test.Sdk 17.12.0, and has no account-dependent assembly
  initialization. Existing `TaskCompletionSource` helpers use
  `RunContinuationsAsynchronously`.
- The existing thin/emulator lifecycle tests require `COSMOSDB_THINCLIENT` and
  are not the right home for this credential-free local cohort.
- The relevant core tests' in-memory content tracker is not a real sockets or
  protocol witness. It and the unit-only PR remain unchanged.
- No applicable existing Kestrel fixture was found by scoped SDK semantic
  navigation. Bluebird's configured `msdata/CosmosDB` service index is not SDK
  evidence. The deferred catalog exposes no `ado` server relevant to this
  source-only task.

Use the existing core unit project and private utilities. Add only its test-only
`Microsoft.AspNetCore.App` framework reference, a local fixture/observer, and a
separate `LocalHttp2DisposalFixtureTests` class. Do not add a product dependency,
retarget a project, change central versions, or add an account-based initializer.
The new tests must have their own category, not `ThinClient`, `Ignore` or
`Quarantine`, and must be eligible for the existing ordinary unit selections.
This tests/docs-only change needs no customer changelog entry.

## Protocol and lifetime contract

1. A fixture owns an in-process Kestrel host bound to `IPAddress.Loopback` and
   port zero. Read the actual ephemeral port after bounded startup. Do not bind
   wildcard/public addresses or use development-certificate discovery.
2. Generate an ephemeral self-signed loopback certificate in memory. A fresh
   fixture-owned `SocketsHttpHandler` disables proxies and redirects, pins that
   exact certificate, rejects name mismatch, and permits socket connections only
   to the fixture's exact loopback endpoint. No trust-store, global TLS callback,
   credential, environment or persisted private-key change is permitted.
3. Use TLS and HTTP/2-only Kestrel for positive cases. The client requests
   HTTP/2 exactly. Require the actual response version `2.0`, server request
   protocol `HTTP/2`, and server-observed ALPN `h2`. Requested version alone,
   plaintext h2c or HTTP/1.1 fallback cannot pass admission.
4. Pre-register harmless, uniquely named exchanges. A held fault emits actual
   408 headers, no declared content length, and no response-body bytes or end of
   stream. Flush headers, signal that the body is held, and await request abort
   or fixture shutdown. At client ownership, require held state, no abort, and
   no completed server handler. Never read or buffer this measured failed body.
5. Replace only the actual response's `HttpContent` with a private transparent
   delegator. Copy required content headers, forward streaming and cancellation,
   and retain the real response's status, headers and version. Do not synthesize
   the response or proactively dispose it. Publish completed disposal only after
   the underlying content and wrapper disposal return successfully. A thrown
   disposal must not create a completed witness.
6. Observe completed content disposal at the next synthetic dispatch entry,
   before forwarding the next request. The test driver, not a Cosmos retry loop,
   explicitly owns the disposal/continuation decision. Exact send counts and
   ordered client events must discriminate skipped/deferred cleanup.
7. Observe the actual server request-abort callback and handler exit separately,
   within a finite guard. Fixture shutdown is not client-release evidence.
   Never require server RST arrival to precede a later request: synchronous client
   disposal and eventual peer observation are different boundaries.
8. A terminal success remains readable and caller-owned until caller disposal.
   Cancellation/error paths propagate their original token/error, do not invent
   success or dispatch extra requests, and preserve a following healthy request.

Every assertion about failed ownership/disposal occurs before test teardown.
Teardown may clean up retained responses on a failing test, but its events cannot
retroactively satisfy an earlier dispatch or server-release assertion.

## Owned resources and finite guards

Use the preceding specification's section 10 safeguards as local fixture caps:
readiness 60 seconds, measured exchange 30 seconds, server release 5 seconds,
cleanup 30 seconds and whole case 120 seconds. These are failure guards, not
SDK latency SLAs, performance targets, sleeps or production-policy changes.
Explicit handshakes, not elapsed-time guesses, establish event ordering.

Own and cancel/await request tasks, server handlers, host startup/shutdown and
held streams. Stop the listener, dispose retained responses and the owned client,
cancel held handlers, await their exit, and dispose the host/certificate/tokens on
success, failure and cancellation. Bound waits and propagate timeout/cleanup
errors. Do not use global process kills, GC/unobserved-exception claims,
fire-and-forget continuations or success-shaped exception fallbacks.

## Test plan and discriminatory controls

| ID | Independent fixture obligation |
|---|---|
| L-01 | Healthy baseline: actual TLS/ALPN HTTP/2 at both ends; synthetic payload/header/status intact; final content readable and not disposed until caller ownership ends; following request succeeds on the same client. |
| L-02 | Two real held 408s followed by a synthetic success: each fault is incomplete and unread at client ownership; each completed-disposal witness precedes its next dispatch; exact three measured sends; separately await both server aborts/handler exits; final success and following request readable. |
| L-03 | Cancel from the first completed-disposal callback: caller token propagates, exactly one measured request, no second dispatch, eventual actual server release, then fresh-token recovery. |
| L-04 | Already-cancelled caller: no send or server exchange, original token propagation; subsequent healthy request still works. |
| L-05 | On a separately designated held-body read, cancellation propagates through the real delegated stream; it does not fabricate content-disposal completion; explicit response disposal and server release remain observable. This is not the unread L-02 witness. |
| L-06 | Skip/defer disposal: the unchanged next-dispatch gate rejects the live prior content before another request is sent. The journal must contain no disposal witness. Explicit cleanup restores the positive continuation. |
| L-07 | Requested HTTP/2 with allowed downgrade against an HTTP/1.1-only TLS control endpoint produces actual HTTP/1.1 and is rejected by the same admission gate. No fallback is green HTTP/2 proof. |
| L-08 | Before actual abort/handler exit, the server-release gate rejects the claim; after real disposal/release it accepts. Shutdown-only handler exit does not count as client release. |
| L-09 | Delegator controls with small synthetic sentinel contents/streams: headers and readable body, synchronous/asynchronous stream forwarding, copy error identity, read/copy cancellation, stream-owned disposal versus content witness, idempotent content disposal, and no completion after an underlying disposal error. |
| L-10 | Bounded teardown while a real response remains held: every owned handler exits, listener stops and cleanup completes, while the reason remains fixture shutdown rather than fabricated measured client-release proof. |

Controls are explicit local cases, not a mutation/fuzz framework. The recovery
after each negative control is the restored positive control. Preserve first
failures, skipped/aborted runs and exact case identities; totals alone are not
protocol or lifetime evidence.

## Runtime and ordinary CI applicability

The source remains net6.0/C# 10 and uses matching framework APIs. The actual local
host is macOS 26.7 / Darwin 25.6.0, arm64, SDK 9.0.304, with
Microsoft.NETCore.App and Microsoft.AspNetCore.App 9.0.8 (plus old 2.1 runtimes),
but initially no 6.x shared framework. SDK presence does not supply ASP.NET 6.

Microsoft explicitly documents that [Kestrel TLS HTTP/2 on macOS requires
.NET 8 or later][macos]. Consequently net6.0 HTTP/2 server execution on this
host is expected to be a visible unsupported-capability **nonpass**, not ignored,
retargeted, rolled forward, or replaced with plaintext. Installing a matching
official session-private 6.0.36 host/framework after an actual missing-dependency
failure or manifest change can validate compilation and independent observer/
HTTP1 control cases, but cannot remove the macOS ALPN limitation. Record official
distribution hashes and actual testhost identity; do not borrow another
worktree's runtime or install globally.

The existing `templates/build-test.yml` and preview task select this exact core
test project, without new filters. Their declarations install .NET 6 **runtime**
and .NET 8 **SDK**, not demonstrably ASP.NET 6. The test framework reference
therefore introduces an explicit native-agent prerequisite:
`Microsoft.AspNetCore.App` 6.x must actually exist.

The smallest source-controlled setup correction is to change only the existing
six-runtime acquisition task's `packageType` from `runtime` to `sdk` in the
three core unit jobs (ordinary, flaky and coverage) in `templates/build-test.yml`
and the two core preview unit jobs in `templates/build-preview.yml`. The
official .NET 6 SDK supplies the matching ASP.NET shared framework; the unchanged
subsequent .NET 8 SDK task remains the build SDK selection. No job/task is added
or removed; no other suite, project selector, argument/filter, trust/fork rule,
condition, retry/timeout, release boundary or protected-CI setting changes.
Do not modify emulator/encryption/performance jobs.

Agent/image labels, this setup declaration and old results are not an execution
receipt. A native testhost/framework/certificate/ALPN failure must remain a
nonpass and be reported; do not change pipeline settings or queue/rerun builds
to conceal it. Only ordinary PR-triggered CI is authorized here.

Native Windows/CI and GA/preview results may be claimed only when actually read
for this candidate. Local compilation or an MSBuild `OS=Windows` input is not
native execution. No new net8/net9 test target is included.

## Delivery and remaining gates

Commit/push this spec before executable edits; record that commit and its
implementation descendant. Keep the fixture feature branch independent of the
four existing PRs. Deliver a readable implementation/validation receipt retaining
commands, case leaves, initial failures, control/restoration outcomes, runtime,
protocol, resource-cleanup disposition and artifact identities. Open the user's
requested standalone fixture-only PR, not the completed pair.

Still separate and unapproved: isolated thin-capable account/topology authority,
signed test-envelope routing, public SDK operation integration, valid real
proxy-binary recovery, actual service/diagnostics results, native qualification
where absent, and protected-CI admission/activation. These local fixture tests
cannot close those gates or #5996's broader runtime characterization.

## Review correction: MSData/direct framework prerequisite

The [existing review thread][msdata-review] identifies a prerequisite omitted
from the initial implementation at
`491a312994e3154b89995d7da80a4e69c843931c`. The user subsequently requested
implementing applicable review feedback. This addendum precedes the correction
to the sibling templates; it does not authorize another PR or a TLS repair.

The unconditional `Microsoft.AspNetCore.App` framework reference applies to the
entire net6.0 testhost, before test filtering/discovery. A Flaky-only selection
therefore still needs ASP.NET Core 6 even though it does not select this fixture.
The three core unit jobs in `templates/build-test-msdata.yml` and two core unit
jobs in `templates/build-preview-msdata.yml` still acquire only runtime 6 and
SDK 8. `azure-pipelines-msdata-direct.yml` consumes these templates, while the
jobs select the same core test project as the ordinary templates.

Actual MSData/direct image framework inventories have **not** been verified.
Neither the `OneES` pool nor a `windows-latest` variable proves that ASP.NET
Core 6 is installed. This is a source-visible missing acquisition guarantee,
not an observed MSData job failure or an explanation for the separate native
TLS fixture failure.

Mirror the already implemented ordinary-template setup: change only the five
existing core-unit `UseDotNet@2` steps for `version: '6.x'` from
`packageType: 'runtime'` to `packageType: 'sdk'`. Preserve the following SDK 8
steps, all TFMs, job/task counts, test projects, filters/arguments, conditions,
retries/timeouts, pool/trust/fork rules and root pipeline configuration. Do not
change the MSData preview solution-build, emulator, encryption or performance
jobs. The [official .NET 6 SDK distribution][sdk6] includes both .NET and ASP.NET
Core runtimes; no new runtime generation, SDK/product retargeting or global
installation is needed.

Validation must parse both complete candidate YAML templates and compare them
with the preceding PR head. Independently identify all five core test jobs by
their actual test-project selection, require SDK 6 followed by SDK 8 before
their tests, and reject any other structural change. As controls, the old
templates must fail that prerequisite assertion and restoring the correction
must pass; a synthetic filter mutation must fail the unchanged-structure check.
Retain the existing test runtimeconfig's net6.0/ASP.NET 6 dependency as corroboration,
not native image evidence. No manual CI run, unrelated suite execution, TLS fix,
protected-check activation or permission change belongs to this correction.

[transport]: https://github.com/tvaron3/cosmosdb-design-docs/blob/bcfbc786fa5296deebcccd11369efb583e63e48c/06-transport-layer-rntbd.md#two-connectivity-modes
[retry]: https://github.com/tvaron3/cosmosdb-design-docs/blob/bcfbc786fa5296deebcccd11369efb583e63e48c/17-status-codes-and-sdk-retries.md#1-overview
[macos]: https://learn.microsoft.com/aspnet/core/grpc/troubleshoot#unable-to-start-aspnet-core-grpc-app-on-macos
[msdata-review]: https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6190#discussion_r4448100414
[sdk6]: https://dotnet.microsoft.com/download/dotnet/6.0
