//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
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
    /// using Microsoft.Azure.Cosmos.Linq;
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
    /// <seealso cref="Microsoft.Azure.Cosmos.IndexingPolicy"/>
    /// <seealso cref="Microsoft.Azure.Cosmos.PartitionKeyDefinition"/>
    /// <seealso cref="CosmosDatabaseSettings"/>
    public class CosmosContainerSettings : CosmosResource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosContainerSettings"/> class for the Azure Cosmos DB service.
        /// </summary>
        public CosmosContainerSettings()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosContainerSettings"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="id">The Id of the resource in the Azure Cosmos service.</param>
        /// <param name="partitionKeyPath">The path to the partition key. Example: /location</param>
        public CosmosContainerSettings(string id, string partitionKeyPath)
        {
            this.Id = id;
            if (!string.IsNullOrEmpty(partitionKeyPath))
            {
                this.PartitionKey = new PartitionKeyDefinition();
                this.PartitionKey.Paths = new Collection<string>() { partitionKeyPath };
            }

            ValidateRequiredProperties();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosContainerSettings"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="id">The Id of the resource in the Azure Cosmos service.</param>
        /// <param name="partitionKeyDefinition">The partition key <see cref="PartitionKeyDefinition"/></param>
        internal CosmosContainerSettings(string id, PartitionKeyDefinition partitionKeyDefinition)
        {
            this.Id = id;
            this.PartitionKey = partitionKeyDefinition;

            ValidateRequiredProperties();
        }

        /// <summary>
        /// Gets the <see cref="IndexingPolicy"/> associated with the collection from the Azure Cosmos DB service. 
        /// </summary>
        /// <value>
        /// The indexing policy associated with the collection.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.IndexingPolicy)]
        public IndexingPolicy IndexingPolicy { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonIgnore]
        public string PartitionKeyPath
        {
            get
            {
                if (this.PartitionKey != null)
                {
                    return null;
                }

                return this.PartitionKey.Paths[0];
            }
        }

        /// <summary>
        /// Gets or sets <see cref="PartitionKeyDefinition"/> object in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// <see cref="PartitionKeyDefinition"/> object.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.PartitionKey)]
        internal PartitionKeyDefinition PartitionKey { get; set; }

        /// <summary>
        /// Gets the default time to live in seconds for item in a container from the Azure Cosmos service.
        /// </summary>
        /// <value>
        /// It is an optional property.
        /// A valid value must be either a nonzero positive timespan or <c>null</c>.
        /// By default, DefaultTimeToLive is set to null meaning the time to live is turned off for the container.
        /// The unit of measurement is seconds. The maximum allowed value is 2147483647.
        /// </value>
        /// <remarks>
        /// <para>
        /// The <see cref="DefaultTimeToLive"/> will be applied to all the items in the container as the default time-to-live policy.
        /// The individual item could override the default time-to-live policy by setting its time to live.
        /// </para>
        /// <para>
        /// When the <see cref="DefaultTimeToLive"/> is <c>null</c>, the time-to-live will be turned off for the container.
        /// It means all the items will never expire. The individual item's  time to live will be disregarded.
        /// </para>
        /// <para>
        /// When the <see cref="DefaultTimeToLive"/> is '-1', the time-to-live will be turned on for the container.
        /// By default, all the items will never expire. The individual item could be given a specific time-to-live value by setting its
        /// time to live. The item's time to live will be honored, and the expired items
        /// will be deleted in background.
        /// </para>
        /// <para>
        /// When the <see cref="DefaultTimeToLive"/> is a nonzero positive integer, the time-to-live will be turned on for the container.
        /// And a default time-to-live in seconds will be applied to all the items. A item will be expired after the
        /// specified <see cref="DefaultTimeToLive"/> value in seconds since its last write time.
        /// </para>
        /// </remarks>
        /// <example>
        /// The example below disables time-to-live on a container.
        /// <code language="c#">
        /// <![CDATA[
        ///     container.DefaultTimeToLive = null;
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// The example below enables time-to-live on a container. By default, all the items never expire.
        /// <code language="c#">
        /// <![CDATA[
        ///     container.DefaultTimeToLive = TimeSpan.FromDays(2);
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// The example below enables time-to-live on a container. By default, the item will expire after 1000 seconds
        /// since its last write time.
        /// <code language="c#">
        /// <![CDATA[
        ///     container.DefaultTimeToLive = TimeSpan.FromSeconds(1000);
        /// ]]>
        /// </code>
        /// </example>
        public TimeSpan? DefaultTimeToLive
        {
            get
            {
                int? seconds = this.InternalTimeToLive;
                return seconds != null && seconds.HasValue ? TimeSpan.FromSeconds(seconds.Value) : (TimeSpan?)null;
            }
            set
            {
                this.InternalTimeToLive = value != null && value.HasValue ? (int?)value.Value.TotalSeconds : (int?)null;
            }
        }

        /// <summary>
        /// Internal property used as a helper to convert to the back-end type int?
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.DefaultTimeToLive, NullValueHandling = NullValueHandling.Ignore)]
        internal int? InternalTimeToLive { get; set; }

        /// <summary>
        /// Gets the <see cref="SchemaDiscoveryPolicy"/> associated with the collection. 
        /// </summary>
        /// <value>
        /// The schema discovery policy associated with the collection.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.SchemaDiscoveryPolicy)]
        internal SchemaDiscoveryPolicy SchemaDiscoveryPolicy { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="UniqueKeyPolicy"/> that guarantees uniqueness of documents in collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.UniqueKeyPolicy)]
        public UniqueKeyPolicy UniqueKeyPolicy { get; set; }

        /// <summary>
        /// Throws an exception if an invalid id or partition key is set.
        /// </summary>
        internal void ValidateRequiredProperties()
        {
            if (this.Id == null)
            {
                throw new ArgumentNullException(nameof(Id));
            }

            if (this.PartitionKey == null)
            {
                throw new ArgumentNullException(nameof(PartitionKey));
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="ConflictResolutionPolicy"/> that is used for resolving conflicting writes on documents in different regions, in a collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.ConflictResolutionPolicy)]
        internal ConflictResolutionPolicy ConflictResolutionPolicy { get; set; }

        /// <summary>
        /// Gets a collection of <see cref="PartitionKeyRangeStatistics"/> object in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// <see cref="PartitionKeyRangeStatistics"/> object.
        /// </value>
        /// <remarks>
        /// This is reported based on a sub-sampling of partition keys within the collection and hence these are approximate. 
        /// If your partition keys are below 1GB of storage, they may not show up in the reported statistics.
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
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions.PopulatePartitionKeyRangeStatistics"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.PartitionKeyStatistics"/>
        [JsonIgnore]
        internal IReadOnlyList<PartitionKeyRangeStatistics> PartitionKeyRangeStatistics
        {
            get
            {
                var list = (this.StatisticsJRaw ?? new Collection<JRaw>())
                    .Where(jraw => jraw != null)
                    .Select(jraw => JsonConvert.DeserializeObject<PartitionKeyRangeStatistics>((string)jraw.Value))
                    .ToList();

                return new JsonSerializableList<PartitionKeyRangeStatistics>(list);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.Statistics)]
        internal IReadOnlyList<JRaw> StatisticsJRaw { get; set; }

        /// <summary>
        /// Gets the <see cref="ChangeFeedPolicy"/> associated with the collection from the Azure Cosmos DB service. 
        /// </summary>
        /// <value>
        /// The change feed policy associated with the collection.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.ChangeFeedPolicy)]
        internal ChangeFeedPolicy ChangeFeedPolicy { get; set; }
    }
}
