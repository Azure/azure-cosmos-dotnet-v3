//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary> 
    /// Represents a <see cref="AccountProperties"/>. A AccountProperties is the container for databases in the Azure Cosmos DB service.
    /// </summary>
    public class AccountProperties
    {
        private Collection<AccountRegion> readRegions;
        private Collection<AccountRegion> writeRegions;

        internal readonly Lazy<IDictionary<string, object>> QueryEngineConfigurationInternal;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountProperties"/> class.
        /// </summary>
        internal AccountProperties()
        {
            this.QueryEngineConfigurationInternal = new Lazy<IDictionary<string, object>>(() => this.QueryStringToDictConverter());
        }

        /// <summary>
        /// Gets the list of locations representing the writable regions of
        /// this database account from the Azure Cosmos DB service.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<AccountRegion> WritableRegions => this.WriteLocationsInternal;

        /// <summary>
        /// Gets the list of locations representing the readable regions of
        /// this database account from the Azure Cosmos DB service.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<AccountRegion> ReadableRegions => this.ReadLocationsInternal;

        /// <summary>
        /// Gets the Id of the resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The Id associated with the resource.</value>
        /// <remarks>
        /// <para>
        /// Every resource within an Azure Cosmos DB database account needs to have a unique identifier. 
        /// Unlike <see cref="Documents.Resource.ResourceId"/>, which is set internally, this Id is settable by the user and is not immutable.
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
        public string Id { get; internal set; }

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
        public string ETag { get; internal set; }

        /// <summary>
        /// Gets or sets the Resource Id associated with the resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Resource Id associated with the resource.
        /// </value>
        /// <remarks>
        /// A Resource Id is the unique, immutable, identifier assigned to each Azure Cosmos DB 
        /// resource whether that is a database, a collection or a document.
        /// These resource ids are used when building up SelfLinks, a static addressable Uri for each resource within a database account.
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.RId, NullValueHandling = NullValueHandling.Ignore)]
        internal string ResourceId { get; set; }

        [JsonProperty(PropertyName = Constants.Properties.WritableLocations)]
        internal Collection<AccountRegion> WriteLocationsInternal
        {
            get
            {
                if (this.writeRegions == null)
                {
                    this.writeRegions = new Collection<AccountRegion>();
                }
                return this.writeRegions;
            }
            set => this.writeRegions = value;
        }

        [JsonProperty(PropertyName = Constants.Properties.ReadableLocations)]
        internal Collection<AccountRegion> ReadLocationsInternal
        {
            get
            {
                if (this.readRegions == null)
                {
                    this.readRegions = new Collection<AccountRegion>();
                }
                return this.readRegions;
            }
            set => this.readRegions = value;
        }

        /// <summary>
        /// Gets the storage quota for media storage in the databaseAccount from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The storage quota in measured MBs.
        /// </value>
        /// <remarks>
        /// This value is retrieved from the gateway.
        /// </remarks>
        internal long MaxMediaStorageUsageInMB
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the current attachment content (media) usage in MBs from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The attachment content (media) usage in MBs.
        /// </value>
        /// <remarks>
        /// The value is retrieved from the gateway. The value is returned from cached information updated periodically 
        /// and is not guaranteed to be real time.
        /// </remarks>
        internal long MediaStorageUsageInMB
        {
            get;
            set;
        }

        /// <summary>
        /// The cumulative sum of current sizes of created collection in MB
        /// Value is returned from cached information which is updated periodically and is not guaranteed to be real time
        /// TODO remove this property tfs 4442779
        /// </summary>
        internal long ConsumedDocumentStorageInMB
        {
            get;
            set;
        }

        /// <summary>
        /// The cumulative sum of maximum sizes of created collection in MB
        /// Value is returned from cached information which is updated periodically and is not guaranteed to be real time
        /// TODO remove this property tfs 4442779
        /// </summary>
        internal long ReservedDocumentStorageInMB
        {
            get;
            set;
        }

        /// <summary>
        /// The provisioned documented storage capacity for the database account
        /// Value is returned from cached information which is updated periodically and is not guaranteed to be real time
        /// TODO remove this property tfs 4442779
        /// </summary>
        internal long ProvisionedDocumentStorageInMB
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the <see cref="Consistency"/> from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The ConsistencySetting.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.UserConsistencyPolicy)]
        public AccountConsistency Consistency { get; internal set; }

        /// <summary>
        /// Gets the self-link for Address Routing Table in the databaseAccount
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.AddressesLink)]
        internal string AddressesLink { get; set; }

        /// <summary>
        /// Gets the ReplicationPolicy properties
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.UserReplicationPolicy)]
        internal ReplicationPolicy ReplicationPolicy { get; set; }

        /// <summary>
        /// Gets the SystemReplicationPolicy 
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.SystemReplicationPolicy)]
        internal ReplicationPolicy SystemReplicationPolicy { get; set; }

        [JsonProperty(PropertyName = Constants.Properties.ReadPolicy)]
        internal ReadPolicy ReadPolicy { get; set; }

        internal IDictionary<string, object> QueryEngineConfiguration => this.QueryEngineConfigurationInternal.Value;

        [JsonProperty(PropertyName = Constants.Properties.QueryEngineConfiguration)]
        internal string QueryEngineConfigurationString { get; set; }

        [JsonProperty(PropertyName = Constants.Properties.EnableMultipleWriteLocations)]
        internal bool EnableMultipleWriteLocations { get; set; }

        private IDictionary<string, object> QueryStringToDictConverter()
        {
            if (!string.IsNullOrEmpty(this.QueryEngineConfigurationString))
            {
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(this.QueryEngineConfigurationString);
            }
            else
            {
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
