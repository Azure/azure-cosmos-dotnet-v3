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
        private bool passThrough;
        
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
        public SqlQuerySpec(string queryText, SqlParameterCollection parameters)
            : this(queryText, parameters, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.SqlQuerySpec"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="queryText">The text of the database query.</param>
        /// <param name="parameters">The <see cref="T:Microsoft.Azure.Documents.SqlParameterCollection"/> instance, which represents the collection of query parameters.</param>
        /// <param name="passThrough">A Boolean value that indicates whether the query should be executed as pass-through.</param>
        public SqlQuerySpec(string queryText, SqlParameterCollection parameters, bool passThrough)
        {
            this.QueryText = queryText;
            this.parameters = parameters ?? throw new ArgumentNullException("parameters");
            this.passThrough = passThrough;
        }
        
        /// <summary>
        /// Gets or sets the text of the Azure Cosmos DB database query.
        /// </summary>
        /// <value>The text of the database query.</value>
        [DataMember(Name = "query")]
        public string QueryText { get; set; }

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
        /// Gets or sets the boolean value that indicates whether the query should be executed as pass-through..
        /// </summary>
        /// <value>A boolean value that indicates whether the query should be executed as pass-through.</value>
        [DataMember(Name = "passThrough")]        
        public bool PassThrough
        {
            get
            {
                return this.passThrough;
            }
            set
            {
                this.passThrough = value;
            }
        }
        
        /// <summary>
        /// Returns a value that indicates whether the Azure Cosmos DB database <see cref="Parameters"/> property should be serialized.
        /// </summary>
        public bool ShouldSerializeParameters()
        {
            return this.parameters.Count > 0;
        }

        /// <summary>
        /// Returns a value that indicates whether the Azure Cosmos DB database <see cref="PassThrough"/> property should be serialized.
        /// </summary>
        public bool ShouldSerializePassThrough()
        {
            return this.passThrough;
        }
    }
}
