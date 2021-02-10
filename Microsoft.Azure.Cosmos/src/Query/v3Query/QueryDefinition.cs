//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Newtonsoft.Json;

    /// <summary>
    /// Defines a Cosmos SQL query
    /// </summary>
    public class QueryDefinition
    {
        [JsonProperty(PropertyName = "parameters", NullValueHandling = NullValueHandling.Ignore, Order = 1)]
        private List<SqlParameter> parameters { get; set; }

        private ParametersListAdapter parametersAdapter;

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
        }

        /// <summary>
        /// Gets the text of the Azure Cosmos DB SQL query.
        /// </summary>
        /// <value>The text of the SQL query.</value>
        [JsonProperty(PropertyName = "query", Order = 0)]
        public string QueryText { get; }

        internal static QueryDefinition CreateFromQuerySpec(SqlQuerySpec sqlQuery)
        {
            if (sqlQuery == null)
            {
                throw new ArgumentNullException(nameof(sqlQuery));
            }

            QueryDefinition queryDefinition = new QueryDefinition(sqlQuery.QueryText);
            foreach (SqlParameter sqlParameter in sqlQuery.Parameters)
            {
                queryDefinition = queryDefinition.WithParameter(sqlParameter.Name, sqlParameter.Value);
            }

            return queryDefinition;
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

            if (this.parameters == null)
            {
                this.parameters = new List<SqlParameter>();
            }

            // Required to maintain previous contract when backed by a dictionary.
            int index = this.parameters.FindIndex(param => param.Name == name);
            if (index != -1)
            {
                this.parameters.RemoveAt(index);
            }

            this.parameters.Add(new SqlParameter(name, value));

            return this;
        }

        /// <summary>
        /// Returns the names and values of parameters in this <see cref="QueryDefinition"/>.
        /// </summary>
        /// <returns>
        /// A list of name/value tuples representing the parameters of this <see cref="QueryDefinition"/>.
        /// </returns>
        public IReadOnlyList<(string Name, object Value)> GetQueryParameters()
        {
            return this.parametersAdapter ??= new ParametersListAdapter(this);
        }

        internal SqlQuerySpec ToSqlQuerySpec()
        {
            return new SqlQuerySpec(this.QueryText, new SqlParameterCollection(this.parameters ?? (IReadOnlyList<SqlParameter>)Array.Empty<SqlParameter>()));
        }

        /// <summary>
        /// Gets the sql parameters for the class
        /// </summary>
        [JsonIgnore]
        internal IReadOnlyList<SqlParameter> Parameters => this.parameters ?? (IReadOnlyList<SqlParameter>)Array.Empty<SqlParameter>();

        private class ParametersListAdapter : IReadOnlyList<(string Name, object Value)>
        {
            private readonly QueryDefinition queryDefinition;

            public ParametersListAdapter(QueryDefinition queryDefinition)
            {
                this.queryDefinition = queryDefinition;
            }

            public (string Name, object Value) this[int index]
            {
                get
                {
                    SqlParameter param = this.queryDefinition.Parameters[index];
                    return (param.Name, param.Value);
                }
            }

            public int Count => this.queryDefinition.Parameters.Count;

            public IEnumerator<(string Name, object Value)> GetEnumerator()
            {
                foreach (SqlParameter param in this.queryDefinition.Parameters)
                {
                    yield return (param.Name, param.Value);
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }
    }
}
