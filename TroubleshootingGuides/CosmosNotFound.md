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

### 1. Race condition
    Cause: There is multiple SDK client instances and the read happened before the write.

    Fix:
    1. For session consistency the create item will return a session token that can be passed between SDK instances to guarantee that the read request is reading from a replica with that change.
    2. Change the [consistency level](https://docs.microsoft.com/azure/cosmos-db/consistency-levels-choosing) to a [stronger level](https://docs.microsoft.com/azure/cosmos-db/consistency-levels-tradeoffs)

### 2. Invalid Partition Key and ID combination
    Cause: The partition key and id combination are not valid.

    Fix: Fix the application logic that is causing the incorrect combination. 

### 3. TTL purge
    Cause: The item had the [Time To Live (TTL)](https://docs.microsoft.com/azure/cosmos-db/time-to-live) property set. The item was purged because the time to live had expired.

    Fix: Change the Time To Live to prevent the item from getting purged.

### 4. Lazy indexing
    Cause: The [lazy indexing](https://docs.microsoft.com/azure/cosmos-db/index-policy#indexing-mode) has not caught up.

    Fix: Wait for the indexing to catch up or change the indexing policy

### 5. Parent resource deleted
    Cause: The database and/or container that the item exists in has been deleted.

    Fix: [Restore](https://docs.microsoft.com/azure/cosmos-db/online-backup-and-restore#backup-retention-period) the parent resource or recreate the resources.