namespace Microsoft.Azure.Cosmos.Tests.Query.SampleQueryContainer
{
    using System;
    using System.Text;
    using Microsoft.Azure.Cosmos.Routing;

    internal class PhysicalPartitionKey : IComparable<PhysicalPartitionKey>
    {
        // Hash of individual partition key component does not preserve relative order.
        // In other words, although "Tenant0" < "Tenant1" (in ordering semantics), it's not guaranteed that Hash("Tenant0") < Hash("Tenant1").
        // However, hashes for full partition key are obetained by concatenating the hashes of individual components together.
        // As a result, while hash for a given value does not preserve order, hashes for same logical key prefix have same physical hash prefix.
        // This means for a given value of TenantId, say "Tenant0"; all possible values of logical partitions share physical hash prefix - HASH("Tenant0")
        public PhysicalPartitionKey(string tenantId, string userId, string sessionId)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(GetHash(tenantId));
            stringBuilder.Append(GetHash(sessionId));
            stringBuilder.Append(GetHash(userId));

            this.Hash = stringBuilder.ToString();
        }

        private static string GetHash(string value)
        {
            PartitionKeyHash physicalPartitionKeyHash = PartitionKeyHash.V2.Hash(value);
            return physicalPartitionKeyHash.Value;
        }

        public string Hash { get; }

        public int CompareTo(PhysicalPartitionKey other)
        {
            return StringComparer.Ordinal.Compare(this.Hash, other.Hash);
        }
    }
}
