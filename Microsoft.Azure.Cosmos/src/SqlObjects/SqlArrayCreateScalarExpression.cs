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
        private static readonly SqlArrayCreateScalarExpression Empty = new SqlArrayCreateScalarExpression(ImmutableArray<SqlScalarExpression>.Empty);

        private SqlArrayCreateScalarExpression(ImmutableArray<SqlScalarExpression> items)
        {
            foreach (SqlScalarExpression item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException($"{nameof(item)} must not have null items.");
                }
            }

            this.Items = items;
        }

        public ImmutableArray<SqlScalarExpression> Items { get; }

        public static SqlArrayCreateScalarExpression Create() => SqlArrayCreateScalarExpression.Empty;

        public static SqlArrayCreateScalarExpression Create(params SqlScalarExpression[] items) => new SqlArrayCreateScalarExpression(items.ToImmutableArray());

        public static SqlArrayCreateScalarExpression Create(ImmutableArray<SqlScalarExpression> items) => new SqlArrayCreateScalarExpression(items);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
