//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlSubqueryScalarExpression : SqlScalarExpression
    {
        public SqlQuery Query
        { 
            get; 
            private set;
        }

        public SqlSubqueryScalarExpression(SqlQuery query)
            : base(SqlObjectKind.SubqueryScalarExpression)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            this.Query = query;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append('(');
            this.Query.AppendToBuilder(builder);
            builder.Append(')');
        }
    }
}
