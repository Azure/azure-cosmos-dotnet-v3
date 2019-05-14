//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Defines a Cosmos SQL query
    /// </summary>
    public class CosmosSqlQueryDefinition
    {
        private string Query { get; }
        private Dictionary<string, SqlParameter> SqlParameters { get; }

        /// <summary>
        /// Create a <see cref="CosmosSqlQueryDefinition"/>
        /// </summary>
        /// <param name="query">A valid Cosmos SQL query "Select * from test t"</param>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosSqlQueryDefinition query = new CosmosSqlQueryDefinition(
        ///     "select * from t where t.Account = @account")
        ///     .UseParameter("@account", "12345");
        /// ]]>
        /// </code>
        /// </example>
        public CosmosSqlQueryDefinition(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentNullException(nameof(query));
            }

            this.Query = query;
            this.SqlParameters = new Dictionary<string, SqlParameter>();
        }

        /// <summary>
        /// Add parameters to the SQL query
        /// </summary>
        /// <remarks>
        /// If the same name is added again it will replace the original value
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosSqlQueryDefinition query = new CosmosSqlQueryDefinition(
        ///     "select * from t where t.Account = @account")
        ///     .UseParameter("@account", "12345");
        /// ]]>
        /// </code>
        /// </example>
        public virtual CosmosSqlQueryDefinition UseParameter(string name, object value)
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
    }
}
