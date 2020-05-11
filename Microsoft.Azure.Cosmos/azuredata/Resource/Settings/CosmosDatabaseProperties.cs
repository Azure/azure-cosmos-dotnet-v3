//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;

    /// <summary>
    /// Represents a database in the Azure Cosmos DB account.
    /// </summary>
    /// <remarks>
    /// Each Azure Cosmos DB database account can have zero or more databases. A database in Azure Cosmos DB is a logical container for 
    /// document collections and users.
    /// Refer to <see>http://azure.microsoft.com/documentation/articles/documentdb-resources/#databases</see> for more details on databases.
    /// </remarks>
    /// <example>
    /// The example below creates a new Database with an Id property of 'MyDatabase'.
    /// <code language="c#">
    /// <![CDATA[ 
    /// using (CosmosClient client = new CosmosClient("connection string"))
    /// {
    ///     CosmosDatabase db = await client.CreateDatabaseAsync(new Database { Id = "MyDatabase" });
    /// }
    /// ]]>
    /// </code>
    /// </example>
    /// <seealso cref="CosmosContainerProperties"/>
    public class CosmosDatabaseProperties
    {
        private string id;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDatabaseProperties"/> class for the Azure Cosmos DB service.
        /// </summary>
        public CosmosDatabaseProperties()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDatabaseProperties"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="id">The Id of the resource in the Azure Cosmos service.</param>
        public CosmosDatabaseProperties(string id)
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
        /// You can override this auto Id generation by setting the disableAutomaticIdGeneration parameter on the <see cref="Microsoft.Azure.Cosmos.DocumentClient"/> instance to true.
        /// This will prevent the SDK from generating new Ids. 
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
    }
}
