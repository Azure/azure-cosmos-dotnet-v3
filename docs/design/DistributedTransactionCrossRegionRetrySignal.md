# Distributed Write Transactions: Cross-Region Retry Signal

## 1. Purpose

This document proposes the wire contract that lets the Distributed Transactions Coordinator recognise a distributed write transaction request as a **retry that has crossed write regions**.

In `FastResponse` mode the coordinator acknowledges a transaction as soon as Phase 1 is durably complete, so a transaction can still be in progress when `CosmosClient` stops waiting for it. If `CosmosClient` then fails over to another write region and resubmits, the coordinator in the new region receives a request it cannot distinguish from a brand-new transaction. This document defines a request header that removes that ambiguity.

## 2. Problem: Same Token, Different Region

An idempotency token identifies a logical attempt. `CosmosClient` replays the **same** token for every retry of an attempt, and only issues a new token when it resubmits after a retriable abort.

Replaying the same token is what makes retries safe within one region: the coordinator that owns the attempt finds its own record for the token and returns the outcome it already produced instead of executing the operations twice.

That guarantee does not carry across a write-region failover. The coordinator in the new region is not the coordinator that accepted the attempt, so from its point of view an incoming request carrying a token it has never seen is indistinguishable from a first submission. In `Standard` mode this is largely academic, because the transaction is already resolved before `CosmosClient` returns. In `FastResponse` mode it is not: the transaction may still be in flight, and the coordinator in the new region has to choose between two incompatible answers — acknowledge fast, or reconcile first and answer `Standard` — with no information on which is correct.

The coordinator cannot infer this on its own. It needs `CosmosClient` to state it, because only the client knows which region the previous dispatch of this token went to.

## 3. Wire Contract

`CosmosClient` signals a cross-region retry with a single request header:

```http
x-ms-cosmos-dtx-cross-region-retry: True
```

- **Name** — `x-ms-cosmos-dtx-cross-region-retry`, following the existing DTC request header family (`x-ms-cosmos-idempotency-token`, `x-ms-cosmos-operation-type`, `x-ms-cosmos-resource-type`). The final name is pending coordinator-team sign-off (section 7).
- **Value** — `True`. No other value is defined.
- **Semantics** — presence-only. The header is present **only** on a cross-region retry. Its absence means "not a cross-region retry" and MUST carry exactly the meaning a request carries today.

The header is never sent on its own: it always accompanies an `x-ms-cosmos-idempotency-token` that has already been dispatched at least once, in a different write region.

> Transport dependency: the gateway-to-coordinator hop is RNTBD, so this header also requires a matching request identifier in that protocol, allocated from the next free slot (the highest currently allocated identifier is `0x0109`). That allocation is coordinator-side work and is out of scope for this document.

## 4. Client Emission Rules

`CosmosClient` tracks the write region each dispatch of the current idempotency token is sent to, and applies the following rules.

### 4.1 When the header is set

`CosmosClient` MUST set the header on a dispatch when **both** hold:

- the token being sent has already been dispatched at least once, and
- the write region resolved for this dispatch differs from the region of the previous dispatch of that same token.

### 4.2 When the header is omitted

`CosmosClient` MUST NOT set the header when:

- this is the first dispatch of the token — there is no previous region, so there is nothing to reconcile against; or
- the resolved write region is unchanged from the previous dispatch of the token.

### 4.3 The signal is sent once per region change

The header is set on the **first** dispatch into the newly-resolved region and MUST NOT be repeated on subsequent dispatches into that same region, even if the dispatch that carried it failed and is retried.

Each further region change is a new signal: if a later dispatch resolves to a region that again differs from the preceding one, the header is set once more. Returning to a region the token was dispatched to earlier still counts as a change, because it differs from the immediately preceding dispatch.

### 4.4 New-token resubmission resets tracking

After a retriable abort, `CosmosClient` resubmits the same operations under a **new** idempotency token. Region tracking MUST reset at that point: the new token has no record in any region, so its first dispatch is a first dispatch under section 4.2 and carries no header.

### 4.5 The signal is independent of diagnostics

The header MUST be emitted from the client's own routing state, and MUST NOT depend on diagnostics being enabled, captured, or serialised by the caller.

## 5. Coordinator Expectations

On receiving a request carrying the header, the coordinator is expected to resolve the idempotency token against durable state — the transaction ledger and the participant records — **before** deciding how to answer, rather than treating the request as a new transaction.

That lookup yields one of:

- the token corresponds to a transaction that has reached a terminal outcome, and the recorded outcome is returned;
- the token corresponds to a transaction still in progress, and the coordinator drives or awaits its resolution rather than executing the operations again;
- the token has no durable record, and the request is handled as a first submission.

A request **without** the header keeps its current handling. The header therefore only ever adds reconciliation work on the small number of requests that actually crossed regions, which is what keeps `FastResponse` fast in the common case.

## 6. Applicability and Compatibility

**Write transactions only.** The header applies to distributed *write* transactions. Distributed read transactions carry no ledger or commit state, so replaying one in another region cannot double-execute a write or produce an inconsistent outcome, and `CosmosClient` MUST NOT set the header on them.

**Additive and ignorable.** The header is purely additive. A coordinator that does not recognise it MUST handle the request exactly as it does today, and `CosmosClient` MUST remain correct when talking to such a coordinator — the header changes what the coordinator *can* know, never what the client requires it to do.

**No public API change.** Nothing in this contract is caller-visible. It introduces no new public type, member, or option; `ExecuteTransactionAsync` and `DistributedTransactionResponse` are unchanged.

## 7. Open Questions

- **Header name.** `x-ms-cosmos-dtx-cross-region-retry` is proposed here for consistency with the existing DTC header family; the final name and its RNTBD identifier need coordinator-team sign-off before implementation.
- **Signal lost in flight.** Under section 4.3 the signal is sent once per region change. If the dispatch carrying it never reaches the coordinator, the following retry into that same region carries no header, and the coordinator handles it as an ordinary same-region request. The alternative — repeating the header on every dispatch until an answer is received — would close this gap at the cost of extra reconciliation on every retry after a failover. The once-per-change rule is proposed on the basis that the failed dispatch left no durable record to reconcile against.
