# Distributed Write Transactions: Cross-Region Retry Signal

## 1. Purpose

This document proposes the wire contract that lets the Distributed Transactions Coordinator recognise a distributed write transaction request as a **retry that has crossed write regions**.

When a write region fails over, `CosmosClient` retries in-flight transactions against the coordinator in the new write region. That coordinator has no way to tell such a retry apart from a brand-new transaction, so it cannot apply the extra checks needed to keep the transaction's boundary intact. This document defines a request header that removes that ambiguity.

## 2. Background: Transaction Boundaries and Failover

A distributed write transaction is driven by the coordinator in three durable steps: it inserts a record into the transaction ledger, prepares the participants, and then updates the ledger to reflect the terminal commit or abort. Holding to that sequence is what maintains the transaction boundary — either every operation in the transaction takes effect, or none does.

How far that guarantee extends across regions depends on how the account commits:

- **N-region commit** — the transaction waits for commits from all N designated regions (R1, R2, …). This preserves the transaction boundary and avoids data loss even when a region is lost mid-transaction, at the cost of significantly higher latency. It is the default for single-write-region accounts, and applies conceptually to multi-region accounts although it is less meaningful there.
- **Quorum commit in the primary write region** — the transaction commits on a quorum in R1 alone. This is materially faster, and some customers choose it for that reason, but the transaction boundary is not guaranteed to be honoured if a failover happens while the transaction is in flight.

This trade-off is independent of the account's consistency level; it applies even under eventual consistency, because what N-region commit buys is protection against data loss, not a stronger read guarantee.

The gap this document addresses is the second case. When a failover interrupts a transaction, the client retries in the new region — and the coordinator there needs to know that is what happened.

## 3. Problem: Same Token, Different Region

An idempotency token identifies a logical attempt. `CosmosClient` replays the **same** token for every retry of an attempt, and only issues a new token when it resubmits after a retriable abort.

Replaying the same token is what makes retries safe within one region: the coordinator that owns the attempt finds its own ledger record for the token and resolves the request against the state it already has, instead of executing the operations a second time.

That does not carry across a write-region failover. The coordinator in the new region did not accept the attempt, so an incoming request carrying a token it has no record of is indistinguishable from a first submission — and handling it as a first submission is exactly what risks executing the transaction twice, or answering inconsistently while the original attempt is still resolving.

The coordinator cannot infer this on its own. It needs `CosmosClient` to state it, because only the client knows that this attempt was previously dispatched to a different write region.

This matters at scale. A regional failover can leave a very large number of transactions in flight at once. Signalling the cross-region retry lets the coordinator resolve the great majority of them automatically, so that customers are left to manually resolve a transaction stuck in limbo only in rare edge cases.

## 4. Wire Contract

`CosmosClient` reports whether a distributed write transaction request has crossed write regions with a single boolean request header:

```http
x-ms-cosmos-dtx-cross-region-retry: False
```

- **Name** — `x-ms-cosmos-dtx-cross-region-retry`, following the existing DTC request header family (`x-ms-cosmos-idempotency-token`, `x-ms-cosmos-operation-type`, `x-ms-cosmos-resource-type`). The final name is pending coordinator-team sign-off (section 8).
- **Value** — `True` or `False`. No other value is defined.
- **Semantics** — the header is present on **every** distributed write transaction request. `False` means this attempt has not crossed a write-region boundary, and MUST carry exactly the meaning a request carries today. `True` means it has, and is the signal to reconcile.

Because the header is always present, the coordinator can read the value directly rather than inferring intent from a missing header, and a request whose header is absent is a request from a client that predates this contract.

> Transport dependency: the gateway-to-coordinator hop is RNTBD, so this header also requires a matching request identifier in that protocol. Allocating it is coordinator-side work and is out of scope for this document.

## 5. Client Emission Rules

