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

        public static async Task<PartitionedQueryExecutionInfo> GetQueryPlanWithServiceInteropAsync(
            CosmosQueryContext cosmosQueryContext,
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            QueryPartitionProvider queryPartitionProvider = await cosmosQueryContext.QueryClient.GetQueryPartitionProviderAsync(cancellationToken);
            QueryPlanHandler queryPlanHandler = new QueryPlanHandler(queryPartitionProvider);

            return queryPlanHandler.GetQueryPlan(
                    sqlQuerySpec,
                    partitionKeyDefinition,
                    SupportedQueryFeatures);
        }

        public static async Task<PartitionedQueryExecutionInfo> GetQueryPlanThroughGatewayAsync(
            DocumentClient documentClient,
            SqlQuerySpec sqlQuerySpec,
            Uri resourceLink,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DocumentServiceRequest request = CreateRequest(
                sqlQuerySpec,
                resourceLink);

            DocumentServiceResponse documentServiceResponse = await documentClient.ExecuteQueryAsync(
                request: request,
                retryPolicy: null,
                cancellationToken: cancellationToken);

            MemoryStream body = new MemoryStream();
            await documentServiceResponse.ResponseBody.CopyToAsync(body);
            string unencodedBody = Encoding.UTF8.GetString(body.GetBuffer());
            PartitionedQueryExecutionInfo partitionedQueryInfo = JsonConvert.DeserializeObject<PartitionedQueryExecutionInfo>(unencodedBody);

            return partitionedQueryInfo;
        }

        private static DocumentServiceRequest CreateRequest(
            SqlQuerySpec sqlQuerySpec,
            Uri resourceLink)
        {
            StringKeyValueCollection headers = new StringKeyValueCollection()
            {
                { HttpConstants.HttpHeaders.ContentType, RuntimeConstants.MediaTypes.QueryJson },
                { HttpConstants.HttpHeaders.IsQueryPlanRequest, bool.TrueString },
                { HttpConstants.HttpHeaders.SupportedQueryFeatures, SupportedQueryFeaturesString },
                { HttpConstants.HttpHeaders.QueryVersion, new Version(major: 1, minor: 0).ToString() }
            };

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.QueryPlan,
                ResourceType.Document,
                resourceLink.OriginalString,
                AuthorizationTokenType.PrimaryMasterKey,
                headers);

            string queryText = JsonConvert.SerializeObject(sqlQuerySpec);

            request.Body = new MemoryStream(Encoding.UTF8.GetBytes(queryText));
            request.UseGatewayMode = true;
            return request;
        }
    }
}