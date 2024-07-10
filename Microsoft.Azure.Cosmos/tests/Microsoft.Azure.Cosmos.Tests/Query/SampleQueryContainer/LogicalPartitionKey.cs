namespace Microsoft.Azure.Cosmos.Tests.Query.SampleQueryContainer
{
    internal class LogicalPartitionKey
    {
        public LogicalPartitionKey(string tenantId, string userId, string sessionId)
        {
            this.TenantId = tenantId;
            this.UserId = userId;
            this.SessionId = sessionId;
            this.PhysicalPartitionKey = new PhysicalPartitionKey(tenantId, userId, sessionId);
        }

        public string TenantId { get; }
        public string UserId { get; }
        public string SessionId { get; }
        public PhysicalPartitionKey PhysicalPartitionKey { get; }
    }
}
