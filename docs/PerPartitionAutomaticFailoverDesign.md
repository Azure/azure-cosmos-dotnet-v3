# Working Principles for Per Partition Automatic Failover

## Table of Contents

* [Scope.](#scope)
* [Backgraound.](#backgraound)
* [Proposed Solution.](#proposed-solution)
* [Design Approach.](#design-approach)
    * [Outline.](#outline)
    * [Updated Sequence Diagram for `CosmosClient` initialization.](#updated-sequence-diagram-for-cosmosclient-initialization)
    * [Sequence Diagram when `StoreReader` invokes the `GatewayAddressCache` to resolve addresses and leverages `AddressEnumerator` to enumerate the transport addresses.](#sequence-diagram-when-storereader-invokes-the-gatewayaddresscache-to-resolve-addresses-and-leverages-addressenumerator-to-enumerate-the-transport-addresses)
    * [State Diagram to Understand the `TransportAddressUri` Health State Transformations.](#state-diagram-to-understand-the-transportaddressuri-health-state-transformations)
    * [`Microsoft.Azure.Cosmos.Direct` package class diagrams.](#azurecosmosdirect-package-class-diagrams)
    * [`Microsoft.Azure.Cosmos` package class diagrams.](#microsoftazurecosmos-package-class-diagrams)
* [Pull Request with Sample Code Changes.](#pull-request-with-sample-code-changes)
* [References.](#references)

## Scope

The scope of the per partition automatic failover is applicable for the `CosmosClient` configured for both `Gateway` and `Direct` mode.

## Backgraund

During an upgrade scenario in the backend replica nodes, there has been an observation of increased request latency. One of the primary reason for the latency is that, during an upgrade, a replica which is still undergoing upgrade may still be returned back to SDK, when an address refresh occurres. As of today, the incoming request will have `25%` chance to hit the replica that not ready yet, therefore causing the `ConnectionTimeoutException`, which contributes to the increased latency.

To understand the problem statement better, please take a look at the below sequence diagram which reflects the connection timeouts caused by the replica upgrade.

## Design Approach

Today, the partition level failover is applicable for multi-master write accounts. In order to enable the partition level failover for single master write accounts, below changes are required to be made:

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

## How Does the SDK knows Which Region to Fail Over

Right now, the .NET SDK depends upon the `GlobalPartitionEndpointManagerCore` to resolve the endpoints. There is a method `TryMarkEndpointUnavailableForPartitionKeyRange()` within the class, that is responsible to add the override by iterating over the next read regions. This is how the .NET SDK knows which region to fail over.