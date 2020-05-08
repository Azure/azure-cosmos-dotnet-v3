//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;

    /// <summary> 
    /// Represents a user in the Azure Cosmos DB service.
    /// </summary>
    public class UserProperties
    {
        private string id;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserProperties"/> class for the Azure Cosmos DB service.
        /// </summary>
        internal UserProperties()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserProperties"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="id">The Id of the resource in the Azure Cosmos service.</param>
        public UserProperties(string id)
        {
            this.Id = id;
        }

        /// <summary>
        /// Gets or sets the Id of the resource in the Azure Cosmos DB service.
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
        public string Id
        {
            get => this.id;
            set => this.id = value ?? throw new ArgumentNullException(nameof(this.Id));
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
        public ETag? ETag { get; internal set; }

        /// <summary>
        /// Gets the last modified time stamp associated with <see cref="CosmosDatabaseProperties" /> from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified time stamp associated with the resource.</value>
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

        /// <summary>
        /// Gets the self-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link associated with the resource.</value> 
        /// <remarks>
        /// A self-link is a static addressable Uri for each resource within a database account and follows the Azure Cosmos DB resource model.
        /// E.g. a self-link for a document could be dbs/db_resourceid/colls/coll_resourceid/documents/doc_resourceid
        /// </remarks>
        internal string SelfLink { get; set; }

        /// <summary>
        /// Gets the permissions associated with the user for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The permissions associated with the user.</value> 
        internal string Permissions { get; set; }

        /// <summary>
        /// Gets the self-link of the permissions associated with the user for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link of the permissions associated with the user.</value>
        internal string PermissionsLink
        {
            get
            {
                return $"{this.SelfLink?.TrimEnd('/')}/{ this.Permissions}";
            }
        }
    }
}
