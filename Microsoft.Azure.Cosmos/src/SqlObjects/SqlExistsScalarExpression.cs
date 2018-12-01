//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlExistsScalarExpression : SqlScalarExpression
    {
        public SqlQuery SqlQuery { get; }

        public SqlExistsScalarExpression(SqlQuery sqlQuery)
            : base(SqlObjectKind.ExistsScalarExpression)
        {
            if (sqlQuery == null)
            {
                throw new ArgumentNullException($"{nameof(sqlQuery)} can not be null");
            }

            this.SqlQuery = sqlQuery;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append("EXISTS");
            builder.Append("(");
            this.SqlQuery.AppendToBuilder(builder);
            builder.Append(")");
        }
    }
}