//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary> 
    /// Represents a permission in the Azure Cosmos DB service.
    /// </summary>
    public sealed class PermissionProperties
    {
        /// <summary>
        /// Initialize a new instance of the <see cref="PermissionProperties"/> with permssion to <see cref="CosmosContainer"/>.
        /// </summary>
        /// <param name="id">The permission id.</param>
        /// <param name="permissionMode">The <see cref="PermissionMode"/>.</param>
        /// <param name="container">The <see cref="CosmosContainer"/> object.</param>
        /// <param name="resourcePartitionKey">(Optional) The partition key value for the permission in the Azure Cosmos DB service. see <see cref="PartitionKey"/></param>
        public PermissionProperties(string id,
            PermissionMode permissionMode,
            CosmosContainer container,
            PartitionKey? resourcePartitionKey = null)
        {
            this.Id = id;
            this.PermissionMode = permissionMode;
            this.ResourceUri = ((ContainerCore)container).LinkUri.OriginalString;
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
        /// Initialize a new instance of the <see cref="PermissionProperties"/> with permssion to cosnmos item.
        /// </summary>
        /// <param name="id">The permission id.</param>
        /// <param name="permissionMode">The <see cref="PermissionMode"/>.</param>
        /// <param name="container">The <see cref="CosmosContainer"/> object.</param>
        /// <param name="resourcePartitionKey">The <see cref="PartitionKey"/> of the resource in the Azure Cosmos service.</param>
        /// <param name="itemId">The cosmos item id</param>
        public PermissionProperties(string id,
            PermissionMode permissionMode,
            CosmosContainer container,
            PartitionKey resourcePartitionKey,
            string itemId)
        {
            this.Id = id;
            this.PermissionMode = permissionMode;
            this.ResourceUri = ((ContainerCore)container).ClientContext.CreateLink(
                    parentLink: ((ContainerCore)container).LinkUri.OriginalString,
                    uriPathSegment: Paths.DocumentsPathSegment,
                    id: id).OriginalString;
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
        public string Id { get; internal set; }

        /// <summary> 
        /// Gets the self-uri of resource to which the permission applies in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The-uri of the resource to which the permission applies.
        /// </value>
        public string ResourceUri { get; internal set; }

        /// <summary>
        /// Gets optional partition key value for the permission in the Azure Cosmos DB service.
        /// A permission applies to resources when two conditions are met:
        ///       1. <see cref="ResourceUri"/> is prefix of resource's link.
        ///             For example "/dbs/mydatabase/colls/mycollection" applies to "/dbs/mydatabase/colls/mycollection" and "/dbs/mydatabase/colls/mycollection/docs/mydocument"
        ///       2. <see cref="ResourcePartitionKey"/> is superset of resource's partition key.
        ///             For example absent/empty partition key is superset of all partition keys.
        /// </summary>
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
        public PermissionMode PermissionMode { get; internal set; }

        /// <summary>
        /// Gets the access token granting the defined permission from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The access token granting the defined permission.
        /// </value>
        public string Token { get; internal set; }

        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources. 
        /// </remarks>
        public ETag? ETag { get; internal set; }

        /// <summary>
        /// Gets the last modified time stamp associated with <see cref="PermissionProperties" /> from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified time stamp associated with the resource.</value>
        /// <remarks>ResourceToken generation and reading does not apply.</remarks>
        public DateTime? LastModified { get; internal set; }

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
        internal string ResourceId { get; set; }

        internal Microsoft.Azure.Documents.Routing.PartitionKeyInternal InternalResourcePartitionKey { get; set; }
    }
}
