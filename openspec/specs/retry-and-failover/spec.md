# Retry and Failover

## Purpose

The SDK's retry and failover layer handles transient errors, throttling (429), service unavailability (503), and cross-region failover to maintain high availability and resilience.

## Requirements

### Requirement: Throttling Retry (429)
The SDK SHALL automatically retry requests that receive a 429 (Too Many Requests) response with exponential backoff.

#### Retry on throttle
**When** a request receives a 429 response, the SDK shall retry the request after waiting for the duration specified in the `x-ms-retry-after-ms` header, continuing retries up to `CosmosClientOptions.MaxRetryAttemptsOnRateLimitedRequests` times (default: 9).

#### Max retry time exceeded
**If** the cumulative wait time for throttling retries exceeds `CosmosClientOptions.MaxRetryWaitTimeOnRateLimitedRequests` (default: 30 seconds), **then** the SDK shall stop retrying and return the 429 response to the caller.

#### Custom retry configuration
**Where** `CosmosClientOptions.MaxRetryAttemptsOnRateLimitedRequests = 3` and `MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(10)`, **when** a request is continuously throttled, the SDK shall attempt at most 3 retries within a 10-second window.

### Requirement: Region Failover on Endpoint Unavailability
The SDK SHALL fail over to the next preferred region when an endpoint becomes unavailable.

#### Service unavailable (503)
**When** a request receives a 503 (Service Unavailable) response, the SDK shall mark the current endpoint as unavailable, retry the request on the next region in `ApplicationPreferredRegions`, and attempt at most 1 service-unavailable retry per region.

#### Write forbidden (403/3)
**When** a write request receives a 403 with sub-status code 3 (WriteForbidden), the SDK shall mark the current write endpoint as unavailable and retry the request on the next preferred write region.

#### Request timeout (408)
**When** a request receives a 408 (Request Timeout), the SDK shall mark the partition key range as unavailable in the current region and retry the request on the next preferred region.

#### HTTP request exception
**When** a request fails with an `HttpRequestException` (network-level failure), the SDK shall mark the partition as unavailable and retry the request on the next preferred region.

#### Maximum failover retries
**If** the failover retry count reaches 120, **then** the SDK shall stop retrying and return the error to the caller.

### Requirement: Gone (410) Handling
The SDK SHALL handle 410 (Gone) responses by refreshing routing information.

#### Partition key range gone
**When** a request receives a 410 with sub-status 1002 (PartitionKeyRangeGone), the SDK shall invalidate the partition key range cache and retry the request with refreshed routing information.

#### Name cache stale
**When** a request receives a 410 with sub-status indicating a stale cache, the SDK shall refresh the collection cache and retry the request.

### Requirement: Per-Partition Automatic Failover (PPAF)
The SDK SHALL support per-partition-level failover for Direct mode connections.

#### Partition-level 503 in single-master
**While** the account is single-master with `ApplicationPreferredRegions` configured, **when** a write request to a specific partition receives a 503, the SDK shall mark that specific partition as unavailable in the current region and retry targeting the next region in `ApplicationPreferredRegions` for that partition only.

#### System resource unavailable (429/3092)
**When** a request receives a 429 with sub-status code 3092 (SystemResourceUnavailable), the SDK shall treat this as a 503-equivalent and trigger partition-level failover to the next region.

#### PPAF does not affect other partitions
**While** partition A is marked unavailable in Region 1, **when** a request for partition B is made, the SDK shall route partition B's request to Region 1 normally and route only partition A's requests to Region 2.

### Requirement: Retry Wait Time
The SDK SHALL wait an appropriate interval between failover retries.

#### Failover retry delay
**When** a region failover is triggered, the SDK shall wait 1000 milliseconds before sending the retry to the next region.

### Requirement: Retry Policy Independence
The SDK SHALL ensure each retry scope operates independently.

#### Independent retry chains
**While** a request is being retried due to region failover, **when** the retry request encounters a new transient error, the SDK shall evaluate the new error independently by the retry policy and preserve the original retry context.

### Requirement: Excluded Regions
The SDK SHALL support excluding specific regions from request routing.

#### Exclude region via RequestOptions
**Where** `RequestOptions.ExcludeRegions` contains "West US 2", **when** a request is routed, the SDK shall not consider "West US 2" for that request and use the next available region in the preferred list.

## Key Source Files
- `Microsoft.Azure.Cosmos/src/ClientRetryPolicy.cs` — main retry orchestrator
- `Microsoft.Azure.Cosmos/src/RetryPolicy.cs` — factory for ClientRetryPolicy
- `Microsoft.Azure.Cosmos/src/ResourceThrottleRetryPolicy.cs` — 429 throttle retry
- `Microsoft.Azure.Cosmos/src/Routing/GlobalEndpointManager.cs` — region endpoint management
- `Microsoft.Azure.Cosmos/src/Routing/GlobalPartitionEndpointManager.cs` — per-partition failover (PPAF)
- `Microsoft.Azure.Cosmos/src/Handler/RetryHandler.cs` — handler pipeline retry integration
