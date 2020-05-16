//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Scripts;

    /// <summary>
    /// Helper class to invoke User Defined Functions via Linq queries in the Azure Cosmos DB service.
    /// </summary>
    public static class UserDefinedFunctionProvider
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
        ///  await client.CreateUserDefinedFunctionAsync(collectionLink, new UserDefinedFunction { Id = "calculateTax", Body = @"function(amt) { return amt * 0.05; }" });
        ///  var queryable = client.CreateDocumentQuery<Book>(collectionLink).Select(b => UserDefinedFunctionProvider.Invoke("calculateTax", b.Price));
        ///  
        /// // Equivalent to SELECT * FROM books b WHERE udf.toLowerCase(b.title) = 'war and peace'" 
        /// await client.CreateUserDefinedFunctionAsync(collectionLink, new UserDefinedFunction { Id = "toLowerCase", Body = @"function(s) { return s.ToLowerCase(); }" });
        /// queryable = client.CreateDocumentQuery<Book>(collectionLink).Where(b => UserDefinedFunctionProvider.Invoke("toLowerCase", b.Title) == "war and peace");
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="UserDefinedFunctionProperties"/>
        /// <returns>Placeholder for the udf result.</returns>
        public static object Invoke(string udfName, params object[] arguments)
        {
            throw new Exception(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidCallToUserDefinedFunctionProvider));
        }
    }
}
