﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

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
        public SqlQuerySpec(string queryText, SqlParameterCollection parameters)
            : this(queryText, parameters, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.SqlQuerySpec"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="queryText">The text of the database query.</param>
        /// <param name="parameters">The <see cref="T:Microsoft.Azure.Documents.SqlParameterCollection"/> instance, which represents the collection of query parameters.</param>
        /// <param name="resumeFilter">The <see cref="T:Microsoft.Azure.Cosmos.Query.Core.SqlQueryResumeFilter"/> instance, which represents the query resume filter.</param>
        public SqlQuerySpec(string queryText, SqlParameterCollection parameters, SqlQueryResumeFilter resumeFilter)
        {
            this.QueryText = queryText;
            this.parameters = parameters ?? throw new ArgumentNullException("parameters");
            this.ResumeFilter = resumeFilter;
        }

        /// <summary>
        /// Gets or sets the text of the Azure Cosmos DB database query.
        /// </summary>
        /// <value>The text of the database query.</value>
        [DataMember(Name = "query")]
        [JsonPropertyName("query")]
        public string QueryText { get; set; }

        /// <summary>
        /// Gets or sets the ClientQL Compatibility Level supported by the client.
        /// </summary>
        /// <value>The integer value representing the compatibility of the client.</value>
        [DataMember(Name = "clientQLCompatibilityLevel", EmitDefaultValue = false)]
        [JsonPropertyName("clientQLCompatibilityLevel")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int? ClientQLCompatibilityLevel { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="T:Microsoft.Azure.Documents.SqlParameterCollection"/> instance, which represents the collection of Azure Cosmos DB query parameters.
        /// </summary>
        /// <value>The <see cref="T:Microsoft.Azure.Documents.SqlParameterCollection"/> instance.</value>
        [DataMember(Name = "parameters")]
        [JsonPropertyName("parameters")]
        public SqlParameterCollection Parameters
        {
            get => this.parameters;
            set => this.parameters = value ?? throw new ArgumentNullException("value");
        }

        [DataMember(Name = "resumeFilter", EmitDefaultValue = false)]
        [JsonPropertyName("resumeFilter")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public SqlQueryResumeFilter ResumeFilter { get; set; }

        /// <summary>
        /// Returns a value that indicates whether the Azure Cosmos DB database <see cref="Parameters"/> property should be serialized.
        /// </summary>
        public bool ShouldSerializeParameters()
        {
            return this.parameters.Count > 0;
        }

    }
}