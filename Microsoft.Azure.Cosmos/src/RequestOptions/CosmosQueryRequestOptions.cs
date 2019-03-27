//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Internal;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.Internal.RuntimeConstants;

    /// <summary>
    /// The Cosmos query request options
    /// </summary>
    public class CosmosQueryRequestOptions : CosmosRequestOptions
    {
        private readonly FeedOptions feedOptions = null;

        /// <summary>
        /// The default empty constructor
        /// </summary>
        public CosmosQueryRequestOptions() : base() { }

        /// <summary>
        /// This allows easy conversion from feed options to CosmosQueryRequestOptions.
        /// DEVNOTE: This can be removed once query is completely on new v3 handler pipeline.
        /// </summary>
        internal CosmosQueryRequestOptions(FeedOptions feedOptions)
        {
            if (feedOptions != null)
            {
                this.RequestContinuation = feedOptions.RequestContinuation;
                this.MaxItemCount = feedOptions.MaxItemCount;
                this.ResponseContinuationTokenLimitInKb = feedOptions.ResponseContinuationTokenLimitInKb;
                this.EnableScanInQuery = feedOptions.EnableScanInQuery;
                this.EnableLowPrecisionOrderBy = feedOptions.EnableLowPrecisionOrderBy;
                this.MaxBufferedItemCount = feedOptions.MaxBufferedItemCount;
                this.SessionToken = feedOptions.SessionToken;
                this.ConsistencyLevel = feedOptions.ConsistencyLevel;
                this.MaxConcurrency = feedOptions.MaxDegreeOfParallelism;
                this.PartitionKey = feedOptions.PartitionKey;
                this.EnableCrossPartitionQuery = feedOptions.EnableCrossPartitionQuery;
                this.CosmosSerializationOptions = feedOptions.CosmosSerializationOptions;
                this.Properties = feedOptions.Properties;
                this.feedOptions = new FeedOptions(feedOptions);
            }
        }

        /// <summary>
        ///  Gets or sets the <see cref="ResponseContinuationTokenLimitInKb"/> request option for document query requests in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <para> 
        /// ResponseContinuationTokenLimitInKb is used to limit the length of continuation token in the query response. Valid values are >= 0.
        /// </para>
        /// </remarks>
        public virtual int? ResponseContinuationTokenLimitInKb { get; set; }

        /// <summary>
        /// Gets or sets the option to enable scans on the queries which couldn't be served
        /// as indexing was opted out on the requested paths in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Option is true if scan on queries is enabled; otherwise, false.
        /// </value>
        public virtual bool? EnableScanInQuery { get; set; }

        /// <summary>
        /// Gets or sets the option to enable low precision order by in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The option to enable low-precision order by.
        /// </value>
        public virtual bool? EnableLowPrecisionOrderBy { get; set; }

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
        public virtual int MaxBufferedItemCount { get; set; }

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
        /// you would have to send the SessionToken from <see cref="ResourceResponse{T}"/> of the write action on one node
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
        public virtual string SessionToken { get; set; }

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
        public virtual ConsistencyLevel? ConsistencyLevel { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum number of items to be returned in the enumeration operation.
        /// </value> 
        /// <remarks>
        /// Used for query pagination.
        /// '-1' Used for dynamic page size.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// // Fetch query results 10 at a time.
        /// using (var queryable = client.CreateDocumentQuery<Book>(collectionLink, new FeedOptions { MaxItemCount = 10 }))
        /// {
        ///     while (queryable.HasResults)
        ///     {
        ///         FeedResponse<Book> response = await queryable.ExecuteNext<Book>();
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual int? MaxItemCount { get; set; }

        /// <summary>
        /// Gets or sets the request continuation token in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request continuation token.
        /// </value>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// // Resume query execution using the continuation from the previous query
        /// var queryable = client.CreateDocumentQuery<Book>(collectionLink, new FeedOptions { RequestContinuation = prevQuery.ResponseContinuation });
        /// ]]>
        /// </code>
        /// </example>
        public virtual string RequestContinuation { get; set; }

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
        internal int MaxConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="PartitionKey"/> for the current request in the Azure Cosmos DB service.
        /// </summary>
        internal PartitionKey PartitionKey { get; set; }

        internal bool EnableCrossPartitionQuery { get; set; }

        internal CosmosSerializationOptions CosmosSerializationOptions { get; set; }

        /// <summary>
        /// DEVNOTE: This exists to keep compatability with FeedOptions
        /// Gets the partition key range id for the current request.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ReadFeed requests can use this to forward request to specific range.
        /// This is usefull in case of bulk export scenarios.
        /// </para>
        /// </remarks>
        internal string PartitionKeyRangeId => this.feedOptions == null ? null : this.feedOptions.PartitionKeyRangeId;

        /// <summary>
        /// DEVNOTE: This exists to keep compatability with FeedOptions. Support for JsonSerializerSettings is now obsolete. Please use CosmosJsonSerializer.
        /// Gets the <see cref="JsonSerializerSettings"/> for the current request used to deserialize the document.
        /// If null, uses the default serializer settings set up in the DocumentClient.
        /// </summary>
        internal JsonSerializerSettings JsonSerializerSettings => this.feedOptions == null ? null : this.feedOptions.JsonSerializerSettings;

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="CosmosRequestMessage"/></param>
        public override void FillRequestOptions(CosmosRequestMessage request)
        {
            if (request.OperationType == OperationType.SqlQuery)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                request.Headers.Add(HttpConstants.HttpHeaders.EnableCrossPartitionQuery, this.EnableCrossPartitionQuery ? bool.TrueString : bool.FalseString);
                request.Headers.Add(HttpConstants.HttpHeaders.ContentType, MediaTypes.QueryJson);
            }

            if (this.EnableScanInQuery.HasValue && this.EnableScanInQuery.Value)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.EnableScanInQuery, bool.TrueString);
            }

            CosmosRequestOptions.SetSessionToken(request, this.SessionToken);
            CosmosRequestOptions.SetConsistencyLevel(request, this.ConsistencyLevel);

            base.FillRequestOptions(request);
        }

        /// <summary>
        /// DEVNOTE: This will be converted to use CosmosRequestMessage in next PR
        /// </summary>
        internal INameValueCollection CreateCommonHeadersAsync(
            CosmosQueryRequestOptions queryRequestOptions,
            ConsistencyLevel defaultConsistencyLevel,
            ConsistencyLevel? desiredConsistencyLevel,
            ResourceType resourceType)
        {
            INameValueCollection requestHeaders = new StringKeyValueCollection();

            if (!string.IsNullOrEmpty(queryRequestOptions.SessionToken) && !ReplicatedResourceClient.IsReadingFromMaster(resourceType, OperationType.ReadFeed))
            {
                if (defaultConsistencyLevel == Microsoft.Azure.Cosmos.ConsistencyLevel.Session ||
                    (desiredConsistencyLevel.HasValue && desiredConsistencyLevel.Value == Microsoft.Azure.Cosmos.ConsistencyLevel.Session))
                {
                    // Query across partitions is not supported today. Master resources (for e.g., database) 
                    // can span across partitions, whereas server resources (viz: collection, document and attachment)
                    // don't span across partitions. Hence, session token returned by one partition should not be used 
                    // when quering resources from another partition. 
                    // Since master resources can span across partitions, don't send session token to the backend.
                    // As master resources are sync replicated, we should always get consistent query result for master resources,
                    // irrespective of the chosen replica.
                    // For server resources, which don't span partitions, specify the session token 
                    // for correct replica to be chosen for servicing the query result.
                    requestHeaders[HttpConstants.HttpHeaders.SessionToken] = queryRequestOptions.SessionToken;
                }
            }

            requestHeaders[HttpConstants.HttpHeaders.Continuation] = queryRequestOptions.RequestContinuation;
            requestHeaders[HttpConstants.HttpHeaders.IsQuery] = bool.TrueString;

            // Flow the pageSize only when we are not doing client eval
            if (queryRequestOptions.MaxItemCount.HasValue)
            {
                requestHeaders[HttpConstants.HttpHeaders.PageSize] = queryRequestOptions.MaxItemCount.ToString();
            }

            requestHeaders[HttpConstants.HttpHeaders.EnableCrossPartitionQuery] = queryRequestOptions.EnableCrossPartitionQuery.ToString();

            if (queryRequestOptions.MaxConcurrency != 0)
            {
                requestHeaders[HttpConstants.HttpHeaders.ParallelizeCrossPartitionQuery] = bool.TrueString;
            }

            if (this.EnableScanInQuery != null)
            {
                requestHeaders[HttpConstants.HttpHeaders.EnableScanInQuery] = this.EnableScanInQuery.ToString();
            }

            if (this.feedOptions.EmitVerboseTracesInQuery != null)
            {
                requestHeaders[HttpConstants.HttpHeaders.EmitVerboseTracesInQuery] = this.feedOptions.EmitVerboseTracesInQuery.ToString();
            }

            if (this.EnableLowPrecisionOrderBy != null)
            {
                requestHeaders[HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy] = this.EnableLowPrecisionOrderBy.ToString();
            }

            if (!string.IsNullOrEmpty(this.feedOptions.FilterBySchemaResourceId))
            {
                requestHeaders[HttpConstants.HttpHeaders.FilterBySchemaResourceId] = this.feedOptions.FilterBySchemaResourceId;
            }

            if (this.ResponseContinuationTokenLimitInKb != null)
            {
                requestHeaders[HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB] = this.ResponseContinuationTokenLimitInKb.ToString();
            }

            if (this.ConsistencyLevel.HasValue)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.ConsistencyLevel, this.ConsistencyLevel.Value.ToString());
            }
            else if (desiredConsistencyLevel.HasValue)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.ConsistencyLevel, desiredConsistencyLevel.Value.ToString());
            }

            if (this.feedOptions.EnumerationDirection.HasValue)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.EnumerationDirection, this.feedOptions.EnumerationDirection.Value.ToString());
            }

            if (this.feedOptions.ReadFeedKeyType.HasValue)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.ReadFeedKeyType, this.feedOptions.ReadFeedKeyType.Value.ToString());
            }

            if (this.feedOptions.StartId != null)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.StartId, this.feedOptions.StartId);
            }

            if (this.feedOptions.EndId != null)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.EndId, this.feedOptions.EndId);
            }

            if (this.feedOptions.StartEpk != null)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.StartEpk, this.feedOptions.StartEpk);
            }

            if (this.feedOptions.EndEpk != null)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.EndEpk, this.feedOptions.EndEpk);
            }

            if (this.feedOptions.PopulateQueryMetrics)
            {
                requestHeaders[HttpConstants.HttpHeaders.PopulateQueryMetrics] = bool.TrueString;
            }

            if (this.feedOptions.ForceQueryScan)
            {
                requestHeaders[HttpConstants.HttpHeaders.ForceQueryScan] = bool.TrueString;
            }

            if (this.CosmosSerializationOptions != null)
            {
                requestHeaders[HttpConstants.HttpHeaders.ContentSerializationFormat] = this.CosmosSerializationOptions.ContentSerializationFormat;
            }
            else if (this.feedOptions.ContentSerializationFormat.HasValue)
            {
                requestHeaders[HttpConstants.HttpHeaders.ContentSerializationFormat] = this.feedOptions.ContentSerializationFormat.Value.ToString();
            }

            return requestHeaders;
        }

        internal CosmosQueryRequestOptions Clone()
        {
            return new CosmosQueryRequestOptions(this.feedOptions)
            {
                AccessCondition = this.AccessCondition,
                RequestContinuation = this.RequestContinuation,
                MaxItemCount = this.MaxItemCount,
                ResponseContinuationTokenLimitInKb = this.ResponseContinuationTokenLimitInKb,
                EnableScanInQuery = this.EnableScanInQuery,
                EnableLowPrecisionOrderBy = this.EnableLowPrecisionOrderBy,
                MaxBufferedItemCount = this.MaxBufferedItemCount,
                SessionToken = this.SessionToken,
                ConsistencyLevel = this.ConsistencyLevel,
                MaxConcurrency = this.MaxConcurrency,
                PartitionKey = this.PartitionKey,
                EnableCrossPartitionQuery = this.EnableCrossPartitionQuery,
                CosmosSerializationOptions = this.CosmosSerializationOptions,
                Properties = this.Properties,
            };
        }

        /// <summary>
        /// Copy the internal feed options and update all the properties that might have changed.
        /// </summary>
        /// <returns></returns>
        internal FeedOptions ToFeedOptions()
        {
            FeedOptions feedOptions = null;
            if (this.feedOptions != null)
            {
                feedOptions = new FeedOptions(this.feedOptions);
            }
            else
            {
                feedOptions = new FeedOptions();
            }

            feedOptions.MaxItemCount = this.MaxItemCount;
            feedOptions.EnableCrossPartitionQuery = this.EnableCrossPartitionQuery;
            feedOptions.MaxDegreeOfParallelism = this.MaxConcurrency;
            feedOptions.PartitionKey = this.PartitionKey;
            feedOptions.RequestContinuation = this.RequestContinuation;
            feedOptions.ResponseContinuationTokenLimitInKb = this.ResponseContinuationTokenLimitInKb;
            feedOptions.EnableScanInQuery = this.EnableScanInQuery;
            feedOptions.EnableLowPrecisionOrderBy = this.EnableLowPrecisionOrderBy;
            feedOptions.MaxBufferedItemCount = this.MaxBufferedItemCount;
            feedOptions.CosmosSerializationOptions = this.CosmosSerializationOptions;
            feedOptions.Properties = this.Properties;

            return feedOptions;
        }

        internal static void FillContinuationToken(
            CosmosRequestMessage request,
            string continuationToken)
        {
            if (!string.IsNullOrWhiteSpace(continuationToken))
            {
                request.Headers.Add(HttpConstants.HttpHeaders.Continuation, continuationToken);
            }
        }

        internal static void FillMaxItemCount(
            CosmosRequestMessage request,
            int? maxItemCount)
        {
            if (maxItemCount != null && maxItemCount.HasValue)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.PageSize, maxItemCount.Value.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}