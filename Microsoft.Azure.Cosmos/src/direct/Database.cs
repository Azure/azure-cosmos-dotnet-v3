//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
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
    /// using (DocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
    /// {
    ///     Database db = await client.CreateDatabaseAsync(new Database { Id = "MyDatabase" });
    /// }
    /// ]]>
    /// </code>
    /// </example>
    /// <example> 
    /// The example below creates a collection within this database with OfferThroughput set to 10000.
    /// <code language="c#">
    /// <![CDATA[
    /// DocumentCollection coll = await client.CreateDocumentCollectionAsync(db.SelfLink,
    ///     new DocumentCollection { Id = "MyCollection" }, 
    ///     new RequestOptions { OfferThroughput = 10000} );
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// The example below queries for a Database by Id to retrieve the SelfLink.
    /// <code language="c#">
    /// <![CDATA[
    /// using Microsoft.Azure.Documents.Linq;
    /// Database database = client.CreateDatabaseQuery().Where(d => d.Id == "MyDatabase").AsEnumerable().FirstOrDefault();
    /// string databaseLink = database.SelfLink;
    /// ]]>
    /// </code>
    /// </example>    
    /// <example>
    /// The example below deletes the database using its SelfLink property.
    /// <code language="c#">
    /// <![CDATA[
    /// await client.DeleteDatabaseAsync(db.SelfLink);
    /// ]]>
    /// </code>
    /// </example>
    /// <seealso cref="Microsoft.Azure.Documents.DocumentCollection"/>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    class Database : Resource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Database"/> class for the Azure Cosmos DB service.
        /// </summary>
        public Database()
        {

        }   

        /// <summary>
        /// Gets the self-link for collections from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The self-link for collections in the database.
        /// </value>
        /// <remarks>
        /// Every Azure Cosmos DB resource has a static, immutable, addressable URI. 
        /// For collections, this takes the form of;
        /// /dbs/db_rid/colls/ where db_rid represents the value of the database's resource id.
        /// A resource id is not the id given to a database on creation, but an internally generated immutable id.
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.CollectionsLink)]
        public string CollectionsLink
        {
            get
            {
                return this.SelfLink.TrimEnd('/') + "/" + base.GetValue<string>(Constants.Properties.CollectionsLink);
            }
        }

        /// <summary>
        /// Gets the self-link for users from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The self-link for users in the database.
        /// </value>
        /// <remarks>
        /// Every Azure Cosmos DB resource has a static, immutable, addressable URI. 
        /// For users, this takes the form of;
        /// /dbs/db_rid/users/ where db_rid represents the value of the database's resource id.
        /// A resource id is not the id given to a database on creation, but an internally generated immutable id.
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.UsersLink)]
        public string UsersLink
        {
            get
            {
                return this.SelfLink.TrimEnd('/') + "/" + base.GetValue<string>(Constants.Properties.UsersLink);
            }
        }

        /// <summary>
        /// Gets the self-link for user defined types from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The self-link for user defined types in the database.
        /// </value>
        /// <remarks>
        /// Every Azure Cosmos DB resource has a static, immutable, addressable URI. 
        /// For user defined types, this takes the form of;
        /// /dbs/db_rid/udts/ where db_rid represents the value of the database's resource id.
        /// A resource id is not the id given to a database on creation, but an internally generated immutable id.
        /// </remarks>
        internal string UserDefinedTypesLink
        {
            get
            {
                return this.SelfLink.TrimEnd('/') + "/" + Paths.UserDefinedTypesPathSegment + "/";
            }
        }

        internal override void Validate()
        {
            base.Validate();
        }
    }
}
