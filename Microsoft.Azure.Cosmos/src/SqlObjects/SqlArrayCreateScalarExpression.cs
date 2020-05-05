//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;

    internal sealed class SqlArrayCreateScalarExpression : SqlScalarExpression
    {
        private static readonly SqlArrayCreateScalarExpression Empty = new SqlArrayCreateScalarExpression(new ImmutableArray<SqlScalarExpression>());

        private SqlArrayCreateScalarExpression(ImmutableArray<SqlScalarExpression> items)
            : base(SqlObjectKind.ArrayCreateScalarExpression)
        {
            if (items == null)
            {
                throw new ArgumentNullException($"{nameof(items)} must not be null.");
            }

            foreach (SqlScalarExpression item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException($"{nameof(item)} must not have null items.");
                }
            }

            this.Items = items;
        }

        public ImmutableArray<SqlScalarExpression> Items
        {
            get;
        }

        public static SqlArrayCreateScalarExpression Create()
        {
            return SqlArrayCreateScalarExpression.Empty;
        }

        public static SqlArrayCreateScalarExpression Create(params SqlScalarExpression[] items)
        {
            return new SqlArrayCreateScalarExpression(items.ToImmutableArray());
        }

        public static SqlArrayCreateScalarExpression Create(IReadOnlyList<SqlScalarExpression> items)
        {
            return new SqlArrayCreateScalarExpression(items.ToImmutableArray());
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
