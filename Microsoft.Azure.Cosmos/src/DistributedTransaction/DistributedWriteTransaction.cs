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
        public abstract DistributedWriteTransaction Create<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            T resource);

        public abstract DistributedWriteTransaction Replace<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            T resource);

        public abstract DistributedWriteTransaction Delete(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id);

        public abstract DistributedWriteTransaction Patch(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            IReadOnlyList<PatchOperation> patchOperations);

        public abstract DistributedWriteTransaction Upsert<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            T resource);
    }
}
