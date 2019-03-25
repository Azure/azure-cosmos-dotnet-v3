//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
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
    /// using Microsoft.Azure.Cosmos.Linq;
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
    /// <seealso cref="CosmosContainerSettings"/>
    public class CosmosDatabaseSettings : CosmosResource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDatabaseSettings"/> class for the Azure Cosmos DB service.
        /// </summary>
        public CosmosDatabaseSettings()
        {

        }
    }
}
