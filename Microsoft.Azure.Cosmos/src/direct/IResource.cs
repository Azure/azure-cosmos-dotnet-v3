//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.IO;

    /// <summary> 
    ///  Represents an abstract resource type in the Azure Cosmos DB service.
    ///  All Azure Cosmos DB resources, such as <see cref="Database"/>, <see cref="DocumentCollection"/>, and <see cref="Document"/> extend this abstract type.
    /// </summary>
    internal interface IResource
    {
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
#pragma warning restore CS1574 // XML comment has cref attribute that could not be resolved
        string Id { get; set; }

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
        string ResourceId { get; set; }

        /// <summary>
        /// Gets the self-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link associated with the resource.</value> 
        /// <remarks>
        /// A self-link is a static addressable Uri for each resource within a database account and follows the Azure Cosmos DB resource model.
        /// E.g. a self-link for a document could be dbs/db_resourceid/colls/coll_resourceid/documents/doc_resourceid
        /// </remarks>
        string SelfLink { get; }

        /// <summary>
        /// Gets the alt-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The alt-link associated with the resource.</value>
        string AltLink { get; set; }

        /// <summary>
        /// Gets the last modified timestamp associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified timestamp associated with the resource.</value>
        DateTime Timestamp { get; }

        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources. 
        /// </remarks>
        string ETag { get; }
    }
}
