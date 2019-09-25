//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal class CosmosQueryContextCore : CosmosQueryContext
    {
        private readonly QueryRequestOptions queryRequestOptions;

        public CosmosQueryContextCore(
            CosmosQueryClient client,
            QueryRequestOptions queryRequestOptions,
            ResourceType resourceTypeEnum,
            OperationType operationType,
            Type resourceType,
            Uri resourceLink,
            Guid correlatedActivityId,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            string containerResourceId = null)
            : base(
                client,
                resourceTypeEnum,
                operationType,
                resourceType,
                resourceLink,
                correlatedActivityId,
                isContinuationExpected,
                allowNonValueAggregateQuery,
                containerResourceId)
        {
            this.queryRequestOptions = queryRequestOptions;
        }

        internal override Task<QueryResponseCore> ExecuteQueryAsync(
            SqlQuerySpec querySpecForInit,
            string continuationToken,
            PartitionKeyRangeIdentity partitionKeyRange,
            bool isContinuationExpected,
            int pageSize,
            CancellationToken cancellationToken)
        {
            QueryRequestOptions requestOptions = null;
            if (this.queryRequestOptions != null)
            {
                requestOptions = this.queryRequestOptions.Clone();
            }    

            return this.QueryClient.ExecuteItemQueryAsync(
                           resourceUri: this.ResourceLink,
                           resourceType: this.ResourceTypeEnum,
                           operationType: this.OperationTypeEnum,
                           requestOptions: requestOptions,
                           sqlQuerySpec: querySpecForInit,
                           continuationToken: continuationToken,
                           partitionKeyRange: partitionKeyRange,
                           isContinuationExpected: isContinuationExpected,
                           pageSize: pageSize,
                           cancellationToken: cancellationToken);
        }
    }
}
