//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.CosmosQueryExecutionContextFactory;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    internal sealed class QueryOptimizedIterator : FeedIteratorInternal
    {
        private readonly ContainerInternal container;
        private readonly QueryDefinition queryDefinition;
        private readonly QueryRequestOptions queryRequestOptions;
        private readonly CosmosClientContext clientContext;

        private bool hasMoreResults = true;
        private string continuationToken = null;
        private PartitionedQueryExecutionInfo partitionedQueryExecutionInfo;

        public QueryOptimizedIterator(
            ContainerInternal container,
            QueryDefinition queryDefinition,
            QueryRequestOptions requestOptions,
            CosmosClientContext clientContext)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.queryDefinition = queryDefinition;
            this.queryRequestOptions = requestOptions;
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
        }

        public override bool HasMoreResults => this.hasMoreResults;

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotImplementedException();
        }

        public override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            return this.ReadNextAsync(NoOpTrace.Singleton, cancellationToken);
        }

        public override async Task<ResponseMessage> ReadNextAsync(ITrace trace, CancellationToken cancellationToken)
        {
            ResponseMessage responseMessage = await this.ExecuteQueryAsync(
                !this.queryRequestOptions.ForceAntlrQueryPlan && !this.queryRequestOptions.ForceGatewayQueryPlan,
                trace, 
                cancellationToken);

            if (this.partitionedQueryExecutionInfo == null &&
                this.hasMoreResults)
            {
                if (this.queryRequestOptions.ForceAntlrQueryPlan)
                {
                    bool parsed;
                    SqlQuery sqlQuery;
                    using (trace.StartChild("Antlr Parser"))
                    {
                        parsed = SqlQueryParser.TryParse(this.queryDefinition.QueryText, out sqlQuery);
                    }

                    if (parsed)
                    {
                        bool hasDistinct = sqlQuery.SelectClause.HasDistinct;
                        bool hasGroupBy = sqlQuery.GroupByClause != default;
                        bool hasAggregates = AggregateProjectionDetector.HasAggregate(sqlQuery.SelectClause.SelectSpec);
                        bool createPassthroughQuery = !hasAggregates && !hasDistinct && !hasGroupBy;

                        if (createPassthroughQuery)
                        {
                            // Only thing that matters is that we target the correct range.
                            ContainerProperties containerProperties = await this.container.GetCachedContainerPropertiesAsync(false, trace, cancellationToken);
                            this.partitionedQueryExecutionInfo = new PartitionedQueryExecutionInfo();
                        }
                    }
                }
                else if (this.queryRequestOptions.ForceGatewayQueryPlan)
                {
                    using (ResponseMessage message = await this.clientContext.ProcessResourceOperationStreamAsync(
                        resourceUri: this.container.LinkUri,
                        resourceType: Documents.ResourceType.Document,
                        operationType: Documents.OperationType.Query,
                        requestOptions: null,
                        feedRange: new FeedRangePartitionKey(this.queryRequestOptions.PartitionKey.Value),
                        cosmosContainerCore: this.container,
                        streamPayload: this.clientContext.SerializerCore.ToStreamSqlQuerySpec(this.queryDefinition.ToSqlQuerySpec(), Documents.ResourceType.Document),
                        requestEnricher: (requestMessage) =>
                        {
                            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ContentType, RuntimeConstants.MediaTypes.QueryJson);
                            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsQueryPlanRequest, bool.TrueString);
                            requestMessage.Headers.Add(HttpConstants.HttpHeaders.SupportedQueryFeatures, QueryPlanRetriever.SupportedQueryFeaturesString);
                            requestMessage.Headers.Add(HttpConstants.HttpHeaders.QueryVersion, new Version(major: 1, minor: 0).ToString());
                            requestMessage.UseGatewayMode = true;
                        },
                        trace: trace,
                        cancellationToken: cancellationToken))
                    {
                        // Syntax exception are argument exceptions and thrown to the user.
                        message.EnsureSuccessStatusCode();
                        this.partitionedQueryExecutionInfo = this.clientContext.SerializerCore.FromStream<PartitionedQueryExecutionInfo>(message.Content);
                    }
                }
                else
                {
                    using (trace.StartChild("BackendHeaders Headers"))
                    {
                        string queryPlanString = responseMessage.Headers.Get(WFConstants.BackendHeaders.IndexUtilization);
                        PartitionedQueryExecutionInfoInternal queryExecutionInfoInternal = JsonConvert.DeserializeObject<PartitionedQueryExecutionInfoInternal>(
                           queryPlanString,
                           new JsonSerializerSettings
                           {
                               DateParseHandling = DateParseHandling.None
                           });

                        ContainerProperties containerProperties = await this.container.GetCachedContainerPropertiesAsync(false, trace, cancellationToken);
                        this.partitionedQueryExecutionInfo = QueryPartitionProvider.ConvertPartitionedQueryExecutionInfo(queryExecutionInfoInternal, containerProperties.PartitionKey);
                    }
                }
            }

            return responseMessage;
        }

        private async Task<ResponseMessage> ExecuteQueryAsync(
            bool includeQueryPlan,
            ITrace trace, 
            CancellationToken cancellationToken)
        {
            ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
                             this.container.LinkUri,
                             resourceType: Documents.ResourceType.Document,
                             operationType: Documents.OperationType.Query,
                             this.queryRequestOptions,
                             this.container,
                             feedRange: new FeedRangePartitionKey(this.queryRequestOptions.PartitionKey.Value),
                             streamPayload: this.clientContext.SerializerCore.ToStreamSqlQuerySpec(this.queryDefinition.ToSqlQuerySpec(), Documents.ResourceType.Document),
                             requestEnricher: (cosmosRequestMessage) =>
                             {
                                 QueryRequestOptions.FillContinuationToken(
                                    cosmosRequestMessage,
                                    this.continuationToken);

                                 cosmosRequestMessage.Headers.Add(HttpConstants.HttpHeaders.ContentType, MediaTypes.QueryJson);
                                 cosmosRequestMessage.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);

                                 if (includeQueryPlan)
                                 {
                                     cosmosRequestMessage.Headers.Set(HttpConstants.HttpHeaders.A_IM, bool.TrueString);
                                 }
                             },
                             trace: trace,
                             cancellationToken: cancellationToken);

            this.continuationToken = responseMessage.Headers.ContinuationToken;
            this.hasMoreResults = this.continuationToken != null;

            return responseMessage;
        }
    }
}