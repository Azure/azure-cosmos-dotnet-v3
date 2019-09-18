//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OperationType = Documents.OperationType;
    using PartitionKeyRangeIdentity = Documents.PartitionKeyRangeIdentity;
    using ResourceType = Documents.ResourceType;

    internal abstract class CosmosQueryContext
    {
        public virtual CosmosQueryClient QueryClient { get; }
        public virtual ResourceType ResourceTypeEnum { get; }
        public virtual OperationType OperationTypeEnum { get; }
        public virtual Type ResourceType { get; }
        public virtual bool IsContinuationExpected { get; }
        public virtual bool AllowNonValueAggregateQuery { get; }
        public virtual Uri ResourceLink { get; }
        public virtual string ContainerResourceId { get; set; }
        public virtual bool EnableGroupBy { get; set; }
        public virtual Guid CorrelatedActivityId { get; }

        internal CosmosQueryContext()
        {
        }

        public CosmosQueryContext(
            CosmosQueryClient client,
            ResourceType resourceTypeEnum,
            OperationType operationType,
            Type resourceType,
            Uri resourceLink,
            Guid correlatedActivityId,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool enableGroupBy,
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

            if (correlatedActivityId == Guid.Empty)
            {
                throw new ArgumentException(nameof(correlatedActivityId));
            }

            this.EnableGroupBy = enableGroupBy;
            this.OperationTypeEnum = operationType;
            this.QueryClient = client;
            this.ResourceTypeEnum = resourceTypeEnum;
            this.ResourceType = resourceType;
            this.ResourceLink = resourceLink;
            this.ContainerResourceId = containerResourceId;
            this.IsContinuationExpected = isContinuationExpected;
            this.AllowNonValueAggregateQuery = allowNonValueAggregateQuery;
            this.CorrelatedActivityId = correlatedActivityId;
        }

        internal abstract Task<QueryResponseCore> ExecuteQueryAsync(
            SqlQuerySpec querySpecForInit,
            string continuationToken,
            PartitionKeyRangeIdentity partitionKeyRange,
            bool isContinuationExpected,
            int pageSize,
            CancellationToken cancellationToken);
    }
}
