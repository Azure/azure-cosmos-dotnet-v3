//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents a per-User permission to access a specific resource in the Azure Cosmos DB service, for example Document or Collection.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    class Permission : Resource
    {
        /// <summary> 
        /// Gets or sets the self-link of resource to which the permission applies in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The self-link of the resource to which the permission applies.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.ResourceLink)]
        public string ResourceLink
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.ResourceLink);
            }
            set
            {
                base.SetValue(Constants.Properties.ResourceLink, value);
            }
        }

        /// <summary>
        /// Gets or sets optional partition key value for the permission in the Azure Cosmos DB service.
        /// A permission applies to resources when two conditions are met:
        ///       1. <see cref="ResourceLink"/> is prefix of resource's link.
        ///             For example "/dbs/mydatabase/colls/mycollection" applies to "/dbs/mydatabase/colls/mycollection" and "/dbs/mydatabase/colls/mycollection/docs/mydocument"
        ///       2. <see cref="ResourcePartitionKey"/> is superset of resource's partition key.
        ///             For example absent/empty partition key is superset of all partition keys.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.ResourcePartitionKey)]
        public PartitionKey ResourcePartitionKey
        {
            get
            {
                PartitionKeyInternal partitionKey = base.GetValue<PartitionKeyInternal>(Constants.Properties.ResourcePartitionKey);
                return partitionKey == null ? null : new PartitionKey(partitionKey.ToObjectArray()[0]);
            }
            set
            {
                if (value != null)
                {
                    base.SetValue(Constants.Properties.ResourcePartitionKey, value.InternalKey);
                }
            }
        }

        /// <summary>
        /// Gets or sets the permission mode in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The <see cref="PermissionMode"/> mode: Read or All.
        /// </value>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = Constants.Properties.PermissionMode)]
        public PermissionMode PermissionMode
        {
            get                
            {
                return base.GetValue<PermissionMode>(Constants.Properties.PermissionMode, PermissionMode.All);
            }
            set
            {
                base.SetValue(Constants.Properties.PermissionMode, value.ToString());                
            }
        }

        /// <summary>
        /// Gets the access token granting the defined permission from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The access token granting the defined permission.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.Token)]
        public string Token
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Token);
            }
            private set
            {
                base.SetValue(Constants.Properties.Token, value);
            }
        }        
    }
}
