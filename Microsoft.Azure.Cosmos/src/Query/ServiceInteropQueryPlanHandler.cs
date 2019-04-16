namespace Microsoft.Azure.Cosmos.Query
{
    using Microsoft.Azure.Documents;

    internal sealed class ServiceInteropQueryPlanHandler
    {
        private readonly QueryPlanHandler queryPlanHandler;
        private readonly QueryFeatures supportedQueryFeatures;

        public ServiceInteropQueryPlanHandler(
            QueryPartitionProvider queryPartitionProvider,
            QueryFeatures supportedQueryFeatures)
        {
            this.queryPlanHandler = new QueryPlanHandler(queryPartitionProvider);
            this.supportedQueryFeatures = supportedQueryFeatures;
        }

        public PartitionedQueryExecutionInfo GetPlanForQuery(
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            return this.queryPlanHandler.GetQueryPlan(
                sqlQuerySpec,
                partitionKeyDefinition,
                supportedQueryFeatures);
        }
    }
}
