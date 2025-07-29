//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    /// <summary> 
    /// Represents a user in the Azure Cosmos DB service.
    /// </summary>
    public class UserProperties
    {
        private string id;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserProperties"/> class for the Azure Cosmos DB service.
        /// </summary>
        protected UserProperties()
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
        /// The following characters are restricted and cannot be used in the Id property:
        ///  '/', '\\', '?', '#'
        /// </para>
        /// </remarks>
        [JsonPropertyName(Constants.Properties.Id)]
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
        [JsonPropertyName(Constants.Properties.ETag)]
        public string ETag { get; private set; }

        /// <summary>
        /// Gets the last modified time stamp associated with <see cref="DatabaseProperties" /> from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified time stamp associated with the resource.</value>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        [JsonPropertyName(Constants.Properties.LastModified)]
        public DateTime? LastModified { get; private set; }

        /// <summary>
        /// Gets the self-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link associated with the resource.</value> 
        /// <remarks>
        /// A self-link is a static addressable Uri for each resource within a database account and follows the Azure Cosmos DB resource model.
        /// E.g. a self-link for a document could be dbs/db_resourceid/colls/coll_resourceid/documents/doc_resourceid
        /// </remarks>
        [JsonPropertyName(Constants.Properties.SelfLink)]
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
        [JsonPropertyName(Constants.Properties.RId)]
        internal string ResourceId { get; set; }

        /// <summary>
        /// Gets the permissions associated with the user for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The permissions associated with the user.</value> 
        [JsonPropertyName(Constants.Properties.PermissionsLink)]
        internal string Permissions { get; private set; }

        /// <summary>
        /// Gets the self-link of the permissions associated with the user for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link of the permissions associated with the user.</value>
        internal string PermissionsLink => $"{this.SelfLink?.TrimEnd('/')}/{ this.Permissions}";

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JsonElement> AdditionalProperties { get; private set; }
    }
}
