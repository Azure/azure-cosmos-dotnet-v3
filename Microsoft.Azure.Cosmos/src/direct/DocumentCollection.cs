//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic; 
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents a document collection in the Azure Cosmos DB service. A collection is a named logical container for documents. 
    /// </summary>
    /// <remarks>
    /// A database may contain zero or more named collections and each collection consists of zero or more JSON documents. 
    /// Being schema-free, the documents in a collection do not need to share the same structure or fields. Since collections are application resources, 
    /// they can be authorized using either the master key or resource keys.
    /// Refer to <see>http://azure.microsoft.com/documentation/articles/documentdb-resources/#collections</see> for more details on collections.
    /// </remarks>
    /// <example>
    /// The example below creates a new partitioned collection with 50000 Request-per-Unit throughput.
    /// The partition key is the first level 'country' property in all the documents within this collection.
    /// <code language="c#">
    /// <![CDATA[
    /// DocumentCollection collection = await client.CreateDocumentCollectionAsync(
    ///     databaseLink,
    ///     new DocumentCollection 
    ///     { 
    ///         Id = "MyCollection",
    ///         PartitionKey = new PartitionKeyDefinition
    ///         {
    ///             Paths = new Collection<string> { "/country" }
    ///         }
    ///     }, 
    ///     new RequestOptions { OfferThroughput = 50000} ).Result;
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// The example below creates a new collection with OfferThroughput set to 10000.
    /// <code language="c#">
    /// <![CDATA[
    /// DocumentCollection collection = await client.CreateDocumentCollectionAsync(
    ///     databaseLink,
    ///     new DocumentCollection { Id = "MyCollection" }, 
    ///     new RequestOptions { OfferThroughput = 10000} ).Result;
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// The example below creates a new collection with a custom indexing policy.
    /// <code language="c#">
    /// <![CDATA[
    /// DocumentCollection collectionSpec = new DocumentCollection { Id ="MyCollection" };
    /// collectionSpec.IndexingPolicy.Automatic = true;
    /// collectionSpec.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
    /// collection = await client.CreateDocumentCollectionAsync(database.SelfLink, collectionSpec);
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// The example below creates a document of type Book inside this collection.
    /// <code language="c#">
    /// <![CDATA[
    /// Document doc = await client.CreateDocumentAsync(collection.SelfLink, new Book { Title = "War and Peace" });
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// The example below queries for a Database by Id to retrieve the SelfLink.
    /// <code language="c#">
    /// <![CDATA[
    /// using Microsoft.Azure.Documents.Linq;
    /// DocumentCollection collection = client.CreateDocumentCollectionQuery(databaseLink).Where(c => c.Id == "myColl").AsEnumerable().FirstOrDefault();
    /// string collectionLink = collection.SelfLink;
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// The example below deletes this collection.
    /// <code language="c#">
    /// <![CDATA[
    /// await client.DeleteDocumentCollectionAsync(collection.SelfLink);
    /// ]]>
    /// </code>
    /// </example>
    /// <seealso cref="Microsoft.Azure.Documents.IndexingPolicy"/>
    /// <seealso cref="Microsoft.Azure.Documents.PartitionKeyDefinition"/>
    /// <seealso cref="Microsoft.Azure.Documents.Document"/>
    /// <seealso cref="Microsoft.Azure.Documents.Database"/>
    /// <seealso cref="Microsoft.Azure.Documents.Offer"/>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    class DocumentCollection : Resource
    {
        private IndexingPolicy indexingPolicy;
        private GeospatialConfig geospatialConfig;
        private PartitionKeyDefinition partitionKey;
        private SchemaDiscoveryPolicy schemaDiscoveryPolicy;
        private UniqueKeyPolicy uniqueKeyPolicy;
        private ConflictResolutionPolicy conflictResolutionPolicy;
        private ChangeFeedPolicy changeFeedPolicy;
        private CollectionBackupPolicy collectionBackupPolicy;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentCollection"/> class for the Azure Cosmos DB service.
        /// </summary>
        public DocumentCollection()
        {
        }

        /// <summary>
        /// Gets the <see cref="IndexingPolicy"/> associated with the collection from the Azure Cosmos DB service. 
        /// </summary>
        /// <value>
        /// The indexing policy associated with the collection.
        /// </value>
        public IndexingPolicy IndexingPolicy
        {
            get
            {
                if (this.indexingPolicy == null)
                {
                    this.indexingPolicy = base.GetObject<IndexingPolicy>(Constants.Properties.IndexingPolicy) ?? new IndexingPolicy();
                }

                return this.indexingPolicy;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, "IndexingPolicy"));
                }

                this.indexingPolicy = value;
                base.SetObject<IndexingPolicy>(Constants.Properties.IndexingPolicy, value);
            }
        }

        /// <summary>
        /// Gets the <see cref="GeospatialConfig"/> associated with the collection from the Azure Cosmos DB service. 
        /// </summary>
        /// <value>
        /// Geospatial type of collection i.e. geography or geometry 
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.GeospatialConfig)]
        public GeospatialConfig GeospatialConfig
        {
            get
            {
                if (this.geospatialConfig == null)
                {
                    this.geospatialConfig = base.GetObject<GeospatialConfig>(Constants.Properties.GeospatialConfig) ?? new GeospatialConfig();
                }

                return this.geospatialConfig;
            }
            set
            {
                this.geospatialConfig = value;
                base.SetObject<GeospatialConfig>(Constants.Properties.GeospatialConfig, value);
            }
        }

        /// <summary>
        /// Gets the self-link for documents in a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The self-link for documents in a collection.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.DocumentsLink)]
        public string DocumentsLink
        {
            get
            {
                return this.SelfLink.TrimEnd('/') + "/" + base.GetValue<string>(Constants.Properties.DocumentsLink);
            }
        }

        /// <summary>
        /// Gets the self-link for stored procedures in a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The self-link for stored procedures in a collection.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.StoredProceduresLink)]
        public string StoredProceduresLink
        {
            get
            {
                return this.SelfLink.TrimEnd('/') + "/" + base.GetValue<string>(Constants.Properties.StoredProceduresLink);
            }
        }

        /// <summary>
        /// Gets the self-link for triggers in a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The self-link for triggers in a collection.
        /// </value>
        public string TriggersLink
        {
            get
            {
                return this.SelfLink.TrimEnd('/') + "/" + base.GetValue<string>(Constants.Properties.TriggersLink);
            }
        }

        /// <summary>
        /// Gets the self-link for user defined functions in a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The self-link for user defined functions in a collection.
        /// </value>
        public string UserDefinedFunctionsLink
        {
            get
            {
                return this.SelfLink.TrimEnd('/') + "/" + base.GetValue<string>(Constants.Properties.UserDefinedFunctionsLink);
            }
        }

        /// <summary>
        /// Gets the self-link for conflicts in a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The self-link for conflicts in a collection.
        /// </value>
        public string ConflictsLink
        {
            get
            {
                return this.SelfLink.TrimEnd('/') + "/" + base.GetValue<string>(Constants.Properties.ConflictsLink);
            }
        }

        /// <summary>
        /// Gets or sets <see cref="PartitionKeyDefinition"/> object in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// <see cref="PartitionKeyDefinition"/> object.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.PartitionKey)]
        public PartitionKeyDefinition PartitionKey
        {
            get
            {
                // Thread safe lazy initialization for case when collection is cached (and is basically readonly).
                if (this.partitionKey == null)
                {
                    this.partitionKey = base.GetValue<PartitionKeyDefinition>(Constants.Properties.PartitionKey) ?? new PartitionKeyDefinition();
                }

                return this.partitionKey;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, "PartitionKey"));
                }

                this.partitionKey = value;
                base.SetValue(Constants.Properties.PartitionKey, this.partitionKey);
            }
        }

        /// <summary>
        /// Gets the default time to live in seconds for documents in a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// It is an optional property.
        /// A valid value must be either a nonzero positive integer, '-1', or <c>null</c>.
        /// By default, DefaultTimeToLive is set to null meaning the time to live is turned off for the collection.
        /// The unit of measurement is seconds. The maximum allowed value is 2147483647.
        /// </value>
        /// <remarks>
        /// <para>
        /// The <see cref="DefaultTimeToLive"/> will be applied to all the documents in the collection as the default time-to-live policy.
        /// The individual document could override the default time-to-live policy by setting its <see cref="Document.TimeToLive"/>.
        /// </para>
        /// <para>
        /// When the <see cref="DefaultTimeToLive"/> is <c>null</c>, the time-to-live will be turned off for the collection.
        /// It means all the documents will never expire. The individual document's <see cref="Document.TimeToLive"/> will be disregarded.
        /// </para>
        /// <para>
        /// When the <see cref="DefaultTimeToLive"/> is '-1', the time-to-live will be turned on for the collection.
        /// By default, all the documents will never expire. The individual document could be given a specific time-to-live value by setting its
        /// <see cref="Document.TimeToLive"/>. The document's <see cref="Document.TimeToLive"/> will be honored, and the expired documents
        /// will be deleted in background.
        /// </para>
        /// <para>
        /// When the <see cref="DefaultTimeToLive"/> is a nonzero positive integer, the time-to-live will be turned on for the collection.
        /// And a default time-to-live in seconds will be applied to all the documents. A document will be expired after the
        /// specified <see cref="DefaultTimeToLive"/> value in seconds since its last write time.
        /// The individual document could override the default time-to-live policy by setting its <see cref="Document.TimeToLive"/>.
        /// Please refer to the <see cref="Document.TimeToLive"/> for more details about evaluating the final time-to-live policy of a document.
        /// </para>
        /// </remarks>
        /// <example>
        /// The example below disables time-to-live on a collection.
        /// <code language="c#">
        /// <![CDATA[
        ///     collection.DefaultTimeToLive = null;
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// The example below enables time-to-live on a collection. By default, all the documents never expire.
        /// <code language="c#">
        /// <![CDATA[
        ///     collection.DefaultTimeToLive = -1;
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// The example below enables time-to-live on a collection. By default, the document will expire after 1000 seconds
        /// since its last write time.
        /// <code language="c#">
        /// <![CDATA[
        ///     collection.DefaultTimeToLive = 1000;
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        [JsonProperty(PropertyName = Constants.Properties.DefaultTimeToLive, NullValueHandling = NullValueHandling.Ignore)]
        public int? DefaultTimeToLive
        {
            get
            {
                return base.GetValue<int?>(Constants.Properties.DefaultTimeToLive);
            }
            set
            {
                base.SetValue(Constants.Properties.DefaultTimeToLive, value);
            }
        }

        /// <summary>
        /// Gets or sets the time to live base timestamp property path.
        /// </summary>
        /// <value>
        /// It is an optional property.
        /// This property should be only present when DefaultTimeToLive is set. When this property is present, time to live
        /// for a document is decided based on the value of this property in document.
        /// By default, TimeToLivePropertyPath is set to null meaning the time to live is based on the _ts property in document.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.TimeToLivePropertyPath, NullValueHandling = NullValueHandling.Ignore)]
        public string TimeToLivePropertyPath
        {
            get
            {
                return this.GetValue<string>(Constants.Properties.TimeToLivePropertyPath);
            }
            set
            {
                this.SetValue(Constants.Properties.TimeToLivePropertyPath, value);
            }
        }

        /// <summary>
        /// Gets the <see cref="SchemaDiscoveryPolicy"/> associated with the collection. 
        /// </summary>
        /// <value>
        /// The schema discovery policy associated with the collection.
        /// </value>
        internal SchemaDiscoveryPolicy SchemaDiscoveryPolicy
        {
            get
            {
                // Thread safe lazy initialization for case when collection is cached (and is basically readonly).
                if (this.schemaDiscoveryPolicy == null)
                {
                    this.schemaDiscoveryPolicy = base.GetObject<SchemaDiscoveryPolicy>(Constants.Properties.SchemaDiscoveryPolicy) ?? new SchemaDiscoveryPolicy();
                }

                return this.schemaDiscoveryPolicy;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, "SchemaDiscoveryPolicy"));
                }

                this.schemaDiscoveryPolicy = value;
                base.SetObject<SchemaDiscoveryPolicy>(Constants.Properties.SchemaDiscoveryPolicy, value);
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="UniqueKeyPolicy"/> that guarantees uniqueness of documents in collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.UniqueKeyPolicy)]
        public UniqueKeyPolicy UniqueKeyPolicy
        {
            get
            {
                // Thread safe lazy initialization for case when collection is cached (and is basically readonly).
                if (this.uniqueKeyPolicy == null)
                {
                    this.uniqueKeyPolicy = base.GetObject<UniqueKeyPolicy>(Constants.Properties.UniqueKeyPolicy) ?? new UniqueKeyPolicy();
                }

                return this.uniqueKeyPolicy;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, "UniqueKeyPolicy"));
                }

                this.uniqueKeyPolicy = value;
                base.SetObject<UniqueKeyPolicy>(Constants.Properties.UniqueKeyPolicy, value);
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="ConflictResolutionPolicy"/> that is used for resolving conflicting writes on documents in different regions, in a collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.ConflictResolutionPolicy)]
        public ConflictResolutionPolicy ConflictResolutionPolicy
        {
            get
            {
                // Thread safe lazy initialization for case when collection is cached (and is basically readonly).
                if (this.conflictResolutionPolicy == null)
                {
                    this.conflictResolutionPolicy = base.GetObject<ConflictResolutionPolicy>(Constants.Properties.ConflictResolutionPolicy) ?? new ConflictResolutionPolicy();
                }

                return this.conflictResolutionPolicy;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, "ConflictResolutionPolicy"));
                }

                this.conflictResolutionPolicy = value;
                base.SetObject<ConflictResolutionPolicy>(Constants.Properties.ConflictResolutionPolicy, value);
            }
        }

        /// <summary>
        /// Gets or sets the PartitionKeyDeleteThroughputFraction for the collection.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.PartitionKeyDeleteThroughputFraction)]
        public double PartitionKeyDeleteThroughputFraction
        {
            get
            {
                return this.GetValue<double>(Constants.Properties.PartitionKeyDeleteThroughputFraction);
            }
            set
            {
                this.SetValue(Constants.Properties.PartitionKeyDeleteThroughputFraction, value);
            }
        }

        /// <summary>
        /// Gets a collection of <see cref="PartitionKeyRangeStatistics"/> object in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// <see cref="PartitionKeyRangeStatistics"/> object.
        /// </value>
        /// <remarks>
        /// This is reported based on a sub-sampling of partition keys within the collection and hence these are approximate. If your partition keys are below 1GB of storage, they may not show up in the reported statistics.
        /// </remarks>
        /// <example>
        /// The following code shows how to log statistics for all partition key ranges as a string:
        /// <code language="c#">
        /// <![CDATA[
        /// var collection = await client.ReadDocumentCollectionAsync(
        ///     collectionUri,
        ///     new RequestOptions { PopulatePartitionKeyRangeStatistics = true } );
        /// 
        /// Console.WriteLine(collection.PartitionKeyRangeStatistics.ToString());
        /// ]]>
        /// </code>
        /// To log individual partition key range statistics, use the following code:
        /// <code language="c#">
        /// <![CDATA[
        /// var collection = await client.ReadDocumentCollectionAsync(
        ///     collectionUri,
        ///     new RequestOptions { PopulatePartitionKeyRangeStatistics = true } );
        ///     
        /// foreach(var partitionKeyRangeStatistics in collection.PartitionKeyRangeStatistics)
        /// {
        ///     Console.WriteLine(partitionKeyRangeStatistics.PartitionKeyRangeId);
        ///     Console.WriteLine(partitionKeyRangeStatistics.DocumentCount);
        ///     Console.WriteLine(partitionKeyRangeStatistics.SizeInKB);
        ///     
        ///     foreach(var partitionKeyStatistics in partitionKeyRangeStatistics.PartitionKeyStatistics)
        ///     {
        ///         Console.WriteLine(partitionKeyStatistics.PartitionKey);
        ///         Console.WriteLine(partitionKeyStatistics.SizeInKB);
        ///     }
        ///  }
        /// ]]>
        /// </code>
        /// The output will look something like that:
        /// "statistics": [
        /// {"id":"0","sizeInKB":1410184,"documentCount":42807,"partitionKeys":[]},
        /// {"id":"1","sizeInKB":3803113,"documentCount":150530,"partitionKeys":[{"partitionKey":["4009696"],"sizeInKB":3731654}]},
        /// {"id":"2","sizeInKB":1447855,"documentCount":59056,"partitionKeys":[{"partitionKey":["4009633"],"sizeInKB":2861210},{"partitionKey":["4004207"],"sizeInKB":2293163}]},
        /// {"id":"3","sizeInKB":1026254,"documentCount":44241,"partitionKeys":[]},
        /// {"id":"4","sizeInKB":3250973,"documentCount":124959,"partitionKeys":[]}
        /// ]
        /// </example>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions.PopulatePartitionKeyRangeStatistics"/>
        /// <seealso cref="Microsoft.Azure.Documents.PartitionKeyStatistics"/>
        [JsonIgnore]
        public IReadOnlyList<PartitionKeyRangeStatistics> PartitionKeyRangeStatistics
        {
            get
            {
                List<PartitionKeyRangeStatistics> list = this.StatisticsJRaw
                    .Where(jraw => jraw != null)
                    .Select(jraw => JsonConvert.DeserializeObject<PartitionKeyRangeStatistics>((string)jraw.Value))
                    .ToList();

                return new JsonSerializableList<PartitionKeyRangeStatistics>(list);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.Statistics)]
        internal IReadOnlyList<JRaw> StatisticsJRaw
        {
            get
            {
                return base.GetValue<IReadOnlyList<JRaw>>(Constants.Properties.Statistics) ?? new Collection<JRaw>();
            }
            set
            {
                base.SetValue(Constants.Properties.Statistics, value);
            }
        }

        internal bool HasPartitionKey
        {
            get
            {
                if (this.partitionKey != null)
                {
                    return true;
                }

                return base.GetValue<object>(Constants.Properties.PartitionKey) != null;
            }
        }

        /// <summary>
        /// Instantiates a new instance of the <see cref="PartitionKeyInternal"/> object.
        /// </summary>
        /// <remarks>
        /// The function selects the right partition key constant for inserting documents that don't have
        /// a value for partition key. The constant selection is based on whether the collection is migrated
        /// or user partitioned
        /// </remarks>
        internal PartitionKeyInternal NonePartitionKeyValue
        {
            get
            {
                if (this.PartitionKey.Paths.Count == 0 || this.PartitionKey.IsSystemKey.GetValueOrDefault(false))
                {
                    return PartitionKeyInternal.Empty;
                }
                else
                {
                    return PartitionKeyInternal.Undefined;
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="ChangeFeedPolicy"/> associated with the collection from the Azure Cosmos DB service. 
        /// </summary>
        /// <value>
        /// The change feed policy associated with the collection.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.ChangeFeedPolicy, DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        internal ChangeFeedPolicy ChangeFeedPolicy
        {
            get
            {
                if (this.changeFeedPolicy == null)
                {
                    this.changeFeedPolicy = base.GetObject<ChangeFeedPolicy>(Constants.Properties.ChangeFeedPolicy);
                }

                return this.changeFeedPolicy;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, "ChangeFeedPolicy"));
                }

                this.changeFeedPolicy = value;
                base.SetObject<ChangeFeedPolicy>(Constants.Properties.ChangeFeedPolicy, value);
            }
        }

        /// <summary>
        /// Gets the analytical storage time to live in seconds for documents in a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// It is an optional property.
        /// A valid value must be either a nonzero positive integer, '-1', or 0.
        /// By default, AnalyticalStorageTimeToLive is set to 0 meaning the analytical store is turned off for the collection; -1 means documents
        /// in analytical store never expire.
        /// The unit of measurement is seconds. The maximum allowed value is 2147483647.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.AnalyticalStorageTimeToLive, NullValueHandling = NullValueHandling.Ignore)]
        internal int? AnalyticalStorageTimeToLive
        {
            get
            {
                return base.GetValue<int?>(Constants.Properties.AnalyticalStorageTimeToLive);
            }
            set
            {
                base.SetValue(Constants.Properties.AnalyticalStorageTimeToLive, value);
            }
        }

        /// <summary>
        /// Gets if creation of MaterializedViews is allowed on the collection.
        /// </summary>
        /// <value>
        /// It is an optional property.
        /// A valid value must be either true or false
        /// By default, AllowMaterializedViews is set to false meaning the creation of materializedViews is not allowed on collection;
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.AllowMaterializedViews, NullValueHandling = NullValueHandling.Ignore)]
        internal bool? AllowMaterializedViews
        {
            get
            {
                return base.GetValue<bool?>(Constants.Properties.AllowMaterializedViews);
            }
            set
            {
                base.SetValue(Constants.Properties.AllowMaterializedViews, value);
            }
        }

        /// <summary>
        /// Gets the <see cref="CollectionBackupPolicy"/> associated with the collection from the Azure Cosmos DB service. 
        /// </summary>
        /// <value>
        /// The backup policy associated with the collection.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.CollectionBackupPolicy, DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        internal CollectionBackupPolicy CollectionBackupPolicy
        {
            get
            {
                if (this.collectionBackupPolicy == null)
                {
                    this.collectionBackupPolicy = base.GetObject<CollectionBackupPolicy>(Constants.Properties.CollectionBackupPolicy);
                }

                return this.collectionBackupPolicy;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, "CollectionBackupPolicy"));
                }

                this.collectionBackupPolicy = value;
                base.SetObject<CollectionBackupPolicy>(Constants.Properties.CollectionBackupPolicy, value);
            }
        }

        /// <summary>
        /// Gets or sets the schema policy on collection in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// The schema policy here currently is just a raw JSON. Bringing full object model
        /// (see Product\Cosmos\Compute\Core\InteropClient\SchemaPolicy.cs) is not currenlty needed.
        /// </remarks>
        /// <example>
        /// string schemaPolicy = ...;
        /// collection.SchemaPolicy = new JRaw(schemaPolicy);
        /// collection.InternalSchemaProperties = new InternalSchemaProperties { UseSchemaForAnalyticsOnly = true };
        /// </example>
        [JsonProperty(PropertyName = Constants.Properties.SchemaPolicy, NullValueHandling = NullValueHandling.Ignore)]
        internal JRaw SchemaPolicy
        {
            get
            {
                return base.GetValue<JRaw>(Constants.Properties.SchemaPolicy);
            }
            set
            {
                base.SetValue(Constants.Properties.SchemaPolicy, value);
            }
        }

        /// <summary>
        /// Gets or sets the schema policy on collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.InternalSchemaProperties, NullValueHandling = NullValueHandling.Ignore)]
        internal InternalSchemaProperties InternalSchemaProperties
        {
            get
            {
                return base.GetValue<InternalSchemaProperties>(Constants.Properties.InternalSchemaProperties);
            }
            set
            {
                base.SetValue(Constants.Properties.InternalSchemaProperties, value);
            }
        }

        internal override void Validate()
        {
            base.Validate();
            base.GetValue<int?>(Constants.Properties.DefaultTimeToLive);
            base.GetValue<string>(Constants.Properties.TimeToLivePropertyPath);
            this.IndexingPolicy.Validate();
            this.PartitionKey.Validate();
            this.UniqueKeyPolicy.Validate();
            this.ConflictResolutionPolicy.Validate();
        }

        internal override void OnSave()
        {
            if (this.indexingPolicy != null)
            {
                this.indexingPolicy.OnSave();
                base.SetObject(Constants.Properties.IndexingPolicy, this.indexingPolicy);
            }

            if (this.partitionKey != null)
            {
                base.SetValue(Constants.Properties.PartitionKey, this.partitionKey);
            }

            if (this.uniqueKeyPolicy != null)
            {
                this.uniqueKeyPolicy.OnSave();
                base.SetObject(Constants.Properties.UniqueKeyPolicy, this.uniqueKeyPolicy);
            }

            if (this.conflictResolutionPolicy != null)
            {
                this.conflictResolutionPolicy.OnSave();
                base.SetObject(Constants.Properties.ConflictResolutionPolicy, this.conflictResolutionPolicy);
            }

            if (this.schemaDiscoveryPolicy != null)
            {
                base.SetObject(Constants.Properties.SchemaDiscoveryPolicy, this.schemaDiscoveryPolicy);
            }
            
            if (this.changeFeedPolicy != null)
            {
                base.SetObject(Constants.Properties.ChangeFeedPolicy, this.changeFeedPolicy);
            }

            if (this.collectionBackupPolicy != null)
            {
                base.SetObject(Constants.Properties.CollectionBackupPolicy, this.collectionBackupPolicy);
            }

            if (this.geospatialConfig != null)
            {
                base.SetObject(Constants.Properties.GeospatialConfig, this.geospatialConfig);
            }
        }
    }
}
