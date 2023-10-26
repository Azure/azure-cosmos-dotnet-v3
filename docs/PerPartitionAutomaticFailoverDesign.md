# Working Principles for Per Partition Automatic Failover

## Table of Contents

* [Scope.](#scope)
* [Background.](#background)
* [How Does the SDK know Which Region to Fail Over.](#how-does-the-sdk-know-which-region-to-fail-over)
* [References.](#references)

## Scope

The scope of the per partition automatic failover design document is applicable for the Cosmos .NET SDK configured only for `Direct` mode at the moment.

## Background

Today, the partition level failovers are applicable for multi master write acounts, for a simple reason. If one of the write region fails with a write forbidden 403 exception, then the SDK has the knowledge (by looking up the `ApplicationPreferredRegions`) of the other regions to failover. With the per partition automatic failover, if a partition is in quorum loss, then the backend automatically marks another region as the write region, based on the account configuration. Therefore, any retry for the write requests, to the next preferred region should be successful.

This idea extends the SDK's retry logic to retry the write requests for single master write accounts, for any service unavailable (status codes 503) errors.

## How Does the SDK know Which Region to Fail Over

Right now, the .NET SDK depends upon the `GlobalPartitionEndpointManagerCore` to resolve the endpoints. There is a method `TryMarkEndpointUnavailableForPartitionKeyRange()` within the class, that is responsible to add the override by iterating over the next read regions. This is how the .NET SDK knows which region to fail over.

## References

- [SDK not retrying with next region in case address resolution call to Gateway call fails with 503.](https://msdata.visualstudio.com/CosmosDB/_workitems/edit/2475521/)
- [First client write request after failover is failing with 503(21005)](https://msdata.visualstudio.com/CosmosDB/_workitems/edit/2492475/)
- [PPAF Testing in Test, Staging and Prod.](https://microsoft.sharepoint.com/:w:/r/teams/DocumentDB/_layouts/15/doc2.aspx?sourcedoc=%7B7587D267-212F-47BE-AAD6-18FC53482B68%7D&file=PPAF%20Testing%20in%20Test%2C%20Staging%20and%20Prod.docx&action=default&mobileredirect=true)