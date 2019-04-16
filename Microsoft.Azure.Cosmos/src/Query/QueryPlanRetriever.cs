namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;

    internal static class QueryPlanRetriever
    {
        private static readonly QueryFeatures SupportedQueryFeatures =
            QueryFeatures.Aggregate
            | QueryFeatures.Distinct
            | QueryFeatures.MultipleOrderBy
            | QueryFeatures.OffsetAndLimit
            | QueryFeatures.OrderBy
            | QueryFeatures.Top;

        private static readonly string SupportedQueryFeaturesString = SupportedQueryFeatures.ToString();

        // Remove once direct package is updated.
        private const OperationType QueryPlanOperationType = (OperationType)41;

        // Remove once direct package is updated.
        private static class Headers
        {
            public const string IsQueryPlanRequest = "x-ms-cosmos-is-query-plan-request";
            public const string SupportedQueryFeatures = "x-ms-cosmos-supported-query-features";
            public const string QueryVersion = "x-ms-cosmos-query-version";
        }

        public static async Task<PartitionedQueryExecutionInfo> GetQueryPlanWithServiceInteropAsync(
            CosmosQueryContext cosmosQueryContext,
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            QueryPartitionProvider queryPartitionProvider = await cosmosQueryContext.QueryClient.GetQueryPartitionProviderAsync(cancellationToken);
            ServiceInteropQueryPlanHandler serviceInteropQueryPlanHandler = new ServiceInteropQueryPlanHandler(
                queryPartitionProvider,
                SupportedQueryFeatures);

            return serviceInteropQueryPlanHandler.GetPlanForQuery(
                sqlQuerySpec,
                partitionKeyDefinition);
        }

        public static async Task<PartitionedQueryExecutionInfo> GetQueryPlanThroughGatewayAsync(
            DocumentClient documentClient,
            SqlQuerySpec sqlQuerySpec,
            string resourceId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DocumentServiceRequest request = CreateRequest(
                sqlQuerySpec,
                resourceId);

            DocumentServiceResponse documentServiceResponse = await documentClient.ExecuteQueryAsync(
                request, 
                cancellationToken);

            MemoryStream body = new MemoryStream();
            await documentServiceResponse.ResponseBody.CopyToAsync(body);
            string unencodedBody = Encoding.UTF8.GetString(body.GetBuffer());
            PartitionedQueryExecutionInfo partitionedQueryInfo = JsonConvert.DeserializeObject<PartitionedQueryExecutionInfo>(unencodedBody);

            return partitionedQueryInfo;
        }

        private static DocumentServiceRequest CreateRequest(
            SqlQuerySpec sqlQuerySpec,
            string resourceId)
        {
            StringKeyValueCollection headers = new StringKeyValueCollection()
            {
                { HttpConstants.HttpHeaders.ContentType, RuntimeConstants.MediaTypes.QueryJson },
                { Headers.IsQueryPlanRequest, bool.TrueString },
                { Headers.SupportedQueryFeatures, SupportedQueryFeaturesString },
                { Headers.QueryVersion, new Version(major: 1, minor: 0).ToString() }
            };

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                QueryPlanOperationType,
                resourceId,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey,
                headers);

            string queryText = JsonConvert.SerializeObject(sqlQuerySpec);

            request.Body = new MemoryStream(Encoding.UTF8.GetBytes(queryText));
            request.UseGatewayMode = true;
            return request;
        }
    }
}
