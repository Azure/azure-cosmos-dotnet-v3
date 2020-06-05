## CosmosNotFound

| Http Status Code | Name | Category |
|---|---|---|
|404|CosmosNotFound|Service|

## Description
This status code represents that the resource no longer exists. 

## Expected behavior
Normally there is no issue as this is by design and the application correctly handles this scenario. There are many valid scenarios where application expect an item to not exist.

## The document should or does exist, but got a 404 Not Found status code
Below are the possible reason for this behavior

### 1. Race condition

There are multiple SDK client instances and the read happened before the write.

#### Solution:
1. For session consistency the create item will return a session token that can be passed between SDK instances to guarantee that the read request is reading from a replica with that change.
2. Change the [consistency level](https://docs.microsoft.com/azure/cosmos-db/consistency-levels-choosing) to a [stronger level](https://docs.microsoft.com/azure/cosmos-db/consistency-levels-tradeoffs)

### 2. Invalid Partition Key and ID combination

The partition key and id combination are not valid.

#### Solution:
Fix the application logic that is causing the incorrect combination. 

### 3. TTL purge
The item had the [Time To Live (TTL)](https://docs.microsoft.com/azure/cosmos-db/time-to-live) property set. The item was purged because the time to live had expired.

#### Solution:
Change the Time To Live to prevent the item from getting purged.

### 4. Lazy indexing
The [lazy indexing](https://docs.microsoft.com/azure/cosmos-db/index-policy#indexing-mode) has not caught up.

#### Solution:
Wait for the indexing to catch up or change the indexing policy

### 5. Parent resource deleted
The database and/or container that the item exists in has been deleted.

#### Solution:
1. [Restore](https://docs.microsoft.com/azure/cosmos-db/online-backup-and-restore#backup-retention-period) the parent resource or recreate the resources.
2. Create a new resource to replace the deleted resource