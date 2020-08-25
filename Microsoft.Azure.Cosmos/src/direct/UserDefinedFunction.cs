//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a user defined function in the Azure Cosmos DB service.
    /// </summary> 
    /// <remarks>
    /// Azure Cosmos DB supports JavaScript user defined functions (UDFs) which are stored in the database and can be used inside queries. 
    /// Refer to http://azure.microsoft.com/documentation/articles/documentdb-sql-query/#javascript-integration for how to use UDFs within queries.
    /// Refer to http://azure.microsoft.com/documentation/articles/documentdb-programming/#udf for more details about implementing UDFs in JavaScript.
    /// </remarks>
    /// <example>
    /// The following examples show how to register and use UDFs.
    /// <code language="c#">
    /// <![CDATA[
    /// await client.CreateUserDefinedFunctionAsync(collectionLink, new UserDefinedFunction { Id = "calculateTax", Body = @"function(amt) { return amt * 0.05; }" });
    /// client.CreateDocumentQuery<Book>(collectionLink, "SELECT VALUE udf.calculateTax(b.price) FROM books b");
    /// client.CreateDocumentQuery<Book>(collectionLink, new SqlQuerySpec("SELECT VALUE udf.calculateTax(b.price) FROM books b"));
    /// client.CreateDocumentQuery<Book>(collectionLink).Select(b => UserDefinedFunctionProvider.Invoke("calculateTax", b.Price));
    /// 
    /// await client.CreateUserDefinedFunctionAsync(collectionLink, new UserDefinedFunction { Id = "toLowerCase", Body = @"function(s) { return s.ToLowerCase(); }" });
    /// client.CreateDocumentQuery<Book>(collectionLink, "SELECT * FROM books b WHERE b.toLowerCase = 'war and peace'");
    /// client.CreateDocumentQuery<Book>(collectionLink, new SqlQuerySpec(
    ///     "SELECT * FROM books b WHERE b.toLowerCase = @bookNameLowerCase",
    ///     new SqlParameterCollection(new SqlParameter[] {new SqlParameter { Name = "@bookNameLowerCase", Value = "War And Peace".ToLower()
    ///  }})));
    ///  client.CreateDocumentQuery<Book>(collectionLink).Where(b => UserDefinedFunctionProvider.Invoke("toLowerCase", b.Title) == "war and peace");
    ///  ]]>
    /// </code>
    /// </example>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    class UserDefinedFunction : Resource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.UserDefinedFunction"/> class for the Azure Cosmos DB service.
        /// </summary>
        public UserDefinedFunction()
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
