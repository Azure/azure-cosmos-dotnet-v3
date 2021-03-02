//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary> 
    /// Represents a permission in the Azure Cosmos DB service.
    /// </summary>
    public class PermissionProperties
    {
        /// <summary>
        /// Initialize a new instance of the <see cref="PermissionProperties"/> with permission to <see cref="Container"/>.
        /// </summary>
        /// <param name="id">The permission id.</param>
        /// <param name="permissionMode">The <see cref="PermissionMode"/>.</param>
        /// <param name="container">The <see cref="Container"/> object.</param>
        /// <param name="resourcePartitionKey">(Optional) The partition key value for the permission in the Azure Cosmos DB service.</param>
        public PermissionProperties(string id,
            PermissionMode permissionMode,
            Container container,
            PartitionKey? resourcePartitionKey = null)
        {
            this.Id = id;
            this.PermissionMode = permissionMode;
            this.ResourceUri = UriFactory.CreateDocumentCollectionUri(container.Database.Id,
                                                                      container.Id).ToString();
            if (resourcePartitionKey == null)
            {
                this.InternalResourcePartitionKey = null;
            }
            else
            {
                this.InternalResourcePartitionKey = resourcePartitionKey?.InternalKey;
            }
        }

        /// <summary>
        /// Initialize a new instance of the <see cref="PermissionProperties"/> with permission to Cosmos item.
        /// </summary>
        /// <param name="id">The permission id.</param>
        /// <param name="permissionMode">The <see cref="PermissionMode"/>.</param>
        /// <param name="container">The <see cref="Container"/> object.</param>
        /// <param name="resourcePartitionKey">The <see cref="PartitionKey"/> of the resource in the Azure Cosmos service.</param>
        /// <param name="itemId">The cosmos item id</param>
        public PermissionProperties(string id,
            PermissionMode permissionMode,
            Container container,
            PartitionKey resourcePartitionKey,
            string itemId)
        {
            this.Id = id;
            this.PermissionMode = permissionMode;
            this.ResourceUri = UriFactory.CreateDocumentUri(container.Database.Id,
                                                            container.Id,
                                                            itemId).ToString();
            this.InternalResourcePartitionKey = resourcePartitionKey.InternalKey;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionProperties"/> class for the Azure Cosmos DB service.
        /// </summary>
        internal PermissionProperties()
        {
        }

        /// <summary>
        /// Gets the Id of the resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The Id associated with the resource.</value>
        /// <remarks>
        /// <para>
        /// Every resource within an Azure Cosmos DB database account needs to have a unique identifier. 
        /// </para>
        /// <para>
        /// When working with document resources, they too have this settable Id property. 
        /// If an Id is not supplied by the user the SDK will automatically generate a new GUID and assign its value to this property before
        /// persisting the document in the database.
        /// </para>
        /// <para>
        /// The following characters are restricted and cannot be used in the Id property:
        ///  '/', '\\', '?', '#'
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.Id)]
        public string Id { get; private set; }

        /// <summary> 
        /// Gets the self-uri of resource to which the permission applies in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The-uri of the resource to which the permission applies.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.ResourceLink)]
        public string ResourceUri { get; private set; }

        /// <summary>
        /// Gets optional partition key value for the permission in the Azure Cosmos DB service.
        /// A permission applies to resources when two conditions are met:
        ///       1. <see cref="ResourceUri"/> is prefix of resource's link.
        ///             For example "/dbs/mydatabase/colls/mycollection" applies to "/dbs/mydatabase/colls/mycollection" and "/dbs/mydatabase/colls/mycollection/docs/mydocument"
        ///       2. <see cref="ResourcePartitionKey"/> is superset of resource's partition key.
        ///             For example absent/empty partition key is superset of all partition keys.
        /// </summary>
        [JsonIgnore]
        public PartitionKey? ResourcePartitionKey
        {
            get
            {
                if (this.InternalResourcePartitionKey == null)
                {
                    return null;
                }
                if (this.InternalResourcePartitionKey.ToObjectArray().Length > 0)
                {
                    return new PartitionKey(this.InternalResourcePartitionKey.ToObjectArray()[0]);
                }
                return null;
            }
            set
            {
                if (value == null || (value.HasValue && value.Value.IsNone))
                {
                    this.InternalResourcePartitionKey = null;
                }
                else
                {
                    this.InternalResourcePartitionKey = value.Value.InternalKey;
                }
            }
        }

        /// <summary>
        /// Gets the permission mode in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The <see cref="PermissionMode"/> mode: Read or All.
        /// </value>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = Constants.Properties.PermissionMode)]
        public PermissionMode PermissionMode { get; private set; }

        /// <summary>
        /// Gets the access token granting the defined permission from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The access token granting the defined permission.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.Token, NullValueHandling = NullValueHandling.Ignore)]
        public string Token { get; private set; }

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
        /// Gets the last modified time stamp associated with <see cref="PermissionProperties" /> from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified time stamp associated with the resource.</value>
        /// <remarks>ResourceToken generation and reading does not apply.</remarks>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        [JsonProperty(PropertyName = Constants.Properties.LastModified, NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? LastModified { get; private set; }

        /// <summary>
        /// Gets the self-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link associated with the resource.</value> 
        /// <remarks>
        /// A self-link is a static addressable Uri for each resource within a database account and follows the Azure Cosmos DB resource model.
        /// E.g. a self-link for a document could be dbs/db_resourceid/colls/coll_resourceid/documents/doc_resourceid
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.SelfLink, NullValueHandling = NullValueHandling.Ignore)]
        public string SelfLink { get; private set; }

        /// <summary>
        /// Gets the Resource Id associated with the resource in the Azure Cosmos DB service.
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
        internal string ResourceId { get; private set; }

        [JsonProperty(PropertyName = Constants.Properties.ResourcePartitionKey, NullValueHandling = NullValueHandling.Ignore)]
        internal Documents.Routing.PartitionKeyInternal InternalResourcePartitionKey { get; private set; }
    }
}
