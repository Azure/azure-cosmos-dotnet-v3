//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Cosmos.Internal;
    using Newtonsoft.Json;

    /// <summary> 
    /// Represents a <see cref="CosmosAccountSettings"/>. A DatabaseAccountSettings is the container for databases in the Azure Cosmos DB service.
    /// </summary>
    public class CosmosAccountSettings : CosmosResource
    {
        private ReplicationPolicy replicationPolicy;
        private CosmosConsistencySettings consistencyPolicy;
        private ReplicationPolicy systemReplicationPolicy;
        private ReadPolicy readPolicy;
        private Dictionary<string, object> queryEngineConfiguration;
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
        internal virtual string DatabasesLink
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.DatabasesLink);
            }
            set
            {
                base.SetValue(Constants.Properties.DatabasesLink, value);
            }
        }

        /// <summary>
        /// Gets the self-link for Media in the databaseAccount from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The self-link for Media in the databaseAccount.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.MediaLink)]
        internal virtual string MediaLink
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.MediaLink);
            }
            set
            {
                base.SetValue(Constants.Properties.MediaLink, value);
            }
        }

        /// <summary>
        /// Gets the list of locations representing the writable regions of
        /// this database account from the Azure Cosmos DB service.
        /// </summary>
        [JsonIgnore]
        public virtual IEnumerable<CosmosAccountLocation> WritableLocations
        {
            get { return this.WriteLocationsInternal; }
        }

        [JsonProperty(PropertyName = Constants.Properties.WritableLocations)]
        internal Collection<CosmosAccountLocation> WriteLocationsInternal
        {
            get
            {
                if (this.writeLocations == null)
                {
                    this.writeLocations = this.GetObjectCollection<CosmosAccountLocation>(Constants.Properties.WritableLocations);

                    if (this.writeLocations == null)
                    {
                        this.writeLocations = new Collection<CosmosAccountLocation>();
                        base.SetObjectCollection(Constants.Properties.WritableLocations, this.writeLocations);
                    }
                }
                return this.writeLocations;
            }
            set
            {
                this.writeLocations = value;
                base.SetObjectCollection(Constants.Properties.WritableLocations, value);
            }
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

        [JsonProperty(PropertyName = Constants.Properties.ReadableLocations)]
        internal Collection<CosmosAccountLocation> ReadLocationsInternal
        {
            get
            {
                if (this.readLocations == null)
                {
                    this.readLocations = this.GetObjectCollection<CosmosAccountLocation>(Constants.Properties.ReadableLocations);

                    if (this.readLocations == null)
                    {
                        this.readLocations = new Collection<CosmosAccountLocation>();
                        base.SetObjectCollection(Constants.Properties.ReadableLocations, this.readLocations);
                    }
                }
                return this.readLocations;
            }
            set
            {
                this.readLocations = value;
                base.SetObjectCollection(Constants.Properties.ReadableLocations, value);
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
        public virtual CosmosConsistencySettings ConsistencySetting
        {
            get
            {
                if (this.consistencyPolicy == null)
                {
                    this.consistencyPolicy = base.GetObject<CosmosConsistencySettings>(Constants.Properties.UserConsistencyPolicy);

                    if (this.consistencyPolicy == null)
                    {
                        this.consistencyPolicy = new CosmosConsistencySettings();
                    }
                }
                return this.consistencyPolicy;
            }
        }

        /// <summary>
        /// Gets the self-link for Address Routing Table in the databaseAccount
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.AddressesLink)]
        internal string AddressesLink
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.AddressesLink);
            }
            set
            {
                base.SetValue(Constants.Properties.AddressesLink, value.ToString());
            }
        }

        /// <summary>
        /// Gets the ReplicationPolicy settings
        /// </summary>
        internal ReplicationPolicy ReplicationPolicy
        {
            get
            {
                if (this.replicationPolicy == null)
                {
                    this.replicationPolicy = base.GetObject<ReplicationPolicy>(Constants.Properties.UserReplicationPolicy);

                    if (this.replicationPolicy == null)
                    {
                        this.replicationPolicy = new ReplicationPolicy();
                    }
                }
                return this.replicationPolicy;
            }
        }

        /// <summary>
        /// Gets the SystemReplicationPolicy settings
        /// </summary>
        internal ReplicationPolicy SystemReplicationPolicy
        {
            get
            {
                if (this.systemReplicationPolicy == null)
                {
                    this.systemReplicationPolicy = base.GetObject<ReplicationPolicy>(Constants.Properties.SystemReplicationPolicy);

                    if (this.systemReplicationPolicy == null)
                    {
                        this.systemReplicationPolicy = new ReplicationPolicy();
                    }
                }
                return this.systemReplicationPolicy;
            }
        }

        internal ReadPolicy ReadPolicy
        {
            get
            {
                if (this.readPolicy == null)
                {
                    this.readPolicy = base.GetObject<ReadPolicy>(Constants.Properties.ReadPolicy);

                    if (this.readPolicy == null)
                    {
                        this.readPolicy = new ReadPolicy();
                    }
                }
                return this.readPolicy;
            }
        }

        internal IDictionary<string, object> QueryEngineConfiuration
        {
            get
            {
                if (this.queryEngineConfiguration == null)
                {
                    string queryEngineConfigurationJsonString = base.GetValue<string>(Constants.Properties.QueryEngineConfiguration);
                    if (!string.IsNullOrEmpty(queryEngineConfigurationJsonString))
                    {
                        this.queryEngineConfiguration = JsonConvert.DeserializeObject<Dictionary<string, object>>(queryEngineConfigurationJsonString);
                    }

                    if (this.queryEngineConfiguration == null)
                    {
                        this.queryEngineConfiguration = new Dictionary<string, object>();
                    }
                }

                return this.queryEngineConfiguration;
            }
        }

        internal bool EnableMultipleWriteLocations
        {
            get { return this.GetValue<bool>(Constants.Properties.EnableMultipleWriteLocations); }
            set { this.SetValue(Constants.Properties.EnableMultipleWriteLocations, value); }
        }

        internal override void OnSave()
        {
            if (this.replicationPolicy != null)
            {
                this.replicationPolicy.OnSave();
                base.SetObject(Constants.Properties.UserReplicationPolicy, this.replicationPolicy);
            }

            if (this.consistencyPolicy != null)
            {
                this.consistencyPolicy.OnSave();
                base.SetObject(Constants.Properties.UserConsistencyPolicy, this.consistencyPolicy);
            }

            if (this.systemReplicationPolicy != null)
            {
                this.systemReplicationPolicy.OnSave();
                base.SetObject(Constants.Properties.SystemReplicationPolicy, this.systemReplicationPolicy);
            }

            if (this.readPolicy != null)
            {
                this.readPolicy.OnSave();
                base.SetObject(Constants.Properties.ReadPolicy, this.readPolicy);
            }

            if (this.readLocations != null)
            {
                base.SetObjectCollection(Constants.Properties.ReadableLocations, this.readLocations);
            }

            if (this.writeLocations != null)
            {
                base.SetObjectCollection(Constants.Properties.WritableLocations, this.writeLocations);
            }

            if (this.queryEngineConfiguration != null)
            {
                base.SetValue(Constants.Properties.QueryEngineConfiguration, JsonConvert.SerializeObject(this.queryEngineConfiguration));
            }
        }

        internal static CosmosAccountSettings CreateNewInstance()
        {
            return new CosmosAccountSettings();
        }
    }
}
