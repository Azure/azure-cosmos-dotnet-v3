//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Internal;
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
    ///     new CosmosUserDefinedFunctionSettings 
    ///     { 
    ///         Id = "calculateTax", 
    ///         Body = @"function(amt) { return amt * 0.05; }" 
    ///     });
    ///
    /// CosmosSqlQueryDefinition sqlQuery = new CosmosSqlQueryDefinition(
    ///     "SELECT VALUE udf.calculateTax(t.cost) FROM toDoActivity t where t.cost > @expensive and t.status = @status")
    ///     .UseParameter("@expensive", 9000)
    ///     .UseParameter("@status", "Done");
    ///
    /// CosmosResultSetIterator<double> setIterator = this.container.Items.CreateItemQuery<double>(
    ///     sqlQueryDefinition: sqlQuery,
    ///     partitionKey: "Done");
    ///
    /// while (setIterator.HasMoreResults)
    /// {
    ///     foreach (var tax in await setIterator.FetchNextSetAsync())
    ///     {
    ///         Console.WriteLine(tax);
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public class CosmosUserDefinedFunctionSettings : CosmosResource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.UserDefinedFunction"/> class for the Azure Cosmos service.
        /// </summary>
        public CosmosUserDefinedFunctionSettings()
        {
        }

        /// <summary>
        /// Gets or sets the body of the user defined function for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The body of the user defined function.</value>
        /// <remarks>This must be a valid JavaScript function e.g. "function (input) { return input.toLowerCase(); }".</remarks>
        [JsonProperty(PropertyName = Constants.Properties.Body)]
        public string Body
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Body);
            }
            set
            {
                base.SetValue(Constants.Properties.Body, value);
            }
        }
    }
}
