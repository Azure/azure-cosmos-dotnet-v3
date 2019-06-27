//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a document container in the Azure Cosmos DB service. A container is a named logical container for documents. 
    /// </summary>
    /// <remarks>
    /// A database may contain zero or more named containers and each container consists of zero or more JSON documents. 
    /// Being schema-free, the documents in a container do not need to share the same structure or fields. Since containers are application resources, 
    /// they can be authorized using either the master key or resource keys.
    /// Refer to <see>http://azure.microsoft.com/documentation/articles/documentdb-resources/#collections</see> for more details on containers.
    /// </remarks>
    /// <example>
    /// The example below creates a new partitioned container with 50000 Request-per-Unit throughput.
    /// The partition key is the first level 'country' property in all the documents within this container.
    /// <code language="c#">
    /// <![CDATA[
    ///     Container container = await client.GetDatabase("dbName"].Containers.CreateAsync("MyCollection", "/country", 50000} );
    ///     ContainerProperties containerProperties = container.Resource;
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// The example below creates a new container with a custom indexing policy.
    /// <code language="c#">
    /// <![CDATA[
    ///     ContainerProperties containerProperties = new ContainerProperties("MyCollection", "/country");
    ///     containerProperties.IndexingPolicy.Automatic = true;
    ///     containerProperties.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
    ///     
    ///     CosmosContainerResponse containerCreateResponse = await client.GetDatabase("dbName"].CreateContainerAsync(containerProperties, 50000);
    ///     ContainerProperties createdContainerProperties = containerCreateResponse.Container;
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// The example below deletes this container.
    /// <code language="c#">
    /// <![CDATA[
    ///     Container container = client.GetDatabase("dbName"].Containers["MyCollection"];
    ///     await container.DeleteAsync();
    /// ]]>
    /// </code>
    /// </example>
    /// <seealso cref="Microsoft.Azure.Cosmos.IndexingPolicy"/>
    /// <seealso cref="Microsoft.Azure.Cosmos.UniqueKeyPolicy"/>
    public class ContainerProperties
    {
        private static readonly char[] partitionKeyTokenDelimeter = new char[] { '/' };

        [JsonProperty(PropertyName = Constants.Properties.IndexingPolicy, NullValueHandling = NullValueHandling.Ignore)]
        private IndexingPolicy indexingPolicyInternal;

        [JsonProperty(PropertyName = Constants.Properties.UniqueKeyPolicy, NullValueHandling = NullValueHandling.Ignore)]
        private UniqueKeyPolicy uniqueKeyPolicyInternal;

        [JsonProperty(PropertyName = Constants.Properties.ConflictResolutionPolicy, NullValueHandling = NullValueHandling.Ignore)]
        private ConflictResolutionPolicy conflictResolutionInternal;

        private string[] partitionKeyPathTokens;
        private string id;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContainerProperties"/> class for the Azure Cosmos DB service.
        /// </summary>
        public ContainerProperties()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContainerProperties"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="id">The Id of the resource in the Azure Cosmos service.</param>
        /// <param name="partitionKeyPath">The path to the partition key. Example: /location</param>
        public ContainerProperties(string id, string partitionKeyPath)
        {
            this.Id = id;
            this.PartitionKeyPath = partitionKeyPath;

            ValidateRequiredProperties();
        }

        /// <summary>
        /// Gets the Partitioning scheme version used. <see cref="Cosmos.PartitionKeyDefinitionVersion"/>
        /// </summary>
        [JsonIgnore]
        public PartitionKeyDefinitionVersion? PartitionKeyDefinitionVersion
        {
            get => (Cosmos.PartitionKeyDefinitionVersion?)this.PartitionKey?.Version;

            set
            {
                if (this.PartitionKey == null)
                {
                    throw new ArgumentOutOfRangeException($"PartitionKey is not defined for container");
                }

                this.PartitionKey.Version = (Documents.PartitionKeyDefinitionVersion)value;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="ConflictResolutionPolicy" />
        /// </summary>
        [JsonIgnore]
        public ConflictResolutionPolicy ConflictResolutionPolicy
        {
            get
            {
                if (this.conflictResolutionInternal == null)
                {
                    this.conflictResolutionInternal = new ConflictResolutionPolicy();
                }

                return this.conflictResolutionInternal;
            }

            set => this.conflictResolutionInternal = value ?? throw new ArgumentNullException($"{nameof(value)}");
        }

        /// <summary>
        /// Gets or sets the Id of the resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The Id associated with the resource.</value>
        /// <remarks>
        /// <para>
        /// Every resource within an Azure Cosmos DB database account needs to have a unique identifier. 
        /// Unlike <see cref="Resource.ResourceId"/>, which is set internally, this Id is settable by the user and is not immutable.
        /// </para>
        /// <para>
        /// When working with document resources, they too have this settable Id property. 
        /// If an Id is not supplied by the user the SDK will automatically generate a new GUID and assign its value to this property before
        /// persisting the document in the database. 
        /// You can override this auto Id generation by setting the disableAutomaticIdGeneration parameter on the <see cref="Microsoft.Azure.Cosmos.DocumentClient"/> instance to true.
        /// This will prevent the SDK from generating new Ids. 
        /// </para>
        /// <para>
        /// The following characters are restricted and cannot be used in the Id property:
        ///  '/', '\\', '?', '#'
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.Id)]
        public string Id
        {
            get => this.id;
            set => this.id = value ?? throw new ArgumentNullException(nameof(this.Id));
        }

        /// <summary>
        /// Gets or sets the <see cref="UniqueKeyPolicy"/> that guarantees uniqueness of documents in container in the Azure Cosmos DB service.
        /// </summary>
        [JsonIgnore]
        public UniqueKeyPolicy UniqueKeyPolicy
        {
            get
            {
                if (this.uniqueKeyPolicyInternal == null)
                {
                    this.uniqueKeyPolicyInternal = new UniqueKeyPolicy();
                }

                return this.uniqueKeyPolicyInternal;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException($"{nameof(value)}");
                }

                this.uniqueKeyPolicyInternal = value;
            }
        }

        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources. 
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.ETag, NullValueHandling = NullValueHandling.Ignore)]
        public string ETag { get; private set; }

        /// <summary>
        /// Gets the last modified time stamp associated with <see cref="ContainerProperties" /> from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified time stamp associated with the resource.</value>
        [JsonProperty(PropertyName = Constants.Properties.LastModified, NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime? LastModified { get; private set; }

        /// <summary>
        /// Gets the <see cref="IndexingPolicy"/> associated with the container from the Azure Cosmos DB service. 
        /// </summary>
        /// <value>
        /// The indexing policy associated with the container.
        /// </value>
        [JsonIgnore]
        public IndexingPolicy IndexingPolicy
        {
            get
            {
                if (this.indexingPolicyInternal == null)
                {
                    this.indexingPolicyInternal = new IndexingPolicy();
                }

                return this.indexingPolicyInternal;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException($"{nameof(value)}");
                }

                this.indexingPolicyInternal = value;
            }
        }

        /// <summary>
        /// JSON path used for containers partitioning
        /// </summary>
        [JsonIgnore]
        public string PartitionKeyPath
        {
            get => this.PartitionKey?.Paths != null && this.PartitionKey.Paths.Count > 0 ? this.PartitionKey?.Paths[0] : null;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentNullException(nameof(this.PartitionKeyPath));
                }

                this.PartitionKey = new PartitionKeyDefinition
                {
                    Paths = new Collection<string>() { value }
                };
            }
        }

        /// <summary>
        /// Gets or sets the time to live base time stamp property path.
        /// </summary>
        /// <value>
        /// It is an optional property.
        /// This property should be only present when DefaultTimeToLive is set. When this property is present, time to live
        /// for a item is decided based on the value of this property in item.
        /// By default, TimeToLivePropertyPath is set to null meaning the time to live is based on the _ts property in item.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.TimeToLivePropertyPath, NullValueHandling = NullValueHandling.Ignore)]
        public string TimeToLivePropertyPath { get; set; }

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
        [JsonProperty(PropertyName = Constants.Properties.DefaultTimeToLive, NullValueHandling = NullValueHandling.Ignore)]
        public int? DefaultTimeToLive { get; set; }

        /// <summary>
        /// The function selects the right partition key constant mapping for <see cref="PartitionKey.None"/>
        /// </summary>
        internal PartitionKeyInternal GetNoneValue()
        {
            if (this.PartitionKey == null)
            {
                throw new ArgumentNullException($"{nameof(this.PartitionKey)}");
            }

            if (this.PartitionKey.Paths.Count == 0 || (this.PartitionKey.IsSystemKey == true))
            {
                return PartitionKeyInternal.Empty;
            }
            else
            {
                return PartitionKeyInternal.Undefined;
            }
        }

        /// <summary>
        /// Only collection cache needs this contract. None are expected to use it. 
        /// </summary>
        /// <param name="resourceId">The resource identifier for the container.</param>
        /// <returns>An instance of <see cref="ContainerProperties"/>.</returns>
        internal static ContainerProperties CreateWithResourceId(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId))
            {
                throw new ArgumentNullException(nameof(resourceId));
            }

            return new ContainerProperties()
            {
                ResourceId = resourceId,
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContainerProperties"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="id">The Id of the resource in the Azure Cosmos service.</param>
        /// <param name="partitionKeyDefinition">The partition key <see cref="PartitionKeyDefinition"/></param>
        internal ContainerProperties(string id, PartitionKeyDefinition partitionKeyDefinition)
        {
            this.Id = id;
            this.PartitionKey = partitionKeyDefinition;

            ValidateRequiredProperties();
        }

        /// <summary>
        /// Gets or sets <see cref="PartitionKeyDefinition"/> object in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// <see cref="PartitionKeyDefinition"/> object.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.PartitionKey)]
        internal PartitionKeyDefinition PartitionKey { get; set; } = new PartitionKeyDefinition();

        /// <summary>
        /// Gets the Resource Id associated with the resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Resource Id associated with the resource.
        /// </value>
        /// <remarks>
        /// A Resource Id is the unique, immutable, identifier assigned to each Azure Cosmos DB 
        /// resource whether that is a database, a container or a document.
        /// These resource ids are used when building up SelfLinks, a static addressable Uri for each resource within a database account.
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.RId, NullValueHandling = NullValueHandling.Ignore)]

        internal string ResourceId { get; private set; }

        internal bool HasPartitionKey => this.PartitionKey != null;

        internal string[] PartitionKeyPathTokens
        {
            get
            {
                if (this.partitionKeyPathTokens != null)
                {
                    return this.partitionKeyPathTokens;
                }

                if (this.PartitionKey.Paths.Count > 1)
                {
                    throw new NotImplementedException("PartitionKey extraction with composite partition keys not supported.");
                }

                if (this.PartitionKeyPath == null)
                {
                    throw new ArgumentOutOfRangeException($"Container {this.Id} is not partitioned");
                }

                this.partitionKeyPathTokens = this.PartitionKeyPath.Split(ContainerProperties.partitionKeyTokenDelimeter, StringSplitOptions.RemoveEmptyEntries);
                return this.partitionKeyPathTokens;
            }
        }

        /// <summary>
        /// Throws an exception if an invalid id or partition key is set.
        /// </summary>
        internal void ValidateRequiredProperties()
        {
            if (this.Id == null)
            {
                throw new ArgumentNullException(nameof(this.Id));
            }

            if (this.PartitionKey == null || this.PartitionKey.Paths.Count == 0)
            {
                throw new ArgumentNullException(nameof(this.PartitionKey));
            }

            // HACK: Till service can handle the defaults (self-mutation)
            // If indexing mode is not 'none' and not paths are set, set them to the defaults
            if (this.indexingPolicyInternal != null
                && this.indexingPolicyInternal.IndexingMode != IndexingMode.None
                && this.indexingPolicyInternal.IncludedPaths.Count == 0
                && this.indexingPolicyInternal.ExcludedPaths.Count == 0)
            {
                this.indexingPolicyInternal.IncludedPaths.Add(new IncludedPath() { Path = IndexingPolicy.DefaultPath });
            }
        }
    }
}