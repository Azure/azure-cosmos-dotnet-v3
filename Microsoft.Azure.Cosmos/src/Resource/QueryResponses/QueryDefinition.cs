//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Defines a Cosmos SQL query
    /// </summary>
    public class QueryDefinition
    {
        private string Query { get; }
        private Dictionary<string, SqlParameter> SqlParameters { get; }

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

            this.Query = query;
            this.SqlParameters = new Dictionary<string, SqlParameter>();
        }

        internal QueryDefinition(SqlQuerySpec sqlQuery)
        {
            if (sqlQuery == null)
            {
                throw new ArgumentNullException(nameof(sqlQuery));
            }

            this.Query = sqlQuery.QueryText;
            this.SqlParameters = new Dictionary<string, SqlParameter>();
            foreach (SqlParameter sqlParameter in sqlQuery.Parameters)
            {
                this.SqlParameters.Add(sqlParameter.Name, sqlParameter);
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

            this.SqlParameters[name] = new SqlParameter(name, value);
            return this;
        }

        internal SqlQuerySpec ToSqlQuerySpec()
        {
            return new SqlQuerySpec(this.Query, new SqlParameterCollection(this.SqlParameters.Values));
        }

        /// <summary>
        /// This method gets the query text from QueryDefinition.
        /// </summary>
        /// <returns>The query text from QueryDefinition.</returns>
        /// <example>
        /// This example shows how to get query text from QueryDefinition.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// String sqlQueryText = queryDefinition.ToSqlQueryText();
        /// ]]>
        /// </code>
        /// </example>
        public string ToSqlQueryText()
        {
            return this.Query;
        }
    }
}
