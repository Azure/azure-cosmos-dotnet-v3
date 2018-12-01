//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Routing;

    internal interface IDocumentQueryClient : IDisposable
    {
        QueryCompatibilityMode QueryCompatibilityMode { get; set; }

        RetryPolicy RetryPolicy { get; }

        Uri ServiceEndpoint { get; }

        ConnectionMode ConnectionMode { get; }

        Action<IQueryable> OnExecuteScalarQueryCallback { get; }

        Task<CollectionCache> GetCollectionCacheAsync();

        Task<IRoutingMapProvider> GetRoutingMapProviderAsync();

        Task<QueryPartitionProvider> GetQueryPartitionProviderAsync(CancellationToken cancellationToken);

        Task<DocumentServiceResponse> ExecuteQueryAsync(DocumentServiceRequest request, CancellationToken cancellationToken);

        Task<DocumentServiceResponse> ReadFeedAsync(DocumentServiceRequest request, CancellationToken cancellationToken);

        Task<ConsistencyLevel> GetDefaultConsistencyLevelAsync();

        Task<ConsistencyLevel?> GetDesiredConsistencyLevelAsync();

        Task EnsureValidOverwrite(ConsistencyLevel desiredConsistencyLevel);

        Task<PartitionKeyRangeCache> GetPartitionKeyRangeCache();
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
