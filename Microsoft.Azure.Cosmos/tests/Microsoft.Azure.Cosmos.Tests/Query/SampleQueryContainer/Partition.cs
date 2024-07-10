namespace Microsoft.Azure.Cosmos.Tests.Query.SampleQueryContainer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    internal class Partition
    {
        public Partition(IEnumerable<SampleDocument> documents, LogicalPartitionRange partitionRange)
        {
            Debug.Assert(
                documents.All(d => partitionRange.Contains(d.LogicalPartitionKey)),
                "Partition Assert!",
                "Document partition hash must be >= min");
            this.Documents = documents.OrderBy(d => d.LogicalPartitionKey.PhysicalPartitionKey.Hash, StringComparer.Ordinal).ToList();
            this.LogicalPartitionRange = partitionRange;
        }

        public IEnumerable<SampleDocument> Documents { get; }
        public LogicalPartitionRange LogicalPartitionRange { get; }
    }
}
