# Consistency and Session Management

## Purpose

The Azure Cosmos DB .NET SDK supports five consistency levels and automatically manages session tokens to ensure read-your-writes guarantees when using session consistency. Consistency can be configured at the account, client, or per-request level, with each level only able to weaken (never strengthen) the parent level's guarantee.

## Public API Surface

### Consistency Level Configuration

```csharp
// Client-level override (weakens account-level)
CosmosClientOptions options = new CosmosClientOptions
{
    ConsistencyLevel = ConsistencyLevel.Session
};

// Per-request override
ItemRequestOptions requestOptions = new ItemRequestOptions
{
    ConsistencyLevel = ConsistencyLevel.Eventual
};
```

### Session Token Management

```csharp
// Automatic: SDK manages session tokens internally

// Manual: Pass session token from one client to another
ItemResponse<T> writeResponse = await client1Container.CreateItemAsync(item, pk);
string sessionToken = writeResponse.Headers.Session;

ItemRequestOptions readOptions = new ItemRequestOptions
{
    SessionToken = sessionToken
};
ItemResponse<T> readResponse = await client2Container.ReadItemAsync<T>(id, pk, readOptions);
```

## Requirements

### Requirement: Consistency Level Configuration

The SDK SHALL support overriding the account-level consistency at the client and request levels.

#### Client-level consistency override

**Where** `CosmosClientOptions.ConsistencyLevel` is set, **when** requests are made through this client, the SDK SHALL use the specified consistency level by default. This MAY only weaken the account-level consistency, never strengthen it.

#### Per-request consistency override

**Where** `RequestOptions.ConsistencyLevel` is set on a specific request, **when** that request is made, the SDK SHALL use the per-request consistency level, overriding the client-level setting.

#### No override (account default)

**Where** neither client-level nor request-level consistency is set, **when** requests are made, the SDK SHALL use the account's default consistency level.

#### Configuration precedence

Per-request `RequestOptions.ConsistencyLevel` > client-wide `CosmosClientOptions.ConsistencyLevel` > account default.

### Requirement: Consistency Levels

The SDK SHALL support all five Azure Cosmos DB consistency levels.

| Level | Guarantee |
|-------|-----------|
| `Strong` | Linearizable reads — always returns the most recent committed write |
| `BoundedStaleness` | Reads lag by at most K versions or T seconds |
| `Session` | Read-your-writes and monotonic reads within a session (default) |
| `ConsistentPrefix` | Reads never see out-of-order writes |
| `Eventual` | Reads eventually converge to latest write |

### Requirement: Session Token Management

The SDK SHALL automatically manage session tokens to maintain session consistency guarantees.

#### Automatic session token capture

**When** a write operation completes, the SDK SHALL automatically capture the session token from the response and associate it with the container and partition key range.

#### Automatic session token propagation

**While** a session token has been captured from a previous write, **when** a subsequent read request is made to the same container, the SDK SHALL automatically include the stored session token in the request header.

#### Session token per partition

**When** writes are performed to multiple partitions, the SDK SHALL maintain a separate session token for each partition key range and ensure session consistency is maintained independently per partition.

#### Manual session token override

**Where** `RequestOptions.SessionToken` is explicitly set, **when** the request is made, the SDK SHALL use the provided session token instead of the SDK-managed token.

### Requirement: Session Container

The SDK SHALL maintain an internal session container for token storage.

#### Token storage lifecycle

**While** a `CosmosClient` instance is active, the SDK SHALL store session tokens in memory for the lifetime of the client.

#### Cross-client session continuity

**When** a session token obtained from one client's response is passed to another client via `RequestOptions.SessionToken`, the SDK SHALL enable the second client to achieve session-consistent reads relative to the first client's writes.

### Requirement: Consistency Weakening Validation

The SDK SHALL only allow weakening the account-level consistency, not strengthening it.

#### Weaken from Strong to Session

**Where** `CosmosClientOptions.ConsistencyLevel = ConsistencyLevel.Session` is set on an account with Strong consistency, the SDK SHALL use Session consistency as a valid weakening.

#### Attempt to strengthen

**If** `CosmosClientOptions.ConsistencyLevel = ConsistencyLevel.Strong` is set on an account with Eventual consistency, **then** the service SHALL reject the request with a 400 (Bad Request) error.

## Interactions

- **Retry Policies**: Session token mismatches (404/1002) trigger `ResetSessionTokenRetryPolicy` retries. See `retry-and-failover` spec.
- **Client Configuration**: Consistency is configured via `CosmosClientOptions`. See `client-and-configuration` spec.
- **Cross-Region Hedging**: Session tokens are propagated to hedged requests. See `cross-region-hedging` spec.
- **Transport**: Session token headers are attached in both Gateway and Direct mode. See `transport-and-connectivity` spec.

## References

- Source: `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs`
- Source: `Microsoft.Azure.Cosmos/src/RequestOptions/RequestOptions.cs`
- Source: `Microsoft.Azure.Cosmos/src/SessionContainer.cs`
- Source: `Microsoft.Azure.Cosmos/src/SessionRetryOptions.cs`
- Source: `Microsoft.Azure.Cosmos/src/GatewayStoreModel.cs`