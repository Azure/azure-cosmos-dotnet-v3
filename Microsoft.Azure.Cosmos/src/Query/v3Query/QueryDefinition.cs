//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core;

    /// <summary>
    /// Defines a Cosmos SQL query
    /// </summary>
    public class QueryDefinition : IEquatable<QueryDefinition>
    {
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

            this.QueryText = query;
            this.SqlParameters = new Dictionary<string, SqlParameter>();
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

        /// <summary>
        /// Checks for equality of two QueryDefinitions ignoring order of sqlParameters
        /// </summary>
        /// <param name="other"></param>
        /// <returns>Boolean representing the equality</returns>
        public bool Equals(QueryDefinition other)
        {
            if (Object.ReferenceEquals(other, this))
            {
                return true;
            }

            if (other is null)
            {
                return false;
            }

            if (this.SqlParameters.Count != other.SqlParameters.Count)
            {
                return false;
            }

            if (!string.Equals(this.QueryText, other.QueryText, StringComparison.Ordinal))
            {
                return false;
            }

            return ParameterEquals(this.SqlParameters, other.SqlParameters);
        }

        /// <summary>
        /// Caculates a hashcode of QueryDefinition, ignoring order of sqlParameters.
        /// </summary>
        /// <returns>Hash code of the object.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                foreach (KeyValuePair<string, SqlParameter> queryParameter in this.SqlParameters)
                {
                    // xor is associative so we generate the same hash code if order of parameters is different
                    hashCode ^= queryParameter.Value.GetHashCode();
                }

                hashCode = (hashCode * 16777619) + this.QueryText.GetHashCode();
                return hashCode;
            }
        }

        private static bool ParameterEquals(Dictionary<string, SqlParameter> parameters, Dictionary<string, SqlParameter> otherParameters)
        {
            if (parameters.Count != otherParameters.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, SqlParameter> queryParameter in parameters)
            {
                if (!otherParameters.TryGetValue(queryParameter.Key, out SqlParameter value) ||
                    !value.Name.Equals(queryParameter.Value.Name) ||
                    (value.Value != null && !value.Value.Equals(queryParameter.Value.Value)))
                {
                    return false;
                }
            }

            return true;
        }

        internal SqlQuerySpec ToSqlQuerySpec()
        {
            return new SqlQuerySpec(this.QueryText, new SqlParameterCollection(this.SqlParameters.Values));
        }
    }
}
