//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    internal interface IDocumentQueryClient : IDisposable
    {
        QueryCompatibilityMode QueryCompatibilityMode { get; set; }

        IRetryPolicyFactory ResetSessionTokenRetryPolicy { get; }

        Uri ServiceEndpoint { get; }

        ConnectionMode ConnectionMode { get; }

        Action<IQueryable> OnExecuteScalarQueryCallback { get; }

        Task<CollectionCache> GetCollectionCacheAsync();

        Task<IRoutingMapProvider> GetRoutingMapProviderAsync();

        Task<QueryPartitionProvider> GetQueryPartitionProviderAsync(CancellationToken cancellationToken);

        Task<DocumentServiceResponse> ExecuteQueryAsync(DocumentServiceRequest request, IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken);

        Task<DocumentServiceResponse> ReadFeedAsync(DocumentServiceRequest request, IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken);

        Task<Documents.ConsistencyLevel> GetDefaultConsistencyLevelAsync();

        Task<Documents.ConsistencyLevel?> GetDesiredConsistencyLevelAsync();

        Task EnsureValidOverwriteAsync(Documents.ConsistencyLevel desiredConsistencyLevel);

        Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync();
    }

    /// <summary>
    /// A client query compatibility mode when making query request.
    /// Can be used to force a specific query request format.
    /// </summary>
    internal enum QueryCompatibilityMode
    {
        /// <summary>
        /// Default (latest) query format.
        /// </summary>
        Default,

        /// <summary>
        /// Query (application/query+json).
        /// Default.
        /// </summary>
        Query,

        /// <summary>
        /// SqlQuery (application/sql).
        /// </summary>
        SqlQuery
    }
}
