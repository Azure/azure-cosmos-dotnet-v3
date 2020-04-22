//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Collections.Generic;

    internal sealed class SqlInScalarExpression : SqlScalarExpression
    {
        private SqlInScalarExpression(SqlScalarExpression expression, bool not, IReadOnlyList<SqlScalarExpression> items)
            : base(SqlObjectKind.InScalarExpression)
        {
            if (items == null)
            {
                throw new ArgumentNullException("items");
            }

            if (items.Count == 0)
            {
                throw new ArgumentException("items can't be empty.");
            }

            foreach (SqlScalarExpression item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException("items can't have a null item.");
                }
            }

            this.Expression = expression ?? throw new ArgumentNullException("expression");
            this.Items = new List<SqlScalarExpression>(items);
            this.Not = not;
        }

        public bool Not
        {
            get;
        }

        public SqlScalarExpression Expression
        {
            get;
        }

        public List<SqlScalarExpression> Items
        {
            get;
        }

        public static SqlInScalarExpression Create(SqlScalarExpression expression, bool not, params SqlScalarExpression[] items)
        {
            return new SqlInScalarExpression(expression, not, items);
        }

        public static SqlInScalarExpression Create(SqlScalarExpression expression, bool not, IReadOnlyList<SqlScalarExpression> items)
        {
            return new SqlInScalarExpression(expression, not, items);
        }

        public override void Accept(SqlObjectVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }

        public override void Accept(SqlScalarExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }
    }
}
