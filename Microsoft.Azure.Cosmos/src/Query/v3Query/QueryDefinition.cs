//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Defines a Cosmos SQL query
    /// </summary>
    public class QueryDefinition
    {
        private readonly Dictionary<string, object> parameters;

        /// <summary>
        /// Create a <see cref="QueryDefinition"/>
        /// </summary>
        /// <param name="query">A valid Cosmos SQL query "Select * from test t"</param>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition query = new QueryDefinition(
        ///     "select * from t where t.Account = @account")
        ///     .WithParameter("@account", "12345");
        /// ]]>
        /// </code>
        /// </example>
        public QueryDefinition(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentNullException(nameof(query));
            }

            this.QueryText = query;
            this.parameters = new Dictionary<string, object>();
        }

        /// <summary>
        /// Gets the text of the Azure Cosmos DB SQL query.
        /// </summary>
        /// <value>The text of the SQL query.</value>
        public string QueryText { get; }

        internal QueryDefinition(SqlQuerySpec sqlQuery)
        {
            if (sqlQuery == null)
            {
                throw new ArgumentNullException(nameof(sqlQuery));
            }

            this.QueryText = sqlQuery.QueryText;
            this.parameters = new Dictionary<string, object>();
            foreach (SqlParameter sqlParameter in sqlQuery.Parameters)
            {
                this.parameters.Add(sqlParameter.Name, sqlParameter.Value);
            }
        }

        /// <summary>
        /// Add parameters to the SQL query
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="value">The value for the parameter.</param>
        /// <remarks>
        /// If the same name is added again it will replace the original value
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition query = new QueryDefinition(
        ///     "select * from t where t.Account = @account")
        ///     .WithParameter("@account", "12345");
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An instance of <see cref="QueryDefinition"/>.</returns>
        public QueryDefinition WithParameter(string name, object value)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            this.parameters[name] = value;
            return this;
        }

        /// <summary>
        /// Add parameters to the SQL query
        /// </summary>
        /// <param name="parameters">The parameters to add, in the form of a list of key-value pairs (can be a dictionary).</param>
        /// <remarks>
        /// If the same name is added again it will replace the original value
        /// </remarks>
        /// <returns>An instance of <see cref="QueryDefinition"/>.</returns>
        public QueryDefinition WithParameters(IEnumerable<KeyValuePair<string, object>> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            foreach (var keyValuePair in parameters)
            {
                this.parameters[keyValuePair.Key] = keyValuePair.Value;
            }
            
            return this;
        }

        internal SqlQuerySpec ToSqlQuerySpec()
        {
            var sqlParameters = this.parameters.Select(p => new SqlParameter(p.Key, p.Value));
            return new SqlQuerySpec(this.QueryText, new SqlParameterCollection(sqlParameters));
        }

        /// <summary>
        /// Returns the query definition's parameters.
        /// </summary>
        /// <returns>A dictionary with the names and values of the parameters.</returns>
        public IReadOnlyDictionary<string, object> Parameters => this.parameters;
    }
}
