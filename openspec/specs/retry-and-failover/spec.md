# Retry and Failover

## Purpose

The SDK's retry and failover layer handles transient errors, throttling (429), service unavailability (503), and cross-region failover to maintain high availability and resilience.

## Requirements

### Requirement: Throttling Retry (429)
The SDK SHALL automatically retry requests that receive a 429 (Too Many Requests) response with exponential backoff.

#### Scenario: Retry on throttle
- GIVEN a request that receives a 429 response
- WHEN the SDK evaluates the response
- THEN the request is retried after waiting for the duration specified in the `x-ms-retry-after-ms` header
- AND retries continue up to `CosmosClientOptions.MaxRetryAttemptsOnRateLimitedRequests` times (default: 9)

#### Scenario: Max retry time exceeded
- GIVEN throttling retries have been attempted
- WHEN the cumulative wait time exceeds `CosmosClientOptions.MaxRetryWaitTimeOnRateLimitedRequests` (default: 30 seconds)
- THEN the SDK stops retrying and returns the 429 response to the caller

#### Scenario: Custom retry configuration
- GIVEN `CosmosClientOptions.MaxRetryAttemptsOnRateLimitedRequests = 3` and `MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(10)`
- WHEN a request is continuously throttled
- THEN at most 3 retries are attempted within a 10-second window

### Requirement: Region Failover on Endpoint Unavailability
The SDK SHALL fail over to the next preferred region when an endpoint becomes unavailable.

#### Scenario: Service unavailable (503)
- GIVEN a request receives a 503 (Service Unavailable) response
- WHEN the SDK evaluates the response
- THEN the current endpoint is marked unavailable
- AND the request is retried on the next region in `ApplicationPreferredRegions`
- AND at most 1 service-unavailable retry is attempted per region

#### Scenario: Write forbidden (403/3)
- GIVEN a write request receives a 403 with sub-status code 3 (WriteForbidden)
- WHEN the SDK evaluates the response
- THEN the current write endpoint is marked unavailable
- AND the request is retried on the next preferred write region

#### Scenario: Request timeout (408)
- GIVEN a request receives a 408 (Request Timeout)
- WHEN the SDK evaluates the response
- THEN the partition key range is marked unavailable in the current region
- AND the request is retried on the next preferred region

#### Scenario: HTTP request exception
- GIVEN a request fails with an `HttpRequestException` (network-level failure)
- WHEN the SDK evaluates the exception
- THEN the partition is marked unavailable
- AND the request is retried on the next preferred region

#### Scenario: Maximum failover retries
- GIVEN the SDK has attempted failover retries
- WHEN the retry count reaches 120
- THEN no further retries are attempted and the error is returned to the caller

### Requirement: Gone (410) Handling
The SDK SHALL handle 410 (Gone) responses by refreshing routing information.

#### Scenario: Partition key range gone
- GIVEN a request receives 410 with sub-status 1002 (PartitionKeyRangeGone)
- WHEN the SDK evaluates the response
- THEN the partition key range cache is invalidated
- AND the request is retried with refreshed routing information

#### Scenario: Name cache stale
- GIVEN a request receives 410 with sub-status indicating stale cache
- WHEN the SDK evaluates the response
- THEN the collection cache is refreshed
- AND the request is retried

### Requirement: Per-Partition Automatic Failover (PPAF)
The SDK SHALL support per-partition-level failover for Direct mode connections.

#### Scenario: Partition-level 503 in single-master
- GIVEN a write request to a specific partition receives 503
- AND the account is single-master with `ApplicationPreferredRegions` configured
- WHEN the SDK evaluates the response
- THEN that specific partition is marked unavailable in the current region
- AND the retry targets the next region in `ApplicationPreferredRegions` for that partition only

#### Scenario: System resource unavailable (429/3092)
- GIVEN a request receives 429 with sub-status code 3092 (SystemResourceUnavailable)
- WHEN the SDK evaluates the response
- THEN this is treated as a 503-equivalent
- AND partition-level failover is triggered to the next region

#### Scenario: PPAF does not affect other partitions
- GIVEN partition A is marked unavailable in Region 1
- WHEN a request for partition B is made
- THEN partition B's request is routed to Region 1 normally
- AND only partition A's requests are routed to Region 2

### Requirement: Retry Wait Time
The SDK SHALL wait an appropriate interval between failover retries.

#### Scenario: Failover retry delay
- GIVEN a region failover is triggered
- WHEN the SDK prepares to retry on the next region
- THEN it waits 1000 milliseconds before sending the retry

### Requirement: Retry Policy Independence
The SDK SHALL ensure each retry scope operates independently.

#### Scenario: Independent retry chains
- GIVEN a request is being retried due to region failover
- WHEN the retry request encounters a new transient error
- THEN the new error is evaluated independently by the retry policy
- AND the original retry context is preserved

### Requirement: Excluded Regions
The SDK SHALL support excluding specific regions from request routing.

#### Scenario: Exclude region via RequestOptions
- GIVEN `RequestOptions.ExcludeRegions` contains "West US 2"
- WHEN a request is routed
- THEN "West US 2" is not considered for that request
- AND the next available region in the preferred list is used

## Key Source Files
- `Microsoft.Azure.Cosmos/src/ClientRetryPolicy.cs` — main retry orchestrator
- `Microsoft.Azure.Cosmos/src/RetryPolicy.cs` — factory for ClientRetryPolicy
- `Microsoft.Azure.Cosmos/src/ResourceThrottleRetryPolicy.cs` — 429 throttle retry
- `Microsoft.Azure.Cosmos/src/Routing/GlobalEndpointManager.cs` — region endpoint management
- `Microsoft.Azure.Cosmos/src/Routing/GlobalPartitionEndpointManager.cs` — per-partition failover (PPAF)
- `Microsoft.Azure.Cosmos/src/Handler/RetryHandler.cs` — handler pipeline retry integration
