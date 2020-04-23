## CosmosNotFound

|   |   |   |
|---|---|---|
|TypeName|CosmosNotFound|
|Status|404_0000|
|Category|Service|

## Description

This status code represents that the resource no longer exists. 

## Known issues

The document does exists, but still returns a 404. 

### Cause 1: Race condition 
There is multiple SDK client instances and the read happened before the write.

### Solution
1. For session consistency the create item will return a session token that can be passed between SDK instances to guarantee that the read request is reading from a replica with that change.
2. Change the consistency level to a stronger level

### Related documentation
* [Consistency levels](https://docs.microsoft.com/azure/cosmos-db/consistency-levels)
* [Choose the right consistency level](https://docs.microsoft.com/azure/cosmos-db/consistency-levels-choosing)
* [Consistency, availability, and performance tradeoffs](https://docs.microsoft.com/azure/cosmos-db/consistency-levels-tradeoffs)