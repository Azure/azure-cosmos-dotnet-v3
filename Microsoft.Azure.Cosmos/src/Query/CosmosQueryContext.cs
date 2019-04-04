//-----------------------------------------------------------------------
// <copyright file="CosmosCrossPartitionQueryExecutionContext.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Collections.Generic;
    using Common;
    using ExecutionComponent;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;
    using ParallelQuery;
    using Routing;

    /// <summary>
    /// 
    /// </summary>
    internal class CosmosQueryContext
    {
        public CosmosQueries QueryClient { get; }
        public ResourceType ResourceTypeEnum { get; }
        public OperationType OperationTypeEnum { get; }
        public Type ResourceType { get; }
        public SqlQuerySpec SqlQuerySpecFromUser { get; }
        public SqlQuerySpec SqlQuerySpecForInit { get; set; }
        public CosmosQueryRequestOptions QueryRequestOptions { get; }
        public Uri ResourceLink { get; }
        public bool GetLazyFeedResponse { get; }
        public Guid CorrelatedActivityId { get; }

        public CosmosQueryContext(
            CosmosQueries client,
            ResourceType resourceTypeEnum,
            OperationType operationType,
            Type resourceType,
            SqlQuerySpec sqlQuerySpecFromUser,
            CosmosQueryRequestOptions queryRequestOptions,
            Uri resourceLink,
            bool getLazyFeedResponse,
            Guid correlatedActivityId)
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
            this.SqlQuerySpecFromUser = sqlQuerySpecFromUser;
            this.QueryRequestOptions = queryRequestOptions;
            this.ResourceLink = resourceLink;
            this.GetLazyFeedResponse = getLazyFeedResponse;
            this.CorrelatedActivityId = correlatedActivityId;
        }

        internal async Task<FeedResponse<CosmosElement>> ExecuteQueryAsync(
            CancellationToken cancellationToken,
            Action<CosmosRequestMessage> requestEnricher = null,
            Action<CosmosQueryRequestOptions> requestOptionsEnricher = null)
        {
            CosmosQueryRequestOptions requestOptions = this.QueryRequestOptions.Clone();
            if (requestOptionsEnricher != null)
            {
                requestOptionsEnricher(requestOptions);
            }

            return await this.QueryClient.ExecuteItemQueryAsync(
                           this.ResourceLink,
                           this.ResourceTypeEnum,
                           this.OperationTypeEnum,
                           requestOptions,
                           this.SqlQuerySpecForInit ?? this.SqlQuerySpecFromUser,
                           requestEnricher,
                           cancellationToken);
        }
    }
}
