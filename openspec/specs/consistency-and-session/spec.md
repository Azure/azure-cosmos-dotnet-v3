# Consistency and Session Management

## Purpose

The SDK supports five consistency levels and automatically manages session tokens to ensure read-your-writes guarantees when using session consistency.

## Requirements

### Requirement: Consistency Level Configuration
The SDK SHALL support overriding the account-level consistency at the client and request levels.

#### Client-level consistency override
**Where** `CosmosClientOptions.ConsistencyLevel = ConsistencyLevel.Eventual`, **when** requests are made through this client, the SDK shall use the Eventual consistency level by default for all requests. This MAY only weaken the account-level consistency, never strengthen it.

#### Per-request consistency override
**Where** `RequestOptions.ConsistencyLevel = ConsistencyLevel.Strong`, **when** a specific request is made, the SDK shall use the Strong consistency level for that request only.

#### No override (account default)
**Where** neither client-level nor request-level consistency is set, **when** requests are made, the SDK shall use the account's default consistency level.

### Requirement: Consistency Levels
The SDK SHALL support all five Azure Cosmos DB consistency levels.

#### Strong consistency
**Where** `ConsistencyLevel.Strong` is configured, **when** a read is performed, the SDK shall always return the most recent committed write and ensure reads are linearizable.

#### Bounded staleness
**Where** `ConsistencyLevel.BoundedStaleness` is configured, **when** a read is performed, the SDK shall return data that is at most K versions or T seconds behind the latest write.

#### Session consistency (default)
**Where** `ConsistencyLevel.Session` is configured (or account default is Session), **when** reads and writes are performed within the same session, the SDK shall guarantee monotonic reads and read-your-writes within that session.

#### Consistent prefix
**Where** `ConsistencyLevel.ConsistentPrefix` is configured, **when** reads are performed, the SDK shall ensure reads never see out-of-order writes (no gaps in write sequence).

#### Eventual consistency
**Where** `ConsistencyLevel.Eventual` is configured, **when** a read is performed, the SDK shall return data that will eventually converge to the latest write.

### Requirement: Session Token Management
The SDK SHALL automatically manage session tokens to maintain session consistency guarantees.

#### Automatic session token capture
**When** a write operation completes and a response is received, the SDK shall automatically capture and store the session token from the response and associate it with the container and partition key range.

#### Automatic session token propagation
**While** a session token has been captured from a previous write, **when** a subsequent read request is made to the same container, the SDK shall automatically include the stored session token in the request header.

#### Session token across partitions
**When** writes are performed to multiple partitions and subsequent reads are performed, the SDK shall maintain a separate session token for each partition and ensure session consistency is maintained independently per partition.

#### Manual session token
**Where** `RequestOptions.SessionToken` is explicitly set, **when** the request is made, the SDK shall use the provided session token instead of the SDK-managed token.

### Requirement: Session Container
The SDK SHALL maintain an internal session container for token storage.

#### Token storage lifecycle
**While** a `CosmosClient` instance is active, **when** operations are performed, the SDK shall store session tokens in memory for the lifetime of the client.

#### Cross-client session continuity
**When** a session token obtained from one client's response is passed to another client via `RequestOptions.SessionToken`, the SDK shall enable the second client to achieve session-consistent reads relative to the first client's writes.

### Requirement: Consistency Weakening Validation
The SDK SHALL only allow weakening the account-level consistency, not strengthening it.

#### Weaken from Strong to Session
**Where** `CosmosClientOptions.ConsistencyLevel = ConsistencyLevel.Session` is set on an account with Strong consistency, the SDK shall use Session consistency as a valid weakening of the account-level consistency.

#### Attempt to strengthen
**If** `CosmosClientOptions.ConsistencyLevel = ConsistencyLevel.Strong` is set on an account with Eventual consistency, **then** the SDK shall have the service reject the request with a 400 (Bad Request) error.

## Key Source Files
- `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs` — `ConsistencyLevel` property
- `Microsoft.Azure.Cosmos/src/RequestOptions/RequestOptions.cs` — per-request consistency and session token
- `Microsoft.Azure.Cosmos/src/SessionContainer.cs` — session token management
- `Microsoft.Azure.Cosmos/src/SessionRetryOptions.cs` — session retry configuration
- `Microsoft.Azure.Cosmos/src/GatewayStoreModel.cs` — session token propagation in gateway mode
