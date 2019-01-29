//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading;
    using Newtonsoft.Json;

    /// <summary>
    /// Encapsulates options that can be specified for different requests issued to the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// Some of these options are valid for specific operations only.
    /// For example,
    /// <para>PreTriggerInclude can be used only on create, replace and delete operations on a <see cref="Internal.Document"/> or <see cref="Internal.Attachment"/>. </para>
    /// <para>ETag, while valid on Replace* and Delete* operations, would have no impact on a Read*, CreateQuery* or Create* operations.</para>
    /// </remarks>
    internal sealed class RequestOptions
    {
        /// <summary>
        /// Gets or sets the trigger to be invoked before the operation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The trigger to be invoked before the operation.
        /// </value>
        /// <remarks>
        /// Only valid when used with Create, Replace and Delete methods for documents.
        /// Currently only one PreTrigger is permitted per operation.
        /// </remarks>
        /// <example>
        /// The following example shows how to use RequestOptions to include a PreTrigger to execute before persisting the document.
        /// <code language="c#">
        /// <![CDATA[
        /// client.CreateDocumentAsync(collection.SelfLink,
        ///     new { id = "AndersenFamily", isRegistered = true },
        ///     new RequestOptions { PreTriggerInclude = new List<string> { "validateDocumentContents" } });
        /// ]]>
        /// </code>
        /// </example>
        /// <see cref="CosmosTriggerSettings"/>
        /// <see cref="System.Collections.Generic.IList{T}"/>
        public IList<string> PreTriggerInclude { get; set; }

        /// <summary>
        /// Gets or sets the trigger to be invoked after the operation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The trigger to be invoked after the operation.
        /// </value>
        /// <remarks>
        /// Only valid when used with Create, Replace and Delete methods for documents.
        /// Currently only one PreTrigger is permitted per operation.
        /// </remarks>
        /// <example>
        /// The following example shows how to use RequestOptions to include a PostTrigger to execute after persisting the document.
        /// <code language="c#">
        /// <![CDATA[
        /// client.CreateDocumentAsync(collection.SelfLink,
        /// new { id = "AndersenFamily", isRegistered = true },
        /// new RequestOptions { PostTriggerInclude = new List<string> { "updateMetadata" } });
        /// ]]>
        /// </code>
        /// </example>
        /// <see cref="CosmosTriggerSettings"/>
        public IList<string> PostTriggerInclude { get; set; }

        /// <summary>
        /// Gets or sets the condition (ETag) associated with the request in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The condition (ETag) associated with the request.
        /// </value>
        /// <remarks>
        /// Most commonly used with the Delete* and Replace* methods of <see cref="DocumentClient"/> such as <see cref="DocumentClient.ReplaceDocumentAsync(Internal.Document, RequestOptions, CancellationToken)"/>
        /// or <see cref="DocumentClient.ReplaceDocumentAsync(string, object, RequestOptions, CancellationToken)"/> methods,
        /// but can be used with other methods like <see cref="DocumentClient.ReadDocumentAsync(string, RequestOptions, CancellationToken)"/> for caching scenarios.
        /// </remarks>
        /// <example>
        /// The following example shows how to use RequestOptions with <see cref="DocumentClient.ReplaceDocumentAsync(string, object, RequestOptions, CancellationToken)"/> to
        /// specify the set of <see cref="AccessCondition"/> to be used when updating a document
        /// <code language="c#">
        /// <![CDATA[
        /// // If ETag is current, then this will succeed. Otherwise the request will fail with HTTP 412 Precondition Failure
        /// await client.ReplaceDocumentAsync(
        ///     readCopyOfBook.SelfLink,
        ///     new Book { Title = "Moby Dick", Price = 14.99 },
        ///     new RequestOptions
        ///     {
        ///         AccessCondition = new AccessCondition
        ///         {
        ///             Condition = readCopyOfBook.ETag,
        ///             Type = AccessConditionType.IfMatch
        ///         }
        ///      });
        /// ]]>
        /// </code>
        /// </example>
        /// <see cref="Cosmos.AccessCondition"/>
        public AccessCondition AccessCondition { get; set; }

        /// <summary>
        /// Gets or sets the indexing directive (Include or Exclude) for the request in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The indexing directive to use with a request.
        /// </value>
        /// <example>
        /// The following example shows how to explicitly index a document in a collection with
        /// automatic indexing turned off.
        /// <code language="c#">
        /// <![CDATA[
        /// client.CreateDocumentAsync(defaultCollection.SelfLink,
        ///     new { id = "AndersenFamily", isRegistered = true },
        ///     new RequestOptions { IndexingDirective = IndexingDirective.Include });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.IndexingPolicy"/>
        /// <seealso cref="IndexingDirective"/>
        public IndexingDirective? IndexingDirective { get; set; }

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
        /// <example>
        /// This example uses RequestOptions to override the consistency level to Eventual for this single Read operation.
        /// <code language="c#">
        /// <![CDATA[
        /// Document doc = client.ReadDocumentAsync(documentLink, new RequestOptions { ConsistencyLevel = ConsistencyLevel.Eventual });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="ConsistencyLevel"/>
        public ConsistencyLevel? ConsistencyLevel { get; set; }

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
        ///
        /// <example>
        /// This example shows how you can retrieve the SessionToken from a <see cref="ResourceResponse{T}"/>
        /// and then use it on a different instance of <see cref="DocumentClient"/> within <see cref="RequestOptions"/>
        /// This example assumes that the each instance of the client is running from code within a different AppDomain, such as on different nodes in the case of multiple node web application
        /// <code language="c#">
        /// <![CDATA[
        /// string sessionToken;
        /// string docSelfLink;
        ///
        /// using (DocumentClient client = new DocumentClient(new Uri(""), ""))
        /// {
        ///     ResourceResponse<Document> response = client.CreateDocumentAsync(collection.SelfLink, new { id = "an id", value = "some value" }).Result;
        ///     sessionToken = response.SessionToken;
        ///     Document created = response.Resource;
        ///     docSelfLink = created.SelfLink;
        /// }
        ///
        /// using (DocumentClient client = new DocumentClient(new Uri(""), ""))
        /// {
        ///     ResourceResponse<Document> read = client.ReadDocumentAsync(docSelfLink, new RequestOptions { SessionToken = sessionToken }).Result;
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="ConsistencyLevel"/>
        public string SessionToken { get; set; }

        /// <summary>
        /// Gets or sets the expiry time for resource token. Used when creating/updating/reading permissions in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The expiry time in seconds for the resource token.
        /// </value>
        /// <remarks>
        /// When working with Azure Cosmos DB Users and Permissions, the way to instantiate an instance of <see cref="DocumentClient"/> is to
        /// get the <see cref="Internal.Permission.Token"/> for the resource the <see cref="Internal.User"/> wants to access and pass this
        /// to the authKeyOrResourceToken parameter of <see cref="DocumentClient"/> constructor
        /// <para>
        /// When requesting this Token, a RequestOption for ResourceTokenExpirySeconds can be used to set the length of time to elapse before the token expires.
        /// This value can range from 10 seconds, to 5 hours (or 18,000 seconds)
        /// The default value for this, should none be supplied is 1 hour (or 3,600 seconds).
        /// </para>
        /// </remarks>
        /// <seealso cref="DocumentClient"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Permission"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.User"/>
        public int? ResourceTokenExpirySeconds { get; set; }

        /// <summary>
        /// Gets or sets the offer type for the resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The offer type value.
        /// </value>
        /// <remarks>
        /// This option is only valid when creating a document collection.
        /// <para>
        /// Refer to http://azure.microsoft.comdocumentation/articles/documentdb-performance-levels/ for the list of valid
        /// offer types.
        /// </para>
        /// </remarks>
        /// <example>
        /// The followng example shows how to create a collection with the S2 offer.
        /// <code language="c#">
        /// <![CDATA[
        /// await client.CreateDocumentCollectionAsync(
        ///     database.SelfLink,
        ///     new DocumentCollection { Id = "newcoll" },
        ///     new RequestOptions { OfferType = "S2" });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="CosmosContainerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Offer"/>
        public string OfferType { get; set; }

        /// <summary>
        /// Gets or sets the offer throughput provisioned for a collection in measurement of Requests-per-Unit in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The provisioned throughput for this offer.
        /// </value>
        /// <remarks>
        /// This option is only valid when creating a document collection.
        /// <para>
        /// Refer to http://azure.microsoft.com/documentation/articles/documentdb-performance-levels/ for details on provision offer throughput.
        /// </para>
        /// </remarks>
        /// <example>
        /// The following example shows how to create a collection with offer throughput.
        /// <code language="c#">
        /// <![CDATA[
        /// await client.CreateDocumentCollectionAsync(
        ///     database.SelfLink,
        ///     new DocumentCollection { Id = "newcoll" },
        ///     new RequestOptions { OfferThroughput = 50000 });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="CosmosContainerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Offer"/>
        public int? OfferThroughput { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="PartitionKey"/> for the current request in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Partition key is used to identify the target partition for this request.  It must be set on read and delete
        /// operations for all document requests; create, read, update and delete operations for all document attachment requests;
        /// and execute operation on stored procedures.
        ///
        /// For create and update operations on documents, the partition key is optional.  When absent, the client library will
        /// extract the partition key from the document before sending the request to the server.
        /// </para>
        /// </remarks>
        /// <example>
        /// The following example shows how to read a document in a partitioned collection using <see cref="PartitionKey"/>.
        /// The example assumes the collection is created with a <see cref="PartitionKeyDefinition"/> of the 'id' property in all the documents.
        /// <code language="c#">
        /// <![CDATA[
        /// await client.ReadDocumentAsync(
        ///     document.SelfLink,
        ///     new RequestOptions { PartitionKey = new PartitionKey(document.Id) } );
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="CosmosContainerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.PartitionKeyDefinition"/>
        public PartitionKey PartitionKey { get; set; }

        /// <summary>
        ///  Gets or sets the <see cref="EnableScriptLogging"/> for the current request in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// EnableScriptLogging is used to enable/disable logging in JavaScript stored procedures.
        /// By default script logging is disabled.
        /// The log can also be accessible in response header (x-ms-documentdb-script-log-results).
        /// </para>
        /// </remarks>
        /// <example>
        /// The following example shows how to enable logging in stored procedures using <see cref="EnableScriptLogging"/>.
        /// <code language="c#">
        /// <![CDATA[
        /// var response = await client.ExecuteStoredProcedureAsync(
        ///     document.SelfLink,
        ///     new RequestOptions { EnableScriptLogging = true } );
        /// Console.WriteLine(response.ScriptLog);
        /// ]]>
        /// </code>
        /// To log, use the following in store procedure:
        /// <code language="JavaScript">
        /// <![CDATA[
        /// console.log("This is trace log");
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="StoredProcedureResponse&lt;TValue&gt;.ScriptLog"/>
        public bool EnableScriptLogging { get; set; }

        /// <summary>
        ///  Gets or sets the <see cref="PopulateQuotaInfo"/> for document collection read requests in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// PopulateQuotaInfo is used to enable/disable getting document collection quota related stats for document collection read requests.
        /// </para>
        /// </remarks>
        public bool PopulateQuotaInfo { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="JsonSerializerSettings"/> for the current request used to deserialize the document.
        /// If null, uses the default serializer settings set up in the DocumentClient.
        /// </summary>
        public JsonSerializerSettings JsonSerializerSettings { get; set; }

        /// <summary>
        ///  Gets or sets the <see cref="PopulatePartitionKeyRangeStatistics"/> for document collection read requests in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="PopulatePartitionKeyRangeStatistics"/> is used to enable/disable getting partition key range statistics.
        /// </para>
        /// </remarks>
        /// <example>
        /// For usage, please refer to the example in <see cref="CosmosContainerSettings.PartitionKeyRangeStatistics"/>.
        /// </example>
        public bool PopulatePartitionKeyRangeStatistics { get; set; }

        /// <summary>
        /// Gets or sets the partition key range id for the current request.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Requests can use this to forward request to specific range.
        /// This is useful in case of bulk export scenarios.
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
        /// <seealso cref="CosmosContainerSettings"/>
        internal string PartitionKeyRangeId { get; set; }

        /// <summary>
        /// Gets or sets source databse from which to restore.
        /// To be used along with RemoteStorageType, SourceCollectionId and RestorePointInTime
        /// during Collection creation operation.
        /// </summary>
        internal string SourceDatabaseId { get; set; }

        /// <summary>
        /// Gets or sets source collection from which to restore.
        /// To be used along with RemoteStorageType, SourceDatabaseId and RestorePointInTime
        /// during Collection creation operation.
        /// </summary>
        internal string SourceCollectionId { get; set; }

        /// <summary>
        /// Gets or sets restore point-in-time.
        /// To be used along with RemoteStorageType, SourceDatabaseId, SourceCollectionId
        /// during Collection creation operation.
        /// </summary>
        internal long? RestorePointInTime { get; set; }

        /// <summary>
        ///  Gets or sets the <see cref="PopulateRestoreStatus"/> for document collection read requests in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// PopulateRestoreStatus is used to  retreieve the status of a collection being restored.
        /// </para>
        /// </remarks>
        internal bool PopulateRestoreStatus { get; set; }

        /// <summary>
        /// Gets or sets exclude system properties.
        /// ExcludeSystemProperties indicates whether system properties 
        /// should be excluded from JSON content of a document or not.
        /// </summary>
        internal bool? ExcludeSystemProperties { get; set; }
    }
}
