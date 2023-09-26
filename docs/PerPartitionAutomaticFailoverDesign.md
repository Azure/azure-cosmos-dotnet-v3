# Working Principles for Per Partition Automatic Failover

## Table of Contents

* [Scope.](#scope)
* [Backgraound.](#backgraound)
* [Design Approach.](#design-approach)
* [How Does the SDK know Which Region to Fail Over.](#how-does-the-sdk-know-which-region-to-fail-over)
* [References.](#references)

## Scope

The scope of the per partition automatic failover design document is applicable for the Cosmos .NET SDK configured for both `Gateway` and `Direct` mode.

## Backgraund

Today, the partition level failovers are applicable for multi master write acounts, for a simple reason. If one of the write region fails with a write forbidden 403 exception, then the SDK has the knowledge (by looking up the `ApplicationPreferredRegions`) of the other regions to failover. With the per partition automatic failover, if a partition is in quorum loss, then the backend automatically marks another region as the write region, based on the account configuration. Therefore, any retry for the write requests, to the next preferred region should be successful.

This idea extends the SDK's retry logic to retry the write requests for single master write accounts, for any service unavailable (status codes 503) errors.

## Design Approach

Today, the partition level failover is applicable only for the multi-master write accounts. In order to enable the partition level failover for single master write accounts, below changes are required to be made:

- In the `ClientRetryPolicy.ShouldRetryOnServiceUnavailable()`, enable the retry for Single Master write accounts. This is done by removing the below piece of code:

    ```
                if (!this.canUseMultipleWriteLocations
    && !this.isReadRequest)
                {
                    // Write requests on single master cannot be retried, no other regions available.
                    return ShouldRetryResult.NoRetry();
                }
    ```


- Today, when a call to get the collection for a specific region fails in the Gateway, the Gateway returns a `Service Unavailable - 503` Status, with a Sub Status code `9002`. Per the current behavior, our .NET SDK doesn't retry for `503.9002`, and it only does so for `503.Unknown` code. Therefore the SDK was not retrying initially. In order to resolve this, delete the `ClientRetryPolicy.IsRetriableServiceUnavailable(SubStatusCodes? subStatusCode)` method completely and with this in place, the SDK should retry on any service unavailable by default and it will not depend upon the sub-status codes to make the retry decision.

- Currently, there is an option `EnablePartitionLevelFailover` in the `CosmosClientOptions` to enable or disable the per partition automatic failover. However this option is not `public` yet. The approach we would like to take is to develop this feature behind a feature flag called `AZURE_COSMOS_PARTITION_LEVEL_FAILOVER_ENABLED`. By setting this feature flag, the external customers can enable of disable the partition level failover.

- For the customers to enable the partition level failover, we have agreed to make the `ApplicationPreferredRegions` as a mandatory parameter the `CosmosClientOptions`. Therefore, if the partition level failover is enabled, and the `ApplicationPreferredRegions` list is not provided, an `ArgumentException` will be thrown. This will be a change in the behavior.

## How Does the SDK know Which Region to Fail Over

Right now, the .NET SDK depends upon the `GlobalPartitionEndpointManagerCore` to resolve the endpoints. There is a method `TryMarkEndpointUnavailableForPartitionKeyRange()` within the class, that is responsible to add the override by iterating over the next read regions. This is how the .NET SDK knows which region to fail over.

## References

- [SDK not retrying with next region in case address resolution call to Gateway call fails with 503.](https://msdata.visualstudio.com/CosmosDB/_workitems/edit/2475521/)
- [First client write request after failover is failing with 503(21005)](https://msdata.visualstudio.com/CosmosDB/_workitems/edit/2492475/)
- [PPAF Testing in Test, Staging and Prod.](https://microsoft.sharepoint.com/:w:/r/teams/DocumentDB/_layouts/15/doc2.aspx?sourcedoc=%7B7587D267-212F-47BE-AAD6-18FC53482B68%7D&file=PPAF%20Testing%20in%20Test%2C%20Staging%20and%20Prod.docx&action=default&mobileredirect=true)