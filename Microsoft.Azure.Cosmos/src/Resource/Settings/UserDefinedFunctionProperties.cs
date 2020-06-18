//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a user defined function in the Azure Cosmos service.
    /// </summary> 
    /// <remarks>
    /// Azure Cosmos supports JavaScript user defined functions (UDFs) which are stored in the database and can be used inside queries. 
    /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-api-sql-query#javascript-integration for how to use UDFs within queries.
    /// Refer to https://docs.microsoft.com/azure/cosmos-db/programming#udf for more details about implementing UDFs in JavaScript.
    /// </remarks>
    /// <example>
    /// The following examples show how to register and use UDFs.
    /// <code language="c#">
    /// <![CDATA[
    /// await this.container.UserDefinedFunctions.CreateUserDefinedFunctionAsync(
    ///     new UserDefinedFunctionProperties 
    ///     { 
    ///         Id = "calculateTax", 
    ///         Body = @"function(amt) { return amt * 0.05; }" 
    ///     });
    ///
    /// QueryDefinition sqlQuery = new QueryDefinition(
    ///     "SELECT VALUE udf.calculateTax(t.cost) FROM toDoActivity t where t.cost > @expensive and t.status = @status")
    ///     .WithParameter("@expensive", 9000)
    ///     .WithParameter("@status", "Done");
    ///
    /// using (FeedIterator<double> feedIterator = this.container.Items.GetItemsQueryIterator<double>(
    ///     sqlQueryDefinition: sqlQuery,
    ///     partitionKey: "Done");
    ///
    /// while (feedIterator.HasMoreResults)
    /// {
    ///     foreach (var tax in await feedIterator.ReadNextAsync())
    ///     {
    ///         Console.WriteLine(tax);
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public class UserDefinedFunctionProperties
    {
        /// <summary>
        /// Gets or sets the body of the user defined function for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The body of the user defined function.</value>
        /// <remarks>This must be a valid JavaScript function e.g. "function (input) { return input.toLowerCase(); }".</remarks>
        [JsonProperty(PropertyName = Constants.Properties.Body)]
        public string Body { get; set; }

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
        /// You can override this auto Id generation by setting the disableAutomaticIdGeneration parameter on the <see cref="Microsoft.Azure.Cosmos.DocumentClient"/> instance to true.
        /// This will prevent the SDK from generating new Ids. 
        /// </para>
        /// <para>
        /// The following characters are restricted and cannot be used in the Id property:
        ///  '/', '\\', '?', '#'
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.Id)]
        public string Id { get; set; }

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
        /// Gets the self-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link associated with the resource.</value> 
        /// <remarks>
        /// A self-link is a static addressable Uri for each resource within a database account and follows the Azure Cosmos DB resource model.
        /// E.g. a self-link for a document could be dbs/db_resourceid/colls/coll_resourceid/documents/doc_resourceid
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.SelfLink, NullValueHandling = NullValueHandling.Ignore)]
        public string SelfLink { get; private set; }
    }
}
