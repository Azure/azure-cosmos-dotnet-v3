//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Scripts;

    /// <summary>
    /// This class provides methods for cosmos LINQ code.
    /// </summary>
    public static class CosmosLinq
    {
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
        /// // Equivalent to SELECT * FROM books b WHERE udf.toLowerCase(b.title) = 'war and peace'" 
        /// IQueryable<Book> queryable = client
        ///     .GetContainer("database", "container")
        ///     .GetItemLinqQueryable<Book>()
        ///     .Where(b => CosmosLinq.InvokeUserDefinedFunction("toLowerCase", b.Title) == "war and peace");
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
#pragma warning disable IDE0060 // Remove unused parameter
        public static object InvokeUserDefinedFunction(string udfName, params object[] arguments)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ClientResources.InvalidCallToUserDefinedFunctionProvider));
        }
    }
}