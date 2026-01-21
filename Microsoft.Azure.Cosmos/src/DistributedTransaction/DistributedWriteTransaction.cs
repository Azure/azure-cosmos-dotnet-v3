// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;

    internal abstract class DistributedWriteTransaction : DistributedTransaction
    {
        public abstract DistributedTransaction Create<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            T resource);

        public abstract DistributedTransaction Replace<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            T resource);

        public abstract DistributedTransaction Delete(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id);

        public abstract DistributedTransaction Patch(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            IReadOnlyList<PatchOperation> patchOperations);

        public abstract DistributedTransaction Upsert<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            T resource);
    }
}
