# Distributed Write Transactions: Cross-Region Retry Signal

## 1. Purpose

This document proposes the wire contract that lets the Distributed Transactions Coordinator recognise a distributed write transaction request as a **retry that has crossed write regions**.

When a write region fails over, `CosmosClient` retries in-flight transactions against the coordinator in the new region. That coordinator cannot tell such a retry apart from a new transaction, so it cannot apply the checks needed to keep the transaction boundary intact.

## 2. Background: Transaction Boundaries and Failover

The coordinator drives a write transaction in three durable steps: insert a record into the transaction ledger, prepare the participants, then update the ledger with the terminal commit or abort. Holding to that sequence is what maintains the transaction boundary — either every operation takes effect, or none does.

How far that guarantee extends across regions depends on how the account commits:

- **N-region commit** — waits for commits from all N designated regions. Preserves the boundary and avoids data loss even when a region is lost mid-transaction, at a significant latency cost. Default for single-write-region accounts.
- **Quorum commit in the primary write region** — commits on a quorum in R1 alone. Materially faster, and chosen by customers for that reason, but the boundary is not guaranteed if a failover interrupts a transaction.

The trade-off is independent of the account's consistency level: what N-region commit buys is protection against data loss, not a stronger read guarantee.

**This document addresses the second case — accounts that do not use N-region commit.** Under N-region commit the boundary already survives a failover, so the signal has nothing to add. It is the accounts that traded that protection away for latency where a failover can strand a transaction, and where the coordinator in the new region needs to be told what happened.

## 3. Problem: Same Token, Different Region

An idempotency token identifies a logical attempt. `CosmosClient` replays the **same** token for every retry of an attempt, and issues a new token only when it resubmits after a retriable abort.

Replaying the token is what makes retries safe within one region: the coordinator that owns the attempt finds its own ledger record and resolves the request against state it already has, instead of executing the operations twice.

That does not survive a failover. The coordinator in the new region did not accept the attempt, so a token it has no record of is indistinguishable from a first submission — and handling it as one is what risks executing the transaction twice, or answering while the original attempt is still resolving. Only the client knows the attempt was previously dispatched elsewhere.

This matters at scale: a regional failover can leave a very large number of transactions in flight at once. The signal lets the coordinator resolve the majority automatically, leaving customers to manually resolve a transaction stuck in limbo only in rare edge cases.

## 4. Wire Contract

```http
x-ms-cosmos-dtx-cross-region-retry: False
```

- **Name** — follows the existing DTC request header family (`x-ms-cosmos-idempotency-token`, `x-ms-cosmos-operation-type`, `x-ms-cosmos-resource-type`). Pending coordinator-team sign-off (section 6).
- **Value** — `True` or `False`; no other value is defined.
- **Presence** — sent on **every** distributed write transaction request. `False` MUST carry exactly the meaning a request carries today; `True` reports that this attempt may already exist under the same token in another region. An absent header identifies a client predating this contract and MUST be treated as `False`.

> Transport dependency: the gateway-to-coordinator hop is RNTBD, so this header also requires a matching request identifier there. Allocating it is coordinator-side work, out of scope here.

## 5. Client Emission Rules

`CosmosClient` tracks the write region of each dispatch of the current idempotency token. Regions are compared by region identity, not by resolved endpoint: reaching the same region through a different endpoint is not a region change.

| Dispatch | Value |
| --- | --- |
| First dispatch of a token | `False` |
| Region unchanged, and no earlier dispatch of this token crossed a boundary | `False` |
| Region differs from the previous dispatch of this token | `True` |
| Any later dispatch of a token already at `True` | `True` |
| First dispatch of a new token after a retriable abort | `False` |

Two rules follow, both normative:

- **Sticky.** Once a token has crossed a region boundary, the header MUST stay `True` for every subsequent retry of that token, including retries within the new region, and MUST NOT revert. A dispatch lost in flight therefore cannot silently drop the signal.
- **Reset per token.** A retriable abort resubmits the same operations under a new idempotency token, which has no record in any region. Tracking MUST reset, so the new token starts at `False` even if its predecessor was `True`.

The value MUST be derived from the client's own routing state. `CosmosClient` already records retry counts and contacted regions in diagnostics, but only when the caller enables and serialises them; this signal MUST NOT depend on that.

## 6. Applicability, Compatibility and Open Questions

- **Write transactions only.** Read transactions carry no ledger or commit state, so replaying one elsewhere cannot execute a write twice or leave a transaction in limbo. `CosmosClient` MUST omit the header on them entirely rather than sending `False`.
- **Independent of account configuration.** Orthogonal to consistency level and commit configuration; it reports a routing fact and does not select behaviour.
- **Additive.** A coordinator that does not recognise the header MUST behave exactly as it does today, and `CosmosClient` MUST remain correct against such a coordinator.
- **No public API change.** Nothing in this contract is caller-visible.
- **Open — header name.** `x-ms-cosmos-dtx-cross-region-retry` is proposed for consistency with the existing family; the final name and its RNTBD identifier need coordinator-team sign-off.
- **Open — rollout sequencing.** Correctness holds in either order since the header is additive, but the protection takes effect only once the coordinator understands it. To confirm: how early the client change ships, so that customers do not have to upgrade twice.
