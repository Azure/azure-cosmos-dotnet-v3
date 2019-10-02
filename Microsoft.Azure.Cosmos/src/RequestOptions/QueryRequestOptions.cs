//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The Cosmos query request options
    /// </summary>
    public class QueryRequestOptions : RequestOptions
    {
        /// <summary>
        ///  Gets or sets the <see cref="ResponseContinuationTokenLimitInKb"/> request option for document query requests in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <para> 
        /// ResponseContinuationTokenLimitInKb is used to limit the length of continuation token in the query response. Valid values are >= 0.
        /// </para>
        /// </remarks>
        public int? ResponseContinuationTokenLimitInKb { get; set; }

        /// <summary>
        /// Gets or sets the option to enable scans on the queries which couldn't be served
        /// as indexing was opted out on the requested paths in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Option is true if scan on queries is enabled; otherwise, false.
        /// </value>
        public bool? EnableScanInQuery { get; set; }

        /// <summary>
        /// Gets or sets the option to enable low precision order by in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The option to enable low-precision order by.
        /// </value>
        public bool? EnableLowPrecisionOrderBy { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of items that can be buffered client side during 
        /// parallel query execution in the Azure Cosmos DB service. 
        /// A positive property value limits the number of buffered 
        /// items to the set value. If it is set to less than 0, the system automatically 
        /// decides the number of items to buffer.
        /// </summary>
        /// <value>
        /// The maximum count of items that can be buffered during parallel query execution.
        /// </value> 
        /// <remarks>
        /// This is only suggestive and cannot be abided by in certain cases.
        /// </remarks>
        public int? MaxBufferedItemCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum number of items to be returned in the enumeration operation.
        /// </value> 
        /// <remarks>
        /// Used for query pagination.
        /// '-1' Used for dynamic page size.
        /// This is a maximum. Query can return 0 items in the page.
        /// </remarks>
        public int? MaxItemCount { get; set; }

        /// <summary>
        /// Gets or sets the number of concurrent operations run client side during 
        /// parallel query execution in the Azure Cosmos DB service. 
        /// A positive property value limits the number of 
        /// concurrent operations to the set value. If it is set to less than 0, the 
        /// system automatically decides the number of concurrent operations to run.
        /// </summary>
        /// <value>
        /// The maximum number of concurrent operations during parallel execution. 
        /// Defaults will be executed serially with no-parallelism
        /// </value> 
        public int? MaxConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Cosmos.PartitionKey"/> for the current request in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// Only applicable to Item operations
        /// </remarks>
        public PartitionKey? PartitionKey { get; set; }

        /// <summary>
        /// Gets or sets the consistency level required for the request in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The consistency level required for the request.
        /// </value>
        /// <remarks>
        /// Azure Cosmos DB offers 5 different consistency levels. Strong, Bounded Staleness, Session, Consistent Prefix and Eventual - in order of strongest to weakest consistency. <see cref="ConnectionPolicy"/>
        /// <para>
        /// While this is set at a database account level, Azure Cosmos DB allows a developer to override the default consistency level
        /// for each individual request.
        /// </para>
        /// </remarks>
        public ConsistencyLevel? ConsistencyLevel
        {
            get => this.BaseConsistencyLevel;
            set => this.BaseConsistencyLevel = value;
        }

        /// <summary>
        /// Gets or sets the token for use with session consistency in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The token for use with session consistency.
        /// </value>
        ///
        /// <remarks>
        /// One of the <see cref="ConsistencyLevel"/> for Azure Cosmos DB is Session. In fact, this is the default level applied to accounts.
        /// <para>
        /// When working with Session consistency, each new write request to Azure Cosmos DB is assigned a new SessionToken.
        /// The DocumentClient will use this token internally with each read/query request to ensure that the set consistency level is maintained.
        ///
        /// <para>
        /// In some scenarios you need to manage this Session yourself;
        /// Consider a web application with multiple nodes, each node will have its own instance of <see cref="DocumentClient"/>
        /// If you wanted these nodes to participate in the same session (to be able read your own writes consistently across web tiers)
        /// you would have to send the SessionToken from <see cref="QueryResponse{T}"/> of the write action on one node
        /// to the client tier, using a cookie or some other mechanism, and have that token flow back to the web tier for subsequent reads.
        /// If you are using a round-robin load balancer which does not maintain session affinity between requests, such as the Azure Load Balancer,
        /// the read could potentially land on a different node to the write request, where the session was created.
        /// </para>
        ///
        /// <para>
        /// If you do not flow the Azure Cosmos DB SessionToken across as described above you could end up with inconsistent read results for a period of time.
        /// </para>
        ///
        /// </para>
        /// </remarks>
        internal string SessionToken { get; set; }

        internal CosmosSerializationFormatOptions CosmosSerializationFormatOptions { get; set; }

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="RequestMessage"/></param>
        internal override void PopulateRequestOptions(RequestMessage request)
        {
            if (this.PartitionKey != null && request.ResourceType != ResourceType.Document)
            {
                throw new ArgumentException($"{nameof(this.PartitionKey)} can only be set for item operations");
            }

            // Cross partition is only applicable to item operations.
            if (this.PartitionKey == null && !this.IsEffectivePartitionKeyRouting && request.ResourceType == ResourceType.Document)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.EnableCrossPartitionQuery, bool.TrueString);
            }

            RequestOptions.SetSessionToken(request, this.SessionToken);

            // Flow the pageSize only when we are not doing client eval
            if (this.MaxItemCount.HasValue)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.PageSize, this.MaxItemCount.ToString());
            }

            if (this.MaxConcurrency.HasValue && this.MaxConcurrency > 0)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.ParallelizeCrossPartitionQuery, bool.TrueString);
            }

            if (this.EnableScanInQuery.HasValue && this.EnableScanInQuery.Value)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.EnableScanInQuery, bool.TrueString);
            }

            if (this.EnableLowPrecisionOrderBy != null)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, this.EnableLowPrecisionOrderBy.ToString());
            }

            if (this.ResponseContinuationTokenLimitInKb != null)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB, this.ResponseContinuationTokenLimitInKb.ToString());
            }

            if (this.CosmosSerializationFormatOptions != null)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.ContentSerializationFormat, this.CosmosSerializationFormatOptions.ContentSerializationFormat);
            }

            request.Headers.Add(HttpConstants.HttpHeaders.PopulateQueryMetrics, bool.TrueString);

            base.PopulateRequestOptions(request);
        }

        internal QueryRequestOptions Clone()
        {
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                IfMatchEtag = this.IfMatchEtag,
                IfNoneMatchEtag = this.IfNoneMatchEtag,
                MaxItemCount = this.MaxItemCount,
                ResponseContinuationTokenLimitInKb = this.ResponseContinuationTokenLimitInKb,
                EnableScanInQuery = this.EnableScanInQuery,
                EnableLowPrecisionOrderBy = this.EnableLowPrecisionOrderBy,
                MaxBufferedItemCount = this.MaxBufferedItemCount,
                SessionToken = this.SessionToken,
                ConsistencyLevel = this.ConsistencyLevel,
                MaxConcurrency = this.MaxConcurrency,
                PartitionKey = this.PartitionKey,
                CosmosSerializationFormatOptions = this.CosmosSerializationFormatOptions,
                Properties = this.Properties,
                IsEffectivePartitionKeyRouting = this.IsEffectivePartitionKeyRouting,
            };

            return queryRequestOptions;
        }

        internal static void FillContinuationToken(
            RequestMessage request,
            string continuationToken)
        {
            if (!string.IsNullOrWhiteSpace(continuationToken))
            {
                request.Headers.ContinuationToken = continuationToken;
            }
        }

        internal static void FillMaxItemCount(
            RequestMessage request,
            int? maxItemCount)
        {
            if (maxItemCount != null && maxItemCount.HasValue)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.PageSize, maxItemCount.Value.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}