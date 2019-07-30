//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary> 
    /// Represents a user in the Azure Cosmos DB service.
    /// </summary>
    public class PermissionProperties
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionProperties"/> class for the Azure Cosmos DB service.
        /// </summary>
        public PermissionProperties()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionProperties"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="id">The Id of the resource in the Azure Cosmos service.</param>
        /// <param name="permissionMode">The permission mode of the resource in the Azure Cosmos service.</param>
        public PermissionProperties(string id, PermissionMode permissionMode) 
            : this(id, permissionMode, PartitionKey.Null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionProperties"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="id">The Id of the resource in the Azure Cosmos service.</param>
        /// <param name="permissionMode">The permission mode of the resource in the Azure Cosmos service.</param>
        /// <param name="resourcePartitionKey">The <see cref="PartitionKey"/> of the resource in the Azure Cosmos service.</param>
        public PermissionProperties(string id, PermissionMode permissionMode, PartitionKey resourcePartitionKey)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            this.Id = id;
            this.PermissionMode = permissionMode;            
            this.InternalResourcePartitionKey = resourcePartitionKey.InternalKey;
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
        /// You can override this auto Id generation by setting the disableAutomaticIdGeneration parameter on the <see cref="Microsoft.Azure.Cosmos.DocumentClient"/> instance to true.
        /// This will prevent the SDK from generating new Ids. 
        /// </para>
        /// <para>
        /// The following characters are restricted and cannot be used in the Id property:
        ///  '/', '\\', '?', '#'
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.Id)]
        public string Id { get; private set; }

        /// <summary> 
        /// Gets the self-link of resource to which the permission applies in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The self-link of the resource to which the permission applies.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.ResourceLink)]
        public string ResourceLink { get; private set; }
        
        [JsonProperty(PropertyName = Constants.Properties.ResourcePartitionKey)]
        internal Documents.Routing.PartitionKeyInternal InternalResourcePartitionKey { get; private set; }

        /// <summary>
        /// Gets optional partition key value for the permission in the Azure Cosmos DB service.
        /// A permission applies to resources when two conditions are met:
        ///       1. <see cref="ResourceLink"/> is prefix of resource's link.
        ///             For example "/dbs/mydatabase/colls/mycollection" applies to "/dbs/mydatabase/colls/mycollection" and "/dbs/mydatabase/colls/mycollection/docs/mydocument"
        ///       2. <see cref="ResourcePartitionKey"/> is superset of resource's partition key.
        ///             For example absent/empty partition key is superset of all partition keys.
        /// </summary>
        [JsonIgnore]
        public PartitionKey ResourcePartitionKey
        {
            get
            {
                return this.InternalResourcePartitionKey == null ? 
                    PartitionKey.Null : 
                    new PartitionKey(this.InternalResourcePartitionKey.ToObjectArray()[0]);
            }
            set
            {
                this.InternalResourcePartitionKey = value.InternalKey;
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
        [JsonProperty(PropertyName = Constants.Properties.Token)]
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
        [JsonProperty(PropertyName = Constants.Properties.ETag)]
        public string ETag { get; private set; }

        /// <summary>
        /// Gets the last modified time stamp associated with <see cref="DatabaseProperties" /> from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified time stamp associated with the resource.</value>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        [JsonProperty(PropertyName = Constants.Properties.LastModified)]
        public DateTime? LastModified { get; private set; }

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
        [JsonProperty(PropertyName = Constants.Properties.RId)]
        internal string ResourceId { get; private set; }

        /// <summary>
        /// Create a new instance of the <see cref="PermissionProperties"/> with permssion to <see cref="Container"/>.
        /// </summary>
        /// <param name="id">The permission id.</param>
        /// <param name="permissionMode">The <see cref="PermissionMode"/>.</param>
        /// <param name="container">The <see cref="Container"/> object.</param>
        /// <returns>an instandce of <see cref="PermissionProperties"/></returns>
        public static PermissionProperties CreateForContainer(string id,
            PermissionMode permissionMode,
            Container container)
        {
            return new PermissionProperties(id, permissionMode)
            {
                ResourceLink = ((ContainerCore)container).LinkUri.OriginalString
            };
        }

        /// <summary>
        /// Create a new instance of the <see cref="PermissionProperties"/> with permssion to cosnmos item.
        /// </summary>
        /// <param name="id">The permission id.</param>
        /// <param name="permissionMode">The <see cref="PermissionMode"/>.</param>
        /// <param name="container">The <see cref="Container"/> object.</param>
        /// <param name="resourcePartitionKey">The <see cref="PartitionKey"/> of the resource in the Azure Cosmos service.</param>
        /// <param name="itemId">The cosmos item id</param>
        /// <returns>an instandce of <see cref="PermissionProperties"/></returns>
        public static PermissionProperties CreateForItem(string id,
            PermissionMode permissionMode, 
            Container container,
            PartitionKey resourcePartitionKey,
            string itemId)
        {
            return new PermissionProperties(id, permissionMode, resourcePartitionKey)
            {
                ResourceLink = ((ContainerCore)container).GetResourceUri(null, OperationType.Read, itemId).OriginalString
            };
        }

        /// <summary>
        /// Create a new instance of the <see cref="PermissionProperties"/> with permssion to cosmos stored procedure.
        /// </summary>
        /// <param name="id">The permission id.</param>
        /// <param name="permissionMode">The <see cref="PermissionMode"/>.</param>
        /// <param name="container">The <see cref="Container"/> object.</param>
        /// <param name="resourcePartitionKey">The <see cref="PartitionKey"/> of the resource in the Azure Cosmos service.</param>
        /// <param name="storeProcedureId">The cosmos stored procedure id</param>
        /// <returns>an instandce of <see cref="PermissionProperties"/></returns>
        public static PermissionProperties CreateForStoredProcedure(string id,
            PermissionMode permissionMode,
            Container container, 
            PartitionKey resourcePartitionKey,
            string storeProcedureId)
        {
            return new PermissionProperties(id, permissionMode, resourcePartitionKey)
            {
                ResourceLink = ((ContainerCore)container).ClientContext.CreateLink(
                    parentLink: ((ContainerCore)container).LinkUri.OriginalString,
                    uriPathSegment: Paths.StoredProceduresPathSegment,
                    id: id).OriginalString
            };
        }

        /// <summary>
        /// Create a new instance of the <see cref="PermissionProperties"/> with permssion to cosmos stored procedure.
        /// </summary>
        /// <param name="id">The permission id.</param>
        /// <param name="permissionMode">The <see cref="PermissionMode"/>.</param>
        /// <param name="container">The <see cref="Container"/> object.</param>
        /// <param name="resourcePartitionKey">The <see cref="PartitionKey"/> of the resource in the Azure Cosmos service.</param>
        /// <param name="userDefinedFunctionId">The cosmos user defined function id</param>
        /// <returns>an instandce of <see cref="PermissionProperties"/></returns>
        public static PermissionProperties CreateForUserDefinedFunction(string id,
            PermissionMode permissionMode,
            Container container,
            PartitionKey resourcePartitionKey,
            string userDefinedFunctionId)
        {
            return new PermissionProperties(id, permissionMode, resourcePartitionKey)
            {
                ResourceLink = ((ContainerCore)container).ClientContext.CreateLink(
                    parentLink: ((ContainerCore)container).LinkUri.OriginalString,
                    uriPathSegment: Paths.UserDefinedFunctionsPathSegment,
                    id: id).OriginalString
            };
        }

        /// <summary>
        /// Create a new instance of the <see cref="PermissionProperties"/> with permssion to cosmos stored procedure.
        /// </summary>
        /// <param name="id">The permission id.</param>
        /// <param name="permissionMode">The <see cref="PermissionMode"/>.</param>
        /// <param name="container">The <see cref="Container"/> object.</param>
        /// <param name="resourcePartitionKey">The <see cref="PartitionKey"/> of the resource in the Azure Cosmos service.</param>
        /// <param name="triggerId">The cosmos trigger id</param>
        /// <returns>an instandce of <see cref="PermissionProperties"/></returns>
        public static PermissionProperties CreateForTrigger(string id,
            PermissionMode permissionMode,
            Container container,
            PartitionKey resourcePartitionKey,
            string triggerId)
        {
            return new PermissionProperties(id, permissionMode, resourcePartitionKey)
            {
                ResourceLink = ((ContainerCore)container).ClientContext.CreateLink(
                    parentLink: ((ContainerCore)container).LinkUri.OriginalString,
                    uriPathSegment: Paths.TriggersPathSegment,
                    id: id).OriginalString
            };
        }
    }
}
