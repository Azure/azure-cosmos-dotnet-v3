//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlFromClause : SqlObject
    {
        public SqlCollectionExpression Expression
        {
            get;
            private set;
        }

        public SqlFromClause(SqlCollectionExpression expression)
            : base(SqlObjectKind.FromClause)
        {
            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }
            
            this.Expression = expression;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append("FROM ");
            this.Expression.AppendToBuilder(builder);
        }
    }
}
