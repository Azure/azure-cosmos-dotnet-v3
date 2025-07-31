//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.IO;
    using System.Text.Json.Serialization;

    /// <summary> 
    ///  Represents an abstract resource type in the Azure Cosmos DB service.
    ///  Internal Azure Cosmos DB resources that don't use Newtonsoft.Json for serialization,
    ///  such as <see cref="Address"/>, extend this abstract type.
    /// </summary>
    public abstract class PlainResource : IResource
    {
        internal static DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Initializes a new instance of the <see cref="Resource"/> class for the Azure Cosmos DB service.
        /// </summary>
        protected PlainResource()
        {
        }

        /// <summary>
        /// Copy constructor for a <see cref="Resource"/> used in the Azure Cosmos DB service.
        /// </summary>
        protected PlainResource(PlainResource resource)
        {
            this.Id = resource.Id;
            this.ResourceId = resource.ResourceId;
            this.SelfLink = resource.SelfLink;
            this.AltLink = resource.AltLink;
            this.Timestamp = resource.Timestamp;
            this.ETag = resource.ETag;
        }

#pragma warning disable CS1574 // XML comment has cref attribute that could not be resolved
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
        /// You can override this auto Id generation by setting the disableAutomaticIdGeneration parameter on the <see cref="Microsoft.Azure.Documents.Client.DocumentClient"/> instance to true.
        /// This will prevent the SDK from generating new Ids. 
        /// </para>
        /// <para>
        /// The following characters are restricted and cannot be used in the Id property:
        ///  '/', '\\', '?', '#'
        /// </para>
        /// </remarks>
        [JsonPropertyName(Constants.Properties.Id)]
#pragma warning restore CS1574 // XML comment has cref attribute that could not be resolved
        public virtual string Id { get; set; }

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
        [JsonPropertyName(Constants.Properties.RId)]
        public virtual string ResourceId { get; set; }

        /// <summary>
        /// Gets the self-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link associated with the resource.</value> 
        /// <remarks>
        /// A self-link is a static addressable Uri for each resource within a database account and follows the Azure Cosmos DB resource model.
        /// E.g. a self-link for a document could be dbs/db_resourceid/colls/coll_resourceid/documents/doc_resourceid
        /// </remarks>
        [JsonPropertyName(Constants.Properties.SelfLink)]
        public string SelfLink { get; internal set; }

        /// <summary>
        /// Gets the alt-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The alt-link associated with the resource.</value>
        [JsonIgnore]
        public string AltLink { get; set; }

        /// <summary>
        /// Gets the last modified timestamp associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified timestamp associated with the resource.</value>
        [JsonPropertyName(Constants.Properties.LastModified)]
        // TODO: Add custom converter for Unix timestamp if needed
        public virtual DateTime Timestamp { get; internal set; }

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
        public string ETag { get; internal set; }

        /// <summary>
        /// Returns a string representation of the resource with all property names and values.
        /// </summary>
        /// <returns>A string containing all property names and their values.</returns>
        public string asString()
        {
            return $"Id={this.Id}, ResourceId={this.ResourceId}, SelfLink={this.SelfLink}, AltLink={this.AltLink}, Timestamp={this.Timestamp}, ETag={this.ETag}";
        }
    }
}
