## Cosmos DB .Net SDK – Timeout configurations and Retry configurations

### Timeout config - Gateway

There are three retries allowed 

| OperationType      | Network Request Timeout | Connection Timeout |
| -----------------  |-------------------------|------------------- |
| AddressRefresh     | .5s, 5s, 10s            | 65s                |
| Database Account   | 5s, 10s, 20s            | 65s                |
| ControlPlaneRead   | 5s, 10s, 20s            | 65s                |
| ControlPlaneHotPath| .5s, 5s, 65s            | 65s                |
| Other Http calls   | 65s, 65s, 65s           | 65s                |


### Timeout config - Direct
| OperationType      | Network Request Timeout | Connection Timeout |
| -----------------  |:----------------------- |:------------------ |
| All Tcp calls      | 6s                      | 6s                 |


### Retry config
`Note: the following config only tracks what would happen within a region.`

| StatusCode      | SubStatusCode | FirstRetryWithDelay | InitialBackoff               | MaxBackoff  | BackoffStrategy  | MaxRetryAttempts   | MaxRetryTimeout                         | Other notes                                   |
|-----------------| ---------------|--------------------| ---------------------------- | ----------- | ---------------- | ------------------ | --------------------------------------- | --------------------------------------------- |
| 410             | 0              | NO                 | 1s                           | 15s         | Exponential      | N/A                | 60s - Strong/Bounded, 30s - Others      |                                               |
| 410             | 1007           | NO                 | 1s                           | 15s         | Exponential      | N/A                | 60s - Strong/Bounded, 30s - Others      |                                               |
| 410             | 1008           | NO                 | 1s                           | 15s         | Exponential      | N/A                | 60s - Strong/Bounded, 30s - Others      |                                               |
| 449             | 0              | YES                | 10ms + random salt [0, 5)    | 1s          | Exponential      | N/A                | 60s - Strong/Bounded, 30s - Others      |                                               |
| 429             | *              | `x-ms-retry-after` | `x-ms-retry-after`           | 5s          | N/A              | 9 (by default)     | 30s (by default)                        |                                               |
| 404             | 1002           | NO                 | 5ms                          | 50ms        | Exponential      | N/A                | 5s                                      |                                               |
| 410             | 1000           | NO                 | N/A                          | N/A         | N/A              | 1                  | N/A                                     |                                               |
| 410             | 1002           | NO                 | N/A                          | N/A         | N/A              | 1                  | N/A                                     | Only applies to `Query`, `ChangeFeed`         |
| 400             | 1001           | NO                 | N/A                          | N/A         | N/A              | 1                  | N/A                                     |                                               |

### Per-Partition Automatic Failover (PPAF) and Thin Client defaults
 
With PPAF enabled, the SDK will also enable threshold-based availability strategy for item-based point-read and non-point-read operations with defaults as below. Three retries are allowed and respective timeouts are given here.

| OperationType      | Network Request Timeout | Connection Timeout |
| -----------------  |-------------------------|------------------- |
| Point-Read         | 6s, 6s, 10s             | 65s                |
| Non-Point-Read     | 6s, 6s, 10s             | 65s                |

### DocumentClient 
Request timeout : 6s
Change Feed Lease Expiration Interval = 60s
