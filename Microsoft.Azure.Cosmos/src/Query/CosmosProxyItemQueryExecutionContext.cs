//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// This class is used as a proxy to wrap the DefaultDocumentQueryExecutionContext which is needed 
    /// for sending the query to Gateway first and then uses PipelinedDocumentQueryExecutionContext after
    /// it gets the necessary info. This has been added since we
    /// haven't produced Linux/Mac version of the ServiceInterop native binary which holds the logic for
    /// parsing the query without having this extra hop to Gateway
    /// </summary>
    internal sealed class CosmosProxyItemQueryExecutionContext : CosmosQueryExecutionContext
    {
        private CosmosQueryExecutionContext innerExecutionContext;
        private CosmosQueryContext queryContext;

        private readonly CosmosContainerSettings containerSettings;

        public CosmosProxyItemQueryExecutionContext(
            CosmosQueryContext queryContext,
            CosmosContainerSettings containerSettings)
        {
            if (queryContext == null)
            {
                throw new ArgumentNullException(nameof(queryContext));
            }

            if (queryContext == null)
            {
                throw new ArgumentNullException(nameof(queryContext));
            }

            this.innerExecutionContext = new CosmosGatewayQueryExecutionContext(queryContext);
            this.queryContext = queryContext;
            this.containerSettings = containerSettings;
        }

        public override bool IsDone => this.innerExecutionContext.IsDone;

        public override void Dispose()
        {
            this.innerExecutionContext.Dispose();
        }

        public override async Task<CosmosQueryResponse> ExecuteNextAsync(CancellationToken token)
        {
            if (this.IsDone)
            {
                throw new InvalidOperationException(RMResources.DocumentQueryExecutionContextIsDone);
            }

            CosmosQueryResponse response = await this.innerExecutionContext.ExecuteNextAsync(token);

            // If the query failed because of cross partition query not servable then parse the query plan that is returned in the error
            // and create the correct context to execute it. For all other responses just return it since there is no query plan to parse.
            if (response.StatusCode != HttpStatusCode.BadRequest || response.Headers.SubStatusCode != SubStatusCodes.CrossPartitionQueryNotServable)
            {
                return response;
            }

            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo =
                    JsonConvert.DeserializeObject<PartitionedQueryExecutionInfo>(response.Error.AdditionalErrorInfo);

            string rewrittenQuery = partitionedQueryExecutionInfo.QueryInfo.RewrittenQuery;
            if (!string.IsNullOrEmpty(rewrittenQuery))
            {
                this.queryContext.SqlQuerySpec.QueryText = rewrittenQuery;
            }

            List<PartitionKeyRange> partitionKeyRanges =
                await this.queryContext.QueryClient.GetTargetPartitionKeyRanges(
                    this.queryContext.ResourceLink.OriginalString,
                    this.containerSettings.ResourceId,
                    partitionedQueryExecutionInfo.QueryRanges);

            this.innerExecutionContext = await CosmosQueryExecutionContextFactory.CreateSpecializedDocumentQueryExecutionContext(
                this.queryContext,
                partitionedQueryExecutionInfo,
                partitionKeyRanges,
                this.containerSettings.ResourceId,
                token);

            return await this.innerExecutionContext.ExecuteNextAsync(token);
        }
    }
}
