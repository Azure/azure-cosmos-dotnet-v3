namespace Microsoft.Azure.Cosmos.Tests.Query.SampleQueryContainer
{
    internal class SampleDocument
    {
        public SampleDocument(string tenantId, string userId, string sessionId, string id)
        {
            this.Id = id;
            this.LogicalPartitionKey = new LogicalPartitionKey(tenantId, userId, sessionId);
        }

        // TenantId, UserId, SessionId together are partition keys
        public string TenantId => this.LogicalPartitionKey.TenantId;
        public string UserId => this.LogicalPartitionKey.UserId;
        public string SessionId => this.LogicalPartitionKey.SessionId;
        public string Id { get; }
        public LogicalPartitionKey LogicalPartitionKey { get; }
    }
}
