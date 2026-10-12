# Cross-SDK Peer Map (canonical)

This is the canonical default peer mapping the **PR Review Router** passes to the **Cross SDK Reviewer** agent. Editing this file lets you tune cross-SDK pollination behaviour without modifying any underlying agent prompt.

The router reads this file at run time. If the file is missing or empty, the router falls back to the embedded table in its own prompt.

## Per-SDK peer defaults

| Primary SDK | Default peers (core) | Default peers (connector) | Notes |
|---|---|---|---|
| dotnet | java, python | — | .NET is reference for most newer features |
| java | dotnet, python | — | Java often leads on async/reactive patterns |
| python | java, dotnet | — | |
| go | java, rust | — | Go shares concurrency idioms with Rust core |
| rust | go, java | — | Rust core engine; check go for FFI/borrow patterns |
| spark | — | java, kafka | Connector — peer with Java SDK and Kafka connector |
| kafka | — | spark, java | Connector — peer with Spark connector and Java SDK |
| spring-data | java | — | Wraps Java SDK; almost always co-touched |

## Connector vs core detection

Treat a PR as a **connector** PR if its primary SDK is `spark`, `kafka`, or `spring-data`, OR if ≥ 60% of its source-changed files live under a connector path prefix. Otherwise it's a core PR.

## Peer scope rules

- Pass at most **3** peers to Cross SDK Reviewer. If you would pass more, drop the least-related.
- Always include the SDK's own area path in each peer's sandbox so Cross SDK Reviewer knows where to start (e.g. `~/Data/microsoft/sandbox/azure-sdk-for-java/sdk/cosmos/`).
- The user's `--peers <csv>` flag overrides this map entirely (no merge).

## When a peer cannot be resolved

If a peer SDK's sandbox repo is missing, Cross SDK Reviewer should:
1. Note the gap explicitly in its findings (`category: parity, severity: nit, message: "<peer> sandbox not available; parity not verified"`).
2. Continue with the remaining peers.

The router treats this as a non-fatal degraded mode.

## Sandbox paths (for Cross SDK Reviewer prompt injection)

| SDK | Sandbox path |
|---|---|
| dotnet | `~/Data/microsoft/sandbox/azure-cosmos-dotnet-v3` |
| java | `~/Data/microsoft/sandbox/azure-sdk-for-java/sdk/cosmos` |
| python | `~/Data/microsoft/sandbox/azure-sdk-for-python/sdk/cosmos` |
| go | `~/Data/microsoft/sandbox/azure-sdk-for-go/sdk/data/azcosmos` |
| rust | `~/Data/microsoft/sandbox/azure-sdk-for-rust/sdk/cosmos` |
| spark | `~/Data/microsoft/sandbox/azure-sdk-for-java/sdk/cosmos/azure-cosmos-spark_3-5_2-12` (and siblings) |
| kafka | `~/Data/microsoft/sandbox/kafka-connect-cosmosdb` |
| spring-data | `~/Data/microsoft/sandbox/azure-sdk-for-java/sdk/spring/azure-spring-data-cosmos` |

Verify with `list_sandbox_repos` if a path looks stale; do not modify this file from inside an automated agent run — the user maintains it.