`CosmosClient` tracks the write region each dispatch of the current idempotency token is sent to, and applies the following rules. Regions are compared by region identity, not by resolved endpoint: two dispatches that reach the same write region through different endpoints are not a region change.

### 5.1 When the header is `True`

`CosmosClient` MUST send `True` on a dispatch when **either** holds:

- the write region resolved for this dispatch differs from the region of the previous dispatch of the same token; or
- the header was already `True` on an earlier dispatch of the same token.

### 5.2 The value is sticky for the token

Once a token has been dispatched across a region boundary, that fact does not become untrue. The header MUST therefore stay `True` for every subsequent retry of that token, including retries that stay within the new region. It MUST NOT revert to `False`.

This keeps the contract simple in both directions: the client does not have to reason about whether an earlier signal was actually received, and the coordinator does not have to distinguish a first cross-region attempt from a later one. It also means a dispatch lost in flight cannot silently drop the signal.

### 5.3 When the header is `False`

`CosmosClient` MUST send `False` when:

- this is the first dispatch of the token — there is no previous region, so there is nothing to reconcile against; or
- no dispatch of this token has yet crossed a region boundary, and the resolved write region is unchanged from the previous dispatch.

In other words, the header starts at `False` and stays there for ordinary same-region retries, whose handling by the coordinator is unchanged. It flips to `True` on the first dispatch after a write-region failover.

### 5.4 New-token resubmission resets the value

After a retriable abort, `CosmosClient` resubmits the same operations under a **new** idempotency token. Region tracking MUST reset at that point: the new token has no record in any region, so its first dispatch is a first dispatch under section 5.3 and carries `False`, even if the token it replaced was at `True`.

### 5.5 The signal is independent of diagnostics

`CosmosClient` already records retry counts and contacted regions in diagnostics, but only when the caller enables and serialises them. This signal MUST NOT depend on that. It MUST be emitted from the client's own routing state, as an explicit indication that this attempt has been dispatched across a write-region boundary.

## 6. Coordinator Expectations

On receiving a request whose header is `True`, the coordinator is expected to read the ledger record and the participant records for that idempotency token **before** deciding how to answer, rather than treating the request as a new transaction.

Based on the state of the participants, that lookup resolves the request to one of:

- the transaction is committed, and the recorded outcome is returned;
- the transaction is aborted, and the abort is returned;
- the transaction cannot yet be resolved, and the request times out rather than being executed again;
- the token has no durable record, and the request is handled as a first submission.

A request whose header is `False`, or absent, keeps its current handling. The additional reconciliation work is therefore incurred only on requests that actually crossed a region boundary.

## 7. Applicability and Compatibility

**Write transactions only.** The header applies to distributed *write* transactions. Distributed read transactions carry no ledger or commit state, so replaying one in another region cannot execute a write twice or leave a transaction in limbo. `CosmosClient` MUST omit the header entirely on them — not send it as `False`.

**Independent of account configuration.** The signal is orthogonal to the account's consistency level and to its commit configuration. It reports a routing fact; it does not select behaviour.

**Additive and ignorable.** The header is purely additive. A coordinator that does not recognise it MUST handle the request exactly as it does today, and `CosmosClient` MUST remain correct when talking to such a coordinator — the header changes what the coordinator *can* know, never what the client requires it to do. Conversely, an updated coordinator MUST treat an absent header as `False`, so that clients predating this contract keep working.

**No public API change.** Nothing in this contract is caller-visible. It introduces no new public type, member, or option.

## 8. Open Questions

- **Header name.** `x-ms-cosmos-dtx-cross-region-retry` is proposed here for consistency with the existing DTC header family; the final name and its RNTBD identifier need coordinator-team sign-off before implementation.
- **Rollout sequencing.** Correctness holds in either order, because the header is additive and ignorable (section 7) — but the boundary protection it enables only takes effect once the coordinator understands it. The sequencing to confirm with the coordinator team is how early the client-side change should ship, so that customers do not need to upgrade the SDK a second time to pick up the behaviour.
