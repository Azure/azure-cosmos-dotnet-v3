//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Globalization;
    using System.Text;

    class SqlInScalarExpression : SqlScalarExpression
    {
        public bool Not
        {
            get;
            private set;
        }

        public SqlScalarExpression Expression
        {
            get;
            private set;
        }

        public SqlScalarExpression[] Items
        {
            get;
            private set;
        }

        public SqlInScalarExpression(SqlScalarExpression expression, SqlScalarExpression[] items, bool not)
            : base(SqlObjectKind.InScalarExpression)
        {
            if(expression == null)
            {
                throw new ArgumentNullException("expression");
            }

            if(items == null)
            {
                throw new ArgumentNullException("items");
            }

            if(items.Length == 0)
            {
                throw new ArgumentException("items can't be empty.");
            }

            this.Expression = expression;
            this.Items = items;
            this.Not = not;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append("(");
            this.Expression.AppendToBuilder(builder);
            if(this.Not)
            {
                builder.Append(" NOT");
            }

            builder.Append(" IN ");
            builder.Append("(");
            for (int i = 0; i < this.Items.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                this.Items[i].AppendToBuilder(builder);
            }
            builder.Append(")");
            builder.Append(")");
        }
    }
}
