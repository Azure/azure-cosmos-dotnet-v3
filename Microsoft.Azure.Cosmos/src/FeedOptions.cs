//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// Specifies the options associated with feed methods (enumeration operations) in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// Used to manage query and ReadFeed execution. Can use FeedOptions to set page size (MaxItemCount)
    /// </remarks>
    internal sealed class FeedOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FeedOptions"/> class for the Azure Cosmos DB service.
        /// </summary>
        public FeedOptions()
        {
        }

        internal FeedOptions(FeedOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            this.MaxItemCount = options.MaxItemCount;
            this.RequestContinuationToken = options.RequestContinuationToken;
            this.SessionToken = options.SessionToken;
            this.EnableScanInQuery = options.EnableScanInQuery;
            this.EnableCrossPartitionQuery = options.EnableCrossPartitionQuery;
            this.EnableLowPrecisionOrderBy = options.EnableLowPrecisionOrderBy;
            this.MaxBufferedItemCount = options.MaxBufferedItemCount;
            this.MaxDegreeOfParallelism = options.MaxDegreeOfParallelism;
            this.PartitionKeyRangeId = options.PartitionKeyRangeId;
            this.PopulateQueryMetrics = options.PopulateQueryMetrics;
            this.ResponseContinuationTokenLimitInKb = options.ResponseContinuationTokenLimitInKb;
            this.DisableRUPerMinuteUsage = options.DisableRUPerMinuteUsage;

            if (options.PartitionKey == null)
            {
                this.PartitionKey = null;
            }
            else
            {
                this.PartitionKey = Documents.PartitionKey.FromInternalKey(options.PartitionKey.InternalKey);
            }

            this.EmitVerboseTracesInQuery = options.EmitVerboseTracesInQuery;
            this.FilterBySchemaResourceId = options.FilterBySchemaResourceId;
            this.RequestContinuationToken = options.RequestContinuationToken;
            this.ConsistencyLevel = options.ConsistencyLevel;
            this.JsonSerializerSettings = options.JsonSerializerSettings;
            this.ForceQueryScan = options.ForceQueryScan;
            this.EnumerationDirection = options.EnumerationDirection;
            this.ReadFeedKeyType = options.ReadFeedKeyType;
            this.StartId = options.StartId;
            this.EndId = options.EndId;
            this.StartEpk = options.StartEpk;
            this.EndEpk = options.EndEpk;
            this.ContentSerializationFormat = options.ContentSerializationFormat;
            this.EnableGroupBy = options.EnableGroupBy;
            this.MergeStaticId = options.MergeStaticId;
            this.Properties = options.Properties;
        }

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
        ///         DoucmentFeedResponse<Book> response = await queryable.ExecuteNext<Book>();
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public int? MaxItemCount { get; set; }

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
        public string RequestContinuationToken { get; set; }

        /// <summary>
        /// Gets or sets the session token for use with session consistency in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The session token for use with session consistency.
        /// </value>
        /// <remarks>
        /// Useful for applications that are load balanced across multiple Microsoft.Azure.Documents.Client.DocumentClient instances. 
        /// In this case, round-trip the token from end user to the application and then back to Azure Cosmos DB so that a session
        /// can be preserved across servers.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// var queryable = client.CreateDocumentQuery<Book>(
        ///     collectionLink, new FeedOptions { SessionToken = lastSessionToken });
        /// ]]>
        /// </code>
        /// </example>
        public string SessionToken { get; set; }

        /// <summary>
        /// Gets or sets the option to enable scans on the queries which couldn't be served
        /// as indexing was opted out on the requested paths in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Option is true if scan on queries is enabled; otherwise, false.
        /// </value>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// // Enable scan when Range index is not specified.
        /// var queryable = client.CreateDocumentQuery<Book>(
        ///     collectionLink, new FeedOptions { EnableScanInQuery = true }).Where(b => b.Price > 1000);
        /// ]]>
        /// </code>
        /// </example>
        public bool? EnableScanInQuery { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether users are enabled to send more than one request to execute
        /// the query in the Azure Cosmos DB service. More than one request is necessary if the query 
        /// is not scoped to single partition key value.
        /// </summary>
        /// <value>
        /// Option is true if cross-partition query execution is enabled; otherwise, false.
        /// </value>
        /// <remarks>
        /// <para>
        /// This option only applies to queries on documents and document attachments.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// // Enable cross partition query.
        /// var queryable = client.CreateDocumentQuery<Book>(
        ///     collectionLink, new FeedOptions { EnableCrossPartitionQuery = true }).Where(b => b.Price > 1000);
        /// ]]>
        /// </code>
        /// </example>
        public bool EnableCrossPartitionQuery { get; set; }

        /// <summary>
        /// Gets or sets the option to enable low precision order by in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The option to enable low-precision order by.
        /// </value>
        public bool? EnableLowPrecisionOrderBy { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Microsoft.Azure.Documents.PartitionKey"/> for the current request in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Partition key is required when read documents or attachments feed in a partitioned collection. 
        /// Specifically Partition key is required for :
        ///     <see cref="DocumentClient.ReadConflictFeedAsync(string, FeedOptions)"/>.  
        /// Only documents in partitions containing the <see cref="Microsoft.Azure.Documents.PartitionKey"/> is returned in the result.
        /// </para>
        /// </remarks>
        /// <example>
        /// The following example shows how to read a document feed in a partitioned collection using <see cref="Microsoft.Azure.Documents.PartitionKey"/>.
        /// The example assumes the collection is created with a <see cref="PartitionKeyDefinition"/> on the 'country' property in all the documents.
        /// <code language="c#">
        /// <![CDATA[
        /// await client.ReadDocumentFeedAsync(
        ///     collection.SelfLink, 
        ///     new RequestOptions { PartitionKey = new PartitionKey("USA") } );
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="ContainerProperties"/>
        /// <seealso cref="Microsoft.Azure.Documents.PartitionKeyDefinition"/>
        public Documents.PartitionKey PartitionKey { get; set; }

        /// <summary>
        /// Gets or sets the partition key range id for the current request.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ReadFeed requests can use this to forward request to specific range.
        /// This is usefull in case of bulk export scenarios.
        /// </para>
        /// </remarks>
        /// <example>
        /// The following example shows how to read a document feed in a partitioned collection from partition key range "20".
        /// <code language="c#">
        /// <![CDATA[
        /// await client.ReadDocumentFeedAsync(
        ///     collection.SelfLink, 
        ///     new RequestOptions { PartitionKeyRangeId = "20" } );
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="ContainerProperties"/>
        public string PartitionKeyRangeId { get; set; }

        /// <summary>
        /// Gets or sets the number of concurrent operations run client side during 
        /// parallel query execution in the Azure Cosmos DB service. 
        /// A positive property value limits the number of 
        /// concurrent operations to the set value. If it is set to less than 0, the 
        /// system automatically decides the number of concurrent operations to run.
        /// </summary>
        /// <value>
        /// The maximum number of concurrent operations during parallel execution. Defaults to 0.
        /// </value> 
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// var queryable = client.CreateDocumentQuery<Book>(collectionLink, new FeedOptions { 
        /// MaxDegreeOfParallelism = 5});
        /// ]]>
        /// </code>
        /// </example>
        public int MaxDegreeOfParallelism { get; set; }

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
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// var queryable = client.CreateDocumentQuery<Book>(collectionLink, new FeedOptions { 
        /// MaximumBufferSize = 10, MaxDegreeOfParallelism = 2 });
        /// ]]>
        /// </code>
        /// </example>
        public int MaxBufferedItemCount { get; set; }

        /// <summary>
        /// Gets or sets the option to allow queries to emit out verbose traces 
        /// for investigation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Option is true if query tracing is enabled; otherwise, false.
        /// </value>
        internal bool? EmitVerboseTracesInQuery { get; set; }

        /// <summary>
        /// Gets or sets the schema rid which could be used to filter the document feed response
        /// in order to focus on the documents for a particular schema.
        /// </summary>
        /// <value>
        /// By default, it is <c>null</c> which means no filtering will be applied.
        /// Otherwise, it must be a valid resource id of Schema resource.
        /// </value>
        internal string FilterBySchemaResourceId { get; set; }

        /// <summary>
        ///  Gets or sets the <see cref="PopulateQueryMetrics"/> request option for document query requests in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <para> 
        /// PopulateQueryMetrics is used to enable/disable getting metrics relating to query execution on document query requests.
        /// </para>
        /// </remarks>
        public bool PopulateQueryMetrics { get; set; }

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
        /// Gets or sets the <see cref="DisableRUPerMinuteUsage"/> option for the current query in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <para> 
        /// DisableRUPerMinuteUsage is used to enable/disable Request Units(RUs)/minute capacity to serve the query if regular provisioned RUs/second is exhausted.
        /// </para>
        /// </remarks>
        public bool DisableRUPerMinuteUsage { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="JsonSerializerSettings"/> for the current request used to deserialize the document.
        /// If null, uses the default serializer settings set up in the DocumentClient.
        /// </summary>
        public JsonSerializerSettings JsonSerializerSettings { get; set; }

        /// <summary>
        /// Gets or sets the consistency level required for the feed (query/read feed) operation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The consistency level required for the request.
        /// </value>
        /// <remarks>
        /// Azure Cosmos DB offers 5 different consistency levels. Strong, Bounded Staleness, Session, Consistent Prefix and Eventual - in order of strongest to weakest consistency. <see cref="ConnectionPolicy"/>
        /// 
        /// Azure Cosmos query/DB feed operations may be retrieved from many partitions, each accessed across many round trips. The consistency level is honored only within a partition and round trip. 
        /// <para>
        /// While this is set at a database account level, Azure Cosmos DB allows a developer to override the default consistency level
        /// for each individual request. 
        /// </para>
        /// </remarks>
        /// <example>
        /// This example uses FeedOptions to override the consistency level to Eventual. 
        /// <code language="c#">
        /// <![CDATA[
        /// Document doc = client.ReadDocumentFeedAsync(documentLink, new FeedOptions { ConsistencyLevel = ConsistencyLevel.Eventual });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="ConsistencyLevel"/>
        public ConsistencyLevel? ConsistencyLevel { get; set; }

        /// <summary>
        /// Gets or sets the flag that tells the backend to forces the query to perform a scan (at a request level).
        /// </summary>
        internal bool ForceQueryScan { get; set; }

        /// <summary>
        /// Gets or sets the EnumerationDirection
        /// To be used along with Read feed operation
        /// </summary>
        internal EnumerationDirection? EnumerationDirection { get; set; }

        /// <summary>
        /// Gets or sets the ReadFeedKeyType
        /// To be used along with Read feed operation
        /// </summary>
        internal ReadFeedKeyType? ReadFeedKeyType { get; set; }

        /// <summary>
        /// Gets or sets the StartId
        /// To be used along with Read feed operation
        /// </summary>
        internal string StartId { get; set; }

        /// <summary>
        /// Gets or sets the EndId
        /// To be used along with Read feed operation
        /// </summary>
        internal string EndId { get; set; }

        /// <summary>
        /// Gets or sets the StartEpk
        /// To be used along with Read feed operation
        /// </summary>
        internal string StartEpk { get; set; }

        /// <summary>
        /// Gets or sets the EndEpk
        /// To be used along with Read feed operation
        /// </summary>
        internal string EndEpk { get; set; }

        /// <summary>
        /// Gets or sets the ContentSerializationFormat for the feed (query/read feed) operation in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// If the document is stored in a different serialization format then the one requested, then there will be a rewrite over the wire, but the source document will be untouched.
        /// </remarks>
        internal ContentSerializationFormat? ContentSerializationFormat { get; set; }

        internal bool EnableGroupBy { get; set; }

        /// <summary>
        /// Gets or sets the MergeStaticId.
        /// To be used along with Read feed operation when Static Column merge is desired.
        /// </summary>
        internal string MergeStaticId { get; set; }

        /// <summary>
        /// Gets or sets the custom serialization options for query
        /// </summary>
        internal CosmosSerializationFormatOptions CosmosSerializationFormatOptions { get; set; }

        internal IDictionary<string, object> Properties { get; set; }
    }
}
