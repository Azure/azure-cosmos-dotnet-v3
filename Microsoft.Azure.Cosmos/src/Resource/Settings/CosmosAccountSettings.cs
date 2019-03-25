//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary> 
    /// Represents a <see cref="CosmosAccountSettings"/>. A DatabaseAccountSettings is the container for databases in the Azure Cosmos DB service.
    /// </summary>
    public class CosmosAccountSettings : CosmosResource
    {
        private Collection<CosmosAccountLocation> readLocations;
        private Collection<CosmosAccountLocation> writeLocations;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosAccountSettings"/> class.
        /// </summary>
        internal CosmosAccountSettings()
        {
            this.SelfLink = string.Empty;
        }

        /// <summary>
        /// Gets the self-link for Databases in the databaseAccount from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The self-link for Databases in the databaseAccount.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.DatabasesLink)]
        internal virtual string DatabasesLink { get; set; }

        /// <summary>
        /// Gets the self-link for Media in the databaseAccount from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The self-link for Media in the databaseAccount.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.MediaLink)]
        internal virtual string MediaLink { get; set; }

        /// <summary>
        /// Gets the list of locations representing the writable regions of
        /// this database account from the Azure Cosmos DB service.
        /// </summary>
        [JsonIgnore]
        public virtual IEnumerable<CosmosAccountLocation> WritableLocations
        {
            get { return this.WriteLocationsInternal; }
        }

        /// <summary>
        /// Gets the list of locations representing the readable regions of
        /// this database account from the Azure Cosmos DB service.
        /// </summary>
        [JsonIgnore]
        public virtual IEnumerable<CosmosAccountLocation> ReadableLocations
        {
            get { return this.ReadLocationsInternal; }
        }

        [JsonProperty(PropertyName = Constants.Properties.WritableLocations)]
        internal Collection<CosmosAccountLocation> WriteLocationsInternal
        {
            get
            {
                if (this.writeLocations == null)
                {
                    this.writeLocations = new Collection<CosmosAccountLocation>();
                }
                return this.writeLocations;
            }
            set
            {
                this.writeLocations = value;
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.ReadableLocations)]
        internal Collection<CosmosAccountLocation> ReadLocationsInternal
        {
            get
            {
                if (this.readLocations == null)
                {
                    this.readLocations = new Collection<CosmosAccountLocation>();
                }
                return this.readLocations;
            }
            set
            {
                this.readLocations = value;
            }
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
        /// Gets the <see cref="ConsistencySetting"/> from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The ConsistencySetting.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.UserConsistencyPolicy)]
        public virtual CosmosConsistencySettings ConsistencySetting { get; internal set; }

        /// <summary>
        /// Gets the self-link for Address Routing Table in the databaseAccount
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.AddressesLink)]
        internal string AddressesLink { get; set; }

        /// <summary>
        /// Gets the ReplicationPolicy settings
        /// </summary>
        internal ReplicationPolicy ReplicationPolicy { get; set; }

        /// <summary>
        /// Gets the SystemReplicationPolicy settings
        /// </summary>
        internal ReplicationPolicy SystemReplicationPolicy { get; set; }

        internal ReadPolicy ReadPolicy { get; set; }

        [JsonProperty(PropertyName = Constants.Properties.QueryEngineConfiguration)]
        internal IDictionary<string, object> QueryEngineConfiuration { get; set; }

        internal bool EnableMultipleWriteLocations { get; set; }
    }
}
