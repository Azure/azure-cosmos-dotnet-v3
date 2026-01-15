// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal abstract class DistributedWriteTransaction : DistributedTransaction
    {
        public abstract DistributedTransaction Create<T>(
            string database,
            string collection,
            PartitionKey partitionKey);

        public abstract DistributedTransaction Replace<T>(
            string database,
            string collection,
            PartitionKey partitionKey);

        public abstract DistributedTransaction Delete(
            string database,
            string collection,
            PartitionKey partitionKey);
        public abstract DistributedTransaction Patch(
            string database,
            string collection,
            PartitionKey partitionKey);
        public abstract DistributedTransaction Upsert(
            string database,
            string collection,
            PartitionKey partitionKey);
    }
}
