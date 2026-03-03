# Consistency and Session Management

## Purpose

The SDK supports five consistency levels and automatically manages session tokens to ensure read-your-writes guarantees when using session consistency.

## Requirements

### Requirement: Consistency Level Configuration
The SDK SHALL support overriding the account-level consistency at the client and request levels.

#### Scenario: Client-level consistency override
- GIVEN `CosmosClientOptions.ConsistencyLevel = ConsistencyLevel.Eventual`
- WHEN requests are made through this client
- THEN the Eventual consistency level is used by default for all requests
- AND this MAY only weaken the account-level consistency, never strengthen it

#### Scenario: Per-request consistency override
- GIVEN `RequestOptions.ConsistencyLevel = ConsistencyLevel.Strong`
- WHEN a specific request is made
- THEN the Strong consistency level is used for that request only

#### Scenario: No override (account default)
- GIVEN neither client-level nor request-level consistency is set
- WHEN requests are made
- THEN the account's default consistency level is used

### Requirement: Consistency Levels
The SDK SHALL support all five Azure Cosmos DB consistency levels.

#### Scenario: Strong consistency
- GIVEN `ConsistencyLevel.Strong` is configured
- WHEN a read is performed
- THEN the most recent committed write is always returned
- AND reads are linearizable

#### Scenario: Bounded staleness
- GIVEN `ConsistencyLevel.BoundedStaleness` is configured
- WHEN a read is performed
- THEN the read is at most K versions or T seconds behind the latest write

#### Scenario: Session consistency (default)
- GIVEN `ConsistencyLevel.Session` is configured (or account default is Session)
- WHEN reads and writes are performed within the same session
- THEN monotonic reads and read-your-writes are guaranteed within that session

#### Scenario: Consistent prefix
- GIVEN `ConsistencyLevel.ConsistentPrefix` is configured
- WHEN reads are performed
- THEN reads never see out-of-order writes (no gaps in write sequence)

#### Scenario: Eventual consistency
- GIVEN `ConsistencyLevel.Eventual` is configured
- WHEN a read is performed
- THEN the read returns data that will eventually converge to the latest write

### Requirement: Session Token Management
The SDK SHALL automatically manage session tokens to maintain session consistency guarantees.

#### Scenario: Automatic session token capture
- GIVEN a write operation completes
- WHEN the response is received
- THEN the SDK automatically captures and stores the session token from the response
- AND associates it with the container and partition key range

#### Scenario: Automatic session token propagation
- GIVEN a session token has been captured from a previous write
- WHEN a subsequent read request is made to the same container
- THEN the SDK automatically includes the stored session token in the request header

#### Scenario: Session token across partitions
- GIVEN writes to partition A and partition B
- WHEN reads are performed
- THEN each partition has its own session token
- AND session consistency is maintained independently per partition

#### Scenario: Manual session token
- GIVEN `RequestOptions.SessionToken` is explicitly set
- WHEN the request is made
- THEN the provided session token is used instead of the SDK-managed token

### Requirement: Session Container
The SDK SHALL maintain an internal session container for token storage.

#### Scenario: Token storage lifecycle
- GIVEN a `CosmosClient` instance
- WHEN operations are performed
- THEN session tokens are stored in memory for the lifetime of the client

#### Scenario: Cross-client session continuity
- GIVEN a session token obtained from one client's response
- WHEN that token is passed to another client via `RequestOptions.SessionToken`
- THEN the second client can achieve session-consistent reads relative to the first client's writes

### Requirement: Consistency Weakening Validation
The SDK SHALL only allow weakening the account-level consistency, not strengthening it.

#### Scenario: Weaken from Strong to Session
- GIVEN account-level consistency is Strong
- WHEN `CosmosClientOptions.ConsistencyLevel = ConsistencyLevel.Session` is set
- THEN the client uses Session consistency (valid weakening)

#### Scenario: Attempt to strengthen
- GIVEN account-level consistency is Eventual
- WHEN `CosmosClientOptions.ConsistencyLevel = ConsistencyLevel.Strong` is set
- THEN the service rejects the request with a 400 (Bad Request) error

## Key Source Files
- `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs` — `ConsistencyLevel` property
- `Microsoft.Azure.Cosmos/src/RequestOptions/RequestOptions.cs` — per-request consistency and session token
- `Microsoft.Azure.Cosmos/src/SessionContainer.cs` — session token management
- `Microsoft.Azure.Cosmos/src/SessionRetryOptions.cs` — session retry configuration
- `Microsoft.Azure.Cosmos/src/GatewayStoreModel.cs` — session token propagation in gateway mode
