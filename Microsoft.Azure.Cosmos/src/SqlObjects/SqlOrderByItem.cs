//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlOrderbyItem : SqlObject
    {
        public SqlScalarExpression Expression
        {
            get;
            private set;
        }

        public bool IsDescending
        {
            get;
            private set;
        }

        public SqlOrderbyItem(
            SqlScalarExpression expression,
            bool isDescending)
            : base(SqlObjectKind.OrderByItem)
        {
            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }

            this.Expression = expression;
            this.IsDescending = isDescending;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            this.Expression.AppendToBuilder(builder);
            if (this.IsDescending)
            {
                builder.Append(" DESC");
            }
            else
            {
                builder.Append(" ASC");
            }
        }
    }
}
