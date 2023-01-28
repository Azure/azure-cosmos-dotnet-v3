//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents a SQL query in the Azure Cosmos DB service.
    /// </summary>
    [DataContract]
    internal sealed class SqlQuerySpec
    {
        private SqlParameterCollection parameters;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.SqlQuerySpec"/> class for the Azure Cosmos DB service.</summary>
        /// <remarks> 
        /// The default constructor initializes any fields to their default values.
        /// </remarks>
        public SqlQuerySpec()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.SqlQuerySpec"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="queryText">The text of the query.</param>
        public SqlQuerySpec(string queryText)
            : this(queryText, new SqlParameterCollection())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.SqlQuerySpec"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="queryText">The text of the database query.</param>
        /// <param name="parameters">The <see cref="T:Microsoft.Azure.Documents.SqlParameterCollection"/> instance, which represents the collection of query parameters.</param>
        /// /// <param name="optimisticDirectExecution">Boolean indicating whether query is direct (optimistic) execution.</param>
        public SqlQuerySpec(string queryText, SqlParameterCollection parameters, bool optimisticDirectExecution = false)
        {
            this.QueryText = queryText;
            this.parameters = parameters ?? throw new ArgumentNullException("parameters");
            this.OptimisticDirectExecution = optimisticDirectExecution;
        }
        
        /// <summary>
        /// Gets or sets the text of the Azure Cosmos DB database query.
        /// </summary>
        /// <value>The text of the database query.</value>
        [DataMember(Name = "query")]
        public string QueryText { get; set; }

        /// <summary>
        /// Gets or sets whether Optimistic Direct execution is desired for this query.
        /// </summary>
        /// <value>Boolean indicating whether query is direct (optimistic) execution.</value>
        [DataMember(Name = "optimisticDirectExecution")]
        public bool OptimisticDirectExecution { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="T:Microsoft.Azure.Documents.SqlParameterCollection"/> instance, which represents the collection of Azure Cosmos DB query parameters.
        /// </summary>
        /// <value>The <see cref="T:Microsoft.Azure.Documents.SqlParameterCollection"/> instance.</value>
        [DataMember(Name = "parameters")]
        public SqlParameterCollection Parameters
        {
            get
            {
                return this.parameters;
            }
            set
            {
                this.parameters = value ?? throw new ArgumentNullException("value");
            }
        }

        /// <summary>
        /// Returns a value that indicates whether the Azure Cosmos DB database <see cref="Parameters"/> property should be serialized.
        /// </summary>
        public bool ShouldSerializeParameters()
        {
            return this.parameters.Count > 0;
        }

    }
}
