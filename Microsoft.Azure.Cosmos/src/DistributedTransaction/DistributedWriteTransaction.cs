// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;

#if INTERNAL
    public 
#else
    internal
#endif
    abstract class DistributedWriteTransaction : DistributedTransaction
    {
        public abstract DistributedWriteTransaction CreateItem<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            T resource);

        public abstract DistributedWriteTransaction ReplaceItem<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            T resource);

        public abstract DistributedWriteTransaction DeleteItem(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id);

        public abstract DistributedWriteTransaction PatchItem(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            IReadOnlyList<PatchOperation> patchOperations);

        public abstract DistributedWriteTransaction UpsertItem<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            T resource);
    }
}
