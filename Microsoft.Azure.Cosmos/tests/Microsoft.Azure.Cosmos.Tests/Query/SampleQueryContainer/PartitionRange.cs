namespace Microsoft.Azure.Cosmos.Tests.Query.SampleQueryContainer
{
    internal class LogicalPartitionRange
    {
        public LogicalPartitionRange(LogicalPartitionKey min, LogicalPartitionKey max)
        {
            this.Min = min;
            this.Max = max;
        }

        public bool Contains(LogicalPartitionKey key)
        {
            bool contains =
                (
                    (this.Min == null) ||
                    (this.Min.PhysicalPartitionKey.CompareTo(key.PhysicalPartitionKey) <= 0)
                ) &&
                (
                    (this.Max == null) ||
                    (this.Max.PhysicalPartitionKey.CompareTo(key.PhysicalPartitionKey) > 0)
                );
            return contains;
        }

        public LogicalPartitionKey Min { get; }
        public LogicalPartitionKey Max { get; }
        public bool IsMinInclusive => true;
        public bool IsMaxInclusive => false;
    }
}
