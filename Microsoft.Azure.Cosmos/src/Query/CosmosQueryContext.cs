//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    internal class CosmosQueryContext
    {
        public virtual CosmosQueryClient QueryClient { get; }
        public virtual ResourceType ResourceTypeEnum { get; }
        public virtual OperationType OperationTypeEnum { get; }
        public virtual Type ResourceType { get; }
        public SqlQuerySpec SqlQuerySpec { get; internal set; }
        public virtual QueryRequestOptions QueryRequestOptions { get; }
        public virtual bool IsContinuationExpected { get; }
        public virtual bool AllowNonValueAggregateQuery { get; }
        public virtual Uri ResourceLink { get; }
        public virtual string ContainerResourceId { get; set; }
        public virtual Guid CorrelatedActivityId { get; }

        internal CosmosQueryContext()
        {
        }

        public CosmosQueryContext(
            CosmosQueryClient client,
            ResourceType resourceTypeEnum,
            OperationType operationType,
            Type resourceType,
            SqlQuerySpec sqlQuerySpecFromUser,
            QueryRequestOptions queryRequestOptions,
            Uri resourceLink,
            bool getLazyFeedResponse,
            Guid correlatedActivityId,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            string containerResourceId = null)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (resourceType == null)
            {
                throw new ArgumentNullException(nameof(resourceType));
            }

            if (sqlQuerySpecFromUser == null)
            {
                throw new ArgumentNullException(nameof(sqlQuerySpecFromUser));
            }

            if (queryRequestOptions == null)
            {
                throw new ArgumentNullException(nameof(queryRequestOptions));
            }

            if (correlatedActivityId == Guid.Empty)
            {
                throw new ArgumentException(nameof(correlatedActivityId));
            }

            this.OperationTypeEnum = operationType;
            this.QueryClient = client;
            this.ResourceTypeEnum = resourceTypeEnum;
            this.ResourceType = resourceType;
            this.SqlQuerySpec = sqlQuerySpecFromUser;
            this.QueryRequestOptions = queryRequestOptions;
            this.ResourceLink = resourceLink;
            this.ContainerResourceId = containerResourceId;
            this.IsContinuationExpected = isContinuationExpected;
            this.AllowNonValueAggregateQuery = allowNonValueAggregateQuery;
            this.CorrelatedActivityId = correlatedActivityId;
        }

        internal virtual async Task<QueryResponse> ExecuteQueryAsync(
            SqlQuerySpec querySpecForInit,
            string continuationToken,
            PartitionKeyRangeIdentity partitionKeyRange,
            bool isContinuationExpected,
            int pageSize,
            CancellationToken cancellationToken)
        {
            QueryRequestOptions requestOptions = this.QueryRequestOptions.Clone();

            return await this.QueryClient.ExecuteItemQueryAsync(
                           this.ResourceLink,
                           this.ResourceTypeEnum,
                           this.OperationTypeEnum,
                           this.ContainerResourceId,
                           requestOptions,
                           querySpecForInit,
                           requestEnricher: (cosmosRequestMessage) =>
                           {
                               this.PopulatePartitionKeyRangeInfo(cosmosRequestMessage, partitionKeyRange);
                               cosmosRequestMessage.Headers.Add(
                                   HttpConstants.HttpHeaders.IsContinuationExpected,
                                   isContinuationExpected.ToString());
                               QueryRequestOptions.FillContinuationToken(
                                   cosmosRequestMessage,
                                   continuationToken);
                               QueryRequestOptions.FillMaxItemCount(
                                   cosmosRequestMessage,
                                   pageSize);
                               cosmosRequestMessage.Headers.Add(HttpConstants.HttpHeaders.ContentType, MediaTypes.QueryJson);
                               cosmosRequestMessage.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                           },
                           cancellationToken);
        }

        private void PopulatePartitionKeyRangeInfo(
            RequestMessage request, 
            PartitionKeyRangeIdentity partitionKeyRangeIdentity)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.ResourceType.IsPartitioned())
            {
                // If the request already has the logical partition key,
                // then we shouldn't add the physical partition key range id.

                bool hasPartitionKey = request.Headers.PartitionKey != null;
                if (!hasPartitionKey)
                {
                    request
                        .ToDocumentServiceRequest()
                        .RouteTo(partitionKeyRangeIdentity);
                }
            }
        }
    }
}
