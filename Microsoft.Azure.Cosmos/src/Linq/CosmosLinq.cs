//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using Microsoft.Azure.Cosmos.Scripts;

    /// <summary>
    /// Helper class to invoke User Defined Functions via Linq queries in the Azure Cosmos DB service.
    /// </summary>
    public abstract class CosmosLinq
    {
        /// <summary>
        /// Returns an instance of <see cref="CosmosLinq"/> to allow access to the contained methods.
        /// </summary>
        public static readonly CosmosLinq Instance = new CosmosLinqCore();

        /// <summary>
        /// Helper method to invoke User Defined Functions via Linq queries in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="udfName">The UserDefinedFunction name</param>
        /// <param name="arguments">The arguments of the UserDefinedFunction</param>
        /// <remarks>
        /// This is a stub helper method for use within LINQ expressions. Cannot be called directly. 
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-linq-to-sql for more details about the LINQ provider.
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/stored-procedures-triggers-udfs for more details about user defined functions.
        /// </remarks>
        /// <example> 
        /// <code language="c#">
        /// <![CDATA[
        /// StoredProcedureResponse storedProcedureResponse = await client
        ///     .GetContainer("database", "container")
        ///     .Scripts
        ///     .CreateStoredProcedureAsync(
        ///         new StoredProcedureProperties()
        ///         {
        ///             Id = "toLowerCase",
        ///             Body = @"function(s) { return s.ToLowerCase(); }",
        ///         });
        ///         
        /// // Equivalent to SELECT * FROM books b WHERE udf.toLowerCase(b.title) = 'war and peace'" 
        /// IQueryable<Book> queryable = client
        ///     .GetContainer("database", "container")
        ///     .GetItemLinqQueryable<Book>()
        ///     .Where(b => CosmosLinq.Instance.InvokeUserDefinedFunction("toLowerCase", b.Title) == "war and peace");
        ///
        /// FeedIterator<Book> bookIterator = queryable.ToFeedIterator();
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     FeedResponse<Book> responseMessage = await feedIterator.ReadNextAsync();
        ///     DoSomethingWithResponse(responseMessage);
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="UserDefinedFunctionProperties"/>
        /// <returns>Placeholder for the udf result.</returns>
        public abstract object InvokeUserDefinedFunction(string udfName, params object[] arguments);
    }
}
